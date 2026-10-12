using System.Globalization;
using StickmanJumper.Core;

namespace StickmanJumper.UI
{
    /// <summary>Все тексты для игрока в одном месте (пока по-русски). Потом заменим на локализацию.</summary>
    public static class UIText
    {
        static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        public const string HintReady = "Тапни — отойти назад для разбега";
        public const string HintBackingUp = "Тапни — бежать к краю!";
        public const string HintRunning = "Тапни у края — прыжок!";
        public const string HintFreefall = "Тапни — парашют!";
        public const string Retry = "ЕЩЁ РАЗ";
        public const string DistanceLabel = "До стрелки";
        public const string BestLabel = "Лучшее";
        public const string PointsLabel = "Очки";
        public const string NewBest = "НОВЫЙ РЕКОРД!";
        public const string Dash = "—";

        public static string Title(JumpOutcome outcome) => outcome switch
        {
            JumpOutcome.WellDone => "ОТЛИЧНО!",
            JumpOutcome.Died => "РАЗБИЛСЯ!",
            JumpOutcome.MissedPlatform => "МИМО КРЫШИ!",
            JumpOutcome.TimesUp => "ВРЕМЯ ВЫШЛО!",
            _ => string.Empty
        };

        public static string Hint(JumpPhase phase, bool canopy) => phase switch
        {
            JumpPhase.Ready => HintReady,
            JumpPhase.BackingUp => HintBackingUp,
            JumpPhase.AtBackWall => HintBackingUp,
            JumpPhase.Running => HintRunning,
            JumpPhase.Airborne => canopy ? string.Empty : HintFreefall,
            _ => string.Empty
        };

        /// <summary>Метры с сотыми, как в оригинале: «0,42 м».</summary>
        public static string Meters(float m) => float.IsNaN(m) || m < 0f ? Dash : m.ToString("0.00", Ru) + " м";

        public static string Seconds(float s) => s.ToString("0.0", Ru);

        public static string Wind(float w) => System.Math.Abs(w).ToString("0.#", Ru);

        public static string Points(int p) => p.ToString("N0", Ru);
    }
}
