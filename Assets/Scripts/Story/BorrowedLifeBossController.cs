using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Makes a lethal hit consume one authored borrowed-life contract instead.
/// When the final contract breaks, that same strike is allowed to kill the boss.
/// </summary>
[DisallowMultipleComponent]
public sealed class BorrowedLifeBossController : MonoBehaviour
{
    [SerializeField, Min(1)] private int startingContracts = 99;
    [SerializeField] private Text contractCountText;
    [SerializeField] private UnityEvent<int> onContractCountChanged = new UnityEvent<int>();
    [SerializeField] private UnityEvent<int> onPhaseChanged = new UnityEvent<int>();
    [SerializeField] private UnityEvent onAllContractsBroken = new UnityEvent();

    public int RemainingContracts { get; private set; }
    public int MaximumContracts => Mathf.Max(1, startingContracts);
    public int Phase { get; private set; } = 1;

    private void Awake()
    {
        RemainingContracts = Mathf.Max(1, startingContracts);
        RefreshPresentation();
    }

    public void ConfigurePresentation(Text countText)
    {
        contractCountText = countText;
        RefreshPresentation();
    }

    /// <summary>Applies the encounter's current difficulty before the Boss fight begins.</summary>
    public void ConfigureMaximumContracts(int maximumContracts)
    {
        startingContracts = Mathf.Max(1, maximumContracts);
        RemainingContracts = startingContracts;
        Phase = 1;
        RefreshPresentation();
    }

    public bool TryAbsorbLethalHit()
    {
        if (!enabled || RemainingContracts <= 0) return false;

        RemainingContracts--;
        int contractTotal = Mathf.Max(1, startingContracts);
        int nextPhase = RemainingContracts * 3 > contractTotal * 2 ? 1
            : RemainingContracts * 3 > contractTotal ? 2
            : 3;
        if (nextPhase != Phase)
        {
            Phase = nextPhase;
            onPhaseChanged.Invoke(Phase);
        }

        RefreshPresentation();
        onContractCountChanged.Invoke(RemainingContracts);
        if (RemainingContracts > 0) return true;

        onAllContractsBroken.Invoke();
        return false;
    }

    private void RefreshPresentation()
    {
        if (contractCountText != null)
            contractCountText.text = $"借命契：{RemainingContracts}";
    }
}
