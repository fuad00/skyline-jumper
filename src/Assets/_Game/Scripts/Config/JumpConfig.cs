using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>
    /// Разбег по крыше и толчок с края (тапы 1–3 из оригинала). Числа разбега и прыжка — из справки оригинала
    /// (reference/decoded/gameplay_reference.json, px/с → м/с делением на 44 px/м).
    /// </summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Jump Config", fileName = "JumpConfig")]
    public class JumpConfig : ScriptableObject
    {
        [Header("Тап 1 — шаги назад от края")]
        [Tooltip("Скорость, с которой стикмен пятится назад, м/с.")]
        public float WalkBackSpeed = 2.5f;
        [Tooltip("Где стикмен стоит в начале: столько метров от края крыши.")]
        public float StartDistanceFromEdge = 0.8f;
        [Tooltip("Ближе этого к дальнему краю крыши он не отходит (останавливается сам), м.")]
        public float BackWallMargin = 0.8f;

        [Header("Тап 2 — разбег к краю")]
        [Tooltip("Ускорение бега, м/с² (оригинал: 165 px/с²). Чем длиннее разбег, тем больше скорость у края.")]
        public float RunAcceleration = 3.75f;
        [Tooltip("Максимальная скорость бега, м/с (оригинал: 1000 px/с).")]
        public float RunMaxSpeed = 22.7f;

        [Header("Тап 3 — прыжок")]
        [Tooltip("Скорость толчка вверх, м/с (оригинал: 500 px/с).")]
        public float JumpUpSpeed = 11.36f;
        [Tooltip("Скорость по X при толчке = скорость бега × это (оригинал: 2) — отсюда дальние полёты.")]
        public float JumpHorizontalMultiplier = 2f;
        [Tooltip("Наименьшая скорость толчка, м/с (оригинал: 205 px/с): даже с очень короткого разбега прыгает хотя бы так (×2 по X).")]
        public float JumpSpeedGate = 4.66f;
        [Tooltip("Проспал край: насколько ступни могут выйти за край, прежде чем он сорвётся, м.")]
        public float EdgeOverrun = 0.15f;
    }
}
