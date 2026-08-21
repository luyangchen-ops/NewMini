using System;
using UnityEngine;

[CreateAssetMenu(menuName = "NewMini/Player/Character Data", fileName = "PlayerCharacterData")]
public sealed class PlayerCharacterData : ScriptableObject
{
    [SerializeField] private MovementSettings movement = new MovementSettings();
    [SerializeField] private DodgeSettings dodge = new DodgeSettings();
    [SerializeField] private CombatSettings combat = new CombatSettings();
    [SerializeField] private VitalsSettings vitals = new VitalsSettings();
    [SerializeField] private KillChainSettings killChain = new KillChainSettings();
    [SerializeField] private UltimateSettings ultimate = new UltimateSettings();
    [SerializeField] private FeedbackSettings feedback = new FeedbackSettings();

    public MovementSettings Movement => movement;
    public DodgeSettings Dodge => dodge;
    public CombatSettings Combat => combat;
    public VitalsSettings Vitals => vitals;
    public KillChainSettings KillChain => killChain;
    public UltimateSettings Ultimate => ultimate;
    public FeedbackSettings Feedback => feedback;

    [Serializable]
    public sealed class MovementSettings
    {
        [field: SerializeField, Min(0f)] public float MoveSpeed { get; private set; } = 5f;
        [field: SerializeField, Min(0f)] public float BoundaryPadding { get; private set; } = .7f;
    }

    [Serializable]
    public sealed class DodgeSettings
    {
        [field: SerializeField, Min(0f)] public float Distance { get; private set; } = 2.5f;
        [field: SerializeField, Min(.01f)] public float Duration { get; private set; } = .25f;
        [field: SerializeField, Range(0f, 1f)] public float PerfectDistanceRatio { get; private set; } = .3f;
        [field: SerializeField, Min(0f)] public float Cooldown { get; private set; } = 1f;
    }

    [Serializable]
    public sealed class CombatSettings
    {
        [field: SerializeField, Min(0f)] public float NormalAttackCooldown { get; private set; } = .5f;
        [field: SerializeField, Min(0f)] public float NormalAttackRange { get; private set; } = 1.5f;
        [field: SerializeField, Range(1f, 360f)] public float NormalAttackArcAngle { get; private set; } = 220f;
        [field: SerializeField, Min(0f)] public float NormalKillHealthRestore { get; private set; } = 5f;
        [field: SerializeField, Min(0f)] public float KillChainHealthRestore { get; private set; } = 15f;
    }

    [Serializable]
    public sealed class VitalsSettings
    {
        [field: SerializeField, Min(1f)] public float MaximumHealth { get; private set; } = 100f;
        [field: SerializeField, Min(1)] public int MaximumMomentum { get; private set; } = 20;
        [field: SerializeField, Min(0)] public int StartingMomentum { get; private set; } = 20;
        [field: SerializeField, Min(1)] public int MomentumPerKill { get; private set; } = 1;
        [field: SerializeField, Min(1)] public int ComboRewardThreshold { get; private set; } = 3;
        [field: SerializeField, Min(0)] public int BonusMomentumPerComboKill { get; private set; } = 1;
    }

    [Serializable]
    public sealed class KillChainSettings
    {
        [field: Header("Dash")]
        [field: SerializeField, Min(.01f)] public float MaximumAimDistance { get; private set; } = 3f;
        [field: SerializeField, Min(0f)] public float AttackDashDistance { get; private set; } = 5f;
        [field: SerializeField, Min(0f)] public float AttackDashWindupDuration { get; private set; } = .03f;
        [field: SerializeField, Min(.01f)] public float AttackDashDuration { get; private set; } = .14f;
        [field: SerializeField, Min(0f)] public float AttackDashOvershoot { get; private set; } = .3f;
        [field: SerializeField, Range(.01f, 1f)] public float BulletTimeEnemyScale { get; private set; } = .1f;
        [field: SerializeField, Range(0f, 1f)] public float DashEnemyTimeScale { get; private set; } = .35f;

        [field: Header("Timing")]
        [field: SerializeField, Min(.01f)] public float BulletTimeEnterDuration { get; private set; } = .12f;
        [field: SerializeField, Min(.01f)] public float BulletTimeExitDuration { get; private set; } = .18f;
        [field: SerializeField, Min(0f)] public float PerfectDodgeFreezeDuration { get; private set; } = .05f;
        [field: SerializeField, Min(.05f)] public float InitialWindow { get; private set; } = 1.5f;
        [field: SerializeField, Min(.05f)] public float TimeRestorePerKill { get; private set; } = 1.5f;
        [field: SerializeField, Min(0f)] public float ImpactFreezeDuration { get; private set; } = .055f;
        [field: SerializeField, Min(0f)] public float InputBufferDuration { get; private set; } = .12f;
        [field: SerializeField, Min(0f)] public float ExitProtection { get; private set; } = .2f;

        [field: Header("Target Assist")]
        [field: SerializeField, Min(0f)] public float TargetAssistWorldRadius { get; private set; } = .9f;
        [field: SerializeField, Range(0f, 90f)] public float TargetAssistMaximumAngle { get; private set; } = 25f;
        [field: SerializeField, Range(1f, 90f)] public float DirectionalSearchHalfAngle { get; private set; } = 50f;
        [field: SerializeField, Min(1f)] public float RangeOverlayWorldDiameter { get; private set; } = 40f;

        [field: Header("Camera")]
        [field: SerializeField, Range(.75f, 1f)] public float CameraZoomFactor { get; private set; } = .95f;
        [field: SerializeField, Min(0f)] public float CameraFocusOffset { get; private set; } = .2f;
        [field: SerializeField, Min(.01f)] public float CameraResponse { get; private set; } = 16f;
        [field: SerializeField, Min(0f)] public float PerfectDodgeCameraShake { get; private set; } = .06f;
        [field: SerializeField, Min(0f)] public float KillCameraShake { get; private set; } = .08f;
        [field: SerializeField, Min(0f)] public float MaximumCameraShake { get; private set; } = .16f;
    }

    [Serializable]
    public sealed class UltimateSettings
    {
        [field: SerializeField, Min(1)] public int MaximumTargets { get; private set; } = 12;
        [field: SerializeField, Min(.05f)] public float MarkRadius { get; private set; } = .55f;
        [field: SerializeField, Min(.01f)] public float TrailPointDistance { get; private set; } = .08f;
        [field: SerializeField, Min(.5f)] public float MarkDuration { get; private set; } = 3.5f;
        [field: SerializeField, Min(.01f)] public float ExecutionInterval { get; private set; } = .075f;
        [field: SerializeField, Min(.01f)] public float FinisherDuration { get; private set; } = .22f;
        [field: SerializeField, Range(.75f, 1f)] public float CameraZoomFactor { get; private set; } = .86f;
    }

    [Serializable]
    public sealed class FeedbackSettings
    {
        [field: Header("Audio Volumes")]
        [field: SerializeField, Range(0f, 1f)] public float BulletTimeLoopVolume { get; private set; } = .45f;
        [field: SerializeField, Range(0f, 1f)] public float DashWindCutVolume { get; private set; } = .9f;
        [field: SerializeField, Range(0f, 1f)] public float HitBladeFleshVolume { get; private set; } = .85f;
        [field: SerializeField, Range(0f, 1f)] public float KillConfirmVolume { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 1f)] public float KillChainEndVolume { get; private set; } = .9f;

        [field: Header("Kill Chain Audio")]
        [field: SerializeField] public AudioClip PerfectDodgeSfx { get; private set; }
        [field: SerializeField] public AudioClip BulletTimeLoopSfx { get; private set; }
        [field: SerializeField] public AudioClip DashWindCutSfx { get; private set; }
        [field: SerializeField] public AudioClip HitBladeFleshSfx { get; private set; }
        [field: SerializeField] public AudioClip KillConfirmSfx { get; private set; }
        [field: SerializeField] public AudioClip KillChainEndSfx { get; private set; }

        [field: Header("Ultimate Trail")]
        [field: SerializeField, Min(1f)] public float UltimateTrailWidthMultiplier { get; private set; } = 1.75f;
        [field: SerializeField] public Color UltimateTrailStartColor { get; private set; } = Color.white;
        [field: SerializeField] public Color UltimateTrailEndColor { get; private set; } = new Color(1f, .08f, .03f, .9f);
        [field: SerializeField] public Color UltimateMarkedColor { get; private set; } = new Color(1f, .12f, .06f, 1f);
    }
}
