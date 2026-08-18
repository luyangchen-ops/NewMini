using System.Collections;
using UnityEngine;

public sealed class PerfectDodgeAfterimage : MonoBehaviour
{
    [SerializeField] private Animator effectAnimator;
    [SerializeField] private SpriteRenderer effectRenderer;
    [SerializeField] private GameObject characterVisual;
    [SerializeField, Min(0.01f)] private float effectDuration = 0.5f;

    private Coroutine hideRoutine;

    private void Awake()
    {
        effectAnimator ??= GetComponent<Animator>();
        effectRenderer ??= GetComponent<SpriteRenderer>();
    }

    public void Play(bool flipX)
    {
        gameObject.SetActive(true);

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

        if (gameObject.activeSelf) gameObject.SetActive(false);
        else RestoreCharacter();
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(effectDuration);
        hideRoutine = null;
        gameObject.SetActive(false);
    }

    private void RestoreCharacter()
    {
        if (characterVisual == null)
        {
            return;
        }

        characterVisual.SetActive(true);
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
