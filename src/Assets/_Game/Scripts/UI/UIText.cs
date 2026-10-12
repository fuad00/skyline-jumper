using System.Globalization;
using StickmanJumper.Core;

namespace StickmanJumper.UI
{
    public enum Language { Ru, En }

    /// <summary>
    /// Все тексты для игрока в одном месте, по-русски и по-английски. Язык выбирается в настройках
    /// (по умолчанию русский) и хранится в сохранении.
    /// </summary>
    public static class UIText
    {
        static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        /// <summary>Текущий язык. Ставит GameRoot из сохранения при запуске и при смене в настройках.</summary>
        public static Language Lang { get; set; } = Language.Ru;

        static bool En => Lang == Language.En;
        static string T(string ru, string en) => En ? en : ru;
        static CultureInfo Culture => En ? CultureInfo.InvariantCulture : Ru;

        /// <summary>Код языка для сохранения («ru»/«en»). Пустой или незнакомый код — русский.</summary>
        public static string Code(Language lang) => lang == Language.En ? "en" : "ru";
        public static Language FromCode(string code) => code == "en" ? Language.En : Language.Ru;

        /// <summary>Английский вариант из настроек, если он заполнен, иначе русский.</summary>
        public static string Pick(string ru, string en) => En && !string.IsNullOrEmpty(en) ? en : ru;

        // Logo: working title, two lines like the original's sticker logo. Placeholder until the real name.
        public const string LogoTop = "Stickman.";
        public const string LogoBottom = "jumper";

        public static string HintReady => T("Тапни — отойти для разбега", "Tap to step back for a run-up");
        public static string HintBackingUp => T("Тапни — развернуться и бежать!", "Tap to turn and run!");
        public static string HintRunning => T("Тапни у края — прыжок!", "Tap at the edge to jump!");
        public static string HintFreefall => T("Тапни — парашют!", "Tap to open the parachute!");

        public static string Wind => T("ВЕТЕР", "WIND");
        public static string Pause => T("пауза", "pause");
        public static string Choose => T("Выбери", "select");
        public static string StageWord => T("этап", "stage");
        public static string LevelWord => T("уровень", "level");
        public static string Soon => T("скоро", "soon");

        // Bottom-bar buttons: small word over a BIG word, like "new JUMP" in the original.
        public static string PlaySmall => T("новый", "new");
        public static string PlayBig => T("ПРЫЖОК", "JUMP");
        public static string SettingsBig => T("НАСТРОЙКИ", "SETTINGS");
        public static string BuilderSmall => T("конструктор", "map");
        public static string BuilderBig => T("КАРТ", "BUILDER");
        public static string SoundSmall => T("звук", "sound");
        public static string MusicSmall => T("музыка", "music");
        public static string On => T("ВКЛ", "ON");
        public static string Off => T("ВЫКЛ", "OFF");
        public static string LanguageSmall => T("язык", "language");
        public static string LanguageBig => T("РУССКИЙ", "ENGLISH");
        public static string ResetSmall => T("сбросить", "reset");
        public static string ResetBig => T("ПРОГРЕСС", "PROGRESS");
        public static string Back => T("НАЗАД", "BACK");
        public static string MenuSmall => T("в", "back to");
        public static string MenuBig => T("МЕНЮ", "MENU");
        public static string RetrySmall => T("ещё", "try");
        public static string RetryBig => T("РАЗ", "AGAIN");
        public static string ContinueSmall => T("продолжить", "continue");
        public static string ContinueBig => T("ИГРУ", "GAME");
        public static string NextSmall => T("дальше", "next");
        public static string NextBig => T("УРОВЕНЬ", "LEVEL");

        // Dialogs: «are you sure?» before wiping progress, and the map builder stub.
        public static string SureTitle => T("Вы уверены?", "Are you sure?");
        public static string ResetText => T("Все звёзды и рекорды будут удалены.\nЗвук и язык останутся как есть.",
            "All stars and records will be deleted.\nSound and language stay as they are.");
        public static string NoSmall => T("нет", "no");
        public static string NoBig => T("ОТМЕНА", "CANCEL");
        public static string YesSmall => T("да", "yes");
        public static string YesBig => T("СБРОСИТЬ", "RESET");
        public static string BuilderTitle => T("Скоро!", "Coming soon!");
        public static string BuilderText => T("Конструктор карт появится в следующих версиях:\nтам можно будет строить свои уровни.",
            "The map builder arrives in a future version:\nyou will build your own levels there.");
        public static string OkBig => T("ПОНЯТНО", "OK");
        public static string SecretTitle => T("Секрет!", "Secret!");
        public static string UnlockAllText => T("Все этапы и уровни открыты — можно проверить каждую карту.\nОбычный «сбросить прогресс» вернёт всё как было.",
            "All stages and levels are open — try every map.\nA normal «reset progress» puts everything back.");

        public static string DistanceLabel => T("ДО ФЛАЖКА", "DISTANCE");
        public static string BestLabel => T("ЛУЧШЕЕ", "BEST");
        public static string PointsSmall => T("всего", "total");
        public static string PointsBig => T("ОЧКОВ", "POINTS");
        public static string NewBest => T("НОВЫЙ РЕКОРД!", "NEW RECORD!");
        public const string Dash = "—";

        /// <summary>Заголовок итога в две строки, как «well DONE» / «you FAILED» в оригинале.</summary>
        public static (string small, string big) Title(JumpOutcome outcome) => outcome switch
        {
            JumpOutcome.WellDone => (T("отличный", "well"), T("ПРЫЖОК", "DONE")),
            JumpOutcome.Died => (T("ты", "you"), T("РАЗБИЛСЯ", "DIED")),
            JumpOutcome.MissedPlatform => (T("мимо", "missed"), T("КРЫШИ", "PLATFORM")),
            JumpOutcome.TimesUp => (T("время", "time's"), T("ВЫШЛО", "UP")),
            _ => (string.Empty, string.Empty)
        };

        /// <param name="canopyUsed">Купол уже раскрыт или оторван — подсказка «парашют» больше не нужна.</param>
        public static string Hint(JumpPhase phase, bool canopyUsed) => phase switch
        {
            JumpPhase.Ready => HintReady,
            JumpPhase.BackingUp => HintBackingUp,
            JumpPhase.AtBackWall => HintBackingUp,
            JumpPhase.Running => HintRunning,
            JumpPhase.Airborne => canopyUsed ? string.Empty : HintFreefall,
            _ => string.Empty
        };

        public static string LevelLine(int stage, int level) => En ? $"STAGE {stage} — LEVEL {level}" : $"ЭТАП {stage} — ПРЫЖОК {level}";

        public static string StarsNeeded(int n) => En
            ? $"{n} more {(n == 1 ? "star" : "stars")}\nto unlock"
            : $"ещё {n} {Plural(n, "звезда", "звезды", "звёзд")}\nчтобы открыть";

        public static string PointsShort(int p) => p.ToString("N0", Culture) + T(" очк.", " pts");

        /// <summary>Метры с сотыми, как в оригинале: «0,42 м» / «0.42 m».</summary>
        public static string Meters(float m) => float.IsNaN(m) || m < 0f ? Dash : m.ToString("0.00", Culture) + T(" м", " m");

        /// <summary>Таймер с сотыми, как в оригинале: «8,54» / «8.54».</summary>
        public static string Timer(float s) => s.ToString("0.00", Culture);

        public static string Points(int p) => p.ToString("N0", Culture);

        static string Plural(int n, string one, string few, string many)
        {
            int n10 = n % 10, n100 = n % 100;
            if (n10 == 1 && n100 != 11) return one;
            if (n10 >= 2 && n10 <= 4 && (n100 < 12 || n100 > 14)) return few;
            return many;
        }
    }
}
