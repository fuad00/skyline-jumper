using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.UI
{
    /// <summary>
    /// Кнопка нижней панели как в оригинале: скошенный параллелограмм с объёмом — светлая фаска сверху и слева,
    /// тёмная тень по правому скосу, между кнопками тёмный зазор подложки. Подпись в две строки («новый / ПРЫЖОК»).
    /// Длинная подпись («НАСТРОЙКИ», «ПРОГРЕСС») сама уменьшается, чтобы влезть в кнопку.
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
        /// <summary>Размер шрифта подписей, пока они влезают (мелкая строка / КРУПНАЯ).</summary>
        public const float SmallSize = 36f, BigSize = 64f;

        readonly Color _fill;
        readonly bool _first, _last;
        readonly Label _small, _big;
        bool _hover, _down, _suppressClick;
        float _holdSeconds, _downAt;
        Action _onHold;
        IVisualElementScheduledItem _holdCheck;

        public SlantButton(string small, string big, Color fill, Action onClick, bool first = false, bool last = false)
        {
            _fill = fill;
            _first = first;
            _last = last;
            AddToClassList("slant");
            if (first) AddToClassList("slant--first");
            if (last) AddToClassList("slant--last");
            text = string.Empty;
            _small = new Label(small) { pickingMode = PickingMode.Ignore, style = { fontSize = SmallSize } };
            _small.AddToClassList("slant-small");
            _big = new Label(big) { pickingMode = PickingMode.Ignore, style = { fontSize = BigSize } };
            _big.AddToClassList("slant-big");
            Add(_small);
            Add(_big);
            ShowLabels();
            if (onClick != null)
                clicked += () =>
                {
                    if (_suppressClick) { _suppressClick = false; return; } // the press was a long hold, not a tap
                    onClick();
                };
            else SetEnabled(false);
            generateVisualContent += Draw;
            RegisterCallback<PointerEnterEvent>(_ => { _hover = true; MarkDirtyRepaint(); });
            RegisterCallback<PointerLeaveEvent>(_ => { _hover = false; _down = false; MarkDirtyRepaint(); });
            RegisterCallback<PointerDownEvent>(_ => { _down = true; _downAt = UnityEngine.Time.realtimeSinceStartup; MarkDirtyRepaint(); }, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(_ => { _down = false; MarkDirtyRepaint(); });
            RegisterCallback<GeometryChangedEvent>(_ => FitText());
        }

        /// <summary>Долгое удержание (секретные действия): держишь seconds — срабатывает action, обычного нажатия уже не будет.</summary>
        public SlantButton OnHold(float seconds, Action action)
        {
            _holdSeconds = seconds;
            _onHold = action;
            _holdCheck ??= schedule.Execute(CheckHold).Every(100);
            return this;
        }

        void CheckHold()
        {
            if (!_down || _onHold == null || _suppressClick) return;
            if (UnityEngine.Time.realtimeSinceStartup - _downAt < _holdSeconds) return;
            _suppressClick = true;
            _onHold();
        }

        public void SetLabels(string small, string big)
        {
            _small.text = small ?? string.Empty;
            _big.text = big ?? string.Empty;
            ShowLabels();
            schedule.Execute(FitText);
        }

        void ShowLabels()
        {
            _small.style.display = string.IsNullOrEmpty(_small.text) ? DisplayStyle.None : DisplayStyle.Flex;
            _big.style.display = string.IsNullOrEmpty(_big.text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Подписи не шире кнопки: если слово длинное, шрифт меньше (но не больше обычного).</summary>
        void FitText()
        {
            float room = contentRect.width;
            Fit(_small, SmallSize, room);
            Fit(_big, BigSize, room);
        }

        static void Fit(Label label, float baseSize, float room)
        {
            if (room <= 1f || string.IsNullOrEmpty(label.text)) return;
            float current = label.resolvedStyle.fontSize;
            if (current <= 0f) return;
            // Text width grows in proportion to the font size: measure at the current size, scale to the base size.
            float atBase = label.MeasureTextSize(label.text, 0f, MeasureMode.Undefined, 0f, MeasureMode.Undefined).x * baseSize / current;
            float size = atBase > room ? Mathf.Floor(baseSize * room / atBase) : baseSize;
            if (Mathf.Abs(size - current) >= 1f) label.style.fontSize = size;
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
