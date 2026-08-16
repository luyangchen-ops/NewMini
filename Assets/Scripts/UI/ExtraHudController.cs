using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates the authored Extra-scene HUD. This component never creates UI objects at runtime.</summary>
public sealed class ExtraHudController : MonoBehaviour
{
    [SerializeField] private PlayerCharacterController player;
    [SerializeField] private PlayerSpecialItemInventory items;
    [SerializeField] private Image healthFill;
    [SerializeField, Min(1f)] private float healthFillMaximumWidth = 350f;
    [SerializeField] private Text healthText;
    [SerializeField] private Image dodgeCooldownFill;
    [SerializeField] private Text dodgeText;
    [SerializeField] private Text knifeText;
    [SerializeField] private Image momentumFill;
    [SerializeField] private Text momentumText;
    [SerializeField] private Color momentumNormal = new Color(.33f, .68f, .85f, 1f);
    [SerializeField] private Color momentumFull = new Color(1f, .67f, .06f, 1f);

    private void Awake()
    {
        player ??= FindAnyObjectByType<PlayerCharacterController>();
        items ??= player != null ? player.GetComponent<PlayerSpecialItemInventory>() : null;
        healthFill ??= FindGraphic<Image>("Img_角色血量填充");
        healthText ??= FindGraphic<Text>("Txt_角色血量");
        dodgeCooldownFill ??= FindGraphic<Image>("Img_翻滚冷却扇形");
        dodgeText ??= FindGraphic<Text>("Txt_翻滚冷却");
        knifeText ??= FindGraphic<Text>("Txt_飞刀栏");
        momentumFill ??= FindGraphic<Image>("Img_势条填充");
        momentumText ??= FindGraphic<Text>("Txt_势条");
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.HealthChanged += RefreshHealth;
        }
    }

    private void Start()
    {
        RefreshHealth(player != null ? player.CurrentHealth : 0f, player != null ? player.MaximumHealth : 0f);
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.HealthChanged -= RefreshHealth;
        }
    }

    private T FindGraphic<T>(string objectName) where T : Component
    {
        foreach (T component in GetComponentsInChildren<T>(true))
            if (component.name == objectName) return component;
        return null;
    }

    private void Update()
    {
        if (player == null) return;
        items ??= player.GetComponent<PlayerSpecialItemInventory>();

        float cooldown = player.DodgeCooldownNormalized;
        dodgeCooldownFill.fillAmount = cooldown;
        dodgeText.text = cooldown > 0f ? $"翻滚冷却  {cooldown * 100f:0}%" : "翻滚  就绪";

        knifeText.text = $"飞刀  Q   x{(items != null ? items.ThrowingKnifeCount : 0)}";

        float momentum = player.MaximumMomentum <= 0 ? 0f : player.CurrentMomentum / (float)player.MaximumMomentum;
        momentumFill.fillAmount = momentum;
        momentumFill.color = Color.Lerp(momentumNormal, momentumFull, momentum);
        momentumText.text = player.IsMomentumFull
            ? "势条  已满"
            : $"势条  {player.CurrentMomentum} / {player.MaximumMomentum}";
    }

    private void RefreshHealth(float currentHealth, float maximumHealth)
    {
        if (healthFill == null || healthText == null)
        {
            return;
        }

        float maximum = Mathf.Max(1f, maximumHealth);
        float current = Mathf.Clamp(currentHealth, 0f, maximum);
        float normalizedHealth = current / maximum;
        healthFill.fillAmount = normalizedHealth;
        healthFill.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            healthFillMaximumWidth * normalizedHealth);
        healthText.text = $"角色血量  {Mathf.RoundToInt(current)} / {Mathf.RoundToInt(maximum)}";
    }
}
