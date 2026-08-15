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
    [SerializeField, Min(0f)] private float clearCheckDelay = .15f;

    [Header("Boundary")]
    [Tooltip("All gate objects that close while this combat zone is active.")]
    [SerializeField] private ArenaBoundaryGate[] boundaryGates;

    [Header("Enemy Waves")]
    [Tooltip("Wave spawners that run when this zone locks. The zone only unlocks after all of them complete.")]
    [SerializeField] private ArenaWaveSpawner[] waveSpawners;

    [Header("Events")]
    [SerializeField] private UnityEvent onZoneLocked;
    [SerializeField] private UnityEvent onZoneCleared;
    [SerializeField] private EnemyCountEvent onEnemyCountChanged;

    public bool IsActive { get; private set; }
    public bool IsCleared { get; private set; }
    public int RemainingEnemyCount { get; private set; }
    public Collider2D ZoneCollider => zoneCollider;

    private float nextClearCheckTime;
    private int previousEnemyCount = -1;

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
        if (!IsActive || IsCleared || Time.time < nextClearCheckTime) return;
        nextClearCheckTime = Time.time + clearCheckDelay;
        RefreshEnemyCount();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!activateWhenPlayerEnters || IsActive || (oneShot && IsCleared)) return;
        if (other.GetComponentInParent<PlayerCharacterController>() != null) ActivateZone();
    }

    [ContextMenu("Activate Zone")]
    public void ActivateZone()
    {
        if (IsActive || (oneShot && IsCleared)) return;
        IsActive = true;
        SetGatesLocked(true);
        onZoneLocked?.Invoke();
        if (waveSpawners != null)
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
            if (enemy != null && IsInsideZone(enemy.transform.position)) count++;
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
        IsActive = false;
        IsCleared = false;
        RemainingEnemyCount = 0;
        previousEnemyCount = -1;
        if (waveSpawners != null)
        {
            foreach (ArenaWaveSpawner spawner in waveSpawners)
                if (spawner != null) spawner.ResetWaves();
        }
        SetGatesLocked(lockOnStart);
    }

    private bool IsInsideZone(Vector3 worldPosition)
    {
        Vector2 closest = zoneCollider.ClosestPoint(worldPosition);
        return ((Vector2)worldPosition - closest).sqrMagnitude < .0001f;
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
