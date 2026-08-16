using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Extra scene cinematic dialogue. Its UI template and bars are authored in the scene.</summary>
public sealed class ClickDialogueSystem : MonoBehaviour
{
    public bool IsDialoguePlaying => isDialoguePlaying;
    public event Action DialogueStarted;
    public event Action DialogueFinished;

    [Serializable]
    public sealed class LegacyDialogueLine
    {
        public Transform speaker;
        [TextArea(2, 4)] public string content;
    }

    private enum SpeakerKind { System, Character, Soldier }

    private readonly struct DialogueLine
    {
        public DialogueLine(SpeakerKind speaker, string content, Transform followTarget = null)
        { Speaker = speaker; Content = content; FollowTarget = followTarget; }
        public SpeakerKind Speaker { get; }
        public string Content { get; }
        public Transform FollowTarget { get; }
    }

    [Header("Scene-authored UI")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private RectTransform bubbleContainer;
    [SerializeField] private RectTransform bubbleTemplate;
    [SerializeField] private GameObject advanceInputLayer;
    [SerializeField] private GameObject gameplayHudRoot;
    [SerializeField] private GameObject topLetterbox;
    [SerializeField] private GameObject bottomLetterbox;

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
    [SerializeField, Min(0f)] private float minimumLineReadTime = 1.8f;
    [SerializeField, Min(0f)] private float secondsPerCharacter = .055f;
    [SerializeField, Min(0f)] private float maximumLineReadTime = 4.5f;

    private readonly List<DialogueLine> dialogueLines = new List<DialogueLine>();
    private readonly List<Behaviour> pausedBehaviours = new List<Behaviour>();
    private RectTransform activeBubble;
    private Transform activeSpeaker;
    private Vector2 activeWorldOffset;
    private int currentLine = -1;
    private bool isDialoguePlaying;
    private bool isTransitioning;
    private bool gameplayHudWasActive;
    private Coroutine bubbleAnimation;
    private float lineAutoAdvanceAt = float.PositiveInfinity;
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

        bool advance = Mouse.current?.leftButton.wasPressedThisFrame == true
            || Keyboard.current?.spaceKey.wasPressedThisFrame == true
            || Keyboard.current?.enterKey.wasPressedThisFrame == true;
        if (!isTransitioning && (advance || Time.unscaledTime >= lineAutoAdvanceAt))
            AdvanceDialogue();
    }

    private void LateUpdate()
    {
        if (activeBubble != null && activeSpeaker != null && bubbleAnimation == null)
            PositionWorldBubble(activeBubble, activeSpeaker, activeWorldOffset);
    }

    /// <summary>Starts the authored dialogue sequence from a persistent scene event or cinematic controller.</summary>
    public bool StartDialogue()
    {
        if (isDialoguePlaying || isTransitioning || dialogueLines.Count == 0)
        {
            return false;
        }

        StartCoroutine(BeginDialogue());
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

    private IEnumerator BeginDialogue()
    {
        if (dialogueLines.Count == 0)
        {
            Debug.LogWarning("Extra dialogue CSV has no playable lines.", this);
            yield break;
        }

        isTransitioning = true;
        isDialoguePlaying = true;
        DialogueStarted?.Invoke();
        if (advanceInputLayer != null) advanceInputLayer.SetActive(true);
        HideGameplayHud();
        currentLine = -1;
        lineAutoAdvanceAt = float.PositiveInfinity;
        PauseGameplay();
        yield return AnimateLetterbox(true);
        isTransitioning = false;
        AdvanceDialogue();
    }

    /// <summary>Persistent scene event target for Btn_ContinueDialogue.</summary>
    public void AdvanceDialogue()
    {
        if (!isDialoguePlaying || isTransitioning) return;
        if (++currentLine >= dialogueLines.Count)
        {
            lineAutoAdvanceAt = float.PositiveInfinity;
            StartCoroutine(EndDialogue());
            return;
        }
        ShowLine(dialogueLines[currentLine]);
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
        if (label != null) label.text = line.Content;

        activeSpeaker = line.FollowTarget;
        if (line.FollowTarget != null) activeWorldOffset = worldOffset;
        else if (line.Speaker == SpeakerKind.Character) { activeSpeaker = character; activeWorldOffset = characterOffset; }
        else if (line.Speaker == SpeakerKind.Soldier) { activeSpeaker = soldier; activeWorldOffset = soldierOffset; }

        bool systemPresentation = line.Speaker == SpeakerKind.System && line.FollowTarget == null;
        Vector2 target = systemPresentation ? systemPosition : GetWorldBubblePosition(activeSpeaker, activeWorldOffset);
        Vector2 start = systemPresentation ? target + Vector2.up * (Screen.height * .55f) : target + Vector2.up * 36f;
        bubbleAnimation = StartCoroutine(AnimateBubble(start, target));
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
        lineAutoAdvanceAt = float.PositiveInfinity;
        isDialoguePlaying = false;
        isTransitioning = false;
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
                dialogueLines.Add(new DialogueLine(SpeakerKind.Character, line.content, line.speaker));
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
            dialogueLines.Add(new DialogueLine(kind, rows[i][1].Trim()));
        }
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

    private void PositionWorldBubble(RectTransform bubble, Transform speaker, Vector2 offset) => bubble.anchoredPosition = GetWorldBubblePosition(speaker, offset);

    private Vector2 GetWorldBubblePosition(Transform speaker, Vector2 offset)
    {
        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null || speaker == null || bubbleContainer == null) return Vector2.zero;
        Vector2 screen = cameraToUse.WorldToScreenPoint(speaker.position + (Vector3)offset);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(bubbleContainer, screen, null, out Vector2 local);
        return local;
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
        if (!isDialoguePlaying) return;
        RestoreGameplayHud();
        ResumeGameplay();
    }
}
