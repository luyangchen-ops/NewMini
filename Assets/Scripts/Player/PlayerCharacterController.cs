using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D), typeof(PlayerSpecialItemInventory))]
/// <summary>
/// Player composition root. This partial owns serialized configuration, shared state,
/// Unity lifecycle methods, and high-level state dispatch. Feature behavior lives in
/// the Locomotion, Combat, KillChain, Ultimate, and Presentation partial modules.
/// </summary>
public partial class PlayerCharacterController : MonoBehaviour
{
    public static float EnemyTimeScale { get; private set; } = 1f;

    [Header("3C Data")]
    [SerializeField] private PlayerCharacterData characterData;

    [Header("Legacy Overrides (used while Character Data is empty)")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float boundaryPadding = .7f;
    [SerializeField] private Camera worldCamera;
    [Tooltip("Viewport dead-zone ratio. Lower values make the camera start following while the player is closer to screen center.")]
    [SerializeField, Range(.1f, 1f)] private float cameraFollowDeadZone = .65f;
    [Tooltip("World-space framing offset applied to the normal player-follow target.")]
    [SerializeField] private Vector2 cameraFollowOffset;
    [SerializeField, Min(.01f)] private float maximumDragDistance = 3f;
    [SerializeField, Min(0f)] private float dodgeDistance = 2.5f;
    [SerializeField, Min(.01f)] private float dodgeDuration = .25f;
    [SerializeField, Range(0f, 1f)] private float perfectDodgeDistanceRatio = .3f;
    [SerializeField, Min(0f)] private float dashCooldown = 1f;
    [SerializeField, Min(0f)] private float attackDashDistance = 5f;
    [SerializeField, Min(.01f)] private float attackDashDuration = .18f;
    [SerializeField, Range(.01f, 1f)] private float bulletTimeEnemyScale = .1f;
    [SerializeField] private string enemyNamePrefix = "Enemy";

    [Header("Presentation")]
    [SerializeField] private Animator visualAnimator;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private PerfectDodgeAfterimage perfectDodgeAfterimage;
    [SerializeField] private BloodHitEffect bloodHitEffectPrefab;
    [SerializeField, Range(0f, 1f)] private float bulletTimeLoopVolume = .45f;
    [SerializeField, Range(0f, 1f)] private float dashWindCutVolume = .9f;
    [SerializeField, Range(0f, 1f)] private float hitBladeFleshVolume = .85f;
    [SerializeField, Range(0f, 1f)] private float killConfirmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float killChainEndVolume = .9f;
    [SerializeField] private AudioClip normalAttackSfx;
    [SerializeField] private AudioClip dashAttackSfx;
    [SerializeField] private AudioClip rollSfx;
    [SerializeField] private AudioClip parrySfx;
    [SerializeField] private AudioClip killSfx;
    [SerializeField, Min(0f)] private float normalAttackCooldown = .5f;
    [SerializeField, Min(0f)] private float normalAttackRange = 1.5f;
    [SerializeField, Range(1f, 360f)] private float normalAttackArcAngle = 220f;

    [Header("Vitals and Momentum")]
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    [SerializeField, Min(1)] private int maximumMomentum = 20;
    [SerializeField, Min(0)] private int startingMomentum = 20;
    [SerializeField, Min(1)] private int momentumPerKill = 1;
    [SerializeField, Min(1)] private int comboRewardThreshold = 3;
    [SerializeField, Min(0)] private int bonusMomentumPerComboKill = 1;

    [Header("Momentum Ultimate - Time Stop Slash")]
    [SerializeField, Min(1)] private int ultimateMaximumTargets = 12;
    [SerializeField, Min(.05f)] private float ultimateMarkRadius = .55f;
    [SerializeField, Min(.01f)] private float ultimateTrailPointDistance = .08f;
    [SerializeField, Min(.5f)] private float ultimateMarkDuration = 3.5f;
    [SerializeField, Min(.01f)] private float ultimateExecutionInterval = .075f;
    [SerializeField, Min(.01f)] private float ultimateFinisherDuration = .22f;
    [SerializeField, Range(.75f, 1f)] private float ultimateCameraZoomFactor = .86f;
    [SerializeField, Min(1f)] private float ultimateTrailWidthMultiplier = 1.75f;
    [SerializeField] private Color ultimateTrailStartColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color ultimateTrailEndColor = new Color(1f, .08f, .03f, .9f);
    [SerializeField] private Color ultimateMarkedColor = new Color(1f, .12f, .06f, 1f);
    [SerializeField] private UnityEvent onUltimateStarted = new UnityEvent();
    [SerializeField] private UnityEvent<Transform> onUltimateTargetMarked = new UnityEvent<Transform>();
    [SerializeField] private UnityEvent<int> onUltimateFinished = new UnityEvent<int>();

    [Header("Authored Kill Chain Feedback (Optional)")]
    [Tooltip("Assign an authored world-space target reticle root. No object is created at runtime.")]
    [SerializeField] private GameObject targetReticleRoot;
    [SerializeField] private Transform targetReticleAnchor;
    [SerializeField] private LineRenderer targetPathLine;
    [Tooltip("Optional renderer whose material accepts a _ChainWindow01 float.")]
    [SerializeField] private SpriteRenderer chainWindowRenderer;

    [Header("Authored Kill Chain Range Overlay")]
    [SerializeField] private SpriteRenderer killChainRangeOverlay;
    [SerializeField, Min(1f)] private float rangeOverlayWorldDiameter = 40f;
    [SerializeField, Range(1f, 90f)] private float directionalTargetSearchHalfAngle = 50f;

    [Header("Persistent Kill Chain Events (Optional)")]
    [SerializeField] private UnityEvent onKillChainStarted = new UnityEvent();
    [SerializeField] private UnityEvent<Transform> onKillChainTargetChanged = new UnityEvent<Transform>();
    [SerializeField] private UnityEvent<int> onKillChainKillConfirmed = new UnityEvent<int>();
    [SerializeField] private UnityEvent<int> onKillChainEnded = new UnityEvent<int>();
    [SerializeField] private UnityEvent onInvalidKillChainTarget = new UnityEvent();

    [Header("Legacy Aim Arrow (not used by the current presentation)")]
    [SerializeField] private GameObject arrowRoot;
    [SerializeField] private LineRenderer arrowLine;
    [SerializeField] private Transform arrowHead;

    private readonly HashSet<Transform> targetCandidates = new HashSet<Transform>();
    private readonly List<Transform> ultimateTargets = new List<Transform>();
    private readonly HashSet<Transform> ultimateTargetSet = new HashSet<Transform>();
    private readonly HashSet<Transform> ultimateBossesInsideSwipe = new HashSet<Transform>();
    private readonly HashSet<Transform> ultimateBossesTouchedThisSegment = new HashSet<Transform>();
    private readonly List<Vector3> ultimateTrailPoints = new List<Vector3>();
    private readonly Dictionary<SpriteRenderer, Color> ultimateMarkedRenderers = new Dictionary<SpriteRenderer, Color>();
    private MaterialPropertyBlock feedbackProperties;
    private Rigidbody2D body;
    private PlayerInputController input;
    private PlayerCameraController cameraController;
    private PlayerStateMachine stateMachine;
    private Collider2D[] playerColliders;
    private Vector2 dashStart;
    private Vector2 dashTarget;
    private Vector2 killDashDirection;
    private Vector2 bossGuardKnockbackStart;
    private Vector2 bossGuardKnockbackTarget;
    private float dashElapsed;
    private float bossGuardKnockbackElapsed;
    private float bossGuardKnockbackDuration;
    private float activeDashDuration;
    private float dashReadyTime;
    private float normalAttackReadyTime;
    private float stateTimer;
    private float chainWindowRemaining;
    private float chainWindowDuration;
    private float bufferedTargetUntil;
    private float exitProtectionUntil;
    private float animatorBaseSpeed = 1f;
    private float enemyTimeScaleTarget = 1f;
    private int killChainCount;
    private float currentHealth;
    private bool isDead;
    private bool bossGuardControlLocked;
    private bool bossGuardKnockbackActive;
    private int currentMomentum;
    private int ultimateExecutionIndex;
    private int ultimateExecutedKills;
    private float ultimateMarkTimeRemaining;
    private bool ultimateSwipeStarted;
    private Vector2 ultimateLastPointerPosition;
    private LineRenderer ultimateLine;
    private bool ultimateLineSettingsSaved;
    private bool ultimateLineOriginalUseWorldSpace;
    private float ultimateLineOriginalWidthMultiplier;
    private Color ultimateLineOriginalStartColor;
    private Color ultimateLineOriginalEndColor;
    private Transform currentTarget;
    private Transform lockedDashTarget;
    private Transform bufferedTarget;
    private Transform lastKilledTarget;
    private bool isFreeKillChainDash;
    private bool killChainTutorialHold;
    private bool presentationLocomotionActive;
    private bool presentationIdleActive;
    private Vector2 presentationLocomotionDirection;
    private PlayerSpecialItemInventory specialItems;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int VerticalDirection = Animator.StringToHash("VerticalDirection");
    private static readonly int NormalAttack = Animator.StringToHash("NormalAttack");
    private static readonly int DashAttack = Animator.StringToHash("DashAttack");
    private static readonly int Roll = Animator.StringToHash("Roll");
    private static readonly int Hurt = Animator.StringToHash("Hurt");
    private static readonly int Sheathe = Animator.StringToHash("Sheathe");
    private static readonly int IsDeadAnimatorParam = Animator.StringToHash("IsDead");
    private static readonly int PresentationIdle = Animator.StringToHash("Idle Hold Ink Echo");
    private static readonly int ChainWindow01 = Shader.PropertyToID("_ChainWindow01");
    private static readonly int RangeRadius01 = Shader.PropertyToID("_RangeRadius01");
    private static readonly int EffectStrength = Shader.PropertyToID("_EffectStrength");
    private const float KillChainPropBreakRadius = .35f;
    private const float KillChainEnemyHitRadius = .35f;

    public PlayerStateId State => stateMachine != null ? stateMachine.Current : PlayerStateId.Locomotion;
    public bool IsDodging => stateMachine != null && stateMachine.Is(PlayerStateId.Dodge);
    public bool IsKillChainActive => stateMachine != null && IsKillChainState(stateMachine.Current);
    public bool IsUltimateActive => stateMachine != null && IsUltimateState(stateMachine.Current);
    public bool IsBossGuardLocked => bossGuardControlLocked;
    public bool IsInvulnerable => IsDodging || IsKillChainActive || IsUltimateActive || Time.unscaledTime < exitProtectionUntil;
    public int KillChainCount => killChainCount;
    public float MaximumHealth => maximumHealth;
    public float CurrentHealth => currentHealth;
    public float HealthNormalized => maximumHealth <= 0f ? 0f : currentHealth / maximumHealth;
    public bool IsDead => isDead;
    public int MaximumMomentum => maximumMomentum;
    public int CurrentMomentum => currentMomentum;
    public bool IsMomentumFull => currentMomentum >= maximumMomentum;
    public float DodgeCooldownNormalized => DodgeCooldown <= 0f
        ? 0f
        : Mathf.Clamp01((dashReadyTime - Time.time) / DodgeCooldown);
    public bool CanUseUltimate => currentMomentum >= maximumMomentum && State == PlayerStateId.Locomotion;
    public Transform CurrentKillChainTarget => currentTarget != null ? currentTarget : lockedDashTarget;
    public float KillChainWindowNormalized => chainWindowDuration <= 0f
        ? 0f
        : Mathf.Clamp01(chainWindowRemaining / chainWindowDuration);

    /// <summary>
    /// Lets an authored cinematic drive the locomotion animation while it moves the
    /// player transform. Normal input animation updates resume when disabled.
    /// </summary>
    public void SetPresentationLocomotion(bool moving, Vector2 direction)
    {
        if (moving) presentationIdleActive = false;
        presentationLocomotionActive = moving;
        presentationLocomotionDirection = direction.sqrMagnitude > .0001f
            ? direction.normalized
            : Vector2.zero;

        if (visualAnimator == null) return;
        if (moving) UpdateFacing(presentationLocomotionDirection);
        else visualAnimator.SetInteger(VerticalDirection, 0);
        visualAnimator.SetFloat(Speed, moving ? 1f : 0f);
    }

    /// <summary>Locks the authored hero presentation to its standing animation.</summary>
    public void SetPresentationIdle(bool active)
    {
        presentationIdleActive = active;
        if (!active) return;

        presentationLocomotionActive = false;
        presentationLocomotionDirection = Vector2.zero;
        if (body != null) body.linearVelocity = Vector2.zero;
        if (stateMachine != null) stateMachine.Change(PlayerStateId.Locomotion);
        if (visualAnimator == null) return;
        visualAnimator.SetFloat(Speed, 0f);
        visualAnimator.SetInteger(VerticalDirection, 0);
        visualAnimator.CrossFade(PresentationIdle, .05f);
    }

    public event Action<int> KillChainKillConfirmed;
    public event Action<int> KillChainFinished;
    public event Action KillChainStarted;
    public event Action<float, float> HealthChanged;
    public event Action Died;
    public event Action<int, int> MomentumChanged;
    public event Action UltimateStarted;
    public event Action<Transform, int> UltimateTargetMarked;
    public event Action<int> UltimateFinished;

    private float MoveSpeed => characterData != null ? characterData.MoveSpeed : moveSpeed;
    private float Padding => characterData != null ? characterData.BoundaryPadding : boundaryPadding;
    private float DodgeDistance => characterData != null ? characterData.DodgeDistance : dodgeDistance;
    private float DodgeDuration => characterData != null ? characterData.DodgeDuration : dodgeDuration;
    private float PerfectRatio => characterData != null ? characterData.PerfectDodgeDistanceRatio : perfectDodgeDistanceRatio;
    private float DodgeCooldown => characterData != null ? characterData.DodgeCooldown : dashCooldown;
    private float MaximumAimDistance => characterData != null ? characterData.MaximumAimDistance : maximumDragDistance;
    private float AttackDashDistance => characterData != null ? characterData.AttackDashDistance : attackDashDistance;
    private float AttackDashWindupDuration => characterData != null ? characterData.AttackDashWindupDuration : .03f;
    private float AttackDashDuration => characterData != null ? characterData.AttackDashDuration : attackDashDuration;
    private float BulletTimeScale => characterData != null ? characterData.BulletTimeEnemyScale : bulletTimeEnemyScale;
    private float AttackCooldown => characterData != null ? characterData.NormalAttackCooldown : normalAttackCooldown;
    private float NormalAttackRange => characterData != null ? characterData.NormalAttackRange : normalAttackRange;
    private float NormalKillHealthRestore => characterData != null ? characterData.NormalKillHealthRestore : 5f;
    private float KillChainHealthRestore => characterData != null ? characterData.KillChainHealthRestore : 15f;
    private float AttackDashOvershoot => characterData != null ? characterData.AttackDashOvershoot : .3f;
    private float DashEnemyTimeScale => characterData != null ? characterData.DashEnemyTimeScale : .35f;
    private float BulletTimeEnterDuration => characterData != null ? characterData.BulletTimeEnterDuration : .12f;
    private float BulletTimeExitDuration => characterData != null ? characterData.BulletTimeExitDuration : .18f;
    private float PerfectDodgeFreezeDuration => characterData != null ? characterData.PerfectDodgeFreezeDuration : .05f;
    private float KillChainInitialWindow => characterData != null ? characterData.KillChainInitialWindow : 1.5f;
    private float KillChainTimeRestore => characterData != null ? characterData.KillChainTimeRestorePerKill : 1.5f;
    private float KillImpactFreezeDuration => characterData != null ? characterData.KillImpactFreezeDuration : .055f;
    private float KillChainInputBufferDuration => characterData != null ? characterData.KillChainInputBufferDuration : .12f;
    private float KillChainExitProtection => characterData != null ? characterData.KillChainExitProtection : .2f;
    private float TargetAssistWorldRadius => characterData != null ? characterData.TargetAssistWorldRadius : .9f;
    private float TargetAssistMaximumAngle => characterData != null ? characterData.TargetAssistMaximumAngle : 25f;
    private float CameraZoomFactor => characterData != null ? characterData.KillChainCameraZoomFactor : .95f;
    private float CameraFocusOffset => characterData != null ? characterData.KillChainCameraFocusOffset : .2f;
    private float CameraResponse => characterData != null ? characterData.KillChainCameraResponse : 16f;
    private float PerfectDodgeCameraShake => characterData != null ? characterData.PerfectDodgeCameraShake : .06f;
    private float KillCameraShake => characterData != null ? characterData.KillCameraShake : .08f;
    private float MaximumCameraShake => characterData != null ? characterData.MaximumCameraShake : .16f;
    private AudioClip PerfectDodgeSfx => characterData != null ? characterData.PerfectDodgeSfx : null;
    private AudioClip BulletTimeLoopSfx => characterData != null ? characterData.BulletTimeLoopSfx : null;
    private AudioClip DashWindCutSfx => characterData != null ? characterData.DashWindCutSfx : null;
    private AudioClip HitBladeFleshSfx => characterData != null ? characterData.HitBladeFleshSfx : null;
    private AudioClip KillConfirmSfx => characterData != null ? characterData.KillConfirmSfx : null;
    private AudioClip KillChainEndSfx => characterData != null ? characterData.KillChainEndSfx : null;

    protected virtual void Awake()
    {
        feedbackProperties = new MaterialPropertyBlock();
        body = GetComponent<Rigidbody2D>();
        specialItems = GetComponent<PlayerSpecialItemInventory>();
        if (specialItems == null)
        {
            specialItems = gameObject.AddComponent<PlayerSpecialItemInventory>();
        }
        currentHealth = maximumHealth;
        currentMomentum = Mathf.Clamp(startingMomentum, 0, maximumMomentum);
        playerColliders = GetComponentsInChildren<Collider2D>(true);
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        input = new PlayerInputController();
        cameraController = new PlayerCameraController(worldCamera, cameraFollowDeadZone, cameraFollowOffset);
        stateMachine = new PlayerStateMachine();
        visualAnimator ??= GetComponentInChildren<Animator>(true);
        if (visualRenderer == null && visualAnimator != null) visualRenderer = visualAnimator.GetComponent<SpriteRenderer>();
        perfectDodgeAfterimage ??= GetComponentInChildren<PerfectDodgeAfterimage>(true);
        if (killChainRangeOverlay == null && arrowRoot != null) killChainRangeOverlay = arrowRoot.GetComponent<SpriteRenderer>();
        if (visualAnimator != null)
        {
            animatorBaseSpeed = visualAnimator.speed;
            visualAnimator.SetBool(IsDeadAnimatorParam, false);
        }

        stateMachine.Changed += OnStateChanged;
        IgnoreExistingEnemyCollisions();
        SetArrowVisible(false);
        HideRangeOverlayImmediately();
        SetTargetPresentation(null);
        UpdateChainWindowPresentation();
    }

    private void Update()
    {
        input.Tick();
        if (isDead) return;

        bool presentationControlsCharacter = presentationIdleActive || presentationLocomotionActive;
        if (!bossGuardControlLocked && !presentationControlsCharacter) TickCurrentState();
        TickSharedSystems();
    }

    private void TickCurrentState()
    {
        switch (State)
        {
            case PlayerStateId.Locomotion:
                HandleLocomotionInput();
                break;
            case PlayerStateId.PerfectDodgeFreeze:
                HandlePerfectDodgeFreeze();
                break;
            case PlayerStateId.KillChainTargeting:
                HandleTargeting();
                break;
            case PlayerStateId.KillChainDash:
                HandleDashInputBuffer();
                break;
            case PlayerStateId.KillChainImpact:
                HandleKillImpact();
                break;
            case PlayerStateId.UltimateTargeting:
                HandleUltimateTargeting();
                break;
            case PlayerStateId.UltimateExecution:
                HandleUltimateExecution();
                break;
            case PlayerStateId.UltimateFinisher:
                HandleUltimateFinisher();
                break;
        }
    }

    private void TickSharedSystems()
    {
        UpdateEnemyTimeScale();
        UpdateKillChainAudio();
        Transform focusTarget = lockedDashTarget != null ? lockedDashTarget : currentTarget;
        if (IsUltimateActive && ultimateExecutionIndex < ultimateTargets.Count)
            focusTarget = ultimateTargets[ultimateExecutionIndex];
        cameraController.SetFocus(body.position, focusTarget);
        cameraController.Tick(Time.unscaledDeltaTime);
        UpdateTargetPresentation();
        UpdateChainWindowPresentation();
        UpdateRangeOverlayPresentation();
        UpdateUltimatePresentation();
        UpdateVisuals();
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        if (bossGuardKnockbackActive)
        {
            UpdateBossGuardKnockback();
            return;
        }
        if (bossGuardControlLocked) return;
        if (presentationIdleActive || presentationLocomotionActive) return;
        if (IsUltimateActive) return;

        FixedTickCurrentState();
    }

    private void FixedTickCurrentState()
    {
        if (State == PlayerStateId.Dodge)
        {
            CheckPerfectDodgeDistance();
            if (State == PlayerStateId.Dodge) UpdateDodge();
            return;
        }

        if (State == PlayerStateId.KillChainDash)
        {
            UpdateKillChainDash();
            return;
        }

        if (State == PlayerStateId.Locomotion && input.Move.sqrMagnitude > 0f)
            Move(input.Move * MoveSpeed * Time.fixedDeltaTime);
    }

    private Transform FindEnemy(Transform candidate)
    {
        EnemyAgent agent = candidate != null ? candidate.GetComponentInParent<EnemyAgent>() : null;
        if (agent != null) return agent.transform;

        while (candidate != null)
        {
            if (candidate.name == enemyNamePrefix || candidate.name.StartsWith(enemyNamePrefix + " (")) return candidate;
            candidate = candidate.parent;
        }

        return null;
    }

    private static void KillEnemy(Transform enemy)
    {
        if (enemy == null) return;

        EnemyAgent agent = enemy.GetComponentInParent<EnemyAgent>();
        if (agent != null)
        {
            agent.Die();
            return;
        }

        Destroy(enemy.gameObject);
    }

    private void OnDisable()
    {
        CancelBossGuardReaction();
        RestoreAllUltimateTargetColors();
        RestoreUltimateLinePresentation();
        ultimateTargets.Clear();
        ultimateTargetSet.Clear();
        ultimateBossesInsideSwipe.Clear();
        ultimateBossesTouchedThisSegment.Clear();
        ultimateTrailPoints.Clear();
        EnemyTimeScale = 1f;
        enemyTimeScaleTarget = 1f;
        chainWindowRemaining = 0f;
        chainWindowDuration = 0f;
        presentationIdleActive = false;
        presentationLocomotionActive = false;
        if (stateMachine != null) stateMachine.Change(PlayerStateId.Locomotion);
        if (visualAnimator != null) visualAnimator.speed = animatorBaseSpeed;
        SetArrowVisible(false);
        HideRangeOverlayImmediately();
        SetTargetPresentation(null);
        perfectDodgeAfterimage?.StopAndRestore();
        GameAudioManager.StopSfxLoop(BulletTimeLoopSfx);
        cameraController?.RestoreImmediately();
    }

    private void OnDestroy()
    {
        if (stateMachine != null) stateMachine.Changed -= OnStateChanged;
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    private static float PerpendicularDistanceToAim(Vector2 origin, Vector2 aim, Vector2 point)
    {
        if (aim.sqrMagnitude <= Mathf.Epsilon) return Vector2.Distance(origin, point);
        Vector2 direction = aim.normalized;
        Vector2 offset = point - origin;
        return Mathf.Abs(direction.x * offset.y - direction.y * offset.x);
    }

    private static bool IsKillChainState(PlayerStateId state)
    {
        return state == PlayerStateId.PerfectDodgeFreeze
            || state == PlayerStateId.KillChainTargeting
            || state == PlayerStateId.KillChainDash
            || state == PlayerStateId.KillChainImpact;
    }

    private static bool IsUltimateState(PlayerStateId state)
    {
        return state == PlayerStateId.UltimateTargeting
            || state == PlayerStateId.UltimateExecution
            || state == PlayerStateId.UltimateFinisher;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEnemyTimeScale() => EnemyTimeScale = 1f;
}
