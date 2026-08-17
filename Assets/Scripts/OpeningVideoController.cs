using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays the authored opening movie once for each application build, then fades
/// its authored UI layer away and releases the main menu.
/// </summary>
[DisallowMultipleComponent]
public sealed class OpeningVideoController : MonoBehaviour
{
    private const string PlayedKeyPrefix = "opening-video.played.";
    private const string BuildStampResourcePath = "Build/OpeningVideoBuildStamp";

    [Header("Authored Video UI")]
    [SerializeField] private GameObject videoRoot;
    [SerializeField] private CanvasGroup videoCanvasGroup;
    [SerializeField] private RawImage videoImage;
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float fadeDuration = 1.25f;

    public bool WillPlayOnThisLaunch { get; private set; }
    public static bool ShouldPlayForCurrentBuild => !PlayerPrefs.HasKey(CurrentBuildPlayedKey);
    public event Action Finished;

    private bool finishing;
    private PresentationSession performanceSession;

    private static string CurrentBuildPlayedKey
    {
        get
        {
            TextAsset buildStamp = Resources.Load<TextAsset>(BuildStampResourcePath);
            string buildId = buildStamp != null && !string.IsNullOrWhiteSpace(buildStamp.text)
                ? buildStamp.text.Trim()
                : Application.buildGUID;
            return PlayedKeyPrefix + buildId;
        }
    }

    private void Awake()
    {
        WillPlayOnThisLaunch = ShouldPlayForCurrentBuild;
        if (!WillPlayOnThisLaunch)
        {
            if (videoRoot != null) videoRoot.SetActive(false);
            return;
        }

        if (videoRoot != null) videoRoot.SetActive(true);
        if (videoCanvasGroup != null)
        {
            videoCanvasGroup.alpha = 1f;
            videoCanvasGroup.interactable = true;
            videoCanvasGroup.blocksRaycasts = true;
        }
    }

    private void Start()
    {
        if (!WillPlayOnThisLaunch) return;

        if (videoPlayer == null || videoPlayer.clip == null)
        {
            Debug.LogWarning("Opening video is not assigned; continuing to the main menu.", this);
            FinishOpening();
            return;
        }

        videoPlayer.loopPointReached += HandleVideoFinished;
        videoPlayer.errorReceived += HandleVideoError;
        videoPlayer.prepareCompleted += HandleVideoPrepared;
        videoPlayer.Prepare();
    }

    private void OnDestroy()
    {
        performanceSession?.Dispose();
        performanceSession = null;
        if (videoPlayer == null) return;
        videoPlayer.loopPointReached -= HandleVideoFinished;
        videoPlayer.errorReceived -= HandleVideoError;
        videoPlayer.prepareCompleted -= HandleVideoPrepared;
    }

    private void OnDisable()
    {
        performanceSession?.Dispose();
        performanceSession = null;
    }

    private void HandleVideoPrepared(VideoPlayer source)
    {
        performanceSession ??= DialoguePerformanceManager.BeginPerformance(this, "Opening Video");
        source.Play();
        PlayerPrefs.SetInt(CurrentBuildPlayedKey, 1);
        PlayerPrefs.Save();
    }

    private void HandleVideoFinished(VideoPlayer source) => FinishOpening();

    private void HandleVideoError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"Opening video could not play: {message}", this);
        FinishOpening();
    }

    private void FinishOpening()
    {
        if (finishing) return;
        finishing = true;
        StartCoroutine(FadeOutAndFinish());
    }

    private IEnumerator FadeOutAndFinish()
    {
        if (videoCanvasGroup != null)
        {
            videoCanvasGroup.interactable = false;
            videoCanvasGroup.blocksRaycasts = false;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                videoCanvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }
            videoCanvasGroup.alpha = 0f;
        }

        if (videoPlayer != null) videoPlayer.Stop();
        if (videoRoot != null) videoRoot.SetActive(false);
        performanceSession?.Dispose();
        performanceSession = null;
        Finished?.Invoke();
    }
}
