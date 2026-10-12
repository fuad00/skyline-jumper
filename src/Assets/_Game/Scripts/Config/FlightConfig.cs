using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>Свободное падение, парашют (тап 4), ветер и правила посадки.</summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Flight Config", fileName = "FlightConfig")]
    public class FlightConfig : ScriptableObject
    {
        [Header("Свободное падение")]
        [Tooltip("Ускорение свободного падения, м/с².")]
        public float Gravity = 9.81f;
        [Tooltip("Предельная скорость падения без парашюта (сопротивление воздуха), м/с.")]
        public float FreefallTerminalSpeed = 45f;

        [Header("Парашют")]
        [Tooltip("Сколько секунд купол раскрывается полностью.")]
        public float CanopyOpenTime = 0.8f;
        [Tooltip("Скорость спуска под полностью раскрытым куполом, м/с.")]
        public float CanopyTerminalSpeed = 4.5f;
        [Tooltip("Купол сам немного летит вперёд (в сторону прыжка), м/с. Рулить им нельзя, как в оригинале.")]
        public float CanopyGlideSpeed = 1.2f;

        [Header("Посадка и удары")]
        [Tooltip("Максимальная скорость касания, при которой стикмен приземляется на ноги, м/с. Быстрее — разбивается.")]
        public float SafeLandingSpeed = 7f;
        [Tooltip("Удар о стену быстрее этого (поперёк стены) — разбился. Медленнее — просто скользит вдоль, м/с.")]
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
