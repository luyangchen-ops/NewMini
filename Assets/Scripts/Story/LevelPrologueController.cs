using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

/// <summary>
/// Runs the scene-authored seamless menu and first combat tutorial in Level_LD.
/// The UI, actors, markers, director and event bindings remain visible in the scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelPrologueController : MonoBehaviour
{
    private enum PrologueState { MainMenu, Entering, Dialogue, Combat, HelpShown }

    [Header("Authored Menu")]
    [SerializeField] private GameObject mainMenuRoot;
    [SerializeField] private CanvasGroup mainMenuCanvasGroup;
    [SerializeField] private UnityEngine.UI.Button startButton;
    [SerializeField] private OpeningVideoController openingVideo;

    [Header("Authored Actors And Markers")]
    [SerializeField] private PlayerCharacterController player;
    [SerializeField] private EnemyAgent openingBandit;
    [SerializeField] private Transform heroIntroStart;
    [SerializeField] private Transform heroIntroEnd;
    [SerializeField] private Transform openingBanditEnd;

    [Header("Existing Scene Systems")]
    [SerializeField] private GameObject gameplayHudRoot;
    [SerializeField] private ClickDialogueSystem openingDialogue;
    [SerializeField] private GameStateUIController gameStateUi;

    [Header("Opening Entrance")]
    [SerializeField] private PlayableDirector openingDirector;
    [SerializeField, Min(0f)] private float menuFadeDuration = .4f;
    [SerializeField, Min(.1f)] private float fallbackWalkDuration = 3f;
    [SerializeField, Min(.1f)] private float banditWalkDuration = 1.35f;
    [SerializeField, Min(0f)] private float offscreenViewportPadding = .12f;
    [SerializeField, Min(0f)] private float helpDelayAfterKill = 1f;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int VerticalDirection = Animator.StringToHash("VerticalDirection");
    private PrologueState state = PrologueState.MainMenu;
    private Animator heroVisualAnimator;
    private Rigidbody2D playerBody;
    private bool startRequested;
    private bool waitingForOpeningVideo;
    private bool banditEntranceStarted;

    private void Awake()
    {
        Time.timeScale = 1f;
        ResolveReferences();

        if (openingDialogue != null)
        {
            openingDialogue.DialogueFinished += HandleDialogueFinished;
            openingDialogue.DialogueLineShouldWait += ShouldPauseBeforeDialogueLine;
        }
        if (openingBandit != null) openingBandit.Died += HandleOpeningBanditDied;
        if (openingVideo != null) openingVideo.Finished += ShowMenuAfterOpeningVideo;

        waitingForOpeningVideo = openingVideo != null && openingVideo.WillPlayOnThisLaunch;

        if (gameplayHudRoot != null) gameplayHudRoot.SetActive(false);
        if (mainMenuRoot != null) mainMenuRoot.SetActive(true);
        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.alpha = waitingForOpeningVideo ? 0f : 1f;
            mainMenuCanvasGroup.interactable = !waitingForOpeningVideo;
            mainMenuCanvasGroup.blocksRaycasts = !waitingForOpeningVideo;
        }

        PrepareActorsForMenu();
    }

    private IEnumerator Start()
    {
        yield return null;
        if (EventSystem.current != null && startButton != null)
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    private void Update()
    {
        if (!waitingForOpeningVideo && state == PrologueState.MainMenu && !startRequested
            && (Keyboard.current?.enterKey.wasPressedThisFrame == true
                || Keyboard.current?.spaceKey.wasPressedThisFrame == true))
        {
            StartGame();
        }
    }

    /// <summary>Persistent scene event target for Btn_StartGame.</summary>
    public void StartGame()
    {
        if (waitingForOpeningVideo || startRequested || state != PrologueState.MainMenu) return;
        startRequested = true;
        StartCoroutine(RunOpening());
    }

    /// <summary>Persistent scene event target for Btn_QuitGame.</summary>
    public void QuitGame()
    {
        GameAudioSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator RunOpening()
    {
        state = PrologueState.Entering;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        yield return FadeOutMenu();

        if (mainMenuRoot != null) mainMenuRoot.SetActive(false);
        bool cinematicStarted = openingDialogue != null && openingDialogue.BeginCinematic();
        ActivateOpeningActors();
        yield return PlayHeroEntrance();
        FinishHeroEntrance();

        state = PrologueState.Dialogue;
        if (!cinematicStarted || !openingDialogue.ShowFirstDialogueLine())
            BeginOpeningCombat();
    }

    private IEnumerator FadeOutMenu()
    {
        if (mainMenuCanvasGroup == null || menuFadeDuration <= 0f) yield break;
        mainMenuCanvasGroup.interactable = false;
        mainMenuCanvasGroup.blocksRaycasts = false;
        float elapsed = 0f;
        while (elapsed < menuFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / menuFadeDuration);
            mainMenuCanvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }
        mainMenuCanvasGroup.alpha = 0f;
    }

    private IEnumerator FallbackWalk()
    {
        if (player == null) yield break;
        yield return MoveActor(player.transform, playerBody, player.transform.position,
            HeroDialoguePosition, fallbackWalkDuration, true);
    }

    private IEnumerator PlayHeroEntrance()
    {
        if (player == null) yield break;

        // The opening Timeline is bound to the visual child Animator.  Its transform
        // curves therefore move that child in local space, rather than moving the
        // player root in world space.  Keep the root as the sole position owner and
        // reserve the child Animator for the walking sprite animation.
        SetHeroWalking(true);
        yield return MoveActor(player.transform, playerBody, player.transform.position,
            HeroDialoguePosition, fallbackWalkDuration, true);
    }

    private void ShowMenuAfterOpeningVideo()
    {
        waitingForOpeningVideo = false;
        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.alpha = 1f;
            mainMenuCanvasGroup.interactable = true;
            mainMenuCanvasGroup.blocksRaycasts = true;
        }
        if (EventSystem.current != null && startButton != null)
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    private void OnDestroy()
    {
        if (openingVideo != null) openingVideo.Finished -= ShowMenuAfterOpeningVideo;
    }

    private IEnumerator BanditWalkIn()
    {
        if (openingBandit == null) yield break;
        Vector3 end = BanditDialoguePosition;
        Vector3 direction = end - openingBandit.transform.position;
        openingBandit.SetDesiredVelocity(direction.normalized * (direction.magnitude / banditWalkDuration));
        yield return MoveActor(openingBandit.transform, openingBandit.Body, openingBandit.transform.position,
            end, banditWalkDuration, false);
        openingBandit.SetDesiredVelocity(Vector2.zero);
    }

    private bool ShouldPauseBeforeDialogueLine(int lineIndex, string speaker, string cue)
    {
        if (state != PrologueState.Dialogue || banditEntranceStarted || speaker != "Soldier") return false;
        banditEntranceStarted = true;
        StartCoroutine(BringInBanditThenRevealDialogue());
        return true;
    }

    private IEnumerator BringInBanditThenRevealDialogue()
    {
        yield return BanditWalkIn();
        FinishBanditEntrance();
        yield return new WaitForSeconds(.12f);
        openingDialogue?.ShowDeferredDialogueLine();
    }

    private IEnumerator MoveActor(Transform actor, Rigidbody2D body, Vector3 start, Vector3 end,
        float duration, bool isHero)
    {
        if (actor == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector3 position = Vector3.LerpUnclamped(start, end, t);
            actor.position = position;
            if (body != null) body.position = position;
            yield return null;
        }
        actor.position = end;
        if (body != null) body.position = end;
        if (isHero) SetHeroWalking(false);
    }

    private void PrepareActorsForMenu()
    {
        if (openingBandit != null)
        {
            openingBandit.enabled = false;
            openingBandit.gameObject.SetActive(false);
        }

        if (player == null) return;
        player.enabled = false;
        player.gameObject.SetActive(false);
    }

    private void ActivateOpeningActors()
    {
        if (player != null)
        {
            // These authored markers define the exact visible entrance path.  The
            // viewport fallback keeps the sequence functional if a marker is absent.
            player.transform.position = heroIntroStart != null
                ? heroIntroStart.position
                : GetOffscreenPosition(HeroDialoguePosition, fromLeft: true);
            player.enabled = false;
            player.gameObject.SetActive(true);
            playerBody ??= player.GetComponent<Rigidbody2D>();
            if (playerBody != null) playerBody.simulated = false;
        }

        if (openingBandit != null)
        {
            openingBandit.transform.position = GetOffscreenPosition(BanditDialoguePosition, fromLeft: false);
            openingBandit.enabled = false;
            openingBandit.gameObject.SetActive(true);
            if (openingBandit.Body != null) openingBandit.Body.simulated = false;
        }
    }

    private void FinishHeroEntrance()
    {
        if (player == null) return;
        SetHeroWalking(false);
        player.transform.position = HeroDialoguePosition;
        if (playerBody != null)
        {
            playerBody.position = player.transform.position;
            playerBody.linearVelocity = Vector2.zero;
        }
    }

    private void FinishBanditEntrance()
    {
        if (openingBandit == null) return;
        openingBandit.transform.position = BanditDialoguePosition;
        if (openingBandit.Body != null)
        {
            openingBandit.Body.position = openingBandit.transform.position;
            openingBandit.Body.linearVelocity = Vector2.zero;
        }
    }

    private Vector3 HeroDialoguePosition => heroIntroEnd != null ? heroIntroEnd.position : player.transform.position;
    private Vector3 BanditDialoguePosition => openingBanditEnd != null ? openingBanditEnd.position : openingBandit.transform.position;

    private Vector3 GetOffscreenPosition(Vector3 target, bool fromLeft)
    {
        Camera camera = Camera.main;
        if (camera == null) return target + (fromLeft ? Vector3.left : Vector3.right) * 12f;
        Vector3 viewportTarget = camera.WorldToViewportPoint(target);
        float viewportX = fromLeft ? -offscreenViewportPadding : 1f + offscreenViewportPadding;
        Vector3 outside = camera.ViewportToWorldPoint(new Vector3(viewportX, viewportTarget.y, viewportTarget.z));
        outside.z = target.z;
        return outside;
    }

    private void SetHeroWalking(bool walking)
    {
        if (heroVisualAnimator == null) return;
        heroVisualAnimator.SetFloat(Speed, walking ? 1f : 0f);
        heroVisualAnimator.SetInteger(VerticalDirection, 0);
        SpriteRenderer renderer = heroVisualAnimator.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.flipX = false;
    }

    private void HandleDialogueFinished()
    {
        if (state == PrologueState.Dialogue) BeginOpeningCombat();
    }

    private void BeginOpeningCombat()
    {
        state = PrologueState.Combat;
        if (playerBody != null) playerBody.simulated = true;
        if (player != null) player.enabled = true;
        if (openingBandit != null)
        {
            if (openingBandit.Body != null) openingBandit.Body.simulated = true;
            openingBandit.enabled = true;
        }
        if (gameplayHudRoot != null) gameplayHudRoot.SetActive(true);
    }

    private void HandleOpeningBanditDied(EnemyAgent enemy)
    {
        if (state != PrologueState.Combat) return;
        StartCoroutine(ShowHelpAfterOpeningKill());
    }

    private IEnumerator ShowHelpAfterOpeningKill()
    {
        state = PrologueState.HelpShown;
        yield return new WaitForSeconds(helpDelayAfterKill);
        gameStateUi?.ShowHelpFromGameplay();
    }

    private void ResolveReferences()
    {
        player ??= FindAnyObjectByType<PlayerCharacterController>(FindObjectsInactive.Include);
        openingDialogue ??= FindAnyObjectByType<ClickDialogueSystem>(FindObjectsInactive.Include);
        gameStateUi ??= FindAnyObjectByType<GameStateUIController>(FindObjectsInactive.Include);
        openingBanditEnd ??= GameObject.Find("Marker_OpeningBandit")?.transform;
        playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
        if (player != null)
        {
            foreach (Animator animator in player.GetComponentsInChildren<Animator>(true))
            {
                if (animator.gameObject != player.gameObject && animator.runtimeAnimatorController != null)
                {
                    heroVisualAnimator = animator;
                    break;
                }
            }
        }
    }

    private void OnDisable()
    {
        if (openingDialogue != null)
        {
            openingDialogue.DialogueFinished -= HandleDialogueFinished;
            openingDialogue.DialogueLineShouldWait -= ShouldPauseBeforeDialogueLine;
        }
        if (openingBandit != null) openingBandit.Died -= HandleOpeningBanditDied;
    }
}
