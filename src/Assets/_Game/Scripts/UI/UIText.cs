using System.Globalization;
using StickmanJumper.Core;

namespace StickmanJumper.UI
{
    /// <summary>Все тексты для игрока в одном месте (пока по-русски). Потом заменим на локализацию.</summary>
    public static class UIText
    {
        static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        // Logo: working title, two lines like the original's sticker logo. Placeholder until the real name.
        public const string LogoTop = "Stickman.";
        public const string LogoBottom = "jumper";

        public const string HintReady = "Тапни — отойти для разбега";
        public const string HintBackingUp = "Тапни — развернуться и бежать!";
        public const string HintRunning = "Тапни у края — прыжок!";
        public const string HintFreefall = "Тапни — парашют!";

        public const string Wind = "ВЕТЕР";
        public const string Pause = "пауза";
        public const string Choose = "Выбери";
        public const string StageWord = "этап";
        public const string LevelWord = "уровень";
        public const string Soon = "скоро";

        // Bottom-bar buttons: small word over a BIG word, like "new JUMP" in the original.
        public const string PlaySmall = "новый", PlayBig = "ПРЫЖОК";
        public const string SoundSmall = "звук", MusicSmall = "музыка", On = "ВКЛ", Off = "ВЫКЛ";
        public const string Back = "НАЗАД";
        public const string MenuSmall = "в", MenuBig = "МЕНЮ";
        public const string RetrySmall = "ещё", RetryBig = "РАЗ";
        public const string ContinueSmall = "продолжить", ContinueBig = "ИГРУ";
        public const string NextSmall = "дальше", NextBig = "УРОВЕНЬ";

        public const string DistanceLabel = "ДО ФЛАЖКА";
        public const string BestLabel = "ЛУЧШЕЕ";
        public const string PointsSmall = "всего", PointsBig = "ОЧКОВ";
        public const string NewBest = "НОВЫЙ РЕКОРД!";
        public const string Dash = "—";

        /// <summary>Заголовок итога в две строки, как «well DONE» / «you FAILED» в оригинале.</summary>
        public static (string small, string big) Title(JumpOutcome outcome) => outcome switch
        {
            JumpOutcome.WellDone => ("отличный", "ПРЫЖОК"),
            JumpOutcome.Died => ("ты", "РАЗБИЛСЯ"),
            JumpOutcome.MissedPlatform => ("мимо", "КРЫШИ"),
            JumpOutcome.TimesUp => ("время", "ВЫШЛО"),
            _ => (string.Empty, string.Empty)
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

        public static string LevelLine(int stage, int level) => $"ЭТАП {stage} — ПРЫЖОК {level}";

        public static string StarsNeeded(int n) => $"ещё {n} {Plural(n, "звезда", "звезды", "звёзд")}\nчтобы открыть";

        public static string PointsShort(int p) => p.ToString("N0", Ru) + " очк.";

        /// <summary>Метры с сотыми, как в оригинале: «0,42 м».</summary>
        public static string Meters(float m) => float.IsNaN(m) || m < 0f ? Dash : m.ToString("0.00", Ru) + " м";

        /// <summary>Таймер с сотыми, как в оригинале: «8,54».</summary>
        public static string Timer(float s) => s.ToString("0.00", Ru);

        public static string Points(int p) => p.ToString("N0", Ru);

        static string Plural(int n, string one, string few, string many)
        {
            int n10 = n % 10, n100 = n % 100;
            if (n10 == 1 && n100 != 11) return one;
            if (n10 >= 2 && n10 <= 4 && (n100 < 12 || n100 > 14)) return few;
            return many;
        }
    }
}
