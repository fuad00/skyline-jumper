using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>
    /// Очки за удачную посадку. Как считал оригинал — неизвестно, поэтому формула своя:
    /// очки = точность (макс. AccuracyPoints у самой стрелки, 0 на краю крыши) + время (макс. TimePoints за мгновенный прыжок, 0 на лимите).
    /// </summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Score Config", fileName = "ScoreConfig")]
    public class ScoreConfig : ScriptableObject
    {
        [Tooltip("Очки за посадку точно в стрелку.")]
        public int AccuracyPoints = 1000;
        [Tooltip("Очки за скорость (сколько дают, если уложиться в 0 секунд).")]
        public int TimePoints = 500;
    }
}
