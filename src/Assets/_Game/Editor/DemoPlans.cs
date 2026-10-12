using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using UnityEditor;
using UnityEngine;

namespace StickmanJumper.EditorTools
{
    /// <summary>
    /// Заставка в меню: игра сама прыгает по случайным уровням и всегда попадает на флажок. Для этого заранее (здесь,
    /// в редакторе) для каждого уровня с одним прыжком автопилот подбирает разбег, место прыжка и высоту раскрытия купола
    /// и проверяет повтор той же физикой. Физика детерминирована, а в игре автопилот тапает на тех же шагах — повтор совпадает.
    /// Пересчитывать после смены чисел физики (делается и в Setup Project).
    /// </summary>
    public static class DemoPlans
    {
        [MenuItem("Stickman Jumper/Recompute Menu Demo Plans")]
        public static void Compute()
        {
            var jump = AssetDatabase.LoadAssetAtPath<JumpConfig>(ProjectSetup.Root + "/Config/JumpConfig.asset");
            var flight = AssetDatabase.LoadAssetAtPath<FlightConfig>(ProjectSetup.Root + "/Config/FlightConfig.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(ProjectSetup.Root + "/Config/GameCatalog.asset");
            if (jump == null || flight == null || catalog == null) return;
            int found = 0, total = 0;
            foreach (var stage in catalog.Stages)
            {
                if (stage == null) continue;
                foreach (var level in stage.Levels)
                {
                    if (level == null) continue;
                    total++;
                    level.DemoRunUp = float.NaN;
                    level.DemoJumpAt = 0f;
                    level.DemoOpenY = float.NaN;
                    bool multiJump = level.Scene != null && level.Scene.InterPlatforms != null && level.Scene.InterPlatforms.Length > 0;
                    if (!multiJump && Plan(level, jump, flight, out var plan))
                    {
                        level.DemoRunUp = plan.RunUp;
                        level.DemoJumpAt = plan.JumpAt;
                        level.DemoOpenY = plan.OpenY;
                        found++;
                    }
                    EditorUtility.SetDirty(level);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Demo] menu demo plans: {found} of {total} levels land on the flag");
        }

        static bool Plan(LevelConfig level, JumpConfig jump, FlightConfig flight, out Autopilot.JumpPlan plan)
        {
            var layout = LevelLayout.Build(level, jump);
            float runway = layout.EdgeX - layout.BackLimitX;
            plan = Autopilot.Plan(new JumpSession(layout, jump, flight), Mathf.Max(0.25f, runway / 24f));
            if (!plan.Found) return false;
            // Replay exactly like the game does (the pilot acts before every fixed step) and keep it only if it lands on the flag.
            var s = new JumpSession(layout, jump, flight);
            var pilot = Autopilot.FromPlan(plan);
            for (int i = 0; i < 20000 && !s.Phase.IsFinished(); i++)
            {
                pilot.Update(s);
                s.Step(s.StepSize);
            }
            return s.Outcome == JumpOutcome.WellDone;
        }
    }
}
