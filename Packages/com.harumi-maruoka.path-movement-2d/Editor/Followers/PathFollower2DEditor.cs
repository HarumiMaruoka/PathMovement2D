using UnityEditor;
using UnityEngine;

namespace Game.PathMovement2D.Editor
{
    [CustomEditor(typeof(PathFollower2D))]
    public sealed class PathFollower2DEditor : UnityEditor.Editor
    {
        private float previewTime;
        private bool playingPreview;
        private bool showTimeMarkers = true;
        private int markerCount = 20;
        private double lastEditorTime;

        private void OnEnable()
        {
            lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
            playingPreview = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("path"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("target"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("timingMode"));
            PathTimingMode timing = (PathTimingMode)serializedObject.FindProperty("timingMode").enumValueIndex;
            EditorGUILayout.PropertyField(serializedObject.FindProperty(timing == PathTimingMode.Duration ? "duration" : "speed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("progressCurve"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loopMode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("initialDirection"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("updateMode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("startOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rotateAlongPath"));
            if (serializedObject.FindProperty("rotateAlongPath").boolValue)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationOffsetDegrees"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playOnAwake"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useUnscaledTime"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Preview", EditorStyles.boldLabel);
            previewTime = EditorGUILayout.Slider("Preview Time", previewTime, 0f, 1f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(playingPreview ? "Pause" : "Play")) playingPreview = !playingPreview;
            if (GUILayout.Button("Stop")) { playingPreview = false; previewTime = 0f; }
            EditorGUILayout.EndHorizontal();
            showTimeMarkers = EditorGUILayout.Toggle("Equal Time Markers", showTimeMarkers);
            if (showTimeMarkers) markerCount = EditorGUILayout.IntSlider("Marker Count", markerCount, 2, 64);
            EditorGUILayout.HelpBox("Preview is drawn as a Scene View ghost and does not modify the target Transform.", MessageType.Info);
            SceneView.RepaintAll();
        }

        private void EditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playingPreview)
            {
                PathFollower2D follower = (PathFollower2D)target;
                float duration = Mathf.Max(0.0001f, follower.EffectiveDuration);
                previewTime = Mathf.Repeat(previewTime + (float)(now - lastEditorTime) / duration, 1f);
                Repaint(); SceneView.RepaintAll();
            }
            lastEditorTime = now;
        }

        private void OnSceneGUI()
        {
            PathFollower2D follower = (PathFollower2D)target;
            if (follower.Path == null) return;
            if (showTimeMarkers)
            {
                Handles.color = new Color(0.7f, 0.9f, 1f, 0.8f);
                for (int i = 0; i <= markerCount; i++)
                {
                    float time = i / (float)markerCount;
                    float progress = ResolveEditorProgress(follower, time);
                    Vector2 position = follower.Path.EvaluatePosition(progress);
                    Handles.DotHandleCap(0, position, Quaternion.identity, HandleUtility.GetHandleSize(position) * 0.035f, EventType.Repaint);
                }
            }
            float currentProgress = ResolveEditorProgress(follower, previewTime);
            Vector2 current = follower.Path.EvaluatePosition(currentProgress);
            Handles.color = Color.magenta;
            Handles.DrawWireDisc(current, Vector3.forward, HandleUtility.GetHandleSize(current) * 0.12f);
            Handles.Label(current, $"Time {previewTime:0.00}  Progress {currentProgress:0.00}");
        }

        private float ResolveEditorProgress(PathFollower2D follower, float time)
        {
            float value = follower.ProgressCurve.Evaluate(time) + serializedObject.FindProperty("startOffset").floatValue;
            PathLoopMode loop = (PathLoopMode)serializedObject.FindProperty("loopMode").enumValueIndex;
            return loop == PathLoopMode.Loop ? Mathf.Repeat(value, 1f) : Mathf.Clamp01(value);
        }
    }
}
