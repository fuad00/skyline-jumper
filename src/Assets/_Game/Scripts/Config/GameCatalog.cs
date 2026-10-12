using System.Collections.Generic;
using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>Все этапы игры по порядку.</summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Game Catalog", fileName = "GameCatalog")]
    public class GameCatalog : ScriptableObject
    {
        public StageConfig[] Stages = new StageConfig[0];

        public LevelConfig Level(int stage, int level) =>
            stage >= 0 && stage < Stages.Length && Stages[stage] != null && level >= 0 && level < Stages[stage].Levels.Length
                ? Stages[stage].Levels[level]
                : null;

        public List<string> LevelIds(int stage)
        {
            var ids = new List<string>();
            if (stage < 0 || stage >= Stages.Length || Stages[stage] == null) return ids;
            foreach (var l in Stages[stage].Levels) ids.Add(l != null ? l.Id : string.Empty);
            return ids;
        }

        /// <summary>Звёзды со всех этапов до указанного (для замков этапов).</summary>
        public List<string> LevelIdsBefore(int stage)
        {
            var ids = new List<string>();
            for (int s = 0; s < stage && s < Stages.Length; s++) ids.AddRange(LevelIds(s));
            return ids;
        }
    }
}
