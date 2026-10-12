using StickmanJumper.Core;
using UnityEngine;

namespace StickmanJumper.Gameplay
{
    /// <summary>Что будет, если раскрыть купол прямо сейчас.</summary>
    public readonly struct CanopyForecast
    {
        /// <summary>Где окажемся на высоте крыши цели (NaN — не долетим до этой высоты).</summary>
        public readonly float X;
        /// <summary>Скорость в этот момент, м/с.</summary>
        public readonly float Speed;

        public CanopyForecast(float x, float speed)
        {
            X = x;
            Speed = speed;
        }
    }

    /// <summary>
    /// «Сам играет» за игрока — для автозаезда и тестов. Пользуется той же физикой, что и игра:
    /// отходит на нужную длину разбега, прыгает у самого края и раскрывает купол так, чтобы сесть у флажка.
    /// </summary>
    public sealed class Autopilot
    {
        /// <summary>Сколько метров отойти от края (больше длины крыши — отойдёт до упора). NaN — подобрать самому под флажок.</summary>
        public float DesiredRunUp = float.NaN;
        /// <summary>Не раскрывать купол вообще (проверка «разбился»).</summary>
        public bool NeverOpenCanopy;
        /// <summary>Не прыгать — проспать край (проверка «сорвался»).</summary>
        public bool NeverJump;
        /// <summary>Раскрыть купол сразу после толчка (проверка «снесло ветром»).</summary>
        public bool OpenRightAway;
        /// <summary>Раскрыть купол на этой высоте (NaN — подобрать самому вместе с разбегом).</summary>
        public float OpenAtY = float.NaN;

        float _lastMiss = float.NaN;

        /// <summary>Вызывать каждый кадр перед session.Advance. true — тапнул.</summary>
        public bool Update(JumpSession s)
        {
            switch (s.Phase)
            {
                case JumpPhase.Ready:
                    _lastMiss = float.NaN;
                    if (float.IsNaN(DesiredRunUp))
                    {
                        var plan = Plan(s);
                        DesiredRunUp = plan.RunUp;
                        if (float.IsNaN(OpenAtY)) OpenAtY = plan.OpenY;
                    }
                    s.Tap();
                    return true;
                case JumpPhase.BackingUp:
                    if (s.Level.EdgeX - s.Feet.x >= DesiredRunUp)
                    {
                        s.Tap();
                        return true;
                    }
                    return false;
                case JumpPhase.AtBackWall:
                    s.Tap();
                    return true;
                case JumpPhase.Running:
                    if (!NeverJump && s.Feet.x + s.Velocity.x * s.StepSize * 2f >= s.Level.EdgeX)
                    {
                        s.Tap();
                        return true;
                    }
                    return false;
                case JumpPhase.Airborne:
                    bool openNow = OpenRightAway
                        || (!float.IsNaN(OpenAtY) ? s.Velocity.y < 0f && s.Feet.y <= OpenAtY : ShouldOpenCanopy(s));
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
        /// Раскрывать, когда прогноз точки посадки дошёл до флажка. Куда он движется со временем — зависит от разбега
        /// и ветра (под куполом ветер несёт вперёд почти так же, как свободное падение, и прогноз даже немного «гуляет»),
        /// поэтому ловим момент, когда промах сменил знак. Не дошёл — раскрываем в последний безопасный момент.
        /// </summary>
        public bool ShouldOpenCanopy(JumpSession s)
        {
            if (s.Phase != JumpPhase.Airborne || s.CanopyDeployed || s.Velocity.y > 0f) return false;
            var f = Forecast(s);
            if (float.IsNaN(f.X)) return true; // about to drop below the target roof: last chance
            if (f.Speed > s.Flight.SafeLandingSpeed * 0.8f) return true; // waiting longer would be too late to slow down
            float miss = f.X - s.Level.ArrowX;
            float last = _lastMiss;
            _lastMiss = miss;
            if (float.IsNaN(last)) return Mathf.Abs(miss) < 0.05f;
            return Mathf.Sign(miss) != Mathf.Sign(last);
        }

        public readonly struct JumpPlan
        {
            public readonly float RunUp, OpenY, Miss;
            public JumpPlan(float runUp, float openY, float miss)
            {
                RunUp = runUp;
                OpenY = openY;
                Miss = miss;
            }
            public bool Found => !float.IsInfinity(Miss);
        }

        /// <summary>
        /// Перебирает длину разбега (шаг 0.25 м) и высоту раскрытия купола и прогоняет прыжок целиком той же физикой
        /// (со столкновениями). Возвращает вариант, после которого садимся ближе всего к флажку.
        /// </summary>
        public static JumpPlan Plan(JumpSession template)
        {
            var level = template.Level;
            float max = level.EdgeX - level.BackLimitX;
            float lowest = level.TargetRect.yMax + 6f;
            var best = new JumpPlan(max, float.NaN, float.PositiveInfinity);
            for (float run = 0.5f; run <= max + 0.001f; run += 0.25f)
            for (float openY = level.RoofY - 2f; openY >= lowest; openY -= 3f)
            {
                var s = new JumpSession(level, template.Jump, template.Flight);
                var pilot = new Autopilot { DesiredRunUp = run, OpenAtY = openY };
                for (int i = 0; i < 9000 && !s.Phase.IsFinished(); i++)
                {
                    pilot.Update(s);
                    s.Step(s.StepSize);
                }
                if (s.Outcome != JumpOutcome.WellDone) continue;
                if (s.LandingDistance < best.Miss) best = new JumpPlan(run, openY, s.LandingDistance);
            }
            return best;
        }

        /// <summary>Только длина разбега из <see cref="Plan"/>.</summary>
        public static float PlanRunUp(JumpSession template) => Plan(template).RunUp;

        /// <summary>Прогноз «раскрываю сейчас» той же физикой, без столкновений.</summary>
        public static CanopyForecast Forecast(JumpSession s, float maxSeconds = 40f)
        {
            var p = s.Params;
            float h = s.StepSize;
            float openRate = h / Mathf.Max(0.05f, s.Flight.CanopyOpenTime);
            float targetY = s.Level.TargetRect.yMax;
            Vector2 pos = s.Feet;
            Vector2 vel = s.Velocity;
            float progress = s.CanopyProgress;
            if (pos.y <= targetY) return new CanopyForecast(float.NaN, vel.magnitude);
            for (float t = 0f; t < maxSeconds; t += h)
            {
                progress = Mathf.Min(1f, progress + openRate);
                vel = FlightModel.Step(vel, FlightModel.OpenCurve(progress), p, h);
                pos += vel * h;
                if (vel.y < 0f && pos.y <= targetY) return new CanopyForecast(pos.x, vel.magnitude);
            }
            return new CanopyForecast(float.NaN, vel.magnitude);
        }
    }
}
