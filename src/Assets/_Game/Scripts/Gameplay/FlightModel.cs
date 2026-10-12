using StickmanJumper.Config;
using UnityEngine;

namespace StickmanJumper.Gameplay
{
    /// <summary>Готовые числа полёта для одного уровня, м и м/с (ветер и предел падения у каждого уровня свои). «За тик» — как в FlightConfig.</summary>
    public readonly struct FlightParams
    {
        /// <summary>Тиков оригинала в секунду (числа «за тик» — за 1/этого секунды).</summary>
        public readonly float TickRate;
        public readonly float Gravity;
        /// <summary>Предел скорости свободного падения, м/с (положительный).</summary>
        public readonly float FallSpeedCap;
        /// <summary>Ветер уровня, м/с (плюс — вправо).</summary>
        public readonly float Wind;
        /// <summary>Ветер в игре (ветер уровня × WindScale), м/с.</summary>
        public readonly float GameWind;
        public readonly float FlightWindPerTick, CanopyWindPerTick;
        public readonly float CanopyBrakeRising;
        public readonly Vector2 CanopyBrakeFalling;
        public readonly float CanopyBrakeRef, CanopyBrakeMaxSpeed;
        public readonly Vector2 CanopyGravity;
        public readonly float CanopyLiftFrom, CanopyLiftTo;
        public readonly float CanopyDragFrom;
        public readonly Vector2 CanopyDragWind;
        public readonly float CanopyDragWindTicks;
        public readonly Vector2 CanopyDrag;
        public readonly float CanopyDragTicks, CanopySlowSide, CanopyDragStep, CanopyDragStart, CanopyDragSideBonus;
        /// <summary>Самая большая скорость, на которой встаёт на ноги (только под куполом), м/с.</summary>
        public readonly float SoftLandingSpeed;
        public readonly bool WindyLandingByVertical;
        /// <summary>Удар о стену сильнее этого — разбился, м/с.</summary>
        public readonly float WallCrashSpeed;

        /// <param name="wind">Ветер уровня, м/с.</param>
        /// <param name="fallSpeedCap">Свой предел скорости падения уровня (maxFlySpeedY), м/с (0 — из настроек).</param>
        public FlightParams(FlightConfig cfg, float wind, float fallSpeedCap = 0f)
        {
            TickRate = Mathf.Max(1, cfg.TicksPerSecond);
            Gravity = cfg.Gravity;
            FallSpeedCap = fallSpeedCap > 0f ? fallSpeedCap : cfg.FallSpeedCap;
            Wind = wind;
            GameWind = wind * cfg.WindScale;
            FlightWindPerTick = cfg.FlightWindPerTick;
            CanopyWindPerTick = cfg.CanopyWindPerTick;
            CanopyBrakeRising = cfg.CanopyBrakeRising;
            CanopyBrakeFalling = cfg.CanopyBrakeFalling;
            CanopyBrakeRef = Mathf.Max(0.001f, cfg.Meters(cfg.CanopyBrakeRefPx));
            CanopyBrakeMaxSpeed = cfg.Meters(cfg.CanopyBrakeMaxSpeedPx);
            CanopyGravity = cfg.CanopyGravity;
            CanopyLiftFrom = cfg.Meters(cfg.CanopyLiftFromPx);
            CanopyLiftTo = cfg.Meters(cfg.CanopyLiftToPx);
            CanopyDragFrom = cfg.Meters(cfg.CanopyDragFromPx);
            CanopyDragWind = cfg.CanopyDragWind;
            CanopyDragWindTicks = Mathf.Max(1f, cfg.CanopyDragWindTicks);
            CanopyDrag = cfg.CanopyDrag;
            CanopyDragTicks = Mathf.Max(1f, cfg.CanopyDragTicks);
            CanopySlowSide = cfg.Meters(cfg.CanopySlowSidePx);
            CanopyDragStep = cfg.CanopyDragStep;
            CanopyDragStart = cfg.CanopyDragStart;
            CanopyDragSideBonus = cfg.CanopyDragSideBonus;
            SoftLandingSpeed = cfg.SoftLandingSpeed;
            WindyLandingByVertical = cfg.WindyLandingByVertical;
            WallCrashSpeed = cfg.WallCrashSpeed;
        }
    }

    /// <summary>Что купол помнит между шагами (как в оригинале).</summary>
    public struct CanopyState
    {
        /// <summary>Тиков с раскрытия.</summary>
        public float Ticks;
        /// <summary>Множитель сопротивления за тик (оригинал: lastParachuteOpenFactor; живёт всю попытку).</summary>
        public float Drag;

        public static CanopyState Start(in FlightParams p) => new CanopyState { Ticks = 0f, Drag = p.CanopyDragStart };
    }

    /// <summary>
    /// Полёт как в оригинале (его код, reference/disasm/FINDINGS.md). Свободный полёт: гравитация до предела скорости падения,
    /// слабый ветер, сопротивления воздуха нет — скорость по X держится до раскрытия. Раскрытие разом гасит часть скорости.
    /// Под куполом гравитация слабее, а когда скорость выше порога (≈68 px/с + доля ветра), купол тормозит её каждый тик:
    /// спуск быстро выходит на ≈66 px/с (3 м/с), по X стикмен остаётся почти на месте и его сносит ветром (под куполом он в 10 раз сильнее).
    /// </summary>
    public static class FlightModel
    {
        /// <summary>
        /// Один шаг полёта. Как в оригинале: без купола сначала смещение, потом скорость; под куполом — наоборот.
        /// </summary>
        /// <returns>Смещение за шаг.</returns>
        public static Vector2 Advance(ref Vector2 velocity, ref CanopyState state, bool canopy, in FlightParams p, float h)
        {
            if (canopy)
            {
                velocity = Canopy(velocity, ref state, p, h);
                return velocity * h;
            }
            Vector2 delta = velocity * h;
            velocity = Fall(velocity, p, h);
            return delta;
        }

        /// <summary>Скорость за шаг свободного полёта.</summary>
        public static Vector2 Fall(Vector2 v, in FlightParams p, float h)
        {
            v.y = Mathf.Max(v.y - p.Gravity * h, -p.FallSpeedCap);
            v.x += p.GameWind * p.FlightWindPerTick * Ticks(p, h);
            return v;
        }

        /// <summary>Раскрытие купола (сразу в момент тапа).</summary>
        public static Vector2 Deploy(Vector2 v, in FlightParams p)
        {
            if (v.y >= 0f) return v * p.CanopyBrakeRising;
            float fall = Mathf.Min(-v.y, p.CanopyBrakeMaxSpeed);
            return v * Mathf.LerpUnclamped(p.CanopyBrakeFalling.x, p.CanopyBrakeFalling.y, fall / p.CanopyBrakeRef);
        }

        /// <summary>Скорость за шаг под куполом.</summary>
        public static Vector2 Canopy(Vector2 v, ref CanopyState state, in FlightParams p, float h)
        {
            float n = Ticks(p, h);
            state.Ticks += n;
            float ticks = Mathf.Floor(state.Ticks + 1e-4f); // the original counts whole ticks

            // Weaker gravity; even weaker at slow descent.
            float k = p.CanopyGravity.x;
            if (v.y > p.CanopyLiftFrom)
                k = Mathf.LerpUnclamped(p.CanopyGravity.x, p.CanopyGravity.y, (v.y - p.CanopyLiftFrom) / (p.CanopyLiftTo - p.CanopyLiftFrom));
            v.y -= k * p.Gravity * h;

            // Drag: only above the threshold (it grows with the wind over the first ticks).
            float threshold = p.CanopyDragFrom + Mathf.Abs(p.GameWind) *
                Mathf.Lerp(p.CanopyDragWind.x, p.CanopyDragWind.y, Mathf.Min(ticks, p.CanopyDragWindTicks) / p.CanopyDragWindTicks);
            if (v.magnitude > threshold)
            {
                float f = Mathf.Abs(v.x) >= p.CanopySlowSide
                    ? Mathf.Lerp(p.CanopyDrag.x, p.CanopyDrag.y, Mathf.Min(ticks, p.CanopyDragTicks) / p.CanopyDragTicks)
                    : state.Drag - p.CanopyDragStep * n;
                if (f < p.CanopyDrag.y) f = p.CanopyDrag.y;
                state.Drag = f;
                v.x *= Mathf.Pow(f + p.CanopyDragSideBonus, n);
                v.y *= Mathf.Pow(f, n);
            }

            v.x += p.GameWind * p.CanopyWindPerTick * n;
            return v;
        }

        /// <summary>Сколько тиков оригинала в шаге h.</summary>
        public static float Ticks(in FlightParams p, float h) => h * p.TickRate;

        /// <summary>Плавное раскрытие для картинки: сырой прогресс 0..1 → доля раскрытия (медленно в начале и в конце).</summary>
        public static float OpenCurve(float progress)
        {
            progress = Mathf.Clamp01(progress);
            return progress * progress * (3f - 2f * progress);
        }
    }
}
