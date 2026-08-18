using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class BreakableMapProp : MonoBehaviour
{
    private static readonly int BreakTrigger = Animator.StringToHash("Break");
    private static readonly int IntactState = Animator.StringToHash("Intact");

    [Tooltip("Optional editor/debug shortcut. Gameplay attacks should break this prop through Break().")]
    [SerializeField] private bool clickToBreak;
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
        GameAudioManager.PlaySfx(GameSfx.BreakableDestroyed);
        SpecialItemDropSpawner.TryDropFromBreakable(GetBreakPosition());
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

    private Vector3 GetBreakPosition()
    {
        bool hasBounds = false;
        Bounds combinedBounds = default;
        foreach (Collider2D propCollider in propColliders)
        {
            if (propCollider == null || !propCollider.enabled) continue;
            if (!hasBounds)
            {
                combinedBounds = propCollider.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(propCollider.bounds);
            }
        }

        if (hasBounds) return combinedBounds.center;

        SpriteRenderer propRenderer = GetComponentInChildren<SpriteRenderer>();
        return propRenderer != null ? propRenderer.bounds.center : transform.position;
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
            propColliders = GetComponentsInChildren<Collider2D>(true);
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
