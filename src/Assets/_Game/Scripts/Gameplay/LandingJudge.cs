using UnityEngine;

namespace StickmanJumper.Gameplay
{
    public enum ContactResult
    {
        /// <summary>Сел на ноги.</summary>
        Landed,
        /// <summary>Разбился.</summary>
        Crash,
        /// <summary>Чиркнул по стене и летит дальше.</summary>
        Slide
    }

    /// <summary>
    /// Что происходит при касании поверхности (как в оригинале). Встать на ноги можно только на крышу сверху, под куполом
    /// и не быстрее мягкой посадки (110 px/с полной скорости; при ветре на уровне хватает и вертикальной). Без купола любое
    /// касание — разбился. Стена: сильнее порога (у оригинала — любое касание) — разбился, иначе скользит.
    /// </summary>
    public static class LandingJudge
    {
        /// <param name="canopy">Купол раскрыт.</param>
        public static ContactResult Judge(Vector2 normal, Vector2 velocity, in FlightParams p, bool canopy)
        {
            if (normal.y > 0.7f)
            {
                if (!canopy) return ContactResult.Crash;
                bool soft = velocity.magnitude <= p.SoftLandingSpeed
                            || (p.WindyLandingByVertical && p.Wind != 0f && Mathf.Abs(velocity.y) < p.SoftLandingSpeed);
                return soft ? ContactResult.Landed : ContactResult.Crash;
            }
            float intoSurface = -Vector2.Dot(velocity, normal);
            return intoSurface > p.WallCrashSpeed ? ContactResult.Crash : ContactResult.Slide;
        }
    }
}
