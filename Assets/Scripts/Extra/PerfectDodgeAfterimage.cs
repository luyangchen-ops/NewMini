using System.Collections;
using UnityEngine;

public sealed class PerfectDodgeAfterimage : MonoBehaviour
{
    [SerializeField] private Animator effectAnimator;
    [SerializeField] private SpriteRenderer effectRenderer;
    [SerializeField] private GameObject characterVisual;
    [SerializeField] private Animator characterAnimator;
    [SerializeField, Min(0.01f)] private float effectDuration = 0.5f;

    private Coroutine hideRoutine;
    private static readonly int IdleState = Animator.StringToHash("Idle");

    public void Play(bool flipX)
    {
        gameObject.SetActive(true);

        if (effectAnimator == null)
        {
            effectAnimator = GetComponent<Animator>();
        }

        if (effectRenderer == null)
        {
            effectRenderer = GetComponent<SpriteRenderer>();
        }

        if (effectRenderer != null)
        {
            effectRenderer.flipX = flipX;
        }

        if (characterVisual != null)
        {
            characterVisual.SetActive(false);
        }

        if (effectAnimator != null)
        {
            effectAnimator.Play(0, 0, 0f);
            effectAnimator.Update(0f);
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    public void StopAndRestore()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        RestoreCharacter();
        gameObject.SetActive(false);
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(effectDuration);
        hideRoutine = null;
        RestoreCharacter();
        gameObject.SetActive(false);
    }

    private void RestoreCharacter()
    {
        if (characterVisual == null)
        {
            return;
        }

        characterVisual.SetActive(true);
        if (characterAnimator != null && characterAnimator.isActiveAndEnabled)
        {
            characterAnimator.Play(IdleState, 0, 0f);
            characterAnimator.Update(0f);
        }
    }

    private void OnDisable()
    {
        hideRoutine = null;
        RestoreCharacter();
    }

    private void OnValidate()
    {
        effectDuration = Mathf.Max(0.01f, effectDuration);
    }
}
