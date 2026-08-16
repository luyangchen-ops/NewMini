using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class BreakableMapProp : MonoBehaviour
{
    private static readonly int BreakTrigger = Animator.StringToHash("Break");
    private static readonly int IntactState = Animator.StringToHash("Intact");

    [SerializeField] private bool clickToBreak = true;
    [SerializeField] private bool disableCollidersOnBreak = true;

    private Animator propAnimator;
    private Collider2D[] propColliders;
    private bool isBroken;

    public bool IsBroken => isBroken;

    private void Awake()
    {
        CacheComponents();
    }

    public void Break()
    {
        if (isBroken)
        {
            return;
        }

        CacheComponents();
        isBroken = true;
        SpecialItemDropSpawner.TryDropFromBreakable(transform.position);
        propAnimator.ResetTrigger(BreakTrigger);
        propAnimator.SetTrigger(BreakTrigger);

        if (disableCollidersOnBreak)
        {
            SetCollidersEnabled(false);
        }
    }

    public void ResetProp()
    {
        CacheComponents();
        isBroken = false;
        propAnimator.ResetTrigger(BreakTrigger);
        propAnimator.Play(IntactState, 0, 0f);
        propAnimator.Update(0f);
        SetCollidersEnabled(true);
    }

    private void OnMouseDown()
    {
        if (Application.isPlaying && clickToBreak)
        {
            Break();
        }
    }

    private void CacheComponents()
    {
        if (propAnimator == null)
        {
            propAnimator = GetComponent<Animator>();
        }

        if (propColliders == null || propColliders.Length == 0)
        {
            propColliders = GetComponents<Collider2D>();
        }
    }

    private void SetCollidersEnabled(bool value)
    {
        foreach (var propCollider in propColliders)
        {
            if (propCollider != null)
            {
                propCollider.enabled = value;
            }
        }
    }
}
