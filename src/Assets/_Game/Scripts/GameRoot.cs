using System;
using System.Collections.Generic;
using StickmanJumper.Audio;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using StickmanJumper.Maps;
using StickmanJumper.Presentation;
using StickmanJumper.Save;
using StickmanJumper.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace StickmanJumper
{
    /// <summary>
    /// Точка сборки сцены и переходы между экранами, как в оригинале: главное меню (настройки) → выбор этапа → выбор уровня →
    /// прыжок (пауза) → итог → ещё раз / дальше / в меню. Плюс конструктор карт (<see cref="MapBuilder"/>): свои карты
    /// играются так же, как уровни. Правила прыжка живут в JumpSession, здесь только склейка.
    /// Синглтонов нет. Клавиатура в редакторе: Пробел — тап, Z — зум, R — ещё раз, Esc/P — пауза (в меню — назад).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameRoot : MonoBehaviour
    {
        [Header("Уровни (этапы по порядку)")]
        [SerializeField] GameCatalog _catalog;

        [Header("Настройки (крути их в Inspector)")]
        [SerializeField] JumpConfig _jumpConfig;
        [SerializeField] FlightConfig _flightConfig;
        [SerializeField] ScoreConfig _scoreConfig;
        [SerializeField] CameraConfig _cameraConfig;
        [SerializeField] StickmanConfig _stickmanConfig;

        [Header("Картинки и звуки")]
        [SerializeField] ArtBank _art;
        [SerializeField] SoundBank _sounds;

        [Header("Сцена")]
        [SerializeField] Camera _camera;

        SaveService _save;
        Color _dayBackground;

        // Menu demo: behind the menus the game plays itself on random levels and always lands on the flag.
        readonly List<(int stage, int level)> _demoLevels = new List<(int, int)>();
        readonly System.Random _demoRng = new System.Random();
        bool _demo;
        float _demoEndAt = -1f;
        int _menuStage;
        Action<JumpSession> _pilotStep;
        CameraRig2D _cameraRig;
        AudioService _audio;
        GameUI _ui;
        MapBuilder _builder;

        // A map from the builder being played (null — an ordinary level). From the editor: back to the editor afterwards.
        MapData _mapPlay;
        bool _mapFromEditor;
        /// <summary>Этапы и уровни выбирают, чтобы сделать из уровня карту (а не сыграть его).</summary>
        bool _pickForMap;
        /// <summary>Уровень, созданный в памяти из карты (удалить, когда больше не нужен).</summary>
        LevelConfig _ownedLevel;

        // Current level (rebuilt by LoadLevel).
        LevelConfig _level;
        LevelLayout _layout;
        JumpSession _session;
        LevelView _levelView;
        StickmanView _stickman;
        Transform _world;
        int _stage = -1, _levelIndex = -1;

        float _finishedAtReal = -1f, _calmSince = -1f, _slowMoUntilReal, _timeScaleBeforePause = 1f;
        int _runIndex;
        bool _tapQueued, _slowMoUsed, _playing;

        public bool Ready => _ui != null && _session != null;
        public JumpSession Session => _session;
        public StickmanView Stickman => _stickman;
        public GameUI UI => _ui;
        public CameraRig2D CameraRig => _cameraRig;
        public LevelConfig Level => _level;
        public GameCatalog Catalog => _catalog;
        public SaveService Save => _save;
        public int StageIndex => _stage;
        public MapBuilder Builder => _builder;
        /// <summary>Сейчас играется карта из конструктора.</summary>
        public MapData PlayingMap => _mapPlay;
        public int LevelIndex => _levelIndex;
        public bool Paused { get; private set; }

        /// <summary>Если задан — играет сам (автозаезд). null — играет человек.</summary>
        public Autopilot Autopilot { get; set; }

        /// <summary>Последний итог (для автотеста).</summary>
        public JumpScore LastScore { get; private set; }

        /// <summary>Сколько уровней годятся для заставки в меню (автопилот на них точно садится на флажок).</summary>
        public int DemoLevelCount => _demoLevels.Count;
        /// <summary>Сейчас за меню играет заставка.</summary>
        public bool DemoRunning => _demo;

        void Awake()
        {
            Application.targetFrameRate = 60; // Android defaults to 30
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Time.timeScale = 1f; // Play mode starts without a domain reload: never inherit a slow-mo
            AudioListener.pause = false;

            _save = new SaveService(new PlayerPrefsSaveStore());
            _save.Load();
            UIText.Lang = UIText.FromCode(_save.Data.Language); // static: survives Play mode without a domain reload
            _cameraRig = new CameraRig2D(_camera, _cameraConfig);
            _dayBackground = _camera.backgroundColor;
            _audio = new AudioService(_camera.gameObject, _sounds);
            _audio.SetSound(_save.Data.Sound);
            _audio.SetMusic(_save.Data.Music);

            _ui = new GameUI(GetComponent<UIDocument>().rootVisualElement, _art);
            _ui.Tapped += () => _tapQueued = true;
            _ui.PlayClicked += () => { _audio.Click(); ShowStageSelect(); };
            _ui.SettingsClicked += () => { _audio.Click(); ShowSettings(); };
            _ui.BuilderClicked += () => { _audio.Click(); ShowMaps(); };
            _ui.LanguageClicked += () => { _audio.Click(); SetLanguage(UIText.Lang == Language.Ru ? Language.En : Language.Ru); };
            _ui.ResetClicked += () => { _audio.Click(); AskResetProgress(); };
            _ui.UnlockAllHeld += () => { _audio.Click(); UnlockAll(); };
            _ui.SoundClicked += () => { _save.Data.Sound = !_save.Data.Sound; _audio.SetSound(_save.Data.Sound); _save.Persist(); _audio.Click(); RefreshSoundButtons(); };
            _ui.MusicClicked += () => { _save.Data.Music = !_save.Data.Music; _audio.SetMusic(_save.Data.Music); _save.Persist(); _audio.Click(); RefreshSoundButtons(); };
            _ui.StageClicked += i => { _audio.Click(); ShowLevelSelect(i); };
            _ui.LevelClicked += i => { _audio.Click(); OnLevelClicked(i); };
            _ui.BackClicked += () => { _audio.Click(); Back(); };
            _ui.MenuClicked += () => { _audio.Click(); LeaveToMenu(); };
            _ui.RetryClicked += () => { _audio.Click(); Retry(); };
            _ui.NextClicked += () => { _audio.Click(); StartLevel(_stage, _levelIndex + 1); };
            _ui.ContinueClicked += () => { _audio.Click(); SetPaused(false); };
            _ui.PauseClicked += () => { _audio.Click(); SetPaused(true); };
            _ui.ZoomClicked += () => { _audio.Click(); _cameraRig.Overview = !_cameraRig.Overview; };
            RefreshSoundButtons();

            _builder = new MapBuilder(_camera, _art, _ui, _dayBackground)
            {
                Play = PlayMap,
                Exit = ShowMainMenu,
                PickLevel = () => { _pickForMap = true; ShowStageSelect(); },
                ListShown = () =>
                {
                    EnterMenu(); // the demo plays behind the list again
                    _camera.backgroundColor = _levelView.Night ? Color.black : _dayBackground;
                },
                Click = () => _audio.Click(),
            };

            _pilotStep = s => Autopilot?.Update(s);
            for (int s = 0; s < _catalog.Stages.Length; s++)
                for (int l = 0; _catalog.Stages[s] != null && l < _catalog.Stages[s].Levels.Length; l++)
                {
                    var level = _catalog.Level(s, l);
                    if (level != null && !float.IsNaN(level.DemoRunUp) && !float.IsNaN(level.DemoOpenY)) _demoLevels.Add((s, l));
                }

            LoadLevel(0, 0);
            ShowMainMenu();
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        // ------------------------------------------------------------------ screens

        public void ShowMainMenu()
        {
            _pickForMap = false;
            _builder.Leave();
            EnterMenu();
            _ui.Show(UIScreen.MainMenu);
        }

        /// <summary>Конструктор карт: «Мои карты» (за списком играет заставка).</summary>
        public void ShowMaps()
        {
            _pickForMap = false;
            _builder.ShowList();
        }

        /// <summary>Настройки, как Options в оригинале: та же нижняя панель, другие кнопки (звук, музыка, язык, сброс).</summary>
        public void ShowSettings()
        {
            EnterMenu();
            _ui.Show(UIScreen.Settings);
        }

        /// <summary>Сменить язык и сразу переписать все подписи (выбор хранится в сохранении).</summary>
        public void SetLanguage(Language lang)
        {
            UIText.Lang = lang;
            _save.Data.Language = UIText.Code(lang);
            _save.Persist();
            _ui.ApplyLanguage();
        }

        /// <summary>«Сбросить прогресс» → «Вы уверены?» → да: звёзды и рекорды стёрты.</summary>
        public void AskResetProgress()
        {
            _ui.ShowDialog(UIText.SureTitle, UIText.ResetText,
                new DialogButton(UIText.NoSmall, UIText.NoBig, SlantButton.Blue, () => { _audio.Click(); _ui.HideDialog(); }),
                new DialogButton(UIText.YesSmall, UIText.YesBig, SlantButton.Red, () => { _audio.Click(); ResetProgress(); _ui.HideDialog(); }));
        }

        void ResetProgress()
        {
            _save.ResetProgress();
            _save.Persist();
            _session.ForgetHistory(); // no «Last» flag from before the reset
            _levelView.SetLastRunFlag(float.NaN);
        }

        /// <summary>Секрет для ручной проверки карт: все этапы и уровни открыты (сброс прогресса возвращает как было).</summary>
        public void UnlockAll()
        {
            _save.Data.UnlockAll = true;
            _save.Persist();
            _ui.ShowDialog(UIText.SecretTitle, UIText.UnlockAllText,
                new DialogButton(null, UIText.OkBig, SlantButton.Green, () => { _audio.Click(); _ui.HideDialog(); }));
        }

        public void ShowStageSelect()
        {
            _builder.Leave();
            EnterMenu();
            var cards = new List<StageCardView>();
            for (int s = 0; s < _catalog.Stages.Length; s++) cards.Add(StageCard(s));
            _ui.SetStages(cards, _menuStage); // back from a stage's levels: that stage stays in the centre
            _ui.Show(UIScreen.StageSelect);
        }

        public void ShowLevelSelect(int stage)
        {
            stage = Mathf.Clamp(stage, 0, _catalog.Stages.Length - 1);
            _menuStage = stage;
            EnterMenu(); // the demo keeps playing behind the level list
            var ids = _catalog.LevelIds(stage);
            var nodes = new List<LevelNodeView>();
            for (int i = 0; i < ids.Count; i++)
            {
                var rec = _save.Get(ids[i]);
                // Picking a level to make a map of it: every level will do (it is only copied, not played).
                nodes.Add(new LevelNodeView { Number = i + 1, Stars = rec?.Stars ?? 0, Points = rec?.BestPoints ?? 0, Locked = !_pickForMap && !_save.IsUnlocked(ids, i) });
            }
            _ui.SetLevels(StageCard(stage), nodes);
            _ui.Show(UIScreen.LevelSelect);
        }

        /// <summary>Первая кнопка паузы и итога: к уровням этапа, а с карты — в редактор или к списку карт.</summary>
        public void LeaveToMenu()
        {
            SetPaused(false);
            if (_mapPlay != null) LeaveMap();
            else ShowLevelSelect(_stage);
        }

        /// <summary>Тап по плитке уровня (для автотеста).</summary>
        public void ClickLevel(int index) => OnLevelClicked(index);

        /// <summary>Тап по уровню: сыграть его — или, если выбирают уровень для конструктора, сделать из него карту.</summary>
        void OnLevelClicked(int index)
        {
            // The level list shows _menuStage (the demo behind it may be on another stage).
            if (_pickForMap)
            {
                var level = _catalog.Level(_menuStage, index);
                if (level == null) return;
                _pickForMap = false;
                var stage = _catalog.Stages[_menuStage];
                _builder.CreateFromLevel(level, $"{UIText.Pick(stage.Title, stage.TitleEn)} {index + 1}");
                return;
            }
            StartLevel(_menuStage, index);
        }

        /// <summary>Начать уровень (экран выбора, «дальше», автотест).</summary>
        public void StartLevel(int stage, int level)
        {
            if (_catalog.Level(stage, level) == null) return;
            // Leave the demo: a fresh session (no «Last» flag from the demo's run), sounds back on.
            _demo = false;
            Autopilot = null;
            _audio.Quiet = false;
            _menuStage = stage;
            _builder.Leave();
            ForgetMap();
            LoadLevel(stage, level);
            _playing = true;
            _ui.Show(UIScreen.Game);
            StartRun();
        }

        StageCardView StageCard(int s)
        {
            var stage = _catalog.Stages[s];
            var ids = _catalog.LevelIds(s);
            int before = _save.TotalStars(_catalog.LevelIdsBefore(s));
            return new StageCardView
            {
                Number = stage.Number,
                Title = UIText.Pick(stage.Title, stage.TitleEn),
                Subtitle = UIText.Pick(stage.Subtitle, stage.SubtitleEn),
                Stars = _save.TotalStars(ids),
                MaxStars = ids.Count * 3,
                NeedStars = Mathf.Max(0, stage.StarsToUnlock - before),
                Locked = !_pickForMap && !_save.Data.UnlockAll && before < stage.StarsToUnlock,
                Empty = ids.Count == 0,
            };
        }

        void Back()
        {
            if (_ui.DialogVisible) _ui.DialogBack();
            else if (_builder.Back()) { }
            else if (_ui.Screen == UIScreen.LevelSelect) ShowStageSelect();
            else if (_ui.Screen == UIScreen.StageSelect && _pickForMap) ShowMaps();
            else ShowMainMenu();
        }

        // ------------------------------------------------------------------ maps from the builder

        /// <summary>Сыграть карту конструктора (из редактора — проверить её; после игры вернуться туда же).</summary>
        public void PlayMap(MapData map, bool fromEditor)
        {
            _demo = false;
            Autopilot = null;
            _audio.Quiet = false;
            _builder.Leave();
            _mapPlay = map;
            _mapFromEditor = fromEditor;
            _ui.SetMenuButton(fromEditor ? () => UIText.ToEditorSmall : () => UIText.ToMapsSmall, fromEditor ? () => UIText.ToEditorBig : () => UIText.ToMapsBig);
            LoadLevel(MapLevel.ToLevel(map, _art.Pieces), -1, -1);
            _playing = true;
            _ui.Show(UIScreen.Game);
            StartRun();
        }

        /// <summary>С карты — назад в редактор (если проверяли) или к списку карт.</summary>
        void LeaveMap()
        {
            bool toEditor = _mapFromEditor;
            ForgetMap();
            _playing = false;
            Time.timeScale = 1f;
            if (toEditor) _builder.Resume();
            else ShowMaps();
        }

        void ForgetMap()
        {
            _mapPlay = null;
            _ui.SetMenuButton(null, null);
        }

        void EnterMenu()
        {
            _ui.HideDialog();
            _playing = false;
            SetPaused(false);
            if (!_demo) StartDemo();
        }

        /// <summary>Заставка в меню: случайный уровень (не тот же, что только что), автопилот с заранее посчитанным планом.</summary>
        void StartDemo()
        {
            if (_demoLevels.Count == 0)
            {
                // Nothing the autopilot can land: the stickman just stands on the roof, like before.
                Autopilot = null;
                if (_session != null && _session.Phase != JumpPhase.Ready) StartRun();
                return;
            }
            var pick = _demoLevels[_demoRng.Next(_demoLevels.Count)];
            if (_demoLevels.Count > 1 && pick.stage == _stage && pick.level == _levelIndex) pick = _demoLevels[(_demoLevels.IndexOf(pick) + 1) % _demoLevels.Count];
            _demo = true;
            _demoEndAt = -1f;
            _audio.Quiet = true;
            LoadLevel(pick.stage, pick.level);
            var level = _catalog.Level(pick.stage, pick.level);
            Autopilot = new Autopilot { DesiredRunUp = level.DemoRunUp, OpenAtY = level.DemoOpenY };
        }

        /// <summary>Заставка доиграла (сел) — небольшая пауза полюбоваться, потом следующий случайный уровень.</summary>
        void UpdateDemo()
        {
            if (!_demo || !_session.Phase.IsFinished()) return;
            if (_demoEndAt < 0f) _demoEndAt = Time.unscaledTime;
            else if (Time.unscaledTime - _demoEndAt > 2.5f) StartDemo();
        }

        void RefreshSoundButtons() => _ui.SetSound(_save.Data.Sound, _save.Data.Music);

        // ------------------------------------------------------------------ level & run

        void LoadLevel(int stage, int level) => LoadLevel(_catalog.Level(stage, level), stage, level);

        /// <param name="stage">Этап; −1 — карта из конструктора (её уровень создан в памяти, удалим его потом).</param>
        void LoadLevel(LevelConfig config, int stage, int level)
        {
            if (_world != null) Destroy(_world.gameObject);
            if (_ownedLevel != null && _ownedLevel != config) MapLevel.Destroy(_ownedLevel);
            _ownedLevel = stage < 0 ? config : null;
            _stickman = null;
            _stage = stage;
            _levelIndex = level;
            _level = config;
            _world = new GameObject("World").transform;
            _layout = LevelLayout.Build(_level, _jumpConfig);
            if (_session != null) _session.Happened -= OnJumpEvent;
            _session = new JumpSession(_layout, _jumpConfig, _flightConfig);
            _session.Happened += OnJumpEvent;
            _levelView = new LevelView(_world, _level, _layout, _art);
            _camera.backgroundColor = _levelView.Night ? Color.black : _dayBackground; // night: no paper, black sky
            StartRun();
        }

        void StartRun()
        {
            _runIndex++;
            Time.timeScale = 1f;
            _finishedAtReal = -1f;
            _calmSince = -1f;
            _tapQueued = false;
            _slowMoUsed = false;
            _slowMoUntilReal = 0f;
            _stickman?.Destroy();
            _session.Reset();
            _levelView.SetLastRunFlag(_session.LastRunStartX);
            _levelView.ResetPlatformFlags();
            _stickman = new StickmanView(_world, _stickmanConfig, _art);
            _stickman.SetNight(_levelView.Night);
            _stickman.RagdollImpact += OnRagdollImpact;
            _audio.ResetRun();
            _cameraRig.Overview = false;
            if (_playing || _demo) _cameraRig.Snap(CameraRig2D.OverviewCenter(_layout.Overview), _cameraRig.FitSize(_layout.Overview));
            else _cameraRig.Snap(MenuCenter, _cameraConfig.SizeOnRoof);
            LastScore = default;
            if (_playing) _ui.Show(UIScreen.Game);
        }

        /// <summary>В меню камера показывает стартовую крышу слева, как главный экран оригинала.</summary>
        Vector2 MenuCenter => new Vector2(_layout.EdgeX + 18f, _layout.RoofY + 5f);

        /// <summary>Начать уровень заново (кнопка «Ещё раз», клавиша R, автотест).</summary>
        public void Retry()
        {
            SetPaused(false);
            _playing = true;
            StartRun();
        }

        /// <summary>Тап игрока (экран, пробел или автотест).</summary>
        public void Tap() => _tapQueued = true;

        /// <summary>Пауза: время стоит, звук молчит.</summary>
        public void SetPaused(bool paused)
        {
            if (Paused == paused) return;
            Paused = paused;
            _ui.ShowPause(paused);
            AudioListener.pause = paused;
            if (paused)
            {
                _timeScaleBeforePause = Time.timeScale;
                Time.timeScale = 0f;
            }
            else Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float realDt = Time.unscaledDeltaTime;
            _ui.ApplySafeArea(Screen.safeArea, Screen.width, Screen.height);
            ReadKeyboard();
            // The map editor has the screen: the game world sleeps behind it.
            bool editing = _builder.EditorOpen;
            if (_world != null && _world.gameObject.activeSelf == editing) _world.gameObject.SetActive(!editing);
            _builder.Update(realDt);
            if (editing) return;
            if (Paused)
            {
                _tapQueued = false;
                return;
            }

            if (_playing)
            {
                if (_tapQueued && !_ui.SummaryVisible) _session.Tap();
            }
            _tapQueued = false;
            _session.Advance(dt, Autopilot != null && (_playing || _demo) ? _pilotStep : null); // the pilot acts on every physics step

            if (!_stickman.IsRagdoll) _stickman.UpdateAlive(_session, dt);
            _stickman.UpdateDetached(dt); // a canopy that was torn or cut away keeps flying

            Vector2 focus = _stickman.IsRagdoll ? _stickman.Center : _session.Feet;
            // Standing on an intermediate platform is not the level start: no overview there, keep following him.
            var cameraPhase = _session.Phase == JumpPhase.Ready && _session.RunwayIndex > 0 ? JumpPhase.AtBackWall : _session.Phase;
            if (_playing || _demo) _cameraRig.Update(focus, _session.Velocity, cameraPhase, _stickman.IsRagdoll, _layout.Overview, realDt);
            else _cameraRig.UpdateFixed(MenuCenter, _cameraConfig.SizeOnRoof, realDt);
            _levelView.SetPlayerLight(focus + Vector2.up);
            _levelView.SetGuideVisible((_playing || _demo) && _session.Phase == JumpPhase.Airborne);
            _levelView.Update(_camera, Time.time, dt);
            _audio.Update(_session, _stickman.IsRagdoll, realDt);

            if (_playing && !_ui.SummaryVisible)
            {
                _ui.SetHud(_session.TimeLeft, _layout.TimeLimit, _layout.Wind, _level.WindMax, _cameraRig.Overview);
                _ui.SetMinimap(_layout, focus, _session.LastRunStartX);
                _ui.SetHint(UIText.Hint(_session.Phase, _session.CanopyDeployed || _session.CanopyCut));
            }

            UpdateSlowMo();
            if (_playing) UpdateFinish();
            UpdateDemo();
        }

        void ReadKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            bool esc = kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame;
            if (!_playing)
            {
                if (_ui.Screen == UIScreen.StageSelect && !_ui.DialogVisible)
                {
                    if (kb.leftArrowKey.wasPressedThisFrame) _ui.FocusStage(_ui.StageFocus - 1);
                    if (kb.rightArrowKey.wasPressedThisFrame) _ui.FocusStage(_ui.StageFocus + 1);
                }
                // Esc / the phone's back button: close the dialog, or go one screen back.
                if (kb.escapeKey.wasPressedThisFrame && (_ui.DialogVisible || _ui.Screen != UIScreen.MainMenu)) Back();
                return;
            }
            if (esc && !_ui.SummaryVisible) SetPaused(!Paused);
            if (Paused) return;
            if (kb.spaceKey.wasPressedThisFrame) _tapQueued = true;
            if (kb.zKey.wasPressedThisFrame) _cameraRig.Overview = !_cameraRig.Overview;
            if (kb.rKey.wasPressedThisFrame) Retry();
        }

        void OnJumpEvent(JumpEvent e)
        {
            _audio.OnEvent(e, _session);
            switch (e)
            {
                case JumpEvent.StartedBackingUp:
                    _cameraRig.Overview = false;
                    break;
                case JumpEvent.ReachedPlatform:
                    _levelView.SetPlatformReached(_session.RunwayIndex, true);
                    _stickman.DropCanopy(_session.ImpactVelocity);
                    break;
                case JumpEvent.CanopyCut:
                    _stickman.CutCanopy(_session.Velocity, _runIndex);
                    break;
                case JumpEvent.Stumbled:
                case JumpEvent.Crashed:
                    _stickman.BecomeRagdoll(_session.ImpactVelocity, _runIndex);
                    break;
            }
        }

        void OnRagdollImpact(float speed)
        {
            _audio.OnRagdollImpact(speed);
            if (speed > 9f && !_slowMoUsed)
            {
                _slowMoUsed = true;
                // First hard hit: dramatic slow-mo + camera shake (once per run).
                Time.timeScale = _stickmanConfig.SlowMoScale;
                _slowMoUntilReal = Time.unscaledTime + _stickmanConfig.SlowMoDuration;
                _cameraRig.Shake(_cameraConfig.ShakeOnCrash);
            }
        }

        void UpdateSlowMo()
        {
            if (_slowMoUntilReal <= 0f) return;
            if (Time.unscaledTime >= _slowMoUntilReal)
            {
                Time.timeScale = Mathf.MoveTowards(Time.timeScale, 1f, Time.unscaledDeltaTime * 2.5f);
                if (Time.timeScale >= 1f) _slowMoUntilReal = 0f;
            }
        }

        void UpdateFinish()
        {
            if (!_session.Phase.IsFinished() || _ui.SummaryVisible) return;
            float now = Time.unscaledTime;
            if (_finishedAtReal < 0f) _finishedAtReal = now;

            // After a landing give the original's animation time: the canopy drops, then he cheers (or sulks).
            float wait = _session.Phase == JumpPhase.Landed ? 2.2f : _session.Phase == JumpPhase.TimeUp ? 0.6f : 4f;
            if (_stickman.IsRagdoll)
            {
                // Show the result once the body has settled (or after 4 s anyway).
                if (_stickman.RagdollSpeed < 0.6f)
                {
                    if (_calmSince < 0f) _calmSince = now;
                    if (now - _calmSince > 0.6f) wait = 0f;
                }
                else _calmSince = -1f;
            }
            if (now - _finishedAtReal < wait) return;
            ShowSummary();
        }

        void ShowSummary()
        {
            Time.timeScale = 1f;
            var outcome = _session.Outcome;
            float distance = outcome == JumpOutcome.WellDone || outcome == JumpOutcome.MissedPlatform ? _session.LandingDistance : float.NaN;
            var score = Scoring.Evaluate(outcome, distance, _session.Elapsed, _layout.TargetRect.width * 0.5f, _level, _scoreConfig);
            LastScore = score;

            bool newBest = _save.Record(_level.Id, score.Stars, score.Points, outcome == JumpOutcome.WellDone ? distance : float.NaN);
            _save.Persist();
            float best = _save.Get(_level.Id)?.BestDistance ?? -1f;
            if (_mapPlay != null)
            {
                if (outcome == JumpOutcome.WellDone) _builder.MarkPassed(_mapPlay); // landed on the flag: the map can be shared
                _ui.ShowSummary(outcome, UIText.MapLine(_mapPlay.Name), distance, best < 0f ? float.NaN : best, newBest, score.Points, score.Stars, false);
                return;
            }
            var ids = _catalog.LevelIds(_stage);
            bool canGoNext = _levelIndex + 1 < ids.Count && _save.IsUnlocked(ids, _levelIndex + 1);
            _ui.ShowSummary(outcome, UIText.LevelLine(_catalog.Stages[_stage].Number, _levelIndex + 1), distance, best < 0f ? float.NaN : best, newBest, score.Points, score.Stars, canGoNext);
        }
    }
}
