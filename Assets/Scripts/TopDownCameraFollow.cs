using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class TopDownCameraFollow : MonoBehaviour
{
    [Header("Authored References")]
    [SerializeField] private Transform target;
    [SerializeField] private SpriteRenderer mapRenderer;

    [Header("Follow")]
    [SerializeField, Min(0f)] private float smoothTime = 0.16f;
    [SerializeField] private Vector2 targetOffset;

    private Camera controlledCamera;
    private Vector3 followVelocity;
    private Object cinematicOverrideSource;
    private Vector3 cinematicOverridePosition;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        if (controlledCamera == null)
        {
            controlledCamera = GetComponent<Camera>();
        }

        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (controlledCamera == null)
        {
            return;
        }

        if (cinematicOverrideSource != null)
        {
            // The presentation owner already supplies an eased path. Apply it exactly
            // so normal camera smoothing cannot lag behind or offset Timeline framing.
            transform.position = cinematicOverridePosition;
            return;
        }

        if (target == null || mapRenderer == null) return;

        Vector3 desiredPosition = GetClampedPosition();
        transform.position = smoothTime <= 0f
            ? desiredPosition
            : Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref followVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
    }

    /// <summary>Temporarily takes camera control away from normal player follow and map clamping.</summary>
    public void SetCinematicOverride(Object source, Vector3 position)
    {
        if (source == null) return;
        cinematicOverrideSource = source;
        cinematicOverridePosition = position;
        followVelocity = Vector3.zero;
    }

    public void ClearCinematicOverride(Object source)
    {
        if (source != null && cinematicOverrideSource == source) cinematicOverrideSource = null;
    }

    private void SnapToTarget()
    {
        if (target == null || mapRenderer == null || controlledCamera == null)
        {
            return;
        }

        followVelocity = Vector3.zero;
        transform.position = GetClampedPosition();
    }

    private Vector3 GetClampedPosition()
    {
        Bounds mapBounds = mapRenderer.bounds;
        float halfHeight = controlledCamera.orthographicSize;
        float halfWidth = halfHeight * controlledCamera.aspect;

        float targetX = target.position.x + targetOffset.x;
        float targetY = target.position.y + targetOffset.y;

        float minimumX = mapBounds.min.x + halfWidth;
        float maximumX = mapBounds.max.x - halfWidth;
        float minimumY = mapBounds.min.y + halfHeight;
        float maximumY = mapBounds.max.y - halfHeight;

        float clampedX = minimumX <= maximumX
            ? Mathf.Clamp(targetX, minimumX, maximumX)
            : mapBounds.center.x;
        float clampedY = minimumY <= maximumY
            ? Mathf.Clamp(targetY, minimumY, maximumY)
            : mapBounds.center.y;

        return new Vector3(clampedX, clampedY, transform.position.z);
    }

    private void OnValidate()
    {
        smoothTime = Mathf.Max(0f, smoothTime);
    }
}
