public sealed class EnemyStateMachine
{
    public EnemyState CurrentState { get; private set; }

    public void ChangeState(EnemyState nextState)
    {
        CurrentState?.Exit();
        CurrentState = nextState;
        CurrentState.Enter();
    }

    public void Tick() => CurrentState?.Tick();
    public void FixedTick() => CurrentState?.FixedTick();
}
