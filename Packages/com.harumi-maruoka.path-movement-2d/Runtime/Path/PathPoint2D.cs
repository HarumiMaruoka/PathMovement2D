using System;
using UnityEngine;

namespace Game.PathMovement2D
{
    [Serializable]
    public sealed class PathPoint2D
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField] private Vector2 position;
        [SerializeField] private Vector2 inTangent = Vector2.left;
        [SerializeField] private Vector2 outTangent = Vector2.right;
        [SerializeField] private PathTangentMode tangentMode = PathTangentMode.Auto;
        [SerializeField] private PathSegmentType segmentType = PathSegmentType.Bezier;

        public string Id => id;
        public Vector2 Position { get => position; set => position = value; }
        public Vector2 InTangent { get => inTangent; set => inTangent = value; }
        public Vector2 OutTangent { get => outTangent; set => outTangent = value; }
        public PathTangentMode TangentMode { get => tangentMode; set => tangentMode = value; }
        public PathSegmentType SegmentType { get => segmentType; set => segmentType = value; }

        public PathPoint2D(Vector2 position)
        {
            this.position = position;
        }

        internal void EnsureId()
        {
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
        }
    }
}
