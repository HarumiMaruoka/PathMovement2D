using System;
using System.Collections.Generic;
using Game.PathMovement2D.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.PathMovement2D.Tests
{
    public sealed class PathMovementClipTests
    {
        private readonly List<Object> objects = new();
        private PathMovementClip2D clip;
        private Transform target;
        private T Track<T>(T value) where T : Object { objects.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            clip = Track(ScriptableObject.CreateInstance<PathMovementClip2D>());
            clip.Points[0].SegmentType = PathSegmentType.Linear;
            clip.Points[1].Position = new Vector2(10f, 0f);
            clip.Rebuild();
            target = Track(new GameObject("Movement target")).transform;
        }
        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [Test]
        public void InstancesHaveIndependentClocksAndData()
        {
            var a = Track(clip.CreatePlaybackInstance());
            var b = Track(clip.CreatePlaybackInstance());
            Transform other = Track(new GameObject("Other")).transform;
            a.Update(target, 0.25f); b.Update(other, 0.75f);
            Assert.That(a.Time, Is.EqualTo(0.25f)); Assert.That(b.Time, Is.EqualTo(0.75f));
            Assert.That(target.position.x, Is.EqualTo(2.5f).Within(0.01f));
            Assert.That(other.position.x, Is.EqualTo(7.5f).Within(0.01f));
            a.Points[1].Position = Vector2.up;
            Assert.That(clip.Points[1].Position.x, Is.EqualTo(10f));
            Assert.That(b.Points[1].Position.x, Is.EqualTo(10f));
            Assert.That(clip.Time, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => clip.Update(target, 0.1f));
        }

        [Test]
        public void DeltaUpdateNeedsNoTargetAndStopsAtOnceEnd()
        {
            // A nonzero start point must not be included in the first delta.
            clip.Points[0].Position = new Vector2(3, 4);
            clip.Points[1].Position = new Vector2(13, 4);
            clip.Rebuild();
            var instance = Track(clip.CreatePlaybackInstance());
            Assert.That(Vector2.Distance(instance.Update(0.25f), new Vector2(2.5f, 0)), Is.LessThan(0.01f));
            Assert.That(instance.IsComplete, Is.False);
            Assert.That(Vector2.Distance(instance.Update(2f), new Vector2(7.5f, 0)), Is.LessThan(0.01f));
            Assert.That(instance.Time, Is.EqualTo(1f));
            Assert.That(instance.IsComplete, Is.True);
            Assert.That(instance.Update(1f), Is.EqualTo(Vector2.zero));
            instance.Reset();
            Assert.That(instance.IsComplete, Is.False);
            Assert.That(instance.Update(0f), Is.EqualTo(Vector2.zero));
            Assert.That(instance.Update(0.25f).x, Is.EqualTo(2.5f).Within(0.01f));
        }

        [TestCase(PathLoopMode.Loop, 0.75f, 0.5f, -5f)]
        [TestCase(PathLoopMode.Loop, 0f, 3.25f, 2.5f)]
        [TestCase(PathLoopMode.PingPong, 0.75f, 0.5f, 0f)]
        [TestCase(PathLoopMode.PingPong, 1f, 0.25f, -2.5f)]
        public void DeltaUpdateMatchesSampleDisplacement(PathLoopMode mode, float start, float step, float expected)
        {
            clip.LoopMode = mode;
            var instance = Track(clip.CreatePlaybackInstance());
            instance.Update(start);
            Assert.That(instance.Update(step).x, Is.EqualTo(expected).Within(0.01f));
            Assert.That(instance.IsComplete, Is.False);
        }

        [Test]
        public void DeltaUsesClipAxesAndMirrorWithoutMovingCapturedTarget()
        {
            var instance = Track(clip.CreatePlaybackInstance());
            target.SetPositionAndRotation(new Vector3(3, 4, 7), Quaternion.Euler(0, 0, 90));
            instance.CaptureOrigin(target);
            instance.MirrorX = true;
            Assert.That(Vector2.Distance(instance.Update(0.25f), new Vector2(-2.5f, 0)), Is.LessThan(0.01f));
            Assert.That(target.position, Is.EqualTo(new Vector3(3, 4, 7)));
            // Evaluate does not seek the clock used by the delta API.
            instance.Evaluate(target, 1f);
            Assert.That(instance.Update(0.25f).x, Is.EqualTo(-2.5f).Within(0.01f));
            instance.Update(target, 0.25f);
            Assert.That(instance.Time, Is.EqualTo(0.75f));
        }

        [Test]
        public void DeltaRejectsAssetsAndInvalidTimeWithoutAdvancing()
        {
            Assert.Throws<InvalidOperationException>(() => clip.Update(0.1f));
            var instance = Track(clip.CreatePlaybackInstance());
            foreach (float step in new[] { -1f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => instance.Update(step));
            Assert.That(instance.Time, Is.Zero);
        }

        [Test]
        public void ScrubbingAndResetKeepTheCapturedOrigin()
        {
            var instance = Track(clip.CreatePlaybackInstance());
            target.SetPositionAndRotation(new Vector3(3, 4, 7), Quaternion.Euler(0, 0, 90));
            instance.Update(target, 0.25f);
            instance.Evaluate(target, 1f);
            Assert.That(Vector3.Distance(target.position, new Vector3(3, 14, 7)), Is.LessThan(0.01f));
            Assert.That(instance.Time, Is.EqualTo(0.25f));
            instance.Reset();
            Assert.That(target.position.y, Is.EqualTo(14f).Within(0.01f));
            instance.Evaluate(target, 0.5f);
            Assert.That(target.position.y, Is.EqualTo(9f).Within(0.01f));
            instance.Evaluate(target, 0f);
            Assert.That(target.position.y, Is.EqualTo(4f).Within(0.01f));
        }

        [TestCase(PathLoopMode.Once, 3.25f, 10f)]
        [TestCase(PathLoopMode.Loop, 3.25f, 2.5f)]
        [TestCase(PathLoopMode.Loop, 1f, 0f)]
        [TestCase(PathLoopMode.PingPong, 3.25f, 7.5f)]
        [TestCase(PathLoopMode.PingPong, 2f, 0f)]
        public void BoundariesAndLargeSteps(PathLoopMode mode, float time, float x)
        {
            clip.LoopMode = mode;
            var instance = Track(clip.CreatePlaybackInstance());
            instance.Update(target, time);
            Assert.That(target.position.x, Is.EqualTo(x).Within(0.01f));
            Assert.That(instance.IsComplete, Is.EqualTo(mode == PathLoopMode.Once));
        }

        [Test]
        public void MirrorAndPingPongChangeFacing()
        {
            clip.LoopMode = PathLoopMode.PingPong;
            var outward = clip.Sample(0.5f, true);
            var returning = clip.Sample(1.5f, true);
            Assert.That(outward.Position.x, Is.EqualTo(-5f).Within(0.01f));
            Assert.That(outward.Tangent.x, Is.LessThan(0f));
            Assert.That(returning.Tangent.x, Is.GreaterThan(0f));
        }

        [Test]
        public void WorldAndReferenceSpaces()
        {
            var instance = Track(clip.CreatePlaybackInstance());
            target.position = new Vector3(100, 100, 9);
            instance.Space = PathClipSpace.World;
            instance.Evaluate(target, 0.5f);
            Assert.That(target.position.x, Is.EqualTo(5f).Within(0.01f));
            Transform basis = Track(new GameObject("Basis")).transform;
            basis.position = new Vector3(2, 3, 0);
            basis.rotation = Quaternion.Euler(0, 0, 90);
            instance.Space = PathClipSpace.ReferenceTransform; instance.ReferenceTransform = basis;
            instance.Evaluate(target, 0.5f);
            Assert.That(Vector3.Distance(target.position, new Vector3(2, 8, 9)), Is.LessThan(0.01f));
            instance.ReferenceTransform = target;
            Assert.Throws<InvalidOperationException>(() => instance.Evaluate(target, 0f));
        }

        [Test]
        public void MultipleSegmentsInterpolateAcrossBoundaryAndScaleChanges()
        {
            Path2D path = Track(new GameObject("Path")).AddComponent<Path2D>();
            path.GetPoint(0).Position = Vector2.zero;
            path.GetPoint(0).SegmentType = PathSegmentType.Linear;
            path.GetPoint(1).Position = new Vector2(10, 0);
            path.GetPoint(1).SegmentType = PathSegmentType.Linear;
            path.AddPoint(new Vector2(10, 10));
            Assert.That(Vector2.Distance(path.EvaluatePosition(0.75f), new Vector2(10, 5)), Is.LessThan(0.01f));
            path.transform.localScale = new Vector3(2, 1, 1);
            Assert.That(path.GetLength(), Is.EqualTo(30f).Within(0.01f));
            Assert.That(Vector2.Distance(path.EvaluatePosition(0.5f), new Vector2(15, 0)), Is.LessThan(0.01f));
        }

        [Test]
        public void ImportPreservesTransformedBezierShapeAndDoesNotSharePoints()
        {
            Path2D path = Track(new GameObject("Path")).AddComponent<Path2D>();
            path.AddPoint(new Vector2(4, 3));
            path.transform.localScale = new Vector3(3, 0.5f, 1);
            path.transform.rotation = Quaternion.Euler(0, 0, 35);
            path.transform.position = new Vector3(20, 10, 0);
            clip.CopyFrom(path);
            Vector2 first = path.EvaluatePosition(0f);
            Quaternion rotation = path.transform.rotation;
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                Vector2 actual = first + (Vector2)(rotation * clip.Sample(t).Position);
                Assert.That(Vector2.Distance(actual, path.EvaluatePosition(t)), Is.LessThan(0.03f));
            }
            clip.Points[0].Position = Vector2.one;
            Assert.That(path.GetPoint(0).Position, Is.Not.EqualTo(Vector2.one));
        }

        [Test]
        public void ClipEditsSupportUndoRedo()
        {
            Undo.RecordObject(clip, "Move Point");
            clip.Points[1].Position = new Vector2(20, 0);
            clip.Rebuild(); Undo.FlushUndoRecordObjects();
            Undo.PerformUndo(); clip.Rebuild();
            Assert.That(clip.Sample(1f).Position.x, Is.EqualTo(10f).Within(0.01f));
            Undo.PerformRedo(); clip.Rebuild();
            Assert.That(clip.Sample(1f).Position.x, Is.EqualTo(20f).Within(0.01f));
            Undo.ClearUndo(clip);
        }

        [Test]
        public void PreviewRestoresMovementAndAnimationProperties()
        {
            if (AnimationMode.InAnimationMode()) Assert.Ignore("Another animation preview is active.");
            string controllerPath = "Assets/PathClipTest-" + Guid.NewGuid().ToString("N") + ".controller";
            var animator = target.gameObject.AddComponent<Animator>();
            var animation = Track(new AnimationClip());
            animation.SetCurve("", typeof(Transform), "localScale.x", AnimationCurve.Linear(0, 1, 1, 3));
            animation.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 100, 1, 200));
            Vector3 originalPosition = new(3, 4, 7);
            target.position = originalPosition;
            try
            {
                animator.runtimeAnimatorController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                using (var session = new PathClipPreviewSession(clip, target, PathClipSpace.Relative, null, false))
                {
                    session.Evaluate(0.5f, animator, animation, 0.5f);
                    Assert.That(target.position.x, Is.EqualTo(8f).Within(0.01f));
                    Assert.That(target.localScale.x, Is.EqualTo(2f).Within(0.01f));
                    session.Evaluate(0.25f, animator, animation, 0.25f);
                    Assert.That(target.position.x, Is.EqualTo(5.5f).Within(0.01f));
                }
                Assert.That(Vector3.Distance(target.position, originalPosition), Is.LessThan(0.001f));
                Assert.That(target.localScale, Is.EqualTo(Vector3.one));
                Assert.That(AnimationMode.InAnimationMode(), Is.False);
            }
            finally { animator.runtimeAnimatorController = null; AssetDatabase.DeleteAsset(controllerPath); }
        }
    }
}
