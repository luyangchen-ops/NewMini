using System;

public enum PlayerStateId
{
    Locomotion,
    Dodge,
    PerfectDodgeFreeze,
    KillChainTargeting,
    KillChainDash,
    KillChainImpact,
    UltimateTargeting,
    UltimateExecution,
    UltimateFinisher
}

public sealed class PlayerStateMachine
{
    public PlayerStateId Current { get; private set; } = PlayerStateId.Locomotion;
    public event Action<PlayerStateId, PlayerStateId> Changed;
    public bool Is(PlayerStateId state) => Current == state;
    public void Change(PlayerStateId next)
    {
        if (next == Current) return;
        PlayerStateId previous = Current;
        Current = next;
        Changed?.Invoke(previous, next);
    }
}
