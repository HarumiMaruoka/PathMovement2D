using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.PathMovement2D.Editor
{
    public sealed class PathMovementEditorWindow : EditorWindow
    {
        [SerializeField] private PathMovementClip2D clip;
        [SerializeField] private Transform movementTarget;
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip animation;
        [SerializeField] private bool lockSelection;
        [SerializeField] private PathClipSpace space;
        [SerializeField] private Transform reference;
        [SerializeField] private bool mirror;
        [SerializeField] private bool normalizedSync = true;
        [SerializeField] private float animationOffset;
        private readonly List<PathMovementClip2D> clips = new();
        private PathSceneHandles handles = new();
        private UnityEditor.Editor clipInspector;
        private PathClipPreviewSession preview;
        private bool playing;
        private float previewTime;
        private double lastTime;
        private Vector2 scroll;
        private string error;
        private int markerCount = 20;

        [MenuItem("Window/Path Movement 2D/Path Movement Editor")]
        public static void OpenForSelection()
        {
            var window = GetWindow<PathMovementEditorWindow>("Path Movement");
            window.lockSelection = false;
            window.ReadSelection();
            window.Show();
        }
        public static void Open(PathMovementClip2D asset)
        {
            var window = GetWindow<PathMovementEditorWindow>("Path Movement");
            window.StopPreview();
            window.clip = asset;
            window.handles = new PathSceneHandles();
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += ReadSelection;
            SceneView.duringSceneGui += DrawScene;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            Undo.undoRedoPerformed += OnUndo;
            lastTime = EditorApplication.timeSinceStartup;
        }
        private void OnDisable()
        {
            StopPreview();
            Selection.selectionChanged -= ReadSelection;
            SceneView.duringSceneGui -= DrawScene;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
            Undo.undoRedoPerformed -= OnUndo;
            if (clipInspector != null) DestroyImmediate(clipInspector);
        }
        private void PlayModeChanged(PlayModeStateChange state) => StopPreview();
        private void OnUndo() { StopPreview(); if (clip != null) clip.Rebuild(); Repaint(); SceneView.RepaintAll(); }

        private void ReadSelection()
        {
            if (lockSelection) return;
            StopPreview();
            clips.Clear();
            if (Selection.activeObject is PathMovementClip2D asset) clip = asset;
            else if (Selection.activeGameObject != null)
            {
                GameObject selected = Selection.activeGameObject;
                foreach (var component in selected.GetComponents<MonoBehaviour>())
                    if (component is IPathMovementClipSource source) source.GetPathMovementClips(clips);
                var unique = clips.Where(c => c != null).Distinct().ToArray();
                clips.Clear(); clips.AddRange(unique);
                clip = clips.Contains(clip) ? clip : clips.FirstOrDefault();
                movementTarget = selected.transform;
                animator = selected.GetComponentInChildren<Animator>(true);
                var player = selected.GetComponent<PathMovementPlayer2D>();
                space = PathClipSpace.Relative; reference = null; mirror = false;
                if (player != null && player.Clip == clip)
                {
                    movementTarget = player.Target; space = player.Space;
                    reference = player.ReferenceTransform; mirror = player.MirrorX;
                }
                animation = null;
            }
            else { clip = null; movementTarget = null; animator = null; animation = null; }
            handles = new PathSceneHandles();
            Repaint(); SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            lockSelection = EditorGUILayout.Toggle("Lock Selection", lockSelection);
            EditorGUI.BeginChangeCheck();
            var nextClip = (PathMovementClip2D)EditorGUILayout.ObjectField("Movement Clip", clip, typeof(PathMovementClip2D), false);
            if (clips.Count > 1)
            {
                int index = Mathf.Max(0, clips.IndexOf(nextClip));
                nextClip = clips[EditorGUILayout.Popup("Source Clips", index, clips.Select(c => c.name).ToArray())];
            }
            var nextTarget = (Transform)EditorGUILayout.ObjectField("Movement Target", movementTarget, typeof(Transform), true);
            var nextSpace = (PathClipSpace)EditorGUILayout.EnumPopup("Space", space);
            var nextReference = reference;
            if (nextSpace == PathClipSpace.ReferenceTransform)
                nextReference = (Transform)EditorGUILayout.ObjectField("Reference Transform", reference, typeof(Transform), true);
            bool nextMirror = EditorGUILayout.Toggle("Mirror X", mirror);
            var nextAnimator = (Animator)EditorGUILayout.ObjectField("Animator", animator, typeof(Animator), true);
            if (EditorGUI.EndChangeCheck())
            {
                StopPreview();
                if (clip != nextClip) handles = new PathSceneHandles();
                if (animator != nextAnimator) animation = null;
                clip = nextClip; movementTarget = nextTarget; space = nextSpace;
                reference = nextReference; mirror = nextMirror; animator = nextAnimator;
            }
            if (clip == null)
            {
                EditorGUILayout.HelpBox("Select an object with IPathMovementClipSource, or assign a movement clip.", MessageType.Info);
                return;
            }

            AnimationClip[] animations = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips.Where(c => c != null).Distinct().ToArray()
                : Array.Empty<AnimationClip>();
            var labels = new[] { "None" }.Concat(animations.Select(c => c.name)).ToArray();
            EditorGUI.BeginChangeCheck();
            int selectedAnimation = EditorGUILayout.Popup("Controller Animation", Array.IndexOf(animations, animation) + 1, labels);
            bool nextSync = EditorGUILayout.Toggle("Normalized Time Sync", normalizedSync);
            float nextOffset = EditorGUILayout.FloatField("Animation Offset (s)", animationOffset);
            if (EditorGUI.EndChangeCheck())
            {
                StopPreview(); animation = selectedAnimation == 0 ? null : animations[selectedAnimation - 1];
                normalizedSync = nextSync; animationOffset = nextOffset;
            }
            if (animator != null && animator.runtimeAnimatorController == null)
                EditorGUILayout.HelpBox("Assign an Animator Controller to enable animation preview.", MessageType.Info);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUI.BeginChangeCheck();
                previewTime = EditorGUILayout.Slider("Time (s)", previewTime, 0f,
                    clip.Duration * (clip.LoopMode == PathLoopMode.PingPong ? 2f : 1f));
                if (EditorGUI.EndChangeCheck()) ApplyPreview();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(playing ? "Pause" : "Play"))
                {
                    if (playing) playing = false;
                    else
                    {
                        if (clip.LoopMode == PathLoopMode.Once && previewTime >= clip.Duration) previewTime = 0f;
                        ApplyPreview(); playing = preview != null;
                        lastTime = EditorApplication.timeSinceStartup;
                    }
                }
                if (GUILayout.Button("Stop / Restore")) { StopPreview(); previewTime = 0f; }
                EditorGUILayout.EndHorizontal();
            }
            markerCount = EditorGUILayout.IntSlider("Time Markers", markerCount, 2, 64);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
            EditorGUILayout.HelpBox("Scene handles edit the shared asset. Preview restores the target when stopped. Controller transitions and Blend Trees are not sampled.", MessageType.Info);
            if (GUILayout.Button("Duplicate Clip")) PathMovementClipEditor.Duplicate(clip);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            UnityEditor.Editor.CreateCachedEditor(clip, null, ref clipInspector);
            EditorGUI.BeginChangeCheck();
            clipInspector.DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) { StopPreview(); clip.Rebuild(); SceneView.RepaintAll(); }
            EditorGUILayout.EndScrollView();
        }

        private Matrix4x4 Basis()
        {
            Matrix4x4 basis = Matrix4x4.identity;
            if (preview != null) basis = preview.Instance.GetBasis();
            else if (space == PathClipSpace.ReferenceTransform && reference != null)
                basis = Matrix4x4.TRS(reference.position, Quaternion.Euler(0, 0, reference.eulerAngles.z), Vector3.one);
            else if (space == PathClipSpace.Relative && movementTarget != null)
                basis = Matrix4x4.TRS(movementTarget.position, Quaternion.Euler(0, 0, movementTarget.eulerAngles.z), Vector3.one);
            if (movementTarget != null) basis.m23 = movementTarget.position.z;
            return basis;
        }

        private void DrawScene(SceneView view)
        {
            if (clip == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (space == PathClipSpace.ReferenceTransform && reference == null) return;
            Matrix4x4 basis = Basis();
            Matrix4x4 editingMatrix = basis * Matrix4x4.Scale(new Vector3(mirror ? -1 : 1, 1, 1));
            int revision = EditorUtility.GetDirtyCount(clip);
            handles.Draw(new PathEditingContext(clip, editingMatrix));
            if (revision != EditorUtility.GetDirtyCount(clip)) { StopPreview(); Repaint(); }
            Handles.color = new Color(0.7f, 0.9f, 1f, 0.8f);
            for (int i = 0; i <= markerCount; i++)
            {
                Vector3 point = basis.MultiplyPoint3x4(clip.Sample(clip.Duration * i / markerCount, mirror).Position);
                Handles.DotHandleCap(0, point, Quaternion.identity, HandleUtility.GetHandleSize(point) * 0.035f, EventType.Repaint);
            }
            Vector3 current = basis.MultiplyPoint3x4(clip.Sample(previewTime, mirror).Position);
            Handles.color = Color.magenta;
            Handles.DrawWireDisc(current, Vector3.forward, HandleUtility.GetHandleSize(current) * 0.12f);
        }

        private void ApplyPreview()
        {
            error = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode || clip == null) return;
            if (movementTarget == null) { SceneView.RepaintAll(); return; }
            if (EditorUtility.IsPersistent(movementTarget)) { error = "Select a scene object or an object in Prefab Mode for preview."; return; }
            if (space == PathClipSpace.ReferenceTransform && (reference == null || reference == movementTarget || reference.IsChildOf(movementTarget)))
            { error = "Use a reference Transform outside the moving target hierarchy."; return; }
            try
            {
                preview ??= new PathClipPreviewSession(clip, movementTarget, space, reference, mirror);
                float phase = clip.GetNormalizedTime(previewTime);
                float animationTime = animation == null ? 0f : phase * (normalizedSync ? animation.length : clip.Duration) + animationOffset;
                if (animation != null) animationTime = animation.isLooping && animation.length > 0f
                    ? Mathf.Repeat(animationTime, animation.length) : Mathf.Clamp(animationTime, 0f, animation.length);
                preview.Evaluate(previewTime, animator, animation, animationTime);
                SceneView.RepaintAll();
            }
            catch (Exception exception) { StopPreview(); error = exception.Message; }
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (preview != null && (movementTarget == null || clip == null || !preview.Active)) StopPreview();
            if (playing && clip != null)
            {
                previewTime += (float)(now - lastTime);
                float end = clip.Duration * (clip.LoopMode == PathLoopMode.PingPong ? 2f : 1f);
                if (previewTime >= end)
                {
                    if (clip.LoopMode == PathLoopMode.Once) { previewTime = end; playing = false; }
                    else previewTime = Mathf.Repeat(previewTime, end);
                }
                ApplyPreview(); Repaint();
            }
            lastTime = now;
        }
        private void StopPreview() { playing = false; preview?.Dispose(); preview = null; SceneView.RepaintAll(); }
    }
}
