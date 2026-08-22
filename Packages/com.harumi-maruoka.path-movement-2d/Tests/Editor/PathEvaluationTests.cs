using NUnit.Framework;
using UnityEngine;

namespace Game.PathMovement2D.Tests
{
    public sealed class PathEvaluationTests
    {
        private GameObject gameObject;
        private Path2D path;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("Path Test");
            path = gameObject.AddComponent<Path2D>();
            path.GetPoint(0).Position = Vector2.zero;
            path.GetPoint(0).SegmentType = PathSegmentType.Linear;
            path.GetPoint(1).Position = new Vector2(10f, 0f);
            path.Rebuild();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(gameObject);

        [Test]
        public void LinearPathUsesDistanceProgress()
        {
            Assert.That(path.GetLength(), Is.EqualTo(10f).Within(0.001f));
            Assert.That(Vector2.Distance(path.EvaluatePosition(0.5f), new Vector2(5f, 0f)), Is.LessThan(0.001f));
        }

        [Test]
        public void LocalPathFollowsTransform()
        {
            gameObject.transform.position = new Vector3(3f, 4f, 0f);
            Assert.That(Vector2.Distance(path.EvaluatePosition(0f), new Vector2(3f, 4f)), Is.LessThan(0.001f));
        }
    }
}
