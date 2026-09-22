using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.PathMovement2D.Editor
{
    [CustomEditor(typeof(Path2D))]
    public sealed class Path2DEditor : UnityEditor.Editor
    {
        private readonly HashSet<int> selection = new();

        private void OnEnable() => Undo.undoRedoPerformed += OnUndo;
        private void OnDisable() => Undo.undoRedoPerformed -= OnUndo;
        private void OnUndo()
        {
            if (target == null) return;
            ((Path2D)target).Rebuild();
            selection.Clear();
            Repaint(); SceneView.RepaintAll();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("space"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("closed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("arcLengthTolerance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxSubdivisionDepth"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Points", EditorStyles.boldLabel);
            SerializedProperty points = serializedObject.FindProperty("points");
            for (int i = 0; i < points.arraySize; i++)
            {
                SerializedProperty point = points.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Toggle(selection.Contains(i), $"Point {i}", "Button")) selection.Add(i); else selection.Remove(i);
                if (GUILayout.Button("Delete", GUILayout.Width(55f)) && points.arraySize > 2)
                { points.DeleteArrayElementAtIndex(i); selection.Remove(i); break; }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(point.FindPropertyRelative("position"));
                EditorGUILayout.PropertyField(point.FindPropertyRelative("tangentMode"));
                EditorGUILayout.PropertyField(point.FindPropertyRelative("segmentType"), new GUIContent("Segment To Next"));
                if ((PathTangentMode)point.FindPropertyRelative("tangentMode").enumValueIndex != PathTangentMode.Auto)
                {
                    EditorGUILayout.PropertyField(point.FindPropertyRelative("inTangent"));
                    EditorGUILayout.PropertyField(point.FindPropertyRelative("outTangent"));
                }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("Add Point"))
            {
                Vector2 position = points.arraySize == 0 ? Vector2.zero : points.GetArrayElementAtIndex(points.arraySize - 1).FindPropertyRelative("position").vector2Value + Vector2.right;
                points.InsertArrayElementAtIndex(points.arraySize);
                SerializedProperty p = points.GetArrayElementAtIndex(points.arraySize - 1);
                p.FindPropertyRelative("position").vector2Value = position;
                p.FindPropertyRelative("inTangent").vector2Value = Vector2.left;
                p.FindPropertyRelative("outTangent").vector2Value = Vector2.right;
            }
            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed) { ((Path2D)target).Rebuild(); SceneView.RepaintAll(); }
        }

        private PathSceneHandles sceneHandles;
        private void OnSceneGUI()
        {
            sceneHandles ??= new PathSceneHandles(selection);
            sceneHandles.Draw(new PathEditingContext((Path2D)target));
        }
    }
}
