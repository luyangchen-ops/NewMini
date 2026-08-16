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

    [Header("Authored Actors And Markers")]
    [SerializeField] private PlayerCharacterController player;
    [SerializeField] private EnemyAgent openingBandit;
    [SerializeField] private Transform heroIntroStart;
    [SerializeField] private Transform heroIntroEnd;

    [Header("Existing Scene Systems")]
    [SerializeField] private GameObject gameplayHudRoot;
    [SerializeField] private ClickDialogueSystem openingDialogue;
    [SerializeField] private GameStateUIController gameStateUi;

    [Header("Opening Timeline")]
    [SerializeField] private PlayableDirector openingDirector;
    [SerializeField, Min(0f)] private float menuFadeDuration = .4f;
    [SerializeField, Min(.1f)] private float fallbackWalkDuration = 3f;
    [SerializeField, Min(0f)] private float helpDelayAfterKill = 1f;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int VerticalDirection = Animator.StringToHash("VerticalDirection");
    private PrologueState state = PrologueState.MainMenu;
    private Animator heroVisualAnimator;
    private Rigidbody2D playerBody;
    private bool startRequested;

    private void Awake()
    {
        Time.timeScale = 1f;
        ResolveReferences();

        if (openingDialogue != null) openingDialogue.DialogueFinished += HandleDialogueFinished;
        if (openingBandit != null) openingBandit.Died += HandleOpeningBanditDied;

        if (gameplayHudRoot != null) gameplayHudRoot.SetActive(false);
        if (mainMenuRoot != null) mainMenuRoot.SetActive(true);
        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.alpha = 1f;
            mainMenuCanvasGroup.interactable = true;
            mainMenuCanvasGroup.blocksRaycasts = true;
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
        if (state == PrologueState.MainMenu && !startRequested
            && (Keyboard.current?.enterKey.wasPressedThisFrame == true
                || Keyboard.current?.spaceKey.wasPressedThisFrame == true))
        {
            StartGame();
        }
    }

    /// <summary>Persistent scene event target for Btn_StartGame.</summary>
    public void StartGame()
    {
        if (startRequested || state != PrologueState.MainMenu) return;
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
        ActivateOpeningActors();
        SetHeroWalking(true);

        if (openingDirector != null && openingDirector.playableAsset != null)
        {
            openingDirector.time = 0d;
            openingDirector.Play();
            double duration = openingDirector.duration;
            while (openingDirector.state == PlayState.Playing
                && (duration <= 0d || openingDirector.time < duration - .001d))
            {
                yield return null;
            }
            openingDirector.Stop();
        }
        else
        {
            yield return FallbackWalk();
        }

        FinishHeroEntrance();
        yield return new WaitForSeconds(.2f);

        state = PrologueState.Dialogue;
        if (openingDialogue == null || !openingDialogue.StartDialogue())
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
        Vector3 start = heroIntroStart != null ? heroIntroStart.position : player.transform.position;
        Vector3 end = heroIntroEnd != null ? heroIntroEnd.position : start + Vector3.right * 5.4f;
        float elapsed = 0f;
        while (elapsed < fallbackWalkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallbackWalkDuration);
            player.transform.position = Vector3.LerpUnclamped(start, end, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
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
            if (heroIntroStart != null) player.transform.position = heroIntroStart.position;
            player.enabled = false;
            player.gameObject.SetActive(true);
            playerBody ??= player.GetComponent<Rigidbody2D>();
            if (playerBody != null) playerBody.simulated = false;
        }

        if (openingBandit != null)
        {
            openingBandit.enabled = false;
            openingBandit.gameObject.SetActive(true);
        }
    }

    private void FinishHeroEntrance()
    {
        if (player == null) return;
        SetHeroWalking(false);
        if (heroIntroEnd != null) player.transform.position = heroIntroEnd.position;
        if (playerBody != null)
        {
            playerBody.position = player.transform.position;
            playerBody.linearVelocity = Vector2.zero;
        }
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
        if (openingBandit != null) openingBandit.enabled = true;
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
        if (openingDialogue != null) openingDialogue.DialogueFinished -= HandleDialogueFinished;
        if (openingBandit != null) openingBandit.Died -= HandleOpeningBanditDied;
    }
}
