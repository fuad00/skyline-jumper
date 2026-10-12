using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.UI
{
    public enum IconKind
    {
        Back, Undo, Redo, Walls, Gear, Play, Share, CopyRight, CopyUp, Building, Flip, Front, Behind,
        Solid, Backdrop, Reset, Trash, RunLeft, RunRight, Plus, Start, Target, Platform,
    }

    /// <summary>
    /// Значок, нарисованный кодом (в шрифте нет стрелок и галочек): линии и заливки в квадрате 100 × 100,
    /// растянутом на размер элемента. Цвет — <see cref="Ink"/>.
    /// </summary>
    public sealed class VectorIcon : VisualElement
    {
        IconKind _kind;
        Color _ink = UIShapes.Ink;

        public VectorIcon(IconKind kind)
        {
            _kind = kind;
            pickingMode = PickingMode.Ignore;
            AddToClassList("vicon");
            generateVisualContent += Draw;
        }

        public IconKind Kind
        {
            get => _kind;
            set { _kind = value; MarkDirtyRepaint(); }
        }

        public Color Ink
        {
            get => _ink;
            set { _ink = value; MarkDirtyRepaint(); }
        }

        Painter2D _p;
        float _s;
        Vector2 _o;

        Vector2 P(float x, float y) => _o + new Vector2(x, y) * _s;

        void Draw(MeshGenerationContext ctx)
        {
            float w = resolvedStyle.width, h = resolvedStyle.height;
            if (w <= 1f || h <= 1f) return;
            _p = ctx.painter2D;
            _s = Mathf.Min(w, h) / 100f;
            _o = new Vector2((w - 100f * _s) * 0.5f, (h - 100f * _s) * 0.5f);
            _p.strokeColor = _ink;
            _p.fillColor = _ink;
            _p.lineWidth = 9f * _s;
            _p.lineCap = LineCap.Round;
            _p.lineJoin = LineJoin.Round;
            switch (_kind)
            {
                case IconKind.Back:
                    Lines(P(80, 50), P(22, 50));
                    Lines(P(44, 27), P(21, 50), P(44, 73));
                    break;
                case IconKind.Undo:
                case IconKind.Redo:
                {
                    bool redo = _kind == IconKind.Redo;
                    float X(float x) => redo ? 100f - x : x;
                    _p.BeginPath();
                    _p.MoveTo(P(X(28), 36));
                    _p.LineTo(P(X(60), 36));
                    _p.Arc(P(X(60), 58), 22f * _s, -90f, 90f, redo ? ArcDirection.CounterClockwise : ArcDirection.Clockwise);
                    _p.LineTo(P(X(36), 80));
                    _p.Stroke();
                    Lines(P(X(44), 20), P(X(27), 36), P(X(44), 52));
                    break;
                }
                case IconKind.Walls:
                    Box(18, 18, 64, 64, fill: false);
                    Lines(P(18, 50), P(50, 18));
                    Lines(P(18, 82), P(82, 18));
                    Lines(P(50, 82), P(82, 50));
                    break;
                case IconKind.Gear:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        _p.lineWidth = 14f * _s;
                        _p.lineCap = LineCap.Butt;
                        Lines(P(50 + d.x * 24, 50 + d.y * 24), P(50 + d.x * 40, 50 + d.y * 40));
                    }
                    _p.lineWidth = 11f * _s;
                    Circle(50, 50, 24, fill: false);
                    break;
                case IconKind.Play:
                    Poly(true, P(30, 18), P(84, 50), P(30, 82));
                    break;
                case IconKind.Share:
                    Lines(P(70, 26), P(30, 50), P(70, 74));
                    Circle(72, 24, 13, fill: true);
                    Circle(28, 50, 13, fill: true);
                    Circle(72, 76, 13, fill: true);
                    break;
                case IconKind.CopyRight:
                    Box(8, 30, 40, 40, fill: true);
                    Box(54, 30, 38, 38, fill: false);
                    break;
                case IconKind.CopyUp:
                    Box(30, 54, 40, 40, fill: true);
                    Box(31, 8, 38, 38, fill: false);
                    break;
                case IconKind.Building:
                    Box(30, 10, 40, 80, fill: false);
                    Lines(P(30, 37), P(70, 37));
                    Lines(P(30, 63), P(70, 63));
                    _p.lineWidth = 5f * _s;
                    Lines(P(14, 10), P(14, 90));
                    Lines(P(86, 10), P(86, 90));
                    break;
                case IconKind.Flip:
                    Poly(true, P(42, 20), P(42, 80), P(10, 80));
                    Poly(false, P(58, 20), P(58, 80), P(90, 80));
                    _p.lineWidth = 4f * _s;
                    for (int y = 10; y < 90; y += 16) Lines(P(50, y), P(50, y + 8));
                    break;
                case IconKind.Front:
                    Box(12, 12, 46, 46, fill: false);
                    Box(40, 40, 48, 48, fill: true);
                    break;
                case IconKind.Behind:
                    Box(12, 12, 46, 46, fill: true);
                    Box(40, 40, 48, 48, fill: false);
                    break;
                case IconKind.Solid:
                    Box(16, 16, 68, 68, fill: true);
                    break;
                case IconKind.Backdrop:
                    _p.lineWidth = 7f * _s;
                    for (int i = 0; i < 4; i++)
                    {
                        float a = 16f + i * 18f;
                        Lines(P(a, 16), P(a + 9, 16));
                        Lines(P(a, 84), P(a + 9, 84));
                        Lines(P(16, a), P(16, a + 9));
                        Lines(P(84, a), P(84, a + 9));
                    }
                    break;
                case IconKind.Reset:
                    _p.BeginPath();
                    _p.Arc(P(50, 54), 30f * _s, -60f, 220f);
                    _p.Stroke();
                    Lines(P(26, 20), P(29, 37), P(46, 34));
                    break;
                case IconKind.Trash:
                    Lines(P(20, 26), P(80, 26));
                    Lines(P(40, 14), P(60, 14));
                    Poly(false, P(28, 34), P(72, 34), P(66, 88), P(34, 88));
                    Lines(P(44, 44), P(44, 78));
                    Lines(P(56, 44), P(56, 78));
                    break;
                case IconKind.RunLeft:
                case IconKind.RunRight:
                {
                    bool right = _kind == IconKind.RunRight;
                    float X(float x) => right ? x : 100f - x;
                    Lines(P(X(14), 64), P(X(80), 64));
                    Lines(P(X(62), 46), P(X(82), 64), P(X(62), 82));
                    _p.lineWidth = 6f * _s;
                    Lines(P(X(14), 80), P(X(46), 80));
                    break;
                }
                case IconKind.Plus:
                    Lines(P(50, 18), P(50, 82));
                    Lines(P(18, 50), P(82, 50));
                    break;
                case IconKind.Start:
                    Box(8, 66, 70, 14, fill: true);
                    Lines(P(52, 40), P(88, 40));
                    Lines(P(74, 26), P(89, 40), P(74, 54));
                    break;
                case IconKind.Target:
                case IconKind.Platform:
                    Box(10, 80, 80, 12, fill: true);
                    Lines(P(36, 78), P(36, 12));
                    Poly(true, P(40, 12), P(82, 28), P(40, 44));
                    break;
            }
        }

        void Lines(params Vector2[] pts)
        {
            _p.BeginPath();
            _p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) _p.LineTo(pts[i]);
            _p.Stroke();
        }

        void Poly(bool fill, params Vector2[] pts)
        {
            _p.BeginPath();
            _p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) _p.LineTo(pts[i]);
            _p.ClosePath();
            if (fill) _p.Fill();
            else _p.Stroke();
        }

        void Box(float x, float y, float w, float h, bool fill) => Poly(fill, P(x, y), P(x + w, y), P(x + w, y + h), P(x, y + h));

        void Circle(float x, float y, float r, bool fill)
        {
            _p.BeginPath();
            _p.Arc(P(x, y), r * _s, 0f, 360f);
            if (fill) _p.Fill();
            else _p.Stroke();
        }
    }
}
