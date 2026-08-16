using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Controls the scene-authored pause and death screens.</summary>
public sealed class GameStateUIController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private PlayerCharacterController player;
    [SerializeField] private GameObject gameplayHudRoot;
    [SerializeField] private GameObject panelPause;
    [SerializeField] private GameObject panelDeath;
    [SerializeField] private Button pauseContinueButton;
    [SerializeField] private Button deathContinueButton;

    [Header("Navigation")]
    [SerializeField] private string mainMenuSceneName = "Begin";

    private Vector3 levelStartPosition;
    private float previousTimeScale = 1f;
    private bool isPauseShown;
    private bool isDeathShown;
    private bool hudWasActive;

    private void Awake()
    {
        player ??= FindAnyObjectByType<PlayerCharacterController>();
        if (player != null) levelStartPosition = player.transform.position;
        if (gameplayHudRoot == null) gameplayHudRoot = GameObject.Find("Root_角色战斗HUD");
        panelPause?.SetActive(false);
        panelDeath?.SetActive(false);
    }

    private void OnEnable()
    {
        if (player == null) player = FindAnyObjectByType<PlayerCharacterController>();
        if (player != null) player.Died += ShowDeath;
    }

    private void OnDisable()
    {
        if (player != null) player.Died -= ShowDeath;
        if (isPauseShown || isDeathShown) RestoreTimeAndHud();
    }

    private void Update()
    {
        if (isDeathShown || Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
        if (isPauseShown) ClosePause();
        else if (!DialogueIsPlaying()) ShowPause();
    }

    public void ShowPause()
    {
        if (isPauseShown || isDeathShown) return;
        isPauseShown = true;
        EnterFrozenUiState();
        panelPause?.SetActive(true);
        Select(pauseContinueButton);
    }

    public void ClosePause()
    {
        if (!isPauseShown) return;
        panelPause?.SetActive(false);
        isPauseShown = false;
        RestoreTimeAndHud();
    }

    public void ContinueFromCheckpoint()
    {
        Vector3 position = RespawnPoint.TryGetActivePosition(out Vector3 checkpoint)
            ? checkpoint
            : levelStartPosition;

        panelPause?.SetActive(false);
        panelDeath?.SetActive(false);
        isPauseShown = false;
        isDeathShown = false;
        RestoreTimeAndHud();
        player?.RespawnAt(position);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    public void ReturnToMainMenu()
    {
        isPauseShown = false;
        isDeathShown = false;
        Time.timeScale = 1f;
        GameAudioSettings.Save();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void ShowDeath()
    {
        if (isDeathShown) return;
        isPauseShown = false;
        isDeathShown = true;
        panelPause?.SetActive(false);
        EnterFrozenUiState();
        panelDeath?.SetActive(true);
        Select(deathContinueButton);
    }

    private void EnterFrozenUiState()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (gameplayHudRoot == null) return;
        hudWasActive = gameplayHudRoot.activeSelf;
        gameplayHudRoot.SetActive(false);
    }

    private void RestoreTimeAndHud()
    {
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        if (gameplayHudRoot != null) gameplayHudRoot.SetActive(hudWasActive);
    }

    private static bool DialogueIsPlaying()
    {
        ClickDialogueSystem dialogue = FindAnyObjectByType<ClickDialogueSystem>();
        return dialogue != null && dialogue.IsDialoguePlaying;
    }

    private static void Select(Selectable selectable)
    {
        if (EventSystem.current != null && selectable != null)
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }
}
