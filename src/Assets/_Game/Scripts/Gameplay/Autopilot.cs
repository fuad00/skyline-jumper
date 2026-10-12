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
    /// отходит на нужную длину разбега, прыгает у самого края и раскрывает купол так, чтобы снесло к стрелке.
    /// </summary>
    public sealed class Autopilot
    {
        /// <summary>Сколько метров отойти от края (больше длины крыши — отойдёт до упора).</summary>
        public float DesiredRunUp = 100f;
        /// <summary>Не раскрывать купол вообще (проверка «разбился»).</summary>
        public bool NeverOpenCanopy;
        /// <summary>Не прыгать — проспать край (проверка «сорвался»).</summary>
        public bool NeverJump;
        /// <summary>Раскрыть купол сразу после толчка (проверка «снесло ветром»).</summary>
        public bool OpenRightAway;

        float _lastMiss = float.NaN;

        /// <summary>Вызывать каждый кадр перед session.Advance. true — тапнул.</summary>
        public bool Update(JumpSession s)
        {
            switch (s.Phase)
            {
                case JumpPhase.Ready:
                    _lastMiss = float.NaN;
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
                    if (!NeverOpenCanopy && !s.CanopyDeployed && (OpenRightAway || ShouldOpenCanopy(s)))
                    {
                        s.Tap();
                        return true;
                    }
                    return false;
            }
            return false;
        }

        /// <summary>
        /// Раскрывать, когда прогноз точки посадки дошёл до стрелки. Куда он движется со временем — зависит от разбега
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

        /// <summary>Прогноз «раскрываю сейчас» той же физикой, без столкновений.</summary>
        public static CanopyForecast Forecast(JumpSession s, float maxSeconds = 40f)
        {
            var p = s.Params;
            float h = s.StepSize;
            float openRate = h / Mathf.Max(0.05f, s.Flight.CanopyOpenTime);
            float targetY = s.Level.TargetRect.yMax;
            Vector2 pos = s.Feet;
            Vector2 vel = s.Velocity;
            float canopy = s.Canopy;
            if (pos.y <= targetY) return new CanopyForecast(float.NaN, vel.magnitude);
            for (float t = 0f; t < maxSeconds; t += h)
            {
                canopy = Mathf.Min(1f, canopy + openRate);
                vel = FlightModel.Step(vel, canopy, p, h);
                pos += vel * h;
                if (vel.y < 0f && pos.y <= targetY) return new CanopyForecast(pos.x, vel.magnitude);
            }
            return new CanopyForecast(float.NaN, vel.magnitude);
        }
    }
}
