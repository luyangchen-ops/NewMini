using UnityEngine;

/// <summary>
/// Boss-only combat rules. This intentionally does not use shield-facing logic:
/// every fifth accepted player hit is answered with an all-direction counter block.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossCombatController : MonoBehaviour
{
    [Header("Five-hit counter")]
    [SerializeField, Min(1)] private int hitsBeforeCounter = 5;
    [SerializeField, Min(.05f)] private float consecutiveHitGracePeriod = 1.35f;
    [SerializeField, Min(0f)] private float counterKnockbackDistance = 2.2f;

    [Header("Aggressive combo")]
    [SerializeField, Range(1, 3)] private int maximumAttackCount = 3;
    [SerializeField, Range(0f, 1f)] private float followUpAttackChance = .72f;

    private EnemyAgent agent;
    private Animator visualAnimator;
    private PlayerCharacterController player;
    private int receivedHitCount;
    private int attackCount;
    private float lastHitTime = float.NegativeInfinity;

    private static readonly int Attack = Animator.StringToHash("Attack");

    private void Awake()
    {
        agent = GetComponent<EnemyAgent>();
        visualAnimator = GetComponentInChildren<Animator>();
        player = FindAnyObjectByType<PlayerCharacterController>();
    }

    /// <summary>Called only when a player strike has actually reached the boss.</summary>
    public bool TryCounterPlayerHit()
    {
        if (Time.time - lastHitTime > consecutiveHitGracePeriod) receivedHitCount = 0;
        lastHitTime = Time.time;
        receivedHitCount++;
        if (receivedHitCount < hitsBeforeCounter) return false;

        receivedHitCount = 0;
        TriggerCounterBlock();
        return true;
    }

    public void BeginAttackSequence() => attackCount = 1;

    public bool TryContinueAttackSequence()
    {
        if (agent == null || agent.IsDead || attackCount >= maximumAttackCount) return false;
        if (Random.value > followUpAttackChance) return false;

        attackCount++;
        return true;
    }

    private void TriggerCounterBlock()
    {
        if (visualAnimator != null)
        {
            // The supplied attack clip is deliberately reused as the counter pose.
            visualAnimator.ResetTrigger(Attack);
            visualAnimator.SetTrigger(Attack);
        }

        player ??= FindAnyObjectByType<PlayerCharacterController>();
        if (player == null) return;

        Vector2 direction = (Vector2)player.transform.position - (Vector2)transform.position;
        if (direction.sqrMagnitude <= .0001f) direction = Vector2.right;
        player.ReceiveKnockback(direction.normalized, counterKnockbackDistance);
    }
}
