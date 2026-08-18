using UnityEngine;

/// <summary>Perfect-dodge targeting, chained dash movement, hit resolution, and target queries.</summary>
public partial class PlayerCharacterController
{
    public bool TryTriggerPerfectDodge(Vector2 projectilePosition)
    {
        float perfectRadius = DodgeDistance * PerfectRatio;
        if (!IsDodging || (body.position - projectilePosition).sqrMagnitude >= perfectRadius * perfectRadius) return false;

        body.linearVelocity = Vector2.zero;
        killChainCount = 0;
        ResetKillChainWindow(KillChainInitialWindow);
        currentTarget = lockedDashTarget = bufferedTarget = lastKilledTarget = null;
        bufferedTargetUntil = 0f;
        stateTimer = PerfectDodgeFreezeDuration;
        stateMachine.Change(PlayerStateId.PerfectDodgeFreeze);
        PlaySfx(PerfectDodgeSfx != null ? PerfectDodgeSfx : parrySfx);
        perfectDodgeAfterimage?.Play(visualRenderer != null && visualRenderer.flipX);
        cameraController.BeginKillChain(CameraZoomFactor, CameraFocusOffset, CameraResponse,
            PerfectDodgeCameraShake, MaximumCameraShake);
        onKillChainStarted?.Invoke();
        KillChainStarted?.Invoke();
        return true;
    }

    private void HandlePerfectDodgeFreeze()
    {
        stateTimer -= Time.unscaledDeltaTime;
        if (stateTimer <= 0f) EnterTargeting();
    }

    private void EnterTargeting()
    {
        lockedDashTarget = null;
        stateMachine.Change(PlayerStateId.KillChainTargeting);
        SetCurrentTarget(FindBestTarget(null));
    }

    private void HandleTargeting()
    {
        if (input.CancelPressed)
        {
            EndKillChain();
            return;
        }

        if (!killChainTutorialHold)
            chainWindowRemaining = Mathf.Max(0f, chainWindowRemaining - Time.unscaledDeltaTime);
        if (chainWindowRemaining <= 0f)
        {
            EndKillChain();
            return;
        }

        SetCurrentTarget(FindBestTarget(null));
        if (!input.PointerPressed) return;
        killChainTutorialHold = false;

        Transform directionalTarget = FindBestDirectionalTarget(PointerWorld() - body.position, null);
        if (IsValidTarget(directionalTarget))
            StartKillChainDash(directionalTarget);
        else if (IsValidTarget(currentTarget))
            StartKillChainDash(currentTarget);
        else if (!HasAnyTargetInRange(null))
            StartFreeKillChainDash();
        else
            onInvalidKillChainTarget?.Invoke();
    }

    private void StartKillChainDash(Transform target)
    {
        if (!IsValidTarget(target)) return;

        isFreeKillChainDash = false;
        lockedDashTarget = target;
        SetCurrentTarget(null);
        bufferedTarget = null;
        bufferedTargetUntil = 0f;
        perfectDodgeAfterimage?.StopAndRestore();
        dashStart = body.position;
        Vector2 targetOffset = (Vector2)target.position - dashStart;
        Vector2 pointerOffset = PointerWorld() - dashStart;
        killDashDirection = targetOffset.sqrMagnitude > Mathf.Epsilon
            ? targetOffset.normalized
            : pointerOffset.sqrMagnitude > Mathf.Epsilon
                ? pointerOffset.normalized
                : visualRenderer != null && visualRenderer.flipX ? Vector2.left : Vector2.right;
        RecalculateKillDashTarget();
        dashElapsed = -AttackDashWindupDuration;
        activeDashDuration = Mathf.Max(.01f, AttackDashDuration);
        stateMachine.Change(PlayerStateId.KillChainDash);
        UpdateFacing(killDashDirection);
        visualAnimator?.SetTrigger(DashAttack);
        PlaySfx(dashAttackSfx);
        PlaySfx(DashWindCutSfx, dashWindCutVolume);
    }

    private void StartFreeKillChainDash()
    {
        Vector2 direction = PointerWorld() - body.position;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            direction = visualRenderer != null && visualRenderer.flipX ? Vector2.left : Vector2.right;
        else
            direction.Normalize();

        isFreeKillChainDash = true;
        lockedDashTarget = null;
        SetCurrentTarget(null);
        bufferedTarget = null;
        bufferedTargetUntil = 0f;
        perfectDodgeAfterimage?.StopAndRestore();
        dashStart = body.position;
        killDashDirection = direction;
        dashTarget = PlayAreaBounds.ClampPosition(
            dashStart + direction * AttackDashDistance,
            Padding);
        dashElapsed = -AttackDashWindupDuration;
        activeDashDuration = Mathf.Max(.01f, AttackDashDuration);
        stateMachine.Change(PlayerStateId.KillChainDash);
        UpdateFacing(direction);
        visualAnimator?.SetTrigger(DashAttack);
        PlaySfx(dashAttackSfx);
        PlaySfx(DashWindCutSfx, dashWindCutVolume);
    }

    private void HandleDashInputBuffer()
    {
        Transform candidate = FindBestTarget(lockedDashTarget);
        SetCurrentTarget(candidate);
        if (input.PointerPressed && IsValidTarget(candidate)) BufferTarget(candidate);
    }

    private void UpdateKillChainDash()
    {
        if (!isFreeKillChainDash && !IsTargetAlive(lockedDashTarget))
        {
            lockedDashTarget = null;
            EnterTargeting();
            return;
        }

        if (!isFreeKillChainDash) RecalculateKillDashTarget();
        dashElapsed += Time.fixedDeltaTime;
        if (dashElapsed <= 0f) return;
        float t = Mathf.Clamp01(dashElapsed / activeDashDuration);
        Vector2 nextPosition = Vector2.LerpUnclamped(dashStart, dashTarget, EaseOutCubic(t));
        BreakPropsAlongKillDash(body.position, nextPosition);

        Transform crossedEnemy = FindEnemyAlongKillDash(body.position, nextPosition);
        if (crossedEnemy != null)
        {
            // A free dash can happen when the pointer assist did not acquire a nearby
            // enemy. Convert it back into a targeted dash when its path actually
            // intersects one. A targeted dash only accepts its locked target here so
            // an unrelated enemy cannot steal an intentional attack.
            lockedDashTarget = crossedEnemy;
            isFreeKillChainDash = false;
            RecalculateKillDashTarget();
            ConfirmKill(crossedEnemy);
            return;
        }

        body.MovePosition(nextPosition);
        if (t < 1f) return;

        if (isFreeKillChainDash)
        {
            body.position = dashTarget;
            body.linearVelocity = Vector2.zero;
            isFreeKillChainDash = false;
            EndKillChain();
            return;
        }

        ConfirmKill(lockedDashTarget);
    }

    private Transform FindEnemyAlongKillDash(Vector2 from, Vector2 to)
    {
        Vector2 offset = to - from;
        float distance = offset.magnitude;
        if (distance <= Mathf.Epsilon) return null;

        Transform bestTarget = null;
        float bestDistance = float.PositiveInfinity;
        targetCandidates.Clear();
        foreach (RaycastHit2D hit in Physics2D.CircleCastAll(
                     from,
                     KillChainEnemyHitRadius,
                     offset / distance,
                     distance))
        {
            Transform enemy = hit.collider != null ? FindEnemy(hit.collider.transform) : null;
            if (enemy == null || enemy == lastKilledTarget || !targetCandidates.Add(enemy)) continue;
            if (!isFreeKillChainDash && enemy != lockedDashTarget) continue;
            if (!IsValidTarget(enemy)) continue;
            if (hit.distance >= bestDistance) continue;
            bestDistance = hit.distance;
            bestTarget = enemy;
        }

        return bestTarget;
    }

    private static void BreakPropsAlongKillDash(Vector2 from, Vector2 to)
    {
        Vector2 offset = to - from;
        float distance = offset.magnitude;
        if (distance <= Mathf.Epsilon) return;

        foreach (RaycastHit2D hit in Physics2D.CircleCastAll(
                     from,
                     KillChainPropBreakRadius,
                     offset / distance,
                     distance))
        {
            BreakableMapProp breakable = hit.collider != null
                ? hit.collider.GetComponentInParent<BreakableMapProp>()
                : null;
            if (breakable != null && !breakable.IsBroken) breakable.Break();
        }
    }

    private void RecalculateKillDashTarget()
    {
        if (lockedDashTarget == null) return;
        Vector2 targetPosition = lockedDashTarget.position;
        Vector2 offset = targetPosition - dashStart;
        if (offset.sqrMagnitude > Mathf.Epsilon) killDashDirection = offset.normalized;
        dashTarget = PlayAreaBounds.ClampPosition(
            targetPosition + killDashDirection * AttackDashOvershoot,
            Padding);
    }

    private void ConfirmKill(Transform enemy)
    {
        if (!IsValidTarget(enemy))
        {
            EnterTargeting();
            return;
        }

        EnemyAgent targetAgent = enemy.GetComponentInParent<EnemyAgent>();
        EnemyAgent.PlayerAttackResult hitResult = targetAgent != null
            ? targetAgent.ReceivePlayerAttack(body.position, false)
            : EnemyAgent.PlayerAttackResult.Defeated;
        if (hitResult == EnemyAgent.PlayerAttackResult.Guarded)
        {
            PlaySfx(parrySfx);
            EndKillChain();
            return;
        }
        if (hitResult == EnemyAgent.PlayerAttackResult.Damaged)
        {
            if (targetAgent != null && targetAgent.IsBossCombatant)
                AwardMomentumFromBossDamage();
            PlayBloodHitEffect(enemy, killDashDirection);
            PlaySfx(HitBladeFleshSfx, hitBladeFleshVolume);
            EndKillChain();
            return;
        }

        body.position = dashTarget;
        body.linearVelocity = Vector2.zero;
        lastKilledTarget = enemy;
        if (bufferedTarget == enemy) bufferedTarget = null;
        PlayBloodHitEffect(enemy, killDashDirection);
        SpecialItemDropSpawner.TryDropFromEnemy(enemy.position);
        if (targetAgent == null) KillEnemy(enemy);
        RestoreHealth(KillChainHealthRestore);
        lockedDashTarget = null;
        killChainCount++;
        AwardMomentum(killChainCount);
        ResetKillChainWindow(KillChainTimeRestore);
        dashReadyTime = 0f;
        PlayKillSfx();
        cameraController.AddKillImpact(killDashDirection, KillCameraShake, killChainCount);
        onKillChainKillConfirmed?.Invoke(killChainCount);
        KillChainKillConfirmed?.Invoke(killChainCount);
        stateTimer = KillImpactFreezeDuration;
        stateMachine.Change(PlayerStateId.KillChainImpact);
    }

    private void HandleKillImpact()
    {
        Transform candidate = FindBestTarget(lastKilledTarget);
        SetCurrentTarget(candidate);
        if (input.PointerPressed && IsValidTarget(candidate)) BufferTarget(candidate);

        stateTimer -= Time.unscaledDeltaTime;
        if (stateTimer > 0f) return;

        if (bufferedTarget != null && Time.unscaledTime <= bufferedTargetUntil && IsValidTarget(bufferedTarget))
        {
            StartKillChainDash(bufferedTarget);
            return;
        }

        bufferedTarget = null;
        if (HasAnyTargetInRange(lastKilledTarget)) EnterTargeting();
        else EndKillChain();
    }

    private void BufferTarget(Transform target)
    {
        bufferedTarget = target;
        float actionTimeRemaining = State == PlayerStateId.KillChainDash
            ? Mathf.Max(0f, activeDashDuration - dashElapsed) + KillImpactFreezeDuration
            : Mathf.Max(0f, stateTimer);
        bufferedTargetUntil = Time.unscaledTime + actionTimeRemaining + KillChainInputBufferDuration;
    }

    private void EndKillChain()
    {
        killChainTutorialHold = false;
        int completedKills = killChainCount;
        isFreeKillChainDash = false;
        SetCurrentTarget(null);
        lockedDashTarget = bufferedTarget = lastKilledTarget = null;
        chainWindowRemaining = 0f;
        chainWindowDuration = 0f;
        exitProtectionUntil = Time.unscaledTime + KillChainExitProtection;
        cameraController.EndKillChain();
        perfectDodgeAfterimage?.StopAndRestore();
        if (completedKills >= 3)
            PlaySheathePresentation();
        stateMachine.Change(PlayerStateId.Locomotion);
        onKillChainEnded?.Invoke(completedKills);
        KillChainFinished?.Invoke(completedKills);
    }

    private void ResetKillChainWindow(float duration)
    {
        chainWindowDuration = Mathf.Max(.05f, duration);
        chainWindowRemaining = chainWindowDuration;
    }

    /// <summary>
    /// Lets the authored first-use tutorial keep the initial kill-chain decision open
    /// while its one-line prompt is being read. The first target click releases it.
    /// </summary>
    public void SetKillChainTutorialHold(bool hold)
    {
        killChainTutorialHold = hold && IsKillChainActive;
    }

    private Transform FindBestTarget(Transform excludedTarget)
    {
        Vector2 playerPosition = body.position;
        Vector2 pointerPosition = PointerWorld();
        Vector2 aim = pointerPosition - playerPosition;
        float maximumAimDistance = Mathf.Max(.01f, MaximumAimDistance);
        if (aim.sqrMagnitude > maximumAimDistance * maximumAimDistance)
        {
            aim = aim.normalized * maximumAimDistance;
            pointerPosition = playerPosition + aim;
        }
        bool hasAim = aim.sqrMagnitude > .0001f;
        Transform bestTarget = null;
        float bestScore = float.PositiveInfinity;

        targetCandidates.Clear();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(playerPosition, AttackDashDistance))
        {
            Transform enemy = FindEnemy(hit.transform);
            if (enemy == null || enemy == excludedTarget || enemy == lastKilledTarget || !targetCandidates.Add(enemy)) continue;
            EnemyAgent agent = enemy.GetComponent<EnemyAgent>();
            if (agent != null && !agent.CanBeKilledBy(body.position, IsMomentumFull)) continue;

            Vector2 targetPoint = GetTargetPoint(enemy, playerPosition);
            Vector2 offset = targetPoint - playerPosition;
            float distance = offset.magnitude;
            if (distance > AttackDashDistance + .01f) continue;

            float pointerDistance = DistanceToTarget(enemy, pointerPosition);
            float angle = hasAim && offset.sqrMagnitude > Mathf.Epsilon ? Vector2.Angle(aim, offset) : 0f;
            bool directAssist = pointerDistance <= TargetAssistWorldRadius;
            // A target already within the close-assist radius should remain selectable
            // even when the pointer is slightly past it or has no reliable direction.
            bool closeAssist = distance <= TargetAssistWorldRadius;
            if (!directAssist && !closeAssist && (!hasAim || angle > TargetAssistMaximumAngle)) continue;

            float score = closeAssist
                ? distance * .01f
                : directAssist
                ? pointerDistance * .2f + distance * .01f
                : 100f + angle + PerpendicularDistanceToAim(playerPosition, aim, targetPoint) * .35f + distance * .02f;
            if (score >= bestScore) continue;
            bestScore = score;
            bestTarget = enemy;
        }

        return bestTarget;
    }

    /// <summary>
    /// Lets a click outside the displayed dash radius act as a directional command.
    /// The click need not land directly on an enemy; the nearest target in its forward
    /// search cone is selected as long as it is reachable by the kill-chain dash.
    /// </summary>
    private Transform FindBestDirectionalTarget(Vector2 direction, Transform excludedTarget)
    {
        if (direction.sqrMagnitude <= .0001f) return null;

        Vector2 playerPosition = body.position;
        Vector2 normalizedDirection = direction.normalized;
        Vector2 pointerPosition = PointerWorld();
        float bestScore = float.PositiveInfinity;
        Transform bestTarget = null;
        targetCandidates.Clear();

        foreach (Collider2D hit in Physics2D.OverlapCircleAll(playerPosition, AttackDashDistance))
        {
            Transform enemy = FindEnemy(hit.transform);
            if (enemy == null || enemy == excludedTarget || enemy == lastKilledTarget || !targetCandidates.Add(enemy)) continue;
            if (!IsValidTarget(enemy)) continue;

            Vector2 targetPoint = GetTargetPoint(enemy, playerPosition);
            Vector2 offset = targetPoint - playerPosition;
            if (offset.sqrMagnitude <= Mathf.Epsilon) return enemy;

            float angle = Vector2.Angle(normalizedDirection, offset);
            if (angle > directionalTargetSearchHalfAngle) continue;

            // A direct click always wins. Otherwise favor the nearest enemy in the
            // commanded forward cone, with angle used as a smaller tie breaker.
            float pointerDistance = DistanceToTarget(enemy, pointerPosition);
            bool directAssist = pointerDistance <= TargetAssistWorldRadius;
            float score = directAssist
                ? -1000f + pointerDistance
                : offset.magnitude + angle * .05f;
            if (score >= bestScore) continue;
            bestScore = score;
            bestTarget = enemy;
        }

        return bestTarget;
    }

    private bool HasAnyTargetInRange(Transform excludedTarget)
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(body.position, AttackDashDistance))
        {
            Transform enemy = FindEnemy(hit.transform);
            if (enemy != null && enemy != excludedTarget && enemy != lastKilledTarget && IsValidTarget(enemy)) return true;
        }

        return false;
    }

    private bool IsValidTarget(Transform target)
    {
        if (!IsTargetAlive(target)) return false;
        Vector2 targetPoint = GetTargetPoint(target, body.position);
        if ((targetPoint - body.position).sqrMagnitude > AttackDashDistance * AttackDashDistance + .01f)
            return false;
        EnemyAgent agent = target.GetComponent<EnemyAgent>();
        return agent == null || agent.CanBeKilledBy(body.position, IsMomentumFull);
    }

    private static Vector2 GetTargetPoint(Transform target, Vector2 from)
    {
        if (target == null) return from;
        Collider2D targetCollider = target.GetComponentInChildren<Collider2D>();
        return targetCollider != null ? targetCollider.ClosestPoint(from) : (Vector2)target.position;
    }

    private static float DistanceToTarget(Transform target, Vector2 from)
    {
        return Vector2.Distance(from, GetTargetPoint(target, from));
    }

    private static bool IsTargetAlive(Transform target)
    {
        return target != null && target.gameObject.activeInHierarchy;
    }

    private void SetCurrentTarget(Transform target)
    {
        if (currentTarget == target) return;
        currentTarget = target;
        onKillChainTargetChanged?.Invoke(target);
    }

    private void CheckPerfectDodgeDistance()
    {
        float radius = DodgeDistance * PerfectRatio;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(body.position, radius))
        {
            EnemyProjectile projectile = hit.GetComponentInParent<EnemyProjectile>();
            if (projectile == null || !TryTriggerPerfectDodge(projectile.transform.position)) continue;
            projectile.IgnorePlayerCollisions(this);
            return;
        }

        foreach (Collider2D hit in Physics2D.OverlapCircleAll(body.position, radius))
        {
            EnemyAgent enemy = hit.GetComponentInParent<EnemyAgent>();
            if (enemy == null || !enemy.IsMeleeAttackPerfectDodgeable) continue;
            if (TryTriggerPerfectDodge(enemy.transform.position)) return;
        }
    }
}
