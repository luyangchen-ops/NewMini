using System.Collections.Generic;
using UnityEngine;

/// <summary>Ultimate target marking, execution, trail rendering, and cleanup.</summary>
public partial class PlayerCharacterController
{
    private void StartUltimate()
    {
        ultimateTargets.Clear();
        ultimateTargetSet.Clear();
        ultimateBossesInsideSwipe.Clear();
        ultimateBossesTouchedThisSegment.Clear();
        ultimateTrailPoints.Clear();
        ultimateMarkedRenderers.Clear();
        ultimateExecutionIndex = 0;
        ultimateExecutedKills = 0;
        ultimateSwipeStarted = false;
        ultimateMarkTimeRemaining = UltimateMarkDuration;
        body.linearVelocity = Vector2.zero;

        stateMachine.Change(PlayerStateId.UltimateTargeting);
        EnemyTimeScale = 0f;
        enemyTimeScaleTarget = 0f;
        BeginUltimateLinePresentation();
        cameraController.BeginKillChain(UltimateCameraZoomFactor, 0f, 22f, .08f, MaximumCameraShake * 1.5f);
        PlaySfx(PerfectDodgeSfx != null ? PerfectDodgeSfx : parrySfx);
        onUltimateStarted?.Invoke();
        UltimateStarted?.Invoke();
    }

    private void HandleUltimateTargeting()
    {
        if (input.CancelPressed)
        {
            EndUltimate(false);
            return;
        }

        ultimateMarkTimeRemaining = Mathf.Max(0f, ultimateMarkTimeRemaining - Time.unscaledDeltaTime);
        Vector2 pointerPosition = PointerWorld();
        if (input.PointerPressed)
        {
            ultimateSwipeStarted = true;
            ultimateBossesInsideSwipe.Clear();
            ultimateBossesTouchedThisSegment.Clear();
            ultimateTrailPoints.Clear();
            ultimateLastPointerPosition = pointerPosition;
            AddUltimateTrailPoint(pointerPosition);
            MarkUltimateTargetsAlong(pointerPosition, pointerPosition);
        }

        if (ultimateSwipeStarted && input.PointerHeld)
        {
            MarkUltimateTargetsAlong(ultimateLastPointerPosition, pointerPosition);
            if ((pointerPosition - ultimateLastPointerPosition).sqrMagnitude
                >= UltimateTrailPointDistance * UltimateTrailPointDistance)
            {
                AddUltimateTrailPoint(pointerPosition);
                ultimateLastPointerPosition = pointerPosition;
            }
        }

        if (ultimateSwipeStarted && input.PointerReleased)
        {
            AddUltimateTrailPoint(pointerPosition);
            if (ultimateTargets.Count > 0) CommitUltimate();
            else ResetUltimateSwipe();
            return;
        }

        if (ultimateMarkTimeRemaining > 0f) return;
        if (ultimateTargets.Count > 0) CommitUltimate();
        else EndUltimate(false);
    }

    private void MarkUltimateTargetsAlong(Vector2 from, Vector2 to)
    {
        ultimateBossesTouchedThisSegment.Clear();
        float distance = Vector2.Distance(from, to);
        float sampleSpacing = Mathf.Max(.05f, UltimateMarkRadius * .5f);
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(distance / sampleSpacing));
        for (int sample = 0; sample <= sampleCount; sample++)
        {
            Vector2 point = Vector2.Lerp(from, to, sample / (float)sampleCount);
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(point, UltimateMarkRadius))
            {
                Transform enemy = FindEnemy(hit.transform);
                if (!IsTargetAlive(enemy)) continue;

                bool isBoss = enemy.GetComponent<BossCombatController>() != null;
                if (isBoss)
                {
                    if (!ultimateBossesTouchedThisSegment.Add(enemy)
                        || ultimateBossesInsideSwipe.Contains(enemy)) continue;
                }
                else
                {
                    if (ultimateTargetSet.Contains(enemy)
                        || ultimateTargetSet.Count >= UltimateMaximumTargets) continue;
                    ultimateTargetSet.Add(enemy);
                }

                MarkUltimateTarget(enemy);
            }
        }

        ultimateBossesInsideSwipe.Clear();
        ultimateBossesInsideSwipe.UnionWith(ultimateBossesTouchedThisSegment);
    }

    private void MarkUltimateTarget(Transform enemy)
    {
        ultimateTargets.Add(enemy);
        foreach (SpriteRenderer renderer in enemy.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null || ultimateMarkedRenderers.ContainsKey(renderer)) continue;
            ultimateMarkedRenderers.Add(renderer, renderer.color);
            renderer.color = Color.Lerp(renderer.color, UltimateMarkedColor, .78f);
        }

        Vector2 impactDirection = (Vector2)enemy.position - body.position;
        cameraController.AddKillImpact(impactDirection, .025f, ultimateTargets.Count);
        PlaySfx(HitBladeFleshSfx, .28f);
        onUltimateTargetMarked?.Invoke(enemy);
        UltimateTargetMarked?.Invoke(enemy, ultimateTargets.Count);
    }

    private void CommitUltimate()
    {
        if (ultimateTargets.Count == 0)
        {
            EndUltimate(false);
            return;
        }

        currentMomentum = 0;
        MomentumChanged?.Invoke(currentMomentum, MaximumMomentum);
        ultimateExecutionIndex = 0;
        ultimateExecutedKills = 0;
        stateTimer = .08f;
        BuildUltimateExecutionTrail();
        stateMachine.Change(PlayerStateId.UltimateExecution);
        PlaySfx(DashWindCutSfx != null ? DashWindCutSfx : dashAttackSfx, 1f);
    }

    private void HandleUltimateExecution()
    {
        stateTimer -= Time.unscaledDeltaTime;
        if (stateTimer > 0f) return;

        while (ultimateExecutionIndex < ultimateTargets.Count
            && !IsTargetAlive(ultimateTargets[ultimateExecutionIndex]))
            ultimateExecutionIndex++;

        if (ultimateExecutionIndex >= ultimateTargets.Count)
        {
            BeginUltimateFinisher();
            return;
        }

        Transform target = ultimateTargets[ultimateExecutionIndex++];
        Vector2 targetPosition = target.position;
        Vector2 slashDirection = targetPosition - body.position;
        if (slashDirection.sqrMagnitude <= Mathf.Epsilon) slashDirection = Vector2.right;
        else slashDirection.Normalize();

        UpdateFacing(slashDirection);
        visualAnimator?.SetTrigger(DashAttack);
        perfectDodgeAfterimage?.Play(visualRenderer != null && visualRenderer.flipX);
        body.position = PlayAreaBounds.ClampPosition(
            targetPosition + slashDirection * AttackDashOvershoot,
            Padding);
        body.linearVelocity = Vector2.zero;
        RestoreUltimateTargetColor(target);

        EnemyAgent targetAgent = target.GetComponentInParent<EnemyAgent>();
        EnemyAgent.PlayerAttackResult hitResult = targetAgent != null
            ? targetAgent.ReceivePlayerAttack(
                body.position,
                !targetAgent.IsBossCombatant)
            : EnemyAgent.PlayerAttackResult.Defeated;
        if (hitResult == EnemyAgent.PlayerAttackResult.Guarded)
        {
            PlaySfx(parrySfx);
            stateTimer = UltimateExecutionInterval;
            return;
        }

        PlayBloodHitEffect(target, slashDirection);
        if (hitResult == EnemyAgent.PlayerAttackResult.Damaged)
        {
            PlaySfx(HitBladeFleshSfx, HitBladeFleshVolume);
            stateTimer = UltimateExecutionInterval;
            return;
        }

        SpecialItemDropSpawner.TryDropFromEnemy(target.position);
        if (targetAgent == null) KillEnemy(target);
        RestoreHealth(NormalKillHealthRestore);
        ultimateExecutedKills++;

        cameraController.AddKillImpact(slashDirection, MaximumCameraShake, ultimateExecutedKills);
        PlaySfx(HitBladeFleshSfx, HitBladeFleshVolume);
        PlaySfx(KillConfirmSfx != null ? KillConfirmSfx : killSfx, KillConfirmVolume);
        stateTimer = UltimateExecutionInterval;
    }

    private void BeginUltimateFinisher()
    {
        stateTimer = UltimateFinisherDuration;
        stateMachine.Change(PlayerStateId.UltimateFinisher);
        cameraController.AddKillImpact(Vector2.up, MaximumCameraShake * 1.4f, ultimateExecutedKills + 3);
    }

    private void HandleUltimateFinisher()
    {
        stateTimer -= Time.unscaledDeltaTime;
        if (ultimateLine != null)
        {
            float alpha = Mathf.Clamp01(stateTimer / Mathf.Max(.01f, UltimateFinisherDuration));
            ultimateLine.startColor = WithAlpha(UltimateTrailStartColor, alpha);
            ultimateLine.endColor = WithAlpha(UltimateTrailEndColor, alpha);
        }

        if (stateTimer <= 0f) EndUltimate(true);
    }

    private void ResetUltimateSwipe()
    {
        ultimateSwipeStarted = false;
        ultimateBossesInsideSwipe.Clear();
        ultimateBossesTouchedThisSegment.Clear();
        ultimateTrailPoints.Clear();
        if (ultimateLine != null) ultimateLine.positionCount = 0;
    }

    private void AddUltimateTrailPoint(Vector2 point)
    {
        if (ultimateTrailPoints.Count >= 256) ultimateTrailPoints.RemoveAt(0);
        ultimateTrailPoints.Add(new Vector3(point.x, point.y, transform.position.z - .2f));
        if (ultimateLine == null) return;
        ultimateLine.positionCount = ultimateTrailPoints.Count;
        ultimateLine.SetPositions(ultimateTrailPoints.ToArray());
    }

    private void BuildUltimateExecutionTrail()
    {
        ultimateTrailPoints.Clear();
        ultimateTrailPoints.Add(new Vector3(body.position.x, body.position.y, transform.position.z - .2f));
        foreach (Transform target in ultimateTargets)
        {
            if (!IsTargetAlive(target)) continue;
            Vector3 position = target.position;
            ultimateTrailPoints.Add(new Vector3(position.x, position.y, transform.position.z - .2f));
        }

        if (ultimateLine == null) return;
        ultimateLine.positionCount = ultimateTrailPoints.Count;
        ultimateLine.SetPositions(ultimateTrailPoints.ToArray());
    }

    private void BeginUltimateLinePresentation()
    {
        ultimateLine = arrowLine != null ? arrowLine : targetPathLine;
        if (ultimateLine == null) return;

        ultimateLineOriginalUseWorldSpace = ultimateLine.useWorldSpace;
        ultimateLineOriginalWidthMultiplier = ultimateLine.widthMultiplier;
        ultimateLineOriginalStartColor = ultimateLine.startColor;
        ultimateLineOriginalEndColor = ultimateLine.endColor;
        ultimateLineSettingsSaved = true;
        ultimateLine.useWorldSpace = true;
        ultimateLine.widthMultiplier = ultimateLineOriginalWidthMultiplier * UltimateTrailWidthMultiplier;
        ultimateLine.startColor = UltimateTrailStartColor;
        ultimateLine.endColor = UltimateTrailEndColor;
        ultimateLine.positionCount = 0;
        if (arrowRoot != null) arrowRoot.SetActive(true);
        if (arrowHead != null) arrowHead.gameObject.SetActive(false);
        ultimateLine.enabled = true;
    }

    private void RestoreUltimateLinePresentation()
    {
        if (ultimateLine != null)
        {
            ultimateLine.positionCount = 0;
            if (ultimateLineSettingsSaved)
            {
                ultimateLine.useWorldSpace = ultimateLineOriginalUseWorldSpace;
                ultimateLine.widthMultiplier = ultimateLineOriginalWidthMultiplier;
                ultimateLine.startColor = ultimateLineOriginalStartColor;
                ultimateLine.endColor = ultimateLineOriginalEndColor;
            }
        }

        ultimateLineSettingsSaved = false;
        ultimateLine = null;
        SetArrowVisible(false);
    }

    private void RestoreUltimateTargetColor(Transform target)
    {
        if (target == null) return;
        foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null || !ultimateMarkedRenderers.TryGetValue(renderer, out Color originalColor)) continue;
            renderer.color = originalColor;
            ultimateMarkedRenderers.Remove(renderer);
        }
    }

    private void RestoreAllUltimateTargetColors()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> marked in ultimateMarkedRenderers)
            if (marked.Key != null) marked.Key.color = marked.Value;
        ultimateMarkedRenderers.Clear();
    }

    private void UpdateUltimatePresentation()
    {
        if (!IsUltimateActive) return;

        float pulse = .58f + Mathf.Sin(Time.unscaledTime * 14f) * .2f;
        foreach (KeyValuePair<SpriteRenderer, Color> marked in ultimateMarkedRenderers)
        {
            if (marked.Key != null) marked.Key.color = Color.Lerp(marked.Value, UltimateMarkedColor, pulse);
        }

        if (ultimateLine != null && ultimateLineSettingsSaved && State != PlayerStateId.UltimateFinisher)
            ultimateLine.widthMultiplier = ultimateLineOriginalWidthMultiplier
                * UltimateTrailWidthMultiplier
                * (1f + Mathf.Sin(Time.unscaledTime * 18f) * .08f);
    }

    private void EndUltimate(bool completed)
    {
        int completedKills = ultimateExecutedKills;
        RestoreAllUltimateTargetColors();
        RestoreUltimateLinePresentation();
        ultimateTargets.Clear();
        ultimateTargetSet.Clear();
        ultimateBossesInsideSwipe.Clear();
        ultimateBossesTouchedThisSegment.Clear();
        ultimateTrailPoints.Clear();
        ultimateSwipeStarted = false;
        perfectDodgeAfterimage?.StopAndRestore();
        exitProtectionUntil = Time.unscaledTime + KillChainExitProtection;
        cameraController.EndKillChain();
        stateMachine.Change(PlayerStateId.Locomotion);

        if (!completed) return;
        PlaySheathePresentation();
        onUltimateFinished?.Invoke(completedKills);
        UltimateFinished?.Invoke(completedKills);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a *= Mathf.Clamp01(alpha);
        return color;
    }
}
