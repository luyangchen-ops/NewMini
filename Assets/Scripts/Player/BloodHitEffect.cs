using UnityEngine;

public sealed class BloodHitEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer effectRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 16f;
    [SerializeField, Min(.01f)] private float scalePerTargetUnit = .24f;
    [SerializeField, Min(.01f)] private float minimumScale = .16f;
    [SerializeField, Min(.01f)] private float maximumScale = .55f;

    private float elapsed;

    public void PlayAt(Vector2 position, Vector2 slashDirection, float targetSize, int sortingOrder)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        if (slashDirection.sqrMagnitude > .0001f)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(slashDirection.y, slashDirection.x) * Mathf.Rad2Deg);

        float scale = Mathf.Clamp(targetSize * scalePerTargetUnit, minimumScale, maximumScale);
        transform.localScale = Vector3.one * scale;
        if (effectRenderer != null) effectRenderer.sortingOrder = sortingOrder;
    }

    private void Awake()
    {
        effectRenderer ??= GetComponent<SpriteRenderer>();
        if (effectRenderer != null && frames != null && frames.Length > 0)
            effectRenderer.sprite = frames[0];
    }

    private void Update()
    {
        if (effectRenderer == null || frames == null || frames.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        elapsed += Time.unscaledDeltaTime;
        int frameIndex = Mathf.FloorToInt(elapsed * framesPerSecond);
        if (frameIndex >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }

        effectRenderer.sprite = frames[frameIndex];
    }
}
