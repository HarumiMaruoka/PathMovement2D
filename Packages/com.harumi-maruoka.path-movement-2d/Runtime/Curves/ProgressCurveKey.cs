using System;
using UnityEngine;

namespace Game.PathMovement2D
{
    [Serializable]
    public sealed class ProgressCurveKey
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField, Range(0f, 1f)] private float time;
        [SerializeField] private float value;
        [SerializeField] private Vector2 inHandle = new(-0.1f, -0.1f);
        [SerializeField] private Vector2 outHandle = new(0.1f, 0.1f);
        [SerializeField] private ProgressTangentMode tangentMode = ProgressTangentMode.Aligned;

        public string Id => id;
        public float Time { get => time; set => time = value; }
        public float Value { get => this.value; set => this.value = value; }
        public Vector2 InHandle { get => inHandle; set => inHandle = value; }
        public Vector2 OutHandle { get => outHandle; set => outHandle = value; }
        public ProgressTangentMode TangentMode { get => tangentMode; set => tangentMode = value; }

        public ProgressCurveKey(float time, float value) { this.time = time; this.value = value; }
        internal void EnsureId() { if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N"); }
    }
}
