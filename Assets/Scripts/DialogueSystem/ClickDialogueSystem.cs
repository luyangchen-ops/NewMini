using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

/// <summary>Extra scene cinematic dialogue. Its UI template and bars are authored in the scene.</summary>
public sealed class ClickDialogueSystem : MonoBehaviour
{
    public bool IsDialoguePlaying => isDialoguePlaying;
    public bool IsInterruptedForPerformance => isInterruptedForPerformance;
    public event Action DialogueStarted;
    public event Action DialogueFinished;
    public event Action DialogueInterrupted;
    public event Action DialogueResumed;
    /// <summary>Raised when the player finishes the current line and advances past it.</summary>
    public event Action<int, string, string> DialogueLineCompleted;
    /// <summary>Return true after starting a director to consume an authored CSV interruption cue.</summary>
    public event Func<string, bool> DialogueInterruptionShouldPlay;
    /// <summary>Return true to defer revealing the indexed line until ShowDeferredDialogueLine is called.</summary>
    public event Func<int, string, string, bool> DialogueLineShouldWait;

    public void SetWorldCamera(Camera cameraToUse)
    {
        if (cameraToUse != null) worldCamera = cameraToUse;
    }

    [Serializable]
    public sealed class LegacyDialogueLine
    {
        public Transform speaker;
        [TextArea(2, 4)] public string content;
    }

    [Serializable]
    public sealed class DialogueInterruptionCue
    {
        [Tooltip("Matches the optional third CSV column on the line after which this performance plays.")]
        public string cueId;
        public PlayableDirector director;
    }

    private enum SpeakerKind { System, Character, Soldier }

    private readonly struct DialogueLine
    {
        public DialogueLine(SpeakerKind speaker, string speakerName, string content, Transform followTarget = null,
            string interruptionCue = null, string completionCue = null)
        {
            Speaker = speaker;
            SpeakerName = speakerName;
            Content = content;
            FollowTarget = followTarget;
            InterruptionCue = interruptionCue;
            CompletionCue = completionCue;
        }
        public SpeakerKind Speaker { get; }
        public string SpeakerName { get; }
        public string Content { get; }
        public Transform FollowTarget { get; }
        public string InterruptionCue { get; }
        public string CompletionCue { get; }
    }

    [Header("Scene-authored UI")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private RectTransform bubbleContainer;
    [SerializeField] private RectTransform bubbleTemplate;
    [SerializeField] private GameObject advanceInputLayer;
    [SerializeField] private GameObject gameplayHudRoot;
    [SerializeField] private GameObject topLetterbox;
    [SerializeField] private GameObject bottomLetterbox;

    [Header("Dialogue Interruption Performance")]
    [Tooltip("Optional Timeline used by Play Configured Interruption. The dialogue resumes when it stops.")]
    [SerializeField] private PlayableDirector interruptionDirector;
    [SerializeField] private DialogueInterruptionCue[] interruptionCues;

    [Header("Dialogue Data")]
    [SerializeField] private TextAsset dialogueCsv;
    [SerializeField] private Transform character;
    [SerializeField] private Transform soldier;
    [SerializeField] private string soldierObjectName = "Enemy";

    [Header("Legacy Scene Compatibility")]
    [SerializeField] private LegacyDialogueLine[] lines;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.35f, 0f);
    [SerializeField] private bool playFirstLineOnStart;
    [SerializeField] private Behaviour[] controlsToPause;
    [SerializeField] private Behaviour[] enemiesToPause;

    [Header("Presentation")]
    [SerializeField, Min(.05f)] private float letterboxDuration = .65f;
    [SerializeField, Min(.05f)] private float bubbleEnterDuration = .32f;
    [SerializeField] private Vector2 characterOffset = new Vector2(1.05f, 1.45f);
    [SerializeField] private Vector2 soldierOffset = new Vector2(-1.05f, 1.45f);
    [SerializeField] private Vector2 systemPosition = new Vector2(0f, 310f);
    [SerializeField] private Vector2 minimumBubbleSize = new Vector2(420f, 128f);
    [SerializeField, Min(1f)] private float maximumBubbleWidth = 760f;
    [SerializeField] private Vector2 bubbleTextPadding = new Vector2(180f, 80f);
    [SerializeField, Min(0f)] private float dialogueSafeMargin = 18f;
    [SerializeField, Min(0f)] private float minimumLineReadTime = 1.8f;
    [SerializeField, Min(0f)] private float secondsPerCharacter = .055f;
    [SerializeField, Min(0f)] private float maximumLineReadTime = 4.5f;

    private readonly List<DialogueLine> dialogueLines = new List<DialogueLine>();
    private readonly List<Behaviour> pausedBehaviours = new List<Behaviour>();
    private readonly HashSet<int> triggeredInterruptionLines = new HashSet<int>();
    private readonly Vector3[] letterboxWorldCorners = new Vector3[4];
    private RectTransform activeBubble;
    private Transform activeSpeaker;
    private Vector2 activeWorldOffset;
    private int currentLine = -1;
    private bool isDialoguePlaying;
    private bool isTransitioning;
    private bool isWaitingForFirstLine;
    private bool isWaitingForDeferredLine;
    private bool firstLineRevealRequested;
    private bool isInterruptedForPerformance;
    private bool advanceAfterActiveInterruption;
    private bool activeBubbleWasVisibleBeforeInterruption;
    private bool gameplayHudWasActive;
    private bool useLowerScreenDialoguePlacement;
    private string conditionalLowerScreenSpeakerName;
    private Transform conditionalLowerScreenSpeaker;
    private Coroutine bubbleAnimation;
    private float lineAutoAdvanceAt = float.PositiveInfinity;
    private float remainingLineReadTime = float.PositiveInfinity;
    private PlayableDirector activeInterruptionDirector;
    private PresentationSession dialogueSession;
    private PresentationSession interruptionPerformanceSession;
    private Vector2 topShownPosition;
    private Vector2 bottomShownPosition;
    private RectTransform TopLetterboxRect => topLetterbox != null ? topLetterbox.transform as RectTransform : null;
    private RectTransform BottomLetterboxRect => bottomLetterbox != null ? bottomLetterbox.transform as RectTransform : null;

    private void Awake()
    {
        if (bubbleTemplate != null) bubbleTemplate.gameObject.SetActive(false);
        if (advanceInputLayer != null) advanceInputLayer.SetActive(false);
        ResolveSceneActors();
        ParseDialogue();
        CacheLetterboxPositions();
        SetLetterboxImmediate(false);
    }

    private void Start()
    {
        if (playFirstLineOnStart) StartDialogue();
    }

    private void Update()
    {
        if (!isDialoguePlaying)
        {
            if (!isTransitioning && Keyboard.current?.lKey.wasPressedThisFrame == true)
                StartDialogue();
            return;
        }

        if (isInterruptedForPerformance) return;

        bool advance = Mouse.current?.leftButton.wasPressedThisFrame == true
            || Keyboard.current?.spaceKey.wasPressedThisFrame == true
            || Keyboard.current?.enterKey.wasPressedThisFrame == true;
        if (!isTransitioning && !isWaitingForFirstLine
            && (advance || Time.unscaledTime >= lineAutoAdvanceAt))
            AdvanceDialogue();
    }

    private void LateUpdate()
    {
        if (!isInterruptedForPerformance && activeBubble != null && activeSpeaker != null && bubbleAnimation == null)
            PositionWorldBubble(activeBubble, activeSpeaker, activeWorldOffset);
    }

    /// <summary>Starts the authored dialogue sequence from a persistent scene event or cinematic controller.</summary>
    public bool StartDialogue()
    {
        return BeginDialoguePresentation(showFirstLineWhenReady: true);
    }

    /// <summary>
    /// Starts the cinematic framing immediately, but waits for the owner to reveal the first line.
    /// Use this while an actor entrance plays beneath the letterbox animation.
    /// </summary>
    public bool BeginCinematic()
    {
        return BeginDialoguePresentation(showFirstLineWhenReady: false);
    }

    /// <summary>Reveals the first line after <see cref="BeginCinematic"/> has prepared the presentation.</summary>
    public bool ShowFirstDialogueLine()
    {
        if (!isDialoguePlaying || !isWaitingForFirstLine) return false;
        if (isTransitioning)
        {
            firstLineRevealRequested = true;
            return true;
        }
        isWaitingForFirstLine = false;
        AdvanceDialogue();
        return true;
    }

    /// <summary>Reveals the line currently held by a <see cref="DialogueLineShouldWait"/> listener.</summary>
    public bool ShowDeferredDialogueLine()
    {
        if (!isDialoguePlaying || isTransitioning || !isWaitingForDeferredLine
            || currentLine < 0 || currentLine >= dialogueLines.Count) return false;

        isWaitingForDeferredLine = false;
        ShowLine(dialogueLines[currentLine]);
        return true;
    }

    /// <summary>Suspends the active line for a performance without ending its dialogue session.</summary>
    public bool InterruptForPerformance()
    {
        if (!isDialoguePlaying || isTransitioning || isInterruptedForPerformance) return false;

        isInterruptedForPerformance = true;
        interruptionPerformanceSession?.Dispose();
        interruptionPerformanceSession = DialoguePerformanceManager.BeginPerformance(this, "Dialogue Interruption");
        remainingLineReadTime = float.IsPositiveInfinity(lineAutoAdvanceAt)
            ? float.PositiveInfinity
            : Mathf.Max(0f, lineAutoAdvanceAt - Time.unscaledTime);
        lineAutoAdvanceAt = float.PositiveInfinity;
        if (bubbleAnimation != null) StopCoroutine(bubbleAnimation);
        bubbleAnimation = null;
        activeBubbleWasVisibleBeforeInterruption = activeBubble != null && activeBubble.gameObject.activeSelf;
        if (activeBubble != null) activeBubble.gameObject.SetActive(false);
        if (advanceInputLayer != null) advanceInputLayer.SetActive(false);
        DialogueInterrupted?.Invoke();
        return true;
    }

    /// <summary>Restores the exact dialogue line and its remaining auto-advance time.</summary>
    public bool ResumeAfterPerformance()
    {
        if (!isDialoguePlaying || !isInterruptedForPerformance) return false;

        isInterruptedForPerformance = false;
        interruptionPerformanceSession?.Dispose();
        interruptionPerformanceSession = null;
        lineAutoAdvanceAt = float.IsPositiveInfinity(remainingLineReadTime)
            ? float.PositiveInfinity
            : Time.unscaledTime + remainingLineReadTime;
        remainingLineReadTime = float.PositiveInfinity;
        if (activeBubble != null && activeBubbleWasVisibleBeforeInterruption)
        {
            activeBubble.gameObject.SetActive(true);
            if (activeSpeaker != null) PositionWorldBubble(activeBubble, activeSpeaker, activeWorldOffset);
        }
        if (advanceInputLayer != null) advanceInputLayer.SetActive(true);
        DialogueResumed?.Invoke();
        return true;
    }

    /// <summary>Plays the authored interruption Timeline and resumes when it stops.</summary>
    public bool PlayConfiguredInterruption()
    {
        return PlayInterruptionDirector(interruptionDirector);
    }

    private void HandleInterruptionStopped(PlayableDirector director)
    {
        if (director != activeInterruptionDirector) return;
        activeInterruptionDirector.stopped -= HandleInterruptionStopped;
        activeInterruptionDirector = null;
        bool advanceAfterPerformance = advanceAfterActiveInterruption;
        advanceAfterActiveInterruption = false;
        ResumeAfterPerformance();
        if (advanceAfterPerformance && isDialoguePlaying) AdvanceDialogue();
    }

    public bool PlayInterruptionDirector(PlayableDirector director)
    {
        if (director == null || !InterruptForPerformance()) return false;

        activeInterruptionDirector = director;
        director.stopped -= HandleInterruptionStopped;
        director.stopped += HandleInterruptionStopped;
        director.time = 0d;
        director.Play();
        return true;
    }

    /// <summary>
    /// Reuses the authored dialogue presentation for a different story sequence.
    /// Speaker transforms remain explicit scene references supplied by the caller.
    /// </summary>
    public bool StartDialogue(TextAsset csv, Transform characterSpeaker, Transform npcSpeaker)
    {
        if (isDialoguePlaying || isTransitioning || csv == null) return false;

        dialogueCsv = csv;
        if (characterSpeaker != null) character = characterSpeaker;
        if (npcSpeaker != null) soldier = npcSpeaker;
        ParseDialogue();
        return StartDialogue();
    }

    /// <summary>
    /// Plays an authored dialogue sequence in the lower visible screen area.
    /// This is intended for endings where the speaker is outside the camera view.
    /// </summary>
    public bool StartDialogueAtLowerScreen(TextAsset csv, Transform characterSpeaker, Transform npcSpeaker)
    {
        if (isDialoguePlaying || isTransitioning || csv == null) return false;

        useLowerScreenDialoguePlacement = true;
        bool started = StartDialogue(csv, characterSpeaker, npcSpeaker);
        if (!started) useLowerScreenDialoguePlacement = false;
        return started;
    }

    /// <summary>
    /// Keeps the normal presentation for every line except one named speaker,
    /// whose bubble moves to the lower screen only while that speaker is outside the camera view.
    /// </summary>
    public bool StartDialogueWithOffscreenSpeakerAtLowerScreen(
        TextAsset csv,
        Transform characterSpeaker,
        Transform npcSpeaker,
        string offscreenSpeakerName,
        Transform offscreenSpeaker)
    {
        if (isDialoguePlaying || isTransitioning || csv == null || string.IsNullOrWhiteSpace(offscreenSpeakerName))
            return false;

        conditionalLowerScreenSpeakerName = offscreenSpeakerName;
        conditionalLowerScreenSpeaker = offscreenSpeaker;
        bool started = StartDialogue(csv, characterSpeaker, npcSpeaker);
        if (!started)
        {
            conditionalLowerScreenSpeakerName = null;
            conditionalLowerScreenSpeaker = null;
        }
        return started;
    }

    private bool BeginDialoguePresentation(bool showFirstLineWhenReady)
    {
        if (isDialoguePlaying || isTransitioning || dialogueLines.Count == 0) return false;
        StartCoroutine(BeginDialogue(showFirstLineWhenReady));
        return true;
    }

    private IEnumerator BeginDialogue(bool showFirstLineWhenReady)
    {
        if (dialogueLines.Count == 0)
        {
            Debug.LogWarning("Extra dialogue CSV has no playable lines.", this);
            yield break;
        }

        isTransitioning = true;
        isDialoguePlaying = true;
        dialogueSession?.Dispose();
        dialogueSession = DialoguePerformanceManager.BeginDialogue(this, dialogueCsv != null ? dialogueCsv.name : name);
        DialogueStarted?.Invoke();
        if (advanceInputLayer != null) advanceInputLayer.SetActive(true);
        HideGameplayHud();
        currentLine = -1;
        triggeredInterruptionLines.Clear();
        lineAutoAdvanceAt = float.PositiveInfinity;
        remainingLineReadTime = float.PositiveInfinity;
        isWaitingForFirstLine = !showFirstLineWhenReady;
        isWaitingForDeferredLine = false;
        firstLineRevealRequested = false;
        isInterruptedForPerformance = false;
        advanceAfterActiveInterruption = false;
        PauseGameplay();
        yield return AnimateLetterbox(true);
        isTransitioning = false;
        if (showFirstLineWhenReady || firstLineRevealRequested)
        {
            isWaitingForFirstLine = false;
            firstLineRevealRequested = false;
            AdvanceDialogue();
        }
    }

    /// <summary>Persistent scene event target for Btn_ContinueDialogue.</summary>
    public void AdvanceDialogue()
    {
        if (!isDialoguePlaying || isTransitioning || isInterruptedForPerformance || isWaitingForDeferredLine) return;
        if (TryPlayMarkedInterruptionAfterCurrentLine()) return;
        NotifyCurrentLineCompleted();
        int nextLine = currentLine + 1;
        if (nextLine >= dialogueLines.Count)
        {
            lineAutoAdvanceAt = float.PositiveInfinity;
            StartCoroutine(EndDialogue());
            return;
        }
        currentLine = nextLine;
        if (ShouldDeferLine(currentLine))
        {
            isWaitingForDeferredLine = true;
            lineAutoAdvanceAt = float.PositiveInfinity;
            return;
        }
        ShowLine(dialogueLines[currentLine]);
    }

    private void NotifyCurrentLineCompleted()
    {
        if (currentLine < 0 || currentLine >= dialogueLines.Count) return;
        DialogueLine line = dialogueLines[currentLine];
        DialogueLineCompleted?.Invoke(currentLine, line.SpeakerName, line.CompletionCue);
    }

    private void ShowLine(DialogueLine line)
    {
        float maximum = Mathf.Max(minimumLineReadTime, maximumLineReadTime);
        float readTime = Mathf.Clamp(
            minimumLineReadTime + line.Content.Length * secondsPerCharacter,
            minimumLineReadTime,
            maximum);
        lineAutoAdvanceAt = Time.unscaledTime + readTime;

        if (activeBubble != null) Destroy(activeBubble.gameObject);
        if (bubbleTemplate == null || bubbleContainer == null) return;

        activeBubble = Instantiate(bubbleTemplate, bubbleContainer);
        activeBubble.name = line.Speaker == SpeakerKind.System ? "Panel_ActiveSystemBubble"
            : line.Speaker == SpeakerKind.Soldier ? "Panel_ActiveSoldierBubble"
            : "Panel_ActiveCharacterBubble";
        activeBubble.gameObject.SetActive(true);
        Text label = activeBubble.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = line.Content;
            ResizeBubbleToContent(activeBubble, label);
        }

        activeSpeaker = line.FollowTarget;
        if (line.FollowTarget != null) activeWorldOffset = worldOffset;
        else if (line.Speaker == SpeakerKind.Character) { activeSpeaker = character; activeWorldOffset = characterOffset; }
        else if (line.Speaker == SpeakerKind.Soldier) { activeSpeaker = soldier; activeWorldOffset = soldierOffset; }

        bool isConditionalLowerScreenSpeaker = !string.IsNullOrEmpty(conditionalLowerScreenSpeakerName)
            && string.Equals(line.SpeakerName, conditionalLowerScreenSpeakerName, StringComparison.Ordinal);
        if (isConditionalLowerScreenSpeaker)
        {
            activeSpeaker = conditionalLowerScreenSpeaker;
            activeWorldOffset = soldierOffset;
        }

        bool systemPresentation = line.Speaker == SpeakerKind.System && line.FollowTarget == null;
        bool lowerScreenPresentation = useLowerScreenDialoguePlacement
            || isConditionalLowerScreenSpeaker && IsOutsideCameraView(activeSpeaker, activeWorldOffset);
        if (lowerScreenPresentation) activeSpeaker = null;
        Vector2 target = lowerScreenPresentation
            ? GetLowerScreenBubblePosition()
            : systemPresentation ? systemPosition : GetWorldBubblePosition(activeSpeaker, activeWorldOffset);
        target = ClampBubbleToPresentationArea(target, activeBubble);
        float enterOffset = systemPresentation ? 72f : 36f;
        float enterDirection = target.y >= 0f ? -1f : 1f;
        Vector2 start = ClampBubbleToPresentationArea(
            target + Vector2.up * (enterOffset * enterDirection), activeBubble);
        bubbleAnimation = StartCoroutine(AnimateBubble(start, target));
    }

    private void ResizeBubbleToContent(RectTransform bubble, Text label)
    {
        float minimumWidth = minimumBubbleSize.x > 0f ? minimumBubbleSize.x : 420f;
        float minimumHeight = minimumBubbleSize.y > 0f ? minimumBubbleSize.y : 128f;
        float maximumWidth = Mathf.Max(minimumWidth, maximumBubbleWidth);
        float horizontalPadding = Mathf.Max(0f, bubbleTextPadding.x);
        float verticalPadding = Mathf.Max(0f, bubbleTextPadding.y);

        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        Canvas.ForceUpdateCanvases();

        float width = Mathf.Clamp(label.preferredWidth + horizontalPadding, minimumWidth, maximumWidth);
        bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        Canvas.ForceUpdateCanvases();

        float height = Mathf.Max(minimumHeight, label.preferredHeight + verticalPadding);
        bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        LayoutRebuilder.ForceRebuildLayoutImmediate(bubble);
    }

    private IEnumerator EndDialogue()
    {
        isTransitioning = true;
        if (advanceInputLayer != null) advanceInputLayer.SetActive(false);
        if (bubbleAnimation != null) StopCoroutine(bubbleAnimation);
        bubbleAnimation = null;
        activeSpeaker = null;
        if (activeBubble != null) { Destroy(activeBubble.gameObject); activeBubble = null; }
        yield return AnimateLetterbox(false);
        RestoreGameplayHud();
        ResumeGameplay();
        currentLine = -1;
        triggeredInterruptionLines.Clear();
        lineAutoAdvanceAt = float.PositiveInfinity;
        remainingLineReadTime = float.PositiveInfinity;
        isWaitingForFirstLine = false;
        isWaitingForDeferredLine = false;
        firstLineRevealRequested = false;
        isInterruptedForPerformance = false;
        advanceAfterActiveInterruption = false;
        useLowerScreenDialoguePlacement = false;
        conditionalLowerScreenSpeakerName = null;
        conditionalLowerScreenSpeaker = null;
        isDialoguePlaying = false;
        isTransitioning = false;
        dialogueSession?.Dispose();
        dialogueSession = null;
        DialogueFinished?.Invoke();
    }

    private IEnumerator AnimateBubble(Vector2 start, Vector2 target)
    {
        float elapsed = 0f;
        while (elapsed < bubbleEnterDuration && activeBubble != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / bubbleEnterDuration));
            activeBubble.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
            yield return null;
        }
        if (activeBubble != null) activeBubble.anchoredPosition = target;
        bubbleAnimation = null;
    }

    private IEnumerator AnimateLetterbox(bool enter)
    {
        RectTransform topRect = TopLetterboxRect;
        RectTransform bottomRect = BottomLetterboxRect;
        if (topRect == null || bottomRect == null) yield break;
        topLetterbox.SetActive(true);
        bottomLetterbox.SetActive(true);
        Vector2 topHidden = topShownPosition + Vector2.up * topRect.rect.height;
        Vector2 bottomHidden = bottomShownPosition + Vector2.down * bottomRect.rect.height;
        Vector2 topStart = enter ? topHidden : topShownPosition;
        Vector2 topEnd = enter ? topShownPosition : topHidden;
        Vector2 bottomStart = enter ? bottomHidden : bottomShownPosition;
        Vector2 bottomEnd = enter ? bottomShownPosition : bottomHidden;

        float elapsed = 0f;
        while (elapsed < letterboxDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / letterboxDuration));
            topRect.anchoredPosition = Vector2.LerpUnclamped(topStart, topEnd, t);
            bottomRect.anchoredPosition = Vector2.LerpUnclamped(bottomStart, bottomEnd, t);
            yield return null;
        }
        topRect.anchoredPosition = topEnd;
        bottomRect.anchoredPosition = bottomEnd;
        if (!enter) { topLetterbox.SetActive(false); bottomLetterbox.SetActive(false); }
    }

    private void ResolveSceneActors()
    {
        worldCamera ??= Camera.main;
        if (character == null)
        {
            PlayerCharacterController player = FindAnyObjectByType<PlayerCharacterController>();
            if (player != null) character = player.transform;
        }
        if (soldier == null)
        {
            GameObject namedSoldier = GameObject.Find(soldierObjectName);
            EnemyAgent enemy = namedSoldier != null ? namedSoldier.GetComponent<EnemyAgent>() : null;
            enemy ??= FindAnyObjectByType<EnemyAgent>();
            if (enemy != null) soldier = enemy.transform;
        }
    }

    private void PauseGameplay()
    {
        pausedBehaviours.Clear();
        PlayerCharacterController player = character != null ? character.GetComponent<PlayerCharacterController>() : FindAnyObjectByType<PlayerCharacterController>();
        PauseBehaviour(player);
        foreach (EnemyAgent enemy in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Exclude)) PauseBehaviour(enemy);
        PauseConfiguredBehaviours(controlsToPause);
        PauseConfiguredBehaviours(enemiesToPause);
    }

    private void PauseConfiguredBehaviours(Behaviour[] behaviours)
    {
        if (behaviours == null) return;
        foreach (Behaviour behaviour in behaviours) PauseBehaviour(behaviour);
    }

    private void PauseBehaviour(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;
        pausedBehaviours.Add(behaviour);
        behaviour.enabled = false;
    }

    private void ResumeGameplay()
    {
        foreach (Behaviour behaviour in pausedBehaviours) if (behaviour != null) behaviour.enabled = true;
        pausedBehaviours.Clear();
    }

    private void HideGameplayHud()
    {
        if (gameplayHudRoot == null) return;
        gameplayHudWasActive = gameplayHudRoot.activeSelf;
        gameplayHudRoot.SetActive(false);
    }

    private void RestoreGameplayHud()
    {
        if (gameplayHudRoot != null) gameplayHudRoot.SetActive(gameplayHudWasActive);
    }

    private void ParseDialogue()
    {
        dialogueLines.Clear();
        if (dialogueCsv == null)
        {
            if (lines == null) return;
            foreach (LegacyDialogueLine line in lines)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.content)) continue;
                dialogueLines.Add(new DialogueLine(SpeakerKind.Character, null, line.content, line.speaker));
            }
            return;
        }
        List<List<string>> rows = ParseCsv(dialogueCsv.text);
        for (int i = 1; i < rows.Count; i++)
        {
            if (rows[i].Count < 2 || string.IsNullOrWhiteSpace(rows[i][1])) continue;
            string speaker = rows[i][0].Trim();
            SpeakerKind kind = speaker == "系统"
                ? SpeakerKind.System
                : speaker == "角色" ? SpeakerKind.Character : SpeakerKind.Soldier;
            string cue = rows[i].Count > 2 ? rows[i][2].Trim() : null;
            string completionCue = rows[i].Count > 3 ? rows[i][3].Trim() : null;
            dialogueLines.Add(new DialogueLine(kind, speaker, rows[i][1].Trim(), null, cue, completionCue));
        }
    }

    private bool TryPlayMarkedInterruptionAfterCurrentLine()
    {
        if (currentLine < 0 || currentLine >= dialogueLines.Count || triggeredInterruptionLines.Contains(currentLine)) return false;
        string cueId = dialogueLines[currentLine].InterruptionCue;
        if (string.IsNullOrWhiteSpace(cueId)) return false;

        if (DialogueInterruptionShouldPlay != null)
        {
            foreach (Func<string, bool> listener in DialogueInterruptionShouldPlay.GetInvocationList())
            {
                if (listener == null || !listener(cueId)) continue;
                triggeredInterruptionLines.Add(currentLine);
                advanceAfterActiveInterruption = true;
                return true;
            }
        }

        if (interruptionCues != null)
        {
            foreach (DialogueInterruptionCue cue in interruptionCues)
            {
                if (cue == null || !string.Equals(cue.cueId, cueId, StringComparison.OrdinalIgnoreCase)) continue;
                if (cue.director == null) break;
                triggeredInterruptionLines.Add(currentLine);
                bool started = PlayInterruptionDirector(cue.director);
                advanceAfterActiveInterruption = started;
                return started;
            }
        }

        Debug.LogWarning($"Dialogue interruption cue '{cueId}' has no assigned PlayableDirector.", this);
        triggeredInterruptionLines.Add(currentLine);
        return false;
    }

    private bool ShouldDeferLine(int lineIndex)
    {
        if (DialogueLineShouldWait == null) return false;
        string speaker = dialogueLines[lineIndex].Speaker.ToString();
        string cue = dialogueLines[lineIndex].InterruptionCue;
        foreach (Func<int, string, string, bool> listener in DialogueLineShouldWait.GetInvocationList())
            if (listener != null && listener(lineIndex, speaker, cue)) return true;
        return false;
    }

    private static List<List<string>> ParseCsv(string csv)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];
            if (c == '"') { if (quoted && i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; } else quoted = !quoted; }
            else if (c == ',' && !quoted) { row.Add(cell.ToString()); cell.Clear(); }
            else if ((c == '\n' || c == '\r') && !quoted)
            {
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear(); if (row.Count > 1 || row[0].Length > 0) rows.Add(row); row = new List<string>();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }

    private void PositionWorldBubble(RectTransform bubble, Transform speaker, Vector2 offset)
    {
        Vector2 target = GetWorldBubblePosition(speaker, offset);
        bubble.anchoredPosition = ClampBubbleToPresentationArea(target, bubble);
    }

    private Vector2 GetWorldBubblePosition(Transform speaker, Vector2 offset)
    {
        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null || speaker == null || bubbleContainer == null) return Vector2.zero;
        Vector2 screen = cameraToUse.WorldToScreenPoint(speaker.position + (Vector3)offset);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(bubbleContainer, screen, null, out Vector2 local);
        return local;
    }

    private bool IsOutsideCameraView(Transform speaker, Vector2 offset)
    {
        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null || speaker == null) return true;

        Vector3 viewport = cameraToUse.WorldToViewportPoint(speaker.position + (Vector3)offset);
        return viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f;
    }

    private Vector2 GetLowerScreenBubblePosition()
    {
        if (bubbleContainer == null) return Vector2.zero;

        float lowerVisibleEdge = 0f;
        if (bottomLetterbox != null && bottomLetterbox.activeInHierarchy)
        {
            RectTransform bottomRect = BottomLetterboxRect;
            if (bottomRect != null)
            {
                Vector3[] corners = new Vector3[4];
                bottomRect.GetWorldCorners(corners);
                Camera canvasCamera = GetCanvasCamera();
                foreach (Vector3 corner in corners)
                    lowerVisibleEdge = Mathf.Max(lowerVisibleEdge,
                        RectTransformUtility.WorldToScreenPoint(canvasCamera, corner).y);
            }
        }

        // Place the bubble near the bottom of the usable image area, but leave
        // enough room for the entire bubble above the lower letterbox bar.
        float targetScreenY = Mathf.Lerp(lowerVisibleEdge, Screen.height, .2f);
        Vector2 screen = new(Screen.width * .5f, targetScreenY);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bubbleContainer, screen, GetCanvasCamera(), out Vector2 local);
        return local;
    }

    private Vector2 ClampBubbleToPresentationArea(Vector2 position, RectTransform bubble)
    {
        if (bubbleContainer == null || bubble == null) return position;

        Rect containerRect = bubbleContainer.rect;
        float margin = Mathf.Max(0f, dialogueSafeMargin);
        float safeLeft = containerRect.xMin + margin;
        float safeRight = containerRect.xMax - margin;
        float safeBottom = containerRect.yMin + margin;
        float safeTop = containerRect.yMax - margin;

        RectTransform bottomRect = BottomLetterboxRect;
        if (bottomLetterbox != null && bottomLetterbox.activeInHierarchy && bottomRect != null)
            safeBottom = Mathf.Max(safeBottom, GetLetterboxLocalEdge(bottomRect, maximum: true) + margin);

        RectTransform topRect = TopLetterboxRect;
        if (topLetterbox != null && topLetterbox.activeInHierarchy && topRect != null)
            safeTop = Mathf.Min(safeTop, GetLetterboxLocalEdge(topRect, maximum: false) - margin);

        Rect bubbleRect = bubble.rect;
        float minimumX = safeLeft - bubbleRect.xMin;
        float maximumX = safeRight - bubbleRect.xMax;
        float minimumY = safeBottom - bubbleRect.yMin;
        float maximumY = safeTop - bubbleRect.yMax;

        float x = minimumX <= maximumX
            ? Mathf.Clamp(position.x, minimumX, maximumX)
            : (safeLeft + safeRight - bubbleRect.xMin - bubbleRect.xMax) * .5f;
        float y = minimumY <= maximumY
            ? Mathf.Clamp(position.y, minimumY, maximumY)
            : (safeBottom + safeTop - bubbleRect.yMin - bubbleRect.yMax) * .5f;
        return new Vector2(x, y);
    }

    private float GetLetterboxLocalEdge(RectTransform letterbox, bool maximum)
    {
        letterbox.GetWorldCorners(letterboxWorldCorners);
        float edge = maximum ? float.NegativeInfinity : float.PositiveInfinity;
        foreach (Vector3 worldCorner in letterboxWorldCorners)
        {
            float localY = bubbleContainer.InverseTransformPoint(worldCorner).y;
            edge = maximum ? Mathf.Max(edge, localY) : Mathf.Min(edge, localY);
        }
        return edge;
    }

    private Camera GetCanvasCamera()
    {
        Canvas canvas = bubbleContainer != null ? bubbleContainer.GetComponentInParent<Canvas>() : null;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private void CacheLetterboxPositions()
    {
        if (TopLetterboxRect != null) topShownPosition = TopLetterboxRect.anchoredPosition;
        if (BottomLetterboxRect != null) bottomShownPosition = BottomLetterboxRect.anchoredPosition;
    }

    private void SetLetterboxImmediate(bool shown)
    {
        if (topLetterbox != null) topLetterbox.SetActive(shown);
        if (bottomLetterbox != null) bottomLetterbox.SetActive(shown);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (interruptionDirector != null) interruptionDirector.stopped -= HandleInterruptionStopped;
        if (activeInterruptionDirector != null) activeInterruptionDirector.stopped -= HandleInterruptionStopped;
        activeInterruptionDirector = null;
        interruptionPerformanceSession?.Dispose();
        interruptionPerformanceSession = null;
        dialogueSession?.Dispose();
        dialogueSession = null;
        if (isDialoguePlaying)
        {
            RestoreGameplayHud();
            ResumeGameplay();
        }
        isDialoguePlaying = false;
        isInterruptedForPerformance = false;
        isTransitioning = false;
        isWaitingForFirstLine = false;
        isWaitingForDeferredLine = false;
        currentLine = -1;
        if (advanceInputLayer != null) advanceInputLayer.SetActive(false);
        if (activeBubble != null)
        {
            Destroy(activeBubble.gameObject);
            activeBubble = null;
        }
        activeSpeaker = null;
        SetLetterboxImmediate(false);
    }
}
