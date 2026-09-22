using System.Collections.Generic;
using UnityEngine;

namespace Game.PathMovement2D
{
    [DisallowMultipleComponent]
    public sealed class PathMovementPlayer2D : MonoBehaviour, IPathMovementClipSource
    {
        [SerializeField] private PathMovementClip2D clip;
        [SerializeField] private Transform target;
        [SerializeField] private PathClipSpace space;
        [SerializeField] private Transform referenceTransform;
        [SerializeField] private bool mirrorX;
        [SerializeField] private bool playOnAwake = true;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private PathUpdateMode updateMode = PathUpdateMode.LateUpdate;
        private PathMovementClip2D instance;

        public PathMovementClip2D Clip { get => clip; set { Release(); clip = value; } }
        public Transform Target => target != null ? target : transform;
        public PathClipSpace Space => space;
        public Transform ReferenceTransform => referenceTransform;
        public bool MirrorX => mirrorX;
        public PathMovementClip2D Instance => instance;
        public bool IsPlaying { get; private set; }

        public void GetPathMovementClips(List<PathMovementClip2D> clips) { if (clip != null) clips.Add(clip); }
        private void Start() { if (playOnAwake) Play(); }
        private void Update() { if (updateMode == PathUpdateMode.Update) Tick(useUnscaledTime ? UnityEngine.Time.unscaledDeltaTime : UnityEngine.Time.deltaTime); }
        private void LateUpdate() { if (updateMode == PathUpdateMode.LateUpdate) Tick(useUnscaledTime ? UnityEngine.Time.unscaledDeltaTime : UnityEngine.Time.deltaTime); }
        private void FixedUpdate() { if (updateMode == PathUpdateMode.FixedUpdate) Tick(useUnscaledTime ? UnityEngine.Time.fixedUnscaledDeltaTime : UnityEngine.Time.fixedDeltaTime); }
        private void OnDisable() { IsPlaying = false; }
        private void OnDestroy() => Release();

        public void Play()
        {
            if (clip == null) return;
            EnsureInstance();
            instance.CaptureOrigin(Target);
            instance.Reset();
            instance.Evaluate(Target, 0f);
            IsPlaying = true;
        }
        public void Pause() => IsPlaying = false;
        public void Resume() { if (instance != null && !instance.IsComplete) IsPlaying = true; }
        public void Stop() { IsPlaying = false; if (instance != null) { instance.Reset(); instance.Evaluate(Target, 0f); } }
        public void Evaluate(float seconds) { if (clip == null) return; EnsureInstance(); instance.Evaluate(Target, seconds); }
        private void Tick(float deltaTime)
        {
            if (!IsPlaying || instance == null) return;
            Configure();
            instance.Update(Target, deltaTime);
            if (instance.IsComplete) IsPlaying = false;
        }
        private void EnsureInstance() { if (instance == null) instance = clip.CreatePlaybackInstance(); Configure(); }
        private void Configure() { instance.Space = space; instance.ReferenceTransform = referenceTransform; instance.MirrorX = mirrorX; }
        private void Release()
        {
            IsPlaying = false;
            if (instance == null) return;
            if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
            instance = null;
        }
    }
}
