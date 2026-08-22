using System.Collections.Generic;
using UnityEngine;

namespace Game.PathMovement2D
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Path2D : MonoBehaviour
    {
        [SerializeField] private PathSpace space = PathSpace.Local;
        [SerializeField] private bool closed;
        [SerializeField] private List<PathPoint2D> points = new();
        [SerializeField, Min(0.00001f)] private float arcLengthTolerance = 0.001f;
        [SerializeField, Range(1, 8)] private int maxSubdivisionDepth = 6;
        [SerializeField] private PathArcLengthTable lengthTable = new();

        public PathSpace Space { get => space; set { space = value; Rebuild(); } }
        public bool Closed { get => closed; set { closed = value; Rebuild(); } }
        public IReadOnlyList<PathPoint2D> Points => points;
        public int PointCount => points.Count;
        public int SegmentCount => closed ? points.Count : Mathf.Max(0, points.Count - 1);

        private void Reset()
        {
            points = new List<PathPoint2D> { new(new Vector2(-2f, 0f)), new(new Vector2(2f, 0f)) };
            Rebuild();
        }

        private void OnEnable() => EnsureTable();
        private void OnValidate() { arcLengthTolerance = Mathf.Max(0.00001f, arcLengthTolerance); maxSubdivisionDepth = Mathf.Clamp(maxSubdivisionDepth, 1, 8); Rebuild(); }

        public PathPoint2D GetPoint(int index) => points[index];
        public void AddPoint(Vector2 position) { points.Add(new PathPoint2D(position)); Rebuild(); }
        public void InsertPoint(int index, PathPoint2D point) { points.Insert(index, point); Rebuild(); }
        public void RemovePointAt(int index) { if (index >= 0 && index < points.Count) { points.RemoveAt(index); Rebuild(); } }

        public float GetLength() { EnsureTable(); return lengthTable.Length; }

        public Vector2 EvaluatePosition(float progress)
        {
            EnsureTable();
            if (points.Count == 0) return transform.position;
            if (points.Count == 1) return ToWorld(points[0].Position);
            lengthTable.Map(Mathf.Clamp01(progress) * lengthTable.Length, out int segment, out float t);
            return ToWorld(EvaluateSegmentPosition(segment, t));
        }

        public Vector2 EvaluateTangent(float progress)
        {
            EnsureTable();
            if (SegmentCount == 0) return transform.right;
            lengthTable.Map(Mathf.Clamp01(progress) * lengthTable.Length, out int segment, out float t);
            Vector2 tangent = EvaluateSegmentTangent(segment, t);
            if (space == PathSpace.Local) tangent = transform.TransformVector(tangent);
            return tangent.sqrMagnitude > 0.0000001f ? tangent.normalized : Vector2.right;
        }

        public void Rebuild()
        {
            if (lengthTable == null) lengthTable = new PathArcLengthTable();
            lengthTable.Clear();
            for (int i = 0; i < points.Count; i++) points[i]?.EnsureId();
            if (SegmentCount == 0) return;

            Vector2 previous = EvaluateSegmentPosition(0, 0f);
            float distance = 0f;
            lengthTable.Add(0f, 0, 0f);
            for (int segment = 0; segment < SegmentCount; segment++)
            {
                if (points[segment].SegmentType == PathSegmentType.Linear)
                {
                    Vector2 current = EvaluateSegmentPosition(segment, 1f);
                    distance += Vector2.Distance(ToWorld(previous), ToWorld(current));
                    lengthTable.Add(distance, segment, 1f);
                    previous = current;
                }
                else
                {
                    // Four base intervals prevent inflected or S-shaped curves from being
                    // mistaken for a flat segment, then curvature controls further subdivision.
                    for (int part = 0; part < 4; part++)
                    {
                        float t0 = part * 0.25f, t1 = (part + 1) * 0.25f;
                        AppendAdaptiveSamples(segment, t0, t1, 0, ref previous, ref distance);
                    }
                }
            }
        }

        private void AppendAdaptiveSamples(int segment, float t0, float t1, int depth, ref Vector2 previous, ref float distance)
        {
            float midT = (t0 + t1) * 0.5f;
            Vector2 start = EvaluateSegmentPosition(segment, t0);
            Vector2 middle = EvaluateSegmentPosition(segment, midT);
            Vector2 end = EvaluateSegmentPosition(segment, t1);
            float chord = Vector2.Distance(ToWorld(start), ToWorld(end));
            float polyline = Vector2.Distance(ToWorld(start), ToWorld(middle)) + Vector2.Distance(ToWorld(middle), ToWorld(end));
            if (depth < maxSubdivisionDepth && polyline - chord > arcLengthTolerance)
            {
                AppendAdaptiveSamples(segment, t0, midT, depth + 1, ref previous, ref distance);
                AppendAdaptiveSamples(segment, midT, t1, depth + 1, ref previous, ref distance);
                return;
            }
            distance += Vector2.Distance(ToWorld(previous), ToWorld(end));
            lengthTable.Add(distance, segment, t1);
            previous = end;
        }

        internal Vector2 EvaluateSegmentPosition(int segment, float t)
        {
            PathPoint2D a = points[segment];
            PathPoint2D b = points[(segment + 1) % points.Count];
            if (a.SegmentType == PathSegmentType.Linear) return Vector2.LerpUnclamped(a.Position, b.Position, t);
            GetTangents(segment, out Vector2 outTangent, out Vector2 inTangent);
            Vector2 p0 = a.Position, p1 = p0 + outTangent, p3 = b.Position, p2 = p3 + inTangent;
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        internal Vector2 EvaluateSegmentTangent(int segment, float t)
        {
            PathPoint2D a = points[segment];
            PathPoint2D b = points[(segment + 1) % points.Count];
            if (a.SegmentType == PathSegmentType.Linear) return b.Position - a.Position;
            GetTangents(segment, out Vector2 outTangent, out Vector2 inTangent);
            Vector2 p0 = a.Position, p1 = p0 + outTangent, p3 = b.Position, p2 = p3 + inTangent;
            float u = 1f - t;
            return 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
        }

        public Vector2 ToWorld(Vector2 point) => space == PathSpace.Local ? transform.TransformPoint(point) : point;
        public Vector2 ToPathSpace(Vector2 point) => space == PathSpace.Local ? transform.InverseTransformPoint(point) : point;

        private void EnsureTable() { if (lengthTable == null || (SegmentCount > 0 && lengthTable.Count == 0)) Rebuild(); }

        private void GetTangents(int segment, out Vector2 outgoing, out Vector2 incoming)
        {
            int next = (segment + 1) % points.Count;
            outgoing = ResolveTangent(segment, false);
            incoming = ResolveTangent(next, true);
        }

        private Vector2 ResolveTangent(int index, bool incoming)
        {
            PathPoint2D point = points[index];
            if (point.TangentMode != PathTangentMode.Auto) return incoming ? point.InTangent : point.OutTangent;
            int previous = index - 1, next = index + 1;
            if (closed) { previous = (previous + points.Count) % points.Count; next %= points.Count; }
            else { previous = Mathf.Max(0, previous); next = Mathf.Min(points.Count - 1, next); }
            Vector2 direction = (points[next].Position - points[previous].Position).normalized;
            float distance = incoming ? Vector2.Distance(point.Position, points[previous].Position) : Vector2.Distance(point.Position, points[next].Position);
            return direction * distance / 3f * (incoming ? -1f : 1f);
        }
    }
}
