using StickmanJumper.Config;
using StickmanJumper.Core;
using UnityEngine;

namespace StickmanJumper.Gameplay
{
    public readonly struct JumpScore
    {
        public readonly int Points;
        public readonly int Stars;

        public JumpScore(int points, int stars)
        {
            Points = points;
            Stars = stars;
        }
    }

    /// <summary>Очки и звёзды. Звёзды как в оригинале: за близость к флажку и за время.</summary>
    public static class Scoring
    {
        public static JumpScore Evaluate(JumpOutcome outcome, float distance, float elapsed, float targetHalfWidth, LevelConfig level, ScoreConfig score)
        {
            if (outcome != JumpOutcome.WellDone) return new JumpScore(0, 0);
            float accuracy = Mathf.Clamp01(1f - distance / Mathf.Max(0.01f, targetHalfWidth));
            float speed = Mathf.Clamp01(1f - elapsed / Mathf.Max(0.01f, level.TimeLimit));
            int points = Mathf.RoundToInt(score.AccuracyPoints * accuracy + score.TimePoints * speed);

            int stars = 1;
            if (distance <= level.TwoStarDistance)
            {
                stars = 2;
                if (distance <= level.ThreeStarDistance && elapsed <= level.ThreeStarTime) stars = 3;
            }
            return new JumpScore(points, stars);
        }

        /// <summary>«Лучшее» в оригинале — самое близкое к флажку расстояние.</summary>
        public static bool IsNewBest(float distance, float previousBest) =>
            !float.IsNaN(distance) && (float.IsNaN(previousBest) || previousBest < 0f || distance < previousBest);
    }
}
