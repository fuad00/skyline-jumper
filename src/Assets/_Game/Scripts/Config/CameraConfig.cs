using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>Камера сбоку: следит за стикменом и отъезжает на скорости. Кнопка зума показывает весь уровень.</summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Camera Config", fileName = "CameraConfig")]
    public class CameraConfig : ScriptableObject
    {
        [Header("Приближение (половина высоты кадра, м)")]
        [Tooltip("На крыше перед прыжком.")]
        public float SizeOnRoof = 6f;
        [Tooltip("В полёте на малой скорости.")]
        public float SizeInFlight = 9f;
        [Tooltip("Добавка за каждый м/с скорости.")]
        public float SizePerSpeed = 0.14f;
        [Tooltip("Самое далёкое в полёте.")]
        public float SizeMax = 13f;
        [Tooltip("Насколько быстро меняется приближение (больше — резче).")]
        public float SizeSharpness = 2.5f;

        [Header("Слежение")]
        [Tooltip("Насколько быстро камера догоняет стикмена (больше — жёстче).")]
        public float FollowSharpness = 6f;
        [Tooltip("Смотреть вперёд по скорости: сколько секунд полёта показывать впереди.")]
        public float LookAhead = 0.35f;
        [Tooltip("Ограничение упреждения, м.")]
        public float LookAheadMax = 6f;

        [Header("Обзор уровня (кнопка зума и старт)")]
        [Tooltip("Поля вокруг уровня в режиме обзора, м.")]
        public float OverviewMargin = 4f;

        [Header("Удар")]
        [Tooltip("Тряска камеры при ударе, м.")]
        public float ShakeOnCrash = 0.35f;
    }
}
