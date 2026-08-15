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

    public bool IsLocked { get; private set; }

    private void Awake()
    {
        if (blockingColliders == null || blockingColliders.Length == 0)
            blockingColliders = GetComponents<Collider2D>();
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
    }
}
