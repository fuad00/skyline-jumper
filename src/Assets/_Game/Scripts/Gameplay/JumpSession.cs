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
        float _accumulator;

        public JumpSession(LevelLayout level, JumpConfig jump, FlightConfig flight)
        {
            _level = level;
            _jump = jump;
            _flight = flight;
            _params = new FlightParams(flight, level.Wind, level.MaxFallSpeed);
            _step = 1f / Mathf.Max(30, flight.StepsPerSecond);
            Reset();
        }

        public LevelLayout Level => _level;
        public JumpConfig Jump => _jump;
        public FlightConfig Flight => _flight;
        public FlightParams Params => _params;
        public float StepSize => _step;

        public JumpPhase Phase { get; private set; }
        public JumpOutcome Outcome { get; private set; }
        /// <summary>Точка между ступнями.</summary>
        public Vector2 Feet { get; private set; }
        public Vector2 Velocity { get; private set; }
        /// <summary>Скорость в момент удара/посадки (для ragdoll и звуков).</summary>
        public Vector2 ImpactVelocity { get; private set; }
        public Vector2 ContactNormal { get; private set; }
        /// <summary>Секунды с первого тапа (идут, пока прыжок не закончен).</summary>
        public float Elapsed { get; private set; }
        /// <summary>Раскрытие купола 0..1 (сглаженное — им пользуются физика и картинка).</summary>
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
            Feet = new Vector2(_level.StartFeetX, _level.RoofY);
            Velocity = Vector2.zero;
            ImpactVelocity = Vector2.zero;
            ContactNormal = Vector2.up;
            Elapsed = 0f;
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
                    Phase = JumpPhase.BackingUp;
                    Raise(JumpEvent.StartedBackingUp);
                    break;
                case JumpPhase.BackingUp:
                case JumpPhase.AtBackWall:
                    RunUpLength = _level.EdgeX - Feet.x;
                    RunStartX = Feet.x;
                    Velocity = Vector2.zero;
                    Phase = JumpPhase.Running;
                    Raise(JumpEvent.StartedRunning);
                    break;
                case JumpPhase.Running:
                    // A short run-up still jumps (quick taps must work): below the gate the push-off uses the gate speed.
                    Velocity = new Vector2(Mathf.Max(Velocity.x, _jump.JumpSpeedGate) * _jump.JumpHorizontalMultiplier, _jump.JumpUpSpeed);
                    TakeoffX = Feet.x;
                    Phase = JumpPhase.Airborne;
                    Raise(JumpEvent.Jumped);
                    break;
                case JumpPhase.Airborne:
                    if (!CanopyDeployed && !CanopyCut)
                    {
                        CanopyDeployed = true;
                        _canopyTime = 0f;
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
        public void Advance(float dt)
        {
            if (Phase.IsFinished()) return;
            _accumulator += Mathf.Max(0f, dt);
            int steps = 0;
            while (_accumulator >= _step && steps++ < MaxStepsPerAdvance)
            {
                _accumulator -= _step;
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
                case JumpPhase.Ready:
                    return;
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
            if (Phase.IsFinished()) return;
            // Like the original, the clock starts with the run: stepping back to choose the run-up is free.
            if (Phase == JumpPhase.BackingUp || Phase == JumpPhase.AtBackWall) return;
            Elapsed += h;
            if (Elapsed >= _level.TimeLimit)
            {
                Phase = JumpPhase.TimeUp;
                Outcome = JumpOutcome.TimesUp;
                Velocity = Vector2.zero;
                Raise(JumpEvent.TimeUp);
            }
        }

        void StepBackingUp(float h)
        {
            float x = Feet.x - _jump.WalkBackSpeed * h;
            Velocity = new Vector2(-_jump.WalkBackSpeed, 0f);
            if (x <= _level.BackLimitX)
            {
                x = _level.BackLimitX;
                Velocity = Vector2.zero;
                Phase = JumpPhase.AtBackWall;
                Raise(JumpEvent.ReachedBackWall);
            }
            Feet = new Vector2(x, Feet.y);
        }

        void StepRunning(float h)
        {
            float v = Mathf.Min(Velocity.x + _jump.RunAcceleration * h, _jump.RunMaxSpeed);
            Velocity = new Vector2(v, 0f);
            Feet = new Vector2(Feet.x + v * h, Feet.y);
            if (Feet.x > _level.EdgeX + _jump.EdgeOverrun)
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
            Velocity = FlightModel.Step(Velocity, Canopy, _params, h);
            Vector2 delta = Velocity * h;

            if (!Sweep.Cast(BodyBox, delta, _level.Solids, out var hit))
            {
                Feet += delta;
                return;
            }

            Feet += delta * hit.T;
            ContactNormal = hit.Normal;
            // With the canopy cut away he tumbles (ragdoll already): whatever he hits now, it is a crash.
            var contact = CanopyCut ? ContactResult.Crash : LandingJudge.Judge(hit.Normal, Velocity, _flight, CanopyDeployed);
            switch (contact)
            {
                case ContactResult.Landed:
                    ImpactVelocity = Velocity;
                    Velocity = Vector2.zero;
                    LandedOnTarget = _level.Solids[hit.Index].IsTarget;
                    LandingDistance = _level.DistanceToArrow(Feet.x);
                    Phase = JumpPhase.Landed;
                    Outcome = LandedOnTarget ? JumpOutcome.WellDone : JumpOutcome.MissedPlatform;
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

        void Raise(JumpEvent e) => Happened?.Invoke(e);
    }
}
