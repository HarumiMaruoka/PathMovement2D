using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Game.PathMovement2D.Editor
{
    internal sealed class PathClipPreviewSession : IDisposable
    {
        private readonly AnimationModeDriver driver;
        private readonly Transform target;
        private readonly List<PropertyModification> modifications = new();
        public PathMovementClip2D Instance { get; }
        public bool Active => driver != null && AnimationMode.InAnimationMode(driver);

        public PathClipPreviewSession(PathMovementClip2D clip, Transform target, PathClipSpace space,
            Transform reference, bool mirror)
        {
            if (AnimationMode.InAnimationMode()) throw new InvalidOperationException("Stop the other animation preview first.");
            this.target = target;
            Instance = clip.CreatePlaybackInstance();
            Instance.Space = space;
            Instance.ReferenceTransform = reference;
            Instance.MirrorX = mirror;
            Instance.CaptureOrigin(target);
            driver = ScriptableObject.CreateInstance<AnimationModeDriver>();
            driver.hideFlags = HideFlags.HideAndDontSave;
            var serialized = new SerializedObject(target);
            foreach (string property in new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" })
            {
                modifications.Add(new PropertyModification { target = target, propertyPath = property,
                    value = serialized.FindProperty(property).floatValue.ToString("R", CultureInfo.InvariantCulture) });
            }
            AnimationMode.StartAnimationMode(driver);
        }

        public void Evaluate(float movementTime, Animator animator, AnimationClip animation, float animationTime)
        {
            if (!Active || target == null) return;
            AnimationMode.BeginSampling();
            try
            {
                if (animator != null && animation != null && animator.runtimeAnimatorController != null)
                    AnimationMode.SampleAnimationClip(animator.gameObject, animation, animationTime);
                foreach (PropertyModification modification in modifications)
                {
                    AnimationMode.AddPropertyModification(EditorCurveBinding.FloatCurve("", typeof(Transform), modification.propertyPath),
                        modification, true);
                }
                Instance.Evaluate(target, movementTime);
            }
            finally { AnimationMode.EndSampling(); }
        }

        public void Dispose()
        {
            if (Active) AnimationMode.StopAnimationMode(driver);
            if (Instance != null) UnityEngine.Object.DestroyImmediate(Instance);
            if (driver != null) UnityEngine.Object.DestroyImmediate(driver);
        }
    }
}
