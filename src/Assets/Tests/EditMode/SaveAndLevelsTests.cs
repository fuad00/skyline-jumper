using System.Collections.Generic;
using NUnit.Framework;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using StickmanJumper.Save;
using UnityEditor;
using UnityEngine;

namespace StickmanJumper.Tests
{
    public class SaveServiceTests
    {
        sealed class MemoryStore : ISaveStore
        {
            public string Json = string.Empty;
            public string Read() => Json;
            public void Write(string json) => Json = json;
        }

        [Test]
        public void Record_KeepsBestStarsPointsAndDistance()
        {
            var save = new SaveService(new MemoryStore());
            Assert.IsTrue(save.Record("a", 2, 900, 1.2f));
            Assert.IsFalse(save.Record("a", 1, 500, 3f), "worse attempt is not a record");
            Assert.IsTrue(save.Record("a", 3, 800, 0.4f));
            var r = save.Get("a");
            Assert.AreEqual(3, r.Stars);
            Assert.AreEqual(900, r.BestPoints);
            Assert.AreEqual(0.4f, r.BestDistance, 1e-5f);
        }

        [Test]
        public void FailedAttempt_DoesNotSetDistance()
        {
            var save = new SaveService(new MemoryStore());
            Assert.IsFalse(save.Record("a", 0, 0, float.NaN));
            Assert.AreEqual(-1f, save.Get("a").BestDistance);
        }

        [Test]
        public void RoundTrip_ThroughStore()
        {
            var store = new MemoryStore();
            var save = new SaveService(store);
            save.Record("s1_01", 3, 1300, 0.05f);
            save.Data.Music = false;
            save.Persist();
            var again = new SaveService(store);
            again.Load();
            Assert.AreEqual(3, again.Stars("s1_01"));
            Assert.IsFalse(again.Data.Music);
            Assert.AreEqual(SaveData.CurrentVersion, again.Data.Version);
        }

        [Test]
        public void CorruptSave_StartsFresh()
        {
            Assert.AreEqual(0, SaveService.Parse("{not json").Levels.Count);
            Assert.AreEqual(0, SaveService.Parse("{\"Version\":999}").Levels.Count, "save from a newer game version is ignored");
        }

        [Test]
        public void Levels_UnlockOneByOne()
        {
            var save = new SaveService(new MemoryStore());
            var ids = new List<string> { "a", "b", "c" };
            Assert.IsTrue(save.IsUnlocked(ids, 0));
            Assert.IsFalse(save.IsUnlocked(ids, 1));
            save.Record("a", 1, 100, 2f);
            Assert.IsTrue(save.IsUnlocked(ids, 1));
            Assert.IsFalse(save.IsUnlocked(ids, 2));
        }

        [Test]
        public void ResetProgress_WipesStarsButKeepsSettings()
        {
            var store = new MemoryStore();
            var save = new SaveService(store);
            save.Record("s1_01", 3, 1300, 0.05f);
            save.Data.Sound = false;
            save.Data.Language = "en";
            save.ResetProgress();
            save.Persist();
            var again = new SaveService(store);
            again.Load();
            Assert.AreEqual(0, again.Stars("s1_01"));
            Assert.IsNull(again.Get("s1_01"));
            Assert.IsFalse(again.Data.Sound);
            Assert.AreEqual("en", again.Data.Language);
        }

        [Test]
        public void UnlockAll_OpensEveryLevel_ResetCloses()
        {
            var save = new SaveService(new MemoryStore());
            var ids = new List<string> { "a", "b", "c" };
            Assert.IsFalse(save.IsUnlocked(ids, 2));
            save.Data.UnlockAll = true;
            Assert.IsTrue(save.IsUnlocked(ids, 1));
            Assert.IsTrue(save.IsUnlocked(ids, 2));
            save.ResetProgress();
            Assert.IsFalse(save.IsUnlocked(ids, 2), "a normal reset puts the usual order back");
        }

        [Test]
        public void OldSave_WithoutLanguage_StaysRussian()
        {
            var data = SaveService.Parse("{\"Version\":1,\"Levels\":[],\"Sound\":true,\"Music\":true}");
            Assert.AreEqual(UI.Language.Ru, UI.UIText.FromCode(data.Language));
        }
    }

    public class UITextTests
    {
        [TearDown]
        public void BackToRussian() => UI.UIText.Lang = UI.Language.Ru; // static: do not leak into other tests

        [Test]
        public void Language_SwitchesTextsAndNumberFormat()
        {
            UI.UIText.Lang = UI.Language.Ru;
            Assert.AreEqual("ПРЫЖОК", UI.UIText.PlayBig);
            Assert.AreEqual("0,42 м", UI.UIText.Meters(0.42f));
            Assert.AreEqual("ещё 2 звезды\nчтобы открыть", UI.UIText.StarsNeeded(2));
            UI.UIText.Lang = UI.Language.En;
            Assert.AreEqual("JUMP", UI.UIText.PlayBig);
            Assert.AreEqual("0.42 m", UI.UIText.Meters(0.42f));
            Assert.AreEqual("8.54", UI.UIText.Timer(8.54f));
            Assert.AreEqual("1 more star\nto unlock", UI.UIText.StarsNeeded(1));
            Assert.AreEqual(("well", "DONE"), UI.UIText.Title(JumpOutcome.WellDone));
        }

        [Test]
        public void Pick_FallsBackToRussian_WhenNoEnglish()
        {
            UI.UIText.Lang = UI.Language.En;
            Assert.AreEqual("rookie", UI.UIText.Pick("новичок", "rookie"));
            Assert.AreEqual("новичок", UI.UIText.Pick("новичок", ""));
        }
    }

    public class LevelCatalogTests
    {
        static GameCatalog Catalog() => AssetDatabase.LoadAssetAtPath<GameCatalog>("Assets/_Game/Config/GameCatalog.asset");

        [Test]
        public void Catalog_HasStage1WithTenUniqueLevels()
        {
            var catalog = Catalog();
            Assert.IsNotNull(catalog, "run Stickman Jumper → Setup Project");
            var ids = catalog.LevelIds(0);
            Assert.AreEqual(10, ids.Count);
            CollectionAssert.AllItemsAreUnique(ids);
        }

        /// <summary>
        /// Наши уровни проходятся: автопилот находит разбег и высоту купола и садится на крышу с флажком.
        /// С 2026-10-12 это запасные уровни (в игре — уровни оригинала), а физика подобрана под оригинал, поэтому
        /// допускаем до трёх непроходимых — их надо перестроить, если они снова понадобятся.
        /// </summary>
        [Test, Ignore("Наши запасные уровни строились под прежнюю физику; физика теперь под уровни оригинала (числа разбега и прыжка — его). " +
                      "Перестроить, когда будут точные числа полёта оригинала.")]
        public void OwnLevels_MostlySolvable()
        {
            var catalog = ScriptableObject.CreateInstance<GameCatalog>();
            catalog.Stages = new[] { AssetDatabase.LoadAssetAtPath<StageConfig>("Assets/_Game/Config/Stage1.asset") };
            var jump = AssetDatabase.LoadAssetAtPath<JumpConfig>("Assets/_Game/Config/JumpConfig.asset");
            var flight = AssetDatabase.LoadAssetAtPath<FlightConfig>("Assets/_Game/Config/FlightConfig.asset");
            var failures = new List<string>();
            for (int s = 0; s < catalog.Stages.Length; s++)
            foreach (var level in catalog.Stages[s].Levels)
            {
                var session = new JumpSession(LevelLayout.Build(level, jump), jump, flight);
                var plan = Autopilot.Plan(session);
                if (!plan.Found)
                {
                    failures.Add($"{level.Id}: no run-up/canopy combination lands on the target");
                    continue;
                }
                TestKit.RunToEnd(session, new Autopilot { DesiredRunUp = plan.RunUp, OpenAtY = plan.OpenY });
                if (session.Outcome != JumpOutcome.WellDone) failures.Add($"{level.Id}: replay of the plan ended {session.Outcome}");
                else if (session.LandingDistance > 2.5f) failures.Add($"{level.Id}: best plan lands {session.LandingDistance:0.0} m from the flag");
            }
            Assert.LessOrEqual(failures.Count, 3, string.Join("\n", failures));
        }
    }
}
