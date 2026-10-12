using System.Linq;
using NUnit.Framework;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.EditorTools;
using StickmanJumper.Gameplay;
using UnityEditor;
using UnityEngine;

namespace StickmanJumper.Tests
{
    /// <summary>Подсказки уровня, как в оригинале (TutorialText): фаза, область, задержка, ожидание тапа, обучающий уровень.</summary>
    public class TutorialTests
    {
        static LevelScene.Tutorial Hint(int state, float delay = 0f, bool wait = false, Rect trigger = default) =>
            new LevelScene.Tutorial { Key = "T", Text = "hint", RequiredState = state, ShowDelay = delay, WaitForTouch = wait, Trigger = trigger };

        [Test]
        public void ShowsInItsPhase_AfterTheDelay()
        {
            var s = TestKit.Session();
            var r = new TutorialRunner(new[] { Hint(0, 0.5f) }, false);
            r.Update(s, 0.4f);
            Assert.AreEqual(-1, r.Active);
            r.Update(s, 0.2f);
            Assert.AreEqual(0, r.Active);
            Assert.IsFalse(r.Holding, "no waiting for a tap");
        }

        [Test]
        public void WaitsForItsPhase()
        {
            var s = TestKit.Session();
            var r = new TutorialRunner(new[] { Hint(1) }, false);
            r.Update(s, 1f);
            Assert.AreEqual(-1, r.Active, "standing (0), the hint wants walking back (1)");
            s.Tap();
            r.Update(s, 0.1f);
            Assert.AreEqual(0, r.Active);
        }

        /// <summary>С областью — только когда ступни в ней; с ожиданием тапа — игра стоит, тап убирает подсказку и идёт в игру.</summary>
        [Test]
        public void Trigger_AndWaitForTouch()
        {
            var s = TestKit.Session();
            var area = new Rect(s.Feet.x - 3f, s.Feet.y - 1f, 1f, 3f); // 2–3 m behind the start
            var r = new TutorialRunner(new[] { Hint(1, wait: true, trigger: area) }, false);
            s.Tap();
            for (int i = 0; i < 600 && r.Active < 0; i++)
            {
                r.Update(s, s.StepSize);
                if (!r.Holding) s.Step(s.StepSize);
            }
            Assert.AreEqual(0, r.Active);
            Assert.IsTrue(r.Holding);
            Assert.IsTrue(area.Contains(s.Feet + Vector2.up * s.Flight.BodyHeight * 0.5f));
            Assert.IsTrue(r.OnTap(), "the tap also does its job");
            Assert.IsFalse(r.Holding);
            Assert.AreEqual(-1, r.Active);
        }

        /// <summary>Обучающий уровень: тап без подсказки на экране не действует.</summary>
        [Test]
        public void Guided_TapWithoutHint_DoesNothing()
        {
            var s = TestKit.Session();
            var guided = new TutorialRunner(new[] { Hint(0, 0.5f) }, true);
            Assert.IsFalse(guided.OnTap());
            guided.Update(s, 1f);
            Assert.IsTrue(guided.OnTap());
            Assert.IsFalse(guided.OnTap(), "the hint is gone");
            Assert.IsTrue(new TutorialRunner(new LevelScene.Tutorial[0], false).OnTap(), "an ordinary level: every tap counts");
        }

        /// <summary>
        /// Первый уровень оригинала проходится, как его ведут подсказки: тапаем только по подсказке — и садимся на цель.
        /// Проверяет и подсказки, и то, что наша физика повторяет оригинал (точки остановки стоят в его файле).
        /// </summary>
        [Test]
        public void Easy1_GuidedByItsHints_LandsOnTheTarget()
        {
            var stages = LevelImporter.LoadStages();
            if (stages.Length == 0) Assert.Ignore("original levels are not imported");
            var level = stages[0].Levels.First(l => l.Id == "easy1");
            var jump = AssetDatabase.LoadAssetAtPath<JumpConfig>("Assets/_Game/Config/JumpConfig.asset");
            var flight = AssetDatabase.LoadAssetAtPath<FlightConfig>("Assets/_Game/Config/FlightConfig.asset");
            var layout = LevelLayout.Build(level, jump);
            Assert.IsTrue(layout.Guided);
            Assert.AreEqual(4, layout.Tutorials.Count);
            var s = new JumpSession(layout, jump, flight);
            var r = new TutorialRunner(layout.Tutorials, layout.Guided);
            int hints = 0;
            for (int i = 0; i < 60 * 120 && !s.Phase.IsFinished(); i++)
            {
                r.Update(s, s.StepSize);
                if (r.Active >= 0)
                {
                    hints++;
                    Assert.IsTrue(r.OnTap());
                    s.Tap();
                }
                if (!r.Holding) s.Step(s.StepSize);
            }
            Assert.AreEqual(4, hints);
            Assert.AreEqual(JumpOutcome.WellDone, s.Outcome, $"landed {s.LandingDistance:0.0} m from the flag at x={s.Feet.x:0.0}");
        }
    }
}
