using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.PathMovement2D
{
    [Serializable]
    internal sealed class PathArcLengthTable
    {
        [Serializable]
        internal struct Sample
        {
            public float distance;
            public int segment;
            public float parameter;
        }

        [SerializeField] private List<Sample> samples = new();
        [SerializeField] private float length;

        public float Length => length;
        public int Count => samples.Count;

        public void Clear()
        {
            samples.Clear();
            length = 0f;
        }

        public void Add(float distance, int segment, float parameter)
        {
            samples.Add(new Sample { distance = distance, segment = segment, parameter = parameter });
            length = distance;
        }

        public void Map(float distance, out int segment, out float parameter)
        {
            if (samples.Count == 0) { segment = 0; parameter = 0f; return; }
            if (distance <= 0f) { segment = samples[0].segment; parameter = samples[0].parameter; return; }
            if (distance >= length) { var last = samples[^1]; segment = last.segment; parameter = last.parameter; return; }

            int low = 0, high = samples.Count - 1;
            while (high - low > 1)
            {
                int mid = (low + high) >> 1;
                if (samples[mid].distance < distance) low = mid; else high = mid;
            }

            Sample a = samples[low];
            Sample b = samples[high];
            if (a.segment != b.segment)
            {
                segment = b.segment;
                parameter = b.parameter;
                return;
            }
            float span = b.distance - a.distance;
            float u = span > 0.000001f ? (distance - a.distance) / span : 0f;
            segment = a.segment;
            parameter = Mathf.LerpUnclamped(a.parameter, b.parameter, u);
        }
    }
}
