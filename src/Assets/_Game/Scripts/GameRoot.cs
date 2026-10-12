using System.Collections.Generic;
using StickmanJumper.Audio;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using StickmanJumper.Presentation;
using StickmanJumper.Save;
using StickmanJumper.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace StickmanJumper
{
    /// <summary>
    /// Точка сборки сцены и переходы между экранами, как в оригинале: главное меню → выбор этапа → выбор уровня →
    /// прыжок (пауза) → итог → ещё раз / дальше / в меню. Правила прыжка живут в JumpSession, здесь только склейка.
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
        CameraRig2D _cameraRig;
        AudioService _audio;
        GameUI _ui;

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
        public int LevelIndex => _levelIndex;
        public bool Paused { get; private set; }

        /// <summary>Если задан — играет сам (автозаезд). null — играет человек.</summary>
        public Autopilot Autopilot { get; set; }

        /// <summary>Последний итог (для автотеста).</summary>
        public JumpScore LastScore { get; private set; }

        void Awake()
        {
            Application.targetFrameRate = 60; // Android defaults to 30
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Time.timeScale = 1f; // Play mode starts without a domain reload: never inherit a slow-mo
            AudioListener.pause = false;

            _save = new SaveService(new PlayerPrefsSaveStore());
            _save.Load();
            _cameraRig = new CameraRig2D(_camera, _cameraConfig);
            _audio = new AudioService(_camera.gameObject, _sounds);
            _audio.SetSound(_save.Data.Sound);
            _audio.SetMusic(_save.Data.Music);

            _ui = new GameUI(GetComponent<UIDocument>().rootVisualElement, _art);
            _ui.Tapped += () => _tapQueued = true;
            _ui.PlayClicked += () => { _audio.Click(); ShowStageSelect(); };
            _ui.SoundClicked += () => { _save.Data.Sound = !_save.Data.Sound; _audio.SetSound(_save.Data.Sound); _save.Persist(); _audio.Click(); RefreshSoundButtons(); };
            _ui.MusicClicked += () => { _save.Data.Music = !_save.Data.Music; _audio.SetMusic(_save.Data.Music); _save.Persist(); _audio.Click(); RefreshSoundButtons(); };
            _ui.StageClicked += i => { _audio.Click(); ShowLevelSelect(i); };
            _ui.LevelClicked += i => { _audio.Click(); StartLevel(_stage, i); };
            _ui.BackClicked += () => { _audio.Click(); Back(); };
            _ui.MenuClicked += () => { _audio.Click(); SetPaused(false); ShowLevelSelect(_stage); };
            _ui.RetryClicked += () => { _audio.Click(); Retry(); };
            _ui.NextClicked += () => { _audio.Click(); StartLevel(_stage, _levelIndex + 1); };
            _ui.ContinueClicked += () => { _audio.Click(); SetPaused(false); };
            _ui.PauseClicked += () => { _audio.Click(); SetPaused(true); };
            _ui.ZoomClicked += () => { _audio.Click(); _cameraRig.Overview = !_cameraRig.Overview; };
            RefreshSoundButtons();

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
            EnterMenu();
            _ui.Show(UIScreen.MainMenu);
        }

        public void ShowStageSelect()
        {
            EnterMenu();
            var cards = new List<StageCardView>();
            for (int s = 0; s < _catalog.Stages.Length; s++) cards.Add(StageCard(s));
            _ui.SetStages(cards);
            _ui.Show(UIScreen.StageSelect);
        }

        public void ShowLevelSelect(int stage)
        {
            stage = Mathf.Clamp(stage, 0, _catalog.Stages.Length - 1);
            EnterMenu();
            if (stage != _stage) LoadLevel(stage, 0);
            var ids = _catalog.LevelIds(stage);
            var nodes = new List<LevelNodeView>();
            for (int i = 0; i < ids.Count; i++)
            {
                var rec = _save.Get(ids[i]);
                nodes.Add(new LevelNodeView { Number = i + 1, Stars = rec?.Stars ?? 0, Points = rec?.BestPoints ?? 0, Locked = !_save.IsUnlocked(ids, i) });
            }
            _ui.SetLevels(StageCard(stage), nodes);
            _ui.Show(UIScreen.LevelSelect);
        }

        /// <summary>Начать уровень (экран выбора, «дальше», автотест).</summary>
        public void StartLevel(int stage, int level)
        {
            if (_catalog.Level(stage, level) == null) return;
            if (stage != _stage || level != _levelIndex) LoadLevel(stage, level);
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
                Title = stage.Title,
                Subtitle = stage.Subtitle,
                Stars = _save.TotalStars(ids),
                MaxStars = ids.Count * 3,
                NeedStars = Mathf.Max(0, stage.StarsToUnlock - before),
                Locked = before < stage.StarsToUnlock,
                Empty = ids.Count == 0,
            };
        }

        void Back()
        {
            if (_ui.Screen == UIScreen.LevelSelect) ShowStageSelect();
            else ShowMainMenu();
        }

        void EnterMenu()
        {
            _playing = false;
            SetPaused(false);
            Autopilot = null;
            if (_session != null && _session.Phase != JumpPhase.Ready) StartRun(); // stickman back on the roof
        }

        void RefreshSoundButtons() => _ui.SetSound(_save.Data.Sound, _save.Data.Music);

        // ------------------------------------------------------------------ level & run

        void LoadLevel(int stage, int level)
        {
            if (_world != null) Destroy(_world.gameObject);
            _stickman = null;
            _stage = stage;
            _levelIndex = level;
            _level = _catalog.Level(stage, level);
            _world = new GameObject("World").transform;
            _layout = LevelLayout.Build(_level, _jumpConfig);
            if (_session != null) _session.Happened -= OnJumpEvent;
            _session = new JumpSession(_layout, _jumpConfig, _flightConfig);
            _session.Happened += OnJumpEvent;
            _levelView = new LevelView(_world, _level, _layout, _art);
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
            _stickman = new StickmanView(_world, _stickmanConfig, _art);
            _stickman.RagdollImpact += OnRagdollImpact;
            _audio.ResetRun();
            _cameraRig.Overview = false;
            if (_playing) _cameraRig.Snap(CameraRig2D.OverviewCenter(_layout.Overview), _cameraRig.FitSize(_layout.Overview));
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
            if (Paused)
            {
                _tapQueued = false;
                return;
            }

            if (_playing)
            {
                if (Autopilot != null) Autopilot.Update(_session);
                if (_tapQueued && !_ui.SummaryVisible) _session.Tap();
            }
            _tapQueued = false;
            _session.Advance(dt);

            if (_stickman.IsRagdoll) _stickman.UpdateDetached(dt);
            else _stickman.UpdateAlive(_session, dt);

            Vector2 focus = _stickman.IsRagdoll ? _stickman.Center : _session.Feet;
            if (_playing) _cameraRig.Update(focus, _session.Velocity, _session.Phase, _stickman.IsRagdoll, _layout.Overview, realDt);
            else _cameraRig.UpdateFixed(MenuCenter, _cameraConfig.SizeOnRoof, realDt);
            _levelView.SetGuideVisible(_playing && _session.Phase == JumpPhase.Airborne);
            _levelView.Update(_camera, Time.time, dt);
            _audio.Update(_session, _stickman.IsRagdoll, realDt);

            if (_playing && !_ui.SummaryVisible)
            {
                _ui.SetHud(_session.TimeLeft, _layout.TimeLimit, _layout.Wind, _level.WindMax, _cameraRig.Overview);
                _ui.SetMinimap(_layout, focus, _session.LastRunStartX);
                _ui.SetHint(UIText.Hint(_session.Phase, _session.CanopyDeployed));
            }

            UpdateSlowMo();
            if (_playing) UpdateFinish();
        }

        void ReadKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            bool esc = kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame;
            if (!_playing)
            {
                if (kb.escapeKey.wasPressedThisFrame && _ui.Screen != UIScreen.MainMenu) Back();
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

            float wait = _session.Phase == JumpPhase.Landed ? 1.3f : _session.Phase == JumpPhase.TimeUp ? 0.6f : 4f;
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
            var ids = _catalog.LevelIds(_stage);
            bool canGoNext = _levelIndex + 1 < ids.Count && _save.IsUnlocked(ids, _levelIndex + 1);
            _ui.ShowSummary(outcome, _catalog.Stages[_stage].Number, _levelIndex + 1, distance, best < 0f ? float.NaN : best, newBest, score.Points, score.Stars, canGoNext);
        }
    }
}
