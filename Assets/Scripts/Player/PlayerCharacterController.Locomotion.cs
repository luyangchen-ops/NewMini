using UnityEngine;

/// <summary>Movement input, facing intent, dodge movement, and normal-attack dispatch.</summary>
public partial class PlayerCharacterController
{
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
        dashTarget = PlayAreaBounds.ClampPosition(dashStart + direction * DodgeDistance, Padding);
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

    private void Move(Vector2 delta)
    {
        body.MovePosition(PlayAreaBounds.ClampPosition(body.position + delta, Padding));
    }
}
