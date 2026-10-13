using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>
    /// Рисует уровень в стиле оригинала: тетрадный лист в клетку, серый силуэт города, здания из кусков
    /// фасада, улица (тумана перед домами у оригинала нет). Плюс подсказки для игрока: линейка на крыше (метры от края), зелёный флажок — откуда бежал в
    /// прошлый раз, красный флажок цели и пунктир от него вверх во время прыжка. Ставит коллайдеры для ragdoll.
    /// </summary>
    public sealed class LevelView
    {
        sealed class ParallaxLayer
        {
            public Transform T;
            public Vector2 Base;
            public Vector2 Factor;
            public float SnapX, SnapY; // >0: endless tiled layer, re-centred on the camera by whole tiles
        }

        sealed class Cloud
        {
            public Transform T;
            public float X, Y, Speed, Factor, Wrap;
        }

        static readonly Color PaperFallback = new Color(0.93f, 0.92f, 0.88f);
        // The silhouette texture is already see-through; only darken it, like the grey city on the original's paper.
        static readonly Color SkylineTint = new Color(0.42f, 0.42f, 0.45f, 1f);
        static readonly Color Asphalt = new Color(0.36f, 0.36f, 0.38f);
        // Below the street: the camera never goes there (like the original); this only hides pictures that reach under it.
        static readonly Color AsphaltDeep = new Color(0.3f, 0.3f, 0.32f);
        static readonly Color Curb = new Color(0.62f, 0.62f, 0.62f);
        static readonly Color MarkerInk = new Color(0.1f, 0.1f, 0.12f, 1f); // the original's markers are dark on the paper
        const int MarkerStep = 5;
        static readonly Color TargetRed = new Color(0.86f, 0.1f, 0.1f);

        const float FlagHeight = 2.6f;
        // Signal posts at the target's edges, like the original: 35 of its physics units tall (≈1.6 m), 3 units in from each edge.
        const float SignalHeight = 1.6f;
        const float SignalInset = 0.14f;
        static readonly Color SignalPost = new Color(0.55f, 0.55f, 0.57f);
        static readonly Color SignalDark = new Color(0.25f, 0.25f, 0.27f);
        static readonly Color SignalGreen = new Color(0.2f, 0.9f, 0.25f);
        // The original shows ≈36 grid cells across the screen; one tile of the paper has ≈51.
        const float PaperTilesAcross = 36f / 51.2f;
        const float PaperDrift = 0.12f;
        const float DecorScale = 0.75f;

        readonly SpriteKit _kit;
        readonly ArtBank _art;
        readonly LevelLayout _layout;
        readonly Transform _root;
        readonly List<ParallaxLayer> _parallax = new List<ParallaxLayer>();
        readonly List<Cloud> _clouds = new List<Cloud>();
        SpriteRenderer _paper, _guide, _lastFlag;
        /// <summary>Сигнальные столбики у краёв цели: картинка столбика и (без картинок оригинала) отдельная лампа.</summary>
        readonly List<(SpriteRenderer post, SpriteRenderer lamp)> _signals = new List<(SpriteRenderer, SpriteRenderer)>();
        /// <summary>Как сейчас горят столбики у цели.</summary>
        public TargetLight Lights { get; private set; } = TargetLight.Off;
        /// <summary>Флажки промежуточных платформ (по номеру полосы; 0 — старт, без флажка).</summary>
        readonly Dictionary<int, SpriteRenderer> _platformFlags = new Dictionary<int, SpriteRenderer>();

        // Night (the original's «night only»): black sky; LightSprite pictures light up near the stickman and the lamps.
        readonly bool _night;
        readonly List<(SpriteRenderer r, Vector2 c, float a)> _litPictures = new List<(SpriteRenderer, Vector2, float)>();
        LevelScene.LightSource[] _lights = new LevelScene.LightSource[0];
        Vector2 _playerLight;
        const float PlayerLightFull = 10f, PlayerLightFade = 26f, NightFloor = 0.12f;
        static readonly Color NightInk = new Color(0.92f, 0.92f, 0.95f, 1f);
        /// <summary>Уровень ночной: небо чёрное, освещено только рядом со стикменом и фонарями.</summary>
        public bool Night => _night;
        Transform _paperLayer;
        float _flagTopY;

        public Transform Root => _root;

        public LevelView(Transform parent, LevelConfig level, LevelLayout layout, ArtBank art)
        {
            _art = art;
            _layout = layout;
            _night = level.Scene != null && level.Scene.Night;
            if (_night) _lights = level.Scene.Lights ?? new LevelScene.LightSource[0];
            _kit = new SpriteKit(art.SpriteMaterial, art.White);
            _root = new GameObject("Level").transform;
            _root.SetParent(parent, false);

            if (!_night)
            {
                BuildPaper();
                BuildSkyline();
            }
            BuildStreet();
            if (level.Scene != null)
            {
                BuildScene(level.Scene); // the original's level: its own buildings, cranes, trees and clouds
            }
            else
            {
                BuildClouds();
                BuildBuilding("Start Building", level.Start);
                BuildBuilding("Target Building", level.Target);
                if (level.Others != null)
                    for (int i = 0; i < level.Others.Length; i++) BuildBuilding($"Building {i + 1}", level.Others[i]);
                BuildDecor(level);
            }
            BuildRuler();
            BuildTarget();
            BuildPlatformFlags();
            BuildLastFlag();
            BuildColliders();
        }

        /// <summary>Пунктир от флажка цели вверх — показываем во время прыжка.</summary>
        public void SetGuideVisible(bool visible)
        {
            if (_guide != null && _guide.enabled != visible) _guide.enabled = visible;
        }

        /// <summary>Зелёный флажок там, откуда стартовал прошлый разбег (NaN — спрятать).</summary>
        public void SetLastRunFlag(float x)
        {
            if (_lastFlag == null) return;
            bool show = !float.IsNaN(x);
            _lastFlag.gameObject.SetActive(show);
            if (show) _lastFlag.transform.localPosition = new Vector3(x, _layout.RoofY, 0f);
        }

        /// <summary>Параллакс, облака, пунктир до верха кадра. Вызывать после движения камеры.</summary>
        public void Update(Camera cam, float time, float dt)
        {
            Vector2 c = cam.transform.position;
            float viewH = cam.orthographicSize * 2f;
            float viewW = viewH * Mathf.Max(cam.aspect, 2.4f);

            if (_night) UpdateNightLight();
            else UpdatePaper(c, viewH * cam.aspect, viewH);
            foreach (var p in _parallax)
            {
                Vector2 pos = p.Base + Vector2.Scale(c, p.Factor);
                if (p.SnapX > 0f) pos.x += Mathf.Round((c.x - pos.x) / p.SnapX) * p.SnapX;
                if (p.SnapY > 0f) pos.y += Mathf.Round((c.y - pos.y) / p.SnapY) * p.SnapY;
                p.T.localPosition = new Vector3(pos.x, pos.y, 0f);
            }

            foreach (var cl in _clouds)
            {
                cl.X += cl.Speed * dt;
                float x = cl.X + c.x * cl.Factor;
                float rel = Mathf.Repeat(x - c.x + cl.Wrap * 0.5f, cl.Wrap) - cl.Wrap * 0.5f;
                cl.T.localPosition = new Vector3(c.x + rel, cl.Y + c.y * cl.Factor * 0.4f, 0f);
            }

            if (_guide != null && _guide.enabled)
            {
                float top = c.y + viewH * 0.5f + 1f;
                float h = Mathf.Max(0.1f, top - _flagTopY);
                _guide.size = new Vector2(_guide.size.x, h);
                _guide.transform.localPosition = new Vector3(_layout.ArrowX, _flagTopY, 0f);
            }
        }

        // ------------------------------------------------------------------ background

        /// <summary>Тетрадный лист. Как в оригинале, клетка на экране всегда одного размера (лист — фон, а не часть мира).</summary>
        void BuildPaper()
        {
            _paperLayer = new GameObject("Paper").transform;
            _paperLayer.SetParent(_root, false);
            if (_art.Paper == null) return;
            _paper = _kit.Make("Sheet", _paperLayer, _art.Paper, SortOrder.Sky);
            _paper.drawMode = SpriteDrawMode.Tiled;
            _paper.size = (Vector2)_art.Paper.bounds.size * 3f;
        }

        void UpdatePaper(Vector2 cam, float viewW, float viewH)
        {
            if (_paper == null) return;
            Vector2 tile = _art.Paper.bounds.size;
            // One texture tile (≈51 cells) spans the screen width, like the original.
            float s = viewW / (tile.x * PaperTilesAcross);
            _paperLayer.localScale = new Vector3(s, s, 1f);
            // Drift slowly with the camera (deep background), re-centred by whole tiles so it never runs out.
            Vector2 drift = cam * PaperDrift;
            float tw = tile.x * s, th = tile.y * s;
            float x = cam.x - Mathf.Repeat(drift.x, tw);
            float y = cam.y - Mathf.Repeat(drift.y, th);
            _paperLayer.localPosition = new Vector3(x, y, 0f);
        }

        void BuildSkyline()
        {
            if (_art.Skyline == null) return;
            var layer = new GameObject("Skyline").transform;
            layer.SetParent(_root, false);
            var r = _kit.Make("Silhouette", layer, _art.Skyline, SortOrder.Skyline, SkylineTint);
            r.drawMode = SpriteDrawMode.Tiled;
            Vector2 tile = _art.Skyline.bounds.size;
            r.size = new Vector2(tile.x * 8f, tile.y);
            r.transform.localScale = new Vector3(2.6f, 2.6f, 1f);
            SpriteKit.PlaceBottomCenter(r, 0f, 0f);
            _parallax.Add(new ParallaxLayer { T = layer, Base = Vector2.zero, Factor = new Vector2(0.85f, 0.72f), SnapX = tile.x * 2.6f });
        }

        void BuildClouds()
        {
            if (_art.Clouds == null || _art.Clouds.Length == 0) return;
            var layer = new GameObject("Clouds").transform;
            layer.SetParent(_root, false);
            var rng = new System.Random(7);
            for (int i = 0; i < 6; i++)
            {
                var sprite = _art.Clouds[i % _art.Clouds.Length];
                if (sprite == null) continue;
                var r = _kit.Make($"Cloud {i}", layer, sprite, SortOrder.Clouds, new Color(1f, 1f, 1f, 0.85f));
                float s = 0.6f + (float)rng.NextDouble() * 0.5f;
                r.transform.localScale = new Vector3(s, s, 1f);
                _clouds.Add(new Cloud
                {
                    T = r.transform,
                    X = (float)rng.NextDouble() * 200f - 80f,
                    Y = 25f + (float)rng.NextDouble() * 55f,
                    Speed = _layout.Wind * (0.4f + (float)rng.NextDouble() * 0.4f) + 0.2f,
                    Factor = 0.7f + (float)rng.NextDouble() * 0.15f,
                    Wrap = 220f,
                });
            }
        }

        // ------------------------------------------------------------------ street & buildings

        void BuildStreet()
        {
            var street = new GameObject("Street").transform;
            street.SetParent(_root, false);
            float x0 = _layout.Overview.xMin - 150f, x1 = _layout.Overview.xMax + 150f;
            _kit.Box("Curb", street, Rect.MinMaxRect(x0, -0.4f, x1, 0f), _night ? new Color(0.22f, 0.22f, 0.24f) : Curb, SortOrder.Street + 1);
            _kit.Box("Asphalt", street, Rect.MinMaxRect(x0, -1.2f, x1, -0.4f), _night ? new Color(0.12f, 0.12f, 0.13f) : Asphalt, SortOrder.Street);
            _kit.Box("Underground", street, Rect.MinMaxRect(x0, -60f, x1, -1.2f), _night ? Color.black : AsphaltDeep, SortOrder.Underground);
        }

        /// <summary>
        /// Уровень оригинала — картинки как в его редакторе: слой из файла, при равном слое позже записанная — выше
        /// (небольшой сдвиг по глубине: ортокамера сортирует равные по расстоянию).
        /// </summary>
        void BuildScene(LevelScene scene)
        {
            var root = new GameObject("Scene").transform;
            root.SetParent(_root, false);
            var pictures = scene.Pictures;
            for (int i = 0; i < pictures.Length; i++)
            {
                var p = pictures[i];
                if (p.Sprite == null) continue;
                var r = _kit.Make(p.Sprite.name, root, p.Sprite, SortOrder.Buildings + Mathf.Clamp(p.Layer, -5, 9), new Color(1f, 1f, 1f, p.Alpha));
                r.transform.localPosition = new Vector3(p.Position.x, p.Position.y, -i * 0.0005f);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, p.Rotation);
                r.transform.localScale = new Vector3(p.Scale.x, p.Scale.y, 1f);
                if (_night && p.Kind == "LightSprite" && !p.AlwaysLit) _litPictures.Add((r, p.Position, p.Alpha));
            }
        }

        void BuildBuilding(string name, BuildingSpec spec)
        {
            var go = new GameObject(name).transform;
            go.SetParent(_root, false);
            DrawFacade("Facade", go, new Rect(spec.X, 0f, spec.Width, spec.Height), _art.Building(spec.Style), Color.white, SortOrder.Buildings);
        }

        /// <summary>Фасад: первый этаж + плитка этажей + бортик крыши. Без картинок — сплошной цвет.</summary>
        void DrawFacade(string name, Transform parent, Rect rect, BuildingArt art, Color tint, int order)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            if (art.Body == null)
            {
                _kit.Box("Block", root, rect, art.Fallback * tint, order);
                _kit.Box("RoofLine", root, new Rect(rect.xMin - 0.1f, rect.yMax - 0.3f, rect.width + 0.2f, 0.3f), art.Fallback * tint * 0.7f, order + 2);
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
                var roof = _kit.FitWidth("Roof", root, art.Roof, rect.center.x, rect.yMax - art.Roof.bounds.size.y, rect.width + 0.2f, order + 2, false);
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
                x += 3.5f + (float)rng.NextDouble() * 7f;
                if (InsideBuilding(x, 2f)) continue;
                int kind = rng.Next(0, 10);
                Sprite sprite;
                if (kind < 3 && _art.Trees.Length > 0) sprite = _art.Trees[rng.Next(_art.Trees.Length)];
                else if (kind < 6 && _art.Bushes.Length > 0) sprite = _art.Bushes[rng.Next(_art.Bushes.Length)];
                else if (kind < 8) sprite = _art.Lamp;
                else sprite = _art.Hydrant;
                if (sprite == null) continue;
                var r = _kit.Make($"Decor {i++}", decor, sprite, SortOrder.Decor + (kind < 3 ? 0 : 1));
                float s = DecorScale * (kind < 3 ? 0.9f + (float)rng.NextDouble() * 0.3f : 1f);
                r.transform.localScale = new Vector3(s, s, 1f);
                SpriteKit.PlaceBottomCenter(r, x, -0.05f);
            }
        }

        bool InsideBuilding(float x, float margin)
        {
            foreach (var s in _layout.Solids)
                if (s.Kind == SolidKind.Building && x > s.Rect.xMin - margin && x < s.Rect.xMax + margin) return true;
            return false;
        }

        // ------------------------------------------------------------------ player guides

        /// <summary>
        /// Метки метров над стартовой крышей, как в оригинале: каждые 5 м от края стоит число и стрелка вниз.
        /// Вместе с флажком «Last» видно, на сколько отойти больше или меньше.
        /// </summary>
        void BuildRuler()
        {
            var ruler = new GameObject("MeterMarkers").transform;
            ruler.SetParent(_root, false);
            // On every runway: the start roof and each intermediate platform (counted from its own edge, in its direction).
            foreach (var runway in _layout.Runways)
            {
                float edge = runway.EdgeX, back = runway.BackLimitX, roof = runway.RoofY;
                int dir = runway.Dir;
                for (int m = MarkerStep; dir * (edge - dir * m - back) >= -0.01f; m += MarkerStep)
                {
                    float x = edge - dir * m;
                    float arrowTop = roof + 1.35f;
                    if (_art.MarkerArrow != null)
                    {
                        var a = _kit.Make("Arrow", ruler, _art.MarkerArrow, SortOrder.Ruler, _night ? NightInk : MarkerInk);
                        float s = 0.75f / Mathf.Max(0.01f, _art.MarkerArrow.bounds.size.x);
                        a.transform.localScale = new Vector3(s, s, 1f);
                        SpriteKit.PlaceBottomCenter(a, x, arrowTop - _art.MarkerArrow.bounds.size.y * s);
                    }
                    Number(ruler, m, x, arrowTop + 0.12f, 0.85f);
                }
            }
        }

        /// <summary>Число из спрайтов-цифр (белые «электронные» цифры, высота h), по центру x.</summary>
        void Number(Transform parent, int value, float centerX, float bottomY, float h)
        {
            if (_art.Digits == null || _art.Digits.Length < 10) return;
            string text = value.ToString();
            var sprites = new List<Sprite>();
            float width = 0f, gap = h * 0.08f;
            foreach (char ch in text)
            {
                var d = _art.Digits[ch - '0'];
                if (d == null) return;
                sprites.Add(d);
                width += d.bounds.size.x / d.bounds.size.y * h + gap;
            }
            float x = centerX - (width - gap) * 0.5f;
            foreach (var d in sprites)
            {
                var r = _kit.Make("Digit", parent, d, SortOrder.Ruler + 1, _night ? NightInk : MarkerInk);
                float s = h / d.bounds.size.y;
                r.transform.localScale = new Vector3(s, s, 1f);
                float w = d.bounds.size.x * s;
                SpriteKit.PlaceBottomCenter(r, x + w * 0.5f, bottomY);
                x += w + gap;
            }
        }

        void BuildTarget()
        {
            float y = _layout.TargetY; // the roof under the platform, where he lands
            var target = new GameObject("Target").transform;
            target.SetParent(_root, false);
            _flagTopY = y + FlagHeight;
            if (_art.Flag != null)
            {
                var f = _kit.Make("Flag", target, _art.Flag, SortOrder.Target);
                float s = FlagHeight / Mathf.Max(0.01f, _art.Flag.bounds.size.y);
                f.transform.localScale = new Vector3(s, s, 1f);
                // Flag sprites are imported with the pivot at the foot of the pole.
                f.transform.localPosition = new Vector3(_layout.ArrowX, y, 0f);
            }
            else
            {
                _kit.Box("Pole", target, new Rect(_layout.ArrowX - 0.05f, y, 0.1f, FlagHeight), new Color(0.3f, 0.3f, 0.3f), SortOrder.Target);
                _kit.Box("Cloth", target, new Rect(_layout.ArrowX, y + FlagHeight - 0.8f, 1.1f, 0.8f), TargetRed, SortOrder.Target);
            }
            BuildSignal(target, _layout.TargetRect.xMin + SignalInset, y);
            BuildSignal(target, _layout.TargetRect.xMax - SignalInset, y);
            if (_art.Dash != null)
            {
                _guide = _kit.Make("Guide", target, _art.Dash, SortOrder.Target - 1);
                _guide.drawMode = SpriteDrawMode.Tiled;
                _guide.size = new Vector2(_art.Dash.bounds.size.x, 10f);
                _guide.enabled = false;
            }
        }

        void BuildSignal(Transform parent, float x, float y)
        {
            if (_art.SignalOff != null)
            {
                var post = _kit.Make("Signal", parent, _art.SignalOff, SortOrder.Target);
                float s = SignalHeight / Mathf.Max(0.01f, _art.SignalOff.bounds.size.y);
                post.transform.localScale = new Vector3(s, s, 1f);
                post.transform.localPosition = new Vector3(x, y, 0f); // the pivot is at the foot of the post
                _signals.Add((post, null));
                return;
            }
            // No placeholders (fresh clone): a grey post with a square lamp on top.
            var pole = _kit.Box("Signal", parent, new Rect(x - 0.1f, y, 0.2f, SignalHeight), SignalPost, SortOrder.Target);
            var lamp = _kit.Box("SignalLamp", parent, new Rect(x - 0.17f, y + SignalHeight - 0.45f, 0.34f, 0.34f), SignalDark, SortOrder.Target + 1);
            _signals.Add((pole, lamp));
        }

        /// <summary>
        /// Столбики у цели (как в оригинале): пока прыжок идёт — выключены, сел на цель — зелёные, любой провал — красные.
        /// </summary>
        public void SetTargetLights(TargetLight light)
        {
            if (light == Lights) return;
            Lights = light;
            foreach (var (post, lamp) in _signals)
            {
                if (lamp != null)
                {
                    lamp.color = light == TargetLight.Green ? SignalGreen : light == TargetLight.Red ? TargetRed : SignalDark;
                    continue;
                }
                var sprite = light == TargetLight.Green ? _art.SignalGreen : light == TargetLight.Red ? _art.SignalRed : _art.SignalOff;
                if (sprite != null) post.sprite = sprite;
            }
        }

        /// <summary>Флажок на каждой промежуточной платформе: красный, пока не сел на неё, потом зелёный (как в оригинале).</summary>
        void BuildPlatformFlags()
        {
            for (int i = 1; i < _layout.Runways.Count; i++)
            {
                var strip = _layout.Runways[i].Strip;
                SpriteRenderer f;
                if (_art.Flag != null)
                {
                    f = _kit.Make($"PlatformFlag {i}", _root, _art.Flag, SortOrder.Target);
                    float s = FlagHeight * 0.8f / Mathf.Max(0.01f, _art.Flag.bounds.size.y);
                    f.transform.localScale = new Vector3(s, s, 1f);
                }
                else f = _kit.Box($"PlatformFlag {i}", _root, new Rect(-0.05f, 0f, 0.1f, FlagHeight * 0.8f), TargetRed, SortOrder.Target);
                f.transform.localPosition = new Vector3(strip.center.x, _layout.Runways[i].RoofY, 0f);
                _platformFlags[i] = f;
            }
        }

        /// <summary>Сел на промежуточную платформу — её флажок зеленеет.</summary>
        public void SetPlatformReached(int runway, bool reached)
        {
            if (!_platformFlags.TryGetValue(runway, out var f)) return;
            if (_art.Flag != null && _art.FlagGreen != null)
            {
                f.sprite = reached ? _art.FlagGreen : _art.Flag;
                f.color = Color.white;
            }
            else f.color = reached ? new Color(0.2f, 0.75f, 0.25f) : (_art.Flag != null ? Color.white : TargetRed);
        }

        /// <summary>Новая попытка: все флажки платформ снова красные.</summary>
        public void ResetPlatformFlags()
        {
            foreach (var key in _platformFlags.Keys) SetPlatformReached(key, false);
        }

        /// <summary>Ночью — где сейчас стикмен (он сам светит, как фонарь).</summary>
        public void SetPlayerLight(Vector2 position) => _playerLight = position;

        /// <summary>
        /// Ночное освещение как в оригинале: каждая «светящаяся» картинка ярче, чем ближе к стикмену или фонарю
        /// (по своему центру — поэтому дома светлеют кусками), вдали почти чёрная.
        /// </summary>
        void UpdateNightLight()
        {
            foreach (var (r, c, a) in _litPictures)
            {
                float b = Lit(Vector2.Distance(c, _playerLight), PlayerLightFull, PlayerLightFade);
                foreach (var l in _lights) b = Mathf.Max(b, Lit(Vector2.Distance(c, l.Position), l.RadiusFull, l.RadiusFade));
                r.color = new Color(b, b, b * 1.04f, a);
            }
        }

        static float Lit(float distance, float full, float fade)
        {
            if (distance <= full) return 1f;
            float t = Mathf.Clamp01((distance - full) / Mathf.Max(0.01f, fade - full));
            return Mathf.Lerp(1f, NightFloor, t * t * (3f - 2f * t));
        }

        void BuildLastFlag()
        {
            if (_art.FlagLast == null) return;
            _lastFlag = _kit.Make("LastRunFlag", _root, _art.FlagLast, SortOrder.Target);
            float s = FlagHeight * 0.8f / Mathf.Max(0.01f, _art.FlagLast.bounds.size.y);
            _lastFlag.transform.localScale = new Vector3(s, s, 1f);
            _lastFlag.gameObject.SetActive(false);
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

    /// <summary>Лампы сигнальных столбиков у цели.</summary>
    public enum TargetLight { Off, Green, Red }
}
