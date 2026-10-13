using System.Collections.Generic;
using System.Globalization;
using StickmanJumper.Core;
using UnityEngine;

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

        /// <summary>
        /// Подсказки, стоящие в уровнях (как в оригинале — только там, где появляется новое). Ключ — «уровень:имя надписи».
        /// Нет перевода — текст из файла уровня.
        /// </summary>
        static readonly Dictionary<string, (string ru, string en)> Tutorials = new Dictionary<string, (string, string)>
        {
            ["easy1:Tutorial1"] = ("Тапни — отойди назад для разбега", "Tap to walk back for a run-up"),
            ["easy1:Tutorial2"] = ("Тапни ещё раз — разбегись", "Tap again to run and gain speed"),
            ["easy1:Tutorial3"] = ("Ещё тап — прыжок!", "One more tap to jump off"),
            ["easy1:Tutorial4"] = ("Самое время раскрыть парашют.\nТапни!", "Now is a good time to open your\nparachute. Tap again!"),
            ["easy2:Tutorial1"] = ("Приземлись, пока не вышло время!", "Land before time's up!"),
            ["advanced1:Tutorial1"] = ("Панель ветра показывает\nего направление и силу", "The wind display shows\nwind direction and speed"),
            ["advanced6:Tutorial1"] = ("Можно пролететь сквозь леса?\nМожно!", "Can I fly through scaffolds?\nYes I can!"),
            ["construction1:Tutorial1"] = ("Разобьём пару стёкол…", "Let's smash some window panes…"),
            ["expert1:Tutorial1"] = ("Иногда нужна промежуточная посадка.\nПрыгай на площадку с зелёным флажком", "Some jumps need an intermediate landing.\nJump to the landing zone with the green flag"),
            ["expert1:Tutorial2"] = ("Теперь прыгай к последней площадке", "Now jump to the final landing zone"),
        };

        public static string Tutorial(string levelId, string key, string fallback) =>
            Tutorials.TryGetValue($"{levelId}:{key}", out var t) ? T(t.ru, t.en) : fallback;

        public static string Wind => T("ВЕТЕР", "WIND");
        public static string Pause => T("пауза", "pause");
        public static string SettingsTitle => T("настройки", "settings");
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

        // Dialogs: «are you sure?» before wiping progress.
        public static string SureTitle => T("Вы уверены?", "Are you sure?");
        public static string ResetText => T("Все звёзды и рекорды будут удалены.\nЗвук и язык останутся как есть.",
            "All stars and records will be deleted.\nSound and language stay as they are.");
        public static string NoSmall => T("нет", "no");
        public static string NoBig => T("ОТМЕНА", "CANCEL");
        public static string YesSmall => T("да", "yes");
        public static string YesBig => T("СБРОСИТЬ", "RESET");
        public static string OkBig => T("ПОНЯТНО", "OK");
        public static string SecretTitle => T("Секрет!", "Secret!");
        public static string UnlockAllText => T("Все этапы и уровни открыты — можно проверить каждую карту.\nОбычный «сбросить прогресс» вернёт всё как было.",
            "All stages and levels are open — try every map.\nA normal «reset progress» puts everything back.");

        // ---------------------------------------------------------------- map builder

        public static string MapsSmall => T("мои", "my");
        public static string MapsBig => T("карты", "maps");
        public static string MapWord => T("Карта", "Map");
        public static string NewMapSmall => T("новая", "new");
        public static string NewMapBig => T("КАРТА", "MAP");
        public static string FromLevelSmall => T("из", "from");
        public static string FromLevelBig => T("УРОВНЯ", "LEVEL");
        public static string PasteSmall => T("вставить", "paste");
        public static string PasteBig => T("КОД", "CODE");
        public static string NoMaps => T("Пока нет своих карт.\nНажми «новая карта» — или вставь код от друга.",
            "No maps yet.\nTap «new map» — or paste a code from a friend.");
        public static string StatusPassed => T("ПРОЙДЕНА", "PASSED");
        public static string StatusDraft => T("ЧЕРНОВИК", "DRAFT");
        public static string StatusFriend => T("ОТ ДРУГА", "FROM A FRIEND");
        public static string MapActions => T("Что сделать с картой?", "What to do with the map?");
        public static string PlayMapBig => T("ИГРАТЬ", "PLAY");
        public static string EditBig => T("ПРАВИТЬ", "EDIT");
        public static string ShareBig => T("ПОДЕЛИТЬСЯ", "SHARE");
        public static string DeleteBig => T("УДАЛИТЬ", "DELETE");
        public static string DeleteTitle => T("Удалить карту?", "Delete the map?");
        public static string DeleteText(string name) => T($"«{name}» пропадёт с телефона навсегда.", $"«{name}» will be gone from the phone for good.");
        public static string ToEditorSmall => T("в", "to");
        public static string ToEditorBig => T("РЕДАКТОР", "EDITOR");
        public static string ToMapsSmall => T("к", "to");
        public static string ToMapsBig => T("КАРТАМ", "MAPS");
        public static string MapLine(string name) => T($"КАРТА «{name}»", $"MAP «{name}»");

        public static string GroupName(Config.PieceGroup g) => g switch
        {
            Config.PieceGroup.Buildings => T("Дома", "Buildings"),
            Config.PieceGroup.Construction => T("Стройка", "Construction"),
            Config.PieceGroup.Street => T("Улица", "Street"),
            _ => T("Небо", "Sky"),
        };
        public static string StripsTab => T("Площадки", "Platforms");
        public static string StartWord => T("Старт", "Start");
        public static string TargetWord => T("Цель", "Target");
        public static string PlatformWord => T("Платформа", "Platform");
        public static string PlatformNew => T("Новая платформа", "New platform");
        public static string Undo => T("Отменить", "Undo");
        public static string Redo => T("Вернуть", "Redo");
        public static string Physics => T("Стены", "Walls");
        public static string Test => T("Проверить", "Test");
        public static string SettingsWord => T("Настройки", "Settings");
        public static string ShareWord => T("Поделиться", "Share");

        public static string CopyRight => T("Рядом", "Beside");
        public static string CopyUp => T("Сверху", "On top");
        public static string WholeBuilding => T("Здание", "Building");
        public static string Flip => T("Отразить", "Flip");
        public static string Closer => T("Ближе", "Front");
        public static string Farther => T("Дальше", "Back");
        public static string SolidWord => T("Твёрдое", "Solid");
        public static string BackdropWord => T("Фон", "Backdrop");
        public static string ResetShape => T("Сброс", "Reset");
        public static string DeleteWord => T("Удалить", "Delete");
        public static string RunLeft => T("Налево", "Run left");
        public static string RunRight => T("Направо", "Run right");
        public static string StripHint => T("Тяни полосу или её края", "Drag the strip or its ends");

        public static string MapSettings => T("Настройки карты", "Map settings");
        public static string NameWord => T("Название", "Name");
        public static string TimeWord => T("Время", "Time");
        public static string WindWord => T("Ветер", "Wind");
        public static string NightWord => T("Ночь", "Night");
        public static string Seconds(float s) => s.ToString("0", Culture) + T(" с", " s");
        public static string WindSpeed(float w) => w == 0f ? T("нет", "none") : (w > 0f ? T("вправо ", "right ") : T("влево ", "left ")) + Mathf.Abs(w).ToString("0.00", Culture) + T(" м/с", " m/s");
        public static string YesWord => T("ДА", "YES");
        public static string NoWord => T("НЕТ", "NO");
        public static string DoneBig => T("ГОТОВО", "DONE");

        public static string Passed => T("Пройдено! Теперь картой можно поделиться", "Passed! Now you can share the map");
        public static string PassFirst => T("Сначала пройди карту сам — потом можно делиться", "Pass the map yourself first — then you can share it");
        public static string StartTooShort => T("Старт короче 4 м — негде разбежаться", "The start is shorter than 4 m — no room to run");
        public static string TargetTooNarrow => T("Цель слишком узкая", "The target is too narrow");
        public static string CodeCopied => T("Код скопирован — отправь его другу", "Code copied — send it to a friend");
        public static string NoCode => T("В буфере нет кода карты", "No map code in the clipboard");
        public static string BrokenCode => T("Код повреждён — попроси прислать ещё раз", "The code is broken — ask for it again");
        public static string NewerCode => T("Код от более новой версии игры — обнови игру", "The code is from a newer version — update the game");
        public static string MapAdded => T("Карта добавлена", "Map added");

        public static string ShareTitle => T("Поделиться картой", "Share the map");
        public static string ShareText(int length) => T($"Код карты — {length} знаков. Отправь его другу:\nон нажмёт «вставить код» в своих картах.",
            $"The map code is {length} characters. Send it to a friend:\nthey tap «paste code» in their maps.");
        public static string CopyBig => T("КОПИРОВАТЬ", "COPY");
        public static string SendBig => T("ОТПРАВИТЬ", "SEND");
        public static string ShareMessage(string name, string code) => T(
            $"Карта «{name}» для Stickman Jumper. Скопируй всё сообщение и нажми «вставить код» в «моих картах»:\n{code}",
            $"Map «{name}» for Stickman Jumper. Copy this whole message and tap «paste code» in «my maps»:\n{code}");
        public static string PasteTitle => T("Код карты", "Map code");
        public static string PasteHelp => T("Вставь сюда сообщение с кодом (долгое нажатие → «Вставить»)", "Paste the message with the code here (long press → «Paste»)");
        public static string AddBig => T("ДОБАВИТЬ", "ADD");
        public static string ImportTitle => T("Новая карта!", "A new map!");
        public static string ImportText(string name, int pieces, bool passed) => T(
            $"«{name}», деталей: {pieces}." + (passed ? "\nАвтор её прошёл." : string.Empty),
            $"«{name}», {pieces} pieces." + (passed ? "\nThe author has passed it." : string.Empty));

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
