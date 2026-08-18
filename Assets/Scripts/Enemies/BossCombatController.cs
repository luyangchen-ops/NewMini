using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public enum BossNodeStatus
{
    Running,
    Success,
    Failure
}

public abstract class BossBehaviorNode
{
    public abstract BossNodeStatus Tick();
    public virtual void Reset() { }
}

public sealed class BossConditionNode : BossBehaviorNode
{
    private readonly Func<bool> condition;

    public BossConditionNode(Func<bool> condition) => this.condition = condition;

    public override BossNodeStatus Tick() => condition()
        ? BossNodeStatus.Success
        : BossNodeStatus.Failure;
}

public sealed class BossActionNode : BossBehaviorNode
{
    private readonly Func<BossNodeStatus> action;
    private readonly Action reset;

    public BossActionNode(Func<BossNodeStatus> action, Action reset = null)
    {
        this.action = action;
        this.reset = reset;
    }

    public override BossNodeStatus Tick() => action();
    public override void Reset() => reset?.Invoke();
}

public sealed class BossSequenceNode : BossBehaviorNode
{
    private readonly IReadOnlyList<BossBehaviorNode> children;
    private int currentIndex;

    public BossSequenceNode(params BossBehaviorNode[] children) => this.children = children;

    public override BossNodeStatus Tick()
    {
        while (currentIndex < children.Count)
        {
            BossNodeStatus status = children[currentIndex].Tick();
            if (status == BossNodeStatus.Running) return status;
            if (status == BossNodeStatus.Failure)
            {
                Reset();
                return status;
            }

            currentIndex++;
        }

        Reset();
        return BossNodeStatus.Success;
    }

    public override void Reset()
    {
        foreach (BossBehaviorNode child in children) child.Reset();
        currentIndex = 0;
    }
}

public sealed class BossPrioritySelectorNode : BossBehaviorNode
{
    private readonly IReadOnlyList<BossBehaviorNode> children;
    private int runningIndex = -1;

    public BossPrioritySelectorNode(params BossBehaviorNode[] children) => this.children = children;

    public override BossNodeStatus Tick()
    {
        for (int i = 0; i < children.Count; i++)
        {
            BossNodeStatus status = children[i].Tick();
            if (status == BossNodeStatus.Failure) continue;

            if (runningIndex != i)
            {
                if (runningIndex >= 0) children[runningIndex].Reset();
                runningIndex = status == BossNodeStatus.Running ? i : -1;
            }
            else if (status != BossNodeStatus.Running)
            {
                runningIndex = -1;
            }

            return status;
        }

        runningIndex = -1;
        return BossNodeStatus.Failure;
    }

    public override void Reset()
    {
        foreach (BossBehaviorNode child in children) child.Reset();
        runningIndex = -1;
    }
}

public enum BossHitResolution
{
    Guarded,
    Damaged,
    Lethal
}

/// <summary>
/// Boss-specific behavior tree and combat blackboard. The tree gives reactive guarding
/// priority over committed attacks, then alternates pressure, attacks, and readable openings.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossCombatController : MonoBehaviour
{
    private enum CombatAction
    {
        None,
        SingleAttack,
        TripleAttack,
        DashAttack
    }

    [Header("Standalone Boss Vitals")]
    [Tooltip("Hit count required to defeat a standalone Boss. Story Bosses keep using Borrowed Life instead.")]
    [SerializeField, Min(1)] private int hitsToDefeat = 20;

    [Header("Hurt Reaction")]
    [SerializeField, Min(.05f)] private float hurtDuration = .6f;
    [SerializeField, Min(1)] private int hurtReactionsBeforeLockout = 3;
    [SerializeField, Min(.05f)] private float consecutiveHurtWindow = 2f;
    [SerializeField, Min(0f)] private float hurtReactionLockoutDuration = 2f;

    [Header("Reactive Guard")]
    [SerializeField, Range(0f, 1f)] private float guardChance = .3f;
    [SerializeField, Min(.05f)] private float guardDuration = 1.2f;
    [SerializeField, Min(0f)] private float guardKnockbackDelay = .9f;
    [SerializeField, Min(.01f)] private float guardKnockbackDuration = .18f;
    [SerializeField, Min(0f)] private float guardKnockbackDistance = 2.2f;
    [SerializeField] private Vector2 guardOpeningDurationRange = new(.38f, .58f);

    [Header("Pressure Loop")]
    [SerializeField, Min(.1f)] private float attackDistance = 2.05f;
    [SerializeField, Min(.1f)] private float preferredOpeningDistance = 2.7f;
    [SerializeField, Min(0f)] private float approachSpeedMultiplier = 1.08f;
    [SerializeField, Min(0f)] private float openingMoveSpeed = 2.2f;
    [SerializeField, Range(0f, 1f)] private float approachWeaveStrength = .22f;
    [SerializeField] private Vector2 initialDecisionDelayRange = new(.35f, .55f);
    [Tooltip("How long the Boss stands in Idle after repositioning and before resuming pursuit or attacking.")]
    [SerializeField] private Vector2 postOpeningIdleDurationRange = new(.25f, .45f);
    [SerializeField] private Vector2 singleOpeningDurationRange = new(.62f, .92f);
    [SerializeField] private Vector2 tripleOpeningDurationRange = new(1f, 1.35f);
    [SerializeField] private Vector2 dashOpeningDurationRange = new(1.1f, 1.5f);

    [Header("Attack Selection")]
    [SerializeField, Range(0f, 1f)] private float singleAttackWeight = .46f;
    [SerializeField, Range(0f, 1f)] private float tripleAttackWeight = .34f;
    [SerializeField, Range(0f, 1f)] private float dashAttackWeight = .2f;
    [SerializeField, Min(0f)] private float tripleAttackCooldown = 2.8f;
    [SerializeField, Min(0f)] private float dashAttackCooldown = 4.2f;

    [Header("Single Attack")]
    [SerializeField, Min(.01f)] private float singleHitTime = .36f;
    [SerializeField, Min(.05f)] private float singleAttackDuration = .78f;

    [Header("Fast Triple Attack")]
    [SerializeField, Min(.05f)] private float tripleStrikeInterval = .3f;
    [SerializeField, Min(.01f)] private float tripleStrikeHitDelay = .15f;
    [SerializeField, Min(.05f)] private float tripleRecoveryDuration = .38f;

    [Header("Dash Attack")]
    [SerializeField, Min(.1f)] private float dashMinimumDistance = 2.2f;
    [SerializeField, Min(.1f)] private float dashMaximumDistance = 5.8f;
    [SerializeField, Min(.05f)] private float dashWindupDuration = .42f;
    [SerializeField, Min(.05f)] private float dashTravelDuration = .4f;
    [SerializeField, Min(0f)] private float dashSpeed = 15f;
    [SerializeField, Range(0f, 1f)] private float dashImpactNormalizedTime = 1f;
    [SerializeField, Min(0f)] private float dashRecoveryDuration = .62f;

    [Header("Melee Hitbox")]
    [SerializeField, Min(0f)] private float meleeHitboxForwardOffset = .9f;
    [SerializeField] private Vector2 meleeHitboxSize = new(1.9f, 2.2f);

    private EnemyAgent agent;
    private Animator visualAnimator;
    private PlayerCharacterController player;
    private BossBehaviorNode behaviorTree;
    private CombatAction currentAction;
    private CombatAction previousAction;
    private int receivedDamageCount;
    private int consecutiveHurtReactionCount;
    private int tripleStrikeIndex;
    private float actionElapsed;
    private float openingEndTime;
    private float decisionReadyTime;
    private float hurtEndTime;
    private float lastHurtReactionTime = float.NegativeInfinity;
    private float hurtReactionLockoutEndTime;
    private float guardEndTime;
    private float guardKnockbackReleaseTime;
    private float tripleReadyTime;
    private float dashReadyTime;
    private float activeDashTravelDuration;
    private float openingStrafeSign = 1f;
    private bool actionDamageDealt;
    private bool guardKnockbackReleased = true;
    private Vector2 guardKnockbackDirection = Vector2.right;
    private Vector2 swingDirection = Vector2.right;
    private Vector2 dashDirection = Vector2.right;
    private Vector3 startingPosition;
    private Quaternion startingRotation;
    private bool supportsHurtAnimation;
    private bool supportsGuardAnimation;
    private bool supportsTripleAnimation;
    private bool supportsDashAnimation;

    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Hurt = Animator.StringToHash("Hurt");
    private static readonly int Guard = Animator.StringToHash("Guard");
    private static readonly int TripleAttack = Animator.StringToHash("TripleAttack");
    private static readonly int DashAttack = Animator.StringToHash("DashAttack");
    private static readonly int Idle = Animator.StringToHash("Idle");

    public bool UsesBehaviorTree => true;
    public bool IsHurt => Time.time < hurtEndTime;
    public bool IsGuarding => Time.time < guardEndTime;
    public bool IsCounterGuarding => IsGuarding;

    private bool HasTarget => agent != null && agent.HasTarget && player != null && !agent.IsDead;
    private float TargetDistance => HasTarget
        ? Vector2.Distance(agent.Body.position, player.transform.position)
        : float.PositiveInfinity;

    private void Awake()
    {
        agent = GetComponent<EnemyAgent>();
        visualAnimator = GetComponentInChildren<Animator>();
        player = FindAnyObjectByType<PlayerCharacterController>();
        startingPosition = transform.position;
        startingRotation = transform.rotation;
        CacheAnimatorParameters();
        BuildBehaviorTree();
        decisionReadyTime = Time.time + RandomRange(initialDecisionDelayRange);
    }

    private void Start()
    {
        if (RespawnPointManager.Instance != null)
            RespawnPointManager.Instance.PlayerRespawned += ResetForCheckpointRetry;
    }

    private void Update()
    {
        if (!guardKnockbackReleased && Time.time >= guardKnockbackReleaseTime)
            ReleaseGuardKnockback();
        if (agent == null || agent.IsDead) return;
        player ??= FindAnyObjectByType<PlayerCharacterController>();
        if (!HasTarget)
        {
            agent.SetDesiredVelocity(Vector2.zero);
            return;
        }

        behaviorTree.Tick();
    }

    private void OnDestroy()
    {
        player?.CancelBossGuardReaction();
        if (RespawnPointManager.Instance != null)
            RespawnPointManager.Instance.PlayerRespawned -= ResetForCheckpointRetry;
    }

    private void BuildBehaviorTree()
    {
        BossBehaviorNode hurtBranch = new BossSequenceNode(
            new BossConditionNode(() => IsHurt),
            new BossActionNode(TickHurt));

        BossBehaviorNode guardBranch = new BossSequenceNode(
            new BossConditionNode(() => IsGuarding),
            new BossActionNode(TickGuard));

        BossBehaviorNode committedActionBranch = new BossSequenceNode(
            new BossConditionNode(() => currentAction != CombatAction.None),
            new BossActionNode(TickCommittedAction));

        BossBehaviorNode openingBranch = new BossSequenceNode(
            new BossConditionNode(() => Time.time < openingEndTime),
            new BossActionNode(TickOpening));

        BossBehaviorNode decisionPauseBranch = new BossSequenceNode(
            new BossConditionNode(() => Time.time < decisionReadyTime),
            new BossActionNode(TickDecisionPause));

        BossBehaviorNode approachBranch = new BossSequenceNode(
            new BossConditionNode(ShouldApproach),
            new BossActionNode(TickApproach));

        BossBehaviorNode combatSelector = new BossPrioritySelectorNode(
            hurtBranch,
            guardBranch,
            committedActionBranch,
            openingBranch,
            decisionPauseBranch,
            approachBranch,
            new BossActionNode(ChooseNextAttack));

        behaviorTree = new BossSequenceNode(
            new BossConditionNode(() => HasTarget),
            combatSelector);
    }

    /// <summary>Resolves defense and standalone health at the actual player-hit boundary.</summary>
    public BossHitResolution ResolvePlayerAttack(Vector2 attackerPosition, bool allowGuard)
    {
        if (allowGuard && TryGuardPlayerAttack(attackerPosition)) return BossHitResolution.Guarded;
        if (GetComponent<BorrowedLifeBossController>() != null) return BossHitResolution.Lethal;

        receivedDamageCount++;
        return receivedDamageCount >= hitsToDefeat
            ? BossHitResolution.Lethal
            : BossHitResolution.Damaged;
    }

    /// <summary>Interrupts the current action and plays the non-lethal Boss damage reaction.</summary>
    public void PlayHurtReaction()
    {
        if (agent == null || agent.IsDead) return;
        if (Time.time < hurtReactionLockoutEndTime)
        {
            StartHurtLockoutCounterattack();
            return;
        }

        if (Time.time - lastHurtReactionTime > consecutiveHurtWindow)
            consecutiveHurtReactionCount = 0;

        lastHurtReactionTime = Time.time;
        consecutiveHurtReactionCount++;

        ReleaseGuardKnockback();
        CancelCurrentAction();
        guardEndTime = 0f;
        openingEndTime = 0f;
        hurtEndTime = Time.time + hurtDuration;
        decisionReadyTime = hurtEndTime + RandomRange(postOpeningIdleDurationRange);
        agent.CancelBossBehaviorAttack();
        TriggerHurtAnimation();
        behaviorTree.Reset();

        if (consecutiveHurtReactionCount < hurtReactionsBeforeLockout) return;

        consecutiveHurtReactionCount = 0;
        hurtReactionLockoutEndTime = Time.time + hurtReactionLockoutDuration;
    }

    private void StartHurtLockoutCounterattack()
    {
        if (!HasTarget || currentAction != CombatAction.None) return;

        ReleaseGuardKnockback();
        hurtEndTime = 0f;
        guardEndTime = 0f;
        openingEndTime = 0f;
        decisionReadyTime = 0f;
        agent.CancelBossBehaviorAttack();
        behaviorTree.Reset();
        StartAction(CombatAction.SingleAttack);
    }

    private bool TryGuardPlayerAttack(Vector2 attackerPosition)
    {
        if (agent == null || agent.IsDead || IsGuarding || Random.value >= guardChance) return false;

        CancelCurrentAction();
        float activeGuardDuration = Mathf.Max(guardDuration, guardKnockbackDelay + guardKnockbackDuration);
        guardEndTime = Time.time + activeGuardDuration;
        guardKnockbackReleaseTime = Time.time + guardKnockbackDelay;
        guardKnockbackReleased = false;
        agent.CancelBossBehaviorAttack();
        TriggerGuardAnimation();

        player ??= FindAnyObjectByType<PlayerCharacterController>();
        if (player != null)
        {
            Vector2 awayFromBoss = attackerPosition - (Vector2)transform.position;
            if (awayFromBoss.sqrMagnitude <= .0001f)
                awayFromBoss = player.transform.position.x < transform.position.x ? Vector2.left : Vector2.right;
            guardKnockbackDirection = awayFromBoss.normalized;
            player.BeginBossGuardStun();
        }
        else guardKnockbackReleased = true;

        behaviorTree.Reset();
        return true;
    }

    public void BeginMeleeSwing()
    {
        if (!HasTarget) return;
        swingDirection = player.transform.position.x < transform.position.x ? Vector2.left : Vector2.right;
    }

    public bool IsCurrentMeleeSwingHittingPlayer(PlayerCharacterController targetPlayer)
    {
        if (targetPlayer == null || agent?.Body == null) return false;

        Vector2 size = new(Mathf.Max(.01f, meleeHitboxSize.x), Mathf.Max(.01f, meleeHitboxSize.y));
        Vector2 center = agent.Body.position + swingDirection * meleeHitboxForwardOffset;
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            if (hit != null && hit.GetComponent<PlayerCharacterController>() == targetPlayer) return true;
        }

        return false;
    }

    // Legacy generic-melee entry points remain no-ops because Boss attack chaining
    // is now owned entirely by the behavior tree.
    public void BeginAttackSequence() { }
    public bool TryContinueAttackSequence() => false;

    private BossNodeStatus TickHurt()
    {
        if (!IsHurt) return BossNodeStatus.Success;
        agent.SetDesiredVelocity(Vector2.zero);
        agent.FaceTarget();
        return BossNodeStatus.Running;
    }

    private BossNodeStatus TickGuard()
    {
        agent.SetDesiredVelocity(Vector2.zero);
        agent.FaceTarget();
        if (!guardKnockbackReleased && Time.time >= guardKnockbackReleaseTime)
            ReleaseGuardKnockback();
        if (IsGuarding) return BossNodeStatus.Running;

        ReleaseGuardKnockback();
        BeginOpening(guardOpeningDurationRange);
        return BossNodeStatus.Success;
    }

    private void ReleaseGuardKnockback()
    {
        if (guardKnockbackReleased) return;

        guardKnockbackReleased = true;
        player?.ReceiveKnockback(guardKnockbackDirection, guardKnockbackDistance, guardKnockbackDuration);
    }

    private BossNodeStatus TickCommittedAction()
    {
        return currentAction switch
        {
            CombatAction.SingleAttack => TickSingleAttack(),
            CombatAction.TripleAttack => TickTripleAttack(),
            CombatAction.DashAttack => TickDashAttack(),
            _ => BossNodeStatus.Failure
        };
    }

    private BossNodeStatus TickSingleAttack()
    {
        actionElapsed += agent.EnemyDeltaTime;
        agent.SetDesiredVelocity(Vector2.zero);
        agent.FaceTarget();

        if (!actionDamageDealt && actionElapsed >= singleHitTime)
        {
            actionDamageDealt = true;
            agent.PerformMeleeAttack();
        }

        if (actionElapsed < singleAttackDuration) return BossNodeStatus.Running;
        agent.CompleteMeleeAttack();
        FinishAction(singleOpeningDurationRange);
        return BossNodeStatus.Success;
    }

    private BossNodeStatus TickTripleAttack()
    {
        actionElapsed += agent.EnemyDeltaTime;
        agent.SetDesiredVelocity(Vector2.zero);
        agent.FaceTarget();

        float strikeStartTime = tripleStrikeIndex * tripleStrikeInterval;
        if (!actionDamageDealt && actionElapsed >= strikeStartTime + tripleStrikeHitDelay)
        {
            actionDamageDealt = true;
            agent.PerformMeleeAttack(false);
        }

        if (tripleStrikeIndex < 2 && actionElapsed >= (tripleStrikeIndex + 1) * tripleStrikeInterval)
        {
            tripleStrikeIndex++;
            actionDamageDealt = false;
            agent.BeginBossBehaviorAttack(tripleStrikeHitDelay, .2f, !supportsTripleAnimation);
        }

        float finishTime = tripleStrikeInterval * 2f + tripleStrikeHitDelay + tripleRecoveryDuration;
        if (actionElapsed < finishTime) return BossNodeStatus.Running;
        agent.CompleteMeleeAttack();
        tripleReadyTime = Time.time + tripleAttackCooldown;
        FinishAction(tripleOpeningDurationRange);
        return BossNodeStatus.Success;
    }

    private BossNodeStatus TickDashAttack()
    {
        actionElapsed += agent.EnemyDeltaTime;
        float travelElapsed = actionElapsed - dashWindupDuration;
        if (travelElapsed < 0f)
        {
            agent.SetDesiredVelocity(Vector2.zero);
            agent.FaceTarget();
            return BossNodeStatus.Running;
        }

        if (travelElapsed < activeDashTravelDuration)
        {
            agent.SetDesiredVelocity(dashDirection * dashSpeed);
            float impactTime = activeDashTravelDuration * dashImpactNormalizedTime;
            if (!actionDamageDealt && travelElapsed >= impactTime)
            {
                actionDamageDealt = true;
                agent.PerformMeleeAttack(false);
            }
            return BossNodeStatus.Running;
        }

        agent.SetDesiredVelocity(Vector2.zero);
        if (travelElapsed < activeDashTravelDuration + dashRecoveryDuration) return BossNodeStatus.Running;

        agent.CompleteMeleeAttack();
        dashReadyTime = Time.time + dashAttackCooldown;
        FinishAction(dashOpeningDurationRange);
        return BossNodeStatus.Success;
    }

    private BossNodeStatus TickOpening()
    {
        if (Time.time >= openingEndTime)
        {
            agent.SetDesiredVelocity(Vector2.zero);
            decisionReadyTime = Time.time + RandomRange(postOpeningIdleDurationRange);
            return BossNodeStatus.Success;
        }

        Vector2 away = ((Vector2)agent.Body.position - (Vector2)player.transform.position).normalized;
        if (away.sqrMagnitude <= .0001f) away = Vector2.left;
        Vector2 tangent = new(-away.y, away.x);
        float retreatStrength = TargetDistance < preferredOpeningDistance ? 1f : .22f;
        Vector2 openingDirection = (away * retreatStrength + tangent * openingStrafeSign * .52f).normalized;
        agent.SetDesiredVelocity(openingDirection * openingMoveSpeed);
        agent.FaceTarget();
        return BossNodeStatus.Running;
    }

    private BossNodeStatus TickDecisionPause()
    {
        agent.SetDesiredVelocity(Vector2.zero);
        agent.FaceTarget();
        return Time.time < decisionReadyTime ? BossNodeStatus.Running : BossNodeStatus.Success;
    }

    private BossNodeStatus TickApproach()
    {
        Vector2 towardPlayer = ((Vector2)player.transform.position - agent.Body.position).normalized;
        float weave = Mathf.Sin(Time.time * 3.1f) * approachWeaveStrength;
        Vector2 approach = new Vector2(towardPlayer.x, towardPlayer.y + weave).normalized;
        agent.SetDesiredVelocity(approach * agent.Data.MoveSpeed * approachSpeedMultiplier);
        return TargetDistance > attackDistance ? BossNodeStatus.Running : BossNodeStatus.Success;
    }

    private BossNodeStatus ChooseNextAttack()
    {
        CombatAction chosen = RollAttack();
        StartAction(chosen);
        return BossNodeStatus.Running;
    }

    private bool ShouldApproach()
    {
        float distance = TargetDistance;
        if (distance <= attackDistance) return false;

        bool canDashNow = Time.time >= dashReadyTime
            && distance >= dashMinimumDistance
            && distance <= dashMaximumDistance;
        return !canDashNow;
    }

    private CombatAction RollAttack()
    {
        float distance = TargetDistance;
        float singleWeight = distance <= attackDistance + .1f ? Mathf.Max(0f, singleAttackWeight) : 0f;
        float tripleWeight = Time.time >= tripleReadyTime && distance <= attackDistance + .35f
            ? Mathf.Max(0f, tripleAttackWeight)
            : 0f;
        float dashWeight = Time.time >= dashReadyTime
            && distance >= dashMinimumDistance
            && distance <= dashMaximumDistance
                ? Mathf.Max(0f, dashAttackWeight)
                : 0f;

        // Memory prevents identical specials from producing a mechanical repeating loop.
        if (previousAction == CombatAction.TripleAttack) tripleWeight *= .2f;
        if (previousAction == CombatAction.DashAttack) dashWeight *= .1f;

        float total = singleWeight + tripleWeight + dashWeight;
        if (total <= .0001f) return CombatAction.SingleAttack;
        float roll = Random.value * total;
        if (roll < dashWeight) return CombatAction.DashAttack;
        if (roll < dashWeight + tripleWeight) return CombatAction.TripleAttack;
        return CombatAction.SingleAttack;
    }

    private void StartAction(CombatAction action)
    {
        currentAction = action;
        actionElapsed = 0f;
        actionDamageDealt = false;
        tripleStrikeIndex = 0;

        switch (action)
        {
            case CombatAction.SingleAttack:
                agent.BeginBossBehaviorAttack(singleHitTime, .24f, true);
                break;
            case CombatAction.TripleAttack:
                agent.BeginBossBehaviorAttack(tripleStrikeHitDelay, .2f, false);
                TriggerTripleAnimation();
                break;
            case CombatAction.DashAttack:
                dashDirection = ((Vector2)player.transform.position - agent.Body.position).normalized;
                if (dashDirection.sqrMagnitude <= .0001f) dashDirection = Vector2.right;
                swingDirection = dashDirection.x < 0f ? Vector2.left : Vector2.right;
                float requestedTravelDistance = TargetDistance + meleeHitboxForwardOffset * .35f;
                activeDashTravelDuration = Mathf.Clamp(
                    requestedTravelDistance / Mathf.Max(.01f, dashSpeed),
                    .12f,
                    dashTravelDuration);
                agent.BeginBossBehaviorAttack(
                    dashWindupDuration + activeDashTravelDuration * dashImpactNormalizedTime,
                    .24f,
                    false);
                TriggerDashAnimation();
                break;
        }
    }

    private void FinishAction(Vector2 openingRange)
    {
        previousAction = currentAction;
        currentAction = CombatAction.None;
        BeginOpening(openingRange);
    }

    private void CancelCurrentAction()
    {
        currentAction = CombatAction.None;
        actionElapsed = 0f;
        actionDamageDealt = false;
        tripleStrikeIndex = 0;
        activeDashTravelDuration = 0f;
    }

    private void BeginOpening(Vector2 durationRange)
    {
        openingStrafeSign = Random.value < .5f ? -1f : 1f;
        openingEndTime = Time.time + RandomRange(durationRange);
    }

    private void TriggerHurtAnimation()
    {
        if (visualAnimator == null || !supportsHurtAnimation) return;
        visualAnimator.ResetTrigger(Attack);
        if (supportsGuardAnimation) visualAnimator.ResetTrigger(Guard);
        if (supportsTripleAnimation) visualAnimator.ResetTrigger(TripleAttack);
        if (supportsDashAnimation) visualAnimator.ResetTrigger(DashAttack);
        visualAnimator.ResetTrigger(Hurt);
        visualAnimator.SetTrigger(Hurt);
    }

    private void TriggerGuardAnimation()
    {
        if (visualAnimator == null) return;
        if (!supportsGuardAnimation)
        {
            ReturnToIdleAnimation();
            return;
        }

        visualAnimator.ResetTrigger(Attack);
        if (supportsHurtAnimation) visualAnimator.ResetTrigger(Hurt);
        if (supportsTripleAnimation) visualAnimator.ResetTrigger(TripleAttack);
        if (supportsDashAnimation) visualAnimator.ResetTrigger(DashAttack);
        visualAnimator.ResetTrigger(Guard);
        visualAnimator.SetTrigger(Guard);
    }

    private void ReturnToIdleAnimation()
    {
        if (visualAnimator == null) return;
        visualAnimator.ResetTrigger(Attack);
        if (supportsHurtAnimation) visualAnimator.ResetTrigger(Hurt);
        if (supportsGuardAnimation) visualAnimator.ResetTrigger(Guard);
        if (supportsTripleAnimation) visualAnimator.ResetTrigger(TripleAttack);
        if (supportsDashAnimation) visualAnimator.ResetTrigger(DashAttack);
        visualAnimator.CrossFade(Idle, .08f);
    }

    private void TriggerTripleAnimation()
    {
        if (visualAnimator == null) return;
        if (supportsGuardAnimation) visualAnimator.ResetTrigger(Guard);
        if (supportsTripleAnimation)
        {
            visualAnimator.ResetTrigger(Attack);
            visualAnimator.ResetTrigger(TripleAttack);
            visualAnimator.SetTrigger(TripleAttack);
        }
        else
        {
            visualAnimator.ResetTrigger(Attack);
            visualAnimator.SetTrigger(Attack);
        }
    }

    private void TriggerDashAnimation()
    {
        if (visualAnimator == null) return;
        if (supportsGuardAnimation) visualAnimator.ResetTrigger(Guard);
        if (supportsTripleAnimation) visualAnimator.ResetTrigger(TripleAttack);
        if (supportsDashAnimation)
        {
            visualAnimator.ResetTrigger(DashAttack);
            visualAnimator.SetTrigger(DashAttack);
        }
        else
        {
            visualAnimator.ResetTrigger(Attack);
            visualAnimator.SetTrigger(Attack);
        }
    }

    private void CacheAnimatorParameters()
    {
        if (visualAnimator == null) return;
        foreach (AnimatorControllerParameter parameter in visualAnimator.parameters)
        {
            if (parameter.nameHash == Hurt && parameter.type == AnimatorControllerParameterType.Trigger)
                supportsHurtAnimation = true;
            else if (parameter.nameHash == Guard && parameter.type == AnimatorControllerParameterType.Trigger)
                supportsGuardAnimation = true;
            else if (parameter.nameHash == TripleAttack && parameter.type == AnimatorControllerParameterType.Trigger)
                supportsTripleAnimation = true;
            else if (parameter.nameHash == DashAttack && parameter.type == AnimatorControllerParameterType.Trigger)
                supportsDashAnimation = true;
        }
    }

    private void ResetForCheckpointRetry()
    {
        player?.CancelBossGuardReaction();
        receivedDamageCount = 0;
        consecutiveHurtReactionCount = 0;
        currentAction = CombatAction.None;
        previousAction = CombatAction.None;
        actionElapsed = 0f;
        hurtEndTime = hurtReactionLockoutEndTime = guardEndTime = openingEndTime = tripleReadyTime = dashReadyTime = 0f;
        lastHurtReactionTime = float.NegativeInfinity;
        guardKnockbackReleaseTime = 0f;
        guardKnockbackReleased = true;
        decisionReadyTime = Time.time + RandomRange(initialDecisionDelayRange);
        transform.SetPositionAndRotation(startingPosition, startingRotation);
        behaviorTree.Reset();

        agent?.CancelBossBehaviorAttack();
        if (visualAnimator == null) return;
        visualAnimator.Rebind();
        visualAnimator.Update(0f);
    }

    private static float RandomRange(Vector2 range)
    {
        float minimum = Mathf.Min(range.x, range.y);
        float maximum = Mathf.Max(range.x, range.y);
        return Random.Range(Mathf.Max(0f, minimum), Mathf.Max(0f, maximum));
    }
}
