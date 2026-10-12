using System;
using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Maps
{
    /// <summary>Какая площадка выбрана в редакторе.</summary>
    public enum StripKind { None, Start, Target, Platform }

    /// <summary>Ручка выбранной детали или площадки.</summary>
    public enum Handle { None, Scale, Width, Height, Rotate, StripLeft, StripRight }

    /// <summary>Что делает палец, который тянут.</summary>
    public enum DragKind { None, Move, Scale, Width, Height, Rotate, StripMove, StripLeft, StripRight }

    /// <summary>
    /// Правка карты без картинки (её рисует MapEditorView, пальцы читает MapBuilder): выбор, перетаскивание с магнитом к краям
    /// соседних деталей, ручки масштаба/ширины/высоты/поворота, копии, слои, площадки, отмена и повтор.
    /// Каждая законченная правка — один шаг отмены и событие <see cref="Changed"/> (карта сохраняется).
    /// </summary>
    public sealed class MapEditor
    {
        const int UndoLimit = 80;
        const float MinScale = 0.1f, MaxScale = 30f;
        /// <summary>Прилипаем к соседям только если они не дальше этого (зазор), м.</summary>
        const float Near = 2f;
        /// <summary>Поворот прилипает к 0°, 90°, 180°, −90°, если до них меньше этого, градусы.</summary>
        const float RotationSnap = 4f;
        /// <summary>Новая промежуточная платформа, м.</summary>
        public const float NewPlatformWidth = 6f;

        readonly PieceCatalog _catalog;
        readonly List<string> _undo = new List<string>(), _redo = new List<string>();
        string _before;

        // Drag state.
        DragKind _drag;
        Vector2 _dragStart;
        readonly List<MapPiece> _dragOrigin = new List<MapPiece>();
        MapStrip _stripOrigin;

        public MapData Map { get; private set; }
        public PieceCatalog Catalog => _catalog;

        /// <summary>Выбранные детали (номера в Map.Pieces).</summary>
        public readonly List<int> Selected = new List<int>();
        public StripKind Strip { get; private set; }
        public int StripIndex { get; private set; } = -1;

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public DragKind Dragging => _drag;

        /// <summary>Карта изменилась (сохранить, перерисовать).</summary>
        public event Action Changed;
        /// <summary>Выбор изменился (панель действий).</summary>
        public event Action SelectionChanged;

        public MapEditor(MapData map, PieceCatalog catalog)
        {
            Map = map;
            _catalog = catalog;
        }

        // ------------------------------------------------------------------ selection

        public bool HasSelection => Selected.Count > 0 || Strip != StripKind.None;
        public bool SinglePiece => Selected.Count == 1 && Strip == StripKind.None;
        public MapPiece SelectedPiece => SinglePiece ? Map.Pieces[Selected[0]] : null;

        /// <summary>Выбранная площадка (null — не площадка).</summary>
        public MapStrip SelectedStrip => StripOf(Strip, StripIndex);

        MapStrip StripOf(StripKind kind, int index) => kind switch
        {
            StripKind.Start => Map.Start,
            StripKind.Target => Map.Target,
            StripKind.Platform when index >= 0 && index < Map.Platforms.Count => Map.Platforms[index],
            _ => null,
        };

        public void ClearSelection()
        {
            if (!HasSelection) return;
            Selected.Clear();
            Strip = StripKind.None;
            StripIndex = -1;
            SelectionChanged?.Invoke();
        }

        public void SelectPieces(IEnumerable<int> pieces)
        {
            Selected.Clear();
            Strip = StripKind.None;
            StripIndex = -1;
            foreach (int i in pieces)
                if (i >= 0 && i < Map.Pieces.Count && !Selected.Contains(i)) Selected.Add(i);
            SelectionChanged?.Invoke();
        }

        public void SelectStrip(StripKind kind, int index = -1)
        {
            Selected.Clear();
            Strip = kind;
            StripIndex = kind == StripKind.Platform ? index : -1;
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Тап по карте: площадка (они поверх всего) или самая верхняя деталь под пальцем. Повторный тап туда же выбирает
        /// следующую деталь под ней — так добираются до задних. Мимо всего — выбор снимается. true — что-то выбрано.
        /// </summary>
        public bool SelectAt(Vector2 world, float slack)
        {
            var strip = StripAt(world, slack, out int stripIndex);
            if (strip != StripKind.None)
            {
                SelectStrip(strip, stripIndex);
                return true;
            }
            var hits = PiecesAt(world, slack);
            if (hits.Count == 0)
            {
                ClearSelection();
                return false;
            }
            int pick = hits[0];
            if (SinglePiece && hits.Contains(Selected[0])) pick = hits[(hits.IndexOf(Selected[0]) + 1) % hits.Count];
            SelectPieces(new[] { pick });
            return true;
        }

        /// <summary>Детали под точкой, сверху вниз (как нарисованы).</summary>
        public List<int> PiecesAt(Vector2 world, float slack)
        {
            var hits = new List<int>();
            foreach (int i in DrawOrder())
            {
                var p = Map.Pieces[i];
                if (_catalog != null && _catalog.TryGet(p.Art, out var def) && MapGeometry.Hit(def, p, world, slack)) hits.Add(i);
            }
            hits.Reverse();
            return hits;
        }

        /// <summary>Порядок рисования: по слою, при равном — по месту в списке.</summary>
        public List<int> DrawOrder()
        {
            var order = new List<int>(Map.Pieces.Count);
            for (int i = 0; i < Map.Pieces.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int la = Map.Pieces[a].Layer, lb = Map.Pieces[b].Layer;
                return la != lb ? la.CompareTo(lb) : a.CompareTo(b);
            });
            return order;
        }

        /// <summary>Площадка под пальцем: полоса и флажок над ней.</summary>
        public StripKind StripAt(Vector2 world, float slack, out int index)
        {
            index = -1;
            if (InStrip(Map.Target, world, slack)) return StripKind.Target;
            for (int i = Map.Platforms.Count - 1; i >= 0; i--)
                if (InStrip(Map.Platforms[i], world, slack))
                {
                    index = i;
                    return StripKind.Platform;
                }
            return InStrip(Map.Start, world, slack) ? StripKind.Start : StripKind.None;
        }

        static bool InStrip(MapStrip s, Vector2 p, float slack) =>
            p.x >= s.X - slack && p.x <= s.XMax + slack && p.y >= s.Y - 0.6f - slack && p.y <= s.Y + 2.6f + slack;

        /// <summary>Рамка выбранного (непрозрачные части деталей или площадка с флажком).</summary>
        public Rect SelectionBounds()
        {
            var s = SelectedStrip;
            if (s != null) return Rect.MinMaxRect(s.X, s.Y - 0.3f, s.XMax, s.Y + 2.6f);
            bool any = false;
            Rect r = default;
            foreach (int i in Selected)
            {
                var b = Bounds(i);
                r = any ? Union(r, b) : b;
                any = true;
            }
            return r;
        }

        Rect Bounds(int i)
        {
            var p = Map.Pieces[i];
            return _catalog != null && _catalog.TryGet(p.Art, out var def) ? MapGeometry.OpaqueBounds(def, p) : new Rect(p.X - 0.5f, p.Y - 0.5f, 1f, 1f);
        }

        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        /// <summary>
        /// «Всё здание»: к выбранной детали добавляются все, что касаются её и друг друга (этажи, бортики, стёкла) —
        /// кроме неба, а улица — только если выбрана уличная деталь.
        /// </summary>
        public void SelectConnected()
        {
            if (Selected.Count == 0) return;
            bool street = Selected.Exists(i => GroupOf(i) == PieceGroup.Street);
            var set = new List<int>(Selected);
            for (int k = 0; k < set.Count; k++)
            {
                var a = Bounds(set[k]);
                for (int j = 0; j < Map.Pieces.Count; j++)
                {
                    if (set.Contains(j)) continue;
                    var g = GroupOf(j);
                    if (g == PieceGroup.Sky || g == PieceGroup.Street && !street) continue;
                    var b = Bounds(j);
                    if (b.xMin <= a.xMax + 0.15f && b.xMax >= a.xMin - 0.15f && b.yMin <= a.yMax + 0.15f && b.yMax >= a.yMin - 0.15f) set.Add(j);
                }
            }
            SelectPieces(set);
        }

        PieceGroup GroupOf(int i) => _catalog != null && _catalog.TryGet(Map.Pieces[i].Art, out var def) ? def.Group : PieceGroup.Street;

        // ------------------------------------------------------------------ handles

        /// <summary>Где рисовать ручки выбранного (rotateGap — вынос ручки поворота над деталью, м).</summary>
        public List<(Handle handle, Vector2 at)> Handles(float rotateGap)
        {
            var list = new List<(Handle, Vector2)>();
            var s = SelectedStrip;
            if (s != null)
            {
                list.Add((Handle.StripLeft, new Vector2(s.X, s.Y)));
                list.Add((Handle.StripRight, new Vector2(s.XMax, s.Y)));
                return list;
            }
            var p = SelectedPiece;
            if (p == null || _catalog == null || !_catalog.TryGet(p.Art, out var def)) return list;
            var o = def.OpaquePx;
            Vector2 W(Vector2 px) => PictureShapes.ToWorld(px, def.SizePx, p.Position, p.Scale, p.Rotation);
            list.Add((Handle.Scale, W(o.max)));
            list.Add((Handle.Width, W(new Vector2(o.xMax, o.center.y))));
            list.Add((Handle.Height, W(new Vector2(o.center.x, o.yMax))));
            Vector2 top = W(new Vector2(o.center.x, o.yMax)), up = (top - W(o.center)).normalized;
            if (up.sqrMagnitude < 0.5f) up = Vector2.up;
            list.Add((Handle.Rotate, top + up * rotateGap));
            return list;
        }

        public Handle HandleAt(Vector2 world, float radius, float rotateGap)
        {
            Handle best = Handle.None;
            float bestD = radius;
            foreach (var (h, at) in Handles(rotateGap))
            {
                float d = Vector2.Distance(world, at);
                if (d <= bestD)
                {
                    best = h;
                    bestD = d;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ dragging

        /// <summary>Начать тянуть: ручку (если палец на ней) или выбранное целиком.</summary>
        public void BeginDrag(DragKind kind, Vector2 world)
        {
            if (kind == DragKind.None || !HasSelection) return;
            BeginChange();
            _drag = kind;
            _dragStart = world;
            _dragOrigin.Clear();
            foreach (int i in Selected) _dragOrigin.Add(Map.Pieces[i].Clone());
            _stripOrigin = SelectedStrip?.Clone();
        }

        public static DragKind DragFor(Handle h) => h switch
        {
            Handle.Scale => DragKind.Scale,
            Handle.Width => DragKind.Width,
            Handle.Height => DragKind.Height,
            Handle.Rotate => DragKind.Rotate,
            Handle.StripLeft => DragKind.StripLeft,
            Handle.StripRight => DragKind.StripRight,
            _ => DragKind.None,
        };

        /// <summary>Палец сдвинулся. snap — на сколько метров прилипать к краям (≈ 12 точек экрана).</summary>
        public void Drag(Vector2 world, float snap)
        {
            switch (_drag)
            {
                case DragKind.Move: MovePieces(world - _dragStart, snap); break;
                case DragKind.Scale:
                case DragKind.Width:
                case DragKind.Height: ScalePiece(world); break;
                case DragKind.Rotate: RotatePiece(world); break;
                case DragKind.StripMove: MoveStrip(world - _dragStart, snap); break;
                case DragKind.StripLeft:
                case DragKind.StripRight: ResizeStrip(world, snap); break;
            }
        }

        public void EndDrag()
        {
            if (_drag == DragKind.None) return;
            _drag = DragKind.None;
            EndChange();
        }

        /// <summary>Отменить начатое перетаскивание (второй палец лёг — это щипок, а не перенос).</summary>
        public void CancelDrag()
        {
            if (_drag == DragKind.None) return;
            for (int k = 0; k < Selected.Count && k < _dragOrigin.Count; k++) Map.Pieces[Selected[k]] = _dragOrigin[k].Clone();
            var s = SelectedStrip;
            if (s != null && _stripOrigin != null) Copy(_stripOrigin, s);
            _drag = DragKind.None;
            _before = null;
        }

        void MovePieces(Vector2 delta, float snap)
        {
            for (int k = 0; k < Selected.Count; k++)
                Map.Pieces[Selected[k]].Position = _dragOrigin[k].Position + delta;
            Vector2 fix = SnapOffset(SelectionBounds(), snap);
            if (fix == Vector2.zero) return;
            foreach (int i in Selected) Map.Pieces[i].Position += fix;
        }

        /// <summary>Сдвиг, чтобы рамка g прилипла к краям соседних деталей и к улице (по каждой оси — ближайший край в пределах snap).</summary>
        Vector2 SnapOffset(Rect g, float snap)
        {
            float bestX = snap + 1f, bestY = snap + 1f, dx = 0f, dy = 0f;
            void TryX(float from, float to) { float d = to - from; if (Mathf.Abs(d) < Mathf.Abs(bestX) && Mathf.Abs(d) <= snap) { bestX = d; dx = d; } }
            void TryY(float from, float to) { float d = to - from; if (Mathf.Abs(d) < Mathf.Abs(bestY) && Mathf.Abs(d) <= snap) { bestY = d; dy = d; } }

            TryY(g.yMin, 0f); // stand on the street
            for (int j = 0; j < Map.Pieces.Count; j++)
            {
                if (Selected.Contains(j)) continue;
                var b = Bounds(j);
                float gapY = Mathf.Max(b.yMin - g.yMax, g.yMin - b.yMax);
                float gapX = Mathf.Max(b.xMin - g.xMax, g.xMin - b.xMax);
                if (gapY <= Near)
                {
                    TryX(g.xMin, b.xMin); TryX(g.xMin, b.xMax);
                    TryX(g.xMax, b.xMax); TryX(g.xMax, b.xMin);
                    TryX(g.center.x, b.center.x);
                }
                if (gapX <= Near)
                {
                    TryY(g.yMin, b.yMax); TryY(g.yMin, b.yMin);
                    TryY(g.yMax, b.yMax); TryY(g.yMax, b.yMin);
                }
            }
            return new Vector2(dx, dy);
        }

        void ScalePiece(Vector2 world)
        {
            if (Selected.Count != 1 || _catalog == null) return;
            var p = Map.Pieces[Selected[0]];
            var o0 = _dragOrigin[0];
            if (!_catalog.TryGet(p.Art, out var def)) return;
            var o = def.OpaquePx;
            // The opposite side stays where it was.
            Vector2 anchorPx = _drag switch
            {
                DragKind.Width => new Vector2(o.xMin, o.center.y),
                DragKind.Height => new Vector2(o.center.x, o.yMin),
                _ => o.min,
            };
            Vector2 anchor = PictureShapes.ToWorld(anchorPx, def.SizePx, o0.Position, o0.Scale, o0.Rotation);
            float a = o0.Rotation * Mathf.Deg2Rad;
            Vector2 axisX = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Sign(o0.ScaleX), axisY = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * Mathf.Sign(o0.ScaleY);
            Vector2 from = _dragStart - anchor, to = world - anchor;
            float sx = 1f, sy = 1f;
            switch (_drag)
            {
                case DragKind.Width: sx = Ratio(Vector2.Dot(to, axisX), Vector2.Dot(from, axisX)); break;
                case DragKind.Height: sy = Ratio(Vector2.Dot(to, axisY), Vector2.Dot(from, axisY)); break;
                default: sx = sy = Ratio(to.magnitude, from.magnitude); break;
            }
            p.ScaleX = NiceScale(o0.ScaleX * sx, def.Scale);
            p.ScaleY = NiceScale(o0.ScaleY * sy, def.Scale);
            p.Position = o0.Position;
            Vector2 moved = PictureShapes.ToWorld(anchorPx, def.SizePx, p.Position, p.Scale, p.Rotation);
            p.Position += anchor - moved;
        }

        static float Ratio(float now, float start) => Mathf.Abs(start) < 0.05f ? 1f : Mathf.Max(0.02f, now / start);

        /// <summary>Масштаб в разумных пределах; рядом с обычным для детали — ровно обычный.</summary>
        static float NiceScale(float s, float usual)
        {
            float sign = s < 0f ? -1f : 1f, m = Mathf.Clamp(Mathf.Abs(s), MinScale, MaxScale);
            usual = Mathf.Abs(usual) > 0.01f ? Mathf.Abs(usual) : 1f;
            if (Mathf.Abs(m - usual) / usual < 0.04f) m = usual;
            return sign * Mathf.Round(m * 1000f) / 1000f;
        }

        void RotatePiece(Vector2 world)
        {
            if (Selected.Count != 1) return;
            var p = Map.Pieces[Selected[0]];
            var o0 = _dragOrigin[0];
            Vector2 c = o0.Position;
            float a0 = Mathf.Atan2(_dragStart.y - c.y, _dragStart.x - c.x) * Mathf.Rad2Deg;
            float a1 = Mathf.Atan2(world.y - c.y, world.x - c.x) * Mathf.Rad2Deg;
            float r = Mathf.DeltaAngle(0f, o0.Rotation + Mathf.DeltaAngle(a0, a1));
            for (int k = -2; k <= 2; k++)
                if (Mathf.Abs(Mathf.DeltaAngle(r, k * 90f)) < RotationSnap) r = Mathf.DeltaAngle(0f, k * 90f);
            p.Rotation = Mathf.Round(r * 10f) / 10f;
        }

        void MoveStrip(Vector2 delta, float snap)
        {
            var s = SelectedStrip;
            if (s == null || _stripOrigin == null) return;
            s.X = _stripOrigin.X + delta.x;
            s.Y = _stripOrigin.Y + delta.y;
            // Roofs pull harder than piece edges: the strip should lie exactly on the surface.
            var solids = MapGeometry.Solids(Map, _catalog);
            float bestY = snap * 2f + 1f, bestX = snap + 1f, dy = 0f, dx = 0f;
            if (Mathf.Abs(s.Y) <= snap * 2f) { bestY = Mathf.Abs(s.Y); dy = -s.Y; }
            foreach (var r in solids)
            {
                if (r.xMax > s.X && r.xMin < s.XMax && Mathf.Abs(r.yMax - s.Y) < bestY) { bestY = Mathf.Abs(r.yMax - s.Y); dy = r.yMax - s.Y; }
                if (Mathf.Abs(r.yMax - s.Y) > Near) continue;
                foreach (var (from, to) in new[] { (s.XMax, r.xMax), (s.X, r.xMin), (s.XMax, r.xMin), (s.X, r.xMax) })
                    if (Mathf.Abs(to - from) < bestX) { bestX = Mathf.Abs(to - from); dx = to - from; }
            }
            s.X += dx;
            s.Y += dy;
        }

        void ResizeStrip(Vector2 world, float snap)
        {
            var s = SelectedStrip;
            if (s == null || _stripOrigin == null) return;
            float min = Strip == StripKind.Start ? MapLevel.MinStartWidth : MapLevel.MinTargetWidth;
            float edge = world.x;
            // Ends pull to the edges of the roof under the strip.
            float best = snap + 1f;
            foreach (var r in MapGeometry.Solids(Map, _catalog))
            {
                if (Mathf.Abs(r.yMax - s.Y) > 0.3f) continue;
                foreach (float e in new[] { r.xMin, r.xMax })
                    if (Mathf.Abs(e - world.x) < best) { best = Mathf.Abs(e - world.x); edge = e; }
            }
            if (_drag == DragKind.StripLeft)
            {
                float right = _stripOrigin.XMax;
                s.X = Mathf.Min(edge, right - min);
                s.Width = right - s.X;
            }
            else
            {
                s.X = _stripOrigin.X;
                s.Width = Mathf.Max(min, edge - s.X);
            }
        }

        // ------------------------------------------------------------------ actions (each one is an undo step)

        /// <summary>
        /// Новая деталь в точке at (дома и всё уличное встают на то, что под ними, или на улицу) — сразу выбрана.
        /// </summary>
        public int Add(string art, Vector2 at)
        {
            if (_catalog == null || !_catalog.TryGet(art, out var def)) return -1;
            BeginChange();
            var p = new MapPiece
            {
                Art = art,
                ScaleX = def.Scale > 0.01f ? def.Scale : 1f,
                ScaleY = def.Scale > 0.01f ? def.Scale : 1f,
                Layer = def.Layer,
                Solid = def.Solid,
                Position = at,
            };
            Map.Pieces.Add(p);
            int index = Map.Pieces.Count - 1;
            if (def.Group != PieceGroup.Sky)
            {
                var b = MapGeometry.OpaqueBounds(def, p);
                float ground = 0f;
                for (int j = 0; j < index; j++)
                {
                    var o = Bounds(j);
                    if (Map.Pieces[j].Solid && o.xMax > b.xMin && o.xMin < b.xMax && o.yMax <= b.center.y) ground = Mathf.Max(ground, o.yMax);
                }
                p.Y += ground - b.yMin;
            }
            Selected.Clear();
            Selected.Add(index);
            Strip = StripKind.None;
            EndChange();
            SelectionChanged?.Invoke();
            return index;
        }

        /// <summary>Копия выбранного: сбоку (вправо) или сверху — удобно строить этажи и ряды.</summary>
        public void Duplicate(bool above)
        {
            if (Selected.Count == 0) return;
            BeginChange();
            var b = SelectionBounds();
            Vector2 shift = above ? new Vector2(0f, b.height) : new Vector2(b.width, 0f);
            var copies = new List<int>();
            foreach (int i in Selected)
            {
                var c = Map.Pieces[i].Clone();
                c.Position += shift;
                Map.Pieces.Add(c);
                copies.Add(Map.Pieces.Count - 1);
            }
            Selected.Clear();
            Selected.AddRange(copies);
            EndChange();
            SelectionChanged?.Invoke();
        }

        /// <summary>Отразить слева направо (группу — целиком, относительно её середины).</summary>
        public void Flip()
        {
            if (Selected.Count == 0) return;
            BeginChange();
            float mid = SelectionBounds().center.x;
            foreach (int i in Selected)
            {
                var p = Map.Pieces[i];
                p.ScaleX = -p.ScaleX;
                p.Rotation = Mathf.DeltaAngle(0f, -p.Rotation);
                p.X = 2f * mid - p.X;
                if (_catalog != null && _catalog.TryGet(p.Art, out var def) && Selected.Count == 1)
                {
                    // Off-centre pictures: keep the visible part in place.
                    float now = MapGeometry.OpaqueBounds(def, p).center.x;
                    p.X += mid - now;
                }
            }
            EndChange();
        }

        /// <summary>Ближе (+1) или дальше (−1) по слоям.</summary>
        public void ShiftLayer(int step)
        {
            if (Selected.Count == 0) return;
            BeginChange();
            foreach (int i in Selected) Map.Pieces[i].Layer = Mathf.Clamp(Map.Pieces[i].Layer + step, -5, 9);
            EndChange();
        }

        /// <summary>Твёрдая ↔ фон (для группы: если хоть одна твёрдая — все станут фоном).</summary>
        public void ToggleSolid()
        {
            if (Selected.Count == 0) return;
            BeginChange();
            bool any = Selected.Exists(i => Map.Pieces[i].Solid);
            foreach (int i in Selected) Map.Pieces[i].Solid = !any;
            EndChange();
        }

        public bool SelectionSolid => Selected.Exists(i => Map.Pieces[i].Solid);

        /// <summary>Сбросить масштаб и поворот выбранной детали к обычным.</summary>
        public void ResetTransform()
        {
            var p = SelectedPiece;
            if (p == null || _catalog == null || !_catalog.TryGet(p.Art, out var def)) return;
            BeginChange();
            var before = MapGeometry.OpaqueBounds(def, p);
            float s = def.Scale > 0.01f ? def.Scale : 1f;
            p.ScaleX = Mathf.Sign(p.ScaleX) * s;
            p.ScaleY = s;
            p.Rotation = 0f;
            var after = MapGeometry.OpaqueBounds(def, p);
            p.Position += new Vector2(before.center.x - after.center.x, before.yMin - after.yMin);
            EndChange();
        }

        /// <summary>Удалить выбранное (старт и цель не удаляются — они нужны всегда).</summary>
        public void Delete()
        {
            if (Strip == StripKind.Platform && StripIndex >= 0 && StripIndex < Map.Platforms.Count)
            {
                BeginChange();
                Map.Platforms.RemoveAt(StripIndex);
                EndChange();
                ClearSelection();
                return;
            }
            if (Selected.Count == 0) return;
            BeginChange();
            var order = new List<int>(Selected);
            order.Sort();
            for (int k = order.Count - 1; k >= 0; k--) Map.Pieces.RemoveAt(order[k]);
            EndChange();
            ClearSelection();
        }

        public bool CanDelete => Selected.Count > 0 || Strip == StripKind.Platform;

        /// <summary>Новая промежуточная платформа в точке (на поверхность под ней, если она рядом) — сразу выбрана.</summary>
        public void AddPlatform(Vector2 at)
        {
            BeginChange();
            var s = new MapStrip(at.x - NewPlatformWidth * 0.5f, at.y, NewPlatformWidth, at.x > Map.Target.CenterX);
            MapLevel.Settle(s, MapGeometry.Solids(Map, _catalog));
            Map.Platforms.Add(s);
            EndChange();
            SelectStrip(StripKind.Platform, Map.Platforms.Count - 1);
        }

        /// <summary>Промежуточная платформа: бежать влево ↔ вправо.</summary>
        public void ToggleDirection()
        {
            if (Strip != StripKind.Platform || SelectedStrip == null) return;
            BeginChange();
            SelectedStrip.Left = !SelectedStrip.Left;
            EndChange();
        }

        public void SetTimeLimit(float seconds) => Set(() => Map.TimeLimit = Mathf.Clamp(Mathf.Round(seconds), MapData.MinTime, MapData.MaxTime));
        public void SetWind(float wind) => Set(() => Map.Wind = Mathf.Clamp(Mathf.Round(wind * 4f) / 4f, -MapData.MaxWind, MapData.MaxWind));
        public void SetNight(bool night) => Set(() => Map.Night = night);
        public void Rename(string name) => Set(() => Map.Name = string.IsNullOrWhiteSpace(name) ? Map.Name : name.Trim());

        void Set(Action change)
        {
            BeginChange();
            change();
            EndChange();
        }

        // ------------------------------------------------------------------ undo

        public void Undo() => Step(_undo, _redo);
        public void Redo() => Step(_redo, _undo);

        void Step(List<string> from, List<string> to)
        {
            if (from.Count == 0) return;
            to.Add(JsonUtility.ToJson(Map));
            string json = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            Restore(json);
            Selected.Clear();
            Strip = StripKind.None;
            StripIndex = -1;
            Changed?.Invoke();
            SelectionChanged?.Invoke();
        }

        void Restore(string json)
        {
            string id = Map.Id, verified = Map.VerifiedHash;
            JsonUtility.FromJsonOverwrite(json, Map);
            Map.Id = id;
            Map.VerifiedHash = verified; // the stamp says which content was passed; it is not undone
        }

        void BeginChange()
        {
            if (_before == null) _before = JsonUtility.ToJson(Map);
        }

        void EndChange()
        {
            if (_before == null) return;
            string after = JsonUtility.ToJson(Map);
            if (after != _before)
            {
                _undo.Add(_before);
                if (_undo.Count > UndoLimit) _undo.RemoveAt(0);
                _redo.Clear();
                Changed?.Invoke();
            }
            _before = null;
        }

        static void Copy(MapStrip from, MapStrip to)
        {
            to.X = from.X;
            to.Y = from.Y;
            to.Width = from.Width;
            to.Left = from.Left;
        }
    }
}
