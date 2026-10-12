using System.Collections.Generic;
using UnityEngine;

namespace StickmanJumper.Gameplay
{
    /// <summary>
    /// Столкновения картинки уровня (как в редакторе оригинала): её многоугольники в пикселях → прямоугольники в метрах
    /// с учётом места, масштаба и поворота. Общий расчёт для импорта уровней оригинала и для карт из редактора.
    /// </summary>
    public static class PictureShapes
    {
        /// <summary>Пикселей картинки на метр при масштабе 1 (масштаб редактора оригинала: 44 единицы = 1 м).</summary>
        public const float PixelsPerMeter = 44f;
        /// <summary>
        /// Единиц физики оригинала (скорости, ветер, maxFlySpeedY) в метре: при загрузке уровня он умножает координаты файла
        /// на 0.5 и дальше считает в них (reference/disasm/FINDINGS.md). То же число — FlightConfig.PixelsPerMeter.
        /// </summary>
        public const float PhysicsUnitsPerMeter = PixelsPerMeter * 0.5f;
        /// <summary>Тоньше этого кусок столкновения не считаем (линии-разделители, ошибки разметки), м.</summary>
        const float MinThickness = 0.05f;

        /// <summary>
        /// Добавляет в into прямоугольники (м) многоугольников polys (px от левого нижнего угла картинки, ось Y вверх)
        /// картинки размером sizePx с центром в center (м), масштабом scale (минус — отражена) и поворотом rotation
        /// (градусы против часовой). Без многоугольников — вся картинка.
        /// </summary>
        public static void AddSolids(List<Rect> into, IReadOnlyList<Vector2[]> polys, Vector2 sizePx, Vector2 center, Vector2 scale, float rotation)
        {
            if (polys == null || polys.Count == 0)
            {
                AddPolygon(into, new[] { Vector2.zero, new Vector2(sizePx.x, 0f), sizePx, new Vector2(0f, sizePx.y) }, sizePx, center, scale, rotation);
                return;
            }
            foreach (var poly in polys) AddPolygon(into, poly, sizePx, center, scale, rotation);
        }

        static void AddPolygon(List<Rect> into, Vector2[] poly, Vector2 sizePx, Vector2 center, Vector2 scale, float rotation)
        {
            if (poly == null || poly.Length == 0) return;
            float a = rotation * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in poly)
            {
                var w = ToWorld(p, sizePx, center, scale, c, s);
                x0 = Mathf.Min(x0, w.x); x1 = Mathf.Max(x1, w.x);
                y0 = Mathf.Min(y0, w.y); y1 = Mathf.Max(y1, w.y);
            }
            if (x1 - x0 > MinThickness && y1 - y0 > MinThickness) into.Add(Rect.MinMaxRect(x0, y0, x1, y1));
        }

        /// <summary>Точка картинки (px от левого нижнего угла) → мир (м).</summary>
        public static Vector2 ToWorld(Vector2 px, Vector2 sizePx, Vector2 center, Vector2 scale, float rotation)
        {
            float a = rotation * Mathf.Deg2Rad;
            return ToWorld(px, sizePx, center, scale, Mathf.Cos(a), Mathf.Sin(a));
        }

        static Vector2 ToWorld(Vector2 px, Vector2 sizePx, Vector2 center, Vector2 scale, float c, float s)
        {
            float lx = (px.x - sizePx.x * 0.5f) * scale.x / PixelsPerMeter;
            float ly = (px.y - sizePx.y * 0.5f) * scale.y / PixelsPerMeter;
            return new Vector2(center.x + lx * c - ly * s, center.y + lx * s + ly * c);
        }

        /// <summary>Мир (м) → точка картинки (px от левого нижнего угла). Обратное к <see cref="ToWorld(Vector2, Vector2, Vector2, Vector2, float)"/>.</summary>
        public static Vector2 ToPicture(Vector2 world, Vector2 sizePx, Vector2 center, Vector2 scale, float rotation)
        {
            float a = -rotation * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            Vector2 d = world - center;
            float lx = d.x * c - d.y * s, ly = d.x * s + d.y * c;
            float sx = Mathf.Abs(scale.x) < 1e-4f ? 1e-4f : scale.x, sy = Mathf.Abs(scale.y) < 1e-4f ? 1e-4f : scale.y;
            return new Vector2(lx * PixelsPerMeter / sx + sizePx.x * 0.5f, ly * PixelsPerMeter / sy + sizePx.y * 0.5f);
        }

        /// <summary>Прямоугольник (м), в который попадает часть картинки rectPx (px) после масштаба и поворота.</summary>
        public static Rect Bounds(Rect rectPx, Vector2 sizePx, Vector2 center, Vector2 scale, float rotation)
        {
            float a = rotation * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in new[] { rectPx.min, new Vector2(rectPx.xMax, rectPx.yMin), rectPx.max, new Vector2(rectPx.xMin, rectPx.yMax) })
            {
                var w = ToWorld(p, sizePx, center, scale, c, s);
                x0 = Mathf.Min(x0, w.x); x1 = Mathf.Max(x1, w.x);
                y0 = Mathf.Min(y0, w.y); y1 = Mathf.Max(y1, w.y);
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }
    }
}
