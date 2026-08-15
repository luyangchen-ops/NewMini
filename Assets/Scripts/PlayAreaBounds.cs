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
}
