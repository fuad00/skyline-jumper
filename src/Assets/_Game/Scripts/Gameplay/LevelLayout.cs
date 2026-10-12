using System.Collections.Generic;
using StickmanJumper.Config;
using UnityEngine;

namespace StickmanJumper.Gameplay
{
    public enum SolidKind { Ground, Building }

    /// <summary>Твёрдый прямоугольник уровня (улица или здание).</summary>
    public readonly struct Solid
    {
        public readonly Rect Rect;
        public readonly SolidKind Kind;
        public readonly bool IsTarget;

        public Solid(Rect rect, SolidKind kind, bool isTarget)
        {
            Rect = rect;
            Kind = kind;
            IsTarget = isTarget;
        }
    }

    /// <summary>
    /// Геометрия уровня, посчитанная из LevelConfig: прямоугольники для столкновений, край крыши, флажок цели.
    /// Чистый класс — его используют и игра, и тесты.
    /// </summary>
    public sealed class LevelLayout
    {
        public const float GroundDepth = 20f;
        public const float GroundMargin = 150f;

        readonly List<Solid> _solids = new List<Solid>();

        public IReadOnlyList<Solid> Solids => _solids;
        public float RoofY { get; private set; }
        public float EdgeX { get; private set; }
        public float BackLimitX { get; private set; }
        public float StartFeetX { get; private set; }
        public Rect TargetRect { get; private set; }
        public float ArrowX { get; private set; }
        public float Wind { get; private set; }
        public float TimeLimit { get; private set; }
        /// <summary>Своя предельная скорость падения уровня, м/с (0 — общая).</summary>
        public float MaxFallSpeed { get; private set; }
        public int TargetIndex { get; private set; }

        /// <summary>Вся интересная часть уровня (для режима обзора): от стартовой крыши до цели.</summary>
        public Rect Overview { get; private set; }

        public static LevelLayout Build(LevelConfig level, JumpConfig jump)
        {
            if (level.Scene != null) return BuildScene(level, level.Scene, jump);
            var layout = new LevelLayout
            {
                Wind = level.Wind,
                TimeLimit = Mathf.Max(1f, level.TimeLimit),
                MaxFallSpeed = level.MaxFallSpeed,
            };

            var s = level.Start;
            var t = level.Target;
            float minX = Mathf.Min(s.X, t.X);
            float maxX = Mathf.Max(s.X + s.Width, t.X + t.Width);
            layout._solids.Add(new Solid(new Rect(minX - GroundMargin, -GroundDepth, maxX - minX + 2f * GroundMargin, GroundDepth), SolidKind.Ground, false));
            layout._solids.Add(new Solid(ToRect(s), SolidKind.Building, false));
            layout.TargetIndex = layout._solids.Count;
            layout._solids.Add(new Solid(ToRect(t), SolidKind.Building, true));
            if (level.Others != null)
                foreach (var o in level.Others)
                    layout._solids.Add(new Solid(ToRect(o), SolidKind.Building, false));

            layout.RoofY = s.Height;
            layout.EdgeX = s.X + s.Width;
            layout.BackLimitX = s.X + Mathf.Min(jump.BackWallMargin, s.Width * 0.5f);
            layout.StartFeetX = Mathf.Max(layout.BackLimitX, layout.EdgeX - jump.StartDistanceFromEdge);
            layout.TargetRect = ToRect(t);
            layout.ArrowX = t.X + t.Width * Mathf.Clamp01(level.ArrowPosition);

            float top = Mathf.Max(s.Height, t.Height) + 3f;
            layout.Overview = Rect.MinMaxRect(s.X - 2f, 0f, t.X + t.Width + 2f, top);
            return layout;
        }

        /// <summary>
        /// Уровень оригинала: твёрдые прямоугольники из сцены (соседние куски одного здания склеены), цель — платформа,
        /// старт — полоса разбега (её правый край — край прыжка).
        /// </summary>
        static LevelLayout BuildScene(LevelConfig level, LevelScene scene, JumpConfig jump)
        {
            var layout = new LevelLayout
            {
                Wind = level.Wind,
                TimeLimit = Mathf.Max(1f, level.TimeLimit),
                MaxFallSpeed = level.MaxFallSpeed,
            };
            float width = Mathf.Max(scene.Size.x, scene.Platform.xMax, scene.Runway.xMax);
            layout._solids.Add(new Solid(new Rect(-GroundMargin, -GroundDepth, width + 2f * GroundMargin, GroundDepth), SolidKind.Ground, false));
            var rects = Merge(scene.Solids);
            foreach (var r in rects) layout._solids.Add(new Solid(r, SolidKind.Building, false));
            if (scene.InterPlatforms != null)
                foreach (var r in scene.InterPlatforms) layout._solids.Add(new Solid(r, SolidKind.Building, false));
            layout.TargetIndex = layout._solids.Count;
            layout._solids.Add(new Solid(scene.Platform, SolidKind.Building, true));
            layout.TargetRect = scene.Platform;
            layout.ArrowX = scene.Platform.center.x;

            // The runway strip is the run-up surface (on a roof, a crane jib or a plank in mid-air): feet on its bottom,
            // the jump edge at its front end.
            var run = scene.Runway;
            float roof = run.yMin, back = run.xMin, right = run.xMax;
            layout.RoofY = roof;
            layout.EdgeX = right;
            layout.BackLimitX = back + Mathf.Min(jump.BackWallMargin, (right - back) * 0.5f);
            layout.StartFeetX = Mathf.Max(layout.BackLimitX, layout.EdgeX - jump.StartDistanceFromEdge);

            float x0 = Mathf.Min(back, scene.Platform.xMin) - 4f, x1 = Mathf.Max(right, scene.Platform.xMax) + 4f;
            layout.Overview = Rect.MinMaxRect(x0, 0f, x1, Mathf.Max(roof, scene.Platform.yMax) + 4f);
            return layout;
        }

        /// <summary>Склеивает куски одного здания (одинаковые края по X, стоят друг на друге) — меньше прямоугольников и швов.</summary>
        public static List<Rect> Merge(IEnumerable<Rect> rects)
        {
            var list = new List<Rect>(rects);
            list.Sort((a, b) => a.xMin != b.xMin ? a.xMin.CompareTo(b.xMin) : a.yMin.CompareTo(b.yMin));
            for (bool any = true; any;)
            {
                any = false;
                for (int i = 0; i < list.Count; i++)
                    for (int j = list.Count - 1; j > i; j--)
                    {
                        Rect a = list[i], b = list[j];
                        bool sameColumn = Mathf.Abs(a.xMin - b.xMin) < 0.1f && Mathf.Abs(a.xMax - b.xMax) < 0.1f;
                        bool touching = b.yMin <= a.yMax + 0.25f && a.yMin <= b.yMax + 0.25f;
                        if (!sameColumn || !touching) continue;
                        list[i] = Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
                        list.RemoveAt(j);
                        any = true;
                    }
            }
            return list;
        }

        /// <summary>Расстояние от точки до флажка по горизонтали, м.</summary>
        public float DistanceToArrow(float x) => Mathf.Abs(x - ArrowX);

        static Rect ToRect(BuildingSpec b) => new Rect(b.X, 0f, Mathf.Max(0.1f, b.Width), Mathf.Max(0.1f, b.Height));
    }
}
