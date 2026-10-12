using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>Этап (пак) уровней, как в оригинале: «1 stage — rookie — your first adrenaline jumps».</summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Stage", fileName = "Stage")]
    public class StageConfig : ScriptableObject
    {
        [Tooltip("Номер этапа на карточке.")]
        public int Number = 1;
        [Tooltip("Название этапа (крупно).")]
        public string Title = "новичок";
        [Tooltip("Подпись под названием.")]
        public string Subtitle = "первые прыжки";
        [Tooltip("Сколько звёзд нужно собрать на прошлых этапах, чтобы открыть этот.")]
        public int StarsToUnlock;
        [Tooltip("Уровни этапа по порядку.")]
        public LevelConfig[] Levels = new LevelConfig[0];
    }
}
