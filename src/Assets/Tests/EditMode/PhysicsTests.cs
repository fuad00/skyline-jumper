using System.Collections.Generic;
using NUnit.Framework;
using StickmanJumper.Config;
using StickmanJumper.Gameplay;
using UnityEditor;
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

    /// <summary>Полёт как в коде оригинала (reference/disasm/FINDINGS.md). Скорости в тестах — в единицах физики оригинала (22 на метр).</summary>
    public class FlightModelTests
    {
        const float Px = 1f / 22f;

        static Vector2 Free(FlightParams p, Vector2 v, float seconds)
        {
            var c = CanopyState.Start(p);
            for (int i = 0; i < Mathf.RoundToInt(seconds * 60f); i++) FlightModel.Advance(ref v, ref c, false, p, 1f / 60f);
            return v;
        }

        /// <summary>Раскрыть купол и спускаться. Возвращает скорость; soft — через сколько секунд скорость стала мягкой.</summary>
        static Vector2 UnderCanopy(FlightParams p, Vector2 v, float seconds, out float soft, out Vector2 path)
        {
            var c = CanopyState.Start(p);
            v = FlightModel.Deploy(v, p);
            soft = float.NaN;
            path = Vector2.zero;
            for (int i = 1; i <= Mathf.RoundToInt(seconds * 60f); i++)
            {
                path += FlightModel.Advance(ref v, ref c, true, p, 1f / 60f);
                if (float.IsNaN(soft) && Autopilot.IsSoft(v, p)) soft = i / 60f;
            }
            return v;
        }

        /// <summary>В конфигах — числа оригинала как есть (px, «за тик» = за 1/60 с); в метры их переводит один масштаб.</summary>
        [Test]
        public void Configs_HoldTheOriginalNumbers()
        {
            var f = TestKit.Flight();
            var j = TestKit.Jump();
            Assert.AreEqual(22f, f.PixelsPerMeter, "the original halves the level files (44 per metre)");
            Assert.AreEqual(60, f.TicksPerSecond);
            Assert.AreEqual(60, f.StepsPerSecond, "one physics step per original tick");
            Assert.AreEqual(165f, f.GravityPx);
            Assert.AreEqual(1000f, f.FallSpeedCapPx);
            Assert.AreEqual(3f, f.WindScale);
            Assert.AreEqual(0.000725f, f.FlightWindPerTick);
            Assert.AreEqual(0.795f, f.CanopyBrakeRising);
            Assert.AreEqual(new Vector2(0.895f, 0.7f), f.CanopyBrakeFalling);
            Assert.AreEqual(500f, f.CanopyBrakeRefPx);
            Assert.AreEqual(800f, f.CanopyBrakeMaxSpeedPx);
            Assert.AreEqual(new Vector2(0.04f, 0.85f), f.CanopyGravity);
            Assert.AreEqual(-300f, f.CanopyLiftFromPx);
            Assert.AreEqual(100f, f.CanopyLiftToPx);
            Assert.AreEqual(68f, f.CanopyDragFromPx);
            Assert.AreEqual(new Vector2(0.055f, 0.195f), f.CanopyDragWind);
            Assert.AreEqual(75f, f.CanopyDragWindTicks);
            Assert.AreEqual(new Vector2(0.975f, 0.955f), f.CanopyDrag);
            Assert.AreEqual(30f, f.CanopyDragTicks);
            Assert.AreEqual(65f, f.CanopySlowSidePx);
            Assert.AreEqual(0.0095f, f.CanopyDragStep);
            Assert.AreEqual(0.965f, f.CanopyDragStart);
            Assert.AreEqual(0.01f, f.CanopyDragSideBonus);
            Assert.AreEqual(0.0075f, f.CanopyWindPerTick);
            Assert.AreEqual(110f, f.SoftLandingSpeedPx);
            Assert.IsTrue(f.WindyLandingByVertical);
            Assert.AreEqual(0f, f.WallCrashSpeedPx, "any touch of a wall is a crash");
            Assert.AreEqual(900f, f.RagdollSpeedCapPx);
            Assert.AreEqual(63f, j.WalkBackStartSpeedPx);
            Assert.AreEqual(0.225f, j.WalkBackGainPerTick);
            Assert.AreEqual(120f, j.WalkBackMaxSpeedPx);
            Assert.AreEqual(10f, j.RunStartSpeedPx);
            Assert.AreEqual(new Vector2(3f, 1f), j.RunGainPerTick);
            Assert.AreEqual(500f, j.RunGainRefSpeedPx);
            Assert.AreEqual(0.925f, j.JumpForwardFactor);
            Assert.AreEqual(new Vector2(180f, 174f), j.JumpUpSpeedPx);
            Assert.AreEqual(500f, j.JumpUpRefSpeedPx);
            Assert.IsFalse(j.TimerFromFirstTap, "the clock starts with the run");
            Assert.AreEqual(165f * Px, f.Gravity, 1e-5f);
        }

        /// <summary>Файлы настроек игры (.asset) держат те же числа, что и код.</summary>
        [Test]
        public void ConfigAssets_MatchTheDefaults()
        {
            AssertSame(TestKit.Flight(), AssetDatabase.LoadAssetAtPath<FlightConfig>("Assets/_Game/Config/FlightConfig.asset"));
            AssertSame(TestKit.Jump(), AssetDatabase.LoadAssetAtPath<JumpConfig>("Assets/_Game/Config/JumpConfig.asset"));
        }

        static void AssertSame(ScriptableObject defaults, ScriptableObject asset)
        {
            Assert.IsNotNull(asset);
            Assert.AreEqual(JsonUtility.ToJson(defaults), JsonUtility.ToJson(asset), asset.name);
        }

        /// <summary>Свободное падение: 165 px/с², не быстрее 1000 px/с; сопротивления нет — скорость по X держится.</summary>
        [Test]
        public void Freefall_165_CappedAt1000_KeepsSpeedX()
        {
            var p = new FlightParams(TestKit.Flight(), 0f);
            var v = Free(p, new Vector2(300f * Px, 0f), 2f);
            Assert.AreEqual(-330f * Px, v.y, 1e-3f);
            Assert.AreEqual(300f * Px, v.x, 1e-5f);
            Assert.AreEqual(-1000f * Px, Free(p, Vector2.zero, 10f).y, 1e-4f);
        }

        /// <summary>Как в оригинале, без купола сначала смещение (со старой скоростью), потом скорость.</summary>
        [Test]
        public void Freefall_MovesFirst_ThenSpeedsUp()
        {
            var p = new FlightParams(TestKit.Flight(), 0f);
            var v = Vector2.zero;
            var c = CanopyState.Start(p);
            Assert.AreEqual(Vector2.zero, FlightModel.Advance(ref v, ref c, false, p, 1f / 60f));
            Assert.AreEqual(-165f * Px / 60f, v.y, 1e-6f);
        }

        /// <summary>Уровень задаёт свой предел падения (maxFlySpeedY −1200 px/с).</summary>
        [Test]
        public void LevelFallCap_ReplacesTheDefault()
        {
            var p = new FlightParams(TestKit.Flight(), 0f, 1200f * Px);
            Assert.AreEqual(-1200f * Px, Free(p, Vector2.zero, 12f).y, 1e-4f);
        }

        /// <summary>Ветер в полёте слабый: ветер уровня × 3 × 0.000725 за тик.</summary>
        [Test]
        public void Wind_InFreeFlight_IsWeak()
        {
            var p = new FlightParams(TestKit.Flight(), 30f * Px);
            Assert.AreEqual(30f * 3f * 0.000725f * 60f * Px, Free(p, Vector2.zero, 1f).x, 1e-5f);
        }

        /// <summary>Раскрыл на подъёме — ×0.795; в падении — от ×0.895 (медленно) до ×0.583 (800 px/с и быстрее).</summary>
        [Test]
        public void Deploy_BrakesAtOnce()
        {
            var p = new FlightParams(TestKit.Flight(), 0f);
            Assert.AreEqual(new Vector2(300f, 100f) * Px * 0.795f, FlightModel.Deploy(new Vector2(300f, 100f) * Px, p));
            Assert.AreEqual(-250f * Px * 0.7975f, FlightModel.Deploy(new Vector2(0f, -250f) * Px, p).y, 1e-5f);
            Assert.AreEqual(-1000f * Px * 0.583f, FlightModel.Deploy(new Vector2(0f, -1000f) * Px, p).y, 1e-5f);
        }

        /// <summary>Купол с 1000 px/с гасит скорость до мягкой меньше чем за секунду (≈175 единиц, 8 м) и дальше спускает на ≈66 единиц/с (3 м/с).</summary>
        [Test]
        public void Canopy_BrakesToAboutThreeMetresPerSecond()
        {
            var p = new FlightParams(TestKit.Flight(), 0f);
            var v = UnderCanopy(p, new Vector2(0f, -1000f) * Px, 5f, out float soft, out var path);
            Assert.That(soft, Is.InRange(0.5f, 0.8f), "soft after");
            Assert.That(-v.y / Px, Is.InRange(60f, 70f), "steady descent, px/s");
            Assert.That(-path.y / Px, Is.InRange(420f, 520f), "5 s under the canopy, px");
        }

        /// <summary>Под куполом стикмен почти не летит вперёд: 300 px/с по X гаснут за пару секунд.</summary>
        [Test]
        public void Canopy_KillsTheForwardSpeed()
        {
            var p = new FlightParams(TestKit.Flight(), 0f);
            var v = UnderCanopy(p, new Vector2(300f, -1000f) * Px, 3f, out _, out var path);
            Assert.Less(v.x / Px, 5f);
            Assert.That(path.x / Px, Is.InRange(90f, 130f), "forward drift after opening, px");
        }

        /// <summary>Под куполом ветер сносит сильно (в 10 раз сильнее, чем без купола): ветер 30 → ≈45 px/с вбок.</summary>
        [Test]
        public void Canopy_DriftsWithTheWind()
        {
            var p = new FlightParams(TestKit.Flight(), 30f * Px);
            var v = UnderCanopy(p, new Vector2(0f, -1000f) * Px, 8f, out float soft, out _);
            Assert.That(v.x / Px, Is.InRange(40f, 50f));
            Assert.Less(soft, 1f);
            var left = UnderCanopy(new FlightParams(TestKit.Flight(), -30f * Px), new Vector2(0f, -1000f) * Px, 8f, out _, out _);
            Assert.AreEqual(-v.x, left.x, 1e-4f, "both directions");
        }

        /// <summary>Сильный ветер (130): полная скорость под куполом больше 110, но вертикальная меньше — садиться можно.</summary>
        [Test]
        public void Canopy_StrongWind_StillLandsByVerticalSpeed()
        {
            var p = new FlightParams(TestKit.Flight(), 130f * Px);
            var v = UnderCanopy(p, new Vector2(0f, -1000f) * Px, 8f, out float soft, out _);
            Assert.Greater(v.magnitude / Px, 110f);
            Assert.Less(-v.y / Px, 110f);
            Assert.Less(soft, 2f);
        }
    }

    public class LandingJudgeTests
    {
        const float Px = 1f / 22f;
        static FlightParams P => new FlightParams(TestKit.Flight(), 0f);

        /// <summary>Без купола любое касание — разбился, даже совсем медленно.</summary>
        [Test]
        public void WithoutCanopy_AlwaysCrashes()
        {
            Assert.AreEqual(ContactResult.Crash, LandingJudge.Judge(Vector2.up, new Vector2(0f, -10f) * Px, P, false));
        }

        /// <summary>Под куполом: полная скорость не больше 110 px/с — сел, больше — разбился (и вбок тоже считается).</summary>
        [Test]
        public void UnderTheCanopy_Upto110_Lands()
        {
            var p = P;
            Assert.AreEqual(ContactResult.Landed, LandingJudge.Judge(Vector2.up, new Vector2(60f, -90f) * Px, p, true));
            Assert.AreEqual(ContactResult.Crash, LandingJudge.Judge(Vector2.up, new Vector2(0f, -120f) * Px, p, true));
            Assert.AreEqual(ContactResult.Crash, LandingJudge.Judge(Vector2.up, new Vector2(100f, -60f) * Px, p, true));
        }

        /// <summary>При ветре на уровне хватает вертикальной скорости меньше 110 px/с.</summary>
        [Test]
        public void Windy_VerticalSpeedDecides()
        {
            var windy = new FlightParams(TestKit.Flight(), 50f * Px);
            Assert.AreEqual(ContactResult.Landed, LandingJudge.Judge(Vector2.up, new Vector2(150f, -60f) * Px, windy, true));
            Assert.AreEqual(ContactResult.Crash, LandingJudge.Judge(Vector2.up, new Vector2(150f, -60f) * Px, P, true));
        }

        /// <summary>Стена: любое касание поперёк — разбился; строго вдоль — нет.</summary>
        [Test]
        public void Wall_AnyTouch_Crashes()
        {
            Assert.AreEqual(ContactResult.Crash, LandingJudge.Judge(Vector2.left, new Vector2(0.5f, -10f), P, false));
            Assert.AreEqual(ContactResult.Crash, LandingJudge.Judge(Vector2.left, new Vector2(0.5f, -1f), P, true));
            Assert.AreEqual(ContactResult.Slide, LandingJudge.Judge(Vector2.left, new Vector2(0f, -10f), P, false));
        }
    }
}
