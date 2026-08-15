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

        Vector2 moveDirection = Agent.GetMeleeFormationMoveDirection(out bool isAtFormation);
        Vector2 targetOffset = (Vector2)Agent.Target.position - Agent.Body.position;
        if (isAtFormation
            && targetOffset.sqrMagnitude <= Agent.Data.StoppingDistance * Agent.Data.StoppingDistance
            && Agent.CanMeleeAttack)
        {
            Agent.SetDesiredVelocity(Vector2.zero);
            StateMachine.ChangeState(Agent.AttackState);
            return;
        }

        Agent.SetDesiredVelocity(moveDirection * Agent.Data.MoveSpeed);
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

    public EnemyAttackState(EnemyAgent agent, EnemyStateMachine stateMachine) : base(agent, stateMachine) { }

    public override void Enter()
    {
        if (Agent.Data.Archetype == EnemyArchetype.Melee)
        {
            Agent.PerformMeleeAttack();
            StateMachine.ChangeState(Agent.DefaultActiveState);
            return;
        }

        elapsed = 0f;
        projectileReleased = false;
        Agent.BeginRangedAttack();
    }

    public override void Tick()
    {
        if (Agent.Data.Archetype == EnemyArchetype.Melee) return;
        if (!Agent.HasTarget)
        {
            StateMachine.ChangeState(Agent.IdleState);
            return;
        }

        elapsed += Agent.EnemyDeltaTime;
        Agent.SetDesiredVelocity(Vector2.zero);
        Agent.FaceTarget();
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
    public EnemyShieldGuardState(EnemyAgent agent, EnemyStateMachine stateMachine) : base(agent, stateMachine) { }

    public override void Tick()
    {
        if (!Agent.HasTarget)
        {
            StateMachine.ChangeState(Agent.IdleState);
            return;
        }

        Vector2 moveDirection = Agent.GetMeleeFormationMoveDirection(out bool isAtFormation);
        Vector2 targetOffset = (Vector2)Agent.Target.position - Agent.Body.position;
        if (!isAtFormation || targetOffset.sqrMagnitude > Agent.Data.StoppingDistance * Agent.Data.StoppingDistance)
        {
            Agent.SetDesiredVelocity(moveDirection * Agent.Data.MoveSpeed);
            return;
        }

        Agent.SetDesiredVelocity(Vector2.zero);
        Agent.FaceTarget();
        if (Agent.CanMeleeAttack)
        {
            StateMachine.ChangeState(Agent.ShieldAttackState);
        }
    }
}

public sealed class EnemyShieldBlockState : EnemyState
{
    private float remaining;

    public EnemyShieldBlockState(EnemyAgent agent, EnemyStateMachine stateMachine) : base(agent, stateMachine) { }

    public override void Enter()
    {
        remaining = Agent.Data.ShieldBlockDuration;
        Agent.SetDesiredVelocity(Vector2.zero);
        Agent.FaceTarget();
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
        Agent.FaceTarget();
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
