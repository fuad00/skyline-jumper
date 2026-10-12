using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>Размеры стикмена, анимация позы, ragdoll и замедление при ударе.</summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Stickman Config", fileName = "StickmanConfig")]
    public class StickmanConfig : ScriptableObject
    {
        [Header("Размеры, м (пропорции оригинала: голова ≈ 40% роста)")]
        public float HeadSize = 0.8f;
        public float TorsoLength = 0.5f;
        public float UpperArmLength = 0.42f;
        public float LowerArmLength = 0.38f;
        public float UpperLegLength = 0.34f;
        public float LowerLegLength = 0.34f;
        [Tooltip("Высота щиколотки над крышей (ступня под ней).")]
        public float AnkleHeight = 0.04f;
        [Tooltip("Длина ступни.")]
        public float FootLength = 0.13f;
        [Tooltip("Толщина рук и ног (тело чуть толще).")]
        public float LimbThickness = 0.085f;
        [Tooltip("Цвет передних рук и ног.")]
        public Color FrontColor = new Color(0.05f, 0.05f, 0.06f);
        [Tooltip("Цвет задних рук и ног (чуть светлее — даёт глубину).")]
        public Color BackColor = new Color(0.17f, 0.17f, 0.19f);

        [Header("Анимации оригинала")]
        [Tooltip("Рост стикмена в стойке, м (по нему масштабируются картинки частей тела).")]
        public float Height = 1.9f;
        [Tooltip("С какой скоростью идёт ходьба оригинала без ускорения, м/с: быстрее идёт — быстрее проигрываем, ступни не скользят.")]
        public float WalkClipSpeed = 2.1f;
        [Tooltip("То же для бега, м/с.")]
        public float RunClipSpeed = 1.75f;
        [Tooltip("Быстрее этого анимацию бега не крутим (в разы), чтобы ноги не мельтешили.")]
        public float RunClipMaxRate = 3.2f;

        [Header("Анимация простого стикмена (если анимаций оригинала нет)")]
        [Tooltip("Двойной шаг при ходьбе, м. Частота шагов = скорость / шаг, поэтому ступни не скользят.")]
        public float WalkStride = 0.95f;
        [Tooltip("Двойной шаг при беге: база + добавка за каждый м/с, но не больше максимума, м.")]
        public float RunStrideBase = 1.05f;
        public float RunStridePerSpeed = 0.09f;
        public float RunStrideMax = 1.95f;
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
