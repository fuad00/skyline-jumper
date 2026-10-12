using System.Collections.Generic;
using NUnit.Framework;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Tests
{
    public class JumpSessionTests
    {
        [Test]
        public void FourTaps_GoThroughOriginalPhases()
        {
            var s = TestKit.Session();
            var events = new List<JumpEvent>();
            s.Happened += events.Add;
            Assert.AreEqual(JumpPhase.Ready, s.Phase);

            s.Tap();
            Assert.AreEqual(JumpPhase.BackingUp, s.Phase);
            TestKit.StepFor(s, 1f);
            Assert.Less(s.Feet.x, s.Level.StartFeetX, "tap 1 walks back from the edge");

            s.Tap();
            Assert.AreEqual(JumpPhase.Running, s.Phase);
            while (s.Phase == JumpPhase.Running && s.Feet.x < s.Level.EdgeX - 0.05f) s.Step(s.StepSize);

            s.Tap();
            Assert.AreEqual(JumpPhase.Airborne, s.Phase);
            Assert.Greater(s.Velocity.y, 0f, "tap 3 jumps up");
            TestKit.StepFor(s, 0.5f);

            s.Tap();
            Assert.IsTrue(s.CanopyDeployed, "tap 4 opens the parachute");
            CollectionAssert.AreEqual(new[] { JumpEvent.StartedBackingUp, JumpEvent.StartedRunning, JumpEvent.Jumped, JumpEvent.CanopyDeployed }, events);
        }

        [Test]
        public void BackingUp_StopsAtFarSideOfRoof()
        {
            var s = TestKit.Session();
            s.Tap();
            TestKit.StepFor(s, 20f);
            Assert.AreEqual(JumpPhase.AtBackWall, s.Phase);
            Assert.AreEqual(s.Level.BackLimitX, s.Feet.x, 1e-4f);
        }

        [Test]
        public void LongerRunUp_GivesFasterTakeoff()
        {
            Assert.Greater(TakeoffSpeed(5f), TakeoffSpeed(1.5f) + 1f);
        }

        static float TakeoffSpeed(float runUp)
        {
            var s = TestKit.Session();
            var pilot = new Autopilot { DesiredRunUp = runUp };
            while (s.Phase != JumpPhase.Airborne) { pilot.Update(s); s.Step(s.StepSize); }
            return s.Velocity.x;
        }

        [Test]
        public void MissingTheEdge_Stumbles_AndDies()
        {
            var s = TestKit.Session();
            var events = new List<JumpEvent>();
            s.Happened += events.Add;
            TestKit.RunToEnd(s, new Autopilot { NeverJump = true });
            Assert.AreEqual(JumpPhase.Crashed, s.Phase);
            Assert.AreEqual(JumpOutcome.Died, s.Outcome);
            Assert.Contains(JumpEvent.Stumbled, events);
        }

        [Test]
        public void NoParachute_Crashes()
        {
            var s = TestKit.Session();
            TestKit.RunToEnd(s, new Autopilot { NeverOpenCanopy = true });
            Assert.AreEqual(JumpOutcome.Died, s.Outcome);
            Assert.Greater(s.ImpactVelocity.magnitude, TestKit.Flight().SafeLandingSpeed);
        }

        [Test]
        public void Autopilot_LandsNearArrow_OnLevel1()
        {
            var s = TestKit.Session();
            TestKit.RunToEnd(s, new Autopilot());
            Assert.AreEqual(JumpOutcome.WellDone, s.Outcome, $"landed at x={s.Feet.x:0.00}, phase {s.Phase}");
            Assert.Less(s.LandingDistance, 1.5f);
            Assert.Less(s.Elapsed, s.Level.TimeLimit);
        }

        [Test]
        public void SafeLandingOffTarget_IsMissedPlatform()
        {
            var level = TestKit.Level(wind: 0f);
            level.Target = new BuildingSpec(200f, 6f, 10f, BuildingStyle.Brick); // unreachable
            level.Others = new BuildingSpec[0];
            var s = TestKit.Session(level);
            TestKit.RunToEnd(s, new Autopilot { DesiredRunUp = 2f, OpenRightAway = true });
            Assert.AreEqual(JumpPhase.Landed, s.Phase);
            Assert.AreEqual(JumpOutcome.MissedPlatform, s.Outcome);
        }

        [Test]
        public void WindPinnedAgainstWall_SlidesDown_DoesNotHang()
        {
            // Regression: short run-up + instant canopy drifts into the target's wall just below its roof.
            var s = TestKit.Session();
            TestKit.RunToEnd(s, new Autopilot { DesiredRunUp = 2f, OpenRightAway = true });
            Assert.AreNotEqual(JumpOutcome.TimesUp, s.Outcome, $"stuck at {s.Feet}");
            Assert.IsTrue(s.Phase.IsFinished());
        }

        [Test]
        public void WaitingTooLong_IsTimesUp()
        {
            var level = TestKit.Level();
            level.TimeLimit = 3f;
            var s = TestKit.Session(level);
            s.Tap();
            TestKit.StepFor(s, 4f);
            Assert.AreEqual(JumpPhase.TimeUp, s.Phase);
            Assert.AreEqual(JumpOutcome.TimesUp, s.Outcome);
        }

        [Test]
        public void NextAttempt_RemembersWhereLastRunStarted()
        {
            var s = TestKit.Session();
            Assert.IsNaN(s.LastRunStartX, "no flag before the first attempt");
            s.Tap();
            TestKit.StepFor(s, 1.2f);
            s.Tap();
            float start = s.RunStartX;
            Assert.Less(start, s.Level.StartFeetX);
            TestKit.StepFor(s, 0.5f);
            s.Reset();
            Assert.AreEqual(start, s.LastRunStartX, 1e-5f);
            s.Reset(); // gave up before running: keep the old flag
            Assert.AreEqual(start, s.LastRunStartX, 1e-5f);
            s.ForgetHistory();
            Assert.IsNaN(s.LastRunStartX);
        }

        [Test]
        public void TimerStartsOnlyWithFirstTap()
        {
            var s = TestKit.Session();
            TestKit.StepFor(s, 5f);
            Assert.AreEqual(0f, s.Elapsed);
            Assert.AreEqual(JumpPhase.Ready, s.Phase);
        }

        [Test]
        public void SameTaps_GiveSameJump()
        {
            Vector2 a = Replay(), b = Replay();
            Assert.AreEqual(a.x, b.x, 0f);
            Assert.AreEqual(a.y, b.y, 0f);
        }

        static Vector2 Replay()
        {
            var s = TestKit.Session();
            int[] tapsAt = { 0, 150, 260, 520 }; // step indices
            for (int i = 0, k = 0; i < 2000 && !s.Phase.IsFinished(); i++)
            {
                if (k < tapsAt.Length && i == tapsAt[k]) { s.Tap(); k++; }
                s.Step(s.StepSize);
            }
            return s.Feet;
        }

        [Test]
        public void Advance_UsesFixedSteps_RegardlessOfFrameRate()
        {
            var a = TestKit.Session();
            var b = TestKit.Session();
            a.Tap();
            b.Tap();
            for (int i = 0; i < 60; i++) a.Advance(1f / 60f);
            for (int i = 0; i < 30; i++) b.Advance(1f / 30f);
            Assert.AreEqual(a.Feet.x, b.Feet.x, 0.03f); // at most one fixed step apart
        }
    }

    public class ScoringTests
    {
        [Test]
        public void Failures_GiveNothing()
        {
            var level = TestKit.Level();
            foreach (var o in new[] { JumpOutcome.Died, JumpOutcome.MissedPlatform, JumpOutcome.TimesUp })
            {
                var s = Scoring.Evaluate(o, 0.1f, 5f, 3f, level, TestKit.Score());
                Assert.AreEqual(0, s.Points);
                Assert.AreEqual(0, s.Stars);
            }
        }

        [Test]
        public void Stars_FollowDistanceAndTime()
        {
            var level = TestKit.Level();
            var score = TestKit.Score();
            Assert.AreEqual(1, Scoring.Evaluate(JumpOutcome.WellDone, 2.5f, 8f, 3.4f, level, score).Stars);
            Assert.AreEqual(2, Scoring.Evaluate(JumpOutcome.WellDone, 1.0f, 8f, 3.4f, level, score).Stars);
            Assert.AreEqual(3, Scoring.Evaluate(JumpOutcome.WellDone, 0.3f, 8f, 3.4f, level, score).Stars);
            Assert.AreEqual(2, Scoring.Evaluate(JumpOutcome.WellDone, 0.3f, level.ThreeStarTime + 1f, 3.4f, level, score).Stars, "too slow for 3 stars");
        }

        [Test]
        public void Points_MaxAtArrowInstantly_DropWithDistanceAndTime()
        {
            var level = TestKit.Level();
            var score = TestKit.Score();
            Assert.AreEqual(score.AccuracyPoints + score.TimePoints, Scoring.Evaluate(JumpOutcome.WellDone, 0f, 0f, 3.4f, level, score).Points);
            int near = Scoring.Evaluate(JumpOutcome.WellDone, 0.5f, 8f, 3.4f, level, score).Points;
            int far = Scoring.Evaluate(JumpOutcome.WellDone, 2.5f, 8f, 3.4f, level, score).Points;
            int slow = Scoring.Evaluate(JumpOutcome.WellDone, 0.5f, 20f, 3.4f, level, score).Points;
            Assert.Greater(near, far);
            Assert.Greater(near, slow);
        }

        [Test]
        public void Best_IsTheClosestDistance()
        {
            Assert.IsTrue(Scoring.IsNewBest(0.4f, float.NaN));
            Assert.IsTrue(Scoring.IsNewBest(0.4f, 0.9f));
            Assert.IsFalse(Scoring.IsNewBest(0.9f, 0.4f));
        }
    }

    public class LevelLayoutTests
    {
        [Test]
        public void Level1_HasStartEdgeAtZero_AndArrowOnTargetRoof()
        {
            var level = TestKit.Level();
            var layout = LevelLayout.Build(level, TestKit.Jump());
            Assert.AreEqual(0f, layout.EdgeX, 1e-4f);
            Assert.AreEqual(level.Start.Height, layout.RoofY);
            Assert.IsTrue(layout.Solids[layout.TargetIndex].IsTarget);
            Assert.That(layout.ArrowX, Is.InRange(layout.TargetRect.xMin, layout.TargetRect.xMax));
            Assert.Less(layout.StartFeetX, layout.EdgeX);
        }
    }
}
