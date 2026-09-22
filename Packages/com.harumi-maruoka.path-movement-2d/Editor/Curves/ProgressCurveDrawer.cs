using UnityEditor;
using UnityEngine;

namespace Game.PathMovement2D.Editor
{
    [CustomPropertyDrawer(typeof(ProgressCurve))]
    public sealed class ProgressCurveDrawer : PropertyDrawer
    {
        private const float GraphHeight = 180f;
        private static readonly string[] DetailFields = { "time", "value", "tangentMode", "inHandle", "outHandle" };
        private static string selectedPath;
        private static int selectedIndex = -1;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float height = GraphHeight + EditorGUIUtility.singleLineHeight * 3f + spacing * 3f;
            SerializedProperty keys = property.FindPropertyRelative("keys");
            if (selectedPath == property.propertyPath && selectedIndex >= 0 && selectedIndex < keys.arraySize)
            {
                SerializedProperty key = keys.GetArrayElementAtIndex(selectedIndex);
                foreach (string field in DetailFields)
                    height += spacing + EditorGUI.GetPropertyHeight(key.FindPropertyRelative(field), true);
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect row = new(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (!property.isExpanded) { EditorGUI.EndProperty(); return; }
            row.y += line + spacing;
            SerializedProperty constraint = property.FindPropertyRelative("constraint");
            SerializedProperty preset = property.FindPropertyRelative("preset");
            Rect left = new(row.x, row.y, row.width * 0.5f - 2f, line);
            Rect right = new(left.xMax + 4f, row.y, row.width * 0.5f - 2f, line);
            EditorGUI.PropertyField(left, constraint, GUIContent.none);
            EditorGUI.BeginChangeCheck();
            ProgressCurvePreset choice = (ProgressCurvePreset)EditorGUI.EnumPopup(right, (ProgressCurvePreset)preset.enumValueIndex);
            if (EditorGUI.EndChangeCheck()) ApplyPreset(property, choice);

            Rect graph = new(position.x, row.yMax + spacing, position.width, GraphHeight);
            DrawGraph(graph, property);
            Rect controls = new(position.x, graph.yMax + spacing, position.width, line);
            if (GUI.Button(new Rect(controls.x, controls.y, 80f, line), "Add Key")) AddKey(property, 0.5f, 0.5f);
            if (GUI.Button(new Rect(controls.x + 84f, controls.y, 90f, line), "Delete Key")) DeleteSelected(property);

            SerializedProperty keys = property.FindPropertyRelative("keys");
            if (selectedPath == property.propertyPath && selectedIndex >= 0 && selectedIndex < keys.arraySize)
            {
                SerializedProperty key = keys.GetArrayElementAtIndex(selectedIndex);
                Rect detail = new(position.x, controls.yMax + spacing, position.width, line);
                foreach (string field in DetailFields)
                {
                    SerializedProperty child = key.FindPropertyRelative(field);
                    detail.height = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(detail, child, true);
                    detail.y += detail.height + spacing;
                }
                preset.enumValueIndex = (int)ProgressCurvePreset.Custom;
            }
            EditorGUI.EndProperty();
        }

        private static void DrawGraph(Rect rect, SerializedProperty property)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
            Handles.BeginGUI();
            for (int i = 0; i <= 10; i++)
            {
                float x = Mathf.Lerp(rect.x, rect.xMax, i / 10f), y = Mathf.Lerp(rect.yMax, rect.y, i / 10f);
                Handles.color = i == 0 || i == 10 ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.25f, 0.25f, 0.25f);
                Handles.DrawLine(new Vector2(x, rect.y), new Vector2(x, rect.yMax));
                Handles.DrawLine(new Vector2(rect.x, y), new Vector2(rect.xMax, y));
            }
            SerializedProperty keys = property.FindPropertyRelative("keys");
            Handles.color = new Color(0.3f, 0.85f, 1f);
            for (int i = 0; i < keys.arraySize - 1; i++)
            {
                SerializedProperty a = keys.GetArrayElementAtIndex(i), b = keys.GetArrayElementAtIndex(i + 1);
                Vector2 p0 = KeyPoint(a), p1 = p0 + a.FindPropertyRelative("outHandle").vector2Value;
                Vector2 p3 = KeyPoint(b), p2 = p3 + b.FindPropertyRelative("inHandle").vector2Value;
                Handles.DrawBezier(ToGui(rect, p0), ToGui(rect, p3), ToGui(rect, p1), ToGui(rect, p2), Handles.color, null, 2f);
            }
            Handles.EndGUI();

            Event e = Event.current;
            for (int i = 0; i < keys.arraySize; i++)
            {
                SerializedProperty key = keys.GetArrayElementAtIndex(i);
                Vector2 kp = ToGui(rect, KeyPoint(key));
                Rect hit = new(kp.x - 5f, kp.y - 5f, 10f, 10f);
                EditorGUI.DrawRect(hit, selectedPath == property.propertyPath && selectedIndex == i ? Color.yellow : Color.white);
                if (e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition))
                { selectedPath = property.propertyPath; selectedIndex = i; e.Use(); }
                if (selectedPath == property.propertyPath && selectedIndex == i) DrawHandles(rect, key, i, keys.arraySize, property);
            }
            if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2 && rect.Contains(e.mousePosition))
            { Vector2 value = FromGui(rect, e.mousePosition); AddKey(property, value.x, value.y); e.Use(); }
        }

        private static void DrawHandles(Rect rect, SerializedProperty key, int index, int count, SerializedProperty curve)
        {
            Vector2 keyPoint = KeyPoint(key);
            DrawHandle(rect, key, keyPoint, "inHandle", true, curve);
            DrawHandle(rect, key, keyPoint, "outHandle", false, curve);
            Event e = Event.current;
            Rect drag = new(ToGui(rect, keyPoint) - Vector2.one * 7f, Vector2.one * 14f);
            if (e.type == EventType.MouseDrag && e.button == 0 && drag.Contains(e.mousePosition - e.delta))
            {
                Vector2 value = FromGui(rect, e.mousePosition);
                if (index == 0) value = Vector2.zero;
                else if (index == count - 1) value = Vector2.one;
                key.FindPropertyRelative("time").floatValue = Mathf.Clamp01(value.x);
                key.FindPropertyRelative("value").floatValue = (ProgressCurveConstraint)curve.FindPropertyRelative("constraint").enumValueIndex == ProgressCurveConstraint.Monotonic ? Mathf.Clamp01(value.y) : value.y;
                curve.FindPropertyRelative("preset").enumValueIndex = (int)ProgressCurvePreset.Custom;
                e.Use();
            }
        }

        private static void DrawHandle(Rect rect, SerializedProperty key, Vector2 kp, string name, bool incoming, SerializedProperty curve)
        {
            SerializedProperty handleProperty = key.FindPropertyRelative(name);
            Vector2 hp = kp + handleProperty.vector2Value, gui = ToGui(rect, hp), keyGui = ToGui(rect, kp);
            Handles.BeginGUI(); Handles.color = Color.gray; Handles.DrawLine(keyGui, gui); Handles.EndGUI();
            Rect hit = new(gui - Vector2.one * 4f, Vector2.one * 8f); EditorGUI.DrawRect(hit, Color.cyan);
            Event e = Event.current;
            if (e.type == EventType.MouseDrag && e.button == 0 && hit.Contains(e.mousePosition - e.delta))
            {
                Vector2 offset = FromGui(rect, e.mousePosition) - kp;
                offset.x = incoming ? Mathf.Min(0f, offset.x) : Mathf.Max(0f, offset.x);
                handleProperty.vector2Value = offset;
                ProgressTangentMode mode = (ProgressTangentMode)key.FindPropertyRelative("tangentMode").enumValueIndex;
                string otherName = incoming ? "outHandle" : "inHandle";
                if (mode == ProgressTangentMode.Mirrored) key.FindPropertyRelative(otherName).vector2Value = -offset;
                else if (mode == ProgressTangentMode.Aligned)
                {
                    Vector2 other = key.FindPropertyRelative(otherName).vector2Value;
                    key.FindPropertyRelative(otherName).vector2Value = -offset.normalized * other.magnitude;
                }
                curve.FindPropertyRelative("preset").enumValueIndex = (int)ProgressCurvePreset.Custom; e.Use();
            }
        }

        private static Vector2 KeyPoint(SerializedProperty key) => new(key.FindPropertyRelative("time").floatValue, key.FindPropertyRelative("value").floatValue);
        private static Vector2 ToGui(Rect r, Vector2 p) => new(Mathf.Lerp(r.x, r.xMax, p.x), Mathf.Lerp(r.yMax, r.y, p.y));
        private static Vector2 FromGui(Rect r, Vector2 p) => new(Mathf.InverseLerp(r.x, r.xMax, p.x), Mathf.InverseLerp(r.yMax, r.y, p.y));

        private static void AddKey(SerializedProperty property, float time, float value)
        {
            SerializedProperty keys = property.FindPropertyRelative("keys");
            int index = keys.arraySize; keys.InsertArrayElementAtIndex(index);
            SerializedProperty key = keys.GetArrayElementAtIndex(index);
            key.FindPropertyRelative("id").stringValue = System.Guid.NewGuid().ToString("N");
            key.FindPropertyRelative("time").floatValue = Mathf.Clamp01(time);
            key.FindPropertyRelative("value").floatValue = value;
            key.FindPropertyRelative("inHandle").vector2Value = new Vector2(-0.08f, -0.08f);
            key.FindPropertyRelative("outHandle").vector2Value = new Vector2(0.08f, 0.08f);
            property.FindPropertyRelative("preset").enumValueIndex = (int)ProgressCurvePreset.Custom;
            selectedPath = property.propertyPath; selectedIndex = index;
        }

        private static void DeleteSelected(SerializedProperty property)
        {
            SerializedProperty keys = property.FindPropertyRelative("keys");
            if (selectedPath != property.propertyPath || selectedIndex <= 0 || selectedIndex >= keys.arraySize - 1) return;
            keys.DeleteArrayElementAtIndex(selectedIndex); selectedIndex = -1;
            property.FindPropertyRelative("preset").enumValueIndex = (int)ProgressCurvePreset.Custom;
        }

        private static void ApplyPreset(SerializedProperty property, ProgressCurvePreset preset)
        {
            if (preset == ProgressCurvePreset.Custom) { property.FindPropertyRelative("preset").enumValueIndex = (int)preset; return; }
            SerializedProperty keys = property.FindPropertyRelative("keys"); keys.arraySize = 2;
            SetKey(keys.GetArrayElementAtIndex(0), 0f, 0f); SetKey(keys.GetArrayElementAtIndex(1), 1f, 1f);
            Vector2 outHandle = new(1f / 3f, 1f / 3f), inHandle = new(-1f / 3f, -1f / 3f);
            switch (preset)
            {
                case ProgressCurvePreset.EaseIn: outHandle = new(0.42f, 0f); inHandle = new(-0.42f, -1f); break;
                case ProgressCurvePreset.EaseOut: outHandle = new(0.42f, 1f); inHandle = new(-0.42f, 0f); break;
                case ProgressCurvePreset.EaseInOut: outHandle = new(0.42f, 0f); inHandle = new(-0.42f, 0f); break;
                case ProgressCurvePreset.Smooth: outHandle = new(1f / 3f, 0f); inHandle = new(-1f / 3f, 0f); break;
            }
            keys.GetArrayElementAtIndex(0).FindPropertyRelative("outHandle").vector2Value = outHandle;
            keys.GetArrayElementAtIndex(1).FindPropertyRelative("inHandle").vector2Value = inHandle;
            property.FindPropertyRelative("preset").enumValueIndex = (int)preset;
        }

        private static void SetKey(SerializedProperty key, float time, float value)
        {
            key.FindPropertyRelative("id").stringValue = System.Guid.NewGuid().ToString("N");
            key.FindPropertyRelative("time").floatValue = time; key.FindPropertyRelative("value").floatValue = value;
            key.FindPropertyRelative("inHandle").vector2Value = Vector2.zero; key.FindPropertyRelative("outHandle").vector2Value = Vector2.zero;
        }
    }
}
