using StickmanJumper.Core;
using UnityEngine;

namespace StickmanJumper.Gameplay
{
    /// <summary>
    /// «Сам играет» за игрока — для автозаезда, заставки в меню и тестов. Пользуется той же физикой, что и игра:
    /// отходит на нужную длину разбега, прыгает у края — или, как фишка оригинала, раньше, посреди крыши (прыжок короче) —
    /// и раскрывает купол на нужной высоте (или сам — в последний момент, когда купол ещё успеет погасить скорость).
    /// Куда сядешь, решают разбег, точка прыжка и высота раскрытия: под куполом стикмен почти не летит вперёд, но его сносит ветром.
    /// </summary>
    public sealed class Autopilot
    {
        /// <summary>Сколько метров отойти от края (больше длины крыши — отойдёт до упора). NaN — подобрать самому под флажок.</summary>
        public float DesiredRunUp = float.NaN;
        /// <summary>Прыгнуть, не добежав до края столько метров (0 — у самого края). Короче разбег и ближе посадка.</summary>
        public float JumpAtDistance;
        /// <summary>Не раскрывать купол вообще (проверка «разбился»).</summary>
        public bool NeverOpenCanopy;
        /// <summary>Не прыгать — проспать край (проверка «сорвался»).</summary>
        public bool NeverJump;
        /// <summary>Раскрыть купол сразу после толчка (проверка «снесёт ветром» и «оторвал купол»).</summary>
        public bool OpenRightAway;
        /// <summary>Раскрыть купол на этой высоте (NaN — сам, в последний момент, когда ещё успеет затормозить).</summary>
        public float OpenAtY = float.NaN;
        /// <summary>Раскрывая сам, тормозить до этой доли скорости мягкой посадки (запас).</summary>
        public float SoftMargin = 0.85f;
        /// <summary>Раскрывая сам, закончить торможение хотя бы на столько выше поверхности, м.</summary>
        public float BrakeClearance = 1f;

        /// <summary>Вызывать перед каждым шагом физики. true — тапнул.</summary>
        public bool Update(JumpSession s)
        {
            switch (s.Phase)
            {
                case JumpPhase.Ready:
                    if (float.IsNaN(DesiredRunUp) && s.RunwayIndex == 0)
                    {
                        var plan = Plan(s);
                        DesiredRunUp = plan.RunUp;
                        JumpAtDistance = plan.JumpAt;
                        if (float.IsNaN(OpenAtY)) OpenAtY = plan.OpenY;
                    }
                    s.Tap();
                    return true;
                case JumpPhase.BackingUp:
                    if (s.DistanceToEdge >= DesiredRunUp)
                    {
                        s.Tap();
                        return true;
                    }
                    return false;
                case JumpPhase.AtBackWall:
                    s.Tap();
                    return true;
                case JumpPhase.Running:
                    // The plan's mid-roof jump is for the start roof; from intermediate platforms jump at the edge.
                    float jumpAt = s.RunwayIndex == 0 ? Mathf.Max(0f, JumpAtDistance) : 0f;
                    if (!NeverJump && s.DistanceToEdge - Mathf.Abs(s.Velocity.x) * s.StepSize * 2f <= jumpAt)
                    {
                        s.Tap();
                        return true;
                    }
                    return false;
                case JumpPhase.Airborne:
                    // At the planned height — or earlier, if later the canopy would no longer brake in time.
                    bool openNow = OpenRightAway
                        || (!float.IsNaN(OpenAtY) && s.Velocity.y < 0f && s.Feet.y <= OpenAtY)
                        || ShouldOpenCanopy(s);
                    if (!NeverOpenCanopy && !s.CanopyDeployed && openNow)
                    {
                        s.Tap();
                        return true;
                    }
                    return false;
            }
            return false;
        }

        /// <summary>
        /// Раскрывать сейчас, если через шаг будет поздно: раскрыв купол шагом позже, стикмен не успеет погасить скорость
        /// до мягкой посадки, не коснувшись поверхности.
        /// </summary>
        public bool ShouldOpenCanopy(JumpSession s)
        {
            if (s.Phase != JumpPhase.Airborne || s.CanopyDeployed || s.CanopyCut || s.Velocity.y > 0f) return false;
            var p = s.Params;
            float h = s.StepSize;
            Vector2 v = s.Velocity;
            var c = s.CanopyMemory;
            Vector2 feet = s.Feet + FlightModel.Advance(ref v, ref c, false, p, h); // one more step in free fall
            var box = new Rect(feet.x - s.Flight.BodyWidth * 0.5f, feet.y, s.Flight.BodyWidth, s.Flight.BodyHeight);
            // Quick check: braking takes well under 1.5 s, and in that time he can't cover more than at the current speed.
            Vector2 reach = v * 1.5f + Vector2.down * BrakeClearance;
            if (!Sweep.Cast(box, reach, s.Level.Solids, out _)) return false;
            // The braking path if he opened one step later.
            v = FlightModel.Deploy(v, p);
            c.Ticks = 0f;
            Vector2 path = Vector2.zero;
            for (int i = 0; i < 600 && !IsSoft(v, p, SoftMargin); i++) path += FlightModel.Advance(ref v, ref c, true, p, h);
            path.y -= BrakeClearance;
            return Sweep.Cast(box, path, s.Level.Solids, out _);
        }

        /// <summary>Мягкая ли посадка на такой скорости (правило <see cref="LandingJudge"/>, с запасом margin).</summary>
        public static bool IsSoft(Vector2 v, in FlightParams p, float margin = 1f)
        {
            float soft = p.SoftLandingSpeed * margin;
            return v.magnitude <= soft || (p.WindyLandingByVertical && p.Wind != 0f && Mathf.Abs(v.y) < soft);
        }

        /// <summary>
        /// Через сколько секунд тело коснётся поверхности сверху, если ничего не делать (той же физикой, со столкновениями).
        /// +∞ — не коснётся за maxSeconds.
        /// </summary>
        public static float SecondsToTouchdown(JumpSession s, float maxSeconds)
        {
            var p = s.Params;
            float h = s.StepSize;
            Vector2 feet = s.Feet, vel = s.Velocity;
            var canopy = s.CanopyMemory;
            var size = new Vector2(s.Flight.BodyWidth, s.Flight.BodyHeight);
            for (float t = 0f; t <= maxSeconds; t += h)
            {
                var box = new Rect(feet.x - size.x * 0.5f, feet.y, size.x, size.y);
                Vector2 delta = FlightModel.Advance(ref vel, ref canopy, s.CanopyDeployed, p, h);
                if (Sweep.Cast(box, delta, s.Level.Solids, out var hit))
                    return hit.Normal.y > 0.7f ? t + h * hit.T : float.PositiveInfinity; // a wall first: the canopy will not help
                feet += delta;
            }
            return float.PositiveInfinity;
        }

        public readonly struct JumpPlan
        {
            /// <summary>Сколько отойти от края, м.</summary>
            public readonly float RunUp;
            /// <summary>Прыгнуть, не добежав до края столько, м (0 — у края).</summary>
            public readonly float JumpAt;
            /// <summary>Высота раскрытия купола (NaN — сам, перед касанием).</summary>
            public readonly float OpenY;
            /// <summary>Промах мимо флажка, м (∞ — плана нет).</summary>
            public readonly float Miss;
            public JumpPlan(float runUp, float jumpAt, float openY, float miss)
            {
                RunUp = runUp;
                JumpAt = jumpAt;
                OpenY = openY;
                Miss = miss;
            }
            public bool Found => !float.IsInfinity(Miss);
        }

        /// <summary>Автопилот, который выполнит план.</summary>
        public static Autopilot FromPlan(in JumpPlan plan) =>
            new Autopilot { DesiredRunUp = plan.RunUp, JumpAtDistance = plan.JumpAt, OpenAtY = plan.OpenY };

        /// <summary>
        /// Высоты раскрытия купола в плане, м над целью (NaN — сам, в последний момент). Раньше раскрыл — дольше спуск
        /// (≈3 м/с) и дальше сносит ветром.
        /// </summary>
        static readonly float[] OpenHeights = { float.NaN, 8f, 12f, 17f, 24f, 34f, 48f };
        /// <summary>Где прыгнуть, не добежав до края, — доли остатка крыши за разбегом (0 — у края).</summary>
        static readonly float[] JumpPoints = { 0.25f, 0.5f, 0.75f, 1f };
        /// <summary>Ближе этого к флажку — план хорош, прыжки посреди крыши не перебираем, м.</summary>
        const float GoodMiss = 1f;

        /// <summary>
        /// Перебирает, сколько пробежать, где прыгнуть и на какой высоте раскрыть купол, и прогоняет прыжок целиком той же
        /// физикой (со столкновениями). Под куполом стикмен почти не летит вперёд, но спускается медленно и его сносит ветром;
        /// прыжок посреди крыши — короче. Сначала только прыжки у края; если флажок так не достать — и посреди крыши.
        /// Возвращает вариант, после которого садимся на цель ближе всего к флажку.
        /// </summary>
        /// <param name="runStep">Шаг перебора длины разбега, м.</param>
        public static JumpPlan Plan(JumpSession template, float runStep = 0.25f)
        {
            var level = template.Level;
            float max = level.EdgeX - level.BackLimitX;
            var best = new JumpPlan(max, 0f, float.NaN, float.PositiveInfinity);
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1 && best.Miss <= GoodMiss) break;
                for (float run = 0.5f; run <= max + 0.001f; run += runStep)
                {
                    // Pass 0: jump at the edge. Pass 1: walk back further and jump mid-roof after the same run.
                    foreach (float point in pass == 0 ? new[] { 0f } : JumpPoints)
                    {
                        float jumpAt = (max - run) * point;
                        if (pass == 1 && jumpAt < 0.5f) continue;
                        foreach (float k in OpenHeights)
                        {
                            float openY = float.IsNaN(k) ? float.NaN : level.TargetY + k;
                            var s = new JumpSession(level, template.Jump, template.Flight);
                            var pilot = new Autopilot { DesiredRunUp = run + jumpAt, JumpAtDistance = jumpAt, OpenAtY = openY };
                            for (int i = 0; i < 20000 && !s.Phase.IsFinished(); i++)
                            {
                                pilot.Update(s);
                                s.Step(s.StepSize);
                            }
                            if (s.Outcome == JumpOutcome.WellDone && s.LandingDistance < best.Miss)
                                best = new JumpPlan(run + jumpAt, jumpAt, openY, s.LandingDistance);
                        }
                    }
                }
            }
            return best;
        }

        /// <summary>Только длина разбега из <see cref="Plan"/>.</summary>
        public static float PlanRunUp(JumpSession template) => Plan(template).RunUp;
    }
}
