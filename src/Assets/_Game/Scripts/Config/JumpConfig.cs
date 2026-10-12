using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>Разбег по крыше и толчок с края (тапы 1–3 из оригинала).</summary>
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
        [Tooltip("Ускорение бега, м/с². Чем длиннее разбег, тем больше скорость у края.")]
        public float RunAcceleration = 5f;
        [Tooltip("Максимальная скорость бега, м/с.")]
        public float RunMaxSpeed = 10f;

        [Header("Тап 3 — прыжок")]
        [Tooltip("Скорость толчка вверх, м/с. Горизонтальная скорость берётся из разбега.")]
        public float JumpUpSpeed = 5.2f;
        [Tooltip("Проспал край: насколько ступни могут выйти за край, прежде чем он сорвётся, м.")]
        public float EdgeOverrun = 0.15f;
    }
}
