using System.Collections.Generic;
using UnityEngine;

namespace StickmanJumper.Gameplay
{
    public struct SweepHit
    {
        /// <summary>Доля пути до касания (0..1).</summary>
        public float T;
        /// <summary>Нормаль поверхности, в которую упёрлись (вверх = крыша/улица).</summary>
        public Vector2 Normal;
        /// <summary>Индекс прямоугольника в списке.</summary>
        public int Index;
    }

    /// <summary>Движущаяся коробка против неподвижных прямоугольников (всё выровнено по осям). Детерминированно — для повторов.</summary>
    public static class Sweep
    {
        /// <summary>Перекрытие меньше этого считаем касанием, а не застреванием, м.</summary>
        public const float Skin = 0.002f;

        public static bool Cast(Rect box, Vector2 delta, IReadOnlyList<Solid> solids, out SweepHit hit)
        {
            hit = new SweepHit { T = float.MaxValue, Index = -1 };
            Vector2 c = box.center;
            Vector2 half = box.size * 0.5f;
            for (int i = 0; i < solids.Count; i++)
            {
                var r = solids[i].Rect;
                // Minkowski: grow the solid by the box half-size and cast the box centre as a ray.
                var min = r.min - half;
                var max = r.max + half;
                if (!Slab(c.x, delta.x, min.x, max.x, out float nearX, out float farX)) continue;
                if (!Slab(c.y, delta.y, min.y, max.y, out float nearY, out float farY)) continue;
                float enter = Mathf.Max(nearX, nearY);
                float exit = Mathf.Min(farX, farY);
                if (enter > exit || exit < 0f || enter > 1f) continue;

                Vector2 normal = nearX > nearY
                    ? new Vector2(-Mathf.Sign(delta.x), 0f)
                    : new Vector2(0f, -Mathf.Sign(delta.y));
                if (enter < 0f)
                {
                    // Already overlapping: only a hair-thin touch counts (standing on a roof, sliding along a wall).
                    float depth = nearX > nearY
                        ? (delta.x > 0f ? c.x - min.x : max.x - c.x)
                        : (delta.y > 0f ? c.y - min.y : max.y - c.y);
                    if (depth > Skin) continue;
                    enter = 0f;
                }
                if (Vector2.Dot(delta, normal) >= 0f) continue; // moving away from the surface
                if (enter < hit.T)
                {
                    hit.T = enter;
                    hit.Normal = normal;
                    hit.Index = i;
                }
            }
            return hit.Index >= 0;
        }

        /// <summary>Box overlaps any solid by more than the skin.</summary>
        public static bool Overlaps(Rect box, IReadOnlyList<Solid> solids)
        {
            for (int i = 0; i < solids.Count; i++)
            {
                var r = solids[i].Rect;
                if (box.xMin < r.xMax - Skin && box.xMax > r.xMin + Skin && box.yMin < r.yMax - Skin && box.yMax > r.yMin + Skin)
                    return true;
            }
            return false;
        }

        static bool Slab(float origin, float dir, float min, float max, out float near, out float far)
        {
            if (Mathf.Abs(dir) < 1e-9f)
            {
                near = float.NegativeInfinity;
                far = float.PositiveInfinity;
                return origin > min && origin < max;
            }
            float t1 = (min - origin) / dir;
            float t2 = (max - origin) / dir;
            near = Mathf.Min(t1, t2);
            far = Mathf.Max(t1, t2);
            return true;
        }
    }
}
