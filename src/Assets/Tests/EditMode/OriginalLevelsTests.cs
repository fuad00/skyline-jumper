using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.EditorTools;
using StickmanJumper.Gameplay;
using UnityEditor;
using UnityEngine;

namespace StickmanJumper.Tests
{
    /// <summary>Уровни оригинала (заглушки). Пропускаются, если их нет — свежий клон без reference/decoded.</summary>
    public class OriginalLevelsTests
    {
        static List<(StageConfig stage, LevelConfig level)> All()
        {
            var stages = LevelImporter.LoadStages();
            if (stages.Length == 0) Assert.Ignore("original levels are not imported (no reference/decoded)");
            return stages.SelectMany(s => s.Levels.Select(l => (s, l))).ToList();
        }

        static JumpConfig Jump() => AssetDatabase.LoadAssetAtPath<JumpConfig>("Assets/_Game/Config/JumpConfig.asset");
        static FlightConfig Flight() => AssetDatabase.LoadAssetAtPath<FlightConfig>("Assets/_Game/Config/FlightConfig.asset");

        [Test]
        public void NineStagesOfTen()
        {
            var stages = LevelImporter.LoadStages();
            if (stages.Length == 0) Assert.Ignore("original levels are not imported");
            Assert.AreEqual(9, stages.Length);
            foreach (var s in stages) Assert.AreEqual(10, s.Levels.Length, s.Title);
            CollectionAssert.AllItemsAreUnique(stages.SelectMany(s => s.Levels).Select(l => l.Id));
        }

        /// <summary>У каждого уровня есть крыша для разбега, край, цель с флажком и время.</summary>
        [Test]
        public void EveryLevel_HasRoofEdgeAndTarget()
        {
            var problems = new List<string>();
            foreach (var (_, level) in All())
            {
                var l = LevelLayout.Build(level, Jump());
                if (l.RoofY < 1f) problems.Add($"{level.Id}: roof at {l.RoofY:0.0} m");
                bool multiJump = level.Scene.InterPlatforms.Length > 0; // jumps from platform to platform: not supported yet
                if (!multiJump && l.EdgeX - l.BackLimitX < 2f) problems.Add($"{level.Id}: run-up only {l.EdgeX - l.BackLimitX:0.0} m");
                if (l.TargetRect.width < 2f) problems.Add($"{level.Id}: target {l.TargetRect.width:0.0} m wide");
                if (l.ArrowX < l.TargetRect.xMin || l.ArrowX > l.TargetRect.xMax) problems.Add($"{level.Id}: flag off the target");
                if (level.TimeLimit < 5f) problems.Add($"{level.Id}: time {level.TimeLimit}");
                if (level.Scene.Pictures.Length < 10) problems.Add($"{level.Id}: only {level.Scene.Pictures.Length} pictures");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        /// <summary>
        /// Первый этап оригинала («новичок») проходится нашей физикой хотя бы на 7 уровнях из 10. Числа разбега и прыжка —
        /// оригинала, числа полёта пока подобраны (CalibrationSweep). Когда будут настоящие числа полёта — поднять до 10.
        /// Сторож: если правка физики ломает уровни оригинала — узнаем сразу.
        /// </summary>
        [Test]
        public void RookieStage_MostlySolvable()
        {
            var rookie = All().Where(x => x.level.Id.StartsWith("easy")).Select(x => x.level).ToList();
            var jump = Jump();
            var flight = Flight();
            var failed = rookie.Where(l => !Solvable(LevelLayout.Build(l, jump), jump, flight, out _)).Select(l => l.Id).ToList();
            Assert.LessOrEqual(failed.Count, 3, "not solvable: " + string.Join(" ", failed));
        }

        /// <summary>Решается ли уровень (автопилот находит разбег и купол, повтор плана садится на цель вовремя).</summary>
        static bool Solvable(LevelLayout layout, JumpConfig jump, FlightConfig flight, out float miss)
        {
            miss = float.NaN;
            float run = layout.EdgeX - layout.BackLimitX;
            float drop = layout.RoofY - layout.TargetRect.yMax;
            var plan = Autopilot.Plan(new JumpSession(layout, jump, flight), Mathf.Max(1f, run / 6f), Mathf.Max(3f, drop / 18f));
            if (!plan.Found) return false;
            var s = new JumpSession(layout, jump, flight);
            TestKit.RunToEnd(s, new Autopilot { DesiredRunUp = plan.RunUp, OpenAtY = plan.OpenY }, 120f);
            miss = s.LandingDistance;
            return s.Outcome == JumpOutcome.WellDone;
        }

        /// <summary>
        /// Подбор физики по уровням оригинала (их размеры и лимиты времени — единственные «замеры» его физики):
        /// перебирает предельную скорость падения, «планирование» по X и максимальную скорость бега,
        /// считает, сколько уровней с одним прыжком проходятся. Пишет Logs/physics-calibration.txt.
        /// </summary>
        [Test, Explicit("long; a calibration sweep")]
        public void CalibrationSweep()
        {
            var levels = All().Where(x => x.level.Scene.InterPlatforms.Length == 0).Select(x => x.level).ToList();
            var baseJump = Jump();
            var baseFlight = Flight();
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            sb.AppendLine($"single-jump levels: {levels.Count}");
            // Run and jump numbers are the original's (gameplay_reference.json); gravity, the fall-speed cap,
            // the glide and the canopy descent are not known — those are swept.
            foreach (float gravity in new[] { 9.81f })
            foreach (float fall in new[] { 22f, 28f, 34f })
            foreach (float glide in new[] { 60f, 150f })
            foreach (float canopy in new[] { 6.5f })
            {
                var jump = Object.Instantiate(baseJump);
                var flight = Object.Instantiate(baseFlight);
                flight.Gravity = gravity;
                flight.FreefallTerminalSpeed = fall;
                flight.FreefallGlideSpeed = glide;
                flight.CanopyTerminalSpeed = canopy;
                var failed = new List<string>();
                foreach (var level in levels)
                    if (!Solvable(LevelLayout.Build(level, jump), jump, flight, out _)) failed.Add(level.Id);
                sb.AppendLine(string.Format(ci, "g {0,5:0.0} fall {1,4:0} glide {2,4:0} canopy {3,4:0.0} | solvable {4,2}/{5} (easy {6}/10) | failed: {7}",
                    gravity, fall, glide, canopy, levels.Count - failed.Count, levels.Count,
                    levels.Count(l => l.Id.StartsWith("easy")) - failed.Count(f => f.StartsWith("easy")), string.Join(" ", failed)));
                Object.DestroyImmediate(jump);
                Object.DestroyImmediate(flight);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/physics-calibration.txt", sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>Сила ветра: в каких единицах ветер в файлах оригинала (проверка на этапе «наверху ветрено»).</summary>
        [Test, Explicit("calibration")]
        public void WindScaleSweep()
        {
            var levels = All().Where(x => x.level.Scene.InterPlatforms.Length == 0 && x.level.Id.StartsWith("advanced")).Select(x => x.level).ToList();
            var jump = Object.Instantiate(Jump());
            var flight = Object.Instantiate(Flight());
            jump.RunMaxSpeed = 32f;
            flight.FreefallTerminalSpeed = 28f;
            flight.FreefallGlideSpeed = 150f;
            flight.CanopyTerminalSpeed = 6.5f;
            var sb = new StringBuilder();
            foreach (float k in new[] { 1f, 3f, 6f, -1f, -3f })
            {
                var failed = new List<string>();
                foreach (var level in levels)
                {
                    var copy = Object.Instantiate(level);
                    copy.Wind *= k;
                    if (!Solvable(LevelLayout.Build(copy, jump), jump, flight, out _)) failed.Add(level.Id);
                    Object.DestroyImmediate(copy);
                }
                sb.AppendLine($"wind ×{k}: solvable {levels.Count - failed.Count}/{levels.Count} | failed: {string.Join(" ", failed)}");
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/wind-scale.txt", sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>Сетка исходов «разбег × высота раскрытия» для нескольких уровней (W — сел на цель, M — мимо, D — разбился, T — время).</summary>
        [Test, Explicit("diagnostics")]
        public void OutcomeGrid()
        {
            var ids = new[] { "easy4", "easy5", "easy6", "advanced2" };
            var jump = Object.Instantiate(Jump());
            var flight = Object.Instantiate(Flight());
            flight.Gravity = 9.81f;
            flight.FreefallTerminalSpeed = 13f;
            flight.FreefallGlideSpeed = 60f;
            flight.CanopyTerminalSpeed = 6.5f;
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            foreach (var (_, level) in All().Where(x => ids.Contains(x.level.Id)))
            {
                var layout = LevelLayout.Build(level, jump);
                float run = layout.EdgeX - layout.BackLimitX;
                sb.AppendLine(string.Format(ci, "{0}: roof {1:0} target {2:0}..{3:0} (top {4:0}) edge {5:0} time {6:0} wind {7:0.0} solids {8}",
                    level.Id, layout.RoofY, layout.TargetRect.xMin, layout.TargetRect.xMax, layout.TargetRect.yMax, layout.EdgeX, level.TimeLimit, level.Wind, layout.Solids.Count));
                sb.Append("open\\run ");
                var runs = new List<float>();
                for (float r = 0.5f; r <= run + 0.01f; r += Mathf.Max(1f, run / 10f)) { runs.Add(r); sb.Append(string.Format(ci, "{0,5:0}", r)); }
                sb.AppendLine();
                for (float openY = layout.RoofY - 2f; openY >= layout.TargetRect.yMax + 4f; openY -= Mathf.Max(3f, (layout.RoofY - layout.TargetRect.yMax) / 20f))
                {
                    sb.Append(string.Format(ci, "{0,9:0} ", openY));
                    foreach (float r in runs)
                    {
                        var s = new JumpSession(layout, jump, flight);
                        TestKit.RunToEnd(s, new Autopilot { DesiredRunUp = r, OpenAtY = openY }, 120f);
                        char c = s.Outcome == JumpOutcome.WellDone ? 'W' : s.Outcome == JumpOutcome.MissedPlatform ? 'M' : s.Outcome == JumpOutcome.Died ? 'D' : 'T';
                        sb.Append(string.Format(ci, "  {0}{1,3:0}", c, s.Feet.x - layout.ArrowX));
                    }
                    sb.AppendLine();
                }
                sb.AppendLine();
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/outcome-grid.txt", sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// Отчёт (не проверка): проходится ли каждый уровень нашей физикой и за отведённое время. Пишет Logs/original-levels-report.txt.
        /// Запуск: -testFilter StickmanJumper.Tests.OriginalLevelsTests.SolvabilityReport
        /// </summary>
        [Test, Explicit("long; a calibration report")]
        public void SolvabilityReport()
        {
            var jump = Jump();
            var flight = Flight();
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            sb.AppendLine("level          roof  drop   run    dx  time wind inter | result");
            int ok = 0, total = 0;
            foreach (var (stage, level) in All())
            {
                total++;
                var layout = LevelLayout.Build(level, jump);
                float run = layout.EdgeX - layout.BackLimitX;
                float drop = layout.RoofY - layout.TargetRect.yMax;
                float dx = layout.ArrowX - layout.EdgeX;
                var template = new JumpSession(layout, jump, flight);
                var plan = Autopilot.Plan(template, Mathf.Max(1f, run / 6f), Mathf.Max(3f, drop / 18f));
                string result;
                if (plan.Found)
                {
                    var s = new JumpSession(layout, jump, flight);
                    TestKit.RunToEnd(s, new Autopilot { DesiredRunUp = plan.RunUp, OpenAtY = plan.OpenY }, 120f);
                    result = s.Outcome == JumpOutcome.WellDone
                        ? string.Format(ci, "OK  miss {0:0.0} m, {1:0.0}/{2:0} s", s.LandingDistance, s.Elapsed, level.TimeLimit)
                        : $"plan replay ended {s.Outcome} ({s.Elapsed:0.0}/{level.TimeLimit:0} s)";
                    if (s.Outcome == JumpOutcome.WellDone) ok++;
                }
                else
                {
                    // How far does the longest jump get? Full run-up, canopy as low as possible.
                    var s = new JumpSession(layout, jump, flight);
                    TestKit.RunToEnd(s, new Autopilot { DesiredRunUp = run, OpenAtY = layout.TargetRect.yMax + 6f }, 120f);
                    result = string.Format(ci, "NO  longest jump: {0} at x−edge {1:0.0} m ({2:0.0} s)", s.Outcome, s.Feet.x - layout.EdgeX, s.Elapsed);
                }
                sb.AppendLine(string.Format(ci, "{0,-13} {1,5:0} {2,5:0} {3,5:0} {4,5:0} {5,5:0} {6,4:0.0} {7,5} | {8}",
                    level.Id, layout.RoofY, drop, run, dx, level.TimeLimit, level.Wind, level.Scene.InterPlatforms.Length, result));
            }
            sb.AppendLine($"solvable: {ok}/{total}");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/original-levels-report.txt", sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
