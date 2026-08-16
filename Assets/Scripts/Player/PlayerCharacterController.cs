using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D), typeof(PlayerSpecialItemInventory))]
public class PlayerCharacterController : MonoBehaviour
{
    public static float EnemyTimeScale { get; private set; } = 1f;

    [Header("3C Data")]
    [SerializeField] private PlayerCharacterData characterData;

    [Header("Legacy Overrides (used while Character Data is empty)")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float boundaryPadding = .7f;
    [SerializeField] private Camera worldCamera;
    [SerializeField, Range(.1f, 1f)] private float cameraFollowDeadZone = .65f;
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
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource bulletTimeLoopSource;
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
    private float dashElapsed;
    private float activeDashDuration;
    private float dashReadyTime;
    private float normalAttackReadyTime;
    private float stateTimer;
    private float chainWindowRemaining;
    private float bufferedTargetUntil;
    private float exitProtectionUntil;
    private float animatorBaseSpeed = 1f;
    private float enemyTimeScaleTarget = 1f;
    private float bulletTimeLoopEnvelope;
    private int killChainCount;
    private float currentHealth;
    private bool isDead;
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
    private PlayerSpecialItemInventory specialItems;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int VerticalDirection = Animator.StringToHash("VerticalDirection");
    private static readonly int NormalAttack = Animator.StringToHash("NormalAttack");
    private static readonly int DashAttack = Animator.StringToHash("DashAttack");
    private static readonly int Roll = Animator.StringToHash("Roll");
    private static readonly int Sheathe = Animator.StringToHash("Sheathe");
    private static readonly int IsDeadAnimatorParam = Animator.StringToHash("IsDead");
    private static readonly int ChainWindow01 = Shader.PropertyToID("_ChainWindow01");
    private static readonly int RangeRadius01 = Shader.PropertyToID("_RangeRadius01");
    private static readonly int EffectStrength = Shader.PropertyToID("_EffectStrength");

    public PlayerStateId State => stateMachine != null ? stateMachine.Current : PlayerStateId.Locomotion;
    public bool IsDodging => stateMachine != null && stateMachine.Is(PlayerStateId.Dodge);
    public bool IsKillChainActive => stateMachine != null && IsKillChainState(stateMachine.Current);
    public bool IsUltimateActive => stateMachine != null && IsUltimateState(stateMachine.Current);
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
    public float KillChainWindowNormalized => Mathf.Clamp01(chainWindowRemaining / KillChainMaximumWindow);

    public event Action<int> KillChainKillConfirmed;
    public event Action<int> KillChainFinished;
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
    private float AttackDashOvershoot => characterData != null ? characterData.AttackDashOvershoot : .3f;
    private float DashEnemyTimeScale => characterData != null ? characterData.DashEnemyTimeScale : .35f;
    private float BulletTimeEnterDuration => characterData != null ? characterData.BulletTimeEnterDuration : .12f;
    private float BulletTimeExitDuration => characterData != null ? characterData.BulletTimeExitDuration : .18f;
    private float PerfectDodgeFreezeDuration => characterData != null ? characterData.PerfectDodgeFreezeDuration : .05f;
    private float KillChainInitialWindow => characterData != null ? characterData.KillChainInitialWindow : .8f;
    private float KillChainMaximumWindow => characterData != null ? characterData.KillChainMaximumWindow : 1.2f;
    private float KillChainTimeRestore => characterData != null ? characterData.KillChainTimeRestorePerKill : .35f;
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
        cameraController = new PlayerCameraController(worldCamera, cameraFollowDeadZone);
        stateMachine = new PlayerStateMachine();
        visualAnimator ??= GetComponentInChildren<Animator>(true);
        if (visualRenderer == null && visualAnimator != null) visualRenderer = visualAnimator.GetComponent<SpriteRenderer>();
        perfectDodgeAfterimage ??= GetComponentInChildren<PerfectDodgeAfterimage>(true);
        ResolveAudioSources();
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

    private void OnEnable()
    {
        GameAudioSettings.VolumesChanged += ApplySfxVolume;
        ApplySfxVolume();
    }

    private void Update()
    {
        input.Tick();
        if (isDead) return;

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
        if (IsUltimateActive) return;

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

    private void HandleLocomotionInput()
    {
        UpdateFacing(input.Move);
        if (input.UltimatePressed && CanUseUltimate)
        {
            StartUltimate();
            return;
        }

        if (input.DodgePressed && Time.time >= dashReadyTime)
        {
            Vector2 direction = PointerWorld() - body.position;
            if (direction.sqrMagnitude > Mathf.Epsilon) StartDodge(direction.normalized);
            return;
        }

        if (!input.AttackPressed || Time.time < normalAttackReadyTime) return;

        Vector2 attackDirection = PointerWorld() - body.position;
        if (attackDirection.sqrMagnitude <= Mathf.Epsilon)
            attackDirection = visualRenderer != null && visualRenderer.flipX ? Vector2.left : Vector2.right;
        else
            attackDirection.Normalize();

        UpdateFacing(attackDirection);
        visualAnimator?.SetTrigger(NormalAttack);
        PlaySfx(normalAttackSfx);
        normalAttackReadyTime = Time.time + AttackCooldown;
        TryNormalAttackHit(attackDirection);
    }

    private void StartDodge(Vector2 direction)
    {
        dashStart = body.position;
        dashTarget = cameraController.Clamp(dashStart + direction * DodgeDistance, Padding, transform.position.z);
        dashElapsed = 0f;
        activeDashDuration = Mathf.Max(.01f, DodgeDuration);
        stateMachine.Change(PlayerStateId.Dodge);
        UpdateFacing(direction);
        visualAnimator?.SetTrigger(Roll);
        PlaySfx(rollSfx);
        dashReadyTime = Time.time + DodgeCooldown;
    }

    private void UpdateDodge()
    {
        dashElapsed += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(dashElapsed / activeDashDuration);
        body.MovePosition(Vector2.LerpUnclamped(dashStart, dashTarget, EaseOutCubic(t)));
        if (t >= 1f) stateMachine.Change(PlayerStateId.Locomotion);
    }

    public bool TryTriggerPerfectDodge(Vector2 projectilePosition)
    {
        float perfectRadius = DodgeDistance * PerfectRatio;
        if (!IsDodging || (body.position - projectilePosition).sqrMagnitude >= perfectRadius * perfectRadius) return false;

        body.linearVelocity = Vector2.zero;
        killChainCount = 0;
        chainWindowRemaining = Mathf.Min(KillChainInitialWindow, KillChainMaximumWindow);
        currentTarget = lockedDashTarget = bufferedTarget = lastKilledTarget = null;
        bufferedTargetUntil = 0f;
        stateTimer = PerfectDodgeFreezeDuration;
        stateMachine.Change(PlayerStateId.PerfectDodgeFreeze);
        PlaySfx(PerfectDodgeSfx != null ? PerfectDodgeSfx : parrySfx);
        perfectDodgeAfterimage?.Play(visualRenderer != null && visualRenderer.flipX);
        cameraController.BeginKillChain(CameraZoomFactor, CameraFocusOffset, CameraResponse,
            PerfectDodgeCameraShake, MaximumCameraShake);
        onKillChainStarted?.Invoke();
        return true;
    }

    private void HandlePerfectDodgeFreeze()
    {
        stateTimer -= Time.unscaledDeltaTime;
        if (stateTimer <= 0f) EnterTargeting();
    }

    private void EnterTargeting()
    {
        lockedDashTarget = null;
        stateMachine.Change(PlayerStateId.KillChainTargeting);
        SetCurrentTarget(FindBestTarget(null));
    }

    private void HandleTargeting()
    {
        if (input.CancelPressed)
        {
            EndKillChain();
            return;
        }

        chainWindowRemaining = Mathf.Max(0f, chainWindowRemaining - Time.unscaledDeltaTime);
        if (chainWindowRemaining <= 0f)
        {
            EndKillChain();
            return;
        }

        SetCurrentTarget(FindBestTarget(null));
        if (!input.PointerPressed) return;

        if (IsValidTarget(currentTarget))
            StartKillChainDash(currentTarget);
        else
            onInvalidKillChainTarget?.Invoke();
    }

    private void StartKillChainDash(Transform target)
    {
        if (!IsValidTarget(target)) return;

        lockedDashTarget = target;
        SetCurrentTarget(null);
        bufferedTarget = null;
        bufferedTargetUntil = 0f;
        perfectDodgeAfterimage?.StopAndRestore();
        dashStart = body.position;
        Vector2 targetOffset = (Vector2)target.position - dashStart;
        killDashDirection = targetOffset.sqrMagnitude > Mathf.Epsilon ? targetOffset.normalized : Vector2.right;
        RecalculateKillDashTarget();
        dashElapsed = -AttackDashWindupDuration;
        activeDashDuration = Mathf.Max(.01f, AttackDashDuration);
        stateMachine.Change(PlayerStateId.KillChainDash);
        UpdateFacing(killDashDirection);
        visualAnimator?.SetTrigger(DashAttack);
        PlaySfx(dashAttackSfx);
        PlaySfx(DashWindCutSfx, dashWindCutVolume, false);
    }

    private void HandleDashInputBuffer()
    {
        Transform candidate = FindBestTarget(lockedDashTarget);
        SetCurrentTarget(candidate);
        if (input.PointerPressed && IsValidTarget(candidate)) BufferTarget(candidate);
    }

    private void UpdateKillChainDash()
    {
        if (!IsTargetAlive(lockedDashTarget))
        {
            lockedDashTarget = null;
            EnterTargeting();
            return;
        }

        RecalculateKillDashTarget();
        dashElapsed += Time.fixedDeltaTime;
        if (dashElapsed <= 0f) return;
        float t = Mathf.Clamp01(dashElapsed / activeDashDuration);
        body.MovePosition(Vector2.LerpUnclamped(dashStart, dashTarget, EaseOutCubic(t)));
        if (t >= 1f) ConfirmKill(lockedDashTarget);
    }

    private void RecalculateKillDashTarget()
    {
        if (lockedDashTarget == null) return;
        Vector2 targetPosition = lockedDashTarget.position;
        Vector2 offset = targetPosition - dashStart;
        if (offset.sqrMagnitude > Mathf.Epsilon) killDashDirection = offset.normalized;
        dashTarget = cameraController.Clamp(
            targetPosition + killDashDirection * AttackDashOvershoot,
            Padding,
            transform.position.z);
    }

    private void ConfirmKill(Transform enemy)
    {
        if (!IsValidTarget(enemy))
        {
            EnterTargeting();
            return;
        }

        body.position = dashTarget;
        body.linearVelocity = Vector2.zero;
        lastKilledTarget = enemy;
        if (bufferedTarget == enemy) bufferedTarget = null;
        PlayBloodHitEffect(enemy, killDashDirection);
        SpecialItemDropSpawner.TryDropFromEnemy(enemy.position);
        Destroy(enemy.gameObject);
        lockedDashTarget = null;
        killChainCount++;
        AwardMomentum(killChainCount);
        chainWindowRemaining = Mathf.Min(KillChainMaximumWindow, chainWindowRemaining + KillChainTimeRestore);
        dashReadyTime = 0f;
        PlayKillSfx();
        cameraController.AddKillImpact(killDashDirection, KillCameraShake, killChainCount);
        onKillChainKillConfirmed?.Invoke(killChainCount);
        KillChainKillConfirmed?.Invoke(killChainCount);
        stateTimer = KillImpactFreezeDuration;
        stateMachine.Change(PlayerStateId.KillChainImpact);
    }

    private void HandleKillImpact()
    {
        Transform candidate = FindBestTarget(lastKilledTarget);
        SetCurrentTarget(candidate);
        if (input.PointerPressed && IsValidTarget(candidate)) BufferTarget(candidate);

        stateTimer -= Time.unscaledDeltaTime;
        if (stateTimer > 0f) return;

        if (bufferedTarget != null && Time.unscaledTime <= bufferedTargetUntil && IsValidTarget(bufferedTarget))
        {
            StartKillChainDash(bufferedTarget);
            return;
        }

        bufferedTarget = null;
        if (HasAnyTargetInRange(lastKilledTarget)) EnterTargeting();
        else EndKillChain();
    }

    private void BufferTarget(Transform target)
    {
        bufferedTarget = target;
        float actionTimeRemaining = State == PlayerStateId.KillChainDash
            ? Mathf.Max(0f, activeDashDuration - dashElapsed) + KillImpactFreezeDuration
            : Mathf.Max(0f, stateTimer);
        bufferedTargetUntil = Time.unscaledTime + actionTimeRemaining + KillChainInputBufferDuration;
    }

    private void EndKillChain()
    {
        int completedKills = killChainCount;
        SetCurrentTarget(null);
        lockedDashTarget = bufferedTarget = lastKilledTarget = null;
        chainWindowRemaining = 0f;
        exitProtectionUntil = Time.unscaledTime + KillChainExitProtection;
        cameraController.EndKillChain();
        perfectDodgeAfterimage?.StopAndRestore();
        if (completedKills >= 3 && HasNoActiveEnemies())
            PlaySheathePresentation();
        stateMachine.Change(PlayerStateId.Locomotion);
        onKillChainEnded?.Invoke(completedKills);
        KillChainFinished?.Invoke(completedKills);
    }

    private Transform FindBestTarget(Transform excludedTarget)
    {
        Vector2 playerPosition = body.position;
        Vector2 pointerPosition = PointerWorld();
        Vector2 aim = pointerPosition - playerPosition;
        float maximumAimDistance = Mathf.Max(.01f, MaximumAimDistance);
        if (aim.sqrMagnitude > maximumAimDistance * maximumAimDistance)
        {
            aim = aim.normalized * maximumAimDistance;
            pointerPosition = playerPosition + aim;
        }
        bool hasAim = aim.sqrMagnitude > .0001f;
        Transform bestTarget = null;
        float bestScore = float.PositiveInfinity;

        targetCandidates.Clear();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(playerPosition, AttackDashDistance))
        {
            Transform enemy = FindEnemy(hit.transform);
            if (enemy == null || enemy == excludedTarget || enemy == lastKilledTarget || !targetCandidates.Add(enemy)) continue;
            EnemyAgent agent = enemy.GetComponent<EnemyAgent>();
            if (agent != null && !agent.CanBeKilledBy(body.position, IsMomentumFull)) continue;

            Vector2 offset = (Vector2)enemy.position - playerPosition;
            float distance = offset.magnitude;
            if (distance > AttackDashDistance + .01f) continue;

            float pointerDistance = Vector2.Distance(pointerPosition, enemy.position);
            float angle = hasAim && offset.sqrMagnitude > Mathf.Epsilon ? Vector2.Angle(aim, offset) : 0f;
            bool directAssist = pointerDistance <= TargetAssistWorldRadius;
            if (!directAssist && (!hasAim || angle > TargetAssistMaximumAngle)) continue;

            float score = directAssist
                ? pointerDistance * .2f + distance * .01f
                : 100f + angle + PerpendicularDistanceToAim(playerPosition, aim, enemy.position) * .35f + distance * .02f;
            if (score >= bestScore) continue;
            bestScore = score;
            bestTarget = enemy;
        }

        return bestTarget;
    }

    private bool HasAnyTargetInRange(Transform excludedTarget)
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(body.position, AttackDashDistance))
        {
            Transform enemy = FindEnemy(hit.transform);
            if (enemy != null && enemy != excludedTarget && enemy != lastKilledTarget && IsValidTarget(enemy)) return true;
        }

        return false;
    }

    private bool IsValidTarget(Transform target)
    {
        if (!IsTargetAlive(target)) return false;
        if (((Vector2)target.position - body.position).sqrMagnitude > AttackDashDistance * AttackDashDistance + .01f)
            return false;
        EnemyAgent agent = target.GetComponent<EnemyAgent>();
        return agent == null || agent.CanBeKilledBy(body.position, IsMomentumFull);
    }

    private static bool IsTargetAlive(Transform target)
    {
        return target != null && target.gameObject.activeInHierarchy;
    }

    private void SetCurrentTarget(Transform target)
    {
        if (currentTarget == target) return;
        currentTarget = target;
        onKillChainTargetChanged?.Invoke(target);
    }

    private void CheckPerfectDodgeDistance()
    {
        float radius = DodgeDistance * PerfectRatio;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(body.position, radius))
        {
            EnemyProjectile projectile = hit.GetComponentInParent<EnemyProjectile>();
            if (projectile == null || !TryTriggerPerfectDodge(projectile.transform.position)) continue;
            projectile.IgnorePlayerCollisions(this);
            return;
        }

        foreach (Collider2D hit in Physics2D.OverlapCircleAll(body.position, radius))
        {
            EnemyAgent enemy = hit.GetComponentInParent<EnemyAgent>();
            if (enemy == null || !enemy.IsMeleeAttackPerfectDodgeable) continue;
            if (TryTriggerPerfectDodge(enemy.transform.position)) return;
        }
    }

    private void TryNormalAttackHit(Vector2 direction)
    {
        Transform closestEnemy = null;
        float closestDistanceSquared = float.PositiveInfinity;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(body.position, NormalAttackRange))
        {
            Transform enemy = FindEnemy(hit.transform);
            if (enemy == null) continue;
            Vector2 offset = (Vector2)enemy.position - body.position;
            if (offset.sqrMagnitude > Mathf.Epsilon
                && Vector2.Angle(direction, offset) > normalAttackArcAngle * .5f) continue;
            if (offset.sqrMagnitude >= closestDistanceSquared) continue;
            closestEnemy = enemy;
            closestDistanceSquared = offset.sqrMagnitude;
        }

        if (closestEnemy == null) return;
        EnemyAgent enemyAgent = closestEnemy.GetComponent<EnemyAgent>();
        if (enemyAgent != null && !enemyAgent.CanBeKilledBy(body.position, IsMomentumFull))
        {
            enemyAgent.BlockIncomingAttack();
            return;
        }
        PlayBloodHitEffect(closestEnemy, direction);
        SpecialItemDropSpawner.TryDropFromEnemy(closestEnemy.position);
        Destroy(closestEnemy.gameObject);
        AwardMomentum(0);
        PlaySfx(HitBladeFleshSfx, hitBladeFleshVolume);
        PlaySfx(killSfx);
    }

    /// <summary>Receives damage from an enemy. Enemy data currently supplies zero damage.</summary>
    public void TakeDamage(float damage)
    {
        if (damage <= 0f || isDead || IsInvulnerable) return;
        if (specialItems != null && specialItems.TryBlockAttack()) return;
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        HealthChanged?.Invoke(currentHealth, maximumHealth);
        if (currentHealth > 0f) return;

        isDead = true;
        body.linearVelocity = Vector2.zero;
        visualAnimator?.SetBool(IsDeadAnimatorParam, true);
        Died?.Invoke();
    }

    public void RespawnAt(Vector3 position)
    {
        isDead = false;
        transform.position = position;
        body.position = position;
        body.linearVelocity = Vector2.zero;
        currentHealth = maximumHealth;
        HealthChanged?.Invoke(currentHealth, maximumHealth);
        visualAnimator?.SetBool(IsDeadAnimatorParam, false);
        if (stateMachine != null) stateMachine.Change(PlayerStateId.Locomotion);
        EnemyTimeScale = 1f;
        enemyTimeScaleTarget = 1f;
        cameraController?.RestoreImmediately();
    }

    public void RestoreHealth(float amount)
    {
        if (amount <= 0f) return;
        currentHealth = Mathf.Min(maximumHealth, currentHealth + amount);
        HealthChanged?.Invoke(currentHealth, maximumHealth);
    }

    private void AwardMomentum(int comboLength)
    {
        int amount = momentumPerKill;
        if (comboLength >= comboRewardThreshold) amount += bonusMomentumPerComboKill;

        int previousMomentum = currentMomentum;
        currentMomentum = Mathf.Min(maximumMomentum, currentMomentum + amount);
        if (currentMomentum != previousMomentum)
            MomentumChanged?.Invoke(currentMomentum, maximumMomentum);
    }

    private void StartUltimate()
    {
        ultimateTargets.Clear();
        ultimateTargetSet.Clear();
        ultimateTrailPoints.Clear();
        ultimateMarkedRenderers.Clear();
        ultimateExecutionIndex = 0;
        ultimateExecutedKills = 0;
        ultimateSwipeStarted = false;
        ultimateMarkTimeRemaining = ultimateMarkDuration;
        body.linearVelocity = Vector2.zero;

        stateMachine.Change(PlayerStateId.UltimateTargeting);
        EnemyTimeScale = 0f;
        enemyTimeScaleTarget = 0f;
        BeginUltimateLinePresentation();
        cameraController.BeginKillChain(ultimateCameraZoomFactor, 0f, 22f, .08f, MaximumCameraShake * 1.5f);
        PlaySfx(PerfectDodgeSfx != null ? PerfectDodgeSfx : parrySfx);
        onUltimateStarted?.Invoke();
        UltimateStarted?.Invoke();
    }

    private void HandleUltimateTargeting()
    {
        if (input.CancelPressed)
        {
            EndUltimate(false);
            return;
        }

        ultimateMarkTimeRemaining = Mathf.Max(0f, ultimateMarkTimeRemaining - Time.unscaledDeltaTime);
        Vector2 pointerPosition = PointerWorld();
        if (input.PointerPressed)
        {
            ultimateSwipeStarted = true;
            ultimateTrailPoints.Clear();
            ultimateLastPointerPosition = pointerPosition;
            AddUltimateTrailPoint(pointerPosition);
            MarkUltimateTargetsAlong(pointerPosition, pointerPosition);
        }

        if (ultimateSwipeStarted && input.PointerHeld)
        {
            MarkUltimateTargetsAlong(ultimateLastPointerPosition, pointerPosition);
            if ((pointerPosition - ultimateLastPointerPosition).sqrMagnitude
                >= ultimateTrailPointDistance * ultimateTrailPointDistance)
            {
                AddUltimateTrailPoint(pointerPosition);
                ultimateLastPointerPosition = pointerPosition;
            }
        }

        if (ultimateSwipeStarted && input.PointerReleased)
        {
            AddUltimateTrailPoint(pointerPosition);
            if (ultimateTargets.Count > 0) CommitUltimate();
            else ResetUltimateSwipe();
            return;
        }

        if (ultimateMarkTimeRemaining > 0f) return;
        if (ultimateTargets.Count > 0) CommitUltimate();
        else EndUltimate(false);
    }

    private void MarkUltimateTargetsAlong(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);
        float sampleSpacing = Mathf.Max(.05f, ultimateMarkRadius * .5f);
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(distance / sampleSpacing));
        for (int sample = 0; sample <= sampleCount && ultimateTargets.Count < ultimateMaximumTargets; sample++)
        {
            Vector2 point = Vector2.Lerp(from, to, sample / (float)sampleCount);
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(point, ultimateMarkRadius))
            {
                Transform enemy = FindEnemy(hit.transform);
                if (!IsTargetAlive(enemy) || !ultimateTargetSet.Add(enemy)) continue;
                MarkUltimateTarget(enemy);
                if (ultimateTargets.Count >= ultimateMaximumTargets) break;
            }
        }
    }

    private void MarkUltimateTarget(Transform enemy)
    {
        ultimateTargets.Add(enemy);
        foreach (SpriteRenderer renderer in enemy.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null || ultimateMarkedRenderers.ContainsKey(renderer)) continue;
            ultimateMarkedRenderers.Add(renderer, renderer.color);
            renderer.color = Color.Lerp(renderer.color, ultimateMarkedColor, .78f);
        }

        Vector2 impactDirection = (Vector2)enemy.position - body.position;
        cameraController.AddKillImpact(impactDirection, .025f, ultimateTargets.Count);
        PlaySfx(HitBladeFleshSfx, .28f);
        onUltimateTargetMarked?.Invoke(enemy);
        UltimateTargetMarked?.Invoke(enemy, ultimateTargets.Count);
    }

    private void CommitUltimate()
    {
        if (ultimateTargets.Count == 0)
        {
            EndUltimate(false);
            return;
        }

        currentMomentum = 0;
        MomentumChanged?.Invoke(currentMomentum, maximumMomentum);
        ultimateExecutionIndex = 0;
        ultimateExecutedKills = 0;
        stateTimer = .08f;
        BuildUltimateExecutionTrail();
        stateMachine.Change(PlayerStateId.UltimateExecution);
        PlaySfx(DashWindCutSfx != null ? DashWindCutSfx : dashAttackSfx, 1f);
    }

    private void HandleUltimateExecution()
    {
        stateTimer -= Time.unscaledDeltaTime;
        if (stateTimer > 0f) return;

        while (ultimateExecutionIndex < ultimateTargets.Count
            && !IsTargetAlive(ultimateTargets[ultimateExecutionIndex]))
            ultimateExecutionIndex++;

        if (ultimateExecutionIndex >= ultimateTargets.Count)
        {
            BeginUltimateFinisher();
            return;
        }

        Transform target = ultimateTargets[ultimateExecutionIndex++];
        Vector2 targetPosition = target.position;
        Vector2 slashDirection = targetPosition - body.position;
        if (slashDirection.sqrMagnitude <= Mathf.Epsilon) slashDirection = Vector2.right;
        else slashDirection.Normalize();

        UpdateFacing(slashDirection);
        visualAnimator?.SetTrigger(DashAttack);
        perfectDodgeAfterimage?.Play(visualRenderer != null && visualRenderer.flipX);
        body.position = cameraController.Clamp(
            targetPosition + slashDirection * AttackDashOvershoot,
            Padding,
            transform.position.z);
        body.linearVelocity = Vector2.zero;
        RestoreUltimateTargetColor(target);
        PlayBloodHitEffect(target, slashDirection);
        SpecialItemDropSpawner.TryDropFromEnemy(target.position);
        Destroy(target.gameObject);
        ultimateExecutedKills++;

        cameraController.AddKillImpact(slashDirection, MaximumCameraShake, ultimateExecutedKills);
        PlaySfx(HitBladeFleshSfx, hitBladeFleshVolume);
        PlaySfx(KillConfirmSfx != null ? KillConfirmSfx : killSfx, killConfirmVolume, false);
        stateTimer = ultimateExecutionInterval;
    }

    private void BeginUltimateFinisher()
    {
        stateTimer = ultimateFinisherDuration;
        stateMachine.Change(PlayerStateId.UltimateFinisher);
        cameraController.AddKillImpact(Vector2.up, MaximumCameraShake * 1.4f, ultimateExecutedKills + 3);
    }

    private void HandleUltimateFinisher()
    {
        stateTimer -= Time.unscaledDeltaTime;
        if (ultimateLine != null)
        {
            float alpha = Mathf.Clamp01(stateTimer / Mathf.Max(.01f, ultimateFinisherDuration));
            ultimateLine.startColor = WithAlpha(ultimateTrailStartColor, alpha);
            ultimateLine.endColor = WithAlpha(ultimateTrailEndColor, alpha);
        }

        if (stateTimer <= 0f) EndUltimate(true);
    }

    private void ResetUltimateSwipe()
    {
        ultimateSwipeStarted = false;
        ultimateTrailPoints.Clear();
        if (ultimateLine != null) ultimateLine.positionCount = 0;
    }

    private void AddUltimateTrailPoint(Vector2 point)
    {
        if (ultimateTrailPoints.Count >= 256) ultimateTrailPoints.RemoveAt(0);
        ultimateTrailPoints.Add(new Vector3(point.x, point.y, transform.position.z - .2f));
        if (ultimateLine == null) return;
        ultimateLine.positionCount = ultimateTrailPoints.Count;
        ultimateLine.SetPositions(ultimateTrailPoints.ToArray());
    }

    private void BuildUltimateExecutionTrail()
    {
        ultimateTrailPoints.Clear();
        ultimateTrailPoints.Add(new Vector3(body.position.x, body.position.y, transform.position.z - .2f));
        foreach (Transform target in ultimateTargets)
        {
            if (!IsTargetAlive(target)) continue;
            Vector3 position = target.position;
            ultimateTrailPoints.Add(new Vector3(position.x, position.y, transform.position.z - .2f));
        }

        if (ultimateLine == null) return;
        ultimateLine.positionCount = ultimateTrailPoints.Count;
        ultimateLine.SetPositions(ultimateTrailPoints.ToArray());
    }

    private void BeginUltimateLinePresentation()
    {
        ultimateLine = arrowLine != null ? arrowLine : targetPathLine;
        if (ultimateLine == null) return;

        ultimateLineOriginalUseWorldSpace = ultimateLine.useWorldSpace;
        ultimateLineOriginalWidthMultiplier = ultimateLine.widthMultiplier;
        ultimateLineOriginalStartColor = ultimateLine.startColor;
        ultimateLineOriginalEndColor = ultimateLine.endColor;
        ultimateLineSettingsSaved = true;
        ultimateLine.useWorldSpace = true;
        ultimateLine.widthMultiplier = ultimateLineOriginalWidthMultiplier * ultimateTrailWidthMultiplier;
        ultimateLine.startColor = ultimateTrailStartColor;
        ultimateLine.endColor = ultimateTrailEndColor;
        ultimateLine.positionCount = 0;
        if (arrowRoot != null) arrowRoot.SetActive(true);
        if (arrowHead != null) arrowHead.gameObject.SetActive(false);
        ultimateLine.enabled = true;
    }

    private void RestoreUltimateLinePresentation()
    {
        if (ultimateLine != null)
        {
            ultimateLine.positionCount = 0;
            if (ultimateLineSettingsSaved)
            {
                ultimateLine.useWorldSpace = ultimateLineOriginalUseWorldSpace;
                ultimateLine.widthMultiplier = ultimateLineOriginalWidthMultiplier;
                ultimateLine.startColor = ultimateLineOriginalStartColor;
                ultimateLine.endColor = ultimateLineOriginalEndColor;
            }
        }

        ultimateLineSettingsSaved = false;
        ultimateLine = null;
        SetArrowVisible(false);
    }

    private void RestoreUltimateTargetColor(Transform target)
    {
        if (target == null) return;
        foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null || !ultimateMarkedRenderers.TryGetValue(renderer, out Color originalColor)) continue;
            renderer.color = originalColor;
            ultimateMarkedRenderers.Remove(renderer);
        }
    }

    private void RestoreAllUltimateTargetColors()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> marked in ultimateMarkedRenderers)
            if (marked.Key != null) marked.Key.color = marked.Value;
        ultimateMarkedRenderers.Clear();
    }

    private void UpdateUltimatePresentation()
    {
        if (!IsUltimateActive) return;

        float pulse = .58f + Mathf.Sin(Time.unscaledTime * 14f) * .2f;
        foreach (KeyValuePair<SpriteRenderer, Color> marked in ultimateMarkedRenderers)
        {
            if (marked.Key != null) marked.Key.color = Color.Lerp(marked.Value, ultimateMarkedColor, pulse);
        }

        if (ultimateLine != null && ultimateLineSettingsSaved && State != PlayerStateId.UltimateFinisher)
            ultimateLine.widthMultiplier = ultimateLineOriginalWidthMultiplier
                * ultimateTrailWidthMultiplier
                * (1f + Mathf.Sin(Time.unscaledTime * 18f) * .08f);
    }

    private void EndUltimate(bool completed)
    {
        int completedKills = ultimateExecutedKills;
        RestoreAllUltimateTargetColors();
        RestoreUltimateLinePresentation();
        ultimateTargets.Clear();
        ultimateTargetSet.Clear();
        ultimateTrailPoints.Clear();
        ultimateSwipeStarted = false;
        perfectDodgeAfterimage?.StopAndRestore();
        exitProtectionUntil = Time.unscaledTime + KillChainExitProtection;
        cameraController.EndKillChain();
        stateMachine.Change(PlayerStateId.Locomotion);

        if (!completed) return;
        PlaySheathePresentation();
        onUltimateFinished?.Invoke(completedKills);
        UltimateFinished?.Invoke(completedKills);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a *= Mathf.Clamp01(alpha);
        return color;
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

    private static bool HasNoActiveEnemies()
    {
        return FindObjectsByType<EnemyAgent>(FindObjectsInactive.Exclude).Length == 0;
    }

    private void PlayBloodHitEffect(Transform enemy, Vector2 slashDirection)
    {
        if (bloodHitEffectPrefab == null || enemy == null) return;

        Collider2D hitCollider = enemy.GetComponentInChildren<Collider2D>();
        Vector2 hitPosition = hitCollider != null ? hitCollider.ClosestPoint(body.position) : enemy.position;
        float targetSize = 1f;
        int sortingOrder = 1;
        foreach (SpriteRenderer renderer in enemy.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null) continue;
            targetSize = Mathf.Max(targetSize, renderer.bounds.size.x, renderer.bounds.size.y);
            sortingOrder = Mathf.Max(sortingOrder, renderer.sortingOrder + 1);
        }

        BloodHitEffect effect = Instantiate(bloodHitEffectPrefab);
        effect.PlayAt(hitPosition, slashDirection, targetSize, sortingOrder);
    }

    private void OnStateChanged(PlayerStateId previous, PlayerStateId next)
    {
        switch (next)
        {
            case PlayerStateId.PerfectDodgeFreeze:
            case PlayerStateId.KillChainImpact:
                enemyTimeScaleTarget = 0f;
                break;
            case PlayerStateId.KillChainTargeting:
                enemyTimeScaleTarget = BulletTimeScale;
                break;
            case PlayerStateId.KillChainDash:
                enemyTimeScaleTarget = DashEnemyTimeScale;
                break;
            case PlayerStateId.UltimateTargeting:
            case PlayerStateId.UltimateExecution:
            case PlayerStateId.UltimateFinisher:
                enemyTimeScaleTarget = 0f;
                break;
            default:
                enemyTimeScaleTarget = 1f;
                break;
        }

        if (visualAnimator != null)
            visualAnimator.speed = next == PlayerStateId.PerfectDodgeFreeze || next == PlayerStateId.KillChainImpact
                ? 0f
                : animatorBaseSpeed;

        if (!IsUltimateState(next)) SetArrowVisible(false);
    }

    private void UpdateEnemyTimeScale()
    {
        float duration = enemyTimeScaleTarget < EnemyTimeScale
            ? BulletTimeEnterDuration
            : BulletTimeExitDuration;
        EnemyTimeScale = Mathf.MoveTowards(
            EnemyTimeScale,
            enemyTimeScaleTarget,
            Time.unscaledDeltaTime / Mathf.Max(.01f, duration));
    }

    private void UpdateVisuals()
    {
        if (visualAnimator == null) return;
        visualAnimator.SetFloat(Speed, State == PlayerStateId.Locomotion ? input.Move.magnitude : 0f);
    }

    private void UpdateTargetPresentation()
    {
        Transform target = lockedDashTarget != null ? lockedDashTarget : currentTarget;
        SetTargetPresentation(target);
        if (target == null) return;

        Transform anchor = targetReticleAnchor != null
            ? targetReticleAnchor
            : targetReticleRoot != null ? targetReticleRoot.transform : null;
        if (anchor != null)
        {
            Vector3 position = anchor.position;
            anchor.position = new Vector3(target.position.x, target.position.y, position.z);
        }

        if (targetPathLine != null)
        {
            targetPathLine.positionCount = 2;
            targetPathLine.SetPosition(0, body.position);
            targetPathLine.SetPosition(1, target.position);
        }
    }

    private void SetTargetPresentation(Transform target)
    {
        bool visible = target != null && IsKillChainActive;
        if (targetReticleRoot != null) targetReticleRoot.SetActive(visible);
        if (targetPathLine != null && targetPathLine != ultimateLine) targetPathLine.enabled = visible;
    }

    private void UpdateChainWindowPresentation()
    {
        if (chainWindowRenderer == null) return;
        chainWindowRenderer.GetPropertyBlock(feedbackProperties);
        feedbackProperties.SetFloat(ChainWindow01, IsKillChainActive ? KillChainWindowNormalized : 0f);
        chainWindowRenderer.SetPropertyBlock(feedbackProperties);
    }

    private void Face(float x)
    {
        if (visualRenderer != null && Mathf.Abs(x) > .01f) visualRenderer.flipX = x < 0f;
    }

    private void UpdateFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude <= .0001f) return;

        // Vertical art is selected only when the vertical input is dominant. Horizontal
        // movement retains the existing flip-X presentation and horizontal clips.
        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
        {
            visualAnimator?.SetInteger(VerticalDirection, direction.y > 0f ? 1 : -1);
            return;
        }

        visualAnimator?.SetInteger(VerticalDirection, 0);
        Face(direction.x);
    }

    private Vector2 PointerWorld() => cameraController.ScreenToWorld(
        input.PointerScreenPosition,
        transform.position.z,
        body.position);

    private void Move(Vector2 delta)
    {
        body.MovePosition(cameraController.Clamp(body.position + delta, Padding, transform.position.z));
    }

    public void IgnoreEnemyCollisions(EnemyAgent enemy)
    {
        if (enemy == null) return;
        playerColliders ??= GetComponentsInChildren<Collider2D>(true);
        Collider2D[] enemyColliders = enemy.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D playerCollider in playerColliders)
        {
            if (playerCollider == null) continue;
            foreach (Collider2D enemyCollider in enemyColliders)
            {
                if (enemyCollider != null) Physics2D.IgnoreCollision(playerCollider, enemyCollider, true);
            }
        }
    }

    private void IgnoreExistingEnemyCollisions()
    {
        EnemyAgent[] enemies = FindObjectsByType<EnemyAgent>(FindObjectsInactive.Include);
        foreach (EnemyAgent enemy in enemies) IgnoreEnemyCollisions(enemy);
    }

    private void SetArrowVisible(bool visible)
    {
        bool sharesRootWithOverlay = arrowRoot != null && killChainRangeOverlay != null
            && killChainRangeOverlay.transform == arrowRoot.transform;
        if (arrowRoot != null && !sharesRootWithOverlay) arrowRoot.SetActive(visible);
        else if (sharesRootWithOverlay) arrowRoot.SetActive(true);
        if (arrowLine != null) arrowLine.enabled = visible;
        if (arrowHead != null) arrowHead.gameObject.SetActive(visible);
    }

    private void UpdateRangeOverlayPresentation()
    {
        if (killChainRangeOverlay == null) return;

        float slowRange = Mathf.Max(.01f, 1f - BulletTimeScale);
        float strength = Mathf.Clamp01((1f - EnemyTimeScale) / slowRange);
        bool visible = strength > .001f;
        killChainRangeOverlay.enabled = visible;
        if (!visible || killChainRangeOverlay.sprite == null) return;

        Transform overlayTransform = killChainRangeOverlay.transform;
        overlayTransform.position = new Vector3(body.position.x, body.position.y, transform.position.z - .1f);
        float spriteSize = Mathf.Max(.01f, killChainRangeOverlay.sprite.bounds.size.x);
        overlayTransform.localScale = Vector3.one * (rangeOverlayWorldDiameter / spriteSize);
        killChainRangeOverlay.GetPropertyBlock(feedbackProperties);
        feedbackProperties.SetFloat(RangeRadius01, AttackDashDistance / rangeOverlayWorldDiameter);
        feedbackProperties.SetFloat(EffectStrength, strength);
        killChainRangeOverlay.SetPropertyBlock(feedbackProperties);
    }

    private void HideRangeOverlayImmediately()
    {
        if (killChainRangeOverlay == null) return;
        killChainRangeOverlay.enabled = false;
        if (feedbackProperties == null) return;
        killChainRangeOverlay.GetPropertyBlock(feedbackProperties);
        feedbackProperties.SetFloat(EffectStrength, 0f);
        killChainRangeOverlay.SetPropertyBlock(feedbackProperties);
    }

    private void ResolveAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sfxSource == null && sources.Length > 0) sfxSource = sources[0];
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

        if (bulletTimeLoopSource == null)
        {
            foreach (AudioSource source in sources)
            {
                if (source == sfxSource) continue;
                bulletTimeLoopSource = source;
                break;
            }
        }

        if (bulletTimeLoopSource == null) bulletTimeLoopSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.priority = 64;
        bulletTimeLoopSource.playOnAwake = false;
        bulletTimeLoopSource.loop = true;
        bulletTimeLoopSource.spatialBlend = 0f;
        bulletTimeLoopSource.priority = 160;
        bulletTimeLoopSource.clip = BulletTimeLoopSfx;
        bulletTimeLoopSource.volume = 0f;
    }

    private void UpdateKillChainAudio()
    {
        if (bulletTimeLoopSource == null || BulletTimeLoopSfx == null) return;

        if (bulletTimeLoopSource.clip != BulletTimeLoopSfx) bulletTimeLoopSource.clip = BulletTimeLoopSfx;
        float slowRange = Mathf.Max(.01f, 1f - BulletTimeScale);
        bulletTimeLoopEnvelope = Mathf.Clamp01((1f - EnemyTimeScale) / slowRange);
        bulletTimeLoopSource.volume = bulletTimeLoopEnvelope * bulletTimeLoopVolume * GameAudioSettings.SfxVolume;

        if (bulletTimeLoopEnvelope > .001f)
        {
            if (!bulletTimeLoopSource.isPlaying) bulletTimeLoopSource.Play();
        }
        else if (bulletTimeLoopSource.isPlaying)
        {
            bulletTimeLoopSource.Stop();
        }
    }

    private void PlaySfx(AudioClip clip, float volumeScale = 1f, bool resetPitch = true)
    {
        if (sfxSource == null || clip == null) return;
        if (resetPitch) sfxSource.pitch = 1f;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    private void PlaySheathePresentation()
    {
        visualAnimator?.SetTrigger(Sheathe);
        PlaySfx(KillChainEndSfx, killChainEndVolume);
    }

    private void PlayKillSfx()
    {
        if (sfxSource == null) return;
        sfxSource.pitch = 1f + Mathf.Min(Mathf.Max(0, killChainCount - 1), 4) * .035f;
        if (HitBladeFleshSfx != null) sfxSource.PlayOneShot(HitBladeFleshSfx, hitBladeFleshVolume);
        AudioClip confirmation = KillConfirmSfx != null ? KillConfirmSfx : killSfx;
        if (confirmation != null) sfxSource.PlayOneShot(confirmation, killConfirmVolume);
    }

    private void ApplySfxVolume()
    {
        if (sfxSource != null) sfxSource.volume = GameAudioSettings.SfxVolume;
        if (bulletTimeLoopSource != null)
            bulletTimeLoopSource.volume = bulletTimeLoopEnvelope * bulletTimeLoopVolume * GameAudioSettings.SfxVolume;
    }

    private void OnDisable()
    {
        GameAudioSettings.VolumesChanged -= ApplySfxVolume;
        RestoreAllUltimateTargetColors();
        RestoreUltimateLinePresentation();
        ultimateTargets.Clear();
        ultimateTargetSet.Clear();
        ultimateTrailPoints.Clear();
        EnemyTimeScale = 1f;
        enemyTimeScaleTarget = 1f;
        if (stateMachine != null) stateMachine.Change(PlayerStateId.Locomotion);
        if (visualAnimator != null) visualAnimator.speed = animatorBaseSpeed;
        SetArrowVisible(false);
        HideRangeOverlayImmediately();
        SetTargetPresentation(null);
        perfectDodgeAfterimage?.StopAndRestore();
        if (bulletTimeLoopSource != null)
        {
            bulletTimeLoopSource.Stop();
            bulletTimeLoopSource.volume = 0f;
        }
        bulletTimeLoopEnvelope = 0f;
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
