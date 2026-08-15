using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在矩形区域内随机生成敌人，或在指定 Transform 处生成指定敌人。
/// 将此组件挂到场景中的空物体，并在 Inspector 中配置敌人 Prefab。
/// </summary>
public sealed class EnemySpawner : MonoBehaviour
{
    public enum EnemyType
    {
        [InspectorName("刀兵")]
        Swordsman,
        [InspectorName("弓兵")]
        Archer,
        [InspectorName("盾兵")]
        ShieldBearer,
        [InspectorName("长矛兵")]
        Spearman
    }

    [Serializable]
    public sealed class EnemyPrefabEntry
    {
        public EnemyType type;
        [Tooltip("可带有 EnemyAgent 的敌人预制体。")]
        public GameObject prefab;
        [Min(0)] public int randomWeight = 1;
    }

    [Serializable]
    public sealed class FixedSpawnEntry
    {
        public EnemyType type;
        [Tooltip("敌人的生成位置与朝向。")]
        public Transform spawnPoint;
    }

    [Header("Enemy Prefabs")]
    [SerializeField] private EnemyPrefabEntry[] enemyPrefabs = new EnemyPrefabEntry[4];

    [Header("Random Area Spawn")]
    [SerializeField] private bool spawnRandomOnStart;
    [SerializeField, Min(0)] private int randomSpawnCount;
    [Tooltip("区域以本物体的位置为中心。")]
    [SerializeField] private Vector2 randomAreaSize = new Vector2(10f, 6f);
    [SerializeField] private Transform spawnedEnemyParent;

    [Header("Runtime")]
    [Tooltip("此生成器生成且当前仍在场上的敌人数量。")]
    [SerializeField] private int currentEnemyCount;

    [Header("Fixed Spawn")]
    [SerializeField] private bool spawnFixedOnStart;
    [SerializeField] private FixedSpawnEntry[] fixedSpawns;

    private readonly Dictionary<EnemyType, GameObject> prefabByType = new();
    private readonly List<GameObject> spawnedEnemies = new();

    /// <summary>此生成器生成且当前仍启用在场上的敌人数量。</summary>
    public int CurrentEnemyCount => currentEnemyCount;

    private void Awake()
    {
        CachePrefabs();
    }

    private void Start()
    {
        if (spawnRandomOnStart)
        {
            SpawnRandomEnemies(randomSpawnCount);
        }

        if (spawnFixedOnStart)
        {
            SpawnFixedEnemies();
        }
    }

    private void Update()
    {
        RefreshCurrentEnemyCount();
    }

    /// <summary>在随机区域内生成指定数量的随机敌人。</summary>
    public void SpawnRandomEnemies(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = GetRandomPrefab();
            if (prefab == null)
            {
                Debug.LogWarning($"{name}: 未配置可用于随机生成的敌人 Prefab。", this);
                return;
            }

            Spawn(prefab, GetRandomPosition(), Quaternion.identity);
        }
    }

    /// <summary>在 Inspector 配置的固定点，生成对应的敌人。</summary>
    public void SpawnFixedEnemies()
    {
        if (fixedSpawns == null)
        {
            return;
        }

        foreach (FixedSpawnEntry entry in fixedSpawns)
        {
            if (entry == null || entry.spawnPoint == null)
            {
                continue;
            }

            if (!prefabByType.TryGetValue(entry.type, out GameObject prefab) || prefab == null)
            {
                Debug.LogWarning($"{name}: 未配置 {entry.type} 的敌人 Prefab。", this);
                continue;
            }

            Spawn(prefab, entry.spawnPoint.position, entry.spawnPoint.rotation);
        }
    }

    /// <summary>供按钮事件、关卡脚本或 UnityEvent 调用：生成一个指定类型的敌人。</summary>
    public GameObject SpawnEnemy(EnemyType type, Transform spawnPoint)
    {
        if (spawnPoint == null || !prefabByType.TryGetValue(type, out GameObject prefab) || prefab == null)
        {
            return null;
        }

        return Spawn(prefab, spawnPoint.position, spawnPoint.rotation);
    }

    /// <summary>立刻刷新并返回当前在场敌人数量。</summary>
    public int RefreshCurrentEnemyCount()
    {
        currentEnemyCount = 0;
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = spawnedEnemies[i];
            if (enemy == null)
            {
                spawnedEnemies.RemoveAt(i);
                continue;
            }

            if (enemy.activeInHierarchy)
            {
                currentEnemyCount++;
            }
        }

        return currentEnemyCount;
    }

    private void CachePrefabs()
    {
        prefabByType.Clear();
        if (enemyPrefabs == null)
        {
            return;
        }

        foreach (EnemyPrefabEntry entry in enemyPrefabs)
        {
            if (entry != null && entry.prefab != null)
            {
                prefabByType[entry.type] = entry.prefab;
            }
        }
    }

    private GameObject GetRandomPrefab()
    {
        int totalWeight = 0;
        foreach (EnemyPrefabEntry entry in enemyPrefabs)
        {
            if (entry != null && entry.prefab != null)
            {
                totalWeight += entry.randomWeight;
            }
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        int selection = UnityEngine.Random.Range(0, totalWeight);
        foreach (EnemyPrefabEntry entry in enemyPrefabs)
        {
            if (entry == null || entry.prefab == null)
            {
                continue;
            }

            selection -= entry.randomWeight;
            if (selection < 0)
            {
                return entry.prefab;
            }
        }

        return null;
    }

    private Vector3 GetRandomPosition()
    {
        Vector2 halfSize = Vector2.Max(Vector2.zero, randomAreaSize) * .5f;
        Vector2 offset = new Vector2(
            UnityEngine.Random.Range(-halfSize.x, halfSize.x),
            UnityEngine.Random.Range(-halfSize.y, halfSize.y));
        return transform.position + (Vector3)offset;
    }

    private GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject enemy = Instantiate(prefab, position, rotation, spawnedEnemyParent);
        spawnedEnemies.Add(enemy);
        currentEnemyCount++;
        return enemy;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, .75f, .1f, .8f);
        Gizmos.DrawWireCube(transform.position, Vector2.Max(Vector2.zero, randomAreaSize));
    }
}
