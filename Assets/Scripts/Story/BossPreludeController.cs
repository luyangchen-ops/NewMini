using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>Final-arena dialogue interlude: Boss/guards appear before normal waves begin.</summary>
[DisallowMultipleComponent]
public sealed class BossPreludeController : MonoBehaviour
{
    private const string BossDialogueSpeakerName = "\u88D8\u4E5D";

    [SerializeField] private ArenaCombatZone arena;
    [SerializeField] private LevelBossEncounterController encounter;
    [SerializeField] private ClickDialogueSystem dialogue;
    [SerializeField] private TextAsset bossBeforeDialogue;
    [SerializeField] private PlayerCharacterController player;
    [SerializeField] private Transform incenseDestination;
    [SerializeField] private PlayableDirector bossEntranceDirector;
    [Tooltip("Legacy fallback. Prefer assigning Cinematic Camera for the Boss prelude.")]
    [SerializeField] private Camera presentationCamera;
    [SerializeField] private Camera cinematicCamera;
    [SerializeField, Min(.1f)] private float bossCameraPanDuration = 1.5f;
    [SerializeField, Min(.1f)] private float heroRunDuration = 1.1f;
    private bool started;
    private Camera mainCamera;
    private bool cinematicCameraObjectWasActive;
    private AudioListener mainAudioListener;
    private AudioListener cinematicAudioListener;
    private PresentationSession performanceSession;
    private bool musicStoppedForDialogue;
    private bool skipPreludeOnRetry;

    private void Awake()
    {
        dialogue ??= FindAnyObjectByType<ClickDialogueSystem>();
        player ??= FindAnyObjectByType<PlayerCharacterController>();
        if (dialogue != null)
        {
            dialogue.DialogueLineShouldWait += HandleDialogueBreakpoint;
            dialogue.DialogueInterruptionShouldPlay += HandleDialogueInterruption;
        }
        ResolvePresentationCameras();
        if (cinematicCamera != null && cinematicCamera != mainCamera)
        {
            cinematicCamera.enabled = false;
            if (cinematicAudioListener != null) cinematicAudioListener.enabled = false;
        }
        arena?.SetWavesDeferred(true);
        if (arena != null) arena.ZoneReset += HandleArenaReset;
    }

    public void BeginPrelude()
    {
        if (started) return;
        started = true;
        if (skipPreludeOnRetry)
        {
            skipPreludeOnRetry = false;
            RestartBossCombatWithoutPrelude();
            return;
        }

        performanceSession = DialoguePerformanceManager.BeginPerformance(this, "Boss Prelude");
        arena?.SetWavesDeferred(true);
        arena?.ResetDeferredWaves();
        player?.SetPresentationIdle(true);
        encounter?.SpawnBoss();
        encounter?.SetBossPresentationIdle(true);
        SwitchToCinematicCamera();
        if (dialogue != null && bossBeforeDialogue != null)
        {
            dialogue.DialogueStarted += HandleBossDialogueStarted;
            dialogue.DialogueFinished += FinishPrelude;
            Transform bossSpeaker = encounter != null ? encounter.ActiveBossTransform : null;
            if (!dialogue.StartDialogueWithOffscreenSpeakerAtLowerScreen(
                    bossBeforeDialogue,
                    player != null ? player.transform : null,
                    bossSpeaker,
                    BossDialogueSpeakerName,
                    bossSpeaker))
                FinishPrelude();
        }
        else FinishPrelude();
    }

    private bool HandleDialogueBreakpoint(int _, string __, string cue)
    {
        if (!started) return false;
        if (cue == "character_entrance") { StartCoroutine(RunHeroThenReveal()); return true; }
        if (cue == "archers_shooting") { StartCoroutine(ShootThenReveal()); return true; }
        return false;
    }

    private bool HandleDialogueInterruption(string cue)
    {
        if (!started || cue != "boss_entrance" || bossEntranceDirector == null) return false;
        if (!dialogue.PlayInterruptionDirector(bossEntranceDirector)) return false;
        StartCoroutine(PanCameraToBoss());
        return true;
    }

    private IEnumerator PanCameraToBoss()
    {
        Transform boss = encounter != null ? encounter.ActiveBossTransform : null;
        if (presentationCamera == null || boss == null) yield break;
        Vector3 from = presentationCamera.transform.position;
        Vector3 focus = encounter.ActiveBossFocusPosition;
        Vector3 to = new Vector3(focus.x, focus.y - presentationCamera.orthographicSize * .42f, from.z);
        for (float elapsed = 0f; elapsed < bossCameraPanDuration; elapsed += Time.unscaledDeltaTime)
        {
            Vector3 position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / bossCameraPanDuration));
            SetPresentationCameraPosition(position);
            yield return null;
        }
        SetPresentationCameraPosition(to);
    }

    private IEnumerator RunHeroThenReveal()
    {
        if (player != null && incenseDestination != null)
        {
            Vector3 start = player.transform.position;
            // This authored entrance always travels upward and explicitly selects the
            // current Run Up state instead of briefly entering the horizontal run state.
            player.SetPresentationIdle(false);
            player.SetPresentationLocomotion(true, Vector2.up);
            for (float t = 0f; t < heroRunDuration; t += Time.unscaledDeltaTime)
            {
                player.transform.position = Vector3.Lerp(start, incenseDestination.position, Mathf.SmoothStep(0f, 1f, t / heroRunDuration));
                yield return null;
            }
            player.transform.position = incenseDestination.position;
            player.SetPresentationLocomotion(false, Vector2.zero);
            player.SetPresentationIdle(true);
        }
        dialogue?.ShowDeferredDialogueLine();
    }

    private IEnumerator ShootThenReveal()
    {
        encounter?.PlayGuardArcherPresentation(player);
        yield return new WaitForSecondsRealtime(.7f);
        dialogue?.ShowDeferredDialogueLine();
    }

    private void HandleBossDialogueStarted()
    {
        if (!started) return;
        if (dialogue != null) dialogue.DialogueStarted -= HandleBossDialogueStarted;
        GameAudioManager.StopMusicForDialogue();
        musicStoppedForDialogue = true;
    }

    private void FinishPrelude()
    {
        if (dialogue != null)
        {
            dialogue.DialogueStarted -= HandleBossDialogueStarted;
            dialogue.DialogueFinished -= FinishPrelude;
        }
        musicStoppedForDialogue = false;
        GameAudioManager.PlayBossMusic();
        encounter?.ActivateGuardArchers();
        encounter?.SetBossPresentationIdle(false);
        player?.SetPresentationIdle(false);
        encounter?.ShowBossHud();
        // Ordinary arena enemies do not exist during the cinematic. Start the
        // first wave only after the complete Boss-before dialogue has closed.
        arena?.BeginDeferredWaves();
        player?.ClearCameraCinematicOverride(this);
        RestoreMainCamera();
        performanceSession?.Dispose();
        performanceSession = null;
    }

    private void HandleArenaReset()
    {
        StopAllCoroutines();
        started = false;
        // A checkpoint retry resets every combat entity and wave, but the authored
        // introduction only plays on the first entrance. Replaying it would hide the
        // gameplay HUD again after the death screen restores it.
        skipPreludeOnRetry = true;
        performanceSession?.Dispose();
        performanceSession = null;
        if (dialogue != null)
        {
            dialogue.DialogueStarted -= HandleBossDialogueStarted;
            dialogue.DialogueFinished -= FinishPrelude;
        }
        RestoreMusicAfterInterruptedDialogue();
        if (bossEntranceDirector != null)
        {
            bossEntranceDirector.Stop();
            bossEntranceDirector.time = 0d;
        }
        if (player != null)
        {
            player.SetPresentationLocomotion(false, Vector2.zero);
            player.SetPresentationIdle(false);
        }
        encounter?.SetBossPresentationIdle(false);
        player?.ClearCameraCinematicOverride(this);
        RestoreMainCamera();
    }

    private void RestartBossCombatWithoutPrelude()
    {
        arena?.SetWavesDeferred(true);
        player?.SetPresentationLocomotion(false, Vector2.zero);
        player?.SetPresentationIdle(false);
        encounter?.SpawnBoss();
        encounter?.SetBossPresentationIdle(false);
        encounter?.ActivateGuardArchers();
        encounter?.ShowBossHud();
        player?.ClearCameraCinematicOverride(this);
        RestoreMainCamera();
        GameAudioManager.PlayBossMusic();
        arena?.BeginDeferredWaves();
    }

    private void OnDisable()
    {
        performanceSession?.Dispose();
        performanceSession = null;
        if (dialogue != null)
        {
            dialogue.DialogueLineShouldWait -= HandleDialogueBreakpoint;
            dialogue.DialogueInterruptionShouldPlay -= HandleDialogueInterruption;
            dialogue.DialogueStarted -= HandleBossDialogueStarted;
            dialogue.DialogueFinished -= FinishPrelude;
        }
        RestoreMusicAfterInterruptedDialogue();
        if (player != null)
        {
            player.SetPresentationLocomotion(false, Vector2.zero);
            player.SetPresentationIdle(false);
        }
        encounter?.SetBossPresentationIdle(false);
        player?.ClearCameraCinematicOverride(this);
        RestoreMainCamera();
    }

    private void RestoreMusicAfterInterruptedDialogue()
    {
        if (!musicStoppedForDialogue) return;
        musicStoppedForDialogue = false;
        GameAudioManager.ResumeSceneMusic();
    }

    private void OnDestroy()
    {
        if (arena != null) arena.ZoneReset -= HandleArenaReset;
    }

    private void ResolvePresentationCameras()
    {
        mainCamera ??= player != null ? player.WorldCamera : null;
        mainCamera ??= Camera.main;
        if (mainCamera == null && presentationCamera != null) mainCamera = presentationCamera;
        if (cinematicCamera == null && presentationCamera != null && presentationCamera != mainCamera)
            cinematicCamera = presentationCamera;
        if (cinematicCamera == null)
        {
            foreach (Camera candidate in FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (candidate == null || candidate == mainCamera) continue;
                cinematicCamera = candidate;
                break;
            }
        }

        presentationCamera = cinematicCamera != null ? cinematicCamera : mainCamera;
        mainAudioListener = mainCamera != null ? mainCamera.GetComponent<AudioListener>() : null;
        cinematicAudioListener = cinematicCamera != null ? cinematicCamera.GetComponent<AudioListener>() : null;
    }

    private void SetPresentationCameraPosition(Vector3 position)
    {
        if (presentationCamera == null) return;
        if (presentationCamera == mainCamera && player != null)
            player.SetCameraCinematicOverride(this, position);
        else
            presentationCamera.transform.position = position;
    }

    private void SwitchToCinematicCamera()
    {
        ResolvePresentationCameras();
        if (cinematicCamera == null || cinematicCamera == mainCamera) return;

        cinematicCameraObjectWasActive = cinematicCamera.gameObject.activeSelf;
        if (mainCamera != null)
        {
            // Start from the exact gameplay view so switching cameras is invisible;
            // boss_entrance then moves this camera from the player view to the boss.
            cinematicCamera.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
            cinematicCamera.orthographic = mainCamera.orthographic;
            cinematicCamera.orthographicSize = mainCamera.orthographicSize;
            cinematicCamera.fieldOfView = mainCamera.fieldOfView;
            cinematicCamera.cullingMask = mainCamera.cullingMask;
        }
        cinematicCamera.gameObject.SetActive(true);
        cinematicCamera.enabled = true;
        if (mainCamera != null) mainCamera.enabled = false;
        if (cinematicAudioListener != null)
        {
            if (mainAudioListener != null) mainAudioListener.enabled = false;
            cinematicAudioListener.enabled = true;
        }
        dialogue?.SetWorldCamera(cinematicCamera);
    }

    private void RestoreMainCamera()
    {
        if (mainCamera != null)
        {
            mainCamera.enabled = true;
            if (mainAudioListener != null) mainAudioListener.enabled = true;
            dialogue?.SetWorldCamera(mainCamera);
        }
        if (cinematicCamera == null || cinematicCamera == mainCamera) return;
        if (cinematicAudioListener != null) cinematicAudioListener.enabled = false;
        cinematicCamera.enabled = false;
        cinematicCamera.gameObject.SetActive(cinematicCameraObjectWasActive);
    }
}
