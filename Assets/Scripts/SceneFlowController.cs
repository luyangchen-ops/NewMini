using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns scene changes that must reset the current level. Add this component to an
/// authored scene object and bind UI buttons to its public methods in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneFlowController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "Begin";
    [SerializeField] private string gameplaySceneName = "Level_01_BambooCourtyard";

    [Header("Authored Transition UI (Optional)")]
    [Tooltip("Assign the CanvasGroup on the authored full-screen Layer_Fade object.")]
    [SerializeField] private CanvasGroup fadeLayer;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;

    [Header("Input To Disable During Transition (Optional)")]
    [Tooltip("For example, assign PlayerCharacterController. These components are disabled before loading.")]
    [SerializeField] private Behaviour[] inputBehaviours;

    public bool IsTransitioning { get; private set; }

    private void Awake()
    {
        // A scene loaded from a paused screen must never inherit the paused clock.
        Time.timeScale = 1f;

        if (fadeLayer == null)
        {
            return;
        }

        fadeLayer.alpha = 0f;
        fadeLayer.interactable = false;
        fadeLayer.blocksRaycasts = false;
    }

    /// <summary>Starts a fresh game by loading the configured gameplay scene.</summary>
    public void StartGame()
    {
        LoadScene(gameplaySceneName);
    }

    /// <summary>
    /// Leaves gameplay and loads the menu scene. Loading another scene destroys all
    /// non-persistent level objects, so the abandoned level state is cleared.
    /// </summary>
    public void ReturnToMainMenu()
    {
        GameAudioSettings.Save();
        LoadScene(mainMenuSceneName);
    }

    /// <summary>Clears and restarts the active level from its authored initial state.</summary>
    public void RestartCurrentLevel()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        LoadScene(activeScene.name);
    }

    public void LoadScene(string sceneName)
    {
        if (IsTransitioning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError($"{nameof(SceneFlowController)} on '{name}' has no target scene configured.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' is not enabled in Build Settings.", this);
            return;
        }

        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        IsTransitioning = true;
        DisableTransitionInput();

        // Pause menus use timeScale = 0, therefore the transition uses unscaled time.
        Time.timeScale = 1f;

        if (fadeLayer != null)
        {
            fadeLayer.interactable = true;
            fadeLayer.blocksRaycasts = true;

            float startAlpha = fadeLayer.alpha;
            float elapsed = 0f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = fadeOutDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / fadeOutDuration);
                fadeLayer.alpha = Mathf.Lerp(startAlpha, 1f, SmoothStep(progress));
                yield return null;
            }

            fadeLayer.alpha = 1f;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
        {
            Debug.LogError($"Failed to begin loading scene '{sceneName}'.", this);
            RestoreAfterFailedLoad();
            yield break;
        }

        while (!operation.isDone)
        {
            yield return null;
        }
    }

    private void DisableTransitionInput()
    {
        if (inputBehaviours == null)
        {
            return;
        }

        foreach (Behaviour behaviour in inputBehaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }
    }

    private void RestoreAfterFailedLoad()
    {
        IsTransitioning = false;

        if (inputBehaviours != null)
        {
            foreach (Behaviour behaviour in inputBehaviours)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = true;
                }
            }
        }

        if (fadeLayer != null)
        {
            fadeLayer.alpha = 0f;
            fadeLayer.interactable = false;
            fadeLayer.blocksRaycasts = false;
        }
    }

    private static float SmoothStep(float value)
    {
        return value * value * (3f - 2f * value);
    }
}
