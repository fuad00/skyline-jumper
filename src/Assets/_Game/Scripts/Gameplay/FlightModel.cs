using StickmanJumper.Config;
using UnityEngine;

namespace StickmanJumper.Gameplay
{
    /// <summary>Готовые коэффициенты полёта для одного уровня (ветер свой у каждого уровня).</summary>
    public readonly struct FlightParams
    {
        public readonly float Gravity;
        /// <summary>Сопротивление без купола по вертикали: a = k·|v|·v, где k = g / v_пред².</summary>
        public readonly float FreeDrag;
        /// <summary>Сопротивление без купола по горизонтали (то же, но со своей «предельной» скоростью).</summary>
        public readonly float FreeDragX;
        /// <summary>Сопротивление купола по вертикали (даёт CanopyTerminalSpeed).</summary>
        public readonly float CanopyDrag;
        /// <summary>Гашение скорости по X относительно ветра под куполом, 1/с.</summary>
        public readonly float BrakeX;
        public readonly float Wind;

        /// <param name="maxFallSpeed">Своя предельная скорость падения уровня, м/с (0 — из настроек).</param>
        public FlightParams(FlightConfig cfg, float wind, float maxFallSpeed = 0f)
        {
            Gravity = cfg.Gravity;
            float vt = maxFallSpeed > 0f ? maxFallSpeed : cfg.FreefallTerminalSpeed;
            FreeDrag = cfg.Gravity / Mathf.Max(1f, vt * vt);
            FreeDragX = cfg.Gravity / Mathf.Max(1f, cfg.FreefallGlideSpeed * cfg.FreefallGlideSpeed);
            float canopyVt = Mathf.Max(0.5f, cfg.CanopyTerminalSpeed);
            CanopyDrag = cfg.Gravity / (canopyVt * canopyVt);
            BrakeX = Mathf.Max(0f, cfg.CanopyBrakeX);
            Wind = wind;
        }
    }

    /// <summary>
    /// Точка с сопротивлением воздуха относительно ветра, по осям отдельно: падает до предельной скорости, а скорость
    /// по X в свободном падении держит долго. Раскрытый купол резко гасит скорость по X (остаётся только снос ветром)
    /// и тормозит спуск до CanopyTerminalSpeed. Рулить нельзя — как в оригинале.
    /// </summary>
    public static class FlightModel
    {
        /// <param name="canopy">Раскрытие купола 0..1 (уже сглаженное).</param>
        public static Vector2 Acceleration(Vector2 velocity, float canopy, in FlightParams p)
        {
            canopy = Mathf.Clamp01(canopy);
            float relX = velocity.x - p.Wind;
            float ax = -p.FreeDragX * Mathf.Abs(relX) * relX - canopy * p.BrakeX * relX;
            float ky = p.FreeDrag + (p.CanopyDrag - p.FreeDrag) * canopy * canopy;
            float ay = -p.Gravity - ky * Mathf.Abs(velocity.y) * velocity.y;
            return new Vector2(ax, ay);
        }

        /// <summary>Полунеявный Эйлер: сначала скорость, потом её же используем для смещения.</summary>
        public static Vector2 Step(Vector2 velocity, float canopy, in FlightParams p, float dt) =>
            velocity + Acceleration(velocity, canopy, p) * dt;

        /// <summary>Плавное раскрытие: сырой прогресс 0..1 → доля раскрытия (медленно в начале и в конце).</summary>
        public static float OpenCurve(float progress)
        {
            progress = Mathf.Clamp01(progress);
            return progress * progress * (3f - 2f * progress);
        }
    }
}
