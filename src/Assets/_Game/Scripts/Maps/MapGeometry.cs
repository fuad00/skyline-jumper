using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Maps
{
    /// <summary>Где на карте деталь: её рамка, непрозрачная часть, столкновения, попадание пальцем, поверхность под площадкой.</summary>
    public static class MapGeometry
    {
        /// <summary>Площадка лежит на поверхности, если до неё не дальше этого (сверху вниз), м.</summary>
        public const float StripSnap = 3f;

        /// <summary>Рамка непрозрачной части детали в мире (по ней детали прилипают друг к другу).</summary>
        public static Rect OpaqueBounds(in PieceDef def, MapPiece p) => PictureShapes.Bounds(def.OpaquePx, def.SizePx, p.Position, p.Scale, p.Rotation);

        /// <summary>Углы всей картинки в мире: левый низ, правый низ, правый верх, левый верх (до отражения).</summary>
        public static Vector2[] Corners(in PieceDef def, MapPiece p)
        {
            var s = def.SizePx;
            return new[]
            {
                PictureShapes.ToWorld(Vector2.zero, s, p.Position, p.Scale, p.Rotation),
                PictureShapes.ToWorld(new Vector2(s.x, 0f), s, p.Position, p.Scale, p.Rotation),
                PictureShapes.ToWorld(s, s, p.Position, p.Scale, p.Rotation),
                PictureShapes.ToWorld(new Vector2(0f, s.y), s, p.Position, p.Scale, p.Rotation),
            };
        }

        /// <summary>Попал ли палец (точка мира) в непрозрачную часть детали; slack — запас вокруг в метрах (для толстого пальца).</summary>
        public static bool Hit(in PieceDef def, MapPiece p, Vector2 world, float slack = 0f)
        {
            var px = PictureShapes.ToPicture(world, def.SizePx, p.Position, p.Scale, p.Rotation);
            if (PieceCatalog.Opaque(def, px)) return true;
            if (slack <= 0f) return false;
            // Thin parts (a crane jib, a plank): look around the finger too.
            float k = slack * PictureShapes.PixelsPerMeter / Mathf.Max(0.05f, Mathf.Min(Mathf.Abs(p.ScaleX), Mathf.Abs(p.ScaleY)));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                if (PieceCatalog.Opaque(def, px + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * k)) return true;
            }
            return false;
        }

        public static void AddSolids(List<Rect> into, PieceCatalog catalog, MapPiece p)
        {
            if (!p.Solid || catalog == null || !catalog.TryGet(p.Art, out var def)) return;
            PictureShapes.AddSolids(into, catalog.Shapes(def), def.SizePx, p.Position, p.Scale, p.Rotation);
        }

        /// <summary>Все твёрдые прямоугольники карты (без улицы).</summary>
        public static List<Rect> Solids(MapData map, PieceCatalog catalog)
        {
            var list = new List<Rect>();
            foreach (var p in map.Pieces) AddSolids(list, catalog, p);
            return list;
        }

        /// <summary>
        /// Верх твёрдого под отрезком [x0, x1], не выше fromY + <see cref="StripSnap"/> (улица — 0).
        /// NaN — под отрезком ничего нет ближе <see cref="StripSnap"/> (площадка висит в воздухе — так тоже можно).
        /// </summary>
        public static float SurfaceUnder(List<Rect> solids, float x0, float x1, float fromY)
        {
            float best = float.NegativeInfinity;
            foreach (var r in solids)
            {
                if (r.xMax <= x0 || r.xMin >= x1) continue;
                if (r.yMax <= fromY + StripSnap && r.yMax > best) best = r.yMax;
            }
            if (fromY + StripSnap >= 0f && 0f > best) best = 0f;
            return best > float.NegativeInfinity && fromY - best <= StripSnap ? best : float.NaN;
        }
    }
}
