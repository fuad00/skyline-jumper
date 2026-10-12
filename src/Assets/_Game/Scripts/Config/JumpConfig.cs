using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>
    /// Отход, разбег по крыше и толчок с края (тапы 1–3 из оригинала), таймер уровня. Числа — из кода оригинала
    /// (reference/disasm/FINDINGS.md), в px; «за тик» — за 1/FlightConfig.TicksPerSecond секунды. В метры их переводит FlightConfig.
    /// </summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Jump Config", fileName = "JumpConfig")]
    public class JumpConfig : ScriptableObject
    {
        [Header("Тап 1 — шаги назад от края")]
        [Tooltip("Скорость, с которой стикмен начинает пятиться, px/с (оригинал: 63).")]
        public float WalkBackStartSpeedPx = 63f;
        [Tooltip("Прибавка скорости отхода за тик, px/с (оригинал: 0.225).")]
        public float WalkBackGainPerTick = 0.225f;
        [Tooltip("Быстрее этого не пятится, px/с (оригинал: 120).")]
        public float WalkBackMaxSpeedPx = 120f;
        [Tooltip("Где стикмен стоит в начале: столько метров от края крыши (наше).")]
        public float StartDistanceFromEdge = 0.8f;
        [Tooltip("Ближе этого к дальнему краю крыши он не отходит (останавливается сам), м (наше).")]
        public float BackWallMargin = 0.8f;

        [Header("Тап 2 — разбег к краю")]
        [Tooltip("Скорость в начале разбега, px/с (оригинал: 10).")]
        public float RunStartSpeedPx = 10f;
        [Tooltip("Прибавка скорости бега за тик, px/с: x — при скорости RunStartSpeedPx, y — при RunGainRefSpeedPx, между и дальше — " +
                 "по прямой, без обрезки (оригинал: 3 и 1 → скорость стремится к 745 px/с).")]
        public Vector2 RunGainPerTick = new Vector2(3f, 1f);
        [Tooltip("Скорость, при которой прибавка = RunGainPerTick.y, px/с (оригинал: 500).")]
        public float RunGainRefSpeedPx = 500f;

        [Header("Тап 3 — прыжок (тап сразу толкает, порога нет)")]
        [Tooltip("Скорость по X при толчке = скорость бега × это (оригинал: 0.925).")]
        public float JumpForwardFactor = 0.925f;
        [Tooltip("Скорость толчка вверх, px/с: x — при скорости бега 0, y — при JumpUpRefSpeedPx, по прямой (оригинал: 180 и 174).")]
        public Vector2 JumpUpSpeedPx = new Vector2(180f, 174f);
        [Tooltip("Скорость бега, при которой толчок вверх = JumpUpSpeedPx.y, px/с (оригинал: 500).")]
        public float JumpUpRefSpeedPx = 500f;
        [Tooltip("Проспал край: насколько ступни могут выйти за край, прежде чем он сорвётся, м (наше).")]
        public float EdgeOverrun = 0.15f;

        [Header("Таймер уровня")]
        [Tooltip("Вкл — время идёт с первого тапа (пока пятится — тоже). Выкл — с начала разбега (оригинал).")]
        public bool TimerFromFirstTap;
    }
}
