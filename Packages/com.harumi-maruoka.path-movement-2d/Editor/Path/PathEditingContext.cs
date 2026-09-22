using UnityEngine;
using UnityEditor;

namespace Game.PathMovement2D.Editor
{
    internal sealed class PathEditingContext
    {
        private readonly Path2D path;
        private readonly PathMovementClip2D clip;
        public Object Owner => path != null ? (Object)path : clip;
        public Matrix4x4 Matrix { get; }
        public int PointCount => path != null ? path.PointCount : clip.PointCount;
        public bool Closed => path != null ? path.Closed : clip.Closed;
        public int SegmentCount => Closed ? PointCount : Mathf.Max(0, PointCount - 1);
        private PathGeometry2D Geometry => path != null ? path.Geometry : clip.Geometry;

        public PathEditingContext(Path2D path)
        {
            this.path = path;
            Matrix = path.Space == PathSpace.Local ? path.transform.localToWorldMatrix : Matrix4x4.identity;
        }
        public PathEditingContext(PathMovementClip2D clip, Matrix4x4 matrix) { this.clip = clip; Matrix = matrix; }
        public PathPoint2D GetPoint(int index) => path != null ? path.GetPoint(index) : clip.Points[index];
        public Vector3 ToWorld(Vector2 point) => Matrix.MultiplyPoint3x4(point);
        public Vector2 ToPathSpace(Vector3 point) => Matrix.inverse.MultiplyPoint3x4(point);
        public Vector2 EvaluateSegmentPosition(int segment, float t) => Geometry.EvaluateSegmentPosition(segment, t);
        public Vector2 EvaluateSegmentTangent(int segment, float t) => Geometry.EvaluateSegmentTangent(segment, t);
        public void Rebuild() { if (path != null) path.Rebuild(); else clip.Rebuild(); }
        public void AddPoint(Vector2 point) { if (path != null) path.AddPoint(point); else clip.AddPoint(point); }
        public void RemovePointAt(int index) { if (path != null) path.RemovePointAt(index); else clip.RemovePointAt(index); }
        public void MarkDirty()
        {
            EditorUtility.SetDirty(Owner);
            if (PrefabUtility.IsPartOfPrefabInstance(Owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(Owner);
        }
    }
}
