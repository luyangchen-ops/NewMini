using UnityEngine;

[CreateAssetMenu(menuName = "NewMini/Player/Character Data", fileName = "PlayerCharacterData")]
public sealed class PlayerCharacterData : ScriptableObject
{
    [field: Header("Movement")]
    [field: SerializeField, Min(0f)] public float MoveSpeed { get; private set; } = 5f;
    [field: SerializeField, Min(0f)] public float BoundaryPadding { get; private set; } = .7f;
    [field: Header("Dodge")]
    [field: SerializeField, Min(0f)] public float DodgeDistance { get; private set; } = 2.5f;
    [field: SerializeField, Min(.01f)] public float DodgeDuration { get; private set; } = .25f;
    [field: SerializeField, Range(0f, 1f)] public float PerfectDodgeDistanceRatio { get; private set; } = .3f;
    [field: SerializeField, Min(0f)] public float DodgeCooldown { get; private set; } = 1f;
    [field: Header("Kill Chain")]
    [field: SerializeField, Min(.01f)] public float MaximumAimDistance { get; private set; } = 3f;
    [field: SerializeField, Min(0f)] public float AttackDashDistance { get; private set; } = 5f;
    [field: SerializeField, Min(0f)] public float AttackDashWindupDuration { get; private set; } = .03f;
    [field: SerializeField, Min(.01f)] public float AttackDashDuration { get; private set; } = .14f;
    [field: SerializeField, Min(0f)] public float AttackDashOvershoot { get; private set; } = .3f;
    [field: SerializeField, Range(.01f, 1f)] public float BulletTimeEnemyScale { get; private set; } = .1f;
    [field: SerializeField, Range(0f, 1f)] public float DashEnemyTimeScale { get; private set; } = .35f;
    [field: SerializeField, Min(.01f)] public float BulletTimeEnterDuration { get; private set; } = .12f;
    [field: SerializeField, Min(.01f)] public float BulletTimeExitDuration { get; private set; } = .18f;
    [field: SerializeField, Min(0f)] public float PerfectDodgeFreezeDuration { get; private set; } = .05f;
    [field: SerializeField, Min(.05f)] public float KillChainInitialWindow { get; private set; } = .8f;
    [field: SerializeField, Min(.05f)] public float KillChainMaximumWindow { get; private set; } = 1.2f;
    [field: SerializeField, Min(0f)] public float KillChainTimeRestorePerKill { get; private set; } = .35f;
    [field: SerializeField, Min(0f)] public float KillImpactFreezeDuration { get; private set; } = .055f;
    [field: SerializeField, Min(0f)] public float KillChainInputBufferDuration { get; private set; } = .12f;
    [field: SerializeField, Min(0f)] public float KillChainExitProtection { get; private set; } = .2f;
    [field: Header("Kill Chain Target Assist")]
    [field: SerializeField, Min(0f)] public float TargetAssistWorldRadius { get; private set; } = .9f;
    [field: SerializeField, Range(0f, 90f)] public float TargetAssistMaximumAngle { get; private set; } = 25f;
    [field: Header("Kill Chain Camera")]
    [field: SerializeField, Range(.75f, 1f)] public float KillChainCameraZoomFactor { get; private set; } = .95f;
    [field: SerializeField, Min(0f)] public float KillChainCameraFocusOffset { get; private set; } = .2f;
    [field: SerializeField, Min(.01f)] public float KillChainCameraResponse { get; private set; } = 16f;
    [field: SerializeField, Min(0f)] public float PerfectDodgeCameraShake { get; private set; } = .06f;
    [field: SerializeField, Min(0f)] public float KillCameraShake { get; private set; } = .08f;
    [field: SerializeField, Min(0f)] public float MaximumCameraShake { get; private set; } = .16f;
    [field: Header("Kill Chain Audio")]
    [field: SerializeField] public AudioClip PerfectDodgeSfx { get; private set; }
    [field: SerializeField] public AudioClip BulletTimeLoopSfx { get; private set; }
    [field: SerializeField] public AudioClip DashWindCutSfx { get; private set; }
    [field: SerializeField] public AudioClip HitBladeFleshSfx { get; private set; }
    [field: SerializeField] public AudioClip KillConfirmSfx { get; private set; }
    [field: SerializeField] public AudioClip KillChainEndSfx { get; private set; }
    [field: Header("Attack")]
    [field: SerializeField, Min(0f)] public float NormalAttackCooldown { get; private set; } = .5f;
    [field: SerializeField, Min(0f)] public float NormalAttackRange { get; private set; } = 1.25f;

    private void OnValidate()
    {
        KillChainMaximumWindow = Mathf.Max(KillChainInitialWindow, KillChainMaximumWindow);
    }
}
