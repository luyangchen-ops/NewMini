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
        if (target == null || mapRenderer == null || controlledCamera == null)
        {
            return;
        }

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
