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

        Vector2 offset = (Vector2)Agent.Target.position - Agent.Body.position;
        if (offset.sqrMagnitude <= Agent.Data.StoppingDistance * Agent.Data.StoppingDistance)
        {
            Agent.SetDesiredVelocity(Vector2.zero);
            if (Agent.CanMeleeAttack)
            {
                StateMachine.ChangeState(Agent.AttackState);
            }
            return;
        }

        Agent.SetDesiredVelocity(offset.normalized * Agent.Data.MoveSpeed);
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
    public EnemyAttackState(EnemyAgent agent, EnemyStateMachine stateMachine) : base(agent, stateMachine) { }

    public override void Enter()
    {
        if (Agent.Data.Archetype == EnemyArchetype.Melee)
        {
            Agent.PerformMeleeAttack();
            StateMachine.ChangeState(Agent.DefaultActiveState);
            return;
        }

        Agent.FireProjectile();
        StateMachine.ChangeState(Agent.RoamState);
    }
}
