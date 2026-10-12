using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Maps;
using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>
    /// Картинка редактора карт: лист в клетку (клетка — ровно 1 м, при отдалении — 5 и 25 м), улица, детали карты,
    /// площадки (старт — синяя полоса со стрелкой прыжка, цель — красная с флажком, промежуточные — оранжевые),
    /// рамка выбранного и ручки. По желанию — красным, что твёрдое (где разобьёшься и куда можно сесть).
    /// </summary>
    public sealed class MapEditorView
    {
        static readonly Color StartBlue = new Color(0.16f, 0.55f, 0.96f, 0.95f);
        static readonly Color TargetRed = new Color(0.9f, 0.12f, 0.12f, 0.95f);
        static readonly Color PlatformOrange = new Color(1f, 0.55f, 0.1f, 0.95f);
        static readonly Color SolidTint = new Color(1f, 0.15f, 0.1f, 0.28f);
        static readonly Color SelectBlue = new Color(0.12f, 0.6f, 1f, 1f);
        static readonly Color NightDim = new Color(0.62f, 0.62f, 0.68f, 1f);

        const int OrderStrips = SortOrder.Target, OrderSolids = SortOrder.Target - 2, OrderOutline = SortOrder.StickmanHead + 4, OrderHandles = OrderOutline + 2;
        const float FlagHeight = 2.6f, StripThickness = 0.28f;
        /// <summary>Размеры на экране (в точках интерфейса 1920 × 1080): рамка, ручки.</summary>
        public const float OutlinePx = 4f, HandlePx = 30f, RotateGapPx = 70f, StripPx = 8f, FlagPx = 56f, ArrowPx = 46f;

        readonly SpriteKit _kit;
        readonly ArtBank _art;
        readonly PieceCatalog _catalog;
        readonly Transform _root, _pieceRoot, _stripRoot, _solidRoot, _overlay;
        readonly List<SpriteRenderer> _pieces = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _solidBoxes = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _outline = new List<SpriteRenderer>();
        readonly List<(SpriteRenderer ring, SpriteRenderer dot)> _handles = new List<(SpriteRenderer, SpriteRenderer)>();
        SpriteRenderer _rotateStem;
        SpriteRenderer _paper;
        Transform _paperLayer;
        SpriteRenderer _curb, _asphalt, _under;
        bool _night;
        MapData _map;

        public bool ShowSolids { get; set; }
        public Transform Root => _root;

        public MapEditorView(Transform parent, ArtBank art)
        {
            _art = art;
            _catalog = art.Pieces;
            _kit = new SpriteKit(art.SpriteMaterial, art.White);
            _root = new GameObject("Map Editor").transform;
            _root.SetParent(parent, false);
            _paperLayer = new GameObject("Paper").transform;
            _paperLayer.SetParent(_root, false);
            if (art.Paper != null)
            {
                _paper = _kit.Make("Sheet", _paperLayer, art.Paper, SortOrder.Sky);
                _paper.drawMode = SpriteDrawMode.Tiled;
            }
            var street = new GameObject("Street").transform;
            street.SetParent(_root, false);
            _curb = _kit.Box("Curb", street, Rect.MinMaxRect(-3000f, -0.4f, 6000f, 0f), new Color(0.62f, 0.62f, 0.62f), SortOrder.Street + 1);
            _asphalt = _kit.Box("Asphalt", street, Rect.MinMaxRect(-3000f, -1.2f, 6000f, -0.4f), new Color(0.36f, 0.36f, 0.38f), SortOrder.Street);
            _under = _kit.Box("Underground", street, Rect.MinMaxRect(-3000f, -400f, 6000f, -1.2f), new Color(0.88f, 0.88f, 0.86f), SortOrder.Street - 1);
            _pieceRoot = Child("Pieces");
            _solidRoot = Child("Solids");
            _stripRoot = Child("Strips");
            _overlay = Child("Selection");
        }

        Transform Child(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_root, false);
            return t;
        }

        public void SetActive(bool active) => _root.gameObject.SetActive(active);

        public void Destroy() => Object.Destroy(_root.gameObject);

        /// <summary>Привести картинку к карте (дёшево: вызывать после каждой правки и во время перетаскивания).</summary>
        public void Sync(MapEditor editor, Camera cam, float metresPerPoint)
        {
            var map = editor.Map;
            _map = map;
            SetNight(map.Night);
            SyncPieces(editor);
            SyncSolids(map);
            UpdateCamera(editor, cam, metresPerPoint);
        }

        void SetNight(bool night)
        {
            _night = night;
            if (_paper != null) _paper.enabled = !night;
            _curb.color = night ? new Color(0.22f, 0.22f, 0.24f) : new Color(0.62f, 0.62f, 0.62f);
            _asphalt.color = night ? new Color(0.12f, 0.12f, 0.13f) : new Color(0.36f, 0.36f, 0.38f);
            _under.color = night ? new Color(0.03f, 0.03f, 0.04f) : new Color(0.88f, 0.88f, 0.86f);
        }

        void SyncPieces(MapEditor editor)
        {
            var map = editor.Map;
            while (_pieces.Count < map.Pieces.Count) _pieces.Add(_kit.Make("Piece", _pieceRoot, null, SortOrder.Buildings));
            for (int i = map.Pieces.Count; i < _pieces.Count; i++) _pieces[i].gameObject.SetActive(false);
            var order = editor.DrawOrder();
            for (int k = 0; k < order.Count; k++)
            {
                int i = order[k];
                var p = map.Pieces[i];
                var r = _pieces[i];
                bool known = _catalog != null && _catalog.TryGet(p.Art, out var def);
                r.gameObject.SetActive(known);
                if (!known) continue;
                _catalog.TryGet(p.Art, out def);
                if (r.sprite != def.Sprite) r.sprite = def.Sprite;
                r.sortingOrder = SortOrder.Buildings + Mathf.Clamp(p.Layer, -5, 9);
                r.transform.localPosition = new Vector3(p.X, p.Y, -k * 0.0005f); // equal layers: later on top
                r.transform.localRotation = Quaternion.Euler(0f, 0f, p.Rotation);
                r.transform.localScale = new Vector3(p.ScaleX, p.ScaleY, 1f);
                r.color = _night && !def.AlwaysLit ? NightDim : Color.white;
                r.name = p.Art;
            }
        }

        // ------------------------------------------------------------------ strips

        readonly List<(SpriteRenderer band, SpriteRenderer flag, SpriteRenderer arrow)> _strips = new List<(SpriteRenderer, SpriteRenderer, SpriteRenderer)>();

        /// <summary>Площадки: полоса и флажок не мельче нескольких точек экрана — их видно и издалека.</summary>
        void DrawStrips(MapData map, float px)
        {
            int need = 2 + map.Platforms.Count;
            while (_strips.Count < need)
            {
                var band = _kit.Make("Band", _stripRoot, _art.White, OrderStrips);
                var flag = _kit.Make("Flag", _stripRoot, _art.Flag != null ? _art.Flag : _art.White, OrderStrips + 1);
                var arrow = _kit.Make("Arrow", _stripRoot, _art.IconWind != null ? _art.IconWind : _art.White, OrderStrips + 1);
                _strips.Add((band, flag, arrow));
            }
            for (int i = 0; i < _strips.Count; i++)
            {
                bool on = i < need;
                _strips[i].band.gameObject.SetActive(on);
                _strips[i].flag.gameObject.SetActive(on);
                _strips[i].arrow.gameObject.SetActive(on);
            }
            DrawStrip(_strips[0], map.Start, StartBlue, flag: false, arrowDir: 1, px);
            DrawStrip(_strips[1], map.Target, TargetRed, flag: true, arrowDir: 0, px);
            for (int i = 0; i < map.Platforms.Count; i++)
                DrawStrip(_strips[2 + i], map.Platforms[i], PlatformOrange, flag: true, arrowDir: map.Platforms[i].Left ? -1 : 1, px, small: true);
        }

        void DrawStrip((SpriteRenderer band, SpriteRenderer flag, SpriteRenderer arrow) v, MapStrip s, Color color, bool flag, int arrowDir, float px, bool small = false)
        {
            float thick = Mathf.Max(StripThickness, StripPx * px);
            Place(v.band, new Rect(s.X, s.Y - thick, Mathf.Max(0.05f, s.Width), thick));
            v.band.color = color;

            v.flag.gameObject.SetActive(flag);
            if (flag)
            {
                float h = Mathf.Max(FlagHeight, FlagPx * px) * (small ? 0.8f : 1f);
                if (_art.Flag != null)
                {
                    float k = h / Mathf.Max(0.01f, _art.Flag.bounds.size.y);
                    v.flag.transform.localScale = new Vector3(k, k, 1f);
                    v.flag.transform.localPosition = new Vector3(s.CenterX, s.Y, 0f); // the flag's pivot is the foot of the pole
                    v.flag.color = small ? new Color(1f, 0.8f, 0.55f) : Color.white;
                }
                else Place(v.flag, new Rect(s.CenterX - 0.05f, s.Y, 0.1f, h));
            }

            v.arrow.gameObject.SetActive(arrowDir != 0);
            if (arrowDir != 0)
            {
                // At the jump edge, pointing where he jumps.
                float edge = arrowDir > 0 ? s.XMax : s.X;
                var a = v.arrow;
                float h = Mathf.Max(1.4f, ArrowPx * px);
                float size = h / Mathf.Max(0.01f, a.sprite.bounds.size.y);
                a.transform.localScale = new Vector3(size * arrowDir, size, 1f);
                a.transform.localRotation = Quaternion.identity;
                a.transform.localPosition = new Vector3(edge - arrowDir * h * 0.7f, s.Y + h * 0.75f, 0f);
                a.color = color;
            }
        }

        void Place(SpriteRenderer r, Rect rect)
        {
            Vector2 unit = r.sprite != null ? (Vector2)r.sprite.bounds.size : Vector2.one;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = new Vector3(rect.width / unit.x, rect.height / unit.y, 1f);
            r.transform.localPosition = rect.center;
        }

        // ------------------------------------------------------------------ solids

        void SyncSolids(MapData map)
        {
            _solidRoot.gameObject.SetActive(ShowSolids);
            if (!ShowSolids) return;
            var solids = MapGeometry.Solids(map, _catalog);
            while (_solidBoxes.Count < solids.Count) _solidBoxes.Add(_kit.Make("Solid", _solidRoot, _art.White, OrderSolids, SolidTint));
            for (int i = 0; i < _solidBoxes.Count; i++)
            {
                bool on = i < solids.Count;
                _solidBoxes[i].gameObject.SetActive(on);
                if (on) Place(_solidBoxes[i], solids[i]);
            }
        }

        // ------------------------------------------------------------------ camera-dependent: paper, outline, handles

        /// <param name="metresPerPoint">Сколько метров в одной точке интерфейса (рамки и ручки — одного размера на экране).</param>
        public void UpdateCamera(MapEditor editor, Camera cam, float metresPerPoint)
        {
            UpdatePaper(cam);
            float px = metresPerPoint;
            if (_map != null) DrawStrips(_map, px);
            DrawOutline(editor, px);
            DrawHandles(editor, px);
        }

        /// <summary>Лист привязан к миру: клетка = 1 м (издалека — 5 или 25 м), по нему удобно мерить.</summary>
        void UpdatePaper(Camera cam)
        {
            if (_paper == null || !_paper.enabled) return;
            Vector2 tile = _art.Paper.bounds.size;
            const float cellsPerTile = 51.2f;
            float viewH = cam.orthographicSize * 2f, viewW = viewH * cam.aspect;
            float cell = 1f;
            while (cell * 1080f / viewH < 14f && cell < 600f) cell *= 5f; // a cell at least ~14 points on screen
            float s = cell * cellsPerTile / tile.x;
            _paperLayer.localScale = new Vector3(s, s, 1f);
            float tw = tile.x * s, th = tile.y * s;
            _paper.size = new Vector2(tile.x * (Mathf.Ceil(viewW / tw) + 2f), tile.y * (Mathf.Ceil(viewH / th) + 2f));
            Vector2 c = cam.transform.position;
            // Whole tiles from the world origin: the grid lines stay on whole metres.
            _paperLayer.localPosition = new Vector3(Mathf.Round(c.x / tw) * tw, Mathf.Round(c.y / th) * th, 0f);
        }

        void DrawOutline(MapEditor editor, float px)
        {
            var pts = new List<Vector2>();
            var piece = editor.SelectedPiece;
            if (piece != null && _catalog != null && _catalog.TryGet(piece.Art, out var def))
            {
                var o = def.OpaquePx;
                foreach (var c in new[] { o.min, new Vector2(o.xMax, o.yMin), o.max, new Vector2(o.xMin, o.yMax) })
                    pts.Add(Gameplay.PictureShapes.ToWorld(c, def.SizePx, piece.Position, piece.Scale, piece.Rotation));
            }
            else if (editor.HasSelection)
            {
                var r = editor.SelectionBounds();
                pts.AddRange(new[] { r.min, new Vector2(r.xMax, r.yMin), r.max, new Vector2(r.xMin, r.yMax) });
            }
            // Two lines per edge: a white one under a blue one — visible on any background.
            while (_outline.Count < 8) _outline.Add(_kit.Make("Edge", _overlay, _art.White, OrderOutline + (_outline.Count >= 4 ? 1 : 0)));
            for (int i = 0; i < 8; i++)
            {
                var e = _outline[i];
                bool on = pts.Count == 4;
                e.gameObject.SetActive(on);
                if (!on) continue;
                bool top = i >= 4;
                Line(e, pts[i % 4], pts[(i + 1) % 4], (top ? OutlinePx : OutlinePx * 2.2f) * px, top ? SelectBlue : Color.white);
            }
        }

        void DrawHandles(MapEditor editor, float px)
        {
            var handles = editor.Handles(RotateGapPx * px);
            while (_handles.Count < handles.Count)
            {
                var ring = _kit.Make("Handle", _overlay, _art.Circle != null ? _art.Circle : _art.White, OrderHandles, Color.white);
                var dot = _kit.Make("Dot", _overlay, _art.Circle != null ? _art.Circle : _art.White, OrderHandles + 1);
                _handles.Add((ring, dot));
            }
            if (_rotateStem == null) _rotateStem = _kit.Make("Stem", _overlay, _art.White, OrderHandles - 1, Color.white);
            _rotateStem.gameObject.SetActive(false);
            for (int i = 0; i < _handles.Count; i++)
            {
                bool on = i < handles.Count;
                _handles[i].ring.gameObject.SetActive(on);
                _handles[i].dot.gameObject.SetActive(on);
                if (!on) continue;
                var (h, at) = handles[i];
                Disc(_handles[i].ring, at, HandlePx * px);
                Disc(_handles[i].dot, at, HandlePx * 0.68f * px);
                _handles[i].dot.color = h == Handle.Rotate ? new Color(0.3f, 0.75f, 0.3f) : h == Handle.Scale ? new Color(0.1f, 0.35f, 0.85f) : SelectBlue;
                if (h == Handle.Rotate)
                {
                    var top = handles.Find(x => x.handle == Handle.Height).at;
                    _rotateStem.gameObject.SetActive(true);
                    Line(_rotateStem, top, at, OutlinePx * px, Color.white);
                }
            }
        }

        void Disc(SpriteRenderer r, Vector2 at, float diameter)
        {
            float k = diameter / Mathf.Max(0.001f, r.sprite.bounds.size.x);
            r.transform.localScale = new Vector3(k, k, 1f);
            r.transform.localRotation = Quaternion.identity;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
        }

        static void Line(SpriteRenderer r, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = b - a;
            Vector2 unit = r.sprite != null ? (Vector2)r.sprite.bounds.size : Vector2.one;
            r.transform.localPosition = (Vector3)((a + b) * 0.5f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3((d.magnitude + width) / unit.x, width / unit.y, 1f);
            r.color = color;
        }
    }
}
