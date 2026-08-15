using UnityEngine;

public sealed class PlayerCameraController
{
    public Camera Camera { get; }
    private readonly Transform cameraTransform;
    private readonly Vector3 baseLocalPosition;
    private readonly float baseOrthographicSize;
    private readonly float followDeadZoneRatio;

    private bool killChainActive;
    private float zoomFactor = 1f;
    private float focusOffset;
    private float response = 16f;
    private float maximumShake;
    private float shakeAmplitude;
    private float shakeRemaining;
    private Vector2 currentOffset;
    private Vector2 desiredOffset;
    private bool hasFollowTarget;
    private Vector3 desiredFollowPosition;
    private Vector3 currentFollowPosition;

    public PlayerCameraController(Camera camera, float followDeadZoneRatio = .65f)
    {
        Camera = camera != null ? camera : Camera.main;
        if (Camera == null) return;

        cameraTransform = Camera.transform;
        baseLocalPosition = cameraTransform.localPosition;
        baseOrthographicSize = Camera.orthographicSize;
        this.followDeadZoneRatio = Mathf.Clamp(followDeadZoneRatio, .1f, 1f);
        currentFollowPosition = cameraTransform.position;
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition, float z, Vector2 fallback)
    {
        if (Camera == null) return fallback;
        Ray ray = Camera.ScreenPointToRay(screenPosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, z));
        return plane.Raycast(ray, out float distance) ? (Vector2)ray.GetPoint(distance) : fallback;
    }
    public Vector2 Clamp(Vector2 position, float padding, float z) => CameraBounds.Clamp(Camera, position, padding, z);

    public void BeginKillChain(float targetZoomFactor, float targetFocusOffset, float targetResponse,
        float perfectDodgeShake, float targetMaximumShake)
    {
        killChainActive = true;
        zoomFactor = Mathf.Clamp(targetZoomFactor, .75f, 1f);
        focusOffset = Mathf.Max(0f, targetFocusOffset);
        response = Mathf.Max(.01f, targetResponse);
        maximumShake = Mathf.Max(0f, targetMaximumShake);
        AddShake(perfectDodgeShake, .06f);
    }

    public void SetFocus(Vector2 playerPosition, Transform target)
    {
        SetFollowTarget(playerPosition);

        if (!killChainActive || target == null)
        {
            desiredOffset = Vector2.zero;
            return;
        }

        Vector2 direction = (Vector2)target.position - playerPosition;
        desiredOffset = direction.sqrMagnitude > Mathf.Epsilon
            ? direction.normalized * focusOffset
            : Vector2.zero;
    }

    public void AddKillImpact(Vector2 dashDirection, float baseAmplitude, int killCount)
    {
        float comboMultiplier = 1f + Mathf.Min(Mathf.Max(0, killCount - 1), 4) * .08f;
        AddShake(baseAmplitude * comboMultiplier, .065f);
        currentOffset -= dashDirection.normalized * Mathf.Min(baseAmplitude, maximumShake) * .35f;
    }

    public void EndKillChain()
    {
        killChainActive = false;
        desiredOffset = Vector2.zero;
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (Camera == null || cameraTransform == null) return;

        float blend = 1f - Mathf.Exp(-response * Mathf.Max(0f, unscaledDeltaTime));
        Vector3 clampedFollowPosition = CameraBounds.ClampCameraPosition(
            Camera,
            hasFollowTarget ? desiredFollowPosition : baseLocalPosition);
        currentFollowPosition = Vector3.Lerp(
            currentFollowPosition,
            clampedFollowPosition,
            blend);
        currentOffset = Vector2.Lerp(currentOffset, killChainActive ? desiredOffset : Vector2.zero, blend);

        float targetSize = baseOrthographicSize * (killChainActive ? zoomFactor : 1f);
        Camera.orthographicSize = Mathf.Lerp(Camera.orthographicSize, targetSize, blend);

        Vector2 shake = Vector2.zero;
        if (shakeRemaining > 0f)
        {
            shakeRemaining = Mathf.Max(0f, shakeRemaining - unscaledDeltaTime);
            shake = Random.insideUnitCircle * shakeAmplitude * (shakeRemaining / .065f);
        }

        Vector3 finalPosition = currentFollowPosition + new Vector3(
            currentOffset.x + shake.x,
            currentOffset.y + shake.y,
            0f);
        cameraTransform.position = CameraBounds.ClampCameraPosition(Camera, finalPosition);
    }

    public void RestoreImmediately()
    {
        if (Camera == null || cameraTransform == null) return;
        killChainActive = false;
        currentOffset = desiredOffset = Vector2.zero;
        shakeAmplitude = shakeRemaining = 0f;
        Camera.orthographicSize = baseOrthographicSize;
        currentFollowPosition = CameraBounds.ClampCameraPosition(
            Camera,
            hasFollowTarget ? desiredFollowPosition : baseLocalPosition);
        cameraTransform.position = currentFollowPosition;
    }

    private void SetFollowTarget(Vector2 playerPosition)
    {
        if (!hasFollowTarget)
        {
            desiredFollowPosition = currentFollowPosition;
            hasFollowTarget = true;
        }

        float halfHeight = Camera.orthographicSize * followDeadZoneRatio;
        float halfWidth = halfHeight * Camera.aspect;
        float horizontalOffset = playerPosition.x - desiredFollowPosition.x;
        float verticalOffset = playerPosition.y - desiredFollowPosition.y;

        if (Mathf.Abs(horizontalOffset) > halfWidth)
        {
            desiredFollowPosition.x += horizontalOffset - Mathf.Sign(horizontalOffset) * halfWidth;
        }

        if (Mathf.Abs(verticalOffset) > halfHeight)
        {
            desiredFollowPosition.y += verticalOffset - Mathf.Sign(verticalOffset) * halfHeight;
        }

        desiredFollowPosition = CameraBounds.ClampCameraPosition(Camera, desiredFollowPosition);
    }

    private void AddShake(float amplitude, float duration)
    {
        float activeAmplitude = shakeRemaining > 0f ? shakeAmplitude : 0f;
        shakeAmplitude = Mathf.Min(maximumShake, Mathf.Max(activeAmplitude, Mathf.Max(0f, amplitude)));
        shakeRemaining = Mathf.Max(shakeRemaining, duration);
    }
}
