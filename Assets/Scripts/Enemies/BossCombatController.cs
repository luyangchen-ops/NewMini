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
    [Tooltip("Maximum interval between successive player hits needed to trigger the counter.")]
    [SerializeField, Min(.05f)] private float consecutiveHitGracePeriod = 3f;
    [SerializeField, Min(0f)] private float counterKnockbackDistance = 2.2f;

    [Header("Aggressive combo")]
    [SerializeField, Range(1, 3)] private int maximumAttackCount = 3;
    [SerializeField, Range(0f, 1f)] private float followUpAttackChance = .72f;
    [Tooltip("Forward offset of the melee hitbox from the Boss body centre.")]
    [SerializeField, Min(0f)] private float meleeHitboxForwardOffset = .9f;
    [Tooltip("World-space size of the melee hitbox. Damage is dealt only when it overlaps the player collider.")]
    [SerializeField] private Vector2 meleeHitboxSize = new(1.9f, 2.2f);

    private EnemyAgent agent;
    private Animator visualAnimator;
    private PlayerCharacterController player;
    private int receivedHitCount;
    private int attackCount;
    private float lastHitTime = float.NegativeInfinity;
    private Vector2 swingDirection = Vector2.right;

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

    /// <summary>Locks this swing's facing direction before its damaging frame.</summary>
    public void BeginMeleeSwing()
    {
        player ??= FindAnyObjectByType<PlayerCharacterController>();
        if (player == null) return;

        swingDirection = player.transform.position.x < transform.position.x
            ? Vector2.left
            : Vector2.right;
    }

    /// <summary>Returns true only while the current sword-swing hitbox overlaps the player collider.</summary>
    public bool IsCurrentMeleeSwingHittingPlayer(PlayerCharacterController targetPlayer)
    {
        if (targetPlayer == null || agent?.Body == null) return false;

        Vector2 size = new(Mathf.Max(.01f, meleeHitboxSize.x), Mathf.Max(.01f, meleeHitboxSize.y));
        Vector2 center = agent.Body.position + swingDirection * meleeHitboxForwardOffset;
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            // The player's body collider lives on the controller object. Do not let
            // any child trigger used for presentation extend the attack reach.
            if (hit != null && hit.GetComponent<PlayerCharacterController>() == targetPlayer)
                return true;
        }

        return false;
    }

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
