using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Signal receiver target for a Timeline dialogue beat. Bind <see cref="PlayDialogueAndResumeTimeline"/>
/// from a Timeline Signal; it pauses the director, plays its assigned dialogue, then resumes it.
/// </summary>
[DisallowMultipleComponent]
public sealed class TimelineDialogueCuePlayer : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;

    [Header("Dialogue Beat")]
    [SerializeField] private ClickDialogueSystem dialogueSystem;
    [SerializeField] private TextAsset dialogueCsv;
    [SerializeField] private Transform characterSpeaker;
    [SerializeField] private Transform npcSpeaker;

    private bool isWaitingForDialogue;
    private PresentationSession performanceSession;

    /// <summary>Persistent SignalReceiver event target. Pauses the active Timeline at this signal.</summary>
    public void PlayDialogueAndResumeTimeline()
    {
        if (isWaitingForDialogue) return;

        director ??= GetComponent<PlayableDirector>();
        dialogueSystem ??= FindAnyObjectByType<ClickDialogueSystem>();
        if (dialogueSystem == null || dialogueCsv == null)
        {
            Debug.LogWarning("Timeline dialogue cue needs a dialogue system and CSV.", this);
            return;
        }

        if (DialoguePerformanceManager.IsDialogueActive)
        {
            Debug.LogWarning("Timeline dialogue cue cannot start while another dialogue session is active.", this);
            return;
        }

        if (director != null) director.Pause();
        isWaitingForDialogue = true;
        string sessionName = director != null && director.playableAsset != null
            ? director.playableAsset.name
            : "Timeline Dialogue Cue";
        performanceSession = DialoguePerformanceManager.BeginPerformance(this, sessionName);
        dialogueSystem.DialogueFinished += ResumeTimeline;
        if (!dialogueSystem.StartDialogue(dialogueCsv, characterSpeaker, npcSpeaker)) ResumeTimeline();
    }

    private void ResumeTimeline()
    {
        if (!isWaitingForDialogue) return;

        isWaitingForDialogue = false;
        performanceSession?.Dispose();
        performanceSession = null;
        if (dialogueSystem != null) dialogueSystem.DialogueFinished -= ResumeTimeline;
        if (director != null && director.state == PlayState.Paused) director.Play();
    }

    private void OnDisable()
    {
        if (dialogueSystem != null) dialogueSystem.DialogueFinished -= ResumeTimeline;
        isWaitingForDialogue = false;
        performanceSession?.Dispose();
        performanceSession = null;
    }
}
