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

    [field: Header("Melee Formation")]
    [field: Tooltip("Distance from the player at which melee enemies reserve their ring position.")]
    [field: SerializeField, Min(.1f)] public float MeleeFormationRadius { get; private set; } = 1.05f;
    [field: SerializeField, Min(.01f)] public float MeleeFormationArrivalDistance { get; private set; } = .18f;
    [field: SerializeField, Min(.1f)] public float MeleeSeparationRadius { get; private set; } = .8f;
    [field: SerializeField, Min(0f)] public float MeleeSeparationStrength { get; private set; } = .9f;

    [field: Header("Combat")]
    [field: Tooltip("Temporarily set to zero for every enemy.")]
    [field: SerializeField, Min(0f)] public float Damage { get; private set; } = 0f;

    [field: Header("Ranged Movement")]
    [field: SerializeField] public bool StayStill { get; private set; }
    [field: SerializeField, Range(0f, 1f)] public float IdleChance { get; private set; } = 0.4f;
    [field: SerializeField] public Vector2 RoamStateDurationRange { get; private set; } = new Vector2(0.5f, 1.4f);

    [field: Header("Ranged Attack")]
    [field: SerializeField] public EnemyProjectile ProjectilePrefab { get; private set; }
    [field: SerializeField, Min(0f)] public float ProjectileSpawnOffset { get; private set; } = 0.65f;
    [field: SerializeField, Min(0f)] public float ProjectileSpeed { get; private set; } = 7f;
    [field: SerializeField, Min(0.05f)] public float FireInterval { get; private set; } = 1.2f;
    [field: SerializeField, Min(0f)] public float RangedAttackReleaseDelay { get; private set; } = .25f;
    [field: SerializeField, Min(.05f)] public float RangedAttackDuration { get; private set; } = .34f;

    [field: Header("Melee Perfect Dodge")]
    [field: SerializeField, Min(0f)] public float MeleePerfectDodgeDelay { get; private set; } = .35f;
    [field: SerializeField, Min(0.01f)] public float MeleePerfectDodgeDuration { get; private set; } = .18f;

    [field: Header("Shield Bearer")]
    [field: Tooltip("Half-angle of the protected frontal arc. Attacks outside the rear arc are blocked.")]
    [field: SerializeField, Range(0f, 89f)] public float ShieldRearKillHalfAngle { get; private set; } = 55f;
    [field: SerializeField, Min(.05f)] public float ShieldBlockDuration { get; private set; } = .55f;
    [field: SerializeField, Min(.05f)] public float ShieldAttackWindup { get; private set; } = .6f;
    [field: SerializeField, Min(.05f)] public float ShieldAttackDuration { get; private set; } = .95f;
    [field: SerializeField, Min(.1f)] public float ShieldAttackInterval { get; private set; } = 2.8f;

    private void OnValidate()
    {
        Vector2 durationRange = RoamStateDurationRange;
        durationRange.x = Mathf.Max(0.05f, durationRange.x);
        durationRange.y = Mathf.Max(durationRange.x, durationRange.y);
        RoamStateDurationRange = durationRange;
        MeleePerfectDodgeDelay = Mathf.Max(0f, MeleePerfectDodgeDelay);
        MeleePerfectDodgeDuration = Mathf.Max(.01f, MeleePerfectDodgeDuration);
        MeleeFormationRadius = Mathf.Max(.1f, MeleeFormationRadius);
        MeleeFormationArrivalDistance = Mathf.Max(.01f, MeleeFormationArrivalDistance);
        MeleeSeparationRadius = Mathf.Max(.1f, MeleeSeparationRadius);
        MeleeSeparationStrength = Mathf.Max(0f, MeleeSeparationStrength);
        ShieldBlockDuration = Mathf.Max(.05f, ShieldBlockDuration);
        ShieldAttackWindup = Mathf.Max(.05f, ShieldAttackWindup);
        ShieldAttackDuration = Mathf.Max(ShieldAttackWindup, ShieldAttackDuration);
        ShieldAttackInterval = Mathf.Max(.1f, ShieldAttackInterval);
        RangedAttackReleaseDelay = Mathf.Max(0f, RangedAttackReleaseDelay);
        RangedAttackDuration = Mathf.Max(RangedAttackReleaseDelay, RangedAttackDuration);
    }
}
