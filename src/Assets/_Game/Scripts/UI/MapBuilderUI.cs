using System;
using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Maps;
using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.UI
{
    /// <summary>Что показать на карточке карты.</summary>
    public struct MapCardView
    {
        public string Name;
        public MapData Map;
        public List<Rect> Solids;
        public bool Passed, FromFriend;
        public int Stars;
    }

    /// <summary>
    /// Экраны конструктора карт (в стиле игры): «Мои карты» — карточки с мини-картой и нижняя панель; редактор — поле
    /// для пальцев на весь экран, сверху — назад, имя, отмена/возврат, стены, настройки, поделиться, «Проверить»,
    /// снизу — палитра деталей по разделам, над ней — действия с выбранным. Логики здесь нет: всё уходит в Action-ы.
    /// </summary>
    public sealed class MapBuilderUI
    {
        public enum Tool { CopyRight, CopyUp, Building, Flip, Front, Behind, Solid, Reset, Delete, Direction }

        /// <summary>Что выбрано — от этого зависят кнопки действий.</summary>
        public enum Selection { None, Piece, Pieces, Platform, StartOrTarget }

        const float TapSlop = 22f;
        static readonly Color ChipGreen = new Color32(84, 178, 82, 255);
        static readonly Color ChipGrey = new Color32(120, 130, 140, 255);
        static readonly Color ChipBlue = new Color32(37, 140, 205, 255);

        readonly ArtBank _art;
        readonly PieceCatalog _catalog;

        public readonly VisualElement ListScreen, EditorScreen, Canvas;

        readonly ScrollView _cards;
        readonly Label _empty;
        readonly VisualElement _listBar;

        readonly Label _name, _status;
        readonly VisualElement _statusChip;
        readonly IconButton _undo, _redo, _walls, _share, _test;
        readonly VisualElement _tabs, _tools;
        readonly ScrollView _items;
        readonly Label _toolHint;
        readonly VisualElement _toast;
        readonly Label _toastText;
        float _toastUntil = -1f;

        readonly VisualElement _settings, _pastePanel;
        readonly TextField _nameField, _pasteField;
        readonly Label _timeValue, _windValue, _nightValue;
        int _tab;

        // List.
        public Action ListBack, NewMap, FromLevel, PasteCode;
        public Action<int> CardClicked;
        // Editor.
        public Action EditorBack, Undo, Redo, ToggleWalls, OpenSettings, Test, Share, AddPlatform;
        public Action<string> AddPiece;
        public Action<StripKind> FocusStrip;
        public Action<Tool> ToolClicked;
        public Action<string> NameChanged;
        public Action<int> TimeStep, WindStep;
        public Action NightToggled, SettingsClosed;
        public Action<string> PasteSubmitted;
        public Action<int, Vector2> PointerDown, PointerMove, PointerUp;
        public Action<Vector2, float> Wheel;

        public bool SettingsVisible => _settings.style.display == DisplayStyle.Flex;
        public bool PasteVisible => _pastePanel.style.display == DisplayStyle.Flex;

        public MapBuilderUI(ArtBank art)
        {
            _art = art;
            _catalog = art.Pieces;

            // ---------------------------------------------------------------- «my maps»
            ListScreen = new VisualElement { pickingMode = PickingMode.Ignore };
            ListScreen.AddToClassList("screen");
            var heading = new VisualElement { pickingMode = PickingMode.Ignore };
            heading.AddToClassList("heading");
            heading.Add(Text(UIText.MapsSmall, "sticker", "sticker--small"));
            heading.Add(Text(UIText.MapsBig, "sticker"));
            heading.name = "maps-heading";
            ListScreen.Add(heading);
            _cards = new ScrollView(ScrollViewMode.Horizontal);
            _cards.AddToClassList("map-cards");
            _cards.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _cards.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _cards.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            ListScreen.Add(_cards);
            _empty = Text(string.Empty, "maps-empty");
            ListScreen.Add(_empty);
            _listBar = new VisualElement { pickingMode = PickingMode.Ignore };
            _listBar.AddToClassList("bar");
            Plate.Dress(_listBar, Plate.Kind.Dark, 24f, 7f);
            ListScreen.Add(_listBar);

            // ---------------------------------------------------------------- editor
            EditorScreen = new VisualElement { pickingMode = PickingMode.Ignore };
            EditorScreen.AddToClassList("screen");
            Canvas = new VisualElement();
            Canvas.AddToClassList("ed-canvas");
            Canvas.RegisterCallback<PointerDownEvent>(e =>
            {
                Canvas.CapturePointer(e.pointerId);
                PointerDown?.Invoke(e.pointerId, e.position);
            });
            Canvas.RegisterCallback<PointerMoveEvent>(e => { if (Canvas.HasPointerCapture(e.pointerId)) PointerMove?.Invoke(e.pointerId, e.position); });
            Canvas.RegisterCallback<PointerUpEvent>(e => EndPointer(e.pointerId, e.position));
            Canvas.RegisterCallback<PointerCancelEvent>(e => EndPointer(e.pointerId, e.position));
            Canvas.RegisterCallback<WheelEvent>(e => Wheel?.Invoke(e.mousePosition, e.delta.y));
            EditorScreen.Add(Canvas);

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("ed-top");
            top.Add(new IconButton(IconKind.Back, null, () => EditorBack?.Invoke()));
            var title = new VisualElement { pickingMode = PickingMode.Ignore };
            title.AddToClassList("ed-title");
            _name = Text(string.Empty, "ed-name");
            title.Add(_name);
            _statusChip = new VisualElement { pickingMode = PickingMode.Ignore };
            _statusChip.AddToClassList("map-chip");
            _status = Text(string.Empty, "map-chip-text");
            _statusChip.Add(_status);
            title.Add(_statusChip);
            top.Add(title);
            _undo = new IconButton(IconKind.Undo, UIText.Undo, () => Undo?.Invoke());
            _redo = new IconButton(IconKind.Redo, UIText.Redo, () => Redo?.Invoke());
            _walls = new IconButton(IconKind.Walls, UIText.Physics, () => ToggleWalls?.Invoke());
            var gear = new IconButton(IconKind.Gear, UIText.SettingsWord, () => OpenSettings?.Invoke());
            _share = new IconButton(IconKind.Share, UIText.ShareWord, () => Share?.Invoke());
            _test = new IconButton(IconKind.Play, UIText.Test, () => Test?.Invoke(), wide: true);
            foreach (var b in new[] { _undo, _redo, _walls, gear, _share, _test }) top.Add(b);
            EditorScreen.Add(top);

            _tools = new VisualElement { pickingMode = PickingMode.Ignore };
            _tools.AddToClassList("ed-tools");
            EditorScreen.Add(_tools);
            _toolHint = Text(string.Empty, "ed-hint");

            var palette = new VisualElement();
            palette.AddToClassList("ed-palette");
            Plate.Dress(palette, Plate.Kind.Dark, 26f, 6f);
            _tabs = new VisualElement { pickingMode = PickingMode.Ignore };
            _tabs.AddToClassList("ed-tabs");
            palette.Add(_tabs);
            _items = new ScrollView(ScrollViewMode.Horizontal);
            _items.AddToClassList("ed-items");
            _items.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _items.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _items.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            palette.Add(_items);
            EditorScreen.Add(palette);

            _toast = new VisualElement { pickingMode = PickingMode.Ignore };
            _toast.AddToClassList("ed-toast");
            Plate.Dress(_toast, Plate.Kind.Dark, 22f, 5f);
            _toastText = Text(string.Empty, "ed-toast-text");
            _toast.Add(_toastText);
            EditorScreen.Add(_toast);
            Display(_toast, false);

            // ---------------------------------------------------------------- map settings
            _settings = Overlay(out var card);
            card.Add(Text(UIText.MapSettings, "dialog-title"));
            _nameField = new TextField { maxLength = 40 };
            _nameField.AddToClassList("ed-field");
            _nameField.RegisterCallback<FocusOutEvent>(_ => NameChanged?.Invoke(_nameField.value));
            card.Add(Row(UIText.NameWord, _nameField));
            _timeValue = Text(string.Empty, "ed-value");
            card.Add(Row(UIText.TimeWord, Stepper(_timeValue, d => TimeStep?.Invoke(d))));
            _windValue = Text(string.Empty, "ed-value");
            card.Add(Row(UIText.WindWord, Stepper(_windValue, d => WindStep?.Invoke(d))));
            _nightValue = Text(string.Empty, "ed-value");
            var night = new VisualElement();
            night.AddToClassList("ed-toggle");
            night.AddToClassList("clickable");
            Plate.Dress(night, Plate.Kind.LightShadow, 20f, 5f, UIShapes.Blue);
            night.Add(_nightValue);
            OnTap(night, () => NightToggled?.Invoke());
            card.Add(Row(UIText.NightWord, night));
            var settingsBar = new VisualElement { pickingMode = PickingMode.Ignore };
            settingsBar.AddToClassList("dialog-bar");
            settingsBar.Add(new SlantButton(null, UIText.DoneBig, SlantButton.Green, () =>
            {
                NameChanged?.Invoke(_nameField.value);
                SettingsClosed?.Invoke();
            }, first: true, last: true));
            card.Add(settingsBar);
            EditorScreen.Add(_settings);

            // ---------------------------------------------------------------- paste a code by hand
            _pastePanel = Overlay(out var pasteCard);
            pasteCard.Add(Text(UIText.PasteTitle, "dialog-title"));
            pasteCard.Add(Text(UIText.PasteHelp, "dialog-text"));
            _pasteField = new TextField { multiline = true, maxLength = 20000 };
            _pasteField.AddToClassList("ed-field");
            _pasteField.AddToClassList("ed-field--big");
            pasteCard.Add(_pasteField);
            var pasteBar = new VisualElement { pickingMode = PickingMode.Ignore };
            pasteBar.AddToClassList("dialog-bar");
            pasteBar.Add(new SlantButton(UIText.NoSmall, UIText.NoBig, SlantButton.Blue, HidePaste, first: true));
            pasteBar.Add(new SlantButton(null, UIText.AddBig, SlantButton.Green, () => PasteSubmitted?.Invoke(_pasteField.value), last: true));
            pasteCard.Add(pasteBar);
            ListScreen.Add(_pastePanel);

            ShowTab(0);
            SetTools(Selection.None, false, false);
        }

        void EndPointer(int id, Vector2 position)
        {
            if (!Canvas.HasPointerCapture(id)) return;
            Canvas.ReleasePointer(id);
            PointerUp?.Invoke(id, position);
        }

        // ------------------------------------------------------------------ «my maps»

        public void SetMaps(IReadOnlyList<MapCardView> maps)
        {
            _cards.Clear();
            for (int i = 0; i < maps.Count; i++)
            {
                var v = maps[i];
                int index = i;
                var card = new VisualElement();
                card.AddToClassList("map-card");
                card.AddToClassList("clickable");
                Plate.Dress(card, Plate.Kind.LightShadow, 30f, 6f, UIShapes.Blue);
                var preview = new MapPreview(v.Map, v.Solids);
                preview.AddToClassList("map-preview");
                card.Add(preview);
                card.Add(Text(v.Name, "map-name"));
                var chip = new VisualElement { pickingMode = PickingMode.Ignore };
                chip.AddToClassList("map-chip");
                chip.style.backgroundColor = v.Passed ? ChipGreen : v.FromFriend ? ChipBlue : ChipGrey;
                chip.Add(Text(v.Passed ? UIText.StatusPassed : v.FromFriend ? UIText.StatusFriend : UIText.StatusDraft, "map-chip-text"));
                card.Add(chip);
                OnTap(card, () => CardClicked?.Invoke(index));
                _cards.Add(card);
            }
            _empty.text = UIText.NoMaps;
            Display(_empty, maps.Count == 0);

            // Bottom bar like the other menus (labels follow the language: rebuilt every time).
            for (int i = _listBar.childCount - 1; i >= 1; i--) _listBar.RemoveAt(i);
            _listBar.Add(new SlantButton(null, UIText.Back, SlantButton.Red, () => ListBack?.Invoke(), first: true));
            _listBar.Add(new SlantButton(UIText.NewMapSmall, UIText.NewMapBig, SlantButton.Green, () => NewMap?.Invoke()));
            _listBar.Add(new SlantButton(UIText.FromLevelSmall, UIText.FromLevelBig, SlantButton.Blue, () => FromLevel?.Invoke()));
            _listBar.Add(new SlantButton(UIText.PasteSmall, UIText.PasteBig, SlantButton.Blue, () => PasteCode?.Invoke()));
            _listBar.Add(SlantButton.Blank(last: true));
        }

        public void ShowPaste(string text)
        {
            _pasteField.value = text ?? string.Empty;
            Display(_pastePanel, true);
        }

        public void HidePaste() => Display(_pastePanel, false);

        // ------------------------------------------------------------------ editor

        public void SetTitle(string name, bool passed, bool fromFriend)
        {
            _name.text = name;
            _status.text = passed ? UIText.StatusPassed : fromFriend ? UIText.StatusFriend : UIText.StatusDraft;
            _statusChip.style.backgroundColor = passed ? ChipGreen : fromFriend ? ChipBlue : ChipGrey;
            _share.Dimmed = !passed;
        }

        public void SetUndo(bool canUndo, bool canRedo)
        {
            _undo.SetEnabled(canUndo);
            _redo.SetEnabled(canRedo);
        }

        public void SetWalls(bool on) => _walls.On = on;

        /// <summary>Кнопки действий над палитрой — под то, что выбрано.</summary>
        public void SetTools(Selection selection, bool solid, bool runLeft)
        {
            _tools.Clear();
            switch (selection)
            {
                case Selection.Piece:
                case Selection.Pieces:
                    AddTool(IconKind.CopyRight, UIText.CopyRight, Tool.CopyRight);
                    AddTool(IconKind.CopyUp, UIText.CopyUp, Tool.CopyUp);
                    AddTool(IconKind.Building, UIText.WholeBuilding, Tool.Building);
                    AddTool(IconKind.Flip, UIText.Flip, Tool.Flip);
                    AddTool(IconKind.Front, UIText.Closer, Tool.Front);
                    AddTool(IconKind.Behind, UIText.Farther, Tool.Behind);
                    AddTool(solid ? IconKind.Solid : IconKind.Backdrop, solid ? UIText.SolidWord : UIText.BackdropWord, Tool.Solid);
                    if (selection == Selection.Piece) AddTool(IconKind.Reset, UIText.ResetShape, Tool.Reset);
                    AddTool(IconKind.Trash, UIText.DeleteWord, Tool.Delete, danger: true);
                    break;
                case Selection.Platform:
                    AddTool(runLeft ? IconKind.RunLeft : IconKind.RunRight, runLeft ? UIText.RunLeft : UIText.RunRight, Tool.Direction);
                    AddTool(IconKind.Trash, UIText.DeleteWord, Tool.Delete, danger: true);
                    break;
                case Selection.StartOrTarget:
                    _toolHint.text = UIText.StripHint;
                    var box = new VisualElement { pickingMode = PickingMode.Ignore };
                    box.AddToClassList("ed-hint-box");
                    Plate.Dress(box, Plate.Kind.LightShadow, 22f, 5f, new Color32(196, 230, 244, 255));
                    box.Add(_toolHint);
                    _tools.Add(box);
                    break;
            }
            Display(_tools, selection != Selection.None);
        }

        void AddTool(IconKind icon, string label, Tool tool, bool danger = false)
        {
            var b = new IconButton(icon, label, () => ToolClicked?.Invoke(tool), small: true);
            if (danger) b.Fill = new Color32(255, 150, 140, 255);
            _tools.Add(b);
        }

        /// <summary>Раздел палитры: 0–3 — детали (дома, стройка, улица, небо), 4 — площадки.</summary>
        public void SelectTab(int tab) => ShowTab(Mathf.Clamp(tab, 0, 4));

        void ShowTab(int tab)
        {
            _tab = tab;
            _tabs.Clear();
            var names = new[] { UIText.GroupName(PieceGroup.Buildings), UIText.GroupName(PieceGroup.Construction), UIText.GroupName(PieceGroup.Street), UIText.GroupName(PieceGroup.Sky), UIText.StripsTab };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var t = new VisualElement();
                t.AddToClassList("ed-tab");
                t.AddToClassList("clickable");
                Plate.Dress(t, Plate.Kind.Light, 18f, 4f, i == tab ? UIShapes.Blue : new Color32(90, 100, 110, 255));
                var l = Text(names[i], "ed-tab-text");
                if (i != tab) l.style.color = new Color(0.92f, 0.94f, 0.96f);
                t.Add(l);
                OnTap(t, () => ShowTab(index));
                _tabs.Add(t);
            }

            _items.Clear();
            _items.scrollOffset = Vector2.zero;
            if (tab < 4)
            {
                if (_catalog == null) return;
                // Big ready-made buildings first, thin borders and small parts at the end.
                var defs = new List<PieceDef>();
                foreach (var d in _catalog.Pieces)
                    if (!d.Hidden && (int)d.Group == tab && d.Sprite != null) defs.Add(d);
                defs.Sort((a, b) => (b.OpaquePx.width * b.OpaquePx.height).CompareTo(a.OpaquePx.width * a.OpaquePx.height));
                foreach (var def in defs)
                {
                    string id = def.Id;
                    var item = new VisualElement();
                    item.AddToClassList("ed-item");
                    item.AddToClassList("clickable");
                    Plate.Dress(item, Plate.Kind.Light, 18f, 4f, new Color32(236, 240, 242, 255));
                    var pic = new VisualElement { pickingMode = PickingMode.Ignore };
                    pic.AddToClassList("ed-item-pic");
                    pic.style.backgroundImage = new StyleBackground(def.Sprite);
                    item.Add(pic);
                    item.name = id;
                    OnTap(item, () => AddPiece?.Invoke(id));
                    _items.Add(item);
                }
                return;
            }
            AddStripItem(IconKind.Start, UIText.StartWord, () => FocusStrip?.Invoke(StripKind.Start), new Color(0.16f, 0.55f, 0.96f));
            AddStripItem(IconKind.Target, UIText.TargetWord, () => FocusStrip?.Invoke(StripKind.Target), new Color(0.86f, 0.1f, 0.1f));
            AddStripItem(IconKind.Platform, UIText.PlatformNew, () => AddPlatform?.Invoke(), new Color(1f, 0.55f, 0.1f));
        }

        void AddStripItem(IconKind icon, string label, Action onTap, Color ink)
        {
            var item = new VisualElement();
            item.AddToClassList("ed-item");
            item.AddToClassList("ed-item--wide");
            item.AddToClassList("clickable");
            Plate.Dress(item, Plate.Kind.Light, 18f, 4f, new Color32(236, 240, 242, 255));
            var i = new VectorIcon(icon) { Ink = ink };
            i.AddToClassList("ed-item-icon");
            item.Add(i);
            item.Add(Text(label, "ed-item-text"));
            OnTap(item, onTap);
            _items.Add(item);
        }

        /// <summary>Короткое сообщение посередине сверху (само исчезает).</summary>
        public void Toast(string text, float seconds = 2.6f)
        {
            _toastText.text = text;
            Display(_toast, true);
            _toastUntil = Time.realtimeSinceStartup + seconds;
        }

        public void Update()
        {
            if (_toastUntil > 0f && Time.realtimeSinceStartup > _toastUntil)
            {
                _toastUntil = -1f;
                Display(_toast, false);
            }
        }

        public void ShowSettings(MapData map)
        {
            _nameField.value = map.Name;
            SetSettings(map);
            Display(_settings, true);
        }

        public void SetSettings(MapData map)
        {
            _timeValue.text = UIText.Seconds(map.TimeLimit);
            _windValue.text = UIText.WindSpeed(map.Wind);
            _nightValue.text = map.Night ? UIText.YesWord : UIText.NoWord;
        }

        public void HideSettings() => Display(_settings, false);

        // ------------------------------------------------------------------ helpers

        static void Display(VisualElement e, bool show) => e.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        static Label Text(string text, params string[] classes)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        /// <summary>Тап (а не прокрутка ленты): палец отпущен недалеко от места нажатия.</summary>
        static void OnTap(VisualElement e, Action action)
        {
            Vector2 down = Vector2.zero;
            bool pressed = false;
            e.RegisterCallback<PointerDownEvent>(ev => { pressed = true; down = ev.position; });
            e.RegisterCallback<PointerUpEvent>(ev =>
            {
                if (!pressed) return;
                pressed = false;
                if (((Vector2)ev.position - down).magnitude <= TapSlop) action();
            });
            e.RegisterCallback<PointerLeaveEvent>(_ => pressed = false);
        }

        VisualElement Overlay(out VisualElement card)
        {
            var overlay = new VisualElement();
            overlay.AddToClassList("screen");
            overlay.AddToClassList("screen--dim");
            overlay.AddToClassList("dialog");
            card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.AddToClassList("dialog-card");
            card.AddToClassList("ed-card");
            Plate.Dress(card, Plate.Kind.Dark, 40f, 8f);
            overlay.Add(card);
            Display(overlay, false);
            return overlay;
        }

        static VisualElement Row(string label, VisualElement value)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("ed-row");
            row.Add(Text(label, "ed-row-label"));
            row.Add(value);
            return row;
        }

        static VisualElement Stepper(Label value, Action<int> step)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList("ed-stepper");
            box.Add(StepButton("–", () => step(-1)));
            box.Add(value);
            box.Add(StepButton("+", () => step(1)));
            return box;
        }

        static VisualElement StepButton(string sign, Action onTap)
        {
            var b = new VisualElement();
            b.AddToClassList("ed-step");
            b.AddToClassList("clickable");
            Plate.Dress(b, Plate.Kind.LightShadow, 20f, 5f, UIShapes.Blue);
            b.Add(Text(sign, "ed-step-text"));
            OnTap(b, onTap);
            return b;
        }

        // ------------------------------------------------------------------ widgets

        /// <summary>Кнопка-значок в стиле HUD: голубая плашка, значок и (если есть) подпись под ним.</summary>
        sealed class IconButton : VisualElement
        {
            readonly Plate _plate;
            readonly VectorIcon _icon;
            bool _on, _dimmed;

            public IconButton(IconKind icon, string label, Action onTap, bool wide = false, bool small = false)
            {
                AddToClassList("ed-btn");
                AddToClassList("clickable");
                if (wide) AddToClassList("ed-btn--wide");
                if (small) AddToClassList("ed-btn--small");
                _plate = Plate.Dress(this, Plate.Kind.LightShadow, 22f, 5f, wide ? (Color)UIShapes.Green : UIShapes.Blue);
                _icon = new VectorIcon(icon);
                _icon.AddToClassList("ed-btn-icon");
                Add(_icon);
                if (!string.IsNullOrEmpty(label)) Add(Text(label, "ed-btn-text"));
                OnTap(this, () => { if (enabledSelf) onTap?.Invoke(); });
            }

            public Color Fill
            {
                set { _plate.Fill = value; _plate.MarkDirtyRepaint(); }
            }

            /// <summary>Включённый переключатель — тёмная плашка и белый значок.</summary>
            public bool On
            {
                get => _on;
                set
                {
                    _on = value;
                    _plate.Fill = value ? (Color)new Color32(37, 120, 180, 255) : UIShapes.Blue;
                    _icon.Ink = value ? Color.white : UIShapes.Ink;
                    _plate.MarkDirtyRepaint();
                }
            }

            /// <summary>Нажать можно, но действие пока недоступно (объяснит всплывающая подсказка).</summary>
            public bool Dimmed
            {
                get => _dimmed;
                set { _dimmed = value; style.opacity = value ? 0.55f : 1f; }
            }
        }

        /// <summary>Мини-карта на карточке: здания, старт (синий), цель (красный), платформы (оранжевые).</summary>
        sealed class MapPreview : VisualElement
        {
            readonly MapData _map;
            readonly List<Rect> _solids;

            public MapPreview(MapData map, List<Rect> solids)
            {
                _map = map;
                _solids = solids ?? new List<Rect>();
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext ctx)
            {
                var full = new Rect(0f, 0f, resolvedStyle.width, resolvedStyle.height);
                if (full.width < 4f || full.height < 4f) return;
                var p = ctx.painter2D;
                UIShapes.FillRoundRect(p, full, 18f, _map.Night ? new Color(0.05f, 0.05f, 0.08f) : new Color(0.93f, 0.92f, 0.88f));
                var r = UIShapes.Inset(full, 12f);
                float x0 = Mathf.Min(_map.Start.X, _map.Target.X), x1 = Mathf.Max(_map.Start.XMax, _map.Target.XMax), y1 = Mathf.Max(_map.Start.Y, _map.Target.Y) + 3f;
                foreach (var s in _solids)
                {
                    x0 = Mathf.Min(x0, s.xMin);
                    x1 = Mathf.Max(x1, s.xMax);
                    y1 = Mathf.Max(y1, s.yMax);
                }
                foreach (var s in _map.Platforms)
                {
                    x0 = Mathf.Min(x0, s.X);
                    x1 = Mathf.Max(x1, s.XMax);
                }
                var world = Rect.MinMaxRect(x0 - 3f, -1f, x1 + 3f, y1 + 2f);
                float k = Mathf.Min(r.width / world.width, r.height / world.height);
                Vector2 off = new Vector2((r.width - world.width * k) * 0.5f, (r.height - world.height * k) * 0.5f);
                Vector2 M(float x, float y) => new Vector2(r.x + off.x + (x - world.xMin) * k, r.y + r.height - (off.y + (y - world.yMin) * k));

                void Box(Rect b, Color c)
                {
                    var a = M(b.xMin, b.yMin);
                    var d = M(b.xMax, b.yMax);
                    p.fillColor = c;
                    p.BeginPath();
                    p.MoveTo(new Vector2(a.x, a.y));
                    p.LineTo(new Vector2(d.x, a.y));
                    p.LineTo(new Vector2(d.x, d.y));
                    p.LineTo(new Vector2(a.x, d.y));
                    p.ClosePath();
                    p.Fill();
                }

                var ink = _map.Night ? new Color(0.55f, 0.58f, 0.66f) : new Color(0.2f, 0.22f, 0.26f, 0.85f);
                foreach (var s in _solids) Box(s, ink);
                float t = 0.9f / k + 0.6f;
                Box(new Rect(_map.Start.X, _map.Start.Y, _map.Start.Width, t), new Color(0.16f, 0.55f, 0.96f));
                foreach (var s in _map.Platforms) Box(new Rect(s.X, s.Y, s.Width, t), new Color(1f, 0.55f, 0.1f));
                Box(new Rect(_map.Target.X, _map.Target.Y, _map.Target.Width, t), new Color(0.9f, 0.12f, 0.12f));
            }
        }
    }
}
