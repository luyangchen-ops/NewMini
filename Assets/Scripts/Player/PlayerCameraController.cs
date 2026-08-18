using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerCameraController
{
    public Camera Camera { get; }
    private readonly Transform cameraTransform;
    private readonly Vector3 baseLocalPosition;
    private readonly float baseOrthographicSize;
    private readonly float followDeadZoneRatio;
    private readonly Vector2 followOffset;
    private readonly List<AreaZoomRequest> areaZoomRequests = new List<AreaZoomRequest>();

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
    private float areaZoomResponse = 6f;
    private int areaZoomSequence;
    private Object cinematicOverrideSource;
    private Vector3 cinematicOverridePosition;

    private sealed class AreaZoomRequest
    {
        public Object Source;
        public float OrthographicSize;
        public int Priority;
        public int Sequence;
    }

    public PlayerCameraController(
        Camera camera,
        float followDeadZoneRatio = .65f,
        Vector2 followOffset = default)
    {
        Camera = camera != null ? camera : Camera.main;
        if (Camera == null) return;

        cameraTransform = Camera.transform;
        baseLocalPosition = cameraTransform.localPosition;
        baseOrthographicSize = Camera.orthographicSize;
        this.followDeadZoneRatio = Mathf.Clamp(followDeadZoneRatio, .1f, 1f);
        this.followOffset = followOffset;
        currentFollowPosition = cameraTransform.position;
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition, float z, Vector2 fallback)
    {
        if (Camera == null) return fallback;
        Ray ray = Camera.ScreenPointToRay(screenPosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, z));
        return plane.Raycast(ray, out float distance) ? (Vector2)ray.GetPoint(distance) : fallback;
    }
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

    public void EnterAreaZoom(Object source, float targetOrthographicSize, float blendSpeed, int priority)
    {
        if (source == null) return;

        AreaZoomRequest request = null;
        foreach (AreaZoomRequest candidate in areaZoomRequests)
        {
            if (candidate.Source != source) continue;
            request = candidate;
            break;
        }

        if (request == null)
        {
            request = new AreaZoomRequest { Source = source };
            areaZoomRequests.Add(request);
        }

        request.OrthographicSize = Mathf.Max(.1f, targetOrthographicSize);
        request.Priority = priority;
        request.Sequence = ++areaZoomSequence;
        areaZoomResponse = Mathf.Max(.01f, blendSpeed);
    }

    public void ExitAreaZoom(Object source, float blendSpeed)
    {
        if (source == null) return;

        for (int i = areaZoomRequests.Count - 1; i >= 0; i--)
        {
            if (areaZoomRequests[i].Source == source) areaZoomRequests.RemoveAt(i);
        }

        areaZoomResponse = Mathf.Max(.01f, blendSpeed);
    }

    /// <summary>Temporarily replaces normal follow, combat offset and shake with an authored camera position.</summary>
    public void SetCinematicOverride(Object source, Vector3 position)
    {
        if (source == null || Camera == null) return;
        cinematicOverrideSource = source;
        cinematicOverridePosition = position;
        currentFollowPosition = position;
    }

    public void ClearCinematicOverride(Object source)
    {
        if (source == null || cinematicOverrideSource != source) return;
        cinematicOverrideSource = null;
        if (cameraTransform != null) currentFollowPosition = cameraTransform.position;
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (Camera == null || cameraTransform == null) return;

        if (cinematicOverrideSource != null)
        {
            cameraTransform.position = cinematicOverridePosition;
            currentFollowPosition = cinematicOverridePosition;
            return;
        }

        float blend = 1f - Mathf.Exp(-response * Mathf.Max(0f, unscaledDeltaTime));
        Vector3 clampedFollowPosition = PlayAreaBounds.ClampCameraPosition(
            Camera,
            hasFollowTarget ? desiredFollowPosition : baseLocalPosition);
        currentFollowPosition = Vector3.Lerp(
            currentFollowPosition,
            clampedFollowPosition,
            blend);
        currentOffset = Vector2.Lerp(currentOffset, killChainActive ? desiredOffset : Vector2.zero, blend);

        float areaTargetSize = GetAreaTargetOrthographicSize();
        float targetSize = areaTargetSize * (killChainActive ? zoomFactor : 1f);
        float zoomResponse = killChainActive ? response : areaZoomResponse;
        float zoomBlend = 1f - Mathf.Exp(-zoomResponse * Mathf.Max(0f, unscaledDeltaTime));
        Camera.orthographicSize = Mathf.Lerp(Camera.orthographicSize, targetSize, zoomBlend);

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
        cameraTransform.position = PlayAreaBounds.ClampCameraPosition(Camera, finalPosition);
    }

    public void RestoreImmediately()
    {
        if (Camera == null || cameraTransform == null) return;
        killChainActive = false;
        currentOffset = desiredOffset = Vector2.zero;
        shakeAmplitude = shakeRemaining = 0f;
        Camera.orthographicSize = GetAreaTargetOrthographicSize();
        currentFollowPosition = PlayAreaBounds.ClampCameraPosition(
            Camera,
            hasFollowTarget ? desiredFollowPosition : baseLocalPosition);
        cameraTransform.position = currentFollowPosition;
    }

    private void SetFollowTarget(Vector2 playerPosition)
    {
        playerPosition += followOffset;
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

        desiredFollowPosition = PlayAreaBounds.ClampCameraPosition(Camera, desiredFollowPosition);
    }

    private void AddShake(float amplitude, float duration)
    {
        float activeAmplitude = shakeRemaining > 0f ? shakeAmplitude : 0f;
        shakeAmplitude = Mathf.Min(maximumShake, Mathf.Max(activeAmplitude, Mathf.Max(0f, amplitude)));
        shakeRemaining = Mathf.Max(shakeRemaining, duration);
    }

    private float GetAreaTargetOrthographicSize()
    {
        AreaZoomRequest activeRequest = null;
        for (int i = areaZoomRequests.Count - 1; i >= 0; i--)
        {
            AreaZoomRequest request = areaZoomRequests[i];
            if (request.Source == null)
            {
                areaZoomRequests.RemoveAt(i);
                continue;
            }

            if (activeRequest == null
                || request.Priority > activeRequest.Priority
                || request.Priority == activeRequest.Priority && request.Sequence > activeRequest.Sequence)
            {
                activeRequest = request;
            }
        }

        return activeRequest != null ? activeRequest.OrthographicSize : baseOrthographicSize;
    }
}
