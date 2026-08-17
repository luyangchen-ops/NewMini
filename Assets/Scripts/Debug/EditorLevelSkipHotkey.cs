#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Play-mode-only level skip shortcut. This component is never compiled into player builds.</summary>
public sealed class EditorLevelSkipHotkey : MonoBehaviour
{
    private const float CheckpointApproachPadding = 1f;
    private static readonly HashSet<RespawnPoint> PassedCheckpoints = new();
    private static ulong checkpointSceneHandle = ulong.MaxValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<EditorLevelSkipHotkey>() != null) return;

        GameObject host = new("Editor_LevelSkipHotkey");
        host.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(host);
        host.AddComponent<EditorLevelSkipHotkey>();
    }

    private void Update()
    {
        if (Keyboard.current?.backquoteKey.wasPressedThisFrame == true) SkipCurrentArena();
    }

    private static void SkipCurrentArena()
    {
        PlayerCharacterController player = FindAnyObjectByType<PlayerCharacterController>();
        if (player == null) return;

        RememberCheckpointsForActiveScene();

        List<ArenaCombatZone> zonesToClear = new();
        foreach (ArenaCombatZone zone in FindObjectsByType<ArenaCombatZone>(FindObjectsInactive.Exclude))
        {
            if (zone != null && (zone.IsActive || zone.ContainsPosition(player.transform.position)))
                zonesToClear.Add(zone);
        }

        foreach (ArenaCombatZone zone in zonesToClear)
        {
            foreach (EnemyAgent enemy in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Exclude))
            {
                if (enemy != null && zone.ContainsPosition(enemy.transform.position)) Destroy(enemy.gameObject);
            }
            zone.EditorForceClear();
        }

        RespawnPoint nextCheckpoint = FindNearestUpcomingCheckpoint(player.transform.position);
        if (nextCheckpoint == null)
        {
            Debug.Log("[Editor Skip] No upcoming checkpoint was found.");
            return;
        }

        Vector3 destination = GetApproachPosition(player.transform.position, nextCheckpoint);
        player.RespawnAt(destination);
        // The player intentionally stops just before this trigger so they can enter
        // the following arena normally. Advance the logical checkpoint now; otherwise
        // the next shortcut can choose this same checkpoint again.
        nextCheckpoint.Activate();
        PassedCheckpoints.Add(nextCheckpoint);
        Debug.Log($"[Editor Skip] Cleared current arena and moved before {nextCheckpoint.PointId}.");
    }

    private static RespawnPoint FindNearestUpcomingCheckpoint(Vector3 playerPosition)
    {
        RespawnPoint nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;
        foreach (RespawnPoint point in FindObjectsByType<RespawnPoint>(FindObjectsInactive.Exclude))
        {
            if (point == null || PassedCheckpoints.Contains(point)) continue;
            float distanceSquared = (point.RespawnPosition - playerPosition).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearest = point;
                nearestDistanceSquared = distanceSquared;
            }
        }
        return nearest;
    }

    private static void RememberCheckpointsForActiveScene()
    {
        ulong activeSceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle.GetRawData();
        if (checkpointSceneHandle != activeSceneHandle)
        {
            PassedCheckpoints.Clear();
            checkpointSceneHandle = activeSceneHandle;
        }

        if (RespawnPoint.Active != null) PassedCheckpoints.Add(RespawnPoint.Active);
    }

    private static Vector3 GetApproachPosition(Vector3 playerPosition, RespawnPoint checkpoint)
    {
        Vector3 checkpointPosition = checkpoint.RespawnPosition;
        Vector3 direction = checkpointPosition - playerPosition;
        direction.z = 0f;
        if (direction.sqrMagnitude < .0001f) direction = Vector3.down;
        direction.Normalize();

        Vector3 approachOrigin = checkpointPosition;
        float triggerExtent = .5f;
        if (checkpoint.ActivationTrigger != null)
        {
            Bounds bounds = checkpoint.ActivationTrigger.bounds;
            // A checkpoint collider may be offset from its transform. Base the
            // approach on its world-space bounds so the destination remains
            // outside the trigger and cannot activate it through OnTriggerEnter2D.
            approachOrigin = bounds.center;
            triggerExtent = Mathf.Abs(direction.x) * bounds.extents.x
                + Mathf.Abs(direction.y) * bounds.extents.y;
        }

        Vector3 destination = approachOrigin - direction * (triggerExtent + CheckpointApproachPadding);
        destination.z = playerPosition.z;
        return destination;
    }
}
#endif
