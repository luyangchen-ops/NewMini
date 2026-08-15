using UnityEngine;

/// <summary>
/// Fades a top-down prop while the player is visually behind it, without
/// disabling the prop or its collision. Add this component to the prop root.
/// </summary>
public sealed class TopDownOccluderFade : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private string fallbackTargetName = "Player";
    [Tooltip("Offset from the target transform to its ground-contact point.")]
    [SerializeField] private float targetGroundYOffset = -0.72f;

    [Header("Occluder")]
    [SerializeField] private Transform sortAnchor;
    [SerializeField] private SpriteRenderer[] occludingRenderers;
    [Tooltip("Optional authored zone that defines where the target is truly hidden. When assigned, it replaces the broad Sprite bounds check.")]
    [SerializeField] private Collider2D occlusionZone;
    [Tooltip("How far above the prop ground point the target must be before it counts as behind.")]
    [SerializeField, Min(0f)] private float behindThreshold = 0.05f;
    [SerializeField, Min(0f)] private float horizontalPadding = 0.1f;
    [SerializeField, Min(0f)] private float verticalPadding = 0.1f;

    [Header("Fade")]
    [SerializeField, Range(0f, 1f)] private float fadedAlpha = 0.35f;
    [SerializeField, Min(0.01f)] private float fadeSpeed = 6f;

    private Color[] originalColors;
    private float currentFade = 1f;
    private float nextTargetSearchTime;

    private void Awake()
    {
        CacheRenderers();
        FindTarget();
        ApplyFade(1f);
    }

    private void LateUpdate()
    {
        if (target == null && Time.unscaledTime >= nextTargetSearchTime)
        {
            FindTarget();
        }

        float desiredFade = ShouldFade() ? fadedAlpha : 1f;
        currentFade = Mathf.MoveTowards(
            currentFade,
            desiredFade,
            fadeSpeed * Time.unscaledDeltaTime);
        ApplyFade(currentFade);
    }

    private bool ShouldFade()
    {
        if (target == null || occludingRenderers == null || occludingRenderers.Length == 0)
        {
            return false;
        }

        Vector2 targetGroundPoint = (Vector2)target.position
            + Vector2.up * targetGroundYOffset;
        Transform anchor = sortAnchor != null ? sortAnchor : transform;

        if (targetGroundPoint.y <= anchor.position.y + behindThreshold)
        {
            return false;
        }

        if (occlusionZone != null)
        {
            return occlusionZone.OverlapPoint(targetGroundPoint);
        }

        if (!TryGetRendererBounds(out Bounds bounds))
        {
            return false;
        }

        return targetGroundPoint.x >= bounds.min.x - horizontalPadding
            && targetGroundPoint.x <= bounds.max.x + horizontalPadding
            && targetGroundPoint.y >= bounds.min.y - verticalPadding
            && targetGroundPoint.y <= bounds.max.y + verticalPadding;
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        foreach (SpriteRenderer spriteRenderer in occludingRenderers)
        {
            if (spriteRenderer == null || !spriteRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = spriteRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(spriteRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private void FindTarget()
    {
        nextTargetSearchTime = Time.unscaledTime + 1f;

        if (string.IsNullOrWhiteSpace(fallbackTargetName))
        {
            return;
        }

        GameObject targetObject = GameObject.Find(fallbackTargetName);
        if (targetObject != null)
        {
            target = targetObject.transform;
        }
    }

    private void CacheRenderers()
    {
        if (occludingRenderers == null || occludingRenderers.Length == 0)
        {
            occludingRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        originalColors = new Color[occludingRenderers.Length];
        for (int i = 0; i < occludingRenderers.Length; i++)
        {
            originalColors[i] = occludingRenderers[i] != null
                ? occludingRenderers[i].color
                : Color.white;
        }
    }

    private void ApplyFade(float alphaMultiplier)
    {
        if (occludingRenderers == null || originalColors == null)
        {
            return;
        }

        for (int i = 0; i < occludingRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = occludingRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            Color color = originalColors[i];
            color.a *= alphaMultiplier;
            spriteRenderer.color = color;
        }
    }

    private void OnDisable()
    {
        ApplyFade(1f);
        currentFade = 1f;
    }

    private void OnValidate()
    {
        fadedAlpha = Mathf.Clamp01(fadedAlpha);
        fadeSpeed = Mathf.Max(0.01f, fadeSpeed);
        behindThreshold = Mathf.Max(0f, behindThreshold);
        horizontalPadding = Mathf.Max(0f, horizontalPadding);
        verticalPadding = Mathf.Max(0f, verticalPadding);
    }
}
