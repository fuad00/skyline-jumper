using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.EditorTools;
using StickmanJumper.Gameplay;
using StickmanJumper.Maps;
using UnityEditor;
using UnityEngine;

namespace StickmanJumper.Tests
{
    /// <summary>Редактор карт: формат, код для обмена, сохранение, превращение в уровень, правка.</summary>
    public class MapTests
    {
        PieceCatalog _catalog;

        /// <summary>Детали для тестов: куб 1 м, башня 10 × 20 м, облако (не твёрдое).</summary>
        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<PieceCatalog>();
            _catalog.Pieces = new[]
            {
                Def("box", 44f, 44f, PieceGroup.Buildings, true),
                Def("tower", 440f, 880f, PieceGroup.Buildings, true),
                Def("cloud", 220f, 88f, PieceGroup.Sky, false),
            };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_catalog);

        static PieceDef Def(string id, float w, float h, PieceGroup group, bool solid) => new PieceDef
        {
            Id = id,
            Group = group,
            SizePx = new Vector2(w, h),
            OpaquePx = new Rect(0f, 0f, w, h),
            Solid = solid,
            Scale = 1f,
        };

        /// <summary>Башня-старт (крыша 20 м, край в x = 0) и куб-цель вдали; можно играть.</summary>
        MapData Sample()
        {
            var map = new MapData { Name = "Тест", TimeLimit = 40f, Wind = 0.5f };
            map.Pieces.Add(new MapPiece { Art = "tower", X = -5f, Y = 10f, ScaleX = 1f, ScaleY = 1f });
            map.Pieces.Add(new MapPiece { Art = "tower", X = 30f, Y = 2.5f, ScaleX = 1f, ScaleY = 0.25f });
            map.Pieces.Add(new MapPiece { Art = "cloud", X = 12f, Y = 30f, ScaleX = -1.5f, ScaleY = 1.5f, Rotation = 12.5f, Layer = 2, Solid = false });
            map.Start = new MapStrip(-10f, 20f, 10f);
            map.Target = new MapStrip(27f, 5f, 6f);
            map.Platforms.Add(new MapStrip(50f, 3f, 5f, true));
            return map;
        }

        // ------------------------------------------------------------------ share code

        [Test]
        public void Code_RoundTrip_KeepsTheMap()
        {
            var map = Sample();
            string code = MapCode.Encode(map);
            StringAssert.StartsWith(MapCode.Prefix, code);
            Assert.IsTrue(MapCode.TryDecode(code, out var back, out var error), error);

            Assert.AreEqual(map.Name, back.Name);
            Assert.AreEqual(map.TimeLimit, back.TimeLimit, 0.01f);
            Assert.AreEqual(map.Wind, back.Wind, 0.01f);
            Assert.AreEqual(map.Pieces.Count, back.Pieces.Count);
            for (int i = 0; i < map.Pieces.Count; i++)
            {
                MapPiece a = map.Pieces[i], b = back.Pieces[i];
                Assert.AreEqual(a.Art, b.Art);
                Assert.AreEqual(a.X, b.X, 0.006f);
                Assert.AreEqual(a.Y, b.Y, 0.006f);
                Assert.AreEqual(a.ScaleX, b.ScaleX, 0.001f);
                Assert.AreEqual(a.ScaleY, b.ScaleY, 0.001f);
                Assert.AreEqual(a.Rotation, b.Rotation, 0.06f);
                Assert.AreEqual(a.Layer, b.Layer);
                Assert.AreEqual(a.Solid, b.Solid);
            }
            Assert.AreEqual(map.Target.X, back.Target.X, 0.006f);
            Assert.AreEqual(map.Platforms[0].Left, back.Platforms[0].Left);
            Assert.AreEqual(MapCode.ContentHash(map), MapCode.ContentHash(back), "the same content gives the same stamp");
            Assert.IsTrue(back.Imported);
            Assert.IsFalse(back.AuthorVerified, "the author has not passed it");
        }

        /// <summary>Код вставили вместе с текстом сообщения, а мессенджер ещё и перенёс строку.</summary>
        [Test]
        public void Code_FoundInsideAMessage()
        {
            string code = MapCode.Encode(Sample());
            string message = "Лови карту! " + code.Substring(0, 30) + "\n" + code.Substring(30) + " — пройдёшь?";
            Assert.IsTrue(MapCode.TryDecode(message, out var map, out var error), error);
            Assert.AreEqual(3, map.Pieces.Count);
        }

        [Test]
        public void Code_Garbage_IsRejected()
        {
            Assert.IsFalse(MapCode.TryDecode("привет", out _, out _));
            Assert.IsFalse(MapCode.TryDecode(MapCode.Prefix + "AAAAAAAAAAAAAAAA", out _, out _));
            Assert.IsFalse(MapCode.TryDecode(MapCode.Encode(Sample()).Substring(0, 40), out _, out _));
        }

        /// <summary>Карта в 80 деталей помещается в одно сообщение Telegram (4096 знаков) с большим запасом.</summary>
        [Test]
        public void Code_IsShort()
        {
            var map = Sample();
            var rng = new System.Random(3);
            for (int i = 0; i < 80; i++)
                map.Pieces.Add(new MapPiece { Art = i % 2 == 0 ? "box" : "tower", X = (float)rng.NextDouble() * 300f, Y = (float)rng.NextDouble() * 100f, ScaleX = 2f, ScaleY = 2f });
            int length = MapCode.Encode(map).Length;
            Assert.Less(length, 2000, $"{length} chars");
        }

        [Test]
        public void Verified_OnlyWhilePassedContentIsUnchanged()
        {
            var map = Sample();
            Assert.IsFalse(map.Verified);
            map.VerifiedHash = MapCode.ContentHash(map);
            Assert.IsTrue(map.Verified);

            Assert.IsTrue(MapCode.TryDecode(MapCode.Encode(map), out var shared, out _));
            Assert.IsTrue(shared.AuthorVerified);
            Assert.IsTrue(shared.Verified, "a passed map stays passed for the friend (they may share it on)");

            map.Name = "Новое имя";
            Assert.IsTrue(map.Verified, "renaming does not change the jump");
            map.Pieces[0].X += 0.5f;
            Assert.IsFalse(map.Verified, "any real change needs a new pass");
        }

        // ------------------------------------------------------------------ store

        [Test]
        public void Store_SaveListLoadDelete()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sj-maps-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var store = new MapStore(dir);
                Assert.IsEmpty(store.All());
                var a = Sample();
                store.Save(a);
                Assert.IsNotEmpty(a.Id);
                var b = Sample();
                b.Name = "Вторая";
                store.Save(b);
                File.WriteAllText(Path.Combine(dir, "broken.json"), "{ not json");

                var all = store.All();
                Assert.AreEqual(2, all.Count, "a broken file is skipped");
                var loaded = store.Load(a.Id);
                Assert.AreEqual(a.Pieces.Count, loaded.Pieces.Count);
                Assert.AreEqual(a.Target.Width, loaded.Target.Width);

                store.Delete(a.Id);
                Assert.IsNull(store.Load(a.Id));
                Assert.AreEqual("Карта 1", MapStore.FreeName(store.All(), "Карта"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        // ------------------------------------------------------------------ map → level

        [Test]
        public void ToLevel_StandsOnTheRoofsAndCanBeLanded()
        {
            var map = Sample();
            var level = MapLevel.ToLevel(map, _catalog);
            try
            {
                var jump = AssetDatabase.LoadAssetAtPath<JumpConfig>("Assets/_Game/Config/JumpConfig.asset");
                var flight = AssetDatabase.LoadAssetAtPath<FlightConfig>("Assets/_Game/Config/FlightConfig.asset");
                var layout = LevelLayout.Build(level, jump);
                Assert.AreEqual(20f, layout.RoofY, 0.01f);
                Assert.AreEqual(0f, layout.EdgeX, 0.01f);
                Assert.AreEqual(5f, layout.TargetY, 0.01f);
                Assert.AreEqual(2, layout.Runways.Count, "start + one intermediate platform");
                Assert.AreEqual(-1, layout.Runways[1].Dir);
                Assert.AreEqual(map.TimeLimit, level.TimeLimit);
                Assert.AreEqual("map:" + map.Id, level.Id);

                var plan = Autopilot.Plan(new JumpSession(layout, jump, flight), 0.5f);
                Assert.IsTrue(plan.Found, "the autopilot finds a way onto the flag");
            }
            finally
            {
                Object.DestroyImmediate(level.Scene);
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void Problems_TooShortStartOrTarget()
        {
            var map = Sample();
            Assert.IsEmpty(MapLevel.Problems(map));
            map.Start.Width = 2f;
            map.Target.Width = 1f;
            CollectionAssert.AreEquivalent(new[] { "start", "target" }, MapLevel.Problems(map));
        }

        // ------------------------------------------------------------------ editing

        [Test]
        public void Add_StandsOnTheGroundAndOnOtherPieces_UndoRedo()
        {
            var editor = new MapEditor(new MapData(), _catalog);
            int changes = 0;
            editor.Changed += () => changes++;

            int a = editor.Add("box", new Vector2(0.5f, 7f));
            Assert.AreEqual(0f, Bottom(editor, a), 1e-4f, "on the street");
            int b = editor.Add("box", new Vector2(0.7f, 7f));
            Assert.AreEqual(1f, Bottom(editor, b), 1e-4f, "on top of the first box");
            int cloud = editor.Add("cloud", new Vector2(0f, 30f));
            Assert.AreEqual(30f, editor.Map.Pieces[cloud].Y, 1e-4f, "the sky stays where it was put");
            Assert.AreEqual(3, changes);

            editor.Undo();
            Assert.AreEqual(2, editor.Map.Pieces.Count);
            editor.Undo();
            Assert.AreEqual(1, editor.Map.Pieces.Count);
            editor.Redo();
            Assert.AreEqual(2, editor.Map.Pieces.Count);
            Assert.IsTrue(editor.CanRedo);
        }

        /// <summary>Тянем куб почти ровно на другой — он прилипает краями, а не стоит со щелью в пару сантиметров.</summary>
        [Test]
        public void Move_SnapsToEdgesOfNeighbours()
        {
            var editor = new MapEditor(new MapData(), _catalog);
            int a = editor.Add("box", new Vector2(0.5f, 5f));
            int b = editor.Add("box", new Vector2(5f, 5f));
            editor.SelectPieces(new[] { b });
            var start = editor.Map.Pieces[b].Position;
            editor.BeginDrag(DragKind.Move, start);
            editor.Drag(start + new Vector2(-4.47f, 1.03f), 0.1f); // nearly on top of box a
            editor.EndDrag();
            var p = editor.Map.Pieces[b];
            Assert.AreEqual(editor.Map.Pieces[a].X, p.X, 1e-4f, "lined up");
            Assert.AreEqual(1f, Bottom(editor, b), 1e-4f, "standing on it");

            editor.Undo();
            Assert.AreEqual(start, editor.Map.Pieces[b].Position, "one drag — one undo step");
        }

        [Test]
        public void DuplicateAbove_StacksFloors_And_SelectConnected_TakesTheBuilding()
        {
            var editor = new MapEditor(new MapData(), _catalog);
            int floor = editor.Add("tower", new Vector2(0f, 0f));
            editor.Duplicate(above: true);
            editor.Duplicate(above: true);
            Assert.AreEqual(3, editor.Map.Pieces.Count);
            Assert.AreEqual(40f, Bottom(editor, 2), 1e-3f, "third floor on the second");
            editor.Add("cloud", new Vector2(0f, 61f)); // touches the roof, but it is the sky
            editor.Add("box", new Vector2(30f, 0f)); // another building

            editor.SelectPieces(new[] { floor });
            editor.SelectConnected();
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, editor.Selected);
        }

        [Test]
        public void SelectAt_CyclesThroughPiecesUnderTheFinger()
        {
            var editor = new MapEditor(new MapData(), _catalog);
            editor.Add("tower", new Vector2(0f, 0f));
            editor.Add("cloud", new Vector2(0f, 10f)); // in front of the tower (added later)
            editor.ClearSelection();
            var at = new Vector2(0f, 10f);
            Assert.IsTrue(editor.SelectAt(at, 0f));
            Assert.AreEqual(1, editor.Selected[0], "the top one first");
            editor.SelectAt(at, 0f);
            Assert.AreEqual(0, editor.Selected[0], "tap again — the one behind");
            Assert.IsFalse(editor.SelectAt(new Vector2(100f, 100f), 0f));
            Assert.IsFalse(editor.HasSelection);
        }

        /// <summary>Полосу разбега растягивают за край: он прилипает к краю крыши, а короче минимума не становится.</summary>
        [Test]
        public void StripEnds_SnapToRoofEdge_AndKeepMinimum()
        {
            var map = Sample();
            var editor = new MapEditor(map, _catalog);
            editor.SelectStrip(StripKind.Start);
            editor.BeginDrag(DragKind.StripRight, new Vector2(map.Start.XMax, 20f));
            editor.Drag(new Vector2(-0.3f, 20f), 0.5f);
            Assert.AreEqual(0f, map.Start.XMax, 1e-4f, "the jump edge is the roof edge");
            editor.Drag(new Vector2(-9f, 20f), 0.5f);
            Assert.AreEqual(MapLevel.MinStartWidth, map.Start.Width, 1e-4f);
            editor.EndDrag();

            editor.SelectStrip(StripKind.Target);
            editor.BeginDrag(DragKind.StripMove, new Vector2(30f, 5f));
            editor.Drag(new Vector2(30.2f, 5.4f), 0.3f);
            editor.EndDrag();
            Assert.AreEqual(5f, map.Target.Y, 1e-4f, "lies on the roof");
        }

        [Test]
        public void ScaleHandle_KeepsTheOppositeCorner()
        {
            var editor = new MapEditor(new MapData(), _catalog);
            int t = editor.Add("tower", new Vector2(0f, 0f));
            var before = Bounds(editor, t);
            var handle = editor.Handles(1f).First(h => h.handle == Handle.Scale).at;
            editor.BeginDrag(DragKind.Scale, handle);
            editor.Drag(before.min + (handle - before.min) * 1.5f, 0.1f);
            editor.EndDrag();
            var after = Bounds(editor, t);
            Assert.AreEqual(before.xMin, after.xMin, 1e-3f);
            Assert.AreEqual(before.yMin, after.yMin, 1e-3f);
            Assert.AreEqual(before.width * 1.5f, after.width, 1e-2f);
        }

        // ------------------------------------------------------------------ with the original's pieces (placeholders)

        PieceCatalog RealCatalog()
        {
            var c = AssetDatabase.LoadAssetAtPath<PieceCatalog>(LevelImporter.CatalogPath);
            if (c == null || c.Pieces.Length == 0) Assert.Ignore("map builder pieces are not imported (no reference/decoded)");
            return c;
        }

        /// <summary>Новая карта сразу играется: автопилот находит, как сесть на флажок.</summary>
        [Test]
        public void NewMap_IsPlayable()
        {
            var catalog = RealCatalog();
            var map = MapLevel.NewMap(catalog, "Карта 1");
            Assert.Greater(map.Pieces.Count, 5);
            Assert.IsEmpty(MapLevel.Problems(map));
            var level = MapLevel.ToLevel(map, catalog);
            try
            {
                var jump = AssetDatabase.LoadAssetAtPath<JumpConfig>("Assets/_Game/Config/JumpConfig.asset");
                var flight = AssetDatabase.LoadAssetAtPath<FlightConfig>("Assets/_Game/Config/FlightConfig.asset");
                var layout = LevelLayout.Build(level, jump);
                Assert.AreEqual(map.Start.Y, layout.RoofY, 0.01f);
                Assert.Greater(layout.RoofY, 20f);
                var plan = Autopilot.Plan(new JumpSession(layout, jump, flight), 0.5f);
                Assert.IsTrue(plan.Found, "the template can be landed");
            }
            finally
            {
                Object.DestroyImmediate(level.Scene);
                Object.DestroyImmediate(level);
            }
        }

        /// <summary>Уровень оригинала, открытый в редакторе и сохранённый как карта, играется так же: та же крыша, цель, стены.</summary>
        [Test]
        public void OriginalLevel_AsMap_IsTheSameLevel()
        {
            var catalog = RealCatalog();
            var jump = AssetDatabase.LoadAssetAtPath<JumpConfig>("Assets/_Game/Config/JumpConfig.asset");
            foreach (var id in new[] { "easy1", "expert4", "x05", "d5", "night3" })
            {
                var original = AssetDatabase.LoadAssetAtPath<LevelConfig>($"{LevelImporter.Folder}/{id}.asset");
                Assert.IsNotNull(original, id);
                var map = MapLevel.FromLevel(original, catalog, id);
                Assert.IsTrue(MapCode.TryDecode(MapCode.Encode(map), out var shared, out var error), error); // through the share code, too
                var level = MapLevel.ToLevel(shared, catalog);
                try
                {
                    LevelLayout a = LevelLayout.Build(original, jump), b = LevelLayout.Build(level, jump);
                    Assert.AreEqual(a.RoofY, b.RoofY, 0.05f, id);
                    Assert.AreEqual(a.EdgeX, b.EdgeX, 0.05f, id);
                    Assert.AreEqual(a.TargetY, b.TargetY, 0.05f, id);
                    Assert.AreEqual(a.ArrowX, b.ArrowX, 0.05f, id);
                    Assert.AreEqual(a.Runways.Count, b.Runways.Count, id);
                    Assert.AreEqual(a.Solids.Count, b.Solids.Count, id);
                    Assert.AreEqual(original.Scene.Pictures.Length, level.Scene.Pictures.Length, id);
                }
                finally
                {
                    Object.DestroyImmediate(level.Scene);
                    Object.DestroyImmediate(level);
                }
            }
        }

        static float Bottom(MapEditor e, int i) => Bounds(e, i).yMin;

        static Rect Bounds(MapEditor e, int i)
        {
            var p = e.Map.Pieces[i];
            Assert.IsTrue(e.Catalog.TryGet(p.Art, out var def));
            return MapGeometry.OpaqueBounds(def, p);
        }
    }
}
