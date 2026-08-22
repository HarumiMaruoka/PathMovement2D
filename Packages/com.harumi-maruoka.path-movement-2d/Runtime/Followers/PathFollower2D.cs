using System;
using UnityEngine;

namespace Game.PathMovement2D
{
    [DisallowMultipleComponent]
    public sealed class PathFollower2D : MonoBehaviour
    {
        [SerializeField] private Path2D path;
        [SerializeField] private Transform target;
        [SerializeField] private PathTimingMode timingMode = PathTimingMode.Duration;
        [SerializeField, Min(0.0001f)] private float duration = 5f;
        [SerializeField, Min(0.0001f)] private float speed = 3f;
        [SerializeField] private ProgressCurve progressCurve = new();
        [SerializeField] private PathLoopMode loopMode = PathLoopMode.Once;
        [SerializeField] private PathDirection initialDirection = PathDirection.Forward;
        [SerializeField] private PathUpdateMode updateMode = PathUpdateMode.Update;
        [SerializeField] private bool rotateAlongPath;
        [SerializeField] private float rotationOffsetDegrees;
        [SerializeField, Range(0f, 1f)] private float startOffset;
        [SerializeField] private bool playOnAwake = true;
        [SerializeField] private bool useUnscaledTime;

        private PathPlaybackState state;
        private PathDirection direction;
        private float normalizedTime;

        public event Action Started;
        public event Action Paused;
        public event Action Resumed;
        public event Action Completed;
        public event Action Looped;
        public event Action Stopped;

        public Path2D Path { get => path; set => path = value; }
        public Transform Target { get => target != null ? target : transform; set => target = value; }
        public ProgressCurve ProgressCurve => progressCurve;
        public PathPlaybackState State => state;
        public float NormalizedTime => normalizedTime;
        public float CurrentProgress => ResolveProgress(normalizedTime);
        public float EffectiveDuration => timingMode == PathTimingMode.Duration ? duration : (path != null ? path.GetLength() / Mathf.Max(speed, 0.0001f) : 0f);

        private void Awake() { direction = initialDirection; normalizedTime = direction == PathDirection.Forward ? 0f : 1f; }
        private void Start() { if (playOnAwake) Play(); else ApplyCurrent(); }
        private void Update() { if (updateMode == PathUpdateMode.Update) Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime); }
        private void LateUpdate() { if (updateMode == PathUpdateMode.LateUpdate) Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime); }
        private void FixedUpdate() { if (updateMode == PathUpdateMode.FixedUpdate) Tick(useUnscaledTime ? Time.fixedUnscaledDeltaTime : Time.fixedDeltaTime); }

        public void Play() { direction = initialDirection; normalizedTime = direction == PathDirection.Forward ? 0f : 1f; state = PathPlaybackState.Playing; Started?.Invoke(); ApplyCurrent(); }
        public void PlayForward() { direction = PathDirection.Forward; state = PathPlaybackState.Playing; Started?.Invoke(); }
        public void PlayBackward() { direction = PathDirection.Backward; state = PathPlaybackState.Playing; Started?.Invoke(); }
        public void Pause() { if (state != PathPlaybackState.Playing) return; state = PathPlaybackState.Paused; Paused?.Invoke(); }
        public void Resume() { if (state != PathPlaybackState.Paused) return; state = PathPlaybackState.Playing; Resumed?.Invoke(); }
        public void Stop() { state = PathPlaybackState.Stopped; normalizedTime = direction == PathDirection.Forward ? 0f : 1f; ApplyCurrent(); Stopped?.Invoke(); }
        public void SetProgress(float progress) { normalizedTime = Mathf.Clamp01(progress); ApplyCurrent(); }

        private void Tick(float deltaTime)
        {
            if (state != PathPlaybackState.Playing || path == null) return;
            float effectiveDuration = EffectiveDuration;
            if (effectiveDuration <= 0.000001f) return;
            float sign = direction == PathDirection.Forward ? 1f : -1f;
            normalizedTime += deltaTime / effectiveDuration * sign;
            HandleBoundary(); ApplyCurrent();
        }

        private void HandleBoundary()
        {
            bool crossed = normalizedTime > 1f || normalizedTime < 0f;
            if (!crossed) return;
            switch (loopMode)
            {
                case PathLoopMode.Once:
                    normalizedTime = Mathf.Clamp01(normalizedTime); state = PathPlaybackState.Stopped; ApplyCurrent(); Completed?.Invoke(); break;
                case PathLoopMode.Loop:
                    normalizedTime = Mathf.Repeat(normalizedTime, 1f); Looped?.Invoke(); break;
                case PathLoopMode.PingPong:
                    normalizedTime = normalizedTime > 1f ? 2f - normalizedTime : -normalizedTime;
                    normalizedTime = Mathf.Clamp01(normalizedTime);
                    direction = direction == PathDirection.Forward ? PathDirection.Backward : PathDirection.Forward;
                    Looped?.Invoke(); break;
            }
        }

        private float ResolveProgress(float time)
        {
            float value = progressCurve?.Evaluate(Mathf.Clamp01(time)) ?? time;
            value += startOffset;
            return loopMode == PathLoopMode.Loop ? Mathf.Repeat(value, 1f) : Mathf.Clamp01(value);
        }

        private void ApplyCurrent()
        {
            if (path == null) return;
            Transform moving = Target;
            float progress = ResolveProgress(normalizedTime);
            Vector2 position = path.EvaluatePosition(progress);
            moving.position = new Vector3(position.x, position.y, moving.position.z);
            if (rotateAlongPath)
            {
                Vector2 tangent = path.EvaluateTangent(progress);
                float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg + rotationOffsetDegrees;
                moving.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }
}
