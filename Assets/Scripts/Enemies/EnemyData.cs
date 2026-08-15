using UnityEngine;

public enum EnemyArchetype
{
    Melee,
    Ranged
}

[CreateAssetMenu(menuName = "NewMini/Enemies/Enemy Data", fileName = "EnemyData")]
public sealed class EnemyData : ScriptableObject
{
    [field: Header("Identity")]
    [field: SerializeField] public EnemyArchetype Archetype { get; private set; } = EnemyArchetype.Melee;

    [field: Header("Movement")]
    [field: SerializeField, Min(0f)] public float MoveSpeed { get; private set; } = 2.5f;
    [field: SerializeField, Min(0f)] public float StoppingDistance { get; private set; } = 1.2f;

    [field: Header("Ranged Movement")]
    [field: SerializeField] public bool StayStill { get; private set; }
    [field: SerializeField, Range(0f, 1f)] public float IdleChance { get; private set; } = 0.4f;
    [field: SerializeField] public Vector2 RoamStateDurationRange { get; private set; } = new Vector2(0.5f, 1.4f);

    [field: Header("Ranged Attack")]
    [field: SerializeField] public EnemyProjectile ProjectilePrefab { get; private set; }
    [field: SerializeField, Min(0f)] public float ProjectileSpawnOffset { get; private set; } = 0.65f;
    [field: SerializeField, Min(0f)] public float ProjectileSpeed { get; private set; } = 7f;
    [field: SerializeField, Min(0.05f)] public float FireInterval { get; private set; } = 1.2f;

    private void OnValidate()
    {
        Vector2 durationRange = RoamStateDurationRange;
        durationRange.x = Mathf.Max(0.05f, durationRange.x);
        durationRange.y = Mathf.Max(durationRange.x, durationRange.y);
        RoamStateDurationRange = durationRange;
    }
}
