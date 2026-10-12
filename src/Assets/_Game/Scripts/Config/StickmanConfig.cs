using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>Размеры стикмена, анимация позы, ragdoll и замедление при ударе.</summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Stickman Config", fileName = "StickmanConfig")]
    public class StickmanConfig : ScriptableObject
    {
        [Header("Размеры, м")]
        public float HeadSize = 0.56f;
        public float TorsoLength = 0.55f;
        public float UpperArmLength = 0.3f;
        public float LowerArmLength = 0.3f;
        public float UpperLegLength = 0.4f;
        public float LowerLegLength = 0.4f;
        [Tooltip("Толщина рук, ног и тела.")]
        public float LimbThickness = 0.13f;
        [Tooltip("Цвет передних рук и ног.")]
        public Color FrontColor = new Color(0.06f, 0.06f, 0.07f);
        [Tooltip("Цвет задних рук и ног (чуть светлее — даёт глубину).")]
        public Color BackColor = new Color(0.24f, 0.24f, 0.26f);

        [Header("Анимация")]
        [Tooltip("Длина шага при беге, м (частота шагов = скорость / длина).")]
        public float StrideLength = 1.5f;
        [Tooltip("Как быстро поза перетекает в новую (больше — резче).")]
        public float PoseSharpness = 12f;

        [Header("Ragdoll")]
        [Tooltip("Масса всего тела, кг (делится между частями).")]
        public float Mass = 70f;
        [Tooltip("Насколько тело закручивается при ударе.")]
        public float CrashSpin = 6f;

        [Header("Замедление при ударе")]
        [Tooltip("Скорость времени во время замедления (0.3 = в 3 раза медленнее).")]
        [Range(0.05f, 1f)] public float SlowMoScale = 0.3f;
        [Tooltip("Сколько реальных секунд длится замедление.")]
        public float SlowMoDuration = 0.8f;
    }
}
