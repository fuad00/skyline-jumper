using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.UI
{
    /// <summary>Палитра и фигуры интерфейса в стиле оригинала (цвета сняты с его картинок).</summary>
    public static class UIShapes
    {
        public static readonly Color Dark = new Color32(35, 31, 32, 255);      // #231F20 — тёмные плашки
        public static readonly Color Blue = new Color32(134, 199, 221, 255);   // #86C7DD — голубые плашки
        public static readonly Color Red = new Color32(255, 96, 96, 255);      // #FF6060 — красные кнопки
        public static readonly Color Green = new Color32(84, 178, 82, 255);
        public static readonly Color HudBlue = new Color32(37, 170, 225, 128); // полупрозрачные панели HUD
        public static readonly Color Ink = new Color32(16, 16, 18, 255);

        /// <summary>Контур скруглённого прямоугольника.</summary>
        public static void RoundRect(Painter2D p, Rect r, float radius)
        {
            radius = Mathf.Max(0f, Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f));
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin + radius, r.yMin));
            p.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), radius);
            p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), radius);
            p.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), radius);
            p.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), radius);
            p.ClosePath();
        }

        public static void FillRoundRect(Painter2D p, Rect r, float radius, Color c)
        {
            p.fillColor = c;
            RoundRect(p, r, radius);
            p.Fill();
        }

        /// <summary>Мягкая тень: несколько расширяющихся полупрозрачных слоёв.</summary>
        public static void Shadow(Painter2D p, Rect r, float radius, float spread, float alpha, Vector2 offset)
        {
            const int layers = 8;
            for (int i = layers; i >= 1; i--)
            {
                float k = i / (float)layers;
                var rr = new Rect(r.x - spread * k + offset.x, r.y - spread * k + offset.y, r.width + 2f * spread * k, r.height + 2f * spread * k);
                FillRoundRect(p, rr, radius + spread * k, new Color(0f, 0f, 0f, alpha / layers));
            }
        }

        public static Rect Inset(Rect r, float d) => new Rect(r.x + d, r.y + d, r.width - 2f * d, r.height - 2f * d);

        /// <summary>
        /// Тёмная плашка оригинала: мягкая тень, белый кант, серая внутренняя тень, тёмная заливка.
        /// Возвращает внутренний прямоугольник (куда класть содержимое).
        /// </summary>
        public static Rect DarkPlate(Painter2D p, Rect r, float radius, float rim, bool shadow = true)
        {
            if (shadow) Shadow(p, r, radius, rim * 3.5f, 0.45f, new Vector2(0f, rim * 0.5f));
            FillRoundRect(p, r, radius, Color.white);
            var a = Inset(r, rim);
            FillRoundRect(p, a, radius - rim, new Color32(150, 148, 148, 255));
            var b = Inset(a, rim * 0.35f);
            FillRoundRect(p, b, radius - rim * 1.35f, new Color32(90, 87, 87, 255));
            var c = new Rect(b.x + rim * 0.4f, b.y + rim * 0.5f, b.width - rim * 0.4f, b.height - rim * 0.5f);
            FillRoundRect(p, c, radius - rim * 1.6f, Dark);
            return c;
        }

        /// <summary>Цветная плашка с белым кантом и светлой фаской сверху-слева (кнопка «всего ОЧКОВ» в оригинале).</summary>
        public static Rect RimmedPlate(Painter2D p, Rect r, float radius, float rim, Color fill)
        {
            FillRoundRect(p, r, radius, Color.white);
            var a = Inset(r, rim);
            FillRoundRect(p, a, radius - rim, Color.Lerp(fill, Color.white, 0.55f));
            var b = new Rect(a.x + rim * 0.8f, a.y + rim * 0.8f, a.width - rim * 0.8f, a.height - rim * 0.8f);
            FillRoundRect(p, b, radius - rim * 1.6f, fill);
            return b;
        }

        /// <summary>Светлая плашка оригинала: чёрная обводка, тонкая белая линия, заливка.</summary>
        public static Rect LightPlate(Painter2D p, Rect r, float radius, float outline, Color fill, bool shadow = false)
        {
            if (shadow) Shadow(p, r, radius, outline * 3f, 0.3f, new Vector2(0f, outline));
            FillRoundRect(p, r, radius, Ink);
            var a = Inset(r, outline);
            FillRoundRect(p, a, radius - outline, Color.white);
            var b = Inset(a, outline * 0.45f);
            FillRoundRect(p, b, radius - outline * 1.45f, fill);
            return b;
        }
    }

    /// <summary>Фон-плашка, нарисованная кодом (тёмная или светлая). Содержимое кладётся детьми с отступами в USS.</summary>
    public sealed class Plate : VisualElement
    {
        public enum Kind { Dark, Light, LightShadow, Hud, Rimmed }

        readonly Kind _kind;
        public Color Fill = UIShapes.Blue;
        public float Radius = 30f, Rim = 7f;

        public Plate(Kind kind)
        {
            _kind = kind;
            if (kind == Kind.Hud) Fill = UIShapes.HudBlue;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = new Rect(0f, 0f, resolvedStyle.width, resolvedStyle.height);
            if (r.width <= 2f || r.height <= 2f) return;
            var p = ctx.painter2D;
            if (_kind == Kind.Dark) UIShapes.DarkPlate(p, r, Radius, Rim);
            else if (_kind == Kind.Rimmed) UIShapes.RimmedPlate(p, r, Radius, Rim, Fill);
            else UIShapes.LightPlate(p, r, Radius, Rim, Fill, _kind == Kind.LightShadow);
        }

        /// <summary>
        /// «Одеть» элемент в плашку: она встаёт первым ребёнком на всю площадь (ниже остального содержимого).
        /// Своих фона и рамок у элемента быть не должно.
        /// </summary>
        public static Plate Dress(VisualElement e, Kind kind, float radius, float rim, Color? fill = null)
        {
            var plate = new Plate(kind) { Radius = radius, Rim = rim, pickingMode = PickingMode.Ignore };
            if (fill.HasValue) plate.Fill = fill.Value;
            plate.style.position = Position.Absolute;
            plate.style.left = 0;
            plate.style.top = 0;
            plate.style.right = 0;
            plate.style.bottom = 0;
            e.Insert(0, plate);
            e.RegisterCallback<GeometryChangedEvent>(_ => plate.MarkDirtyRepaint());
            return plate;
        }
    }

    /// <summary>Часы таймера как в оригинале: тёмный циферблат, белые риски, голубая стрелка — сколько времени прошло.</summary>
    public sealed class ClockDial : VisualElement
    {
        float _used;

        public ClockDial()
        {
            AddToClassList("clock");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        /// <summary>Доля прошедшего времени 0..1.</summary>
        public float Used
        {
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Abs(value - _used) < 0.002f) return;
                _used = value;
                MarkDirtyRepaint();
            }
        }

        void Draw(MeshGenerationContext ctx)
        {
            float size = Mathf.Min(resolvedStyle.width, resolvedStyle.height);
            if (size <= 2f) return;
            var p = ctx.painter2D;
            var c = new Vector2(resolvedStyle.width * 0.5f, resolvedStyle.height * 0.5f);
            float rad = size * 0.5f;
            p.fillColor = Color.white;
            p.BeginPath();
            p.Arc(c, rad, 0f, 360f);
            p.Fill();
            p.fillColor = UIShapes.Dark;
            p.BeginPath();
            p.Arc(c, rad * 0.9f, 0f, 360f);
            p.Fill();
            // Elapsed sector in light blue.
            if (_used > 0.001f)
            {
                p.fillColor = new Color32(37, 170, 225, 150);
                p.BeginPath();
                p.MoveTo(c);
                p.Arc(c, rad * 0.78f, -90f, -90f + 360f * _used);
                p.ClosePath();
                p.Fill();
            }
            p.strokeColor = Color.white;
            p.lineCap = LineCap.Round;
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                p.lineWidth = i % 3 == 0 ? 4f : 2.5f;
                p.BeginPath();
                p.MoveTo(c + dir * rad * (i % 3 == 0 ? 0.6f : 0.68f));
                p.LineTo(c + dir * rad * 0.8f);
                p.Stroke();
            }
            float h = (_used * 360f) * Mathf.Deg2Rad;
            var hand = new Vector2(Mathf.Sin(h), -Mathf.Cos(h));
            p.strokeColor = new Color32(120, 210, 245, 255);
            p.lineWidth = 5f;
            p.BeginPath();
            p.MoveTo(c);
            p.LineTo(c + hand * rad * 0.74f);
            p.Stroke();
            p.fillColor = Color.white;
            p.BeginPath();
            p.Arc(c, rad * 0.1f, 0f, 360f);
            p.Fill();
        }
    }
}
