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
- Имена классов/методов и строки из `.so` оригинала читать можно (разрешено 2026-10-12), код не копируем. Уровни `.landscape` и анимации `.character` зашифрованы — не вскрываем. Эталон внешнего вида — видео оригинала (см. `reference/ORIGINAL_MECHANICS.md`). Бренд Djinnworks не использовать.
- Никаких скриншотов/роликов с заглушками наружу.

## Unity
- Unity 6.6 (6000.6.5f1), проект в `src/`, URP, 2D (ортокамера, спрайты Sprite-Unlit). CLI: `~/.unity/bin/unity` (не в PATH).
- Редактор открыт → управлять вживую (`unity status`, `unity command eval`, скиллы в `src/.claude/skills`). Закрыт → batchmode:
  - сборка проекта: `Unity -batchmode -quit -projectPath src -executeMethod StickmanJumper.EditorTools.ProjectSetup.Run`
  - тесты: `Unity -batchmode -projectPath src -runTests -testPlatform EditMode -testCategory "!ReleaseGuard"`
  - автозаезд: `Unity -batchmode -projectPath src -executeMethod StickmanJumper.EditorTools.SmokeTest.Run -smokeOut DIR` (скриншоты + smoke-summary.txt)
- Код: `src/Assets/_Game/Scripts` (asmdef StickmanJumper). Правила — чистые классы в `Gameplay/` (JumpSession, FlightModel, Sweep, LandingJudge, Scoring), картинка в `Presentation/`, склейка в `GameRoot`.
- Цифры — в ScriptableObject `src/Assets/_Game/Config/*.asset` (подсказки на русском). 1 юнит = 1 м, стикмен ≈ 1.9 м, этаж ≈ 2.3 м, заглушки PPU 22, камера далеко (как в оригинале).
- Грабли 6.6: Play без перезагрузки домена (статику сбрасывать руками), снимки камеры рендерить дважды, после NewScene грузить ассеты заново по пути,
  только новая Input System, русский текст в UI Toolkit — свой TTF (Rubik ExtraBold, OFL).
