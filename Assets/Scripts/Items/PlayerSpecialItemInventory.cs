using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PlayerSpecialItemInventory : MonoBehaviour
{
    [SerializeField, Min(0)] private int shieldCharges;
    [SerializeField, Min(0)] private int throwingKnifeCount;
    [SerializeField, Min(1)] private int maximumThrowingKnives = 3;
    [SerializeField, Min(0f)] private float shieldDuration = 60f;
    [SerializeField, Min(0f)] private float potionHealing = 20f;
    [SerializeField, Min(0f)] private float knifeSpeed = 14f;
    [SerializeField, Min(0f)] private float knifeLifetime = 2f;

    private PlayerCharacterController player;
    private ParticleSystem healingParticles;
    private Material healingParticleMaterial;
    private SpriteRenderer shieldBubble;
    private Texture2D shieldBubbleTexture;
    private Sprite shieldBubbleSprite;
    private float shieldExpiresAt;

    public int ShieldCharges => shieldCharges;
    public int ThrowingKnifeCount => throwingKnifeCount;
    public int MaximumThrowingKnives => maximumThrowingKnives;
    public float ShieldTimeRemaining => shieldCharges > 0
        ? Mathf.Max(0f, shieldExpiresAt - Time.time)
        : 0f;

    private void Awake()
    {
        player = GetComponent<PlayerCharacterController>();
        throwingKnifeCount = Mathf.Clamp(throwingKnifeCount, 0, maximumThrowingKnives);
        shieldCharges = shieldCharges > 0 ? 1 : 0;
        if (shieldCharges > 0) shieldExpiresAt = Time.time + shieldDuration;
        SetShieldVisual(shieldCharges > 0);
    }

    private void Update()
    {
        ExpireShieldIfNeeded();
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            TryThrowKnife();
    }

    public void Collect(SpecialItemType itemType)
    {
        switch (itemType)
        {
            case SpecialItemType.OneHitShield:
                shieldCharges = 1;
                shieldExpiresAt = Time.time + shieldDuration;
                SetShieldVisual(true);
                GameAudioManager.PlaySfx(GameSfx.ShieldActivated);
                break;
            case SpecialItemType.HealingPotion:
                player?.RestoreHealth(potionHealing);
                PlayHealingEffect();
                GameAudioManager.PlaySfx(GameSfx.HealingActivated);
                break;
            case SpecialItemType.ThrowingKnife:
                throwingKnifeCount = Mathf.Min(maximumThrowingKnives, throwingKnifeCount + 1);
                break;
        }
    }

    /// <summary>Called by the player damage entry point before health is reduced.</summary>
    public bool TryBlockAttack()
    {
        ExpireShieldIfNeeded();
        if (shieldCharges <= 0) return false;
        shieldCharges--;
        shieldExpiresAt = 0f;
        SetShieldVisual(shieldCharges > 0);
        return true;
    }

    private void ExpireShieldIfNeeded()
    {
        if (shieldCharges <= 0 || Time.time < shieldExpiresAt) return;
        shieldCharges = 0;
        shieldExpiresAt = 0f;
        SetShieldVisual(false);
    }

    private void PlayHealingEffect()
    {
        EnsureHealingParticles();
        healingParticles.Emit(20);
    }

    private void EnsureHealingParticles()
    {
        if (healingParticles != null) return;

        GameObject effectObject = new GameObject("Vfx_HealingLightPoints");
        effectObject.transform.SetParent(transform, false);
        healingParticles = effectObject.AddComponent<ParticleSystem>();
        healingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = healingParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.75f, 1.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .18f);
        main.startSize = new ParticleSystem.MinMaxCurve(.045f, .095f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(.18f, 1f, .28f, .95f),
            new Color(.62f, 1f, .3f, .72f));
        main.maxParticles = 32;

        var emission = healingParticles.emission;
        emission.enabled = false;

        var shape = healingParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = .48f;
        shape.radiusThickness = 1f;

        var velocity = healingParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.12f, .12f);
        velocity.y = new ParticleSystem.MinMaxCurve(.75f, 1.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystemRenderer particleRenderer = healingParticles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingOrder = 30;
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            healingParticleMaterial = new Material(spriteShader) { name = "Runtime_HealingLightPoints" };
            particleRenderer.material = healingParticleMaterial;
        }

    }

    private void SetShieldVisual(bool visible)
    {
        if (!visible && shieldBubble == null) return;
        EnsureShieldBubble();
        shieldBubble.enabled = visible;
    }

    private void EnsureShieldBubble()
    {
        if (shieldBubble != null) return;

        const int textureSize = 64;
        shieldBubbleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "Runtime_OneHitShieldBubble",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[textureSize * textureSize];
        Vector2 center = new Vector2((textureSize - 1) * .5f, (textureSize - 1) * .5f);
        float radius = textureSize * .48f;
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                if (distance > 1f) continue;

                float interior = .055f * (1f - distance * distance);
                float rim = Mathf.Clamp01(1f - Mathf.Abs(distance - .88f) / .12f) * .34f;
                pixels[y * textureSize + x] = new Color(.12f, .68f, 1f, interior + rim);
            }
        }

        shieldBubbleTexture.SetPixels(pixels);
        shieldBubbleTexture.Apply();
        shieldBubbleSprite = Sprite.Create(
            shieldBubbleTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(.5f, .5f),
            textureSize);
        shieldBubbleSprite.name = "Runtime_OneHitShieldBubble";

        GameObject shieldObject = new GameObject("Vfx_OneHitShieldBubble");
        shieldObject.transform.SetParent(transform, false);
        shieldObject.transform.localScale = Vector3.one * 1.9f;
        shieldBubble = shieldObject.AddComponent<SpriteRenderer>();
        shieldBubble.sprite = shieldBubbleSprite;
        shieldBubble.sortingOrder = 12;
    }

    private void OnDestroy()
    {
        if (healingParticleMaterial != null) Destroy(healingParticleMaterial);
        if (shieldBubbleSprite != null) Destroy(shieldBubbleSprite);
        if (shieldBubbleTexture != null) Destroy(shieldBubbleTexture);
    }

    private void TryThrowKnife()
    {
        if (throwingKnifeCount <= 0 || player == null) return;

        Camera camera = Camera.main;
        Vector2 direction = Vector2.right;
        if (camera != null && Pointer.current != null)
        {
            Vector3 pointer = Pointer.current.position.ReadValue();
            pointer.z = -camera.transform.position.z;
            Vector2 offset = (Vector2)camera.ScreenToWorldPoint(pointer) - (Vector2)transform.position;
            if (offset.sqrMagnitude > .0001f) direction = offset.normalized;
        }

        throwingKnifeCount--;
        GameAudioManager.PlaySfx(GameSfx.ThrowingKnifeLaunched);
        GameObject knifeObject = new GameObject("Projectile_ThrowingKnife");
        knifeObject.transform.position = transform.position + (Vector3)(direction * .35f);
        knifeObject.transform.right = direction;
        knifeObject.AddComponent<ThrowingKnifeProjectile>().Launch(direction, knifeSpeed, knifeLifetime, gameObject);
    }
}
