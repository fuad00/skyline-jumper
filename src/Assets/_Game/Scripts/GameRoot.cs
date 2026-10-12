using StickmanJumper.Audio;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using StickmanJumper.Presentation;
using StickmanJumper.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace StickmanJumper
{
    /// <summary>
    /// Точка сборки сцены: создаёт уровень, стикмена, камеру, звук и интерфейс и гоняет один прыжок за другим.
    /// Правила живут в JumpSession, здесь только склейка и «показать итог». Синглтонов нет.
    /// Клавиатура в редакторе: Пробел — тап, Z — зум, R — ещё раз.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameRoot : MonoBehaviour
    {
        const string BestKeyPrefix = "SJ.Best.";

        [Header("Настройки (крути их в Inspector)")]
        [SerializeField] LevelConfig _level;
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

        LevelLayout _layout;
        JumpSession _session;
        LevelView _levelView;
        StickmanView _stickman;
        CameraRig2D _cameraRig;
        AudioService _audio;
        GameUI _ui;
        Transform _world;

        float _finishedAtReal = -1f, _calmSince = -1f, _slowMoUntilReal;
        int _runIndex;
        bool _tapQueued, _slowMoUsed;

        public bool Ready => _ui != null;
        public JumpSession Session => _session;
        public StickmanView Stickman => _stickman;
        public GameUI UI => _ui;
        public CameraRig2D CameraRig => _cameraRig;
        public LevelConfig Level => _level;

        /// <summary>Если задан — играет сам (автозаезд). null — играет человек.</summary>
        public Autopilot Autopilot { get; set; }

        /// <summary>Последний итог (для автотеста).</summary>
        public JumpScore LastScore { get; private set; }

        void Awake()
        {
            Application.targetFrameRate = 60; // Android defaults to 30
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Time.timeScale = 1f; // Play mode starts without a domain reload: never inherit a slow-mo

            _world = new GameObject("World").transform;
            _layout = LevelLayout.Build(_level, _jumpConfig);
            _session = new JumpSession(_layout, _jumpConfig, _flightConfig);
            _session.Happened += OnJumpEvent;
            _levelView = new LevelView(_world, _level, _layout, _art);
            _cameraRig = new CameraRig2D(_camera, _cameraConfig);
            _audio = new AudioService(_camera.gameObject, _sounds);

            _ui = new GameUI(GetComponent<UIDocument>().rootVisualElement, _art);
            _ui.Tapped += () => _tapQueued = true;
            _ui.RetryClicked += () =>
            {
                _audio.Click();
                Retry();
            };
            _ui.ZoomClicked += () =>
            {
                _audio.Click();
                _cameraRig.Overview = !_cameraRig.Overview;
            };

            StartRun();
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            if (_session != null) _session.Happened -= OnJumpEvent;
        }

        // ------------------------------------------------------------------ run flow

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
            _stickman = new StickmanView(_world, _stickmanConfig, _art);
            _stickman.RagdollImpact += OnRagdollImpact;
            _audio.ResetRun();
            _ui.HideSummary();
            _cameraRig.Overview = false;
            _cameraRig.Snap(_layout.Overview.center, _cameraRig.FitSize(_layout.Overview));
            LastScore = default;
        }

        /// <summary>Начать уровень заново (кнопка «Ещё раз», клавиша R, автотест).</summary>
        public void Retry() => StartRun();

        /// <summary>Тап игрока (экран, пробел или автотест).</summary>
        public void Tap() => _tapQueued = true;

        void Update()
        {
            float dt = Time.deltaTime;
            float realDt = Time.unscaledDeltaTime;
            ReadKeyboard();

            if (Autopilot != null) Autopilot.Update(_session);
            if (_tapQueued)
            {
                _tapQueued = false;
                if (!_ui.SummaryVisible) _session.Tap();
            }
            _session.Advance(dt);

            if (_stickman.IsRagdoll) _stickman.UpdateDetached(dt);
            else _stickman.UpdateAlive(_session, dt);

            Vector2 focus = _stickman.IsRagdoll ? _stickman.Center : _session.Feet;
            _cameraRig.Update(focus, _session.Velocity, _session.Phase, _stickman.IsRagdoll, _layout.Overview, realDt);
            _levelView.SetArrowVisible(_session.Phase != JumpPhase.Landed);
            _levelView.Update(_camera, Time.time, dt);
            _audio.Update(_session, _stickman.IsRagdoll, realDt);

            _ui.SetHud(_session.TimeLeft, _layout.Wind, !_ui.SummaryVisible, _cameraRig.Overview);
            if (!_ui.SummaryVisible) _ui.SetHint(UIText.Hint(_session.Phase, _session.CanopyDeployed));

            UpdateSlowMo();
            UpdateFinish();
        }

        void ReadKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
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
                    _stickman.BecomeRagdoll(_session.ImpactVelocity, _runIndex);
                    break;
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

            string key = BestKeyPrefix + _level.Id;
            float best = PlayerPrefs.GetFloat(key, -1f);
            bool newBest = outcome == JumpOutcome.WellDone && Scoring.IsNewBest(distance, best < 0f ? float.NaN : best);
            if (newBest)
            {
                best = distance;
                PlayerPrefs.SetFloat(key, best);
                PlayerPrefs.Save();
            }
            _ui.ShowSummary(outcome, distance, best < 0f ? float.NaN : best, newBest, score.Points, score.Stars);
        }
    }
}
