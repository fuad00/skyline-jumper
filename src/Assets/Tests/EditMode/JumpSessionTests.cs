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
            Assert.Greater(TakeoffSpeed(6f), TakeoffSpeed(3.2f) + 1f);
        }

        const float Px = 1f / 22f;

        /// <summary>Отход как в оригинале: с 63 px/с, +0.225 за тик, не быстрее 120 px/с.</summary>
        [Test]
        public void WalkBack_SpeedsUpFrom63To120()
        {
            var s = TestKit.Session();
            s.Tap();
            s.Step(s.StepSize);
            Assert.AreEqual(-(63f + 0.225f) * Px, s.Velocity.x, 1e-5f);
            for (int i = 0; i < 60; i++) s.Step(s.StepSize);
            Assert.AreEqual(-(63f + 0.225f * 61f) * Px, s.Velocity.x, 1e-3f);
            float v = 63f; // the default roof is too short to reach the cap: check it on the formula
            for (int i = 0; i < 600; i++) if (v < 120f) v += 0.225f;
            Assert.AreEqual(120f, v, 0.25f);
        }

        /// <summary>Разбег как в оригинале: с 10 px/с, прибавка за тик 3 → 1 (при 500 px/с): за 1 с ≈170 px/с, за 3 с ≈393 px/с.</summary>
        [Test]
        public void Run_AcceleratesLikeTheOriginal()
        {
            var s = TestKit.Session();
            float v = 10f * Px;
            for (int i = 0; i < 60; i++) v = s.RunSpeedAfter(v, s.StepSize);
            Assert.AreEqual(169.94f, v / Px, 0.1f);
            for (int i = 0; i < 120; i++) v = s.RunSpeedAfter(v, s.StepSize);
            Assert.AreEqual(392.99f, v / Px, 0.2f);

            s.Tap();
            TestKit.StepFor(s, 1f);
            s.Tap();
            Assert.AreEqual(10f * Px, s.Velocity.x, 1e-6f, "the run starts at 10 px/s");
            for (int i = 0; i < 60; i++) s.Step(s.StepSize);
            Assert.AreEqual(169.94f, s.Velocity.x / Px, 0.1f);
        }

        /// <summary>Толчок: вперёд — скорость бега × 0.925, вверх — 180 px/с (с места) … 174 px/с (с 500 px/с).</summary>
        [Test]
        public void Takeoff_Forward0925_Up180To174()
        {
            var s = TestKit.Session();
            Assert.AreEqual(0f, s.TakeoffVelocity(0f).x);
            Assert.AreEqual(180f * Px, s.TakeoffVelocity(0f).y, 1e-5f);
            var fast = s.TakeoffVelocity(500f * Px);
            Assert.AreEqual(462.5f * Px, fast.x, 1e-5f);
            Assert.AreEqual(174f * Px, fast.y, 1e-5f);
        }

        /// <summary>Порога нет (как в оригинале): тап сразу после начала разбега — слабый прыжок почти на месте.</summary>
        [Test]
        public void QuickTap_IsAWeakHop()
        {
            var s = TestKit.Session();
            s.Tap();
            TestKit.StepFor(s, 0.5f);
            s.Tap();
            s.Step(s.StepSize);
            s.Tap();
            Assert.AreEqual(JumpPhase.Airborne, s.Phase);
            Assert.AreEqual(13f * 0.925f * Px, s.Velocity.x, 1e-4f);
            Assert.Greater(s.Velocity.y, 179f * Px);
        }

        /// <summary>Прыжок невысокий (≈ 100 единиц ≈ 4.5 м над крышей), с длинного разбега — чуть ниже.</summary>
        [Test]
        public void JumpHeight_AboutTwoMetres()
        {
            float Peak(float runUp)
            {
                var s = TestKit.Session(TestKit.Level(wind: 0f));
                var pilot = new Autopilot { DesiredRunUp = runUp, NeverOpenCanopy = true };
                float peak = float.NegativeInfinity;
                for (int i = 0; i < 20000 && !s.Phase.IsFinished(); i++)
                {
                    pilot.Update(s);
                    s.Step(s.StepSize);
                    if (s.Phase == JumpPhase.Airborne) peak = Mathf.Max(peak, s.Feet.y - s.Level.RoofY);
                }
                return peak;
            }
            float shortRun = Peak(1f), longRun = Peak(14f);
            Assert.That(shortRun / Px, Is.InRange(95f, 102f));
            Assert.Less(longRun, shortRun);
            Assert.Greater(longRun / Px, 90f);
        }

        /// <summary>Скорость по X сразу после толчка (NaN — так и не прыгнул).</summary>
        static float TakeoffSpeed(float runUp, JumpConfig jump = null)
        {
            jump ??= TestKit.Jump();
            var s = new JumpSession(LevelLayout.Build(TestKit.Level(), jump), jump, TestKit.Flight());
            var pilot = new Autopilot { DesiredRunUp = runUp };
            for (int i = 0; i < 20000 && s.Phase != JumpPhase.Airborne && !s.Phase.IsFinished(); i++)
            {
                pilot.Update(s);
                s.Step(s.StepSize);
            }
            return s.Phase == JumpPhase.Airborne ? s.Velocity.x : float.NaN;
        }

        /// <summary>Фишка оригинала: прыгнуть посреди крыши. Тот же разбег — тот же толчок, но с места дальше от края: садится ближе.</summary>
        [Test]
        public void JumpMidRoof_SameRun_LandsCloser()
        {
            var level = TestKit.Level(wind: 0f);
            level.Target = new BuildingSpec(300f, 6f, 10f, BuildingStyle.Brick); // out of reach: lands on the street
            level.Others = new BuildingSpec[0];
            level.TimeLimit = 60f;
            var edge = TestKit.Session(level);
            TestKit.RunToEnd(edge, new Autopilot { DesiredRunUp = 6f });
            var mid = TestKit.Session(level);
            TestKit.RunToEnd(mid, new Autopilot { DesiredRunUp = 10f, JumpAtDistance = 4f });
            Assert.AreEqual(edge.Level.EdgeX - 4f, mid.TakeoffX, 0.3f, "jumped 4 m before the edge");
            Assert.AreEqual(edge.Feet.x - 4f, mid.Feet.x, 0.5f, "and landed about as much closer");
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
            Assert.Greater(-s.ImpactVelocity.y, s.Params.SoftLandingSpeed);
        }

        /// <summary>Купол сразу после толчка: спускается на нём медленно (≈66 px/с) и садится — только намного дольше, чем раскрыв поздно.</summary>
        [Test]
        public void CanopyEarly_DescendsSlowly_LateIsFaster()
        {
            var level = TestKit.Level(wind: 0f);
            level.Target = new BuildingSpec(200f, 6f, 10f, BuildingStyle.Brick); // lands on the street
            level.Others = new BuildingSpec[0];
            level.TimeLimit = 60f;
            var early = TestKit.Session(level);
            TestKit.RunToEnd(early, new Autopilot { DesiredRunUp = 4f, OpenRightAway = true });
            Assert.AreEqual(JumpOutcome.MissedPlatform, early.Outcome, "a safe landing (on the street)");
            Assert.That(-early.ImpactVelocity.y / Px, Is.InRange(55f, 75f), "at the canopy's steady speed");
            var late = TestKit.Session(level);
            TestKit.RunToEnd(late, new Autopilot { DesiredRunUp = 4f });
            Assert.AreEqual(JumpOutcome.MissedPlatform, late.Outcome);
            Assert.Less(late.Elapsed, early.Elapsed - 10f, "opening late saves a lot of time");
            Assert.LessOrEqual(late.ImpactVelocity.magnitude, late.Params.SoftLandingSpeed, "and still lands softly");
        }

        [Test]
        public void Autopilot_LandsNearArrow_OnLevel1()
        {
            // The game's first level is the original's easy1 (placeholders); without it there is nothing to check here.
            var stages = EditorTools.LevelImporter.LoadStages();
            if (stages.Length == 0) Assert.Ignore("original levels are not imported");
            var jump = UnityEditor.AssetDatabase.LoadAssetAtPath<JumpConfig>("Assets/_Game/Config/JumpConfig.asset");
            var flight = UnityEditor.AssetDatabase.LoadAssetAtPath<FlightConfig>("Assets/_Game/Config/FlightConfig.asset");
            var s = new JumpSession(LevelLayout.Build(stages[0].Levels[0], jump), jump, flight);
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
            TestKit.RunToEnd(s, new Autopilot { DesiredRunUp = 4f }); // canopy right before the street
            Assert.AreEqual(JumpPhase.Landed, s.Phase);
            Assert.AreEqual(JumpOutcome.MissedPlatform, s.Outcome);
        }

        [Test]
        public void CanopyDriftingIntoAWall_Crashes()
        {
            // Short run-up + instant canopy: the wind carries him into a tall wall well below its roof.
            // Under the canopy any wall contact is a crash (it used to slide down the wall to the street).
            var level = TestKit.Level(wind: 3f);
            level.Target = new BuildingSpec(10f, 10f, 80f, BuildingStyle.Brick); // taller than the start roof
            var s = TestKit.Session(level);
            TestKit.RunToEnd(s, new Autopilot { DesiredRunUp = 4f, OpenRightAway = true });
            Assert.AreEqual(JumpOutcome.Died, s.Outcome, $"ended {s.Phase} at {s.Feet}");
        }

        /// <summary>Как в оригинале: о стену разбивается всегда, сесть можно только на крышу под куполом.</summary>
        [Test]
        public void WallContact_AlwaysCrashes()
        {
            var flight = new FlightParams(TestKit.Flight(), 0f);
            var gentle = new Vector2(1f, -2f); // brushing a wall that faces left
            Assert.AreEqual(ContactResult.Crash, LandingJudge.Judge(Vector2.left, gentle, flight, false));
            Assert.AreEqual(ContactResult.Crash, LandingJudge.Judge(Vector2.left, gentle, flight, true));
            Assert.AreEqual(ContactResult.Landed, LandingJudge.Judge(Vector2.up, new Vector2(0f, -2f), flight, true), "floors are still landings");
        }

        [Test]
        public void TapUnderCanopy_CutsItAway_AndHeFalls()
        {
            var s = TestKit.Session();
            var pilot = new Autopilot { DesiredRunUp = 4f, OpenRightAway = true };
            for (float t = 0f; t < 20f && !s.CanopyDeployed; t += s.StepSize)
            {
                pilot.Update(s);
                s.Step(s.StepSize);
            }
            Assert.IsTrue(s.CanopyDeployed);
            s.Tap();
            Assert.IsTrue(s.CanopyDeployed, "a quick double tap does not cut the canopy it has just opened");
            TestKit.StepFor(s, 0.5f);
            s.Tap();
            Assert.IsFalse(s.CanopyDeployed);
            Assert.IsTrue(s.CanopyCut);
            s.Tap();
            Assert.IsFalse(s.CanopyDeployed, "there is no second canopy");
            TestKit.RunToEnd(s);
            Assert.AreEqual(JumpOutcome.Died, s.Outcome);
        }

        /// <summary>
        /// Промежуточная платформа (как в оригинале): сел на неё — купол сброшен, флажок «пройдено», и он сразу бежит
        /// к её краю в её сторону (здесь — влево, обратно), без отхода и без тапа; тап — прыжок.
        /// </summary>
        [Test]
        public void IntermediatePlatform_LandThenRunTheOtherWay()
        {
            var scene = ScriptableObject.CreateInstance<LevelScene>();
            scene.Size = new Vector2(120f, 60f);
            scene.Solids = new[] { new Rect(0f, 0f, 10f, 40f) };     // start building
            scene.Runway = new Rect(0f, 40f, 10f, 1f);                // run right, jump at x = 10
            scene.InterPlatforms = new[] { new Rect(12f, 30f, 68f, 1f) }; // a long platform 10 m lower, run left from it
            scene.InterLeft = new[] { true };
            scene.Platform = new Rect(100f, 0f, 10f, 0.5f);
            var level = TestKit.Level(wind: 0f);
            level.Scene = scene;
            level.TimeLimit = 60f;
            var s = TestKit.Session(level);
            var events = new List<JumpEvent>();
            s.Happened += events.Add;

            var pilot = new Autopilot { DesiredRunUp = 4f };
            for (int i = 0; i < 20000 && s.RunwayIndex == 0 && !s.Phase.IsFinished(); i++)
            {
                pilot.Update(s);
                s.Step(s.StepSize);
            }
            Assert.AreEqual(1, s.RunwayIndex, $"ended {s.Outcome} at {s.Feet}");
            Assert.AreEqual(JumpPhase.Running, s.Phase, "runs on at once");
            Assert.Contains(JumpEvent.ReachedPlatform, events);
            Assert.IsTrue(s.Reached(1));
            Assert.IsFalse(s.CanopyDeployed, "a fresh canopy for the next jump");
            Assert.AreEqual(30f, s.Feet.y, 1e-4f);
            Assert.AreEqual(-1, s.Dir);
            Assert.AreEqual(-10f * Px, s.Velocity.x, 1e-5f, "from 10 px/s, like the start roof");

            float x = s.Feet.x;
            TestKit.StepFor(s, 1.5f);
            Assert.Less(s.Feet.x, x - 3f, "running left");
            Assert.Less(s.Velocity.x, -200f * Px);
            s.Tap(); // jump left
            Assert.AreEqual(JumpPhase.Airborne, s.Phase);
            Assert.Less(s.Velocity.x, -180f * Px);
            Object.DestroyImmediate(scene);
        }

        /// <summary>
        /// Платформа-цель оригинала — коробка, стоящая НА крыше: садимся на саму крышу, флажок там же, а не на верх коробки.
        /// </summary>
        [Test]
        public void TargetPlatform_LandOnTheRoofUnderTheBox()
        {
            var scene = ScriptableObject.CreateInstance<LevelScene>();
            scene.Size = new Vector2(120f, 60f);
            scene.Solids = new[] { new Rect(0f, 0f, 10f, 40f), new Rect(12f, 0f, 98f, 30f) };
            scene.Runway = new Rect(0f, 40f, 10f, 1.1f);
            scene.Platform = new Rect(12f, 30f, 98f, 0.7f); // a box 0.7 m tall standing on the 30 m roof
            var level = TestKit.Level(wind: 0f);
            level.Scene = scene;
            var s = TestKit.Session(level);
            Assert.AreEqual(30f, s.Level.TargetY, 1e-4f);

            TestKit.RunToEnd(s, new Autopilot { DesiredRunUp = 4f });
            Assert.AreEqual(JumpOutcome.WellDone, s.Outcome, $"ended at {s.Feet}");
            Assert.AreEqual(30f, s.Feet.y, 1e-3f, "feet on the roof, not on top of the box");
            Object.DestroyImmediate(scene);
        }

        /// <summary>Крыша под промежуточной платформой чуть выше низа её коробки: сел на крышу — значит, сел на платформу.</summary>
        [Test]
        public void IntermediatePlatform_RoofSlightlyAboveTheBox_Counts()
        {
            var scene = ScriptableObject.CreateInstance<LevelScene>();
            scene.Size = new Vector2(120f, 60f);
            scene.Solids = new[] { new Rect(0f, 0f, 10f, 40f), new Rect(12f, 0f, 68f, 30.4f) };
            scene.Runway = new Rect(0f, 40f, 10f, 1.1f);
            scene.InterPlatforms = new[] { new Rect(12f, 30f, 68f, 1.1f) }; // the roof is 0.4 m above the bottom of the box
            scene.InterLeft = new[] { true };
            scene.Platform = new Rect(100f, 0f, 10f, 0.5f);
            var level = TestKit.Level(wind: 0f);
            level.Scene = scene;
            level.TimeLimit = 60f;
            var s = TestKit.Session(level);

            var pilot = new Autopilot { DesiredRunUp = 4f };
            for (int i = 0; i < 20000 && s.RunwayIndex == 0 && !s.Phase.IsFinished(); i++)
            {
                pilot.Update(s);
                s.Step(s.StepSize);
            }
            Assert.AreEqual(1, s.RunwayIndex, $"ended {s.Outcome} at {s.Feet}");
            Assert.AreEqual(30.4f, s.Feet.y, 1e-3f);
            Assert.AreEqual(0f, s.Level.TargetY, 1e-4f, "a platform on the street stands on the street");
            Object.DestroyImmediate(scene);
        }

        /// <summary>Как в оригинале: время вышло в полёте — летит дальше, но посадка на цель — «время вышло».</summary>
        [Test]
        public void TimeOverInTheAir_LandingOnTarget_IsTimesUp()
        {
            var scene = ScriptableObject.CreateInstance<LevelScene>();
            scene.Size = new Vector2(120f, 60f);
            scene.Solids = new[] { new Rect(0f, 0f, 10f, 40f), new Rect(12f, 0f, 98f, 30f) };
            scene.Runway = new Rect(0f, 40f, 10f, 1.1f);
            scene.Platform = new Rect(12f, 30f, 98f, 0.7f);
            var level = TestKit.Level(wind: 0f);
            level.Scene = scene;
            level.TimeLimit = 2f;
            var s = TestKit.Session(level);
            var events = new List<JumpEvent>();
            s.Happened += events.Add;
            TestKit.RunToEnd(s, new Autopilot { DesiredRunUp = 4f });
            Assert.IsTrue(s.TimeOver);
            Assert.Contains(JumpEvent.TimeUp, events);
            Assert.AreEqual(JumpPhase.Landed, s.Phase, "he still lands");
            Assert.IsTrue(s.LandedOnTarget);
            Assert.AreEqual(JumpOutcome.TimesUp, s.Outcome);
            Assert.AreEqual(0f, s.TimeLeft);
            Object.DestroyImmediate(scene);
        }

        /// <summary>Время вышло, а он ещё стоит на крыше (таймер с первого тапа — переключатель) — конец сразу.</summary>
        [Test]
        public void TimeOver_WhileWalkingBack_EndsAtOnce()
        {
            var jump = TestKit.Jump();
            jump.TimerFromFirstTap = true;
            var level = TestKit.Level();
            level.TimeLimit = 1f;
            var s = new JumpSession(LevelLayout.Build(level, jump), jump, TestKit.Flight());
            s.Tap();
            TestKit.StepFor(s, 1.2f);
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

        /// <summary>Как в оригинале: таймер включает тап «бежать»; пока стикмен пятится, время не идёт.</summary>
        [Test]
        public void Timer_StartsWithTheRun()
        {
            var s = TestKit.Session();
            TestKit.StepFor(s, 5f);
            s.Tap();
            TestKit.StepFor(s, 1f);
            Assert.AreEqual(0f, s.Elapsed, "walking back is free");
            s.Tap(); // run
            TestKit.StepFor(s, 0.5f);
            Assert.AreEqual(0.5f, s.Elapsed, 0.02f);
        }

        /// <summary>Переключатель: время с первого тапа (пока пятится — тоже).</summary>
        [Test]
        public void Timer_FromTheFirstTap_WhenSwitched()
        {
            var jump = TestKit.Jump();
            jump.TimerFromFirstTap = true;
            var s = new JumpSession(LevelLayout.Build(TestKit.Level(), jump), jump, TestKit.Flight());
            TestKit.StepFor(s, 5f);
            Assert.AreEqual(0f, s.Elapsed, "standing before the first tap is free");
            s.Tap();
            TestKit.StepFor(s, 1f);
            Assert.AreEqual(1f, s.Elapsed, 0.02f);
            s.Tap(); // run
            TestKit.StepFor(s, 0.5f);
            Assert.AreEqual(1.5f, s.Elapsed, 0.02f);
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
            Assert.AreEqual(a.Feet.x, b.Feet.x, 0.06f); // at most one fixed step apart
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
