using System.Collections.Generic;
using NUnit.Framework;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using StickmanJumper.Save;
using UnityEditor;

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

        /// <summary>Каждый уровень проходится: автопилот находит разбег и высоту купола и садится на крышу с флажком.</summary>
        [Test]
        public void EveryLevel_IsSolvable()
        {
            var catalog = Catalog();
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
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
