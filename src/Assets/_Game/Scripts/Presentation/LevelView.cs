using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>
    /// Рисует уровень: здания (из кусков фасада), улицу с деревьями и фонарями, фон с параллаксом (небо, силуэт города,
    /// дальние дома, облака по ветру) и красную стрелку цели. Ещё ставит коллайдеры для ragdoll.
    /// </summary>
    public sealed class LevelView
    {
        sealed class ParallaxLayer
        {
            public Transform T;
            public Vector2 Base;
            public Vector2 Factor;
        }

        sealed class Cloud
        {
            public Transform T;
            public float X, Y, Speed, Factor, Wrap;
        }

        static readonly Color SkylineTint = new Color(0.72f, 0.8f, 0.9f, 1f);
        // Far city sits in haze: pale and see-through, so it never reads as a real obstacle.
        static readonly Color CityFarTint = new Color(0.78f, 0.86f, 0.98f, 0.33f);
        static readonly Color Asphalt = new Color(0.22f, 0.23f, 0.26f);
        static readonly Color AsphaltDeep = new Color(0.14f, 0.14f, 0.16f);
        static readonly Color Sidewalk = new Color(0.62f, 0.6f, 0.57f);
        static readonly Color LaneMark = new Color(0.92f, 0.9f, 0.82f);
        static readonly Color TargetRed = new Color(0.92f, 0.14f, 0.12f);

        static readonly BuildingStyle[] FarStyles = { BuildingStyle.Office, BuildingStyle.Glass, BuildingStyle.Beige };

        readonly SpriteKit _kit;
        readonly ArtBank _art;
        readonly LevelLayout _layout;
        readonly Transform _root;
        readonly List<ParallaxLayer> _parallax = new List<ParallaxLayer>();
        readonly List<Cloud> _clouds = new List<Cloud>();
        Transform _sky;
        Transform _arrow;
        float _arrowBaseY;

        public Transform Root => _root;

        public LevelView(Transform parent, LevelConfig level, LevelLayout layout, ArtBank art)
        {
            _art = art;
            _layout = layout;
            _kit = new SpriteKit(art.SpriteMaterial, art.White);
            _root = new GameObject("Level").transform;
            _root.SetParent(parent, false);

            BuildSky();
            BuildSkyline();
            BuildFarCity(level);
            BuildClouds();
            BuildStreet();
            BuildBuilding("Start Building", level.Start);
            BuildBuilding("Target Building", level.Target);
            if (level.Others != null)
                for (int i = 0; i < level.Others.Length; i++) BuildBuilding($"Building {i + 1}", level.Others[i]);
            BuildDecor(level);
            BuildTarget();
            BuildColliders();
        }

        /// <summary>Стрелку прячем, когда стикмен уже сел (чтобы не налезала на купол).</summary>
        public void SetArrowVisible(bool visible)
        {
            if (_arrow != null && _arrow.gameObject.activeSelf != visible) _arrow.gameObject.SetActive(visible);
        }

        /// <summary>Параллакс, облака, покачивание стрелки. Вызывать после движения камеры.</summary>
        public void Update(Camera cam, float time, float dt)
        {
            Vector2 c = cam.transform.position;
            foreach (var p in _parallax)
                p.T.localPosition = new Vector3(p.Base.x + c.x * p.Factor.x, p.Base.y + c.y * p.Factor.y, 0f);

            foreach (var cl in _clouds)
            {
                cl.X += cl.Speed * dt;
                float x = cl.X + c.x * cl.Factor;
                float rel = Mathf.Repeat(x - c.x + cl.Wrap * 0.5f, cl.Wrap) - cl.Wrap * 0.5f;
                cl.T.localPosition = new Vector3(c.x + rel, cl.Y + c.y * cl.Factor * 0.5f, 0f);
            }

            if (_sky != null)
            {
                float h = cam.orthographicSize * 2f;
                float w = h * cam.aspect;
                _sky.position = new Vector3(c.x, c.y, 0f);
                // Cover any aspect (offscreen captures can differ from the screen).
                _sky.localScale = new Vector3(Mathf.Max(w, h * 2.6f) * 1.1f, h * 1.1f, 1f);
            }

            if (_arrow != null)
                _arrow.localPosition = new Vector3(_layout.ArrowX, _arrowBaseY + Mathf.Abs(Mathf.Sin(time * 3.2f)) * 0.45f, 0f);
        }

        // ------------------------------------------------------------------ background

        void BuildSky()
        {
            var sprite = _art.SkyGradient != null ? _art.SkyGradient : _art.White;
            var r = _kit.Make("Sky", _root, sprite, SortOrder.Sky, _art.SkyGradient != null ? Color.white : new Color(0.55f, 0.75f, 0.95f));
            // Sky sprite is normalised to 1×1 unit with a centred pivot, so scale = view size.
            var b = sprite.bounds.size;
            r.transform.localScale = new Vector3(1f / b.x, 1f / b.y, 1f);
            var holder = new GameObject("SkyHolder").transform;
            holder.SetParent(_root, false);
            r.transform.SetParent(holder, false);
            _sky = holder;
        }

        void BuildSkyline()
        {
            if (_art.Skyline == null) return;
            var layer = new GameObject("Skyline").transform;
            layer.SetParent(_root, false);
            var r = _kit.Make("Silhouette", layer, _art.Skyline, SortOrder.Skyline, SkylineTint);
            r.drawMode = SpriteDrawMode.Tiled;
            Vector2 tile = _art.Skyline.bounds.size;
            r.size = new Vector2(tile.x * 14f, tile.y);
            r.transform.localScale = new Vector3(4f, 4f, 1f);
            SpriteKit.PlaceBottomCenter(r, 0f, -1f);
            _parallax.Add(new ParallaxLayer { T = layer, Base = new Vector2(_layout.EdgeX * 0.1f, 0f), Factor = new Vector2(0.9f, 0f) });
        }

        void BuildFarCity(LevelConfig level)
        {
            var layer = new GameObject("CityFar").transform;
            layer.SetParent(_root, false);
            var rng = new System.Random(level.Id.GetHashCode());
            float x = _layout.Overview.xMin - 80f;
            float end = _layout.Overview.xMax + 80f;
            int i = 0;
            while (x < end)
            {
                float w = 6f + (float)rng.NextDouble() * 6f;
                float h = 12f + (float)rng.NextDouble() * 26f;
                var style = FarStyles[rng.Next(FarStyles.Length)];
                DrawFacade($"Far {i++}", layer, new Rect(x, 0f, w, h), _art.Building(style), CityFarTint, SortOrder.CityFar);
                x += w + 1f + (float)rng.NextDouble() * 3f;
            }
            _parallax.Add(new ParallaxLayer { T = layer, Base = Vector2.zero, Factor = new Vector2(0.45f, 0f) });
        }

        void BuildClouds()
        {
            if (_art.Clouds == null || _art.Clouds.Length == 0) return;
            var layer = new GameObject("Clouds").transform;
            layer.SetParent(_root, false);
            var rng = new System.Random(7);
            for (int i = 0; i < 9; i++)
            {
                var sprite = _art.Clouds[i % _art.Clouds.Length];
                if (sprite == null) continue;
                var r = _kit.Make($"Cloud {i}", layer, sprite, SortOrder.Clouds, new Color(1f, 1f, 1f, 0.92f));
                float s = 1.2f + (float)rng.NextDouble() * 1.2f;
                r.transform.localScale = new Vector3(s, s, 1f);
                _clouds.Add(new Cloud
                {
                    T = r.transform,
                    X = (float)rng.NextDouble() * 160f - 60f,
                    Y = 22f + (float)rng.NextDouble() * 70f,
                    Speed = _layout.Wind * (0.4f + (float)rng.NextDouble() * 0.4f) + 0.3f,
                    Factor = 0.55f + (float)rng.NextDouble() * 0.2f,
                    Wrap = 170f,
                });
            }
        }

        // ------------------------------------------------------------------ street & buildings

        void BuildStreet()
        {
            var street = new GameObject("Street").transform;
            street.SetParent(_root, false);
            float x0 = _layout.Overview.xMin - 120f, x1 = _layout.Overview.xMax + 120f;
            _kit.Box("Asphalt", street, Rect.MinMaxRect(x0, -1.6f, x1, -0.35f), Asphalt, SortOrder.Street);
            _kit.Box("Underground", street, Rect.MinMaxRect(x0, -40f, x1, -1.6f), AsphaltDeep, SortOrder.Street);
            _kit.Box("Sidewalk", street, Rect.MinMaxRect(x0, -0.35f, x1, 0f), Sidewalk, SortOrder.Street + 1);
            for (float x = x0; x < x1; x += 4f)
                _kit.Box("Lane", street, new Rect(x, -1.0f, 1.8f, 0.09f), LaneMark, SortOrder.Street + 1);
        }

        void BuildBuilding(string name, BuildingSpec spec)
        {
            var go = new GameObject(name).transform;
            go.SetParent(_root, false);
            var rect = new Rect(spec.X, 0f, spec.Width, spec.Height);
            DrawFacade("Facade", go, rect, _art.Building(spec.Style), Color.white, SortOrder.Buildings);
        }

        /// <summary>Фасад: первый этаж + плитка этажей + бортик крыши. Без картинок — сплошной цвет.</summary>
        void DrawFacade(string name, Transform parent, Rect rect, BuildingArt art, Color tint, int order)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            if (art.Body == null)
            {
                _kit.Box("Block", root, rect, art.Fallback * tint, order);
                _kit.Box("RoofLine", root, new Rect(rect.xMin - 0.1f, rect.yMax - 0.25f, rect.width + 0.2f, 0.25f), art.Fallback * tint * 0.7f, order + 2);
                return;
            }

            float groundH = 0f;
            if (art.Ground != null)
            {
                var g = _kit.FitWidth("GroundFloor", root, art.Ground, rect.center.x, rect.yMin, rect.width, order + 1, false);
                g.color = tint;
                groundH = art.Ground.bounds.size.y;
            }

            float bodyH = Mathf.Max(0.1f, rect.height - groundH);
            var body = _kit.Make("Floors", root, art.Body, order, tint);
            body.drawMode = SpriteDrawMode.Tiled;
            body.tileMode = SpriteTileMode.Continuous;
            Vector2 tile = art.Body.bounds.size;
            body.size = new Vector2(tile.x, bodyH);
            body.transform.localScale = new Vector3(rect.width / tile.x, 1f, 1f);
            SpriteKit.PlaceBottomCenter(body, rect.center.x, rect.yMin + groundH);

            if (art.Roof != null)
            {
                var roof = _kit.FitWidth("Roof", root, art.Roof, rect.center.x, rect.yMax - art.Roof.bounds.size.y, rect.width + 0.12f, order + 2, false);
                roof.color = tint;
            }
        }

        void BuildDecor(LevelConfig level)
        {
            var decor = new GameObject("Decor").transform;
            decor.SetParent(_root, false);
            var rng = new System.Random(level.Id.GetHashCode() ^ 0x5f3759df);
            float x = _layout.Overview.xMin - 60f;
            float end = _layout.Overview.xMax + 60f;
            int i = 0;
            while (x < end)
            {
                x += 2.5f + (float)rng.NextDouble() * 5f;
                if (InsideBuilding(x, 1.2f)) continue;
                int kind = rng.Next(0, 10);
                Sprite sprite;
                float scale;
                if (kind < 4 && _art.Trees.Length > 0) { sprite = _art.Trees[rng.Next(_art.Trees.Length)]; scale = 0.9f + (float)rng.NextDouble() * 0.4f; }
                else if (kind < 7 && _art.Bushes.Length > 0) { sprite = _art.Bushes[rng.Next(_art.Bushes.Length)]; scale = 1f; }
                else if (kind < 9) { sprite = _art.Lamp; scale = 1f; }
                else { sprite = _art.Hydrant; scale = 1f; }
                if (sprite == null) continue;
                var r = _kit.Make($"Decor {i++}", decor, sprite, SortOrder.Decor + (kind < 4 ? 0 : 1));
                r.transform.localScale = new Vector3(scale, scale, 1f);
                SpriteKit.PlaceBottomCenter(r, x, -0.05f);
            }
        }

        bool InsideBuilding(float x, float margin)
        {
            foreach (var s in _layout.Solids)
                if (s.Kind == SolidKind.Building && x > s.Rect.xMin - margin && x < s.Rect.xMax + margin) return true;
            return false;
        }

        void BuildTarget()
        {
            var t = _layout.TargetRect;
            var target = new GameObject("Target").transform;
            target.SetParent(_root, false);
            // Landing mark on the roof (own art): a red stripe under the arrow.
            _kit.Box("Mark", target, new Rect(_layout.ArrowX - 0.6f, t.yMax - 0.02f, 1.2f, 0.12f), TargetRed, SortOrder.Target);

            _arrowBaseY = t.yMax + 2.4f;
            if (_art.Arrow != null)
            {
                var r = _kit.Make("Arrow", target, _art.Arrow, SortOrder.Target + 1);
                float s = 1.4f / Mathf.Max(0.01f, _art.Arrow.bounds.size.x);
                r.transform.localScale = new Vector3(s, s, 1f);
                _arrow = r.transform;
            }
            else
            {
                var r = _kit.Make("Arrow", target, _art.Circle, SortOrder.Target + 1, TargetRed);
                r.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
                _arrow = r.transform;
            }
            _arrow.localPosition = new Vector3(_layout.ArrowX, _arrowBaseY, 0f);

            if (_art.Flag != null)
            {
                var f = _kit.Make("Flag", target, _art.Flag, SortOrder.Target);
                float s = 2.2f / Mathf.Max(0.01f, _art.Flag.bounds.size.y);
                f.transform.localScale = new Vector3(s, s, 1f);
                SpriteKit.PlaceBottomCenter(f, t.xMax - 0.9f, t.yMax);
            }
        }

        void BuildColliders()
        {
            var col = new GameObject("Colliders").transform;
            col.SetParent(_root, false);
            var mat = new PhysicsMaterial2D("Level") { friction = 0.7f, bounciness = 0.12f };
            foreach (var s in _layout.Solids)
            {
                var go = new GameObject(s.Kind == SolidKind.Ground ? "Ground" : "Building");
                go.transform.SetParent(col, false);
                go.transform.localPosition = s.Rect.center;
                var box = go.AddComponent<BoxCollider2D>();
                box.size = s.Rect.size;
                box.sharedMaterial = mat;
            }
        }
    }
}
