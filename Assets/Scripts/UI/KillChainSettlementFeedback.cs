using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the authored kill-chain counter and the final sheathe settlement card.
/// All visual objects are scene-authored; this component never creates UI at runtime.
/// </summary>
public sealed class KillChainSettlementFeedback : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private PlayerCharacterController player;
    [SerializeField] private CanvasGroup activeChainGroup;
    [SerializeField] private RectTransform activeChainContent;
    [SerializeField] private Text activeCountText;
    [SerializeField] private Text activeTitleText;
    [SerializeField] private Text activeRewardHintText;
    [SerializeField] private Image activeAccent;
    [SerializeField] private RectTransform activeInkRevealMask;
    [SerializeField] private CanvasGroup activeTextGroup;
    [SerializeField] private CanvasGroup activeBloodInkGroup;
    [SerializeField] private RectTransform activeBloodInk;
    [SerializeField] private CanvasGroup settlementGroup;
    [SerializeField] private RectTransform settlementContent;
    [SerializeField] private Text settlementRankText;
    [SerializeField] private Text settlementCountText;
    [SerializeField] private Text settlementRewardText;
    [SerializeField] private Image settlementAccent;
    [SerializeField] private CanvasGroup settlementSlashGroup;
    [SerializeField] private RectTransform settlementSlash;

    [Header("Mechanic Presentation")]
    [SerializeField, Min(1)] private int bonusMomentumThreshold = 3;
    [SerializeField, Min(.05f)] private float activeInkRevealDuration = .28f;
    [SerializeField, Min(0f)] private float activeBloodInkDelay = .06f;
    [SerializeField, Min(.05f)] private float activeBloodInkFadeDuration = .16f;
    [SerializeField, Min(.1f)] private float settlementHoldDuration = 1.15f;
    [SerializeField, Min(.05f)] private float settlementFadeDuration = .35f;
    [SerializeField] private Color inkColor = new Color(.12f, .105f, .09f, 1f);
    [SerializeField] private Color cinnabarColor = new Color(.74f, .08f, .055f, 1f);
    [SerializeField] private Color rewardColor = new Color(1f, .72f, .28f, 1f);
    [SerializeField] private Color activeTextColor = new Color(.94f, .90f, .78f, 1f);

    private enum PresentationState { Hidden, Chaining, Settling }

    private PresentationState state;
    private int startingMomentum;
    private float startingHealth;
    private float stateElapsed;
    private float countPulse;
    private float activeInkFullWidth;
    private Vector2 activeBloodInkPosition;

    private void Awake()
    {
        if (activeInkRevealMask != null)
        {
            activeInkFullWidth = activeInkRevealMask.sizeDelta.x;
            SetRevealWidth(0f);
        }
        if (activeBloodInk != null)
            activeBloodInkPosition = activeBloodInk.anchoredPosition;
        SetGroup(activeChainGroup, 0f);
        SetGroup(activeTextGroup, 0f);
        SetGroup(activeBloodInkGroup, 0f);
        SetGroup(settlementGroup, 0f);
        SetGroup(settlementSlashGroup, 0f);
    }

    private void OnDisable()
    {
        state = PresentationState.Hidden;
        SetGroup(activeChainGroup, 0f);
        SetGroup(activeTextGroup, 0f);
        SetGroup(activeBloodInkGroup, 0f);
        SetGroup(settlementGroup, 0f);
        SetGroup(settlementSlashGroup, 0f);
    }

    private void Update()
    {
        float delta = Time.unscaledDeltaTime;
        countPulse = Mathf.Max(0f, countPulse - delta * 5.5f);

        if (state == PresentationState.Chaining)
        {
            stateElapsed += delta;
            SetGroup(activeChainGroup, 1f);
            float inkReveal = Mathf.Clamp01(stateElapsed / activeInkRevealDuration);
            SetRevealWidth(activeInkFullWidth * EaseOutCubic(inkReveal));
            float textReveal = Mathf.Clamp01((stateElapsed - .07f) / .13f);
            SetGroup(activeTextGroup, textReveal);
            float bloodReveal = Mathf.Clamp01((stateElapsed - activeBloodInkDelay) / activeBloodInkFadeDuration);
            SetGroup(activeBloodInkGroup, bloodReveal * .72f);
            if (activeBloodInk != null)
                activeBloodInk.anchoredPosition = activeBloodInkPosition + new Vector2(Mathf.Lerp(-7f, 0f, EaseOutCubic(bloodReveal)), 0f);
            if (activeCountText != null)
            {
                float punch = Mathf.Sin(countPulse * Mathf.PI) * .16f;
                activeCountText.rectTransform.localScale = Vector3.one * (1f + punch);
            }
            return;
        }

        if (state != PresentationState.Settling) return;
        stateElapsed += delta;
        float appear = Mathf.Clamp01(stateElapsed / .16f);
        float fadeStart = .16f + settlementHoldDuration;
        float disappear = stateElapsed <= fadeStart
            ? 1f
            : 1f - Mathf.Clamp01((stateElapsed - fadeStart) / settlementFadeDuration);
        SetGroup(settlementGroup, Mathf.Min(appear, disappear));
        float slashAppear = Mathf.Clamp01((stateElapsed - .03f) / .14f);
        float slashDisappear = 1f - Mathf.Clamp01((stateElapsed - .30f) / .24f);
        SetGroup(settlementSlashGroup, Mathf.Min(slashAppear, slashDisappear) * .28f);
        if (settlementContent != null)
        {
            float scale = Mathf.Lerp(.82f, 1f, EaseOutBack(appear));
            settlementContent.localScale = Vector3.one * scale;
            settlementContent.anchoredPosition = new Vector2(0f, Mathf.Lerp(-18f, 0f, appear));
        }
        if (settlementSlash != null)
        {
            float slashScale = Mathf.Lerp(.68f, 1.02f, EaseOutBack(slashAppear));
            settlementSlash.localScale = Vector3.one * slashScale;
            settlementSlash.anchoredPosition = new Vector2(Mathf.Lerp(-48f, 6f, slashAppear), -4f);
        }

        if (disappear > 0f) return;
        state = PresentationState.Hidden;
        SetGroup(settlementGroup, 0f);
        SetGroup(settlementSlashGroup, 0f);
    }

    /// <summary>Persistent UnityEvent endpoint: Player/onKillChainStarted.</summary>
    public void BeginKillChain()
    {
        if (player == null) player = FindAnyObjectByType<PlayerCharacterController>();
        startingHealth = player != null ? player.CurrentHealth : 0f;
        startingMomentum = player != null ? player.CurrentMomentum : 0;
        stateElapsed = 0f;
        countPulse = 0f;
        state = PresentationState.Chaining;
        SetGroup(settlementGroup, 0f);
        SetGroup(activeChainGroup, 1f);
        SetRevealWidth(0f);
        SetGroup(activeTextGroup, 0f);
        SetGroup(activeBloodInkGroup, 0f);
        RefreshActiveChain(0);
    }

    /// <summary>Persistent UnityEvent endpoint: Player/onKillChainKillConfirmed.</summary>
    public void ConfirmKill(int killCount)
    {
        if (state != PresentationState.Chaining) BeginKillChain();
        stateElapsed = 0f;
        countPulse = 1f;
        SetRevealWidth(0f);
        SetGroup(activeTextGroup, 0f);
        SetGroup(activeBloodInkGroup, 0f);
        RefreshActiveChain(Mathf.Max(0, killCount));
    }

    /// <summary>Persistent UnityEvent endpoint: Player/onKillChainEnded.</summary>
    public void EndKillChain(int killCount)
    {
        SetGroup(activeChainGroup, 0f);
        if (killCount <= 0)
        {
            state = PresentationState.Hidden;
            return;
        }

        if (player == null) player = FindAnyObjectByType<PlayerCharacterController>();
        int healthGained = player != null ? Mathf.Max(0, Mathf.RoundToInt(player.CurrentHealth - startingHealth)) : 0;
        int momentumGained = player != null ? Mathf.Max(0, player.CurrentMomentum - startingMomentum) : 0;

        if (settlementRankText != null) settlementRankText.text = RankFor(killCount);
        if (settlementCountText != null) settlementCountText.text = $"{ChineseNumber(killCount)} 斩";
        if (settlementRewardText != null)
        {
            settlementRewardText.text = $"气血 +{healthGained}    气势 +{momentumGained}";
            settlementRewardText.color = killCount >= bonusMomentumThreshold ? rewardColor : inkColor;
        }
        if (settlementAccent != null)
            settlementAccent.color = killCount >= bonusMomentumThreshold ? cinnabarColor : inkColor;

        stateElapsed = 0f;
        state = PresentationState.Settling;
        SetGroup(settlementGroup, 0f);
        SetGroup(settlementSlashGroup, 0f);
    }

    private void RefreshActiveChain(int killCount)
    {
        if (activeCountText != null)
        {
            activeCountText.text = killCount.ToString();
            activeCountText.color = activeTextColor;
        }
        if (activeTitleText != null)
        {
            activeTitleText.text = killCount > 0 ? "连 斩" : "伺 机";
            activeTitleText.color = activeTextColor;
        }
        bool bonusActive = killCount >= bonusMomentumThreshold;
        if (activeRewardHintText != null)
        {
            int remaining = Mathf.Max(0, bonusMomentumThreshold - killCount);
            activeRewardHintText.text = bonusActive ? "势起 · 气势加倍" : $"再斩 {remaining} 人 · 气势加倍";
            activeRewardHintText.color = bonusActive
                ? rewardColor
                : new Color(activeTextColor.r, activeTextColor.g, activeTextColor.b, .72f);
        }
        if (activeAccent != null)
            activeAccent.color = bonusActive ? Color.white : new Color(1f, 1f, 1f, .78f);
    }

    private static string RankFor(int count)
    {
        if (count >= 8) return "无人可挡";
        if (count >= 5) return "势如破竹";
        if (count >= 3) return "行云流水";
        if (count == 2) return "双锋并起";
        return "快意一刀";
    }

    private static string ChineseNumber(int value)
    {
        string[] digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
        if (value >= 0 && value <= 10) return digits[value];
        return value.ToString();
    }

    private static float EaseOutBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = value - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private void SetRevealWidth(float width)
    {
        if (activeInkRevealMask == null) return;
        Vector2 size = activeInkRevealMask.sizeDelta;
        size.x = Mathf.Max(0f, width);
        activeInkRevealMask.sizeDelta = size;
    }

    private static void SetGroup(CanvasGroup group, float alpha)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
