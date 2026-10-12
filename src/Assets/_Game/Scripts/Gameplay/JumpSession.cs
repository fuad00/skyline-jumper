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
            _params = new FlightParams(flight, level.Wind);
            _step = 1f / Mathf.Max(30, flight.StepsPerSecond);
            Reset();
        }

        public LevelLayout Level => _level;
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
        /// <summary>Раскрытие купола 0..1.</summary>
        public float Canopy { get; private set; }
        public bool CanopyDeployed { get; private set; }
        /// <summary>Длина разбега, м (насколько отошёл от края).</summary>
        public float RunUpLength { get; private set; }
        /// <summary>Расстояние от точки посадки до стрелки, м (NaN, пока не сел).</summary>
        public float LandingDistance { get; private set; }
        public bool LandedOnTarget { get; private set; }
        public float TimeLeft => Mathf.Max(0f, _level.TimeLimit - Elapsed);

        /// <summary>Коробка тела для столкновений.</summary>
        public Rect BodyBox => new Rect(Feet.x - _flight.BodyWidth * 0.5f, Feet.y, _flight.BodyWidth, _flight.BodyHeight);

        public event Action<JumpEvent> Happened;

        public void Reset()
        {
            Phase = JumpPhase.Ready;
            Outcome = JumpOutcome.None;
            Feet = new Vector2(_level.StartFeetX, _level.RoofY);
            Velocity = Vector2.zero;
            ImpactVelocity = Vector2.zero;
            ContactNormal = Vector2.up;
            Elapsed = 0f;
            Canopy = 0f;
            CanopyDeployed = false;
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
                    Velocity = Vector2.zero;
                    Phase = JumpPhase.Running;
                    Raise(JumpEvent.StartedRunning);
                    break;
                case JumpPhase.Running:
                    Velocity = new Vector2(Velocity.x, _jump.JumpUpSpeed);
                    Phase = JumpPhase.Airborne;
                    Raise(JumpEvent.Jumped);
                    break;
                case JumpPhase.Airborne:
                    if (!CanopyDeployed)
                    {
                        CanopyDeployed = true;
                        Raise(JumpEvent.CanopyDeployed);
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
            if (CanopyDeployed) Canopy = Mathf.Min(1f, Canopy + h / Mathf.Max(0.05f, _flight.CanopyOpenTime));
            Velocity = FlightModel.Step(Velocity, Canopy, _params, h);
            Vector2 delta = Velocity * h;

            if (!Sweep.Cast(BodyBox, delta, _level.Solids, out var hit))
            {
                Feet += delta;
                return;
            }

            Feet += delta * hit.T;
            ContactNormal = hit.Normal;
            switch (LandingJudge.Judge(hit.Normal, Velocity, _flight))
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
                    break;
            }
        }

        void Raise(JumpEvent e) => Happened?.Invoke(e);
    }
}
