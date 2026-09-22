using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.PathMovement2D
{
    public enum PathClipSpace { Relative, World, ReferenceTransform }

    public readonly struct PathMovementSample2D
    {
        public readonly Vector2 Position;
        public readonly Vector2 Tangent;
        public PathMovementSample2D(Vector2 position, Vector2 tangent)
        { Position = position; Tangent = tangent; }
    }

    [CreateAssetMenu(menuName = "Path Movement 2D/Movement Clip", fileName = "MovementClip")]
    public sealed class PathMovementClip2D : ScriptableObject
    {
        [SerializeField] private List<PathPoint2D> points = new() { new(Vector2.zero), new(Vector2.right * 2f) };
        [SerializeField] private bool closed;
        [SerializeField, Min(0.0001f)] private float duration = 1f;
        [SerializeField] private ProgressCurve progressCurve = new();
        [SerializeField] private PathLoopMode loopMode;
        [SerializeField] private bool rotateAlongPath;
        [SerializeField] private float rotationOffsetDegrees;
        [SerializeField, Min(0.00001f)] private float arcLengthTolerance = 0.001f;
        [SerializeField, Range(1, 8)] private int maxSubdivisionDepth = 6;

        [NonSerialized] private PathGeometry2D geometry;
        [NonSerialized] private bool isPlaybackInstance;
        [NonSerialized] private float time;
        [NonSerialized] private bool hasOrigin;
        [NonSerialized] private Matrix4x4 origin;
        [NonSerialized] private Transform boundTarget;

        public IReadOnlyList<PathPoint2D> Points => points;
        public int PointCount => points.Count;
        public bool Closed { get => closed; set { closed = value; Rebuild(); } }
        public float Duration { get => duration; set => duration = Mathf.Max(0.0001f, value); }
        public ProgressCurve ProgressCurve => progressCurve;
        public PathLoopMode LoopMode { get => loopMode; set => loopMode = value; }
        public bool RotateAlongPath { get => rotateAlongPath; set => rotateAlongPath = value; }
        public float RotationOffsetDegrees { get => rotationOffsetDegrees; set => rotationOffsetDegrees = value; }
        public bool IsPlaybackInstance => isPlaybackInstance;
        public float Time => time;
        public bool IsComplete => loopMode == PathLoopMode.Once && time >= Duration;
        public bool MirrorX { get; set; }
        public PathClipSpace Space { get; set; }
        public Transform ReferenceTransform { get; set; }
        internal Matrix4x4 Origin => origin;
        internal PathGeometry2D Geometry => geometry ??= new PathGeometry2D(points, closed,
            Matrix4x4.identity, arcLengthTolerance, maxSubdivisionDepth, new PathArcLengthTable());

        /// <summary>The caller owns this transient ScriptableObject and must destroy it when finished.</summary>
        public PathMovementClip2D CreatePlaybackInstance()
        {
            var instance = Instantiate(this);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.isPlaybackInstance = true;
            instance.time = 0f;
            instance.hasOrigin = false;
            instance.boundTarget = null;
            instance.geometry = null;
            return instance;
        }

        public void CaptureOrigin(Transform target)
        {
            RequireInstance();
            if (target == null) throw new ArgumentNullException(nameof(target));
            origin = Matrix4x4.TRS(target.position, Quaternion.Euler(0, 0, target.eulerAngles.z), Vector3.one);
            boundTarget = target;
            hasOrigin = true;
        }

        public void Reset() { time = 0f; }

        internal void AdvanceTime(Transform target, float deltaTime)
        {
            float nextTime = GetNextTime(deltaTime);
            Evaluate(target, nextTime);
            time = nextTime;
        }

        internal Vector2 AdvanceTime(float deltaTime)
        {
            float nextTime = GetNextTime(deltaTime);
            Vector2 moveDelta = Sample(nextTime, MirrorX).Position - Sample(time, MirrorX).Position;
            time = nextTime;
            return moveDelta;
        }

        private float GetNextTime(float deltaTime)
        {
            RequireInstance();
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            return loopMode == PathLoopMode.Once ? Mathf.Min(time + deltaTime, Duration) : time + deltaTime;
        }

        public void Evaluate(Transform target, float seconds)
        {
            RequireInstance();
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!hasOrigin) CaptureOrigin(target);
            if (boundTarget != target) throw new InvalidOperationException("Create one playback instance per target, or call CaptureOrigin for a new target.");
            Matrix4x4 basis = GetBasis();
            PathMovementSample2D sample = Sample(seconds, MirrorX);
            Vector3 position = basis.MultiplyPoint3x4(sample.Position);
            target.position = new Vector3(position.x, position.y, target.position.z);
            if (rotateAlongPath)
            {
                Vector3 tangent = basis.MultiplyVector(sample.Tangent);
                float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg + rotationOffsetDegrees;
                target.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        internal Matrix4x4 GetBasis()
        {
            if (Space == PathClipSpace.World) return Matrix4x4.identity;
            if (Space == PathClipSpace.ReferenceTransform)
            {
                if (ReferenceTransform == null || ReferenceTransform == boundTarget || ReferenceTransform.IsChildOf(boundTarget))
                    throw new InvalidOperationException("Reference Transform must be outside the moving target hierarchy.");
                return Matrix4x4.TRS(ReferenceTransform.position,
                    Quaternion.Euler(0f, 0f, ReferenceTransform.eulerAngles.z), Vector3.one);
            }
            return origin;
        }

        /// <summary>Pure evaluation in clip coordinates. Does not change the playback clock.</summary>
        public PathMovementSample2D Sample(float seconds, bool mirrorX = false)
        {
            float normalized = GetNormalizedTime(seconds);
            float phase = Mathf.Max(0f, seconds) / Duration;
            float progress = Mathf.Clamp01(progressCurve.Evaluate(normalized));
            Vector2 position = Geometry.EvaluatePosition(progress);
            Vector2 tangent = Geometry.EvaluateTangent(progress);
            // Determine travel direction from the curve, including free curves and ping-pong.
            float before = progressCurve.Evaluate(Mathf.Max(0f, normalized - 0.0001f));
            float after = progressCurve.Evaluate(Mathf.Min(1f, normalized + 0.0001f));
            if (after < before) tangent = -tangent;
            if (loopMode == PathLoopMode.PingPong && Mathf.Repeat(phase, 2f) >= 1f) tangent = -tangent;
            if (mirrorX) { position.x = -position.x; tangent.x = -tangent.x; }
            return new PathMovementSample2D(position, tangent);
        }

        public float GetNormalizedTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            float phase = Mathf.Max(0f, seconds) / Duration;
            return loopMode == PathLoopMode.Loop ? Mathf.Repeat(phase, 1f)
                : loopMode == PathLoopMode.PingPong ? Mathf.PingPong(phase, 1f) : Mathf.Clamp01(phase);
        }

        public void AddPoint(Vector2 position) { points.Add(new PathPoint2D(position)); Rebuild(); }
        public void RemovePointAt(int index) { points.RemoveAt(index); Rebuild(); }
        public void Rebuild() { geometry = null; Geometry.Rebuild(); }

        /// <summary>Copies geometry only. Relative import anchors the first point at zero.</summary>
        public void CopyFrom(Path2D path, bool worldCoordinates = false)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            points.Clear();
            Vector2 first = path.PointCount > 0 ? path.ToWorld(path.GetPoint(0).Position) : Vector2.zero;
            Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(0, 0, path.transform.eulerAngles.z));
            for (int i = 0; i < path.PointCount; i++)
            {
                PathPoint2D source = path.GetPoint(i);
                Vector2 world = path.ToWorld(source.Position);
                Vector2 incoming = path.ToWorld(source.Position + path.Geometry.ResolveTangent(i, true)) - world;
                Vector2 outgoing = path.ToWorld(source.Position + path.Geometry.ResolveTangent(i, false)) - world;
                points.Add(new PathPoint2D(worldCoordinates ? world : (Vector2)(inverse * (world - first)))
                {
                    InTangent = worldCoordinates ? incoming : (Vector2)(inverse * incoming),
                    OutTangent = worldCoordinates ? outgoing : (Vector2)(inverse * outgoing),
                    // Freeze transformed tangents so nonuniform scale preserves the original shape.
                    TangentMode = PathTangentMode.Free, SegmentType = source.SegmentType
                });
            }
            closed = path.Closed;
            Rebuild();
        }

        private void RequireInstance()
        {
            if (!isPlaybackInstance) throw new InvalidOperationException("Call CreatePlaybackInstance() before applying movement. Assets are shared.");
        }

        private void OnValidate()
        {
            duration = Mathf.Max(0.0001f, duration);
            arcLengthTolerance = Mathf.Max(0.00001f, arcLengthTolerance);
            maxSubdivisionDepth = Mathf.Clamp(maxSubdivisionDepth, 1, 8);
            geometry = null;
        }
    }
}
