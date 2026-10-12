using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.UI
{
    /// <summary>
    /// Кнопка нижней панели как в оригинале: скошенный параллелограмм с объёмом — светлая фаска сверху и слева,
    /// тёмная тень по правому скосу, между кнопками тёмный зазор подложки. Подпись в две строки («новый / ПРЫЖОК»).
    /// </summary>
    public sealed class SlantButton : Button
    {
        public static readonly Color Red = UIShapes.Red;
        public static readonly Color Blue = UIShapes.Blue;
        public static readonly Color Green = UIShapes.Green;
        static readonly Color Disabled = new Color32(150, 170, 182, 255);

        /// <summary>Скос: на сколько верх сдвинут относительно низа, в долях высоты.</summary>
        public const float SlantRatio = 0.22f;
        const float Bevel = 6f, Shade = 7f;

        readonly Color _fill;
        readonly bool _first, _last;
        readonly Label _small, _big;
        bool _hover, _down;

        public SlantButton(string small, string big, Color fill, Action onClick, bool first = false, bool last = false)
        {
            _fill = fill;
            _first = first;
            _last = last;
            AddToClassList("slant");
            if (first) AddToClassList("slant--first");
            if (last) AddToClassList("slant--last");
            text = string.Empty;
            _small = new Label(small) { pickingMode = PickingMode.Ignore };
            _small.AddToClassList("slant-small");
            _big = new Label(big) { pickingMode = PickingMode.Ignore };
            _big.AddToClassList("slant-big");
            if (!string.IsNullOrEmpty(small)) Add(_small);
            if (!string.IsNullOrEmpty(big)) Add(_big);
            if (onClick != null) clicked += onClick;
            else SetEnabled(false);
            generateVisualContent += Draw;
            RegisterCallback<PointerEnterEvent>(_ => { _hover = true; MarkDirtyRepaint(); });
            RegisterCallback<PointerLeaveEvent>(_ => { _hover = false; _down = false; MarkDirtyRepaint(); });
            RegisterCallback<PointerDownEvent>(_ => { _down = true; MarkDirtyRepaint(); }, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(_ => { _down = false; MarkDirtyRepaint(); });
        }

        public void SetLabels(string small, string big)
        {
            _small.text = small;
            _big.text = big;
        }

        /// <summary>Пустой голубой сегмент панели (в оригинале такие есть между кнопками).</summary>
        public static SlantButton Blank(bool first = false, bool last = false) => new SlantButton(null, null, Blue, null, first, last);

        void Draw(MeshGenerationContext ctx)
        {
            float w = resolvedStyle.width, h = resolvedStyle.height;
            if (w <= 1f || h <= 1f) return;
            float s = h * SlantRatio;
            bool blank = string.IsNullOrEmpty(_big.text);
            Color c = enabledSelf || blank ? _fill : Disabled;
            if (_down && enabledSelf) c = Color.Lerp(c, Color.black, 0.2f);
            else if (_hover && enabledSelf) c = Color.Lerp(c, Color.white, 0.12f);
            Color light = Color.Lerp(c, Color.white, 0.55f);
            Color dark = Color.Lerp(c, Color.black, 0.72f);

            var p = ctx.painter2D;
            // Layers: dark shade (full) → light bevel (right edge pulled in) → base (top/left pushed in too).
            Quad(p, w, h, s, 0f, 0f, 0f, dark);
            Quad(p, w, h, s, 0f, 0f, Shade, light);
            Quad(p, w, h, s, Bevel, Bevel, Shade, c);
        }

        /// <summary>Параллелограмм: top — отступ сверху, left — сдвиг левого края, right — сдвиг правого края внутрь.</summary>
        void Quad(Painter2D p, float w, float h, float s, float top, float left, float right, Color color)
        {
            float k = top / h; // slanted edges move with the top
            float tl = (_first ? 0f : s * (1f - k)) + left;
            float bl = left;
            float tr = (_last ? w : w - s * k) - right;
            float br = (_last ? w : w - s) - right;
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(new Vector2(tl, top));
            p.LineTo(new Vector2(tr, top));
            p.LineTo(new Vector2(br, h));
            p.LineTo(new Vector2(bl, h));
            p.ClosePath();
            p.Fill();
        }
    }
}
