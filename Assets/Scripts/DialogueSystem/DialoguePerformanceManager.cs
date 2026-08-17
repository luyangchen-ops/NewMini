using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum PresentationSessionKind
{
    Dialogue,
    Performance
}

/// <summary>Immutable information supplied with every dialogue/performance lifecycle event.</summary>
public readonly struct PresentationSessionInfo
{
    internal PresentationSessionInfo(int id, PresentationSessionKind kind, UnityEngine.Object owner, string name)
    {
        Id = id;
        Kind = kind;
        Owner = owner;
        Name = name;
    }

    public int Id { get; }
    public PresentationSessionKind Kind { get; }
    public UnityEngine.Object Owner { get; }
    public string Name { get; }
}

/// <summary>A lifecycle token returned by <see cref="DialoguePerformanceManager"/>.</summary>
public sealed class PresentationSession : IDisposable
{
    internal PresentationSession(PresentationSessionInfo info) => Info = info;

    public PresentationSessionInfo Info { get; }
    public bool IsActive { get; private set; } = true;

    internal void MarkEnded() => IsActive = false;

    public void Dispose()
    {
        if (!IsActive) return;
        IsActive = false;
        DialoguePerformanceManager.EndSession(Info.Id);
    }
}

/// <summary>
/// Central lifecycle registry for dialogue and authored performances. Sessions may be nested;
/// for example, a dialogue can remain active while an interruption performance plays.
/// </summary>
public static class DialoguePerformanceManager
{
    private static readonly Dictionary<int, PresentationSession> ActiveSessions = new();
    private static int nextSessionId;
    private static int activeDialogueCount;
    private static int activePerformanceCount;

    public static bool IsDialogueActive => activeDialogueCount > 0;
    public static bool IsPerformanceActive => activePerformanceCount > 0;
    public static bool IsAnyPresentationActive => IsDialogueActive || IsPerformanceActive;
    public static int DialogueSessionCount => activeDialogueCount;
    public static int PerformanceSessionCount => activePerformanceCount;

    public static event Action<PresentationSessionInfo> DialogueStarted;
    public static event Action<PresentationSessionInfo> DialogueEnded;
    public static event Action<PresentationSessionInfo> PerformanceStarted;
    public static event Action<PresentationSessionInfo> PerformanceEnded;
    public static event Action<PresentationSessionInfo> PresentationStarted;
    public static event Action<PresentationSessionInfo> PresentationEnded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        foreach (PresentationSession session in ActiveSessions.Values) session.MarkEnded();
        ActiveSessions.Clear();
        nextSessionId = 0;
        activeDialogueCount = 0;
        activePerformanceCount = 0;
        DialogueStarted = null;
        DialogueEnded = null;
        PerformanceStarted = null;
        PerformanceEnded = null;
        PresentationStarted = null;
        PresentationEnded = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() => SceneManager.activeSceneChanged += HandleActiveSceneChanged;

    public static PresentationSession BeginDialogue(UnityEngine.Object owner, string sessionName = null) =>
        BeginSession(PresentationSessionKind.Dialogue, owner, sessionName);

    public static PresentationSession BeginPerformance(UnityEngine.Object owner, string sessionName = null) =>
        BeginSession(PresentationSessionKind.Performance, owner, sessionName);

    internal static void EndSession(int id)
    {
        if (!ActiveSessions.Remove(id, out PresentationSession session)) return;
        session.MarkEnded();
        PresentationSessionInfo info = session.Info;

        if (info.Kind == PresentationSessionKind.Dialogue)
        {
            activeDialogueCount = Mathf.Max(0, activeDialogueCount - 1);
            DialogueEnded?.Invoke(info);
        }
        else
        {
            activePerformanceCount = Mathf.Max(0, activePerformanceCount - 1);
            PerformanceEnded?.Invoke(info);
        }

        PresentationEnded?.Invoke(info);
    }

    private static PresentationSession BeginSession(
        PresentationSessionKind kind,
        UnityEngine.Object owner,
        string sessionName)
    {
        int id = ++nextSessionId;
        string resolvedName = string.IsNullOrWhiteSpace(sessionName)
            ? owner != null ? owner.name : kind.ToString()
            : sessionName;
        PresentationSessionInfo info = new(id, kind, owner, resolvedName);
        PresentationSession session = new(info);
        ActiveSessions.Add(id, session);

        if (kind == PresentationSessionKind.Dialogue)
        {
            activeDialogueCount++;
            DialogueStarted?.Invoke(info);
        }
        else
        {
            activePerformanceCount++;
            PerformanceStarted?.Invoke(info);
        }

        PresentationStarted?.Invoke(info);
        return session;
    }

    private static void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        if (ActiveSessions.Count == 0) return;
        int[] sessionIds = new int[ActiveSessions.Count];
        ActiveSessions.Keys.CopyTo(sessionIds, 0);
        Array.Sort(sessionIds);
        for (int i = sessionIds.Length - 1; i >= 0; i--) EndSession(sessionIds[i]);
    }
}
