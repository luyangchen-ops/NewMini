using UnityEngine;

public enum EnemyArchetype
{
    Melee,
    Ranged,
    Spearman
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

    [field: Header("Melee Attack Timing")]
    [field: Tooltip("Maximum number of melee units allowed to advance into the inner attack formation around one target.")]
    [field: SerializeField, Min(1)] public int MeleePressureLimit { get; private set; } = 3;
    [field: Tooltip("Random delay before a melee unit starts advancing on a newly acquired target. This breaks up synchronized charges.")]
    [field: SerializeField] public Vector2 MeleeEngagementDelayRange { get; private set; } = new Vector2(0f, .6f);
    [field: Tooltip("Per-unit movement speed variation used while advancing into the melee formation.")]
    [field: SerializeField, Range(0f, .5f)] public float MeleeEngagementSpeedVariance { get; private set; } = .1f;
    [field: Tooltip("Random delay applied after a melee unit reaches its attack position. This prevents a group from opening with the same swing.")]
    [field: SerializeField] public Vector2 MeleeAttackPreparationDelayRange { get; private set; } = new Vector2(.1f, .45f);
    [field: Tooltip("Random variation applied to every melee attack cooldown after an attack completes.")]
    [field: SerializeField, Range(0f, .9f)] public float MeleeAttackIntervalVariance { get; private set; } = .2f;
    [field: Tooltip("Random time a melee unit remains still after finishing an attack.")]
    [field: SerializeField] public Vector2 MeleeAttackRecoveryDelayRange { get; private set; } = new Vector2(.15f, .4f);
    [field: Tooltip("Time from starting a melee swing until the weapon reaches its damaging downward strike.")]
    [field: SerializeField, Min(0f)] public float MeleeAttackHitDelay { get; private set; } = .55f;
    [field: Tooltip("Total time a melee unit remains committed to its swing before it can move again.")]
    [field: SerializeField, Min(.05f)] public float MeleeAttackDuration { get; private set; } = 1.15f;

    [field: Header("Combat")]
    [field: Tooltip("Temporarily set to zero for every enemy.")]
    [field: SerializeField, Min(0f)] public float Damage { get; private set; } = 0f;

    [field: Header("Attack Audio")]
    [field: Tooltip("Played once when this enemy starts its melee or spear attack, or when a ranged projectile is released.")]
    [field: SerializeField] public AudioClip AttackSfx { get; private set; }
    [field: SerializeField, Range(0f, 1f)] public float AttackSfxVolume { get; private set; } = .8f;

    [field: Header("Ranged Movement")]
    [field: SerializeField] public bool StayStill { get; private set; }
    [field: SerializeField, Range(0f, 1f)] public float IdleChance { get; private set; } = 0.4f;
    [field: SerializeField] public Vector2 RoamStateDurationRange { get; private set; } = new Vector2(0.5f, 1.4f);

    [field: Header("Ranged Attack")]
    [field: SerializeField] public EnemyProjectile ProjectilePrefab { get; private set; }
    [field: SerializeField, Min(0f)] public float ProjectileSpawnOffset { get; private set; } = 0.65f;
    [field: SerializeField, Min(0f)] public float ProjectileSpeed { get; private set; } = 7f;
    [field: Tooltip("Downward acceleration applied to arrows. Higher values create a more pronounced arc.")]
    [field: SerializeField, Min(0f)] public float ProjectileGravity { get; private set; } = 9.81f;
    [field: SerializeField, Min(0.05f)] public float FireInterval { get; private set; } = 1.2f;
    [field: SerializeField, Min(0f)] public float RangedAttackReleaseDelay { get; private set; } = .25f;
    [field: SerializeField, Min(.05f)] public float RangedAttackDuration { get; private set; } = .34f;

    [field: Header("Melee Perfect Dodge")]
    [field: SerializeField, Min(0f)] public float MeleePerfectDodgeDelay { get; private set; } = .35f;
    [field: SerializeField, Min(0.01f)] public float MeleePerfectDodgeDuration { get; private set; } = .18f;

    [field: Header("Spearman Attack")]
    [field: Tooltip("Time spent holding the spear back before the forward thrust begins.")]
    [field: SerializeField, Min(0.05f)] public float SpearWindupDuration { get; private set; } = .45f;
    [field: Tooltip("How long the forward thrust movement lasts.")]
    [field: SerializeField, Min(0.01f)] public float SpearThrustDuration { get; private set; } = .18f;
    [field: SerializeField, Min(0f)] public float SpearThrustSpeed { get; private set; } = 8f;
    [field: Tooltip("Playback multiplier for the spear attack animation during the stationary windup.")]
    [field: SerializeField, Range(.05f, 1f)] public float SpearWindupAnimationSpeed { get; private set; } = .3f;
    [field: Tooltip("Point within the thrust at which the spear reaches its damaging impact.")]
    [field: SerializeField, Range(.1f, 1f)] public float SpearImpactNormalizedTime { get; private set; } = .85f;
    [field: Tooltip("Perfect-dodge window centred on the spear impact.")]
    [field: SerializeField, Min(.01f)] public float SpearPerfectDodgeWindowDuration { get; private set; } = .24f;
    [field: Tooltip("Maximum distance from the spear soldier's centre that can be struck during the thrust.")]
    [field: SerializeField, Min(0.01f)] public float SpearHitRange { get; private set; } = 2f;
    [field: Tooltip("Width of the spear hit area. Targets need to stay close to the thrust line.")]
    [field: SerializeField, Min(0.01f)] public float SpearHitRadius { get; private set; } = .4f;
    [field: SerializeField, Range(1f, 179f)] public float SpearHitAngle { get; private set; } = 50f;

    [field: Header("Shield Bearer")]
    [field: Tooltip("Half-angle of the protected frontal arc. Attacks outside the rear arc are blocked.")]
    [field: SerializeField, Range(0f, 89f)] public float ShieldRearKillHalfAngle { get; private set; } = 55f;
    [field: SerializeField, Min(.05f)] public float ShieldBlockDuration { get; private set; } = .55f;
    [field: SerializeField, Min(.05f)] public float ShieldAttackWindup { get; private set; } = .6f;
    [field: SerializeField, Min(.05f)] public float ShieldAttackDuration { get; private set; } = .95f;
    [field: SerializeField, Min(.1f)] public float ShieldAttackInterval { get; private set; } = 2.8f;
    [field: Tooltip("Perfect-dodge window centred on the shield bash impact.")]
    [field: SerializeField, Min(.01f)] public float ShieldPerfectDodgeWindowDuration { get; private set; } = .28f;

    private void OnValidate()
    {
        Vector2 meleeEngagementRange = MeleeEngagementDelayRange;
        meleeEngagementRange.x = Mathf.Max(0f, meleeEngagementRange.x);
        meleeEngagementRange.y = Mathf.Max(meleeEngagementRange.x, meleeEngagementRange.y);
        MeleeEngagementDelayRange = meleeEngagementRange;
        MeleeEngagementSpeedVariance = Mathf.Clamp(MeleeEngagementSpeedVariance, 0f, .5f);
        MeleePressureLimit = Mathf.Max(1, MeleePressureLimit);

        Vector2 meleePreparationRange = MeleeAttackPreparationDelayRange;
        meleePreparationRange.x = Mathf.Max(0f, meleePreparationRange.x);
        meleePreparationRange.y = Mathf.Max(meleePreparationRange.x, meleePreparationRange.y);
        MeleeAttackPreparationDelayRange = meleePreparationRange;
        MeleeAttackIntervalVariance = Mathf.Clamp(MeleeAttackIntervalVariance, 0f, .9f);
        Vector2 meleeRecoveryRange = MeleeAttackRecoveryDelayRange;
        meleeRecoveryRange.x = Mathf.Max(0f, meleeRecoveryRange.x);
        meleeRecoveryRange.y = Mathf.Max(meleeRecoveryRange.x, meleeRecoveryRange.y);
        MeleeAttackRecoveryDelayRange = meleeRecoveryRange;
        MeleeAttackHitDelay = Mathf.Max(0f, MeleeAttackHitDelay);
        MeleeAttackDuration = Mathf.Max(MeleeAttackHitDelay, MeleeAttackDuration);
        AttackSfxVolume = Mathf.Clamp01(AttackSfxVolume);

        Vector2 durationRange = RoamStateDurationRange;
        durationRange.x = Mathf.Max(0.05f, durationRange.x);
        durationRange.y = Mathf.Max(durationRange.x, durationRange.y);
        RoamStateDurationRange = durationRange;
        MeleePerfectDodgeDelay = Mathf.Max(0f, MeleePerfectDodgeDelay);
        MeleePerfectDodgeDuration = Mathf.Max(.01f, MeleePerfectDodgeDuration);
        SpearWindupDuration = Mathf.Max(.05f, SpearWindupDuration);
        SpearThrustDuration = Mathf.Max(.01f, SpearThrustDuration);
        SpearThrustSpeed = Mathf.Max(0f, SpearThrustSpeed);
        SpearWindupAnimationSpeed = Mathf.Clamp(SpearWindupAnimationSpeed, .05f, 1f);
        SpearImpactNormalizedTime = Mathf.Clamp(SpearImpactNormalizedTime, .1f, 1f);
        SpearPerfectDodgeWindowDuration = Mathf.Max(.01f, SpearPerfectDodgeWindowDuration);
        SpearHitRange = Mathf.Max(.01f, SpearHitRange);
        SpearHitRadius = Mathf.Max(.01f, SpearHitRadius);
        MeleeFormationRadius = Mathf.Max(.1f, MeleeFormationRadius);
        MeleeFormationArrivalDistance = Mathf.Max(.01f, MeleeFormationArrivalDistance);
        MeleeSeparationRadius = Mathf.Max(.1f, MeleeSeparationRadius);
        MeleeSeparationStrength = Mathf.Max(0f, MeleeSeparationStrength);
        ShieldBlockDuration = Mathf.Max(.05f, ShieldBlockDuration);
        ShieldAttackWindup = Mathf.Max(.05f, ShieldAttackWindup);
        ShieldAttackDuration = Mathf.Max(ShieldAttackWindup, ShieldAttackDuration);
        ShieldAttackInterval = Mathf.Max(.1f, ShieldAttackInterval);
        ShieldPerfectDodgeWindowDuration = Mathf.Max(.01f, ShieldPerfectDodgeWindowDuration);
        ProjectileGravity = Mathf.Max(0f, ProjectileGravity);
        RangedAttackReleaseDelay = Mathf.Max(0f, RangedAttackReleaseDelay);
        RangedAttackDuration = Mathf.Max(RangedAttackReleaseDelay, RangedAttackDuration);
    }

    public float GetMeleeAttackPreparationDelay()
    {
        return Random.Range(MeleeAttackPreparationDelayRange.x, MeleeAttackPreparationDelayRange.y);
    }

    public float GetMeleeEngagementDelay()
    {
        return Random.Range(MeleeEngagementDelayRange.x, MeleeEngagementDelayRange.y);
    }

    public float GetMeleeAttackCooldown(float baseInterval)
    {
        float variation = MeleeAttackIntervalVariance;
        return Mathf.Max(.05f, baseInterval * Random.Range(1f - variation, 1f + variation));
    }

    public float GetMeleeAttackRecoveryDelay()
    {
        return Random.Range(MeleeAttackRecoveryDelayRange.x, MeleeAttackRecoveryDelayRange.y);
    }
}
