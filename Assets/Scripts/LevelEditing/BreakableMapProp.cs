using System.Collections;
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
    [Tooltip("Seconds the broken remains stay visible before their renderers are hidden.")]
    [SerializeField, Min(0f)] private float remainsVisibleDuration = 2f;

    private Animator propAnimator;
    private Collider2D[] propColliders;
    private SpriteRenderer[] propRenderers;
    private bool[] intactRendererStates;
    private Coroutine hideRemainsRoutine;
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

        hideRemainsRoutine = StartCoroutine(HideRemainsAfterDelay());
    }

    public void ResetProp()
    {
        CacheComponents();
        if (hideRemainsRoutine != null)
        {
            StopCoroutine(hideRemainsRoutine);
            hideRemainsRoutine = null;
        }

        isBroken = false;
        RestoreRendererStates();
        propAnimator.ResetTrigger(BreakTrigger);
        propAnimator.Play(IntactState, 0, 0f);
        propAnimator.Update(0f);
        SetCollidersEnabled(true);
    }

    /// <summary>Restores every authored breakable when the player reloads a checkpoint.</summary>
    public static void ResetAllForCheckpointRetry()
    {
        foreach (BreakableMapProp prop in FindObjectsByType<BreakableMapProp>(FindObjectsInactive.Include))
            if (prop != null) prop.ResetProp();
    }

    private IEnumerator HideRemainsAfterDelay()
    {
        if (remainsVisibleDuration > 0f) yield return new WaitForSeconds(remainsVisibleDuration);
        if (isBroken) SetRenderersEnabled(false);
        hideRemainsRoutine = null;
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

        if (propRenderers == null || propRenderers.Length == 0)
        {
            propRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            intactRendererStates = new bool[propRenderers.Length];
            for (int i = 0; i < propRenderers.Length; i++)
                intactRendererStates[i] = propRenderers[i] != null && propRenderers[i].enabled;
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

    private void SetRenderersEnabled(bool value)
    {
        foreach (SpriteRenderer propRenderer in propRenderers)
            if (propRenderer != null) propRenderer.enabled = value;
    }

    private void RestoreRendererStates()
    {
        for (int i = 0; i < propRenderers.Length; i++)
            if (propRenderers[i] != null) propRenderers[i].enabled = intactRendererStates[i];
    }
}
