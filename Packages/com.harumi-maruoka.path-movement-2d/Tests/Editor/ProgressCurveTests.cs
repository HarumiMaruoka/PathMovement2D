using NUnit.Framework;

namespace Game.PathMovement2D.Tests
{
    public sealed class ProgressCurveTests
    {
        [Test]
        public void LinearPresetEvaluatesLinearly()
        {
            var curve = new ProgressCurve();
            Assert.That(curve.Evaluate(0.25f), Is.EqualTo(0.25f).Within(0.002f));
            Assert.That(curve.Evaluate(0.75f), Is.EqualTo(0.75f).Within(0.002f));
        }

        [Test]
        public void MonotonicCurveNeverMovesBackward()
        {
            var curve = new ProgressCurve();
            curve.ApplyPreset(ProgressCurvePreset.EaseInOut);
            float previous = curve.Evaluate(0f);
            for (int i = 1; i <= 100; i++)
            {
                float current = curve.Evaluate(i / 100f);
                Assert.That(current, Is.GreaterThanOrEqualTo(previous - 0.0001f));
                previous = current;
            }
        }
    }
}
