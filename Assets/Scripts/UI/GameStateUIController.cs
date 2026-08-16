using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Controls the scene-authored pause, help, and death screens.</summary>
public sealed class GameStateUIController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private PlayerCharacterController player;
    [SerializeField] private GameObject gameplayHudRoot;
    [SerializeField] private GameObject panelPause;
    [SerializeField] private GameObject panelHelp;
    [SerializeField] private GameObject panelDeath;
    [SerializeField] private Button pauseContinueButton;
    [SerializeField] private GameObject[] helpPages;
    [SerializeField] private Button helpPreviousButton;
    [SerializeField] private Button helpNextButton;
    [SerializeField] private Text helpPageIndicator;
    [SerializeField] private Button deathContinueButton;
    [Header("Navigation")]
    [SerializeField] private string mainMenuSceneName = "Begin";
    [SerializeField] private SceneFlowController sceneFlow;

    private Vector3 levelStartPosition;
    private float previousTimeScale = 1f;
    private bool isPauseShown;
    private bool isHelpShown;
    private bool isDeathShown;
    private bool hudWasActive;
    private int helpPageIndex;

    private void Awake()
    {
        player ??= FindAnyObjectByType<PlayerCharacterController>();
        sceneFlow ??= FindAnyObjectByType<SceneFlowController>();
        if (player != null) levelStartPosition = player.transform.position;
        if (gameplayHudRoot == null) gameplayHudRoot = GameObject.Find("Root_角色战斗HUD");
        panelPause?.SetActive(false); panelHelp?.SetActive(false); panelDeath?.SetActive(false);
        RefreshHelpPages();
    }

    private void OnEnable() { if (player == null) player = FindAnyObjectByType<PlayerCharacterController>(); if (player != null) player.Died += ShowDeath; }
    private void OnDisable() { if (player != null) player.Died -= ShowDeath; if (isPauseShown || isHelpShown || isDeathShown) RestoreTimeAndHud(); }

    private void Update()
    {
        if (isDeathShown || Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
        if (isHelpShown) CloseHelp();
        else if (isPauseShown) ClosePause();
        else if (!DialogueIsPlaying()) ShowPause();
    }

    public void ShowPause()
    {
        if (isPauseShown || isHelpShown || isDeathShown) return;
        isPauseShown = true; EnterFrozenUiState(); panelPause?.SetActive(true); Select(pauseContinueButton);
    }

    public void ClosePause()
    {
        if (!isPauseShown) return;
        panelPause?.SetActive(false); isPauseShown = false; RestoreTimeAndHud();
    }

    public void ShowHelp()
    {
        if (!isPauseShown) return;
        isHelpShown = true; helpPageIndex = 0; panelPause?.SetActive(false); panelHelp?.SetActive(true); RefreshHelpPages(); Select(helpNextButton);
    }

    public void CloseHelp()
    {
        if (!isHelpShown) return;
        isHelpShown = false; panelHelp?.SetActive(false); panelPause?.SetActive(true); Select(pauseContinueButton);
    }

    public void PreviousHelpPage() { if (!isHelpShown || helpPages == null || helpPages.Length == 0) return; helpPageIndex = Mathf.Max(0, helpPageIndex - 1); RefreshHelpPages(); }
    public void NextHelpPage() { if (!isHelpShown || helpPages == null || helpPages.Length == 0) return; helpPageIndex = Mathf.Min(helpPages.Length - 1, helpPageIndex + 1); RefreshHelpPages(); }

    public void ContinueFromCheckpoint()
    {
        Vector3 position = RespawnPoint.TryGetActivePosition(out Vector3 checkpoint) ? checkpoint : levelStartPosition;
        panelPause?.SetActive(false); panelHelp?.SetActive(false); panelDeath?.SetActive(false);
        isPauseShown = false; isHelpShown = false; isDeathShown = false; RestoreTimeAndHud(); player?.RespawnAt(position); EventSystem.current?.SetSelectedGameObject(null);
    }

    public void ReturnToMainMenu()
    {
        isPauseShown = false; isHelpShown = false; isDeathShown = false; Time.timeScale = 1f;
        if (sceneFlow != null) sceneFlow.ReturnToMainMenu();
        else { GameAudioSettings.Save(); SceneManager.LoadScene(mainMenuSceneName); }
    }

    public void RestartCurrentLevel()
    {
        isPauseShown = false; isHelpShown = false; isDeathShown = false; Time.timeScale = 1f;
        if (sceneFlow != null) sceneFlow.RestartCurrentLevel();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ShowDeath()
    {
        if (isDeathShown) return;
        isPauseShown = false; isHelpShown = false; isDeathShown = true; panelPause?.SetActive(false); panelHelp?.SetActive(false); EnterFrozenUiState(); panelDeath?.SetActive(true); Select(deathContinueButton);
    }

    private void RefreshHelpPages()
    {
        int count = helpPages?.Length ?? 0;
        if (count == 0) { if (helpPageIndicator != null) helpPageIndicator.text = "0 / 0"; return; }
        helpPageIndex = Mathf.Clamp(helpPageIndex, 0, count - 1);
        for (int i = 0; i < count; i++) if (helpPages[i] != null) helpPages[i].SetActive(i == helpPageIndex);
        if (helpPageIndicator != null) helpPageIndicator.text = $"{helpPageIndex + 1} / {count}";
        if (helpPreviousButton != null) helpPreviousButton.interactable = helpPageIndex > 0;
        if (helpNextButton != null) helpNextButton.interactable = helpPageIndex < count - 1;
    }

    private void EnterFrozenUiState() { previousTimeScale = Time.timeScale; Time.timeScale = 0f; if (gameplayHudRoot == null) return; hudWasActive = gameplayHudRoot.activeSelf; gameplayHudRoot.SetActive(false); }
    private void RestoreTimeAndHud() { Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale; if (gameplayHudRoot != null) gameplayHudRoot.SetActive(hudWasActive); }
    private static bool DialogueIsPlaying() { ClickDialogueSystem dialogue = FindAnyObjectByType<ClickDialogueSystem>(); return dialogue != null && dialogue.IsDialoguePlaying; }
    private static void Select(Selectable selectable) { if (EventSystem.current != null && selectable != null) EventSystem.current.SetSelectedGameObject(selectable.gameObject); }
}
