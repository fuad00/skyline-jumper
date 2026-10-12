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
    /// Геометрия уровня, посчитанная из LevelConfig: прямоугольники для столкновений, край крыши, стрелка.
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
        public int TargetIndex { get; private set; }

        /// <summary>Вся интересная часть уровня (для режима обзора): от стартовой крыши до цели.</summary>
        public Rect Overview { get; private set; }

        public static LevelLayout Build(LevelConfig level, JumpConfig jump)
        {
            var layout = new LevelLayout
            {
                Wind = level.Wind,
                TimeLimit = Mathf.Max(1f, level.TimeLimit),
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

        /// <summary>Расстояние от точки до стрелки по горизонтали, м.</summary>
        public float DistanceToArrow(float x) => Mathf.Abs(x - ArrowX);

        static Rect ToRect(BuildingSpec b) => new Rect(b.X, 0f, Mathf.Max(0.1f, b.Width), Mathf.Max(0.1f, b.Height));
    }
}
