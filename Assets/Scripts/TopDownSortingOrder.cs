using UnityEngine;

/// <summary>
/// Keeps top-down characters and props ordered by their ground-contact point.
/// Objects lower on screen receive a larger sorting order and render in front.
/// </summary>
[ExecuteAlways]
public sealed class TopDownSortingOrder : MonoBehaviour
{
    [SerializeField] private Transform sortAnchor;
    [SerializeField] private float sortYOffset;
    [SerializeField] private int baseSortingOrder = 1000;
    [SerializeField, Min(1)] private int ordersPerWorldUnit = 100;
    [SerializeField] private Renderer[] targetRenderers;

    private void OnEnable()
    {
        CacheRenderers();
        ApplySortingOrder();
    }

    private void LateUpdate()
    {
        ApplySortingOrder();
    }

    private void OnValidate()
    {
        ordersPerWorldUnit = Mathf.Max(1, ordersPerWorldUnit);
        CacheRenderers();
        ApplySortingOrder();
    }

    private void CacheRenderers()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private void ApplySortingOrder()
    {
        if (targetRenderers == null)
        {
            return;
        }

        Transform anchor = sortAnchor != null ? sortAnchor : transform;
        int order = baseSortingOrder
            - Mathf.RoundToInt((anchor.position.y + sortYOffset) * ordersPerWorldUnit);

        foreach (Renderer targetRenderer in targetRenderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.sortingOrder = order;
            }
        }
    }
}
