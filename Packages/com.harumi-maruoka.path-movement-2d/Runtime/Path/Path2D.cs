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

        private PathGeometry2D geometry;
        private Matrix4x4 geometryMatrix;

        internal PathGeometry2D Geometry
        {
            get
            {
                Matrix4x4 matrix = space == PathSpace.Local ? transform.localToWorldMatrix : Matrix4x4.identity;
                if (geometry == null || matrix != geometryMatrix) Rebuild();
                return geometry;
            }
        }

        public float GetLength() => Geometry.GetLength();
        public Vector2 EvaluatePosition(float progress) => Geometry.EvaluatePosition(progress);
        public Vector2 EvaluateTangent(float progress) => Geometry.EvaluateTangent(progress);
        internal Vector2 EvaluateSegmentPosition(int segment, float t) => Geometry.EvaluateSegmentPosition(segment, t);
        internal Vector2 EvaluateSegmentTangent(int segment, float t) => Geometry.EvaluateSegmentTangent(segment, t);
        public Vector2 ToWorld(Vector2 point) => space == PathSpace.Local ? transform.TransformPoint(point) : point;
        public Vector2 ToPathSpace(Vector2 point) => space == PathSpace.Local ? transform.InverseTransformPoint(point) : point;
        private void EnsureTable() { _ = Geometry; }
        public void Rebuild()
        {
            points ??= new List<PathPoint2D>();
            lengthTable ??= new PathArcLengthTable();
            geometryMatrix = space == PathSpace.Local ? transform.localToWorldMatrix : Matrix4x4.identity;
            geometry = new PathGeometry2D(points, closed, geometryMatrix, arcLengthTolerance, maxSubdivisionDepth, lengthTable);
            geometry.Rebuild();
        }
    }
}
