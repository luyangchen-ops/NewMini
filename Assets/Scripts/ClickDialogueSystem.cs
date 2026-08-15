using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Displays one authored dialogue line at a time. The visual template lives in
/// the scene; only its copies are created at runtime as the active chat bubble.
/// </summary>
public sealed class ClickDialogueSystem : MonoBehaviour
{
    [Serializable]
    public sealed class DialogueLine
    {
        [Tooltip("The character this line follows.")]
        public Transform speaker;
        [TextArea(2, 4)] public string content;
    }

    [Header("Scene References")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private RectTransform bubbleContainer;
    [SerializeField] private RectTransform bubbleTemplate;

    [Header("Dialogue")]
    [SerializeField] private DialogueLine[] lines;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.35f, 0f);
    [SerializeField] private bool playFirstLineOnStart = true;
    [SerializeField] private GameObject topLetterbox;
    [SerializeField] private GameObject bottomLetterbox;
    [SerializeField] private Behaviour[] controlsToPause;
    [SerializeField] private Behaviour[] enemiesToPause;

    private int currentLine = -1;
    private RectTransform activeBubble;
    private Transform activeSpeaker;
    private bool isDialoguePlaying;

    private void Start()
    {
        if (bubbleTemplate != null)
        {
            bubbleTemplate.gameObject.SetActive(false);
        }

        if (playFirstLineOnStart)
        {
            SetDialogueState(true);
            AdvanceDialogue();
        }
    }

    private void Update()
    {
        if (isDialoguePlaying && Mouse.current?.leftButton.wasPressedThisFrame == true)
        {
            AdvanceDialogue();
        }
    }

    private void LateUpdate()
    {
        if (activeBubble != null && activeSpeaker != null)
        {
            PositionBubble(activeBubble, activeSpeaker);
        }
    }

    /// <summary>Persistent target for Btn_AdvanceDialogue's On Click event.</summary>
    public void AdvanceDialogue()
    {
        if (lines == null || lines.Length == 0 || bubbleTemplate == null || bubbleContainer == null)
        {
            return;
        }

        if (currentLine >= lines.Length - 1)
        {
            EndDialogue();
            return;
        }

        currentLine++;
        DialogueLine line = lines[currentLine];
        if (line.speaker == null)
        {
            return;
        }

        if (activeBubble != null)
        {
            Destroy(activeBubble.gameObject);
        }

        activeBubble = Instantiate(bubbleTemplate, bubbleContainer);
        activeBubble.name = "Panel_ActiveDialogueBubble";
        activeBubble.gameObject.SetActive(true);
        activeSpeaker = line.speaker;

        Text contentLabel = activeBubble.GetComponentInChildren<Text>(true);
        if (contentLabel != null)
        {
            contentLabel.text = line.content;
        }

        PositionBubble(activeBubble, activeSpeaker);
    }

    private void EndDialogue()
    {
        if (activeBubble != null)
        {
            Destroy(activeBubble.gameObject);
            activeBubble = null;
        }

        activeSpeaker = null;
        SetDialogueState(false);
    }

    private void SetDialogueState(bool active)
    {
        isDialoguePlaying = active;

        if (topLetterbox != null)
        {
            topLetterbox.SetActive(active);
        }

        if (bottomLetterbox != null)
        {
            bottomLetterbox.SetActive(active);
        }

        SetBehavioursEnabled(controlsToPause, !active);
        SetBehavioursEnabled(enemiesToPause, !active);
    }

    private static void SetBehavioursEnabled(Behaviour[] behaviours, bool enabled)
    {
        if (behaviours == null)
        {
            return;
        }

        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }
    }

    private void PositionBubble(RectTransform bubble, Transform speaker)
    {
        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null)
        {
            return;
        }

        Vector2 screenPosition = cameraToUse.WorldToScreenPoint(speaker.position + worldOffset);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bubbleContainer, screenPosition, null, out Vector2 localPosition);
        bubble.anchoredPosition = localPosition;
    }
}
