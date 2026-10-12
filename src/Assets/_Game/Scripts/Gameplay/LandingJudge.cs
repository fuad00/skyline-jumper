using StickmanJumper.Config;
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

    /// <summary>Что происходит при касании поверхности.</summary>
    public static class LandingJudge
    {
        public static ContactResult Judge(Vector2 normal, Vector2 velocity, FlightConfig cfg)
        {
            if (normal.y > 0.7f)
                return velocity.magnitude <= cfg.SafeLandingSpeed ? ContactResult.Landed : ContactResult.Crash;
            float intoSurface = -Vector2.Dot(velocity, normal);
            return intoSurface > cfg.WallCrashSpeed ? ContactResult.Crash : ContactResult.Slide;
        }
    }
}
