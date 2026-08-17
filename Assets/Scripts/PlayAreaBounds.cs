using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayAreaBounds : MonoBehaviour
{
    [SerializeField] private Collider2D boundsCollider;

    public static PlayAreaBounds Active { get; private set; }

    private void Awake()
    {
        boundsCollider ??= GetComponent<Collider2D>();
        Active = this;
    }

    private void OnEnable() => Active = this;

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    public static bool TryGetWorldBounds(out Bounds worldBounds)
    {
        if (Active == null)
        {
            Active = FindFirstObjectByType<PlayAreaBounds>();
        }

        if (Active == null || Active.boundsCollider == null)
        {
            worldBounds = default;
            return false;
        }

        worldBounds = Active.boundsCollider.bounds;
        return true;
    }

    /// <summary>Clamps an actor to the authored level boundary when one exists.</summary>
    public static bool TryClampPosition(Vector2 position, float padding, out Vector2 clampedPosition)
    {
        if (!TryGetWorldBounds(out Bounds worldBounds))
        {
            clampedPosition = position;
            return false;
        }

        padding = Mathf.Max(0f, padding);
        Vector2 minimum = (Vector2)worldBounds.min + Vector2.one * padding;
        Vector2 maximum = (Vector2)worldBounds.max - Vector2.one * padding;
        if (minimum.x > maximum.x) minimum.x = maximum.x = worldBounds.center.x;
        if (minimum.y > maximum.y) minimum.y = maximum.y = worldBounds.center.y;

        clampedPosition = new Vector2(
            Mathf.Clamp(position.x, minimum.x, maximum.x),
            Mathf.Clamp(position.y, minimum.y, maximum.y));
        return true;
    }

    /// <summary>Returns the input unchanged when this scene has no authored play-area boundary.</summary>
    public static Vector2 ClampPosition(Vector2 position, float padding)
    {
        TryClampPosition(position, padding, out Vector2 clampedPosition);
        return clampedPosition;
    }

    /// <summary>Clamps a camera center so its viewport remains inside the authored level boundary.</summary>
    public static Vector3 ClampCameraPosition(Camera camera, Vector3 position)
    {
        if (camera == null || !TryGetWorldBounds(out Bounds worldBounds)) return position;

        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        float minimumX = worldBounds.min.x + halfWidth;
        float maximumX = worldBounds.max.x - halfWidth;
        float minimumY = worldBounds.min.y + halfHeight;
        float maximumY = worldBounds.max.y - halfHeight;

        float x = minimumX <= maximumX ? Mathf.Clamp(position.x, minimumX, maximumX) : worldBounds.center.x;
        float y = minimumY <= maximumY ? Mathf.Clamp(position.y, minimumY, maximumY) : worldBounds.center.y;
        return new Vector3(x, y, position.z);
    }
}
