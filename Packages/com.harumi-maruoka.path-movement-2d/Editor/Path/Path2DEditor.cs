using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.PathMovement2D.Editor
{
    [CustomEditor(typeof(Path2D))]
    public sealed class Path2DEditor : UnityEditor.Editor
    {
        private readonly HashSet<int> selection = new();

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

        private void OnSceneGUI()
        {
            Path2D path = (Path2D)target;
            DrawPath(path);
            for (int i = 0; i < path.PointCount; i++) DrawPoint(path, i);
            HandleKeyboard(path);
        }

        private void DrawPath(Path2D path)
        {
            for (int segment = 0; segment < path.SegmentCount; segment++)
            {
                PathPoint2D a = path.GetPoint(segment);
                int next = (segment + 1) % path.PointCount;
                PathPoint2D b = path.GetPoint(next);
                Handles.color = a.SegmentType == PathSegmentType.Linear ? new Color(0.25f, 0.85f, 1f) : new Color(1f, 0.65f, 0.2f);
                if (a.SegmentType == PathSegmentType.Linear) Handles.DrawAAPolyLine(3f, path.ToWorld(a.Position), path.ToWorld(b.Position));
                else
                {
                    Vector2 outTangent = a.TangentMode == PathTangentMode.Auto ? EstimateAuto(path, segment, false) : a.OutTangent;
                    Vector2 inTangent = b.TangentMode == PathTangentMode.Auto ? EstimateAuto(path, next, true) : b.InTangent;
                    Handles.DrawBezier(path.ToWorld(a.Position), path.ToWorld(b.Position), path.ToWorld(a.Position + outTangent), path.ToWorld(b.Position + inTangent), Handles.color, null, 3f);
                }
                Vector2 mid = path.EvaluateSegmentPosition(segment, 0.5f);
                Vector2 tangent = path.EvaluateSegmentTangent(segment, 0.5f).normalized;
                Vector2 wm = path.ToWorld(mid), wt = path.Space == PathSpace.Local ? path.transform.TransformVector(tangent).normalized : tangent;
                Handles.ConeHandleCap(0, wm, Quaternion.FromToRotation(Vector3.forward, wt), HandleUtility.GetHandleSize(wm) * 0.08f, EventType.Repaint);
            }
            if (path.PointCount > 0)
            {
                Handles.Label(path.ToWorld(path.GetPoint(0).Position), "Start");
                if (!path.Closed) Handles.Label(path.ToWorld(path.GetPoint(path.PointCount - 1).Position), "End");
            }
        }

        private void DrawPoint(Path2D path, int index)
        {
            PathPoint2D point = path.GetPoint(index);
            Vector2 world = path.ToWorld(point.Position);
            float size = HandleUtility.GetHandleSize(world) * 0.09f;
            Handles.color = selection.Contains(index) ? Color.yellow : Color.white;
            if (Handles.Button(world, Quaternion.identity, size, size, Handles.DotHandleCap))
            {
                if (!Event.current.control && !Event.current.command) selection.Clear();
                if (!selection.Add(index)) selection.Remove(index);
                Repaint();
            }
            Handles.Label(world + Vector2.up * size, index.ToString());
            if (selection.Contains(index))
            {
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(world, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(path, "Move Path Point");
                    Vector2 delta = path.ToPathSpace(moved) - point.Position;
                    foreach (int selected in selection) path.GetPoint(selected).Position += delta;
                    path.Rebuild(); EditorUtility.SetDirty(path);
                }
                if (point.TangentMode != PathTangentMode.Auto) DrawTangents(path, point, world);
            }
        }

        private static void DrawTangents(Path2D path, PathPoint2D point, Vector2 world)
        {
            DrawTangent(path, point, world, true);
            DrawTangent(path, point, world, false);
        }

        private static void DrawTangent(Path2D path, PathPoint2D point, Vector2 world, bool incoming)
        {
            Vector2 offset = incoming ? point.InTangent : point.OutTangent;
            Vector2 handle = path.ToWorld(point.Position + offset);
            Handles.color = new Color(0.6f, 0.8f, 1f);
            Handles.DrawLine(world, handle);
            EditorGUI.BeginChangeCheck();
            Vector2 moved = Handles.FreeMoveHandle(handle, HandleUtility.GetHandleSize(handle) * 0.07f, Vector3.zero, Handles.CircleHandleCap);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(path, "Move Path Tangent");
            Vector2 newOffset = path.ToPathSpace(moved) - point.Position;
            if (incoming) point.InTangent = newOffset; else point.OutTangent = newOffset;
            if (point.TangentMode == PathTangentMode.Mirrored)
            { if (incoming) point.OutTangent = -newOffset; else point.InTangent = -newOffset; }
            else if (point.TangentMode == PathTangentMode.Aligned)
            {
                if (incoming) point.OutTangent = -newOffset.normalized * point.OutTangent.magnitude;
                else point.InTangent = -newOffset.normalized * point.InTangent.magnitude;
            }
            path.Rebuild(); EditorUtility.SetDirty(path);
        }

        private void HandleKeyboard(Path2D path)
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && path.PointCount - selection.Count >= 2)
            {
                Undo.RecordObject(path, "Delete Path Points");
                var indices = new List<int>(selection); indices.Sort((a, b) => b.CompareTo(a));
                foreach (int index in indices) path.RemovePointAt(index);
                selection.Clear(); EditorUtility.SetDirty(path); e.Use();
            }
            if (e.type == EventType.MouseDown && e.shift && e.button == 0 && !e.alt)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                float z = path.Space == PathSpace.Local ? path.transform.position.z : 0f;
                float distance = Mathf.Abs(ray.direction.z) > 0.0001f ? (z - ray.origin.z) / ray.direction.z : 0f;
                Vector3 world = ray.GetPoint(distance);
                Undo.RecordObject(path, "Add Path Point"); path.AddPoint(path.ToPathSpace(world)); EditorUtility.SetDirty(path); e.Use();
            }
        }

        private static Vector2 EstimateAuto(Path2D path, int index, bool incoming)
        {
            int previous = path.Closed ? (index - 1 + path.PointCount) % path.PointCount : Mathf.Max(0, index - 1);
            int next = path.Closed ? (index + 1) % path.PointCount : Mathf.Min(path.PointCount - 1, index + 1);
            Vector2 direction = (path.GetPoint(next).Position - path.GetPoint(previous).Position).normalized;
            float distance = Vector2.Distance(path.GetPoint(index).Position, path.GetPoint(incoming ? previous : next).Position) / 3f;
            return direction * distance * (incoming ? -1f : 1f);
        }
    }
}
