using System.Collections.Generic;
using UnityEngine;

namespace Game.PathMovement2D
{
    // Shared evaluator; owners retain their serialized point and table fields.
    internal sealed class PathGeometry2D
    {
        private readonly List<PathPoint2D> points;
        private readonly bool closed;
        private readonly Matrix4x4 matrix;
        private readonly float arcLengthTolerance;
        private readonly int maxSubdivisionDepth;
        private PathArcLengthTable lengthTable;
        public int SegmentCount => closed ? points.Count : Mathf.Max(0, points.Count - 1);

        public PathGeometry2D(List<PathPoint2D> points, bool closed, Matrix4x4 matrix,
            float tolerance, int depth, PathArcLengthTable table)
        {
            this.points = points; this.closed = closed; this.matrix = matrix;
            arcLengthTolerance = tolerance; maxSubdivisionDepth = depth; lengthTable = table;
        }
        public float GetLength() { EnsureTable(); return lengthTable.Length; }

        public Vector2 EvaluatePosition(float progress)
        {
            EnsureTable();
            if (points.Count == 0) return ToWorld(Vector2.zero);
            if (points.Count == 1) return ToWorld(points[0].Position);
            lengthTable.Map(Mathf.Clamp01(progress) * lengthTable.Length, out int segment, out float t);
            return ToWorld(EvaluateSegmentPosition(segment, t));
        }

        public Vector2 EvaluateTangent(float progress)
        {
            EnsureTable();
            if (SegmentCount == 0) return matrix.MultiplyVector(Vector3.right).normalized;
            lengthTable.Map(Mathf.Clamp01(progress) * lengthTable.Length, out int segment, out float t);
            Vector2 tangent = EvaluateSegmentTangent(segment, t);
            tangent = matrix.MultiplyVector(tangent);
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
                if (segment > 0) lengthTable.Add(distance, segment, 0f);
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

        public Vector2 ToWorld(Vector2 point) => matrix.MultiplyPoint3x4(point);
        public Vector2 ToPathSpace(Vector2 point) => matrix.inverse.MultiplyPoint3x4(point);

        private void EnsureTable() { if (lengthTable == null || (SegmentCount > 0 && lengthTable.Count == 0)) Rebuild(); }

        private void GetTangents(int segment, out Vector2 outgoing, out Vector2 incoming)
        {
            int next = (segment + 1) % points.Count;
            outgoing = ResolveTangent(segment, false);
            incoming = ResolveTangent(next, true);
        }

        internal Vector2 ResolveTangent(int index, bool incoming)
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
