using StickmanJumper.Config;
using UnityEngine;

namespace StickmanJumper.Gameplay
{
    /// <summary>Готовые коэффициенты полёта для одного уровня (ветер свой у каждого уровня).</summary>
    public readonly struct FlightParams
    {
        public readonly float Gravity;
        /// <summary>Сопротивление без купола: a = k·|v|·v, где k = g / v_пред².</summary>
        public readonly float FreeDrag;
        public readonly float CanopyDrag;
        /// <summary>Тяга купола вперёд, м/с² (даёт планирование CanopyGlideSpeed).</summary>
        public readonly float GlidePush;
        public readonly float Wind;
        /// <summary>+1 — прыгаем вправо.</summary>
        public readonly float Facing;

        public FlightParams(FlightConfig cfg, float wind, float facing = 1f)
        {
            Gravity = cfg.Gravity;
            FreeDrag = cfg.Gravity / Mathf.Max(1f, cfg.FreefallTerminalSpeed * cfg.FreefallTerminalSpeed);
            float vt = Mathf.Max(0.5f, cfg.CanopyTerminalSpeed);
            CanopyDrag = cfg.Gravity / (vt * vt);
            float glide = Mathf.Max(0f, cfg.CanopyGlideSpeed);
            GlidePush = CanopyDrag * Mathf.Sqrt(vt * vt + glide * glide) * glide;
            Wind = wind;
            Facing = Mathf.Sign(facing == 0f ? 1f : facing);
        }
    }

    /// <summary>
    /// Точка с сопротивлением воздуха относительно ветра. Купол плавно увеличивает сопротивление (по кубу раскрытия,
    /// чтобы рывок не был мгновенным) и немного тянет вперёд. Рулить нельзя — как в оригинале.
    /// </summary>
    public static class FlightModel
    {
        public static Vector2 Acceleration(Vector2 velocity, float canopy, in FlightParams p)
        {
            canopy = Mathf.Clamp01(canopy);
            Vector2 rel = velocity - new Vector2(p.Wind, 0f);
            float k = p.FreeDrag + (p.CanopyDrag - p.FreeDrag) * canopy * canopy * canopy;
            Vector2 a = new Vector2(0f, -p.Gravity) - k * rel.magnitude * rel;
            a.x += canopy * p.GlidePush * p.Facing;
            return a;
        }

        /// <summary>Полунеявный Эйлер: сначала скорость, потом её же используем для смещения.</summary>
        public static Vector2 Step(Vector2 velocity, float canopy, in FlightParams p, float dt) =>
            velocity + Acceleration(velocity, canopy, p) * dt;
    }
}
