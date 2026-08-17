#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Play-mode-only level skip shortcut. This component is never compiled into player builds.</summary>
public sealed class EditorLevelSkipHotkey : MonoBehaviour
{
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
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null
            && (keyboard.backquoteKey.wasPressedThisFrame || keyboard.f6Key.wasPressedThisFrame))
            SkipCurrentArena();
    }

    private static void SkipCurrentArena()
    {
        PlayerCharacterController player = FindAnyObjectByType<PlayerCharacterController>();
        if (player == null) return;

        RespawnPointManager respawnManager = RespawnPointManager.Instance;
        if (respawnManager == null || !respawnManager.TryGetNextPoint(out RespawnPoint nextCheckpoint))
        {
            Debug.Log("[Editor Skip] No upcoming checkpoint was found in the respawn sequence.");
            return;
        }

        System.Collections.Generic.List<ArenaCombatZone> zonesToClear = new();
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

        if (!respawnManager.TeleportPlayerToNextPoint(out RespawnPoint teleportedPoint))
        {
            Debug.LogWarning("[Editor Skip] The next checkpoint became unavailable before teleporting.");
            return;
        }

        Debug.Log(
            $"[Editor Skip] Cleared current arena and teleported to sequence point " +
            $"{teleportedPoint.SequenceIndex + 1:00}: {teleportedPoint.PointId}.");
    }
}
#endif
