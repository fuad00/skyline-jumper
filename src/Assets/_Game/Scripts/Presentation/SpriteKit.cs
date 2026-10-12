using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>Порядок отрисовки (больше — ближе к зрителю).</summary>
    public static class SortOrder
    {
        public const int Sky = -100;
        public const int Clouds = -90;
        public const int Skyline = -80;
        public const int CityFar = -70;
        public const int Buildings = 0;
        public const int Street = 10;
        public const int Decor = 20;
        public const int Target = 30;
        public const int Canopy = 45;
        public const int StickmanBack = 50;
        public const int StickmanBody = 52;
        public const int StickmanFront = 54;
        public const int StickmanHead = 56;
    }

    /// <summary>Мелкие помощники для спрайтов, чтобы все создавались одинаково (общий материал, порядок).</summary>
    public sealed class SpriteKit
    {
        readonly Material _material;
        readonly Sprite _white;

        public SpriteKit(Material material, Sprite white)
        {
            _material = material;
            _white = white;
        }

        public Sprite White => _white;

        public SpriteRenderer Make(string name, Transform parent, Sprite sprite, int order, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            if (_material != null) r.sharedMaterial = _material;
            r.sortingOrder = order;
            if (color.HasValue) r.color = color.Value;
            return r;
        }

        /// <summary>Сплошной прямоугольник (своя замена, если картинки нет).</summary>
        public SpriteRenderer Box(string name, Transform parent, Rect rect, Color color, int order)
        {
            var r = Make(name, parent, _white, order, color);
            Vector2 unit = _white != null ? (Vector2)_white.bounds.size : Vector2.one;
            r.transform.localScale = new Vector3(rect.width / unit.x, rect.height / unit.y, 1f);
            r.transform.localPosition = rect.center;
            return r;
        }

        /// <summary>Спрайт с опорой внизу по центру, растянутый по ширине до width (высота пропорционально, если keepAspect).</summary>
        public SpriteRenderer FitWidth(string name, Transform parent, Sprite sprite, float centerX, float bottomY, float width, int order, bool keepAspect)
        {
            var r = Make(name, parent, sprite, order);
            Vector2 size = sprite.bounds.size;
            float sx = width / Mathf.Max(0.001f, size.x);
            r.transform.localScale = new Vector3(sx, keepAspect ? sx : 1f, 1f);
            PlaceBottomCenter(r, centerX, bottomY);
            return r;
        }

        /// <summary>Ставит спрайт так, чтобы низ его картинки был на bottomY, а центр по X — на centerX (независимо от опоры спрайта).</summary>
        public static void PlaceBottomCenter(SpriteRenderer r, float centerX, float bottomY)
        {
            var b = r.sprite.bounds; // local, unscaled
            Vector3 s = r.transform.localScale;
            float localCenterX = b.center.x * s.x;
            float localBottom = b.min.y * s.y;
            r.transform.localPosition = new Vector3(centerX - localCenterX, bottomY - localBottom, 0f);
        }
    }
}
