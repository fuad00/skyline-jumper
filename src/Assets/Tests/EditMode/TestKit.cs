using StickmanJumper.Config;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Tests
{
    /// <summary>Готовые настройки с цифрами по умолчанию (как в игре) для тестов.</summary>
    internal static class TestKit
    {
        public static JumpConfig Jump() => ScriptableObject.CreateInstance<JumpConfig>();
        public static FlightConfig Flight() => ScriptableObject.CreateInstance<FlightConfig>();
        public static ScoreConfig Score() => ScriptableObject.CreateInstance<ScoreConfig>();

        public static LevelConfig Level(float wind = 1.5f)
        {
            var l = ScriptableObject.CreateInstance<LevelConfig>();
            l.Wind = wind;
            return l;
        }

        public static JumpSession Session(LevelConfig level = null) =>
            new JumpSession(LevelLayout.Build(level ?? Level(), Jump()), Jump(), Flight());

        /// <summary>Гонит прыжок фиксированными шагами, пока он не закончится (или не выйдет запас).</summary>
        public static void RunToEnd(JumpSession s, Autopilot pilot = null, float maxSeconds = 60f)
        {
            for (float t = 0f; t < maxSeconds && !Core.JumpPhaseExtensions.IsFinished(s.Phase); t += s.StepSize)
            {
                pilot?.Update(s);
                s.Step(s.StepSize);
            }
        }

        public static void StepFor(JumpSession s, float seconds)
        {
            for (float t = 0f; t < seconds; t += s.StepSize) s.Step(s.StepSize);
        }
    }
}
