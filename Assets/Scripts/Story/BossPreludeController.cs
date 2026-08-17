using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>Final-arena dialogue interlude: Boss/guards appear before normal waves begin.</summary>
[DisallowMultipleComponent]
public sealed class BossPreludeController : MonoBehaviour
{
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
    private TopDownCameraFollow cameraFollow;
    private Camera mainCamera;
    private bool cinematicCameraObjectWasActive;
    private AudioListener mainAudioListener;
    private AudioListener cinematicAudioListener;

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
    }

    public void BeginPrelude()
    {
        if (started) return;
        started = true;
        arena?.SetWavesDeferred(true);
        arena?.ResetDeferredWaves();
        encounter?.SpawnBoss();
        SwitchToCinematicCamera();
        if (dialogue != null && bossBeforeDialogue != null)
        {
            dialogue.DialogueFinished += FinishPrelude;
            dialogue.StartDialogue(bossBeforeDialogue, player != null ? player.transform : null, null);
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
            if (cameraFollow != null) cameraFollow.SetCinematicOverride(this, position);
            else presentationCamera.transform.position = position;
            yield return null;
        }
        if (cameraFollow != null) cameraFollow.SetCinematicOverride(this, to);
        else presentationCamera.transform.position = to;
    }

    private IEnumerator RunHeroThenReveal()
    {
        if (player != null && incenseDestination != null)
        {
            Vector3 start = player.transform.position;
            Vector2 direction = incenseDestination.position - start;
            player.SetPresentationLocomotion(true, direction);
            for (float t = 0f; t < heroRunDuration; t += Time.unscaledDeltaTime)
            {
                player.transform.position = Vector3.Lerp(start, incenseDestination.position, Mathf.SmoothStep(0f, 1f, t / heroRunDuration));
                yield return null;
            }
            player.transform.position = incenseDestination.position;
            player.SetPresentationLocomotion(false, Vector2.zero);
        }
        dialogue?.ShowDeferredDialogueLine();
    }

    private IEnumerator ShootThenReveal()
    {
        encounter?.PlayGuardArcherPresentation(player);
        yield return new WaitForSecondsRealtime(.7f);
        dialogue?.ShowDeferredDialogueLine();
    }

    private void FinishPrelude()
    {
        if (dialogue != null) dialogue.DialogueFinished -= FinishPrelude;
        GameAudioManager.PlayBossMusic();
        encounter?.ActivateGuardArchers();
        encounter?.ShowBossHud();
        // Ordinary arena enemies do not exist during the cinematic. Start the
        // first wave only after the complete Boss-before dialogue has closed.
        arena?.BeginDeferredWaves();
        cameraFollow?.ClearCinematicOverride(this);
        RestoreMainCamera();
    }

    private void OnDisable()
    {
        if (dialogue != null)
        {
            dialogue.DialogueLineShouldWait -= HandleDialogueBreakpoint;
            dialogue.DialogueInterruptionShouldPlay -= HandleDialogueInterruption;
            dialogue.DialogueFinished -= FinishPrelude;
        }
        RestoreMainCamera();
    }

    private void ResolvePresentationCameras()
    {
        if (mainCamera == null && presentationCamera != null
            && presentationCamera.GetComponent<TopDownCameraFollow>() != null)
            mainCamera = presentationCamera;
        if (mainCamera == null)
        {
            foreach (TopDownCameraFollow follow in FindObjectsByType<TopDownCameraFollow>(FindObjectsInactive.Include))
            {
                Camera followedCamera = follow != null ? follow.GetComponent<Camera>() : null;
                if (followedCamera != null) { mainCamera = followedCamera; break; }
            }
        }
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
        cameraFollow = presentationCamera != null ? presentationCamera.GetComponent<TopDownCameraFollow>() : null;
        mainAudioListener = mainCamera != null ? mainCamera.GetComponent<AudioListener>() : null;
        cinematicAudioListener = cinematicCamera != null ? cinematicCamera.GetComponent<AudioListener>() : null;
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
