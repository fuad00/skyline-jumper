using NUnit.Framework;
using StickmanJumper.Config;
using StickmanJumper.Presentation;
using UnityEngine;

namespace StickmanJumper.Tests
{
    public class AnimationTests
    {
        static StickmanConfig Cfg() => ScriptableObject.CreateInstance<StickmanConfig>();

        [Test]
        public void LegIK_PutsFootOnTarget_KneeForward()
        {
            var c = Cfg();
            float leg = c.UpperLegLength + c.LowerLegLength;
            var hip = new Vector2(0f, leg * 0.95f);
            // Reachable targets (inside the leg's length) must be hit exactly.
            foreach (var foot in new[] { new Vector2(leg * 0.35f, 0.05f), new Vector2(-leg * 0.35f, 0.1f), new Vector2(0.1f, leg * 0.45f) })
            {
                StickmanPose.LegIK(hip, foot, c.UpperLegLength, c.LowerLegLength, out float thigh, out float knee);
                Vector2 kneePos = hip + StickmanPose.Dir(thigh) * c.UpperLegLength;
                Vector2 ankle = kneePos + StickmanPose.Dir(thigh + knee) * c.LowerLegLength;
                Assert.Less(Vector2.Distance(ankle, foot), 1e-3f, $"foot {foot}");
                Assert.LessOrEqual(knee, 0.01f, "knee bends backwards (shin behind thigh)");
            }
        }

        [TestCase(2.5f)]
        [TestCase(6f)]
        [TestCase(10f)]
        public void StanceFoot_DoesNotSlide(float speed)
        {
            var c = Cfg();
            float stride = StickmanPose.Stride(speed, c);
            float duty = StickmanPose.Duty(speed);
            const float dt = 1f / 240f;
            float phase = 0.02f, bodyX = 0f;
            float plantedX = bodyX + StickmanPose.FootPosition(phase, speed, c).x;
            while (phase < duty - 0.02f)
            {
                bodyX += speed * dt;
                phase += speed * dt / stride;
                float worldX = bodyX + StickmanPose.FootPosition(phase, speed, c).x;
                Assert.AreEqual(plantedX, worldX, 0.01f, $"speed {speed}, phase {phase:0.00}");
                Assert.AreEqual(0f, StickmanPose.FootPosition(phase, speed, c).y, 1e-5f);
            }
        }

        [Test]
        public void SwingFoot_LiftsOffTheRoof()
        {
            var c = Cfg();
            float duty = StickmanPose.Duty(8f);
            float mid = duty + (1f - duty) * 0.5f;
            Assert.Greater(StickmanPose.FootPosition(mid, 8f, c).y, 0.15f);
        }
    }
}
