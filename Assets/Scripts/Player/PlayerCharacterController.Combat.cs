using UnityEngine;

/// <summary>Normal attacks, health, momentum, boss reactions, and combat-facing APIs.</summary>
public partial class PlayerCharacterController
{
    private void TryNormalAttackHit(Vector2 direction)
    {
        Transform closestTarget = null;
        BreakableMapProp closestBreakable = null;
        float closestDistanceSquared = float.PositiveInfinity;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(body.position, NormalAttackRange))
        {
            Transform enemy = FindEnemy(hit.transform);
            BreakableMapProp breakable = enemy == null
                ? hit.GetComponentInParent<BreakableMapProp>()
                : null;
            if (enemy == null && (breakable == null || breakable.IsBroken)) continue;

            Transform target = enemy != null ? enemy : breakable.transform;
            Vector2 targetPoint = enemy != null ? (Vector2)enemy.position : hit.bounds.center;
            Vector2 offset = targetPoint - body.position;
            if (offset.sqrMagnitude > Mathf.Epsilon
                && Vector2.Angle(direction, offset) > NormalAttackArcAngle * .5f) continue;
            if (offset.sqrMagnitude >= closestDistanceSquared) continue;
            closestTarget = target;
            closestBreakable = breakable;
            closestDistanceSquared = offset.sqrMagnitude;
        }

        if (closestTarget == null) return;
        if (closestBreakable != null)
        {
            closestBreakable.Break();
            return;
        }

        EnemyAgent enemyAgent = closestTarget.GetComponent<EnemyAgent>();
        if (enemyAgent != null && !enemyAgent.CanBeKilledBy(body.position, false))
        {
            enemyAgent.BlockIncomingAttack();
            return;
        }

        EnemyAgent.PlayerAttackResult hitResult = enemyAgent != null
            ? enemyAgent.ReceivePlayerAttack(body.position)
            : EnemyAgent.PlayerAttackResult.Defeated;
        if (hitResult == EnemyAgent.PlayerAttackResult.Guarded)
        {
            PlaySfx(parrySfx);
            return;
        }
        PlayBloodHitEffect(closestTarget, direction);
        if (hitResult == EnemyAgent.PlayerAttackResult.Damaged)
        {
            if (enemyAgent != null && enemyAgent.IsBossCombatant)
                AwardMomentumFromBossDamage();
            PlaySfx(HitBladeFleshSfx, HitBladeFleshVolume);
            return;
        }
        SpecialItemDropSpawner.TryDropFromEnemy(closestTarget.position);
        if (enemyAgent == null) KillEnemy(closestTarget);
        RestoreHealth(NormalKillHealthRestore);
        AwardMomentum(0);
        PlaySfx(HitBladeFleshSfx, HitBladeFleshVolume);
        PlaySfx(killSfx);
    }

    /// <summary>Receives damage from an enemy. Enemy data currently supplies zero damage.</summary>
    public void TakeDamage(float damage)
    {
        if (damage <= 0f || isDead || IsInvulnerable) return;
        if (specialItems != null && specialItems.TryBlockAttack()) return;
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        HealthChanged?.Invoke(currentHealth, MaximumHealth);
        if (currentHealth > 0f)
        {
            visualAnimator?.SetTrigger(Hurt);
            return;
        }

        isDead = true;
        CancelBossGuardReaction();
        body.linearVelocity = Vector2.zero;
        visualAnimator?.SetBool(IsDeadAnimatorParam, true);
        Died?.Invoke();
    }

    public void RespawnAt(Vector3 position)
    {
        isDead = false;
        CancelBossGuardReaction();
        transform.position = position;
        body.position = position;
        body.linearVelocity = Vector2.zero;
        currentHealth = MaximumHealth;
        killChainTutorialHold = false;
        HealthChanged?.Invoke(currentHealth, MaximumHealth);
        ResetVisualAnimatorAfterRespawn();
        if (stateMachine != null) stateMachine.Change(PlayerStateId.Locomotion);
        EnemyTimeScale = 1f;
        enemyTimeScaleTarget = 1f;
        cameraController?.RestoreImmediately();
    }

    /// <summary>Locks player input while the Boss holds the guard pose.</summary>
    public void BeginBossGuardStun()
    {
        if (isDead) return;

        if (IsUltimateActive) EndUltimate(false);
        bossGuardControlLocked = true;
        bossGuardKnockbackActive = false;
        bossGuardKnockbackElapsed = 0f;
        body.linearVelocity = Vector2.zero;
        visualAnimator?.SetFloat(Speed, 0f);
        visualAnimator?.SetTrigger(Hurt);
    }

    /// <summary>Used by boss counter attacks. It displaces without dealing health damage.</summary>
    public void ReceiveKnockback(Vector2 direction, float distance, float duration = .18f)
    {
        if (isDead || direction.sqrMagnitude <= .0001f || distance <= 0f) return;

        bossGuardControlLocked = true;
        bossGuardKnockbackActive = true;
        bossGuardKnockbackElapsed = 0f;
        bossGuardKnockbackDuration = Mathf.Max(.01f, duration);
        bossGuardKnockbackStart = body.position;
        bossGuardKnockbackTarget = PlayAreaBounds.ClampPosition(
            body.position + direction.normalized * distance,
            Padding);
        body.linearVelocity = Vector2.zero;
        visualAnimator?.SetTrigger(Hurt);
    }

    private void UpdateBossGuardKnockback()
    {
        bossGuardKnockbackElapsed += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01(bossGuardKnockbackElapsed / bossGuardKnockbackDuration);
        body.MovePosition(Vector2.LerpUnclamped(
            bossGuardKnockbackStart,
            bossGuardKnockbackTarget,
            EaseOutCubic(progress)));

        if (progress < 1f) return;

        body.position = bossGuardKnockbackTarget;
        bossGuardKnockbackActive = false;
        bossGuardControlLocked = false;
        body.linearVelocity = Vector2.zero;
        if (stateMachine != null) stateMachine.Change(PlayerStateId.Locomotion);
    }

    /// <summary>Clears a pending Boss guard reaction during death, respawn, or Boss removal.</summary>
    public void CancelBossGuardReaction()
    {
        bossGuardControlLocked = false;
        bossGuardKnockbackActive = false;
        bossGuardKnockbackElapsed = 0f;
        if (body != null) body.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// Returns the Animator to its controller's entry state after death. Clearing the
    /// IsDead parameter alone leaves an Animator that has no Death-to-Idle transition
    /// displaying its final death frame.
    /// </summary>
    private void ResetVisualAnimatorAfterRespawn()
    {
        if (visualAnimator == null) return;

        visualAnimator.Rebind();
        visualAnimator.speed = animatorBaseSpeed;
        visualAnimator.SetBool(IsDeadAnimatorParam, false);
        visualAnimator.SetFloat(Speed, 0f);
        visualAnimator.SetInteger(VerticalDirection, 0);
        visualAnimator.Update(0f);
    }

    public void RestoreHealth(float amount)
    {
        if (amount <= 0f) return;
        currentHealth = Mathf.Min(MaximumHealth, currentHealth + amount);
        HealthChanged?.Invoke(currentHealth, MaximumHealth);
    }

    private void AwardMomentum(int comboLength)
    {
        int amount = MomentumPerKill;
        if (comboLength >= ComboRewardThreshold) amount += BonusMomentumPerComboKill;

        int previousMomentum = currentMomentum;
        currentMomentum = Mathf.Min(MaximumMomentum, currentMomentum + amount);
        if (currentMomentum != previousMomentum)
            MomentumChanged?.Invoke(currentMomentum, MaximumMomentum);
    }

    /// <summary>Awards momentum for a real Boss health/contract loss outside the ultimate.</summary>
    public void AwardMomentumFromBossDamage()
    {
        if (IsUltimateActive) return;
        AwardMomentum(0);
    }
}
