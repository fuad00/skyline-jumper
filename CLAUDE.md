# Stickman Jumper (рабочее название)

2D бейсджампинг по мотивам заброшенной Stickman Base Jumper (Djinnworks). Android, горизонтальный экран, реклама rewarded + interstitial.
Владелец — начинающий разработчик: отвечать по-русски, просто, с путями и командами.

## Процесс
- «Сначала строим»: план 3–5 строк → «ок» → код → сам проверяю (тесты + автозаезд + смотрю скриншоты) → владелец играет.
- Перед скачиванием, установкой пакетов/программ и коммитами — спрашивать.
- Этап 1: повторить РОВНО механики оригинала (`reference/ORIGINAL_MECHANICS.md`), своё (наклон телефона и т.п.) — потом.

## Чужие ассеты — карантин (обязательно)
- Оригинал и всё извлечённое: `reference/` (в .gitignore). В Unity чужое только в `src/Assets/_ThirdPartyPlaceholder/` (в .gitignore).
- Импорт только через `PlaceholderImporter` (меню Stickman Jumper → Import Placeholders); он же пишет таблицу в `reference/PLACEHOLDER_ASSETS.md`.
- `PlaceholderGuard` валит любую сборку, пока сцены ссылаются на заглушки; тест категории `ReleaseGuard` красный — так задумано.
  Исключение (решено 2026-10-12): Development-сборка с «INTERNAL-TEST» в имени — только для своего телефона (`BuildTools.BuildAndroidTest`) и для симулятора iOS на этом Mac (`BuildTools.BuildIosSimulatorTest`, 2026-10-13); всё в `Builds/` (в .gitignore).
- Имена классов/методов и строки из `.so` оригинала читать можно (разрешено 2026-10-12), код не копируем. Дизассемблировать `libgameCore.so` можно (решено 2026-10-12): только читать числа и логику физики как справку; код оригинала не копируем, разбор лежит в `reference/` и наружу не уходит. Уровни `.landscape` и анимации `.character` в оригинале зашифрованы — сами не вскрываем. Владелец дал расшифрованные XML (2026-10-12): `reference/decoded/` (characters/, levels/). Анимации персонажа импортирует `CharacterImporter` (вызывается из Import Placeholders) → `Assets/_ThirdPartyPlaceholder/Character/` — это тоже заглушки. Формат: кости-дерево, позиция от опоры родителя в px (Y вверх), поворот в градусах ПРОТИВ часовой, anchor от левого нижнего угла картинки; рост стойки 83 px = 1.9 м. Превью: `reference/anim/index.html`. Уровни оригинала (90 шт., 9 этапов) импортирует `LevelImporter` → `Assets/_ThirdPartyPlaceholder/Levels/` (LevelScene + LevelConfig + StageConfig); каталог берёт их, если они есть. Физика (отход, разбег, прыжок, полёт, купол, посадка, таймер) — из кода оригинала, разбор `reference/disasm/FINDINGS.md` (2026-10-13; прежние справочники `GAMEPLAY_REFERENCE.md` и чужой разбор с «бустом» им заменены). В конфигах числа как есть: px и «за тик» (1/60 с), в метры переводит `FlightConfig.PixelsPerMeter` = 22: оригинал при загрузке уровня умножает координаты файла на 0.5 и считает физику в них (файлы — 44 на метр; ptmRatio 8 — не наш метр); шаг физики 1/60 с, как у оригинала. Коротко: разбег с 10 px/с, +3…1 за тик; толчок vx = бег × 0.925, vy 180…174; полёт g 165, предел 1000 (или maxFlySpeedY уровня); купол гасит скорость до ≈66 px/с, сносит ветром (ветер × 3); сесть можно только под куполом при |v| ≤ 110 px/с; таймер с тапа «бежать», вышло в полёте — посадка на цель = «время вышло». Отчёт по 90 уровням: `OriginalLevelsTests.SolvabilityReport` → `src/Logs/`. Подсказки — как у оригинала: только в easy1 (обучение: игра замирает и ждёт тапа, тапы без подсказки не действуют), easy2, advanced1, advanced6, construction1, expert1; импорт `LevelScene.Tutorials`, логика `Gameplay/TutorialRunner`, тексты `UIText.Tutorial`. Итог после прыжка: сел — через 2 с или по тапу, разбился — только по тапу. В тестовой сборке уровень открывается ссылкой `stickmanjumper://level/<id>`. Эталон внешнего вида — видео оригинала (см. `reference/ORIGINAL_MECHANICS.md`). Бренд Djinnworks не использовать.
- Никаких скриншотов/роликов с заглушками наружу.

## Конструктор карт (решено 2026-10-12)
- Детали — картинки уровней оригинала (выбор владельца; это заглушки, в релиз и публичный обмен — только после замены своими). Каталог `PieceCatalog` создаёт `LevelImporter` (`_ThirdPartyPlaceholder/Levels/PieceCatalog.asset`): Id детали = имя картинки (бренд переименован), их НЕ менять — по ним карты находят детали; свои картинки потом подставляем с теми же Id.
- Код: `Scripts/Maps/` — `MapData` (формат, JSON-файл на карту в `persistentDataPath/maps`, `MapStore`), `MapCode` (код для обмена «SJM1.» + сжатые данные; ищется прямо в тексте сообщения), `MapLevel` (карта → обычный `LevelConfig`+`LevelScene`, уровень оригинала → карта, шаблон новой карты), `MapEditor` (правка без картинки: выбор, магнит к краям, ручки, копии, отмена). Пальцы и экраны — `MapBuilder.cs`, картинка — `Presentation/MapEditorView.cs`, интерфейс — `UI/MapBuilderUI.cs`.
- Поделиться можно только пройденной картой: `VerifiedHash` = отпечаток содержимого при посадке на флажок; любая правка (кроме имени) снимает отметку.
- Тесты: `MapTests` (формат, код, сохранение, правка; уровни оригинала через карту играются так же). Автозаезд проходит конструктор (снимки m02…m18) и сохраняет карты в свою папку (`MapStore.FolderOverride`).

## Unity
- Unity 6.6 (6000.6.5f1), проект в `src/`, URP, 2D (ортокамера, спрайты Sprite-Unlit). CLI: `~/.unity/bin/unity` (не в PATH).
- Редактор открыт → управлять вживую (`unity status`, `unity command eval`, скиллы в `src/.claude/skills`). Закрыт → batchmode:
  - сборка проекта: `Unity -batchmode -quit -projectPath src -executeMethod StickmanJumper.EditorTools.ProjectSetup.Run`
  - тесты: `Unity -batchmode -projectPath src -runTests -testPlatform EditMode -testCategory "!ReleaseGuard"`
  - автозаезд: `Unity -batchmode -projectPath src -executeMethod StickmanJumper.EditorTools.SmokeTest.Run -smokeOut DIR` (скриншоты + smoke-summary.txt)
  - тестовый APK: `Unity -batchmode -quit -projectPath src -buildTarget Android -executeMethod StickmanJumper.EditorTools.BuildTools.BuildAndroidTest` → `Builds/StickmanJumper-INTERNAL-TEST-*.apk`
  - iOS-симулятор (модуль iOS Build Support стоит, Xcode 26): `Unity -batchmode -quit -projectPath src -buildTarget iOS -executeMethod StickmanJumper.EditorTools.BuildTools.BuildIosSimulatorTest` → Xcode-проект `Builds/iOS-Simulator-INTERNAL-TEST`; потом
    `xcodebuild -project Builds/iOS-Simulator-INTERNAL-TEST/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Debug -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' -derivedDataPath Builds/iOS-Simulator-DerivedData CODE_SIGNING_ALLOWED=NO build`
    → `Builds/iOS-Simulator-DerivedData/Build/Products/Debug-iphonesimulator/StickmanJumper.app` (ставить через панель симулятора или `xcrun simctl install booted …`). Сборка переключает Unity на iOS; APK потом переключает обратно сам.
- Код: `src/Assets/_Game/Scripts` (asmdef StickmanJumper). Правила — чистые классы в `Gameplay/` (JumpSession, FlightModel, Sweep, LandingJudge, Scoring), картинка в `Presentation/`, склейка в `GameRoot`.
- Цифры — в ScriptableObject `src/Assets/_Game/Config/*.asset` (подсказки на русском). 1 юнит = 1 м, стикмен ≈ 1.9 м, этаж ≈ 2.3 м, заглушки PPU 22, камера далеко (как в оригинале).
- Грабли 6.6: Play без перезагрузки домена (статику сбрасывать руками), снимки камеры рендерить дважды, после NewScene грузить ассеты заново по пути,
  только новая Input System, русский текст в UI Toolkit — свой TTF (PT Sans Bold, OFL — близок к Myriad оригинала).
