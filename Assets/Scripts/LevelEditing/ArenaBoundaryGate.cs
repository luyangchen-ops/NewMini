using UnityEngine;

/// <summary>
/// A physical segment used to seal an arena. Keep the collider and visuals on this object
/// so a designer can see and edit the complete boundary in the scene hierarchy.
/// </summary>
[DisallowMultipleComponent]
public sealed class ArenaBoundaryGate : MonoBehaviour
{
    [SerializeField] private Collider2D[] blockingColliders;
    [SerializeField] private GameObject[] lockedVisuals;
    [SerializeField] private bool startsLocked;
    [Header("Side Boundary Warning")]
    [Tooltip("Only vertical gates (the left and right arena boundaries) receive the red warning line.")]
    [SerializeField] private LineRenderer[] lockedFenceBands;
    [SerializeField, Min(.01f)] private float fenceRevealDistance = 4.5f;
    [SerializeField] private Color fenceCoreColor = new Color(1f, .08f, .08f, .62f);

    public bool IsLocked { get; private set; }
    private PlayerCharacterController player;

    private void Awake()
    {
        if (blockingColliders == null || blockingColliders.Length == 0)
            blockingColliders = GetComponents<Collider2D>();
        EnsureLockedFence();
        SetLocked(startsLocked);
    }

    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        if (blockingColliders != null)
        {
            foreach (Collider2D blocker in blockingColliders)
                if (blocker != null) blocker.enabled = locked;
        }

        if (lockedVisuals != null)
        {
            foreach (GameObject visual in lockedVisuals)
                if (visual != null) visual.SetActive(locked);
        }

        RefreshFenceVisibility();
    }

    private void Update() => RefreshFenceVisibility();

    private void EnsureLockedFence()
    {
        Collider2D fenceCollider = null;
        if (blockingColliders != null)
        {
            foreach (Collider2D collider in blockingColliders)
            {
                if (collider != null)
                {
                    fenceCollider = collider;
                    break;
                }
            }
        }

        if (fenceCollider == null || !IsVerticalBoundary(fenceCollider.bounds)) return;

        if (lockedFenceBands == null || lockedFenceBands.Length != 3)
        {
            lockedFenceBands = new LineRenderer[3];
            CreateFenceBand(0, "Img_RedFenceOuter", 1.45f, .08f);
            CreateFenceBand(1, "Img_RedFenceMiddle", .72f, .22f);
            CreateFenceBand(2, "Img_RedFenceCore", .18f, 1f);
        }

        Bounds bounds = fenceCollider.bounds;
        float z = transform.position.z - .05f;
        Vector3 outward = GetOutwardDirection(bounds);
        for (int index = 0; index < lockedFenceBands.Length; index++)
        {
            LineRenderer band = lockedFenceBands[index];
            if (band == null) continue;
            // Keep the bright edge against the arena, then push the softer bands
            // progressively outward instead of producing a symmetric glow.
            float outwardOffset = index == 0 ? .72f : index == 1 ? .32f : 0f;
            Vector3 center = new Vector3(bounds.center.x, bounds.center.y, z) + outward * outwardOffset;
            band.SetPositions(new[]
            {
                new Vector3(center.x, bounds.min.y, z),
                new Vector3(center.x, bounds.max.y, z)
            });
        }
    }

    private void RefreshFenceVisibility()
    {
        if (lockedFenceBands == null || lockedFenceBands.Length == 0) return;

        float visibility = 0f;
        if (IsLocked)
        {
            player ??= FindAnyObjectByType<PlayerCharacterController>();
            Collider2D fenceCollider = GetBlockingCollider();
            if (player != null && fenceCollider != null)
            {
                float distance = Mathf.Sqrt(fenceCollider.bounds.SqrDistance(player.transform.position));
                visibility = Mathf.SmoothStep(0f, 1f, 1f - distance / fenceRevealDistance);
            }
        }

        for (int index = 0; index < lockedFenceBands.Length; index++)
        {
            LineRenderer band = lockedFenceBands[index];
            if (band == null) continue;
            float alphaMultiplier = index == 0 ? .08f : index == 1 ? .22f : 1f;
            Color color = new(fenceCoreColor.r, fenceCoreColor.g, fenceCoreColor.b,
                fenceCoreColor.a * alphaMultiplier * visibility);
            band.startColor = color;
            band.endColor = color;
            band.enabled = visibility > .001f;
        }
    }

    private void CreateFenceBand(int index, string bandName, float width, float alphaMultiplier)
    {
        GameObject bandObject = new(bandName);
        bandObject.transform.SetParent(transform, false);
        LineRenderer band = bandObject.AddComponent<LineRenderer>();
        band.useWorldSpace = true;
        band.positionCount = 2;
        band.widthMultiplier = width;
        band.sortingOrder = 100;
        band.material = new Material(Shader.Find("Sprites/Default"));
        Color color = fenceCoreColor;
        color.a *= alphaMultiplier;
        band.startColor = color;
        band.endColor = color;
        lockedFenceBands[index] = band;
    }

    private Collider2D GetBlockingCollider()
    {
        if (blockingColliders == null) return null;
        foreach (Collider2D collider in blockingColliders)
            if (collider != null) return collider;
        return null;
    }

    private static bool IsVerticalBoundary(Bounds bounds) => bounds.size.y > bounds.size.x;

    private Vector3 GetOutwardDirection(Bounds bounds)
    {
        Transform arenaRoot = transform.parent != null ? transform.parent.parent : null;
        float arenaCenterX = arenaRoot != null ? arenaRoot.position.x : 0f;
        return bounds.center.x < arenaCenterX ? Vector3.left : Vector3.right;
    }
}
