using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.PathMovement2D
{
    [Serializable]
    public sealed class ProgressCurve : ISerializationCallbackReceiver
    {
        [SerializeField] private ProgressCurveConstraint constraint = ProgressCurveConstraint.Monotonic;
        [SerializeField] private ProgressCurvePreset preset = ProgressCurvePreset.Linear;
        [SerializeField] private List<ProgressCurveKey> keys = new();

        public ProgressCurveConstraint Constraint { get => constraint; set { constraint = value; Validate(); } }
        public ProgressCurvePreset Preset => preset;
        public IReadOnlyList<ProgressCurveKey> Keys => keys;
        public int KeyCount => keys.Count;

        public ProgressCurve() => ApplyPreset(ProgressCurvePreset.Linear);

        public float Evaluate(float time)
        {
            EnsureKeys();
            if (time <= keys[0].Time) return keys[0].Value;
            if (time >= keys[^1].Time) return keys[^1].Value;
            int low = 0, high = keys.Count - 1;
            while (high - low > 1)
            {
                int mid = (low + high) >> 1;
                if (keys[mid].Time <= time) low = mid; else high = mid;
            }
            ProgressCurveKey a = keys[low], b = keys[high];
            Vector2 p0 = new(a.Time, a.Value), p1 = p0 + a.OutHandle;
            Vector2 p3 = new(b.Time, b.Value), p2 = p3 + b.InHandle;
            float left = 0f, right = 1f, t = Mathf.InverseLerp(a.Time, b.Time, time);
            for (int i = 0; i < 12; i++)
            {
                float x = Cubic(p0.x, p1.x, p2.x, p3.x, t);
                if (x < time) left = t; else right = t;
                t = (left + right) * 0.5f;
            }
            return Cubic(p0.y, p1.y, p2.y, p3.y, t);
        }

        public int AddKey(float time, float value)
        {
            keys.Add(new ProgressCurveKey(time, value));
            preset = ProgressCurvePreset.Custom;
            Validate();
            for (int i = 0; i < keys.Count; i++) if (Mathf.Approximately(keys[i].Time, Mathf.Clamp01(time))) return i;
            return -1;
        }

        public void RemoveKey(int index)
        {
            if (index <= 0 || index >= keys.Count - 1) return;
            keys.RemoveAt(index); preset = ProgressCurvePreset.Custom; Validate();
        }

        public void MarkCustom() { preset = ProgressCurvePreset.Custom; Validate(); }

        public void ApplyPreset(ProgressCurvePreset value)
        {
            if (value == ProgressCurvePreset.Custom) { preset = value; return; }
            keys ??= new List<ProgressCurveKey>(); keys.Clear();
            ProgressCurveKey a = new(0f, 0f), b = new(1f, 1f);
            switch (value)
            {
                case ProgressCurvePreset.Linear: a.OutHandle = new Vector2(1f / 3f, 1f / 3f); b.InHandle = new Vector2(-1f / 3f, -1f / 3f); break;
                case ProgressCurvePreset.EaseIn: a.OutHandle = new Vector2(0.42f, 0f); b.InHandle = new Vector2(-0.42f, -1f); break;
                case ProgressCurvePreset.EaseOut: a.OutHandle = new Vector2(0.42f, 1f); b.InHandle = new Vector2(-0.42f, 0f); break;
                case ProgressCurvePreset.EaseInOut: a.OutHandle = new Vector2(0.42f, 0f); b.InHandle = new Vector2(-0.42f, 0f); break;
                case ProgressCurvePreset.Smooth: a.OutHandle = new Vector2(1f / 3f, 0f); b.InHandle = new Vector2(-1f / 3f, 0f); break;
            }
            keys.Add(a); keys.Add(b); preset = value; Validate(false);
        }

        public void Validate(bool setCustom = false)
        {
            EnsureKeys();
            keys.Sort((a, b) => a.Time.CompareTo(b.Time));
            keys[0].Time = 0f; keys[^1].Time = 1f;
            if (constraint == ProgressCurveConstraint.Monotonic) { keys[0].Value = 0f; keys[^1].Value = 1f; }
            for (int i = 0; i < keys.Count; i++)
            {
                ProgressCurveKey key = keys[i]; key.EnsureId(); key.Time = Mathf.Clamp01(key.Time);
                if (constraint == ProgressCurveConstraint.Monotonic)
                {
                    float min = i == 0 ? 0f : keys[i - 1].Value;
                    key.Value = Mathf.Clamp(key.Value, min, 1f);
                }
            }
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i].TangentMode != ProgressTangentMode.Auto) continue;
                ProgressCurveKey previous = keys[Mathf.Max(0, i - 1)];
                ProgressCurveKey next = keys[Mathf.Min(keys.Count - 1, i + 1)];
                Vector2 direction = new(next.Time - previous.Time, next.Value - previous.Value);
                float inTime = i > 0 ? (keys[i].Time - previous.Time) / 3f : 0f;
                float outTime = i < keys.Count - 1 ? (next.Time - keys[i].Time) / 3f : 0f;
                float slope = Mathf.Abs(direction.x) > 0.000001f ? direction.y / direction.x : 0f;
                keys[i].InHandle = new Vector2(-inTime, -inTime * slope);
                keys[i].OutHandle = new Vector2(outTime, outTime * slope);
            }
            for (int i = 0; i < keys.Count - 1; i++) ConstrainSegment(keys[i], keys[i + 1]);
            if (setCustom) preset = ProgressCurvePreset.Custom;
        }

        private void ConstrainSegment(ProgressCurveKey a, ProgressCurveKey b)
        {
            float span = Mathf.Max(0.0001f, b.Time - a.Time);
            Vector2 ah = a.OutHandle, bh = b.InHandle;
            ah.x = Mathf.Clamp(ah.x, 0f, span); bh.x = Mathf.Clamp(bh.x, -span, 0f);
            if (a.Time + ah.x > b.Time + bh.x)
            {
                float mid = (a.Time + ah.x + b.Time + bh.x) * 0.5f;
                ah.x = mid - a.Time; bh.x = mid - b.Time;
            }
            if (constraint == ProgressCurveConstraint.Monotonic)
            {
                float c1 = Mathf.Clamp(a.Value + ah.y, a.Value, b.Value);
                float c2 = Mathf.Clamp(b.Value + bh.y, c1, b.Value);
                ah.y = c1 - a.Value; bh.y = c2 - b.Value;
            }
            a.OutHandle = ah; b.InHandle = bh;
        }

        private void EnsureKeys()
        {
            keys ??= new List<ProgressCurveKey>();
            if (keys.Count < 2) ApplyPreset(ProgressCurvePreset.Linear);
        }

        private static float Cubic(float a, float b, float c, float d, float t)
        { float u = 1f - t; return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d; }

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() => Validate();
    }
}
