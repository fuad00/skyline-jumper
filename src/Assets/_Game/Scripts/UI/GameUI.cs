using System;
using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.UI
{
    public enum UIScreen { None, MainMenu, StageSelect, LevelSelect, Game, Summary }

    /// <summary>Что показать на карточке этапа.</summary>
    public struct StageCardView
    {
        public int Number;
        public string Title, Subtitle;
        public int Stars, MaxStars, NeedStars;
        public bool Locked, Empty;
    }

    /// <summary>Что показать на плитке уровня.</summary>
    public struct LevelNodeView
    {
        public int Number, Stars, Points;
        public bool Locked;
    }

    /// <summary>
    /// Связывает Game.uxml. Экраны как в оригинале: главное меню (логотип + нижняя панель), выбор этапа, выбор уровня,
    /// игра (HUD), пауза и итог прыжка. Кнопки нижних панелей — <see cref="SlantButton"/>.
    /// Тап в любом месте экрана во время игры (кроме кнопок) — это ход игрока.
    /// </summary>
    public sealed class GameUI
    {
        static readonly Color MapBuilding = new Color(0.07f, 0.09f, 0.12f, 0.55f);
        static readonly Color MapTarget = new Color(0.86f, 0.1f, 0.1f, 1f);
        static readonly Color MapLast = new Color(0.12f, 0.62f, 0.22f, 1f);

        readonly ArtBank _art;
        readonly VisualElement _root, _hud, _menu, _stageScreen, _levelScreen, _summary, _pauseOverlay;
        readonly VisualElement _minimap, _windFill, _windArrow, _stageCards, _levelGrid, _levelBadge, _summaryFace;
        readonly Label _time, _hint;
        readonly VisualElement _hintBox;
        readonly ClockDial _clock;
        readonly Label _titleSmall, _titleBig, _levelLine, _distance, _best, _points, _record;
        readonly VisualElement[] _stars = new VisualElement[3];
        readonly Button _zoom;
        readonly SlantButton _soundButton, _musicButton, _nextButton;

        LevelLayout _mapLayout;
        Vector2 _mapPlayer;
        float _mapLastX = float.NaN;

        public event Action Tapped, PlayClicked, SoundClicked, MusicClicked, BackClicked;
        public event Action MenuClicked, RetryClicked, NextClicked, ContinueClicked, PauseClicked, ZoomClicked;
        public event Action<int> StageClicked, LevelClicked;

        public UIScreen Screen { get; private set; } = UIScreen.None;
        public bool SummaryVisible => Screen == UIScreen.Summary;
        public bool PauseVisible { get; private set; }

        public GameUI(VisualElement root, ArtBank art)
        {
            _art = art;
            _root = root.Q<VisualElement>("root");
            _hud = root.Q<VisualElement>("hud");
            _menu = root.Q<VisualElement>("menu-screen");
            _stageScreen = root.Q<VisualElement>("stage-screen");
            _levelScreen = root.Q<VisualElement>("level-screen");
            _summary = root.Q<VisualElement>("summary");
            _pauseOverlay = root.Q<VisualElement>("pause-overlay");
            _minimap = root.Q<VisualElement>("minimap");
            _windFill = root.Q<VisualElement>("wind-fill");
            _windArrow = root.Q<VisualElement>("wind-arrow");
            _stageCards = root.Q<VisualElement>("stage-cards");
            _levelGrid = root.Q<VisualElement>("level-grid");
            _levelBadge = root.Q<VisualElement>("level-badge");
            _summaryFace = root.Q<VisualElement>("summary-face");
            _time = root.Q<Label>("hud-time");
            _hint = root.Q<Label>("hint");
            _hintBox = root.Q<VisualElement>("hint-box");
            _titleSmall = root.Q<Label>("summary-title-small");
            _titleBig = root.Q<Label>("summary-title-big");
            _levelLine = root.Q<Label>("summary-level");
            _distance = root.Q<Label>("summary-distance");
            _best = root.Q<Label>("summary-best");
            _points = root.Q<Label>("summary-points");
            _record = root.Q<Label>("summary-record");
            _zoom = root.Q<Button>("zoom");
            for (int i = 0; i < 3; i++) _stars[i] = root.Q<VisualElement>($"star-{i + 1}");

            root.Q<Label>("wind-label").text = UIText.Wind;
            root.Q<Label>("pause-title").text = UIText.Pause;
            root.Q<Label>("logo-top").text = UIText.LogoTop;
            root.Q<Label>("logo-bottom").text = UIText.LogoBottom;
            root.Q<Label>("stage-heading-small").text = UIText.Choose;
            root.Q<Label>("stage-heading-big").text = UIText.StageWord;
            root.Q<Label>("level-heading-small").text = UIText.Choose;
            root.Q<Label>("level-heading-big").text = UIText.LevelWord;
            root.Q<Label>("distance-label").text = UIText.DistanceLabel;
            root.Q<Label>("best-label").text = UIText.BestLabel;
            root.Q<Label>("points-small").text = UIText.PointsSmall;
            root.Q<Label>("points-big").text = UIText.PointsBig;
            _record.text = UIText.NewBest;

            // Plates drawn in code, in the original's style.
            Plate.Dress(root.Q<VisualElement>("wind-panel"), Plate.Kind.Hud, 22f, 5f);
            Plate.Dress(root.Q<VisualElement>("time-panel"), Plate.Kind.Hud, 22f, 5f);
            Plate.Dress(_zoom, Plate.Kind.Hud, 22f, 5f);
            Plate.Dress(root.Q<VisualElement>("pause"), Plate.Kind.Hud, 22f, 5f);
            Plate.Dress(_hintBox, Plate.Kind.LightShadow, 24f, 5f, new Color32(196, 230, 244, 255));
            Plate.Dress(root.Q<VisualElement>(className: "summary-card"), Plate.Kind.Dark, 40f, 8f);
            Plate.Dress(root.Q<VisualElement>("points-bar"), Plate.Kind.Rimmed, 26f, 6f, UIShapes.Red);
            _clock = new ClockDial();
            root.Q<VisualElement>("clock-slot").Add(_clock);
            foreach (var bar in new[] { "menu-bar", "stage-bar", "level-bar", "pause-bar", "summary-bar" })
                Plate.Dress(root.Q<VisualElement>(bar), Plate.Kind.Dark, 24f, 7f);

            // Bottom bars, like the original: coloured slanted segments on a dark plate, blanks fill the rest.
            var menuBar = root.Q<VisualElement>("menu-bar");
            menuBar.Add(new SlantButton(UIText.PlaySmall, UIText.PlayBig, SlantButton.Red, () => PlayClicked?.Invoke(), first: true));
            _soundButton = new SlantButton(UIText.SoundSmall, UIText.On, SlantButton.Blue, () => SoundClicked?.Invoke());
            menuBar.Add(_soundButton);
            _musicButton = new SlantButton(UIText.MusicSmall, UIText.On, SlantButton.Blue, () => MusicClicked?.Invoke());
            menuBar.Add(_musicButton);
            menuBar.Add(SlantButton.Blank());
            menuBar.Add(SlantButton.Blank(last: true));

            Bar(root.Q<VisualElement>("stage-bar"), new SlantButton(null, UIText.Back, SlantButton.Red, () => BackClicked?.Invoke(), first: true));
            Bar(root.Q<VisualElement>("level-bar"), new SlantButton(null, UIText.Back, SlantButton.Red, () => BackClicked?.Invoke(), first: true));

            var pauseBar = root.Q<VisualElement>("pause-bar");
            pauseBar.Add(new SlantButton(UIText.MenuSmall, UIText.MenuBig, SlantButton.Red, () => MenuClicked?.Invoke(), first: true));
            pauseBar.Add(new SlantButton(UIText.RetrySmall, UIText.RetryBig, SlantButton.Blue, () => RetryClicked?.Invoke()));
            pauseBar.Add(SlantButton.Blank());
            pauseBar.Add(SlantButton.Blank());
            pauseBar.Add(new SlantButton(UIText.ContinueSmall, UIText.ContinueBig, SlantButton.Green, () => ContinueClicked?.Invoke(), last: true));

            var summaryBar = root.Q<VisualElement>("summary-bar");
            summaryBar.Add(new SlantButton(UIText.MenuSmall, UIText.MenuBig, SlantButton.Red, () => MenuClicked?.Invoke(), first: true));
            summaryBar.Add(new SlantButton(UIText.RetrySmall, UIText.RetryBig, SlantButton.Blue, () => RetryClicked?.Invoke()));
            summaryBar.Add(SlantButton.Blank());
            summaryBar.Add(SlantButton.Blank());
            _nextButton = new SlantButton(UIText.NextSmall, UIText.NextBig, SlantButton.Green, () => NextClicked?.Invoke(), last: true);
            summaryBar.Add(_nextButton);

            _zoom.clicked += () => ZoomClicked?.Invoke();
            root.Q<Button>("pause").clicked += () => PauseClicked?.Invoke();

            SetIcon(_windArrow, art.IconWind, "›");
            SetIcon(root.Q<VisualElement>("icon-zoom"), art.IconZoom, "+");
            SetIcon(root.Q<VisualElement>("icon-pause"), art.IconPause, "II");
            if (art.HeadFront != null) _summaryFace.style.backgroundImage = new StyleBackground(art.HeadFront);

            _minimap.generateVisualContent += DrawMinimap;

            // Any press during play that is not on a button is the player's tap.
            _root.RegisterCallback<PointerDownEvent>(e =>
            {
                if (Screen != UIScreen.Game || PauseVisible || IsOnButton(e.target as VisualElement)) return;
                Tapped?.Invoke();
            });

            ShowPause(false);
            Show(UIScreen.None);
        }

        /// <summary>Как в оригинале: 5 сегментов, первый — кнопка, остальные пустые.</summary>
        static void Bar(VisualElement bar, SlantButton first)
        {
            bar.Add(first);
            for (int i = 0; i < 3; i++) bar.Add(SlantButton.Blank());
            bar.Add(SlantButton.Blank(last: true));
        }

        static void SetIcon(VisualElement e, Sprite sprite, string fallback)
        {
            if (e == null) return;
            if (sprite != null)
            {
                e.style.backgroundImage = new StyleBackground(sprite);
                return;
            }
            e.Add(new Label(fallback) { pickingMode = PickingMode.Ignore, style = { fontSize = 40, unityTextAlign = TextAnchor.MiddleCenter, flexGrow = 1 } });
        }

        static bool IsOnButton(VisualElement e)
        {
            for (; e != null; e = e.parent)
                if (e is Button || e.ClassListContains("clickable")) return true;
            return false;
        }

        /// <summary>Вырез экрана и скруглённые углы телефона: всё содержимое интерфейса держим внутри безопасной зоны.</summary>
        public void ApplySafeArea(Rect safe, int screenW, int screenH)
        {
            float w = _root.parent != null ? _root.parent.resolvedStyle.width : 0f;
            float h = _root.parent != null ? _root.parent.resolvedStyle.height : 0f;
            if (w <= 1f || h <= 1f || screenW <= 0 || screenH <= 0) return;
            float sx = w / screenW, sy = h / screenH;
            _root.style.left = safe.xMin * sx;
            _root.style.right = (screenW - safe.xMax) * sx;
            _root.style.top = (screenH - safe.yMax) * sy;
            _root.style.bottom = safe.yMin * sy;
        }

        static void Display(VisualElement e, bool show) => e.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        // ------------------------------------------------------------------ screens

        public void Show(UIScreen screen)
        {
            Screen = screen;
            Display(_menu, screen == UIScreen.MainMenu);
            Display(_stageScreen, screen == UIScreen.StageSelect);
            Display(_levelScreen, screen == UIScreen.LevelSelect);
            Display(_summary, screen == UIScreen.Summary);
            Display(_hud, screen == UIScreen.Game);
            if (screen != UIScreen.Game) SetHint(string.Empty);
        }

        public void SetSound(bool sound, bool music)
        {
            _soundButton.SetLabels(UIText.SoundSmall, sound ? UIText.On : UIText.Off);
            _musicButton.SetLabels(UIText.MusicSmall, music ? UIText.On : UIText.Off);
        }

        public void ShowPause(bool show)
        {
            PauseVisible = show;
            Display(_pauseOverlay, show);
        }

        /// <summary>Экран «Выбери этап»: карточки этапов.</summary>
        public void SetStages(IReadOnlyList<StageCardView> stages)
        {
            _stageCards.Clear();
            for (int i = 0; i < stages.Count; i++)
            {
                var v = stages[i];
                int index = i;
                var card = new VisualElement();
                card.AddToClassList("stage-card");
                card.AddToClassList("clickable");
                Plate.Dress(card, Plate.Kind.LightShadow, 34f, 6f, UIShapes.Blue);
                var head = StageHead(v);
                head.AddToClassList("stage-head");
                card.Add(head);
                var foot = new VisualElement { pickingMode = PickingMode.Ignore };
                foot.AddToClassList("stage-foot");
                var star = new VisualElement { pickingMode = PickingMode.Ignore };
                star.AddToClassList("stage-star");
                if (_art.Star != null) star.style.backgroundImage = new StyleBackground(_art.Star);
                foot.Add(star);
                foot.Add(Text(v.Empty ? UIText.Soon : $"{v.Stars}/{v.MaxStars}", "stage-count"));
                if (v.Locked) star.style.display = DisplayStyle.None;
                card.Add(foot);
                if (v.Locked)
                {
                    foot.style.visibility = Visibility.Hidden;
                    var lockLayer = new VisualElement { pickingMode = PickingMode.Ignore };
                    lockLayer.AddToClassList("stage-lock");
                    var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                    icon.AddToClassList("lock-icon");
                    if (_art.IconLock != null) icon.style.backgroundImage = new StyleBackground(_art.IconLock);
                    lockLayer.Add(icon);
                    lockLayer.Add(Text(UIText.StarsNeeded(v.NeedStars), "lock-text"));
                    card.Add(lockLayer);
                }
                else if (!v.Empty)
                {
                    card.RegisterCallback<ClickEvent>(_ => StageClicked?.Invoke(index));
                }
                _stageCards.Add(card);
            }
        }

        /// <summary>Логотип этапа как в оригинале: крупная цифра, «этап», название и подпись, силуэт города внизу.</summary>
        VisualElement StageHead(StageCardView v)
        {
            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            if (_art.Skyline != null)
            {
                var sky = new VisualElement { pickingMode = PickingMode.Ignore };
                sky.AddToClassList("stage-skyline");
                sky.style.backgroundImage = new StyleBackground(_art.Skyline);
                head.Add(sky);
            }
            head.Add(Text(v.Number.ToString(), "stage-number"));
            head.Add(Text(UIText.StageWord, "stage-word"));
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.AddToClassList("stage-texts");
            texts.Add(Text(v.Title, "stage-title"));
            texts.Add(Text(v.Subtitle, "stage-sub"));
            head.Add(texts);
            return head;
        }

        /// <summary>Экран «Выбери уровень»: плашка этапа и плитки уровней (звёзды, очки, замки).</summary>
        public void SetLevels(StageCardView stage, IReadOnlyList<LevelNodeView> levels)
        {
            _levelBadge.Clear();
            Plate.Dress(_levelBadge, Plate.Kind.Dark, 30f, 7f);
            var head = StageHead(stage);
            head.AddToClassList("stage-badge-inner");
            _levelBadge.Add(head);

            _levelGrid.Clear();
            for (int i = 0; i < levels.Count; i++)
            {
                var v = levels[i];
                int index = i;
                var node = new VisualElement();
                node.AddToClassList("level-node");
                node.AddToClassList("clickable");
                var plate = Plate.Dress(node, Plate.Kind.LightShadow, 28f, 5f, v.Locked ? new Color32(150, 170, 182, 255) : UIShapes.Blue);
                if (!v.Locked)
                {
                    node.RegisterCallback<PointerEnterEvent>(_ => { plate.Fill = new Color32(160, 214, 232, 255); plate.MarkDirtyRepaint(); });
                    node.RegisterCallback<PointerLeaveEvent>(_ => { plate.Fill = UIShapes.Blue; plate.MarkDirtyRepaint(); });
                }
                var stars = new VisualElement { pickingMode = PickingMode.Ignore };
                stars.AddToClassList("node-top");
                for (int s = 0; s < 3; s++)
                {
                    var st = new VisualElement { pickingMode = PickingMode.Ignore };
                    st.AddToClassList("node-star");
                    var sprite = s < v.Stars ? _art.Star : _art.StarEmpty;
                    if (sprite != null) st.style.backgroundImage = new StyleBackground(sprite);
                    stars.Add(st);
                }
                node.Add(stars);
                var body = new VisualElement { pickingMode = PickingMode.Ignore };
                body.AddToClassList("node-body");
                body.Add(Text(v.Number.ToString(), "node-number"));
                body.Add(Text(v.Points > 0 ? UIText.PointsShort(v.Points) : " ", "node-points"));
                node.Add(body);
                if (v.Locked)
                {
                    var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                    icon.AddToClassList("node-lock");
                    if (_art.IconLock != null) icon.style.backgroundImage = new StyleBackground(_art.IconLock);
                    node.Add(icon);
                }
                else node.RegisterCallback<ClickEvent>(_ => LevelClicked?.Invoke(index));
                _levelGrid.Add(node);
            }
        }

        static Label Text(string text, string cls)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList(cls);
            return l;
        }

        // ------------------------------------------------------------------ HUD

        public void SetHud(float timeLeft, float timeLimit, float wind, float windMax, bool zoomOn)
        {
            _time.text = UIText.Timer(timeLeft);
            _clock.Used = 1f - timeLeft / Mathf.Max(0.01f, timeLimit);
            _time.EnableInClassList("time-text--low", timeLeft < 5f);
            float k = Mathf.Clamp01(Mathf.Abs(wind) / Mathf.Max(0.1f, windMax));
            _windFill.style.width = Length.Percent(Mathf.Lerp(18f, 100f, k));
            _windArrow.style.scale = new Scale(new Vector3(wind < 0f ? -1f : 1f, 1f, 1f));
            _zoom.EnableInClassList("btn-square--on", zoomOn);
        }

        /// <summary>Мини-карта: здания, флажок цели, стикмен, флажок прошлого разбега.</summary>
        public void SetMinimap(LevelLayout layout, Vector2 player, float lastRunX)
        {
            _mapLayout = layout;
            _mapPlayer = player;
            _mapLastX = lastRunX;
            _minimap.MarkDirtyRepaint();
        }

        void DrawMinimap(MeshGenerationContext ctx)
        {
            var full = new Rect(0f, 0f, _minimap.resolvedStyle.width, _minimap.resolvedStyle.height);
            if (full.width <= 2f || full.height <= 2f) return;
            var p = ctx.painter2D;
            var inner = UIShapes.LightPlate(p, full, 22f, 5f, UIShapes.HudBlue);
            if (_mapLayout == null) return;
            var r = UIShapes.Inset(inner, 8f);
            Rect world = _mapLayout.Overview;
            world = Rect.MinMaxRect(world.xMin - 3f, -2f, world.xMax + 3f, world.yMax + 6f);
            float s = Mathf.Min(r.width / world.width, r.height / world.height);
            Vector2 off = new Vector2((r.width - world.width * s) * 0.5f, (r.height - world.height * s) * 0.5f);
            Vector2 Map(Vector2 w) => new Vector2(r.x + off.x + (w.x - world.xMin) * s, r.y + r.height - (off.y + (w.y - world.yMin) * s));

            foreach (var solid in _mapLayout.Solids)
            {
                if (solid.Kind != SolidKind.Building) continue;
                var a = Map(new Vector2(solid.Rect.xMin, solid.Rect.yMin));
                var b = Map(new Vector2(solid.Rect.xMax, solid.Rect.yMax));
                p.fillColor = MapBuilding;
                p.BeginPath();
                p.MoveTo(new Vector2(a.x, a.y));
                p.LineTo(new Vector2(b.x, a.y));
                p.LineTo(new Vector2(b.x, b.y));
                p.LineTo(new Vector2(a.x, b.y));
                p.ClosePath();
                p.Fill();
            }
            Dot(p, Map(new Vector2(_mapLayout.ArrowX, _mapLayout.TargetRect.yMax + 1.5f)), 7f, MapTarget);
            if (!float.IsNaN(_mapLastX)) Dot(p, Map(new Vector2(_mapLastX, _mapLayout.RoofY + 1f)), 6f, MapLast);
            Dot(p, Map(_mapPlayer + Vector2.up), 9f, Color.white);
            Dot(p, Map(_mapPlayer + Vector2.up), 6f, MapTarget);
        }

        static void Dot(Painter2D p, Vector2 c, float radius, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.Arc(c, radius, 0f, 360f);
            p.Fill();
        }

        public void SetHint(string text)
        {
            _hint.text = text;
            Display(_hintBox, !string.IsNullOrEmpty(text));
        }

        // ------------------------------------------------------------------ summary

        public void ShowSummary(JumpOutcome outcome, int stage, int level, float distance, float best, bool newBest, int points, int stars, bool canGoNext)
        {
            Show(UIScreen.Summary);
            var (small, big) = UIText.Title(outcome);
            _titleSmall.text = small;
            _titleBig.text = big;
            _levelLine.text = UIText.LevelLine(stage, level);
            _distance.text = UIText.Meters(distance);
            _best.text = UIText.Meters(best);
            _points.text = UIText.Points(points);
            Display(_record, newBest);
            _nextButton.SetEnabled(canGoNext);
            _nextButton.MarkDirtyRepaint();
            for (int i = 0; i < 3; i++)
            {
                var sprite = i < stars ? _art.Star : _art.StarEmpty;
                _stars[i].style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground();
            }
        }
    }
}
