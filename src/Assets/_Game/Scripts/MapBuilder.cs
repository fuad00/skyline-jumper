using System;
using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Maps;
using StickmanJumper.Presentation;
using StickmanJumper.UI;
using UnityEngine;

namespace StickmanJumper
{
    /// <summary>
    /// Конструктор карт: экран «Мои карты» и редактор. Пальцы на поле редактора: тап — выбрать (повторный — деталь под ней),
    /// тянуть выбранное — двигать (прилипает к краям соседей), тянуть ручку — размер/поворот, тянуть пустое место — двигать
    /// камеру, два пальца — масштаб. Каждая правка сразу сохраняется. Играет карту GameRoot (<see cref="Play"/>).
    /// </summary>
    public sealed class MapBuilder
    {
        /// <summary>Расстояния в точках интерфейса (1080 по высоте): палец «сдвинулся», прилипание, промах по тонкой детали, ручка.</summary>
        const float SlopPoints = 14f, SnapPoints = 14f, SlackPoints = 10f, HandlePoints = 46f;
        const float MinSize = 3f, MaxSize = 320f;

        readonly Camera _cam;
        readonly ArtBank _art;
        readonly PieceCatalog _catalog;
        readonly GameUI _gameUI;
        readonly MapBuilderUI _ui;
        readonly MapStore _store;
        readonly Transform _root;
        readonly Color _dayBackground;

        MapEditor _editor;
        MapEditorView _view;
        List<MapData> _maps = new List<MapData>();
        Vector2 _camCenter;
        float _camSize = 30f;
        bool _dirty;

        enum Gesture { None, Tap, Pan, Move, Handle, Pinch }
        readonly Dictionary<int, Vector2> _pointers = new Dictionary<int, Vector2>();
        Gesture _gesture;
        bool _moveStarted;
        Vector2 _downPos, _lastPos, _downWorld, _pinchWorld;
        float _pinchStartDist, _pinchStartSize;

        /// <summary>Сыграть карту (вторым — из редактора ли: тогда после игры назад в редактор).</summary>
        public Action<MapData, bool> Play;
        /// <summary>Назад в главное меню.</summary>
        public Action Exit;
        /// <summary>«Из уровня»: выбрать уровень оригинала, из которого сделать карту.</summary>
        public Action PickLevel;
        /// <summary>Открылся экран «Мои карты» (за ним снова играет заставка).</summary>
        public Action ListShown;
        public Action Click;

        public bool EditorOpen { get; private set; }
        public bool ListOpen { get; private set; }
        public MapEditor Editor => _editor;
        public MapEditorView View => _view;
        public MapBuilderUI UI => _ui;
        public MapStore Store => _store;
        public IReadOnlyList<MapData> Maps => _maps;
        public Vector2 CameraCenter => _camCenter;
        public float CameraSize => _camSize;

        public MapBuilder(Camera cam, ArtBank art, GameUI gameUI, Color dayBackground, MapStore store = null)
        {
            _cam = cam;
            _art = art;
            _catalog = art.Pieces;
            _gameUI = gameUI;
            _dayBackground = dayBackground;
            _store = store ?? MapStore.OnDevice();
            _root = new GameObject("Map Builder").transform;
            _ui = new MapBuilderUI(art);
            gameUI.AddScreen(UIScreen.Maps, _ui.ListScreen);
            gameUI.AddScreen(UIScreen.MapEditor, _ui.EditorScreen);
            Wire();
        }

        void Wire()
        {
            _ui.ListBack = () => { Click?.Invoke(); Exit?.Invoke(); };
            _ui.NewMap = () => { Click?.Invoke(); NewMap(); };
            _ui.FromLevel = () => { Click?.Invoke(); PickLevel?.Invoke(); };
            _ui.PasteCode = () => { Click?.Invoke(); PasteFromClipboard(); };
            _ui.CardClicked = i => { Click?.Invoke(); MapActions(i); };
            _ui.PasteSubmitted = text => { Click?.Invoke(); if (Import(text)) _ui.HidePaste(); };

            _ui.EditorBack = () => { Click?.Invoke(); CloseEditor(); };
            _ui.Undo = () => { Click?.Invoke(); _editor.Undo(); };
            _ui.Redo = () => { Click?.Invoke(); _editor.Redo(); };
            _ui.ToggleWalls = () =>
            {
                Click?.Invoke();
                _view.ShowSolids = !_view.ShowSolids;
                _ui.SetWalls(_view.ShowSolids);
                _dirty = true;
            };
            _ui.OpenSettings = () => { Click?.Invoke(); _ui.ShowSettings(_editor.Map); };
            _ui.SettingsClosed = () => { Click?.Invoke(); _ui.HideSettings(); };
            _ui.NameChanged = name =>
            {
                if (name == _editor.Map.Name) return;
                _editor.Rename(name);
            };
            _ui.TimeStep = d => { Click?.Invoke(); _editor.SetTimeLimit(_editor.Map.TimeLimit + d * 5f); };
            _ui.WindStep = d => { Click?.Invoke(); _editor.SetWind(_editor.Map.Wind + d * 0.25f); };
            _ui.NightToggled = () => { Click?.Invoke(); _editor.SetNight(!_editor.Map.Night); };
            _ui.Test = () => { Click?.Invoke(); Test(); };
            _ui.Share = () =>
            {
                Click?.Invoke();
                if (!_editor.Map.Verified) _ui.Toast(UIText.PassFirst, 3.2f);
                else ShareDialog(_editor.Map);
            };
            _ui.AddPiece = id => { Click?.Invoke(); AddPiece(id); };
            _ui.FocusStrip = kind => { Click?.Invoke(); FocusStrip(kind); };
            _ui.AddPlatform = () =>
            {
                Click?.Invoke();
                _editor.AddPlatform(_camCenter);
                Reveal(_editor.SelectionBounds());
            };
            _ui.ToolClicked = tool => { Click?.Invoke(); Apply(tool); };

            _ui.PointerDown = OnPointerDown;
            _ui.PointerMove = OnPointerMove;
            _ui.PointerUp = OnPointerUp;
            _ui.Wheel = OnWheel;
        }

        // ------------------------------------------------------------------ «my maps»

        public void ShowList()
        {
            HideEditorView();
            ListOpen = true;
            _ui.HidePaste();
            _gameUI.Show(UIScreen.Maps);
            RefreshList();
            ListShown?.Invoke();
        }

        /// <summary>Ушли из конструктора (в меню, в игру).</summary>
        public void Leave()
        {
            HideEditorView();
            ListOpen = false;
        }

        void RefreshList()
        {
            _maps = _store.All();
            var cards = new List<MapCardView>();
            foreach (var m in _maps)
                cards.Add(new MapCardView
                {
                    Name = m.Name,
                    Map = m,
                    Solids = MapGeometry.Solids(m, _catalog),
                    Passed = m.Verified,
                    FromFriend = m.Imported,
                });
            _ui.SetMaps(cards);
        }

        public MapData NewMap()
        {
            var map = MapLevel.NewMap(_catalog, MapStore.FreeName(_store.All(), UIText.MapWord));
            _store.Save(map);
            OpenEditor(map);
            return map;
        }

        /// <summary>Карта из уровня оригинала (выбран на экранах этапов и уровней).</summary>
        public MapData CreateFromLevel(LevelConfig level, string name)
        {
            var map = MapLevel.FromLevel(level, _catalog, name);
            _store.Save(map);
            OpenEditor(map);
            return map;
        }

        void MapActions(int index)
        {
            if (index < 0 || index >= _maps.Count) return;
            var map = _maps[index];
            _gameUI.ShowDialog(map.Name, UIText.MapActions,
                new DialogButton(null, UIText.Back, SlantButton.Blue, () => { Click?.Invoke(); _gameUI.HideDialog(); }),
                new DialogButton(null, UIText.PlayMapBig, SlantButton.Green, () => { Click?.Invoke(); _gameUI.HideDialog(); Leave(); Play?.Invoke(map, false); }),
                new DialogButton(null, UIText.EditBig, SlantButton.Blue, () => { Click?.Invoke(); _gameUI.HideDialog(); OpenEditor(map); }),
                new DialogButton(null, UIText.ShareBig, SlantButton.Blue, () =>
                {
                    Click?.Invoke();
                    if (map.Verified) ShareDialog(map);
                    else Notify(UIText.PassFirst);
                }),
                new DialogButton(null, UIText.DeleteBig, SlantButton.Red, () => { Click?.Invoke(); AskDelete(map); }));
        }

        void AskDelete(MapData map)
        {
            _gameUI.ShowDialog(UIText.DeleteTitle, UIText.DeleteText(map.Name),
                new DialogButton(UIText.NoSmall, UIText.NoBig, SlantButton.Blue, () => { Click?.Invoke(); _gameUI.HideDialog(); }),
                new DialogButton(null, UIText.DeleteBig, SlantButton.Red, () =>
                {
                    Click?.Invoke();
                    _store.Delete(map.Id);
                    if (_editor != null && _editor.Map.Id == map.Id) _editor = null;
                    _gameUI.HideDialog();
                    RefreshList();
                }));
        }

        // ------------------------------------------------------------------ sharing

        void ShareDialog(MapData map)
        {
            string code = MapCode.Encode(map);
            string message = UIText.ShareMessage(map.Name, code);
            var buttons = new List<DialogButton>
            {
                new DialogButton(null, UIText.Back, SlantButton.Blue, () => { Click?.Invoke(); _gameUI.HideDialog(); }),
                new DialogButton(null, UIText.CopyBig, SlantButton.Blue, () =>
                {
                    Click?.Invoke();
                    DeviceShare.Copy(message);
                    _gameUI.HideDialog();
                    Notify(UIText.CodeCopied);
                }),
            };
            if (DeviceShare.CanSend)
                buttons.Add(new DialogButton(null, UIText.SendBig, SlantButton.Green, () =>
                {
                    Click?.Invoke();
                    _gameUI.HideDialog();
                    DeviceShare.Send(UIText.ShareTitle, message);
                }));
            _gameUI.ShowDialog(UIText.ShareTitle, UIText.ShareText(code.Length), buttons.ToArray());
        }

        void PasteFromClipboard()
        {
            DeviceShare.Paste(text =>
            {
                if (MapCode.Contains(text)) Import(text);
                else _ui.ShowPaste(string.Empty); // nothing to take: paste it by hand
            });
        }

        /// <summary>Добавить карту из кода (или сообщения с кодом). false — кода нет или он испорчен (игроку сказано).</summary>
        public bool Import(string text)
        {
            if (!MapCode.TryDecode(text, out var map, out var error))
            {
                Notify(error == "no code" ? UIText.NoCode : error == "newer version" ? UIText.NewerCode : UIText.BrokenCode);
                return false;
            }
            var taken = new HashSet<string>();
            foreach (var m in _store.All()) taken.Add(m.Name);
            string name = string.IsNullOrWhiteSpace(map.Name) ? UIText.MapWord : map.Name;
            for (int i = 2; taken.Contains(name); i++) name = $"{map.Name} ({i})";
            map.Name = name;
            _store.Save(map);
            RefreshList();
            _gameUI.ShowDialog(UIText.ImportTitle, UIText.ImportText(map.Name, map.Pieces.Count, map.AuthorVerified),
                new DialogButton(null, UIText.OkBig, SlantButton.Blue, () => { Click?.Invoke(); _gameUI.HideDialog(); }),
                new DialogButton(null, UIText.PlayMapBig, SlantButton.Green, () => { Click?.Invoke(); _gameUI.HideDialog(); Leave(); Play?.Invoke(map, false); }));
            return true;
        }

        /// <summary>Сообщение игроку: в редакторе — всплывающая строка, в списке — окошко.</summary>
        void Notify(string text)
        {
            if (EditorOpen) _ui.Toast(text, 3f);
            else _gameUI.ShowDialog(string.Empty, text, new DialogButton(null, UIText.OkBig, SlantButton.Blue, () => { Click?.Invoke(); _gameUI.HideDialog(); }));
        }

        // ------------------------------------------------------------------ editor

        public void OpenEditor(MapData map)
        {
            if (_editor == null || _editor.Map.Id != map.Id)
            {
                _editor = new MapEditor(map, _catalog);
                _editor.Changed += OnChanged;
                _editor.SelectionChanged += RefreshTools;
                FitCamera();
            }
            _view ??= new MapEditorView(_root, _art);
            _view.SetActive(true);
            EditorOpen = true;
            ListOpen = false;
            _gameUI.HideDialog();
            _gameUI.Show(UIScreen.MapEditor);
            _ui.HideSettings();
            _ui.SetWalls(_view.ShowSolids);
            RefreshTitle();
            RefreshTools();
            ApplyCamera();
            _dirty = true;
            Sync();
        }

        /// <summary>Назад в редактор после проверки карты.</summary>
        public void Resume()
        {
            if (_editor == null) ShowList();
            else OpenEditor(_editor.Map);
        }

        public void CloseEditor()
        {
            if (_editor != null)
            {
                _ui.HideSettings();
                _store.Save(_editor.Map, touch: false);
            }
            ShowList();
        }

        void HideEditorView()
        {
            EditorOpen = false;
            _view?.SetActive(false);
            _pointers.Clear();
            _gesture = Gesture.None;
        }

        void OnChanged()
        {
            _store.Save(_editor.Map);
            RefreshTitle();
            if (_ui.SettingsVisible) _ui.SetSettings(_editor.Map);
            _dirty = true;
        }

        void RefreshTitle()
        {
            var map = _editor.Map;
            _ui.SetTitle(map.Name, map.Verified, map.Imported);
            _ui.SetUndo(_editor.CanUndo, _editor.CanRedo);
            _cam.backgroundColor = map.Night ? Color.black : _dayBackground;
        }

        void RefreshTools()
        {
            var sel = MapBuilderUI.Selection.None;
            if (_editor.Strip == StripKind.Platform) sel = MapBuilderUI.Selection.Platform;
            else if (_editor.Strip != StripKind.None) sel = MapBuilderUI.Selection.StartOrTarget;
            else if (_editor.Selected.Count == 1) sel = MapBuilderUI.Selection.Piece;
            else if (_editor.Selected.Count > 1) sel = MapBuilderUI.Selection.Pieces;
            _ui.SetTools(sel, _editor.SelectionSolid, _editor.SelectedStrip?.Left ?? false);
            _dirty = true;
        }

        /// <summary>Сыграть: сначала проверка, что старт и цель годятся.</summary>
        void Test()
        {
            var problems = MapLevel.Problems(_editor.Map);
            if (problems.Contains("start"))
            {
                _ui.Toast(UIText.StartTooShort, 3f);
                FocusStrip(StripKind.Start);
                return;
            }
            if (problems.Contains("target"))
            {
                _ui.Toast(UIText.TargetTooNarrow, 3f);
                FocusStrip(StripKind.Target);
                return;
            }
            _store.Save(_editor.Map, touch: false);
            HideEditorView();
            Play?.Invoke(_editor.Map, true);
        }

        /// <summary>Автор сел на флажок своей карты: её такую можно отправлять друзьям.</summary>
        public void MarkPassed(MapData map)
        {
            bool before = map.Verified;
            map.VerifiedHash = MapCode.ContentHash(map);
            _store.Save(map, touch: false);
            if (!before && _editor != null && _editor.Map.Id == map.Id) _pendingToast = UIText.Passed;
        }

        string _pendingToast;

        void AddPiece(string id)
        {
            int i = _editor.Add(id, _camCenter);
            if (i >= 0) Reveal(_editor.SelectionBounds());
        }

        void FocusStrip(StripKind kind)
        {
            _editor.SelectStrip(kind);
            var b = _editor.SelectionBounds();
            if (_camSize > 30f) _camSize = 30f;
            _camCenter = new Vector2(b.center.x, b.center.y - _camSize * 0.14f); // above the palette
        }

        void Apply(MapBuilderUI.Tool tool)
        {
            switch (tool)
            {
                case MapBuilderUI.Tool.CopyRight: _editor.Duplicate(above: false); Reveal(_editor.SelectionBounds()); break;
                case MapBuilderUI.Tool.CopyUp: _editor.Duplicate(above: true); Reveal(_editor.SelectionBounds()); break;
                case MapBuilderUI.Tool.Building: _editor.SelectConnected(); break;
                case MapBuilderUI.Tool.Flip: _editor.Flip(); break;
                case MapBuilderUI.Tool.Front: _editor.ShiftLayer(1); break;
                case MapBuilderUI.Tool.Behind: _editor.ShiftLayer(-1); break;
                case MapBuilderUI.Tool.Solid: _editor.ToggleSolid(); break;
                case MapBuilderUI.Tool.Reset: _editor.ResetTransform(); break;
                case MapBuilderUI.Tool.Delete: _editor.Delete(); break;
                case MapBuilderUI.Tool.Direction: _editor.ToggleDirection(); break;
            }
            RefreshTools();
        }

        /// <summary>Кнопка «назад» телефона / Esc. false — конструктор её не обработал.</summary>
        public bool Back()
        {
            if (_ui.PasteVisible)
            {
                _ui.HidePaste();
                return true;
            }
            if (EditorOpen)
            {
                if (_ui.SettingsVisible) _ui.HideSettings();
                else if (_editor.HasSelection) _editor.ClearSelection();
                else CloseEditor();
                return true;
            }
            if (ListOpen)
            {
                Exit?.Invoke();
                return true;
            }
            return false;
        }

        public void Update(float realDt)
        {
            _ui.Update();
            if (!EditorOpen) return;
            if (_pendingToast != null)
            {
                _ui.Toast(_pendingToast, 3.5f);
                _pendingToast = null;
            }
            ApplyCamera();
            Sync();
        }

        void Sync()
        {
            if (_view == null || _editor == null) return;
            if (_dirty)
            {
                _view.Sync(_editor, _cam, MetresPerPoint);
                _dirty = false;
            }
            else _view.UpdateCamera(_editor, _cam, MetresPerPoint);
        }

        // ------------------------------------------------------------------ camera

        void ApplyCamera()
        {
            _camSize = Mathf.Clamp(_camSize, MinSize, MaxSize);
            _cam.orthographicSize = _camSize;
            var t = _cam.transform;
            t.position = new Vector3(_camCenter.x, _camCenter.y, t.position.z);
        }

        /// <summary>Вся карта в кадре: старт, цель, платформы и детали рядом.</summary>
        void FitCamera()
        {
            var map = _editor.Map;
            var area = Rect.MinMaxRect(Mathf.Min(map.Start.X, map.Target.X), 0f, Mathf.Max(map.Start.XMax, map.Target.XMax), Mathf.Max(map.Start.Y, map.Target.Y) + 4f);
            foreach (var s in map.Platforms) area = Rect.MinMaxRect(Mathf.Min(area.xMin, s.X), 0f, Mathf.Max(area.xMax, s.XMax), Mathf.Max(area.yMax, s.Y + 4f));
            area = Rect.MinMaxRect(area.xMin - 6f, -2f, area.xMax + 6f, area.yMax + 4f);
            Fit(area);
        }

        void Fit(Rect area)
        {
            float aspect = Mathf.Max(0.5f, _cam.aspect);
            // The palette covers the bottom quarter, the top bar a bit: keep the area in the free middle.
            // Free from about 27 % to 87 % of the screen height: the area's half-height is 0.58 of the camera's.
            _camSize = Mathf.Clamp(Mathf.Max(area.height * 0.5f / 0.58f, area.width * 0.5f / aspect / 0.92f), MinSize, MaxSize);
            _camCenter = new Vector2(area.center.x, area.center.y - _camSize * 0.14f);
        }

        /// <summary>Если выбранное не видно — показать его.</summary>
        void Reveal(Rect r)
        {
            float h = _camSize, w = h * _cam.aspect;
            var view = Rect.MinMaxRect(_camCenter.x - w, _camCenter.y - h * 0.5f, _camCenter.x + w, _camCenter.y + h * 0.75f);
            if (view.Contains(r.min) && view.Contains(r.max)) return;
            if (r.width > w * 1.6f || r.height > h * 1.1f) Fit(r);
            else _camCenter = new Vector2(r.center.x, r.center.y + _camSize * 0.12f);
        }

        Vector2 PanelSize()
        {
            var panel = _ui.Canvas.panel;
            if (panel == null) return new Vector2(1920f, 1080f);
            var size = panel.visualTree.layout.size;
            return size.x > 1f && size.y > 1f ? size : new Vector2(1920f, 1080f);
        }

        float MetresPerPoint => _camSize * 2f / PanelSize().y;

        /// <summary>Точка интерфейса (от левого верхнего угла) → мир.</summary>
        public Vector2 ToWorld(Vector2 panel)
        {
            var size = PanelSize();
            float k = MetresPerPoint;
            return new Vector2(_camCenter.x + (panel.x - size.x * 0.5f) * k, _camCenter.y - (panel.y - size.y * 0.5f) * k);
        }

        /// <summary>Мир → точка интерфейса (для автотеста).</summary>
        public Vector2 ToPanel(Vector2 world)
        {
            var size = PanelSize();
            float k = MetresPerPoint;
            return new Vector2((world.x - _camCenter.x) / k + size.x * 0.5f, -(world.y - _camCenter.y) / k + size.y * 0.5f);
        }

        // ------------------------------------------------------------------ fingers

        public void OnPointerDown(int id, Vector2 pos)
        {
            if (!EditorOpen) return;
            _pointers[id] = pos;
            if (_pointers.Count == 1)
            {
                _downPos = _lastPos = pos;
                _downWorld = ToWorld(pos);
                _moveStarted = false;
                float k = MetresPerPoint;
                var handle = _editor.HandleAt(_downWorld, HandlePoints * k, MapEditorView.RotateGapPx * k);
                if (handle != Handle.None)
                {
                    _gesture = Gesture.Handle;
                    _editor.BeginDrag(MapEditor.DragFor(handle), _downWorld);
                }
                else _gesture = OnSelection(_downWorld) ? Gesture.Move : Gesture.Tap;
            }
            else if (_pointers.Count == 2)
            {
                if (_editor.Dragging != DragKind.None)
                {
                    _editor.CancelDrag();
                    _dirty = true;
                }
                _gesture = Gesture.Pinch;
                var (a, b) = TwoPointers();
                _pinchStartDist = Mathf.Max(10f, Vector2.Distance(a, b));
                _pinchStartSize = _camSize;
                _pinchWorld = ToWorld((a + b) * 0.5f);
            }
        }

        public void OnPointerMove(int id, Vector2 pos)
        {
            if (!EditorOpen || !_pointers.ContainsKey(id)) return;
            _pointers[id] = pos;
            switch (_gesture)
            {
                case Gesture.Pinch when _pointers.Count >= 2:
                {
                    var (a, b) = TwoPointers();
                    _camSize = Mathf.Clamp(_pinchStartSize * _pinchStartDist / Mathf.Max(10f, Vector2.Distance(a, b)), MinSize, MaxSize);
                    _camCenter += _pinchWorld - ToWorld((a + b) * 0.5f); // the point between the fingers stays under them
                    break;
                }
                case Gesture.Tap:
                    if ((pos - _downPos).magnitude > SlopPoints) _gesture = Gesture.Pan;
                    goto case Gesture.Pan;
                case Gesture.Pan:
                    if (_gesture == Gesture.Pan)
                    {
                        float k = MetresPerPoint;
                        _camCenter -= new Vector2((pos.x - _lastPos.x) * k, -(pos.y - _lastPos.y) * k);
                    }
                    break;
                case Gesture.Move:
                    if (!_moveStarted && (pos - _downPos).magnitude > SlopPoints)
                    {
                        _moveStarted = true;
                        _editor.BeginDrag(_editor.Strip != StripKind.None ? DragKind.StripMove : DragKind.Move, _downWorld);
                    }
                    if (_moveStarted)
                    {
                        _editor.Drag(ToWorld(pos), SnapPoints * MetresPerPoint);
                        _dirty = true;
                    }
                    break;
                case Gesture.Handle:
                    _editor.Drag(ToWorld(pos), SnapPoints * MetresPerPoint);
                    _dirty = true;
                    break;
            }
            _lastPos = pos;
        }

        public void OnPointerUp(int id, Vector2 pos)
        {
            if (!_pointers.Remove(id) || !EditorOpen) return;
            switch (_gesture)
            {
                case Gesture.Tap:
                    _editor.SelectAt(ToWorld(pos), SlackPoints * MetresPerPoint);
                    break;
                case Gesture.Move:
                    if (_moveStarted) _editor.EndDrag();
                    else _editor.SelectAt(ToWorld(pos), SlackPoints * MetresPerPoint); // a tap on the selected: the one under it
                    break;
                case Gesture.Handle:
                    _editor.EndDrag();
                    break;
                case Gesture.Pinch:
                    if (_pointers.Count == 1)
                    {
                        // One finger left: keep moving the view with it.
                        foreach (var p in _pointers.Values) _lastPos = p;
                        _gesture = Gesture.Pan;
                        return;
                    }
                    break;
            }
            _dirty = true;
            if (_pointers.Count == 0) _gesture = Gesture.None;
        }

        void OnWheel(Vector2 pos, float delta)
        {
            if (!EditorOpen || Mathf.Approximately(delta, 0f)) return;
            var before = ToWorld(pos);
            _camSize = Mathf.Clamp(_camSize * (delta > 0f ? 1.12f : 1f / 1.12f), MinSize, MaxSize);
            _camCenter += before - ToWorld(pos);
        }

        (Vector2, Vector2) TwoPointers()
        {
            Vector2 a = default, b = default;
            int n = 0;
            foreach (var p in _pointers.Values)
            {
                if (n == 0) a = p;
                else if (n == 1) b = p;
                n++;
            }
            return (a, b);
        }

        /// <summary>Палец лёг на уже выбранное — значит, его будут тянуть.</summary>
        bool OnSelection(Vector2 world)
        {
            if (!_editor.HasSelection) return false;
            float slack = SlackPoints * MetresPerPoint;
            if (_editor.Strip != StripKind.None)
                return _editor.StripAt(world, slack, out int index) == _editor.Strip && (_editor.Strip != StripKind.Platform || index == _editor.StripIndex);
            if (_editor.Selected.Count > 1) return _editor.SelectionBounds().Contains(world);
            return _editor.PiecesAt(world, slack).Contains(_editor.Selected[0]);
        }
    }
}
