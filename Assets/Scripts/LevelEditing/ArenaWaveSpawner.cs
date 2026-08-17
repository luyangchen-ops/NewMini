using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scene-authored enemy waves for one combat zone. Each wave can place several enemy
/// prefabs across explicit spawn-point transforms, so all placement remains editable in Unity.
/// </summary>
[DisallowMultipleComponent]
public sealed class ArenaWaveSpawner : MonoBehaviour
{
    [Serializable]
    public sealed class SpawnEntry
    {
        [Tooltip("Enemy category used by level-editing tools and later prefab replacement.")]
        public EnemySpawner.EnemyType enemyType;
        [Tooltip("Enemy prefab with an EnemyAgent component.")]
        public GameObject enemyPrefab;
        [Tooltip("Enemy positions in this entry. The count is distributed across these points in order.")]
        public Transform[] spawnPoints;
        [Min(1)] public int count = 1;
    }

    [Serializable]
    public sealed class WaveDefinition
    {
        public string waveName = "Wave";
        [Min(0f)] public float delayBeforeWave;
        public SpawnEntry[] spawns;
    }

    [Header("Waves")]
    [SerializeField] private WaveDefinition[] waves;
    [SerializeField] private Transform spawnedEnemyParent;
    [Tooltip("Alive-enemy threshold used to start the next wave. Zero waits for a full clear. The final wave always waits for every enemy to die.")]
    [SerializeField, Min(0)] private int remainingEnemiesToStartNextWave;
    [Header("Events")]
    [SerializeField] private UnityEvent<int> onWaveStarted;
    [SerializeField] private UnityEvent<int> onWaveCleared;
    [SerializeField] private UnityEvent onAllWavesCleared;

    public bool IsRunning { get; private set; }
    public bool HasCompletedAllWaves { get; private set; }
    public int CurrentWaveIndex { get; private set; } = -1;
    public int CurrentAliveCount => CountAliveEnemies();
    public UnityEvent<int> WaveStartedEvent => onWaveStarted;
    public UnityEvent AllWavesClearedEvent => onAllWavesCleared;

    private readonly List<GameObject> spawnedEnemies = new();
    private Coroutine waveRoutine;
    private bool spawnEnemiesDisabled;

    public void BeginWaves()
    {
        BeginWaves(false);
    }

    public void BeginWaves(bool keepSpawnedEnemiesDisabled)
    {
        if (IsRunning || HasCompletedAllWaves) return;
        spawnEnemiesDisabled = keepSpawnedEnemiesDisabled;
        if (waves == null || waves.Length == 0)
        {
            HasCompletedAllWaves = true;
            onAllWavesCleared?.Invoke();
            return;
        }

        IsRunning = true;
        waveRoutine = StartCoroutine(RunWaves());
    }

    [ContextMenu("Reset Waves")]
    public void ResetWaves() => ResetWaves(clearSpawnedEnemies: true);

    /// <summary>Stops the current wave and, when retrying, removes every enemy it spawned.</summary>
    public void ResetWaves(bool clearSpawnedEnemies)
    {
        if (waveRoutine != null) StopCoroutine(waveRoutine);
        waveRoutine = null;
        IsRunning = false;
        HasCompletedAllWaves = false;
        CurrentWaveIndex = -1;
        spawnEnemiesDisabled = false;

        if (!clearSpawnedEnemies)
        {
            spawnedEnemies.RemoveAll(enemy => enemy == null);
            return;
        }

        foreach (GameObject enemy in spawnedEnemies)
            if (enemy != null) Destroy(enemy);
        spawnedEnemies.Clear();
    }

    private IEnumerator RunWaves()
    {
        for (int i = 0; i < waves.Length; i++)
        {
            CurrentWaveIndex = i;
            WaveDefinition wave = waves[i];
            if (wave != null && wave.delayBeforeWave > 0f)
                yield return new WaitForSeconds(wave.delayBeforeWave);

            SpawnWave(wave);
            onWaveStarted?.Invoke(i);
            bool hasNextWave = i < waves.Length - 1;
            int advanceThreshold = hasNextWave ? remainingEnemiesToStartNextWave : 0;
            yield return new WaitUntil(() => CountAliveEnemies() <= advanceThreshold);
            onWaveCleared?.Invoke(i);
        }

        IsRunning = false;
        HasCompletedAllWaves = true;
        waveRoutine = null;
        onAllWavesCleared?.Invoke();
    }

    private void SpawnWave(WaveDefinition wave)
    {
        if (wave?.spawns == null) return;
        foreach (SpawnEntry entry in wave.spawns)
        {
            if (entry == null || entry.enemyPrefab == null || entry.spawnPoints == null || entry.spawnPoints.Length == 0) continue;
            for (int index = 0; index < entry.count; index++)
            {
                Transform point = entry.spawnPoints[index % entry.spawnPoints.Length];
                if (point == null) continue;
                GameObject instance = Instantiate(entry.enemyPrefab, point.position, point.rotation, spawnedEnemyParent);
                if (spawnEnemiesDisabled)
                {
                    EnemyAgent enemy = instance.GetComponent<EnemyAgent>();
                    if (enemy != null) enemy.enabled = false;
                }
                spawnedEnemies.Add(instance);
            }
        }
    }

    public void SetSpawnedEnemiesEnabled(bool enabled)
    {
        foreach (GameObject instance in spawnedEnemies)
        {
            if (instance == null) continue;
            EnemyAgent enemy = instance.GetComponent<EnemyAgent>();
            if (enemy != null) enemy.enabled = enabled;
        }
        if (enabled) spawnEnemiesDisabled = false;
    }

    private int CountAliveEnemies()
    {
        int count = 0;
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = spawnedEnemies[i];
            if (enemy == null)
            {
                spawnedEnemies.RemoveAt(i);
                continue;
            }
            if (!enemy.activeInHierarchy) continue;

            // A defeated enemy can remain active while its death animation plays.
            // Treat the lethal hit as the wave clear point so the configured next
            // wave is not held up by the old wave's visual cleanup.
            EnemyAgent enemyAgent = enemy.GetComponent<EnemyAgent>();
            if (enemyAgent == null || !enemyAgent.IsDead) count++;
        }
        return count;
    }
}
