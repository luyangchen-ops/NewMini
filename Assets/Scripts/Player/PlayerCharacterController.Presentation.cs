using UnityEngine;

/// <summary>Animation, camera feedback, authored overlays, audio, and collision presentation.</summary>
public partial class PlayerCharacterController
{
    private void PlayBloodHitEffect(Transform enemy, Vector2 slashDirection)
    {
        if (bloodHitEffectPrefab == null || enemy == null) return;

        Collider2D hitCollider = enemy.GetComponentInChildren<Collider2D>();
        Vector2 hitPosition = hitCollider != null ? hitCollider.ClosestPoint(body.position) : enemy.position;
        float targetSize = 1f;
        int sortingOrder = 1;
        foreach (SpriteRenderer renderer in enemy.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null) continue;
            targetSize = Mathf.Max(targetSize, renderer.bounds.size.x, renderer.bounds.size.y);
            sortingOrder = Mathf.Max(sortingOrder, renderer.sortingOrder + 1);
        }

        BloodHitEffect effect = Instantiate(bloodHitEffectPrefab);
        effect.PlayAt(hitPosition, slashDirection, targetSize, sortingOrder);
    }

    private void OnStateChanged(PlayerStateId previous, PlayerStateId next)
    {
        switch (next)
        {
            case PlayerStateId.PerfectDodgeFreeze:
            case PlayerStateId.KillChainImpact:
                enemyTimeScaleTarget = 0f;
                break;
            case PlayerStateId.KillChainTargeting:
                enemyTimeScaleTarget = BulletTimeScale;
                break;
            case PlayerStateId.KillChainDash:
                enemyTimeScaleTarget = DashEnemyTimeScale;
                break;
            case PlayerStateId.UltimateTargeting:
            case PlayerStateId.UltimateExecution:
            case PlayerStateId.UltimateFinisher:
                enemyTimeScaleTarget = 0f;
                break;
            default:
                enemyTimeScaleTarget = 1f;
                break;
        }

        if (visualAnimator != null)
            visualAnimator.speed = next == PlayerStateId.PerfectDodgeFreeze || next == PlayerStateId.KillChainImpact
                ? 0f
                : animatorBaseSpeed;

        if (!IsUltimateState(next)) SetArrowVisible(false);
    }

    private void UpdateEnemyTimeScale()
    {
        float duration = enemyTimeScaleTarget < EnemyTimeScale
            ? BulletTimeEnterDuration
            : BulletTimeExitDuration;
        EnemyTimeScale = Mathf.MoveTowards(
            EnemyTimeScale,
            enemyTimeScaleTarget,
            Time.unscaledDeltaTime / Mathf.Max(.01f, duration));
    }

    private void UpdateVisuals()
    {
        if (visualAnimator == null) return;
        if (bossGuardControlLocked)
        {
            visualAnimator.SetFloat(Speed, 0f);
            return;
        }
        if (presentationIdleActive)
        {
            visualAnimator.SetFloat(Speed, 0f);
            visualAnimator.SetInteger(VerticalDirection, 0);
            return;
        }
        if (presentationLocomotionActive)
        {
            UpdateFacing(presentationLocomotionDirection);
            visualAnimator.SetFloat(Speed, 1f);
            return;
        }
        visualAnimator.SetFloat(Speed, State == PlayerStateId.Locomotion ? input.Move.magnitude : 0f);
    }

    private void UpdateTargetPresentation()
    {
        Transform target = lockedDashTarget != null ? lockedDashTarget : currentTarget;
        SetTargetPresentation(target);
        if (target == null) return;

        Transform anchor = targetReticleAnchor != null
            ? targetReticleAnchor
            : targetReticleRoot != null ? targetReticleRoot.transform : null;
        if (anchor != null)
        {
            Vector3 position = anchor.position;
            anchor.position = new Vector3(target.position.x, target.position.y, position.z);
        }

        if (targetPathLine != null)
        {
            targetPathLine.positionCount = 2;
            targetPathLine.SetPosition(0, body.position);
            targetPathLine.SetPosition(1, target.position);
        }
    }

    private void SetTargetPresentation(Transform target)
    {
        bool visible = target != null && IsKillChainActive;
        if (targetReticleRoot != null) targetReticleRoot.SetActive(visible);
        if (targetPathLine != null && targetPathLine != ultimateLine) targetPathLine.enabled = visible;
    }

    private void UpdateChainWindowPresentation()
    {
        if (chainWindowRenderer == null) return;
        chainWindowRenderer.GetPropertyBlock(feedbackProperties);
        feedbackProperties.SetFloat(ChainWindow01, IsKillChainActive ? KillChainWindowNormalized : 0f);
        chainWindowRenderer.SetPropertyBlock(feedbackProperties);
    }

    private void Face(float x)
    {
        if (visualRenderer != null && Mathf.Abs(x) > .01f) visualRenderer.flipX = x < 0f;
    }

    private void UpdateFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude <= .0001f) return;

        // Vertical art is selected only when the vertical input is dominant. Horizontal
        // movement retains the existing flip-X presentation and horizontal clips.
        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
        {
            visualAnimator?.SetInteger(VerticalDirection, direction.y > 0f ? 1 : -1);
            return;
        }

        visualAnimator?.SetInteger(VerticalDirection, 0);
        Face(direction.x);
    }

    private Vector2 PointerWorld() => cameraController.ScreenToWorld(
        input.PointerScreenPosition,
        transform.position.z,
        body.position);

    public void EnterCameraZoomZone(UnityEngine.Object source, float targetOrthographicSize, float blendSpeed, int priority = 0)
    {
        cameraController?.EnterAreaZoom(source, targetOrthographicSize, blendSpeed, priority);
    }

    public void ExitCameraZoomZone(UnityEngine.Object source, float blendSpeed)
    {
        cameraController?.ExitAreaZoom(source, blendSpeed);
    }

    public Camera WorldCamera => cameraController?.Camera != null
        ? cameraController.Camera
        : worldCamera != null ? worldCamera : Camera.main;

    public void SetCameraCinematicOverride(UnityEngine.Object source, Vector3 position)
    {
        cameraController?.SetCinematicOverride(source, position);
    }

    public void ClearCameraCinematicOverride(UnityEngine.Object source)
    {
        cameraController?.ClearCinematicOverride(source);
    }

    public void IgnoreEnemyCollisions(EnemyAgent enemy)
    {
        if (enemy == null) return;
        playerColliders ??= GetComponentsInChildren<Collider2D>(true);
        Collider2D[] enemyColliders = enemy.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D playerCollider in playerColliders)
        {
            if (playerCollider == null) continue;
            foreach (Collider2D enemyCollider in enemyColliders)
            {
                if (enemyCollider != null) Physics2D.IgnoreCollision(playerCollider, enemyCollider, true);
            }
        }
    }

    private void IgnoreExistingEnemyCollisions()
    {
        EnemyAgent[] enemies = FindObjectsByType<EnemyAgent>(FindObjectsInactive.Include);
        foreach (EnemyAgent enemy in enemies) IgnoreEnemyCollisions(enemy);
    }

    private void SetArrowVisible(bool visible)
    {
        bool sharesRootWithOverlay = arrowRoot != null && killChainRangeOverlay != null
            && killChainRangeOverlay.transform == arrowRoot.transform;
        if (arrowRoot != null && !sharesRootWithOverlay) arrowRoot.SetActive(visible);
        else if (sharesRootWithOverlay) arrowRoot.SetActive(true);
        if (arrowLine != null) arrowLine.enabled = visible;
        if (arrowHead != null) arrowHead.gameObject.SetActive(visible);
    }

    private void UpdateRangeOverlayPresentation()
    {
        if (killChainRangeOverlay == null) return;

        float slowRange = Mathf.Max(.01f, 1f - BulletTimeScale);
        float strength = Mathf.Clamp01((1f - EnemyTimeScale) / slowRange);
        bool visible = strength > .001f;
        killChainRangeOverlay.enabled = visible;
        if (!visible || killChainRangeOverlay.sprite == null) return;

        Transform overlayTransform = killChainRangeOverlay.transform;
        overlayTransform.position = new Vector3(body.position.x, body.position.y, transform.position.z - .1f);
        float spriteSize = Mathf.Max(.01f, killChainRangeOverlay.sprite.bounds.size.x);
        overlayTransform.localScale = Vector3.one * (rangeOverlayWorldDiameter / spriteSize);
        killChainRangeOverlay.GetPropertyBlock(feedbackProperties);
        feedbackProperties.SetFloat(RangeRadius01, AttackDashDistance / rangeOverlayWorldDiameter);
        feedbackProperties.SetFloat(EffectStrength, strength);
        killChainRangeOverlay.SetPropertyBlock(feedbackProperties);
    }

    private void HideRangeOverlayImmediately()
    {
        if (killChainRangeOverlay == null) return;
        killChainRangeOverlay.enabled = false;
        if (feedbackProperties == null) return;
        killChainRangeOverlay.GetPropertyBlock(feedbackProperties);
        feedbackProperties.SetFloat(EffectStrength, 0f);
        killChainRangeOverlay.SetPropertyBlock(feedbackProperties);
    }

    private void UpdateKillChainAudio()
    {
        float slowRange = Mathf.Max(.01f, 1f - BulletTimeScale);
        float bulletTimeLoopEnvelope = Mathf.Clamp01((1f - EnemyTimeScale) / slowRange);
        float volume = bulletTimeLoopEnvelope * bulletTimeLoopVolume;
        GameAudioManager.SetSfxLoop(BulletTimeLoopSfx, volume, bulletTimeLoopEnvelope > .001f);
    }

    private static void PlaySfx(AudioClip clip, float volumeScale = 1f, float pitch = 1f) =>
        GameAudioManager.PlaySfx(clip, volumeScale, pitch);

    private void PlaySheathePresentation()
    {
        visualAnimator?.SetTrigger(Sheathe);
        PlaySfx(KillChainEndSfx, killChainEndVolume);
    }

    private void PlayKillSfx()
    {
        float pitch = 1f + Mathf.Min(Mathf.Max(0, killChainCount - 1), 4) * .035f;
        PlaySfx(HitBladeFleshSfx, hitBladeFleshVolume, pitch);
        AudioClip confirmation = KillConfirmSfx != null ? KillConfirmSfx : killSfx;
        PlaySfx(confirmation, killConfirmVolume, pitch);
    }
}
