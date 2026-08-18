using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Locks its assigned boundary once the player enters and unlocks it after every enemy
/// whose root position is inside the zone has been cleared.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public sealed class ArenaCombatZone : MonoBehaviour
{
    [Serializable]
    public sealed class EnemyCountEvent : UnityEvent<int> { }

    [Header("Zone")]
    [SerializeField] private Collider2D zoneCollider;
    [SerializeField] private bool activateWhenPlayerEnters = true;
    [SerializeField] private bool lockOnStart;
    [SerializeField] private bool oneShot = true;
    [Tooltip("When disabled, death retries leave this encounter untouched. Use this for Boss arenas with their own encounter state.")]
    [SerializeField] private bool resetOnCheckpointRetry = true;
    [SerializeField, Min(0f)] private float clearCheckDelay = .15f;
    [Tooltip("Distance the player must move inside the zone after crossing its boundary before its gates can close.")]
    [SerializeField, Min(0f)] private float entryConfirmationDistance = .6f;

    [Header("Boundary")]
    [Tooltip("All gate objects that close while this combat zone is active.")]
    [SerializeField] private ArenaBoundaryGate[] boundaryGates;

    [Header("Enemy Waves")]
    [Tooltip("Wave spawners that run when this zone locks. The zone only unlocks after all of them complete.")]
    [SerializeField] private ArenaWaveSpawner[] waveSpawners;
    [SerializeField] private bool deferWavesUntilRequested;

    [Header("Events")]
    [SerializeField] private UnityEvent onZoneLocked;
    [SerializeField] private UnityEvent onZoneCleared;
    [SerializeField] private EnemyCountEvent onEnemyCountChanged;

    public bool IsActive { get; private set; }
    public bool IsCleared { get; private set; }
    public int RemainingEnemyCount { get; private set; }
    public Collider2D ZoneCollider => zoneCollider;
    public UnityEvent ZoneClearedEvent => onZoneCleared;
    public UnityEvent ZoneLockedEvent => onZoneLocked;
    public event Action ZoneReset;
    public void SetResetOnCheckpointRetry(bool enabled) => resetOnCheckpointRetry = enabled;

#if UNITY_EDITOR
    /// <summary>Editor-only shortcut used by play-mode level skipping.</summary>
    public void EditorForceClear()
    {
        if (!deferWavesUntilRequested && waveSpawners != null)
        {
            foreach (ArenaWaveSpawner spawner in waveSpawners)
                if (spawner != null) spawner.ResetWaves(clearSpawnedEnemies: true);
        }

        IsCleared = true;
        IsActive = false;
        RemainingEnemyCount = 0;
        previousEnemyCount = 0;
        awaitingEntryConfirmation = false;
        pendingEnteringPlayer = null;
        SetGatesLocked(false);
        onZoneCleared?.Invoke();
    }
#endif

    /// <summary>Starts an authored deferred encounter after its entrance presentation ends.</summary>
    public void BeginDeferredWaves(bool keepSpawnedEnemiesDisabled = false)
    {
        if (!IsActive || IsCleared || waveSpawners == null) return;
        foreach (ArenaWaveSpawner spawner in waveSpawners)
            if (spawner != null) spawner.BeginWaves(keepSpawnedEnemiesDisabled);
    }

    public void SetWavesDeferred(bool deferred) => deferWavesUntilRequested = deferred;

    public void ResetDeferredWaves()
    {
        if (waveSpawners == null) return;
        foreach (ArenaWaveSpawner spawner in waveSpawners)
            if (spawner != null) spawner.ResetWaves(clearSpawnedEnemies: true);
    }

    public void SetDeferredWaveEnemiesEnabled(bool enabled)
    {
        if (waveSpawners == null) return;
        foreach (ArenaWaveSpawner spawner in waveSpawners)
            if (spawner != null) spawner.SetSpawnedEnemiesEnabled(enabled);
    }

    public bool ContainsPosition(Vector3 worldPosition) => IsInsideZone(worldPosition);

    private float nextClearCheckTime;
    private int previousEnemyCount = -1;
    private PlayerCharacterController pendingEnteringPlayer;
    private bool awaitingEntryConfirmation;

    /// <summary>
    /// Restores every uncleared arena to its pre-entry state for a checkpoint retry.
    /// An arena that was active when the player died is immediately restarted only
    /// when the player respawns inside its trigger. Checkpoints outside an arena keep
    /// its entrance open until the player enters again.
    /// Cleared arenas deliberately remain open and completed.
    /// </summary>
    public static void ResetIncompleteZonesForRetry(Vector3 respawnPosition)
    {
        foreach (ArenaCombatZone zone in FindObjectsByType<ArenaCombatZone>(FindObjectsInactive.Exclude))
        {
            if (zone == null || zone.IsCleared || !zone.resetOnCheckpointRetry) continue;

            bool wasActive = zone.IsActive;
            zone.ResetZone();
            if (!wasActive || !zone.ContainsPosition(respawnPosition)) continue;

            zone.ActivateZone();
        }
    }

    private void Reset()
    {
        zoneCollider = GetComponent<Collider2D>();
        if (zoneCollider != null) zoneCollider.isTrigger = true;
    }

    private void Awake()
    {
        zoneCollider ??= GetComponent<Collider2D>();
        if (zoneCollider == null)
        {
            Debug.LogError($"{name} needs a Collider2D to define its combat area.", this);
            enabled = false;
            return;
        }

        if (lockOnStart) ActivateZone();
        else SetGatesLocked(false);
    }

    private void Update()
    {
        if (awaitingEntryConfirmation)
        {
            if (pendingEnteringPlayer == null)
            {
                awaitingEntryConfirmation = false;
            }
            else if (HasPlayerPassedBoundary(pendingEnteringPlayer.transform.position))
            {
                awaitingEntryConfirmation = false;
                pendingEnteringPlayer = null;
                ActivateZone();
            }
        }

        if (!IsActive || IsCleared || Time.time < nextClearCheckTime) return;
        nextClearCheckTime = Time.time + clearCheckDelay;
        RefreshEnemyCount();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!activateWhenPlayerEnters || IsActive || (oneShot && IsCleared)) return;
        PlayerCharacterController enteringPlayer = other.GetComponentInParent<PlayerCharacterController>();
        if (enteringPlayer == null) return;

        // The zone trigger starts at the same boundary as its gates. Wait until the
        // player's pivot is clearly inside so the entry gate cannot close on them.
        pendingEnteringPlayer = enteringPlayer;
        awaitingEntryConfirmation = !HasPlayerPassedBoundary(enteringPlayer.transform.position);
        if (!awaitingEntryConfirmation) ActivateZone();
    }

    [ContextMenu("Activate Zone")]
    public void ActivateZone()
    {
        if (IsActive || (oneShot && IsCleared)) return;
        IsActive = true;
        SetGatesLocked(true);
        onZoneLocked?.Invoke();
        if (!deferWavesUntilRequested && waveSpawners != null)
        {
            foreach (ArenaWaveSpawner spawner in waveSpawners)
                if (spawner != null) spawner.BeginWaves();
        }
        RefreshEnemyCount();
    }

    [ContextMenu("Refresh Enemy Count")]
    public void RefreshEnemyCount()
    {
        if (!IsActive || IsCleared) return;

        int count = 0;
        foreach (EnemyAgent enemy in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Exclude))
        {
            if (enemy != null && !enemy.IsDead && IsInsideZone(enemy.transform.position)) count++;
        }

        RemainingEnemyCount = count;
        if (previousEnemyCount != count)
        {
            previousEnemyCount = count;
            onEnemyCountChanged?.Invoke(count);
        }

        if (count == 0 && AreAllWavesCompleted()) ClearZone();
    }

    [ContextMenu("Clear Zone")]
    public void ClearZone()
    {
        if (!IsActive || IsCleared) return;
        IsCleared = true;
        IsActive = false;
        RemainingEnemyCount = 0;
        SetGatesLocked(false);
        onZoneCleared?.Invoke();
    }

    [ContextMenu("Reset Zone")]
    public void ResetZone()
    {
        ZoneReset?.Invoke();
        IsActive = false;
        IsCleared = false;
        RemainingEnemyCount = 0;
        previousEnemyCount = -1;
        pendingEnteringPlayer = null;
        awaitingEntryConfirmation = false;
        if (waveSpawners != null)
        {
            foreach (ArenaWaveSpawner spawner in waveSpawners)
                if (spawner != null) spawner.ResetWaves(clearSpawnedEnemies: true);
        }
        SetGatesLocked(lockOnStart);
    }

    private bool IsInsideZone(Vector3 worldPosition)
    {
        Vector2 closest = zoneCollider.ClosestPoint(worldPosition);
        return ((Vector2)worldPosition - closest).sqrMagnitude < .0001f;
    }

    private bool HasPlayerPassedBoundary(Vector3 worldPosition)
    {
        Bounds bounds = zoneCollider.bounds;
        float maxInset = Mathf.Min(bounds.extents.x, bounds.extents.y) - .01f;
        float inset = Mathf.Clamp(entryConfirmationDistance, 0f, Mathf.Max(0f, maxInset));
        return worldPosition.x >= bounds.min.x + inset
            && worldPosition.x <= bounds.max.x - inset
            && worldPosition.y >= bounds.min.y + inset
            && worldPosition.y <= bounds.max.y - inset;
    }

    private void SetGatesLocked(bool locked)
    {
        if (boundaryGates == null) return;
        foreach (ArenaBoundaryGate gate in boundaryGates)
            if (gate != null) gate.SetLocked(locked);
    }

    private bool AreAllWavesCompleted()
    {
        if (waveSpawners == null) return true;
        foreach (ArenaWaveSpawner spawner in waveSpawners)
        {
            if (spawner != null && !spawner.HasCompletedAllWaves) return false;
        }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D area = zoneCollider != null ? zoneCollider : GetComponent<Collider2D>();
        if (area == null) return;
        Gizmos.color = IsActive ? new Color(1f, .3f, .1f, .35f) : new Color(.15f, .75f, 1f, .25f);
        Gizmos.DrawCube(area.bounds.center, area.bounds.size);
    }
}
