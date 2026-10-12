using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.UI
{
    /// <summary>
    /// Кнопка нижней панели как в оригинале: скошенный параллелограмм с подписью в две строки («новый / ПРЫЖОК»).
    /// Соседние кнопки заходят друг на друга скосами; у первой ровный левый край, у последней — правый.
    /// </summary>
    public sealed class SlantButton : Button
    {
        public static readonly Color Red = new Color32(234, 84, 78, 255);
        public static readonly Color Blue = new Color32(127, 192, 222, 255);
        public static readonly Color Green = new Color32(84, 178, 82, 255);
        static readonly Color Disabled = new Color32(150, 170, 182, 255);

        const float SlantRatio = 0.34f;
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

        /// <summary>Пустой декоративный сегмент панели (в оригинале такие есть между кнопками).</summary>
        public static SlantButton Blank(bool first = false, bool last = false) => new SlantButton(null, null, Blue, null, first, last);

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 1f || r.height <= 1f) return;
            float w = r.width, h = r.height, s = h * SlantRatio;
            Color c = enabledSelf || string.IsNullOrEmpty(_big.text) ? _fill : Disabled;
            if (_down) c = Color.Lerp(c, Color.black, 0.18f);
            else if (_hover && enabledSelf) c = Color.Lerp(c, Color.white, 0.15f);

            var p = ctx.painter2D;
            p.fillColor = c;
            p.BeginPath();
            p.MoveTo(new Vector2(_first ? 0f : s, 0f));
            p.LineTo(new Vector2(w, 0f));
            p.LineTo(new Vector2(_last ? w : w - s, h));
            p.LineTo(new Vector2(0f, h));
            p.ClosePath();
            p.Fill();
        }
    }
}
