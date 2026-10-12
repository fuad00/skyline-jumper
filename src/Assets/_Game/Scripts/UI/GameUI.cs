using System;
using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.UI
{
    public enum UIScreen { None, MainMenu, Settings, StageSelect, LevelSelect, Game, Summary }

    /// <summary>Что показать на карточке этапа.</summary>
    public struct StageCardView
    {
        public int Number;
        public string Title, Subtitle;
        public int Stars, MaxStars, NeedStars;
        public bool Locked, Empty;
    }

    /// <summary>Кнопка окна-вопроса: «мелкая / КРУПНАЯ» подпись, цвет и действие.</summary>
    public struct DialogButton
    {
        public string Small, Big;
        public Color Fill;
        public Action OnClick;

        public DialogButton(string small, string big, Color fill, Action onClick)
        {
            Small = small;
            Big = big;
            Fill = fill;
            OnClick = onClick;
        }
    }

    /// <summary>Что показать на плитке уровня.</summary>
    public struct LevelNodeView
    {
        public int Number, Stars, Points;
        public bool Locked;
    }

    /// <summary>
    /// Связывает Game.uxml. Экраны как в оригинале: главное меню (логотип + нижняя панель), настройки (та же панель
    /// с другими кнопками), выбор этапа, выбор уровня, игра (HUD), пауза и итог прыжка, поверх — окно-вопрос.
    /// Кнопки нижних панелей — <see cref="SlantButton"/>. Все подписи берутся из <see cref="UIText"/>:
    /// <see cref="ApplyLanguage"/> переписывает их на текущем языке.
    /// Тап в любом месте экрана во время игры (кроме кнопок) — это ход игрока.
    /// </summary>
    public sealed class GameUI
    {
        static readonly Color MapBuilding = new Color(0.07f, 0.09f, 0.12f, 0.55f);
        static readonly Color MapTarget = new Color(0.86f, 0.1f, 0.1f, 1f);
        static readonly Color MapLast = new Color(0.12f, 0.62f, 0.22f, 1f);

        readonly ArtBank _art;
        readonly VisualElement _root, _hud, _menu, _menuBar, _settingsBar, _stageScreen, _levelScreen, _summary, _pauseOverlay;
        readonly VisualElement _dialog, _dialogBar;
        readonly Label _dialogTitle, _dialogText;
        readonly VisualElement _minimap, _windFill, _windArrow, _stageCards, _levelGrid, _levelBadge, _summaryFace;

        /// <summary>Шаг ленты этапов: ширина карточки + промежуток (как в USS: 620 + 2 × 30).</summary>
        const float StageCardStep = 680f;
        readonly List<StageCardView> _stageViews = new List<StageCardView>();
        readonly List<VisualElement> _stageCardEls = new List<VisualElement>();
        VisualElement _stageTrack;
        int _stageFocus, _downCard = -1;
        float _trackX, _trackTime, _dragStartPointer, _dragStartTrack, _dragMoved;
        bool _dragging, _trackSnap;
        float _trackVelocity;
        /// <summary>За сколько секунд лента в основном доезжает до нужной карточки.</summary>
        const float StageSlideTime = 0.18f;
        /// <summary>Размер соседних карточек относительно центральной.</summary>
        const float StageSideScale = 0.8f;
        readonly Label _time, _hint;
        readonly VisualElement _hintBox;
        readonly ClockDial _clock;
        readonly Label _titleSmall, _titleBig, _levelLine, _distance, _best, _points, _record;
        readonly VisualElement[] _stars = new VisualElement[3];
        readonly Button _zoom;
        readonly SlantButton _nextButton;
        /// <summary>Кнопки и их подписи на текущем языке (переписываются при смене языка).</summary>
        readonly List<(SlantButton button, Func<string> small, Func<string> big)> _buttons = new List<(SlantButton, Func<string>, Func<string>)>();
        readonly Dictionary<Label, Func<string>> _texts = new Dictionary<Label, Func<string>>();
        bool _soundOn = true, _musicOn = true;
        Action _dialogBack;

        LevelLayout _mapLayout;
        Vector2 _mapPlayer;
        float _mapLastX = float.NaN;

        public event Action Tapped, PlayClicked, SoundClicked, MusicClicked, BackClicked;
        public event Action SettingsClicked, BuilderClicked, LanguageClicked, ResetClicked;
        public event Action MenuClicked, RetryClicked, NextClicked, ContinueClicked, PauseClicked, ZoomClicked;
        public event Action<int> StageClicked, LevelClicked;

        public UIScreen Screen { get; private set; } = UIScreen.None;
        public bool SummaryVisible => Screen == UIScreen.Summary;
        public bool PauseVisible { get; private set; }
        public bool DialogVisible { get; private set; }

        public GameUI(VisualElement root, ArtBank art)
        {
            _art = art;
            _root = root.Q<VisualElement>("root");
            _hud = root.Q<VisualElement>("hud");
            _menu = root.Q<VisualElement>("menu-screen");
            _menuBar = root.Q<VisualElement>("menu-bar");
            _settingsBar = root.Q<VisualElement>("settings-bar");
            _dialog = root.Q<VisualElement>("dialog");
            _dialogBar = root.Q<VisualElement>("dialog-bar");
            _dialogTitle = root.Q<Label>("dialog-title");
            _dialogText = root.Q<Label>("dialog-text");
            _stageScreen = root.Q<VisualElement>("stage-screen");
            _levelScreen = root.Q<VisualElement>("level-screen");
            _summary = root.Q<VisualElement>("summary");
            _pauseOverlay = root.Q<VisualElement>("pause-overlay");
            _minimap = root.Q<VisualElement>("minimap");
            _windFill = root.Q<VisualElement>("wind-fill");
            _windArrow = root.Q<VisualElement>("wind-arrow");
            _stageCards = root.Q<VisualElement>("stage-cards");
            _stageCards.pickingMode = PickingMode.Position; // swipes anywhere on the strip
            _stageCards.RegisterCallback<PointerDownEvent>(OnStagePointerDown);
            _stageCards.RegisterCallback<PointerMoveEvent>(OnStagePointerMove);
            _stageCards.RegisterCallback<PointerUpEvent>(OnStagePointerUp);
            _stageCards.RegisterCallback<PointerCancelEvent>(_ => _dragging = false);
            _stageCards.schedule.Execute(AnimateStageTrack).Every(16);
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

            root.Q<Label>("logo-top").text = UIText.LogoTop;
            root.Q<Label>("logo-bottom").text = UIText.LogoBottom;
            _texts[root.Q<Label>("wind-label")] = () => UIText.Wind;
            _texts[root.Q<Label>("pause-title")] = () => UIText.Pause;
            _texts[root.Q<Label>("stage-heading-small")] = () => UIText.Choose;
            _texts[root.Q<Label>("stage-heading-big")] = () => UIText.StageWord;
            _texts[root.Q<Label>("level-heading-small")] = () => UIText.Choose;
            _texts[root.Q<Label>("level-heading-big")] = () => UIText.LevelWord;
            _texts[root.Q<Label>("distance-label")] = () => UIText.DistanceLabel;
            _texts[root.Q<Label>("best-label")] = () => UIText.BestLabel;
            _texts[root.Q<Label>("points-small")] = () => UIText.PointsSmall;
            _texts[root.Q<Label>("points-big")] = () => UIText.PointsBig;
            _texts[_record] = () => UIText.NewBest;

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
            Plate.Dress(root.Q<VisualElement>("dialog-card"), Plate.Kind.Dark, 40f, 8f);
            foreach (var bar in new[] { "menu-bar", "settings-bar", "stage-bar", "level-bar", "pause-bar", "summary-bar" })
                Plate.Dress(root.Q<VisualElement>(bar), Plate.Kind.Dark, 24f, 7f);

            // Bottom bars, like the original: 5 coloured slanted cells on a dark plate, blanks fill the rest.
            _menuBar.Add(Btn(() => UIText.PlaySmall, () => UIText.PlayBig, SlantButton.Red, () => PlayClicked?.Invoke(), first: true));
            _menuBar.Add(Btn(null, () => UIText.SettingsBig, SlantButton.Blue, () => SettingsClicked?.Invoke()));
            _menuBar.Add(Btn(() => UIText.BuilderSmall, () => UIText.BuilderBig, SlantButton.Blue, () => BuilderClicked?.Invoke()));
            _menuBar.Add(SlantButton.Blank());
            _menuBar.Add(SlantButton.Blank(last: true));

            _settingsBar.Add(Btn(null, () => UIText.Back, SlantButton.Red, () => BackClicked?.Invoke(), first: true));
            _settingsBar.Add(Btn(() => UIText.SoundSmall, () => _soundOn ? UIText.On : UIText.Off, SlantButton.Blue, () => SoundClicked?.Invoke()));
            _settingsBar.Add(Btn(() => UIText.MusicSmall, () => _musicOn ? UIText.On : UIText.Off, SlantButton.Blue, () => MusicClicked?.Invoke()));
            _settingsBar.Add(Btn(() => UIText.LanguageSmall, () => UIText.LanguageBig, SlantButton.Blue, () => LanguageClicked?.Invoke()));
            _settingsBar.Add(Btn(() => UIText.ResetSmall, () => UIText.ResetBig, SlantButton.Blue, () => ResetClicked?.Invoke(), last: true));

            Bar(root.Q<VisualElement>("stage-bar"), Btn(null, () => UIText.Back, SlantButton.Red, () => BackClicked?.Invoke(), first: true));
            Bar(root.Q<VisualElement>("level-bar"), Btn(null, () => UIText.Back, SlantButton.Red, () => BackClicked?.Invoke(), first: true));

            // Same cells as the original's pause: menu first, retry fourth, continue last.
            var pauseBar = root.Q<VisualElement>("pause-bar");
            pauseBar.Add(Btn(() => UIText.MenuSmall, () => UIText.MenuBig, SlantButton.Red, () => MenuClicked?.Invoke(), first: true));
            pauseBar.Add(SlantButton.Blank());
            pauseBar.Add(SlantButton.Blank());
            pauseBar.Add(Btn(() => UIText.RetrySmall, () => UIText.RetryBig, SlantButton.Blue, () => RetryClicked?.Invoke()));
            pauseBar.Add(Btn(() => UIText.ContinueSmall, () => UIText.ContinueBig, SlantButton.Green, () => ContinueClicked?.Invoke(), last: true));

            var summaryBar = root.Q<VisualElement>("summary-bar");
            summaryBar.Add(Btn(() => UIText.MenuSmall, () => UIText.MenuBig, SlantButton.Red, () => MenuClicked?.Invoke(), first: true));
            summaryBar.Add(SlantButton.Blank());
            summaryBar.Add(SlantButton.Blank());
            summaryBar.Add(Btn(() => UIText.RetrySmall, () => UIText.RetryBig, SlantButton.Blue, () => RetryClicked?.Invoke()));
            _nextButton = Btn(() => UIText.NextSmall, () => UIText.NextBig, SlantButton.Green, () => NextClicked?.Invoke(), last: true);
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

            ApplyLanguage();
            ShowPause(false);
            HideDialog();
            Show(UIScreen.None);
        }

        /// <summary>Кнопка панели, подписи которой следуют за языком.</summary>
        SlantButton Btn(Func<string> small, Func<string> big, Color fill, Action onClick, bool first = false, bool last = false)
        {
            var b = new SlantButton(small?.Invoke(), big?.Invoke(), fill, onClick, first, last);
            _buttons.Add((b, small, big));
            return b;
        }

        /// <summary>Переписать все подписи на текущем языке (<see cref="UIText.Lang"/>).</summary>
        public void ApplyLanguage()
        {
            foreach (var pair in _texts) pair.Key.text = pair.Value();
            foreach (var (button, small, big) in _buttons) button.SetLabels(small?.Invoke(), big?.Invoke());
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
            Display(_menu, screen == UIScreen.MainMenu || screen == UIScreen.Settings);
            Display(_menuBar, screen != UIScreen.Settings);
            Display(_settingsBar, screen == UIScreen.Settings);
            Display(_stageScreen, screen == UIScreen.StageSelect);
            Display(_levelScreen, screen == UIScreen.LevelSelect);
            Display(_summary, screen == UIScreen.Summary);
            Display(_hud, screen == UIScreen.Game);
            if (screen != UIScreen.Game) SetHint(string.Empty);
        }

        public void SetSound(bool sound, bool music)
        {
            _soundOn = sound;
            _musicOn = music;
            ApplyLanguage();
        }

        /// <summary>
        /// Окно поверх всего: заголовок, пояснение и 1–2 кнопки. Кнопка «назад» на телефоне жмёт первую кнопку.
        /// </summary>
        public void ShowDialog(string title, string text, params DialogButton[] buttons)
        {
            _dialogTitle.text = title;
            _dialogText.text = text;
            _dialogBar.Clear();
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                _dialogBar.Add(new SlantButton(b.Small, b.Big, b.Fill, b.OnClick, first: i == 0, last: i == buttons.Length - 1));
            }
            _dialogBack = buttons.Length > 0 ? buttons[0].OnClick : null;
            DialogVisible = true;
            Display(_dialog, true);
        }

        public void HideDialog()
        {
            DialogVisible = false;
            _dialogBack = null;
            Display(_dialog, false);
        }

        /// <summary>Кнопка «назад» (Esc) при открытом окне — как первая кнопка окна («отмена»/«понятно»).</summary>
        public void DialogBack()
        {
            if (_dialogBack != null) _dialogBack();
            else HideDialog();
        }

        public void ShowPause(bool show)
        {
            PauseVisible = show;
            Display(_pauseOverlay, show);
        }

        /// <summary>
        /// Экран «Выбери этап» как в оригинале: лента карточек, выбранная — по центру, соседние выглядывают по краям.
        /// Листается свайпом или тапом по соседней карточке; тап по центральной — открыть этап. Лента едет плавно
        /// (пружина без раскачки), а карточка растёт по мере приближения к центру.
        /// </summary>
        public void SetStages(IReadOnlyList<StageCardView> stages, int focus = 0)
        {
            _stageCards.Clear();
            _stageCardEls.Clear();
            _stageViews.Clear();
            _stageViews.AddRange(stages);
            _stageTrack = new VisualElement { pickingMode = PickingMode.Ignore };
            _stageTrack.AddToClassList("stage-track");
            _stageCards.Add(_stageTrack);
            for (int i = 0; i < stages.Count; i++)
            {
                var v = stages[i];
                var card = new VisualElement();
                card.AddToClassList("stage-card");
                card.AddToClassList("clickable");
                card.userData = i;
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
                _stageTrack.Add(card);
                _stageCardEls.Add(card);
            }

            _stageFocus = Mathf.Clamp(focus, 0, Mathf.Max(0, stages.Count - 1));
            _trackSnap = true; // jump straight to the focused card, no slide
            _trackVelocity = 0f;
            // Place and size the cards right away (the screen width is known), so the first frame is already right —
            // otherwise the strip flashes at its left edge for a frame and jumps (seen when coming back from the levels).
            _trackTime = Time.realtimeSinceStartup;
            LayoutStageTrack(snap: true);
        }

        /// <summary>Какая карточка этапа сейчас по центру.</summary>
        public int StageFocus => _stageFocus;

        /// <summary>Пролистать ленту этапов к карточке (плавно).</summary>
        public void FocusStage(int index)
        {
            if (_stageViews.Count == 0) return;
            _stageFocus = Mathf.Clamp(index, 0, _stageViews.Count - 1);
        }

        /// <summary>Ширина ленты: во весь экран (берём у корня — он виден всегда, а лента бывает скрыта).</summary>
        float StripWidth => _root.resolvedStyle.width;

        /// <summary>Сдвиг ленты, при котором карточка index стоит по центру.</summary>
        float TrackXFor(int index) => StripWidth * 0.5f - (StageCardStep * index + StageCardStep * 0.5f);

        void AnimateStageTrack()
        {
            if (_stageTrack == null || Screen != UIScreen.StageSelect) return;
            LayoutStageTrack(snap: false);
        }

        void LayoutStageTrack(bool snap)
        {
            if (_stageTrack == null || !(StripWidth > 1f)) return; // not laid out yet
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Clamp(now - _trackTime, 0f, 0.1f);
            _trackTime = now;
            if (!_dragging)
            {
                float target = TrackXFor(_stageFocus);
                if (snap || _trackSnap) { _trackX = target; _trackVelocity = 0f; }
                else if (dt > 0f) _trackX = Mathf.SmoothDamp(_trackX, target, ref _trackVelocity, StageSlideTime, Mathf.Infinity, dt);
                _trackSnap = false;
            }
            _stageTrack.style.translate = new Translate(_trackX, 0f);

            // The nearer a card is to the centre, the bigger and brighter it is — continuously, also while dragging.
            float centre = StripWidth * 0.5f;
            for (int i = 0; i < _stageCardEls.Count; i++)
            {
                float cardCentre = _trackX + StageCardStep * i + StageCardStep * 0.5f;
                float d = Mathf.Clamp01(Mathf.Abs(cardCentre - centre) / StageCardStep);
                float k = d * d * (3f - 2f * d);
                float scale = Mathf.Lerp(1f, StageSideScale, k);
                var card = _stageCardEls[i];
                card.style.scale = new Scale(new Vector3(scale, scale, 1f));
                card.style.opacity = Mathf.Lerp(1f, 0.7f, k);
            }
        }

        void OnStagePointerDown(PointerDownEvent e)
        {
            if (_stageViews.Count == 0) return;
            _dragging = true;
            _dragMoved = 0f;
            _dragStartPointer = e.position.x;
            _dragStartTrack = _trackX;
            _trackVelocity = 0f;
            _downCard = CardIndexOf(e.target as VisualElement);
            _stageCards.CapturePointer(e.pointerId);
        }

        void OnStagePointerMove(PointerMoveEvent e)
        {
            if (!_dragging || !_stageCards.HasPointerCapture(e.pointerId)) return;
            float dx = e.position.x - _dragStartPointer;
            _dragMoved = Mathf.Max(_dragMoved, Mathf.Abs(dx));
            _trackX = _dragStartTrack + dx;
        }

        void OnStagePointerUp(PointerUpEvent e)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_stageCards.HasPointerCapture(e.pointerId)) _stageCards.ReleasePointer(e.pointerId);
            if (_dragMoved < 20f)
            {
                // A tap: the centred card opens its stage, a side card slides to the centre.
                if (_downCard < 0) return;
                if (_downCard != _stageFocus) FocusStage(_downCard);
                else if (!_stageViews[_downCard].Locked && !_stageViews[_downCard].Empty) StageClicked?.Invoke(_downCard);
                return;
            }
            // A swipe: the nearest card, and a short flick still turns at least one card.
            float dx = e.position.x - _dragStartPointer;
            int nearest = Mathf.RoundToInt((StripWidth * 0.5f - _trackX) / StageCardStep - 0.5f);
            if (nearest == _stageFocus && Mathf.Abs(dx) > 60f) nearest -= (int)Mathf.Sign(dx);
            FocusStage(nearest);
        }

        int CardIndexOf(VisualElement e)
        {
            for (; e != null && e != _stageCards; e = e.parent)
                if (e.userData is int i && e.ClassListContains("stage-card")) return i;
            return -1;
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
