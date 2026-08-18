using System;
using UnityEngine;

/// <summary>Owns the scene-authored, ordered sequence of player respawn points.</summary>
[AddComponentMenu("Level/Respawn Point Manager")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class RespawnPointManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private PlayerCharacterController player;
    [Tooltip("Ordered from the beginning of the level to the end. Every respawn and debug teleport uses this sequence.")]
    [SerializeField] private RespawnPoint[] respawnPoints = Array.Empty<RespawnPoint>();
    [SerializeField, Min(0)] private int startingPointIndex;

    public static RespawnPointManager Instance { get; private set; }

    /// <summary>Raised after the player and retryable arenas have been restored.</summary>
    public event Action PlayerRespawned;

    public int PointCount => respawnPoints?.Length ?? 0;
    public int CurrentPointIndex { get; private set; } = -1;
    public RespawnPoint CurrentPoint => IsValidIndex(CurrentPointIndex) ? respawnPoints[CurrentPointIndex] : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one RespawnPointManager may be active in a scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        player ??= FindAnyObjectByType<PlayerCharacterController>();
        BindSequence();

        int initialIndex = FindInitialPointIndex();
        if (initialIndex >= 0) SetCurrentPoint(initialIndex, true);
        else Debug.LogError("RespawnPointManager has no valid respawn points.", this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnValidate()
    {
        int maximumIndex = Mathf.Max(0, PointCount - 1);
        startingPointIndex = Mathf.Clamp(startingPointIndex, 0, maximumIndex);
    }

    public bool Activate(RespawnPoint point)
    {
        int index = IndexOf(point);
        if (index < 0)
        {
            Debug.LogError($"Respawn point '{point?.name}' is not in the manager's ordered sequence.", point);
            return false;
        }

        // Re-entering an earlier trigger must not move the player's retry point backwards.
        if (index < CurrentPointIndex) return false;

        return SetCurrentPoint(index, false);
    }

    public bool TryGetCurrentPosition(out Vector3 position)
    {
        if (CurrentPoint == null)
        {
            position = default;
            return false;
        }

        position = CurrentPoint.RespawnPosition;
        return true;
    }

    /// <summary>Respawns the player at the latest activated point for death and manual retry flows.</summary>
    public bool RespawnPlayerAtCurrentPoint()
    {
        if (!TryGetCurrentPosition(out Vector3 position)) return false;
        player ??= FindAnyObjectByType<PlayerCharacterController>();
        if (player == null)
        {
            Debug.LogError("RespawnPointManager cannot find a PlayerCharacterController.", this);
            return false;
        }

        player.RespawnAt(position);
        ArenaCombatZone.ResetIncompleteZonesForRetry(position);
        PlayerRespawned?.Invoke();
        return true;
    }

    public bool TryGetNextPoint(out RespawnPoint point)
    {
        int nextIndex = CurrentPointIndex + 1;
        if (!IsValidIndex(nextIndex))
        {
            point = null;
            return false;
        }

        point = respawnPoints[nextIndex];
        return point != null;
    }

    /// <summary>Moves to and activates the next point in the authored sequence.</summary>
    public bool TeleportPlayerToNextPoint(out RespawnPoint point)
    {
        if (!TryGetNextPoint(out point)) return false;
        return TeleportPlayerToPoint(CurrentPointIndex + 1, true);
    }

    /// <summary>Moves the player to a serialized point and optionally makes it the current respawn point.</summary>
    public bool TeleportPlayerToPoint(int index, bool activatePoint = true)
    {
        if (!IsValidIndex(index) || respawnPoints[index] == null) return false;
        player ??= FindAnyObjectByType<PlayerCharacterController>();
        if (player == null) return false;

        player.RespawnAt(respawnPoints[index].RespawnPosition);
        if (activatePoint) SetCurrentPoint(index, false);
        return true;
    }

    private void BindSequence()
    {
        for (int i = 0; i < PointCount; i++)
        {
            RespawnPoint point = respawnPoints[i];
            if (point == null)
            {
                Debug.LogError($"Respawn point sequence contains an empty entry at index {i}.", this);
                continue;
            }

            if (IndexOf(point, i + 1) >= 0)
                Debug.LogError($"Respawn point '{point.name}' appears more than once in the sequence.", this);
            point.Bind(this, i);
        }
    }

    private int FindInitialPointIndex()
    {
        for (int i = 0; i < PointCount; i++)
            if (respawnPoints[i] != null && respawnPoints[i].ActiveOnLevelStart)
                return i;

        return IsValidIndex(startingPointIndex) && respawnPoints[startingPointIndex] != null
            ? startingPointIndex
            : -1;
    }

    private bool SetCurrentPoint(int index, bool forceNotification)
    {
        if (!IsValidIndex(index) || respawnPoints[index] == null) return false;
        if (!forceNotification && CurrentPointIndex == index) return false;

        CurrentPointIndex = index;
        respawnPoints[index].NotifyActivated();
        return true;
    }

    private int IndexOf(RespawnPoint point, int startIndex = 0)
    {
        for (int i = Mathf.Max(0, startIndex); i < PointCount; i++)
            if (respawnPoints[i] == point)
                return i;
        return -1;
    }

    private bool IsValidIndex(int index) => index >= 0 && index < PointCount;
}
