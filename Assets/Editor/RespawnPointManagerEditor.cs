using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(RespawnPointManager))]
public sealed class RespawnPointManagerEditor : Editor
{
    private static readonly Color SequenceColor = new(.1f, .85f, 1f, 1f);
    private static readonly Color StartColor = new(.2f, 1f, .35f, 1f);

    private SerializedProperty playerProperty;
    private SerializedProperty pointsProperty;
    private SerializedProperty startingIndexProperty;
    private ReorderableList pointList;

    private void OnEnable()
    {
        playerProperty = serializedObject.FindProperty("player");
        pointsProperty = serializedObject.FindProperty("respawnPoints");
        startingIndexProperty = serializedObject.FindProperty("startingPointIndex");

        pointList = new ReorderableList(serializedObject, pointsProperty, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Respawn Sequence (drag to reorder)"),
            elementHeight = EditorGUIUtility.singleLineHeight + 4f,
            drawElementCallback = DrawSequenceElement,
            onReorderCallback = _ => SceneView.RepaintAll(),
            onChangedCallback = _ => SceneView.RepaintAll()
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(playerProperty);
        EditorGUILayout.Space(3f);
        pointList.DoLayoutList();
        EditorGUILayout.PropertyField(startingIndexProperty, new GUIContent("Fallback Starting Index"));

        DrawValidationMessages();
        EditorGUILayout.HelpBox(
            "The list order is the gameplay order. Drag entries to reorder them; the Scene view updates immediately.",
            MessageType.Info);

        if (serializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }
    }

    private void DrawSequenceElement(Rect rect, int index, bool active, bool focused)
    {
        rect.y += 2f;
        rect.height = EditorGUIUtility.singleLineHeight;
        Rect indexRect = new(rect.x, rect.y, 34f, rect.height);
        Rect pointRect = new(rect.x + 38f, rect.y, rect.width - 38f, rect.height);
        EditorGUI.LabelField(indexRect, $"{index + 1:00}");
        EditorGUI.PropertyField(pointRect, pointsProperty.GetArrayElementAtIndex(index), GUIContent.none);
    }

    private void DrawValidationMessages()
    {
        HashSet<Object> seen = new();
        int activeOnStartCount = 0;
        for (int i = 0; i < pointsProperty.arraySize; i++)
        {
            Object point = pointsProperty.GetArrayElementAtIndex(i).objectReferenceValue;
            if (point == null)
            {
                EditorGUILayout.HelpBox($"Sequence entry {i + 1:00} is empty.", MessageType.Warning);
                continue;
            }

            if (!seen.Add(point))
                EditorGUILayout.HelpBox($"Sequence entry {i + 1:00} is duplicated.", MessageType.Error);

            if (point is RespawnPoint respawnPoint && respawnPoint.ActiveOnLevelStart)
                activeOnStartCount++;
        }

        if (activeOnStartCount > 1)
            EditorGUILayout.HelpBox(
                "More than one point is marked Active On Level Start. Only the first one in this sequence will be used.",
                MessageType.Warning);
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawRespawnSequence(RespawnPointManager manager, GizmoType gizmoType)
    {
        if (manager == null) return;

        SerializedObject serializedManager = new(manager);
        SerializedProperty points = serializedManager.FindProperty("respawnPoints");
        SerializedProperty startingIndex = serializedManager.FindProperty("startingPointIndex");
        if (points == null || points.arraySize == 0) return;
        int effectiveStartingIndex = FindEffectiveStartingIndex(points, startingIndex.intValue);

        CompareFunction previousZTest = Handles.zTest;
        Color previousColor = Handles.color;
        Handles.zTest = CompareFunction.Always;

        RespawnPoint previousPoint = null;
        for (int i = 0; i < points.arraySize; i++)
        {
            RespawnPoint point = points.GetArrayElementAtIndex(i).objectReferenceValue as RespawnPoint;
            if (point == null) continue;

            bool isStartingPoint = i == effectiveStartingIndex;
            Color pointColor = isStartingPoint ? StartColor : SequenceColor;
            float handleSize = HandleUtility.GetHandleSize(point.transform.position);
            float radius = handleSize * .09f;

            Handles.color = new Color(pointColor.r, pointColor.g, pointColor.b, .25f);
            Handles.DrawSolidDisc(point.transform.position, Vector3.forward, radius);
            Handles.color = pointColor;
            Handles.DrawWireDisc(point.transform.position, Vector3.forward, radius);
            Handles.Label(
                point.transform.position + Vector3.up * radius * 1.45f,
                $"{i + 1:00}  {point.PointId}{(isStartingPoint ? "  [START]" : string.Empty)}");

            if (previousPoint != null)
                DrawSequenceArrow(previousPoint.transform.position, point.transform.position, handleSize);
            previousPoint = point;
        }

        Handles.color = previousColor;
        Handles.zTest = previousZTest;
    }

    private static int FindEffectiveStartingIndex(SerializedProperty points, int fallbackIndex)
    {
        for (int i = 0; i < points.arraySize; i++)
        {
            RespawnPoint point = points.GetArrayElementAtIndex(i).objectReferenceValue as RespawnPoint;
            if (point != null && point.ActiveOnLevelStart) return i;
        }

        return Mathf.Clamp(fallbackIndex, 0, points.arraySize - 1);
    }

    private static void DrawSequenceArrow(Vector3 from, Vector3 to, float handleSize)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude <= .0001f) return;

        Vector3 direction = delta.normalized;
        Vector3 perpendicular = new(-direction.y, direction.x, 0f);
        Vector3 tip = Vector3.Lerp(from, to, .72f);
        float arrowSize = Mathf.Min(handleSize * .14f, delta.magnitude * .18f);
        Vector3 arrowBase = tip - direction * arrowSize;

        Handles.color = SequenceColor;
        Handles.DrawAAPolyLine(3f, from, to);
        Handles.DrawAAPolyLine(3f, tip, arrowBase + perpendicular * arrowSize * .55f);
        Handles.DrawAAPolyLine(3f, tip, arrowBase - perpendicular * arrowSize * .55f);
    }
}
