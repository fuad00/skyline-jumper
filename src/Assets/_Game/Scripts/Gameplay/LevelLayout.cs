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
    /// Полоса разбега: стартовая крыша (номер 0) или промежуточная платформа. Бежим в сторону Dir (+1 вправо, −1 влево),
    /// прыгаем с края EdgeX, пятиться можно до BackLimitX, ступни на высоте RoofY.
    /// </summary>
    public readonly struct Runway
    {
        public readonly Rect Strip;
        public readonly int Dir;
        public readonly float RoofY, EdgeX, BackLimitX;

        public Runway(Rect strip, int dir, float roofY, float edgeX, float backLimitX)
        {
            Strip = strip;
            Dir = dir < 0 ? -1 : 1;
            RoofY = roofY;
            EdgeX = edgeX;
            BackLimitX = backLimitX;
        }

        /// <summary>Полоса с прыжком в сторону dir: край — передний конец, отступ от дальнего конца — margin, ступни на высоте roofY.</summary>
        public static Runway Make(Rect strip, int dir, float margin, float roofY)
        {
            margin = Mathf.Min(margin, strip.width * 0.5f);
            return dir < 0
                ? new Runway(strip, -1, roofY, strip.xMin, strip.xMax - margin)
                : new Runway(strip, 1, roofY, strip.xMax, strip.xMin + margin);
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
        /// <summary>Низ коробки платформы в файлах оригинала лежит на крыше с точностью до этого, м.</summary>
        public const float SurfaceSnap = 0.8f;
        /// <summary>Сел на площадку, если ступни не дальше этого от её поверхности по высоте, м.</summary>
        public const float SurfaceSlack = 1f;

        readonly List<Solid> _solids = new List<Solid>();
        readonly List<Runway> _runways = new List<Runway>();

        public IReadOnlyList<Solid> Solids => _solids;
        /// <summary>Полосы разбега: 0 — старт, дальше — промежуточные платформы.</summary>
        public IReadOnlyList<Runway> Runways => _runways;
        public float RoofY { get; private set; }
        public float EdgeX { get; private set; }
        public float BackLimitX { get; private set; }
        public float StartFeetX { get; private set; }
        public Rect TargetRect { get; private set; }
        /// <summary>Высота поверхности цели: на ней стоит флажок и на неё садится стикмен, м.</summary>
        public float TargetY { get; private set; }
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
            layout._runways.Add(new Runway(new Rect(s.X, s.Height, s.Width, 0f), 1, layout.RoofY, layout.EdgeX, layout.BackLimitX));
            layout.TargetRect = ToRect(t);
            layout.TargetY = t.Height;
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
            int scenery = layout._solids.Count;

            // The original's Runway / Platform / InterPlatform objects are boxes standing ON the roof (their bottom is the
            // surface, give or take a few pixels): stand on the roof right under the box, not on top of the box.
            float targetY = layout.SurfaceUnder(scene.Platform, scenery);
            layout.TargetIndex = layout._solids.Count;
            layout._solids.Add(new Solid(new Rect(scene.Platform.xMin, targetY - 1f, scene.Platform.width, 1f), SolidKind.Building, true));
            layout.TargetRect = scene.Platform;
            layout.TargetY = targetY;
            layout.ArrowX = scene.Platform.center.x;

            // The runway strip is the run-up surface (on a roof, a crane jib or a plank in mid-air), the jump edge at its front end.
            var run = scene.Runway;
            float roof = layout.SurfaceUnder(run, scenery), back = run.xMin, right = run.xMax;
            layout.RoofY = roof;
            layout.EdgeX = right;
            layout.BackLimitX = back + Mathf.Min(jump.BackWallMargin, (right - back) * 0.5f);
            layout.StartFeetX = Mathf.Max(layout.BackLimitX, layout.EdgeX - jump.StartDistanceFromEdge);
            layout._runways.Add(Runway.Make(run, 1, jump.BackWallMargin, roof));

            // Intermediate platforms: land on one and the next run-up starts there (its own direction). A thin solid makes
            // sure there is something to stand on even where the picture under it has no physics (a plank, a crane jib).
            if (scene.InterPlatforms != null)
                for (int i = 0; i < scene.InterPlatforms.Length; i++)
                {
                    var strip = scene.InterPlatforms[i];
                    bool left = scene.InterLeft != null && i < scene.InterLeft.Length && scene.InterLeft[i];
                    float y = layout.SurfaceUnder(strip, scenery);
                    layout._runways.Add(Runway.Make(strip, left ? -1 : 1, jump.BackWallMargin, y));
                    layout._solids.Add(new Solid(new Rect(strip.xMin, y - 1f, strip.width, 1f), SolidKind.Building, false));
                }

            // The overview shows the whole route: start, every intermediate platform and the target.
            float x0 = Mathf.Min(back, scene.Platform.xMin), x1 = Mathf.Max(right, scene.Platform.xMax), top = Mathf.Max(roof, targetY);
            foreach (var r in layout._runways)
            {
                x0 = Mathf.Min(x0, r.Strip.xMin);
                x1 = Mathf.Max(x1, r.Strip.xMax);
                top = Mathf.Max(top, r.RoofY);
            }
            layout.Overview = Rect.MinMaxRect(x0 - 4f, 0f, x1 + 4f, top + 4f);
            return layout;
        }

        /// <summary>Верх твёрдого прямо под коробкой оригинала (не дальше <see cref="SurfaceSnap"/> от её низа); нет такого — низ коробки.</summary>
        float SurfaceUnder(Rect box, int count)
        {
            float best = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                var r = _solids[i].Rect;
                if (r.xMax <= box.xMin || r.xMin >= box.xMax) continue;
                if (Mathf.Abs(r.yMax - box.yMin) <= SurfaceSnap && r.yMax > best) best = r.yMax;
            }
            return float.IsNegativeInfinity(best) ? box.yMin : best;
        }

        /// <summary>Стоит ли стикмен (ступни в feet) на платформе-цели.</summary>
        public bool OnTarget(Vector2 feet) => OnStrip(TargetRect, TargetY, feet);

        /// <summary>Номер промежуточной платформы под ступнями (−1 — не на ней).</summary>
        public int PlatformAt(Vector2 feet)
        {
            for (int i = 1; i < _runways.Count; i++)
                if (OnStrip(_runways[i].Strip, _runways[i].RoofY, feet)) return i;
            return -1;
        }

        static bool OnStrip(Rect strip, float surfaceY, Vector2 feet) =>
            feet.x >= strip.xMin && feet.x <= strip.xMax && Mathf.Abs(feet.y - surfaceY) <= SurfaceSlack;

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
