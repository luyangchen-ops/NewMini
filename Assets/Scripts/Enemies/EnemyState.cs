using UnityEngine;

public abstract class EnemyState
{
    protected readonly EnemyAgent Agent;
    protected readonly EnemyStateMachine StateMachine;

    protected EnemyState(EnemyAgent agent, EnemyStateMachine stateMachine)
    {
        Agent = agent;
        StateMachine = stateMachine;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Tick() { }
    public virtual void FixedTick() { }
}

public sealed class EnemyIdleState : EnemyState
{
    public EnemyIdleState(EnemyAgent agent, EnemyStateMachine stateMachine) : base(agent, stateMachine) { }

    public override void Enter() => Agent.SetDesiredVelocity(Vector2.zero);

    public override void Tick()
    {
        if (Agent.HasTarget)
        {
            StateMachine.ChangeState(Agent.DefaultActiveState);
        }
    }
}

public sealed class EnemyChaseState : EnemyState
{
    public EnemyChaseState(EnemyAgent agent, EnemyStateMachine stateMachine) : base(agent, stateMachine) { }

    public override void Tick()
    {
        if (!Agent.HasTarget)
        {
            StateMachine.ChangeState(Agent.IdleState);
            return;
        }

        if (Agent.IsWaitingToEngageInMelee)
        {
            Agent.SetDesiredVelocity(Vector2.zero);
            return;
        }

        if (!Agent.CanPressureTarget())
        {
            Agent.CancelMeleeAttackPreparation();
            Agent.SetDesiredVelocity(Agent.GetMeleeWaitingRoamDirection() * Agent.Data.MoveSpeed);
            return;
        }

        Vector2 moveDirection = Agent.GetMeleeFormationMoveDirection(out _);
        bool isInAttackPosition = Agent.IsWithinMeleeAttackDistance();
        if (Agent.IsMeleeAttackRecovering)
        {
            Agent.CancelMeleeAttackPreparation();
            Agent.SetDesiredVelocity(Vector2.zero);
            return;
        }

        if (isInAttackPosition)
        {
            Agent.SetDesiredVelocity(Vector2.zero);
            if (Agent.TryBeginMeleeAttack())
            {
                StateMachine.ChangeState(Agent.AttackState);
            }
            return;
        }

        if (!isInAttackPosition) Agent.CancelMeleeAttackPreparation();

        Agent.SetDesiredVelocity(moveDirection * Agent.MeleeEngagementMoveSpeed);
    }
}

public sealed class EnemyRoamState : EnemyState
{
    private Vector2 direction;
    private float nextDirectionChangeTime;

    public EnemyRoamState(EnemyAgent agent, EnemyStateMachine stateMachine) : base(agent, stateMachine) { }

    public override void Enter() => PickDirection();

    public override void Tick()
    {
        if (!Agent.HasTarget)
        {
            StateMachine.ChangeState(Agent.IdleState);
            return;
        }

        if (Agent.CanFire)
        {
            StateMachine.ChangeState(Agent.AttackState);
            return;
        }

        if (Time.time >= nextDirectionChangeTime)
        {
            PickDirection();
        }

        Agent.SetDesiredVelocity(Agent.Data.StayStill ? Vector2.zero : direction * Agent.Data.MoveSpeed);
    }

    private void PickDirection()
    {
        Vector2 durationRange = Agent.Data.RoamStateDurationRange;
        nextDirectionChangeTime = Time.time + Random.Range(durationRange.x, durationRange.y);
        direction = Random.value < Agent.Data.IdleChance ? Vector2.zero : Random.Range(0, 4) switch
        {
            0 => Vector2.up,
            1 => Vector2.down,
            2 => Vector2.left,
            _ => Vector2.right
        };
    }
}

public sealed class EnemyAttackState : EnemyState
{
    private float elapsed;
    private bool projectileReleased;
    private bool meleeDamageDealt;
    private bool spearThrustStarted;
    private bool spearDamageDealt;
    private Vector2 spearDirection;

    public EnemyAttackState(EnemyAgent agent, EnemyStateMachine stateMachine) : base(agent, stateMachine) { }

    public override void Enter()
    {
        if (Agent.Data.Archetype == EnemyArchetype.Melee)
        {
            elapsed = 0f;
            meleeDamageDealt = false;
            Agent.BeginMeleeAttack();
            return;
        }

        if (Agent.Data.Archetype == EnemyArchetype.Spearman)
        {
            elapsed = 0f;
            spearThrustStarted = false;
            spearDamageDealt = false;
            spearDirection = Agent.HasTarget
                ? ((Vector2)Agent.Target.position - Agent.Body.position).normalized
                : Vector2.right;
            Agent.BeginSpearAttack();
            return;
        }

        elapsed = 0f;
        projectileReleased = false;
        Agent.BeginRangedAttack();
    }

    public override void Tick()
    {
        if (Agent.Data.Archetype == EnemyArchetype.Melee)
        {
            elapsed += Agent.EnemyDeltaTime;
            Agent.SetDesiredVelocity(Vector2.zero);

            if (!meleeDamageDealt && elapsed >= Agent.Data.MeleeAttackHitDelay)
            {
                meleeDamageDealt = true;
                Agent.PerformMeleeAttack();
            }

            if (elapsed >= Agent.Data.MeleeAttackDuration)
            {
                Agent.CompleteMeleeAttack();
                StateMachine.ChangeState(Agent.DefaultActiveState);
            }
            return;
        }
        if (!Agent.HasTarget)
        {
            StateMachine.ChangeState(Agent.IdleState);
            return;
        }

        elapsed += Agent.EnemyDeltaTime;

        if (Agent.Data.Archetype == EnemyArchetype.Spearman)
        {
            if (!spearThrustStarted && elapsed >= Agent.Data.SpearWindupDuration)
            {
                spearThrustStarted = true;
                Agent.BeginSpearThrust(spearDirection);
            }

            if (spearThrustStarted)
            {
                Agent.SetDesiredVelocity(spearDirection * Agent.Data.SpearThrustSpeed);
                float spearImpactTime = Agent.Data.SpearWindupDuration
                    + Agent.Data.SpearThrustDuration * Agent.Data.SpearImpactNormalizedTime;
                if (!spearDamageDealt && elapsed >= spearImpactTime)
                {
                    spearDamageDealt = true;
                    Agent.TryHitWithSpear(spearDirection);
                }
            }
            else
            {
                Agent.SetDesiredVelocity(Vector2.zero);
                Agent.FaceTarget();
            }

            if (elapsed >= Agent.Data.SpearWindupDuration + Agent.Data.SpearThrustDuration)
            {
                Agent.CompleteSpearAttack();
                StateMachine.ChangeState(Agent.DefaultActiveState);
            }
            return;
        }

        Agent.SetDesiredVelocity(Vector2.zero);
        if (!projectileReleased && elapsed >= Agent.Data.RangedAttackReleaseDelay)
        {
            projectileReleased = true;
            Agent.FireProjectile();
        }

        if (elapsed >= Agent.Data.RangedAttackDuration)
        {
            StateMachine.ChangeState(Agent.RoamState);
        }
    }
}

public sealed class EnemyShieldGuardState : EnemyState
{
    private enum ApproachPattern { Pause, Direct, Upward, Downward }

    private ApproachPattern approachPattern;
    private ApproachPattern pendingApproachPattern;
    private float nextApproachPatternTime;
    private bool isPausingBeforeTurn;

    public EnemyShieldGuardState(EnemyAgent agent, EnemyStateMachine stateMachine) : base(agent, stateMachine) { }

    public override void Enter()
    {
        approachPattern = ApproachPattern.Pause;
        isPausingBeforeTurn = false;
        nextApproachPatternTime = 0f;
    }

    public override void Tick()
    {
        if (!Agent.HasTarget)
        {
            StateMachine.ChangeState(Agent.IdleState);
            return;
        }

        if (Agent.IsWaitingToEngageInMelee)
        {
            Agent.SetDesiredVelocity(Vector2.zero);
            return;
        }

        if (!Agent.CanPressureTarget())
        {
            Agent.CancelMeleeAttackPreparation();
            Agent.SetDesiredVelocity(Agent.GetMeleeWaitingRoamDirection() * Agent.Data.MoveSpeed);
            return;
        }

        if (Agent.IsMeleeAttackRecovering)
        {
            Agent.CancelMeleeAttackPreparation();
            Agent.SetDesiredVelocity(Vector2.zero);
            return;
        }

        Vector2 moveDirection = Agent.GetMeleeFormationMoveDirection(out _);
        bool isInAttackPosition = Agent.IsWithinMeleeAttackDistance();
        if (!isInAttackPosition)
        {
            Agent.CancelMeleeAttackPreparation();
            Agent.SetDesiredVelocity(GetShieldApproachDirection(moveDirection) * Agent.MeleeEngagementMoveSpeed);
            return;
        }

        Agent.SetDesiredVelocity(Vector2.zero);
        Agent.FaceTarget();
        if (Agent.TryBeginMeleeAttack())
        {
            StateMachine.ChangeState(Agent.ShieldAttackState);
        }
    }

    private Vector2 GetShieldApproachDirection(Vector2 formationDirection)
    {
        if (Time.time >= nextApproachPatternTime)
        {
            if (isPausingBeforeTurn)
            {
                approachPattern = pendingApproachPattern;
                isPausingBeforeTurn = false;
                nextApproachPatternTime = Time.time + Random.Range(.35f, .85f);
            }
            else
            {
                ApproachPattern nextPattern = PickApproachPattern();
                bool needsTurnPause = approachPattern != ApproachPattern.Pause
                    && nextPattern != ApproachPattern.Pause
                    && nextPattern != approachPattern;
                if (needsTurnPause)
                {
                    pendingApproachPattern = nextPattern;
                    approachPattern = ApproachPattern.Pause;
                    isPausingBeforeTurn = true;
                    nextApproachPatternTime = Time.time + Random.Range(.25f, .45f);
                }
                else
                {
                    approachPattern = nextPattern;
                    nextApproachPatternTime = Time.time + Random.Range(.35f, .85f);
                }
            }
        }

        return approachPattern switch
        {
            ApproachPattern.Pause => Vector2.zero,
            ApproachPattern.Upward => BlendApproachDirection(formationDirection, Vector2.up),
            ApproachPattern.Downward => BlendApproachDirection(formationDirection, Vector2.down),
            _ => formationDirection
        };
    }

    private static ApproachPattern PickApproachPattern()
    {
        float roll = Random.value;
        return roll < .3f ? ApproachPattern.Pause
            : roll < .55f ? ApproachPattern.Direct
            : roll < .775f ? ApproachPattern.Upward
            : ApproachPattern.Downward;
    }

    private static Vector2 BlendApproachDirection(Vector2 formationDirection, Vector2 verticalDirection)
    {
        // Keep most of the motion oriented toward the assigned formation slot while
        // visibly weaving above and below the player instead of charging in a straight line.
        Vector2 blended = formationDirection * .65f + verticalDirection * .75f;
        return blended.sqrMagnitude > .0001f ? blended.normalized : verticalDirection;
    }
}

public sealed class EnemyShieldBlockState : EnemyState
{
    private float remaining;

    public EnemyShieldBlockState(EnemyAgent agent, EnemyStateMachine stateMachine) : base(agent, stateMachine) { }

    public override void Enter()
    {
        remaining = Agent.Data.ShieldBlockDuration;
        Agent.BeginShieldBlock();
    }

    public override void Tick()
    {
        if (!Agent.HasTarget)
        {
            StateMachine.ChangeState(Agent.IdleState);
            return;
        }

        Agent.SetDesiredVelocity(Vector2.zero);
        Agent.FaceTarget();
        remaining -= Agent.EnemyDeltaTime;
        if (remaining <= 0f) StateMachine.ChangeState(Agent.DefaultActiveState);
    }
}

public sealed class EnemyShieldAttackState : EnemyState
{
    private float elapsed;
    private bool dealtDamage;

    public EnemyShieldAttackState(EnemyAgent agent, EnemyStateMachine stateMachine) : base(agent, stateMachine) { }

    public override void Enter()
    {
        elapsed = 0f;
        dealtDamage = false;
        Agent.BeginShieldAttack();
    }

    public override void Tick()
    {
        if (!Agent.HasTarget)
        {
            StateMachine.ChangeState(Agent.IdleState);
            return;
        }

        elapsed += Agent.EnemyDeltaTime;
        Agent.SetDesiredVelocity(Vector2.zero);
        if (!dealtDamage && elapsed >= Agent.Data.ShieldAttackWindup)
        {
            dealtDamage = true;
            Agent.PerformShieldAttack();
        }

        if (elapsed >= Agent.Data.ShieldAttackDuration)
        {
            StateMachine.ChangeState(Agent.DefaultActiveState);
        }
    }

    public override void Exit() => Agent.EndShieldAttack();
}
