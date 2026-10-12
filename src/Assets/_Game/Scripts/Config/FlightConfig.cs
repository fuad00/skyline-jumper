using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>
    /// Свободное падение, парашют (тап 4), ветер и правила посадки.
    /// Числа полёта временно подобраны под уровни оригинала (тест OriginalLevelsTests.CalibrationSweep, 2026-10-12):
    /// точные значения оригинала неизвестны — заменить, когда будут.
    /// </summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Flight Config", fileName = "FlightConfig")]
    public class FlightConfig : ScriptableObject
    {
        [Header("Свободное падение")]
        [Tooltip("Ускорение свободного падения, м/с².")]
        public float Gravity = 9.81f;
        [Tooltip("Предельная скорость падения без парашюта (по вертикали), м/с. В оригинале падают медленно: ~100 м за ~8 с. " +
                 "Уровень оригинала может задать свою (maxFlySpeedY).")]
        public float FreefallTerminalSpeed = 28f;
        [Tooltip("Как долго держится скорость по X в свободном падении: «предельная скорость» для горизонтали, м/с. " +
                 "Больше — меньше теряет, летит дальше (в оригинале летят «суперменом» почти без потерь).")]
        public float FreefallGlideSpeed = 60f;

        [Header("Парашют")]
        [Tooltip("Сколько секунд купол раскрывается полностью.")]
        public float CanopyOpenTime = 0.6f;
        [Tooltip("Скорость спуска под полностью раскрытым куполом, м/с.")]
        public float CanopyTerminalSpeed = 6.5f;
        [Tooltip("Как резко купол гасит скорость по X (1/с). 6 = за полсекунды остаётся только снос ветром. Рулить нельзя, как в оригинале.")]
        public float CanopyBrakeX = 6f;
        [Tooltip("Тап под куполом отрывает его (быстро разбиться и не ждать). Раньше этого после раскрытия тап не считается — чтобы двойной тап не оторвал купол сразу, с.")]
        public float CanopyCutDelay = 0.3f;

        [Header("Посадка и удары")]
        [Tooltip("Максимальная скорость касания, при которой стикмен приземляется на ноги, м/с. Быстрее — разбивается.")]
        public float SafeLandingSpeed = 7f;
        [Tooltip("Удар о стену быстрее этого (поперёк стены) — разбился. Медленнее — просто скользит вдоль, м/с. Под куполом любое касание стены — удар.")]
        public float WallCrashSpeed = 3f;

        [Header("Тело для столкновений")]
        [Tooltip("Ширина коробки тела, м.")]
        public float BodyWidth = 0.5f;
        [Tooltip("Высота коробки тела, м.")]
        public float BodyHeight = 1.8f;

        [Header("Точность расчёта")]
        [Tooltip("Шагов физики в секунду (больше — точнее, но дороже).")]
        public int StepsPerSecond = 120;
    }
}
