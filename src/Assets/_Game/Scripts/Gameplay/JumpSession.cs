using System;
using StickmanJumper.Config;
using StickmanJumper.Core;
using UnityEngine;

namespace StickmanJumper.Gameplay
{
    /// <summary>
    /// Один прыжок от начала до итога. Чистая логика без сцены: игра вызывает Tap() и Advance(dt), а картинка, звук
    /// и камера только читают состояние и слушают события. Физика идёт фиксированными шагами — одинаковые тапы дают
    /// одинаковый прыжок (это пригодится для видеоповтора).
    /// Числа и порядок расчёта — как в коде оригинала (reference/disasm/FINDINGS.md).
    /// На уровне бывают промежуточные платформы: сел на такую — купол сброшен, и стикмен сразу бежит к её краю
    /// (как в оригинале: без отхода и без тапа), тап — прыжок; бежать там может быть и влево. Время — общее на весь уровень.
    /// </summary>
    public sealed class JumpSession
    {
        const int MaxStepsPerAdvance = 30;
        const float MinGrazeSpeed = 0.5f;

        readonly LevelLayout _level;
        readonly JumpConfig _jump;
        readonly FlightConfig _flight;
        readonly FlightParams _params;
        readonly float _step;
        // Walk-back, run and jump numbers of the original (px in the config) in metres.
        readonly float _walkStart, _walkGain, _walkMax, _runStart, _runRef, _jumpUpRef;
        readonly Vector2 _runGain, _jumpUp;
        float _accumulator;
        bool _clockStarted, _runStarted;
        float _walkSpeed;
        CanopyState _canopyState;

        public JumpSession(LevelLayout level, JumpConfig jump, FlightConfig flight)
        {
            _level = level;
            _jump = jump;
            _flight = flight;
            _params = new FlightParams(flight, level.Wind, level.MaxFallSpeed);
            _step = 1f / Mathf.Max(30, flight.StepsPerSecond);
            _walkStart = flight.Meters(jump.WalkBackStartSpeedPx);
            _walkGain = flight.Meters(jump.WalkBackGainPerTick);
            _walkMax = flight.Meters(jump.WalkBackMaxSpeedPx);
            _runStart = flight.Meters(jump.RunStartSpeedPx);
            _runRef = flight.Meters(jump.RunGainRefSpeedPx);
            _runGain = new Vector2(flight.Meters(jump.RunGainPerTick.x), flight.Meters(jump.RunGainPerTick.y));
            _jumpUp = new Vector2(flight.Meters(jump.JumpUpSpeedPx.x), flight.Meters(jump.JumpUpSpeedPx.y));
            _jumpUpRef = Mathf.Max(0.001f, flight.Meters(jump.JumpUpRefSpeedPx));
            Reset();
        }

        public LevelLayout Level => _level;
        public JumpConfig Jump => _jump;
        public FlightConfig Flight => _flight;
        public FlightParams Params => _params;
        public float StepSize => _step;
        /// <summary>Купол: тики с раскрытия и множитель сопротивления (для прогноза автопилота).</summary>
        public CanopyState CanopyMemory => _canopyState;

        /// <summary>
        /// Скорость бега через шаг h, если сейчас v (оригинал: прибавка за тик падает с ростом скорости), м/с.
        /// На промежуточной платформе (intermediate) оригинал сперва обрезает v до RunGainRefSpeedPx — прибавка не меньше y.
        /// </summary>
        public float RunSpeedAfter(float v, float h, bool intermediate = false)
        {
            float k = intermediate ? Mathf.Clamp(v, 0f, _runRef) : v;
            return v + Mathf.LerpUnclamped(_runGain.x, _runGain.y, (k - _runStart) / Mathf.Max(0.001f, _runRef - _runStart)) * FlightModel.Ticks(_params, h);
        }

        /// <summary>Скорость толчка с разбега v: X — вперёд (без направления), Y — вверх, м/с.</summary>
        public Vector2 TakeoffVelocity(float v) =>
            new Vector2(v * _jump.JumpForwardFactor, Mathf.LerpUnclamped(_jumpUp.x, _jumpUp.y, v / _jumpUpRef));

        public JumpPhase Phase { get; private set; }
        public JumpOutcome Outcome { get; private set; }
        /// <summary>Точка между ступнями.</summary>
        public Vector2 Feet { get; private set; }
        public Vector2 Velocity { get; private set; }
        /// <summary>Скорость в момент удара/посадки (для ragdoll и звуков).</summary>
        public Vector2 ImpactVelocity { get; private set; }
        public Vector2 ContactNormal { get; private set; }
        /// <summary>Секунды на таймере уровня (с начала разбега, как в оригинале, или с первого тапа — JumpConfig.TimerFromFirstTap).</summary>
        public float Elapsed { get; private set; }
        /// <summary>Время вышло. Как в оригинале, полёт идёт дальше, а посадка на цель засчитывается как «время вышло».</summary>
        public bool TimeOver { get; private set; }
        /// <summary>Раскрытие купола 0..1 для картинки (физика тормозит сразу в момент тапа).</summary>
        public float Canopy => FlightModel.OpenCurve(CanopyProgress);
        /// <summary>Сырой прогресс раскрытия 0..1 (линейно по времени).</summary>
        public float CanopyProgress { get; private set; }
        public bool CanopyDeployed { get; private set; }
        /// <summary>Купол оторван тапом: второго нет, падает до конца.</summary>
        public bool CanopyCut { get; private set; }
        float _canopyTime;
        /// <summary>Длина разбега, м (насколько отошёл от края).</summary>
        public float RunUpLength { get; private set; }
        /// <summary>Откуда стартовал разбег в этой попытке (NaN — ещё не бежал).</summary>
        public float RunStartX { get; private set; } = float.NaN;
        /// <summary>Откуда стартовал разбег в прошлой попытке на этом уровне (зелёный флажок). NaN — попыток не было.</summary>
        public float LastRunStartX { get; private set; } = float.NaN;
        /// <summary>Где был толчок (NaN — ещё не прыгал).</summary>
        public float TakeoffX { get; private set; } = float.NaN;
        /// <summary>Расстояние от точки посадки до флажка, м (NaN, пока не сел).</summary>
        public float LandingDistance { get; private set; }
        public bool LandedOnTarget { get; private set; }

        /// <summary>Полоса, с которой сейчас разбег (0 — старт, дальше — промежуточные платформы).</summary>
        public int RunwayIndex { get; private set; }
        public Runway Runway => _level.Runways[RunwayIndex];
        /// <summary>Куда бежим и прыгаем с этой полосы: +1 вправо, −1 влево.</summary>
        public int Dir => Runway.Dir;
        /// <summary>Сколько осталось до края полосы (по направлению бега), м.</summary>
        public float DistanceToEdge => Dir * (Runway.EdgeX - Feet.x);
        /// <summary>На какие промежуточные платформы уже садился в этой попытке.</summary>
        public bool Reached(int runway) => _reached != null && runway >= 0 && runway < _reached.Length && _reached[runway];
        bool[] _reached;
        public float TimeLeft => Mathf.Max(0f, _level.TimeLimit - Elapsed);

        /// <summary>Коробка тела для столкновений.</summary>
        public Rect BodyBox => new Rect(Feet.x - _flight.BodyWidth * 0.5f, Feet.y, _flight.BodyWidth, _flight.BodyHeight);

        public event Action<JumpEvent> Happened;

        /// <summary>Забыть прошлые попытки (смена уровня).</summary>
        public void ForgetHistory()
        {
            RunStartX = float.NaN;
            LastRunStartX = float.NaN;
        }

        /// <summary>Новая попытка на том же уровне: запоминает, откуда бежали в этот раз.</summary>
        public void Reset()
        {
            if (!float.IsNaN(RunStartX)) LastRunStartX = RunStartX;
            RunStartX = float.NaN;
            TakeoffX = float.NaN;
            Phase = JumpPhase.Ready;
            Outcome = JumpOutcome.None;
            RunwayIndex = 0;
            _reached = new bool[_level.Runways.Count];
            Feet = new Vector2(_level.StartFeetX, _level.RoofY);
            Velocity = Vector2.zero;
            ImpactVelocity = Vector2.zero;
            ContactNormal = Vector2.up;
            Elapsed = 0f;
            TimeOver = false;
            _clockStarted = false;
            _runStarted = false;
            _walkSpeed = 0f;
            _canopyState = CanopyState.Start(_params);
            CanopyProgress = 0f;
            CanopyDeployed = false;
            CanopyCut = false;
            _canopyTime = 0f;
            RunUpLength = 0f;
            LandingDistance = float.NaN;
            LandedOnTarget = false;
            _accumulator = 0f;
        }

        /// <summary>Единственное управление в игре — тап. Что он делает, зависит от фазы.</summary>
        public void Tap()
        {
            switch (Phase)
            {
                case JumpPhase.Ready:
                    _clockStarted = true;
                    _walkSpeed = _walkStart;
                    Phase = JumpPhase.BackingUp;
                    Raise(JumpEvent.StartedBackingUp);
                    break;
                case JumpPhase.BackingUp:
                case JumpPhase.AtBackWall:
                    RunUpLength = DistanceToEdge;
                    if (RunwayIndex == 0) RunStartX = Feet.x; // the «Last» flag marks the start roof only
                    Velocity = new Vector2(Dir * _runStart, 0f);
                    _runStarted = true;
                    Phase = JumpPhase.Running;
                    Raise(JumpEvent.StartedRunning);
                    break;
                case JumpPhase.Running:
                {
                    // Like the original: the tap pushes off at once, however slow the run (a quick tap is a weak hop).
                    var push = TakeoffVelocity(Mathf.Abs(Velocity.x));
                    Velocity = new Vector2(Dir * push.x, push.y);
                    TakeoffX = Feet.x;
                    Phase = JumpPhase.Airborne;
                    Raise(JumpEvent.Jumped);
                    break;
                }
                case JumpPhase.Airborne:
                    if (!CanopyDeployed && !CanopyCut)
                    {
                        // Like the original: the canopy acts right at the tap (the opening animation runs alongside).
                        CanopyDeployed = true;
                        _canopyTime = 0f;
                        _canopyState.Ticks = 0f;
                        Velocity = FlightModel.Deploy(Velocity, _params);
                        Raise(JumpEvent.CanopyDeployed);
                    }
                    else if (CanopyDeployed && _canopyTime >= _flight.CanopyCutDelay)
                    {
                        // Like the original: a tap under the canopy cuts it away — a quick way to crash and retry.
                        CanopyDeployed = false;
                        CanopyCut = true;
                        CanopyProgress = 0f;
                        Raise(JumpEvent.CanopyCut);
                    }
                    break;
            }
        }

        /// <summary>Прогнать время вперёд (обычно Time.deltaTime). Внутри — фиксированные шаги.</summary>
        /// <param name="beforeStep">Вызывается перед каждым шагом (автопилот тапает ровно так же, как при расчёте плана).</param>
        public void Advance(float dt, Action<JumpSession> beforeStep = null)
        {
            if (Phase.IsFinished()) return;
            _accumulator += Mathf.Max(0f, dt);
            int steps = 0;
            while (_accumulator >= _step && steps++ < MaxStepsPerAdvance)
            {
                _accumulator -= _step;
                beforeStep?.Invoke(this);
                Step(_step);
                if (Phase.IsFinished())
                {
                    _accumulator = 0f;
                    return;
                }
            }
            if (steps > MaxStepsPerAdvance) _accumulator = 0f; // a long hitch: drop the backlog instead of spiralling
        }

        /// <summary>Один фиксированный шаг (тесты зовут напрямую).</summary>
        public void Step(float h)
        {
            switch (Phase)
            {
                case JumpPhase.Ready: // waiting for a tap (on an intermediate platform the level clock may already run)
                    break;
                case JumpPhase.BackingUp:
                    StepBackingUp(h);
                    break;
                case JumpPhase.AtBackWall:
                    break;
                case JumpPhase.Running:
                    StepRunning(h);
                    break;
                case JumpPhase.Airborne:
                    StepAir(h);
                    break;
                default:
                    return;
            }
            if (Phase.IsFinished() || !ClockRunning) return;
            Elapsed += h;
            if (!TimeOver && Elapsed >= _level.TimeLimit)
            {
                TimeOver = true;
                Raise(JumpEvent.TimeUp);
            }
            // In the air (or on the run) he goes on — the landing decides. Standing on a roof (intermediate platform) — it is over.
            if (TimeOver && Phase != JumpPhase.Running && Phase != JumpPhase.Airborne)
            {
                Phase = JumpPhase.TimeUp;
                Outcome = JumpOutcome.TimesUp;
                Velocity = Vector2.zero;
            }
        }

        /// <summary>
        /// Идёт ли таймер: с первого тапа разбега и до конца (оригинал) — или с первого тапа вообще (JumpConfig.TimerFromFirstTap).
        /// </summary>
        bool ClockRunning => _jump.TimerFromFirstTap ? _clockStarted : _runStarted;

        void StepBackingUp(float h)
        {
            // Like the original: he starts slowly and speeds up a little (63 → 120 px/s).
            if (_walkSpeed < _walkMax) _walkSpeed += _walkGain * FlightModel.Ticks(_params, h);
            float x = Feet.x - Dir * _walkSpeed * h;
            Velocity = new Vector2(-Dir * _walkSpeed, 0f);
            if (Dir * (x - Runway.BackLimitX) <= 0f)
            {
                x = Runway.BackLimitX;
                Velocity = Vector2.zero;
                Phase = JumpPhase.AtBackWall;
                Raise(JumpEvent.ReachedBackWall);
            }
            Feet = new Vector2(x, Feet.y);
        }

        void StepRunning(float h)
        {
            float v = RunSpeedAfter(Mathf.Abs(Velocity.x), h, RunwayIndex > 0);
            Velocity = new Vector2(Dir * v, 0f);
            Feet = new Vector2(Feet.x + Dir * v * h, Feet.y);
            if (-DistanceToEdge > _jump.EdgeOverrun)
            {
                // Missed the edge: loses footing and plummets (ragdoll from here on).
                ImpactVelocity = Velocity;
                Phase = JumpPhase.Crashed;
                Outcome = JumpOutcome.Died;
                Raise(JumpEvent.Stumbled);
            }
        }

        void StepAir(float h)
        {
            if (CanopyDeployed)
            {
                _canopyTime += h;
                CanopyProgress = Mathf.Min(1f, CanopyProgress + h / Mathf.Max(0.05f, _flight.CanopyOpenTime));
            }
            var velocity = Velocity;
            Vector2 delta = FlightModel.Advance(ref velocity, ref _canopyState, CanopyDeployed, _params, h);
            Velocity = velocity;

            if (!Sweep.Cast(BodyBox, delta, _level.Solids, out var hit))
            {
                Feet += delta;
                return;
            }

            Feet += delta * hit.T;
            ContactNormal = hit.Normal;
            // With the canopy cut away he tumbles (ragdoll already): whatever he hits now, it is a crash.
            var contact = CanopyCut ? ContactResult.Crash : LandingJudge.Judge(hit.Normal, Velocity, _params, CanopyDeployed);
            // Which platform he is on is decided by where his feet are (the roof under a platform may be a different solid).
            int platform = contact == ContactResult.Landed ? _level.PlatformAt(Feet) : -1;
            switch (contact)
            {
                case ContactResult.Landed when platform > 0:
                    ReachPlatform(platform);
                    break;
                case ContactResult.Landed:
                    ImpactVelocity = Velocity;
                    Velocity = Vector2.zero;
                    LandedOnTarget = _level.OnTarget(Feet);
                    LandingDistance = _level.DistanceToArrow(Feet.x);
                    Phase = JumpPhase.Landed;
                    // Like the original: on the target after the time ran out — «time's up», elsewhere — a miss.
                    Outcome = !LandedOnTarget ? JumpOutcome.MissedPlatform : TimeOver ? JumpOutcome.TimesUp : JumpOutcome.WellDone;
                    Raise(JumpEvent.Landed);
                    break;
                case ContactResult.Crash:
                    ImpactVelocity = Velocity;
                    Phase = JumpPhase.Crashed;
                    Outcome = JumpOutcome.Died;
                    Raise(JumpEvent.Crashed);
                    break;
                default:
                    float into = -Vector2.Dot(Velocity, hit.Normal);
                    Velocity += hit.Normal * into;
                    if (into > MinGrazeSpeed) Raise(JumpEvent.Grazed);
                    // Keep the rest of the step along the wall (otherwise wind pinning him to a wall freezes him in mid-air).
                    Vector2 rest = delta * (1f - hit.T);
                    rest -= hit.Normal * Vector2.Dot(rest, hit.Normal);
                    if (rest.sqrMagnitude > 1e-10f)
                        Feet += Sweep.Cast(BodyBox, rest, _level.Solids, out var next) ? rest * next.T : rest;
                    break;
            }
        }

        /// <summary>Сел на промежуточную платформу: купол сброшен, и он сразу бежит к её краю (оригинал: intermediateRun).</summary>
        void ReachPlatform(int runway)
        {
            ImpactVelocity = Velocity;
            RunwayIndex = runway;
            _reached[runway] = true;
            var r = Runway;
            Feet = new Vector2(Mathf.Clamp(Feet.x, r.Strip.xMin + 0.3f, r.Strip.xMax - 0.3f), r.RoofY);
            CanopyDeployed = false;
            CanopyCut = false;
            CanopyProgress = 0f;
            _canopyTime = 0f;
            TakeoffX = float.NaN;
            Velocity = new Vector2(Dir * _runStart, 0f);
            RunUpLength = DistanceToEdge;
            Phase = JumpPhase.Running;
            Raise(JumpEvent.ReachedPlatform);
        }

        void Raise(JumpEvent e) => Happened?.Invoke(e);
    }
}
