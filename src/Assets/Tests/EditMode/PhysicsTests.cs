using System.Collections.Generic;
using NUnit.Framework;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Tests
{
    public class SweepTests
    {
        static readonly List<Solid> Roof = new List<Solid> { new Solid(new Rect(0f, 0f, 10f, 20f), SolidKind.Building, false) };

        [Test]
        public void FallingBox_HitsRoofTop_WithUpNormal()
        {
            var box = new Rect(4f, 22f, 0.5f, 1.8f); // feet 2 m above the roof
            Assert.IsTrue(Sweep.Cast(box, new Vector2(0f, -4f), Roof, out var hit));
            Assert.AreEqual(0.5f, hit.T, 1e-4f);
            Assert.AreEqual(Vector2.up, hit.Normal);
        }

        [Test]
        public void BoxMovingRight_HitsWall_WithLeftNormal()
        {
            var box = new Rect(-2f, 5f, 0.5f, 1.8f);
            Assert.IsTrue(Sweep.Cast(box, new Vector2(3f, 0f), Roof, out var hit));
            Assert.AreEqual(Vector2.left, hit.Normal);
            Assert.AreEqual(0.5f, hit.T, 1e-4f); // gap 1.5 m of 3 m
        }

        [Test]
        public void StandingOnRoof_MovingSideways_DoesNotHit()
        {
            var box = new Rect(4f, 20f, 0.5f, 1.8f);
            Assert.IsFalse(Sweep.Cast(box, new Vector2(1f, 0f), Roof, out _));
        }

        [Test]
        public void StandingOnRoof_MovingUp_DoesNotHit()
        {
            var box = new Rect(4f, 20f, 0.5f, 1.8f);
            Assert.IsFalse(Sweep.Cast(box, new Vector2(0.5f, 1f), Roof, out _));
        }

        [Test]
        public void TouchingRoof_MovingDown_HitsImmediately()
        {
            var box = new Rect(4f, 20f, 0.5f, 1.8f);
            Assert.IsTrue(Sweep.Cast(box, new Vector2(0f, -0.1f), Roof, out var hit));
            Assert.AreEqual(0f, hit.T, 1e-4f);
        }

        [Test]
        public void SlidingDownAlongWall_DoesNotHit()
        {
            var box = new Rect(10f, 5f, 0.5f, 1.8f); // touching the right wall face
            Assert.IsFalse(Sweep.Cast(box, new Vector2(0f, -1f), Roof, out _));
        }
    }

    public class FlightModelTests
    {
        static Vector2 Settle(FlightParams p, float canopy, Vector2 v, float seconds = 60f)
        {
            for (float t = 0f; t < seconds; t += 1f / 120f) v = FlightModel.Step(v, canopy, p, 1f / 120f);
            return v;
        }

        [Test]
        public void Freefall_ReachesTerminalSpeed()
        {
            var cfg = TestKit.Flight();
            var v = Settle(new FlightParams(cfg, 0f), 0f, Vector2.zero);
            Assert.AreEqual(-cfg.FreefallTerminalSpeed, v.y, 0.5f);
        }

        [Test]
        public void FullCanopy_DescendsSlowly_AndDriftsWithWindPlusGlide()
        {
            var cfg = TestKit.Flight();
            var v = Settle(new FlightParams(cfg, 2f), 1f, new Vector2(8f, -30f));
            Assert.That(-v.y, Is.InRange(cfg.CanopyTerminalSpeed * 0.85f, cfg.CanopyTerminalSpeed * 1.01f));
            Assert.AreEqual(2f + cfg.CanopyGlideSpeed, v.x, 0.3f);
        }

        [Test]
        public void HeadWind_PushesCanopyBack()
        {
            var cfg = TestKit.Flight();
            var v = Settle(new FlightParams(cfg, -4f), 1f, new Vector2(8f, -30f));
            Assert.Less(v.x, 0f);
        }
    }

    public class LandingJudgeTests
    {
        [Test]
        public void SlowTouchdownOnTop_Lands()
        {
            Assert.AreEqual(ContactResult.Landed, LandingJudge.Judge(Vector2.up, new Vector2(2f, -4.5f), TestKit.Flight()));
        }

        [Test]
        public void FastTouchdownOnTop_Crashes()
        {
            Assert.AreEqual(ContactResult.Crash, LandingJudge.Judge(Vector2.up, new Vector2(3f, -20f), TestKit.Flight()));
        }

        [Test]
        public void FastIntoWall_Crashes_SlowGrazes()
        {
            var cfg = TestKit.Flight();
            Assert.AreEqual(ContactResult.Crash, LandingJudge.Judge(Vector2.left, new Vector2(8f, -10f), cfg));
            Assert.AreEqual(ContactResult.Slide, LandingJudge.Judge(Vector2.left, new Vector2(1f, -20f), cfg));
        }
    }
}
