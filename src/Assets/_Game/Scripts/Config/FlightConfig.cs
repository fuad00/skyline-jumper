using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>
    /// Полёт как в оригинале — числа прочитаны из его кода (разбор: reference/disasm/FINDINGS.md, 2026-10-13), в его единицах, px.
    /// Его физика считает в «точках»: при загрузке уровня он умножает координаты файла на 0.5. В файлах уровней 44 единицы
    /// на наш метр (рост стикмена 83 = 1.9 м), значит в физике — <see cref="PixelsPerMeter"/> = 22.
    /// Оригинал считает физику 60 раз в секунду, и часть чисел у него «за тик» (за 1/60 с) — они так и записаны,
    /// при другом шаге физики пересчитываются сами. Свойства без «Px» — те же числа в м и м/с для расчётов.
    /// </summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Flight Config", fileName = "FlightConfig")]
    public class FlightConfig : ScriptableObject
    {
        [Header("Мир (числа оригинала)")]
        [Tooltip("Сколько единиц физики оригинала в нашем метре: 22 (файлы уровней — 44, оригинал при загрузке ×0.5).")]
        public float PixelsPerMeter = 22f;
        [Tooltip("Тиков оригинала в секунду: числа «за тик» ниже — за 1/этого секунды (оригинал: 60).")]
        public int TicksPerSecond = 60;
        [Tooltip("Шагов нашей физики в секунду (оригинал: 60 — один тик на кадр; так совпадает до шага).")]
        public int StepsPerSecond = 60;

        [Header("Свободный полёт (после толчка)")]
        [Tooltip("Гравитация, px/с² (оригинал: 165).")]
        public float GravityPx = 165f;
        [Tooltip("Предел скорости свободного падения, px/с (оригинал: 1000; уровень может задать свой — maxFlySpeedY).")]
        public float FallSpeedCapPx = 1000f;
        [Tooltip("Ветер в игре = ветер уровня × это (оригинал: 3).")]
        public float WindScale = 3f;
        [Tooltip("Ветер в свободном полёте: скорость по X += ветер в игре × это, за тик (оригинал: 0.000725). Сопротивления воздуха нет.")]
        public float FlightWindPerTick = 0.000725f;

        [Header("Парашют: раскрытие (тап 4)")]
        [Tooltip("Раскрыл, пока ещё летит вверх: скорость умножается на это (оригинал: 0.795).")]
        public float CanopyBrakeRising = 0.795f;
        [Tooltip("Раскрыл в падении: скорость умножается на x при спуске 0 px/с и на y при спуске CanopyBrakeRefPx, " +
                 "между ними и дальше — по прямой (оригинал: 0.895 и 0.7).")]
        public Vector2 CanopyBrakeFalling = new Vector2(0.895f, 0.7f);
        [Tooltip("Спуск, при котором раскрытие умножает на CanopyBrakeFalling.y, px/с (оригинал: 500).")]
        public float CanopyBrakeRefPx = 500f;
        [Tooltip("Быстрее этого спуск в расчёт раскрытия не идёт, px/с (оригинал: 800 → при 800 и быстрее ×0.583).")]
        public float CanopyBrakeMaxSpeedPx = 800f;
        [Tooltip("Анимация раскрытия, с (на физику не влияет).")]
        public float CanopyOpenTime = 0.6f;
        [Tooltip("Тап под куполом отрывает его (быстро разбиться и не ждать). Раньше этого после раскрытия тап не считается — " +
                 "чтобы двойной тап не оторвал купол сразу, с (наше).")]
        public float CanopyCutDelay = 0.3f;

        [Header("Парашют: спуск")]
        [Tooltip("Под куполом тянет вниз гравитация × k. k = x, пока спуск быстрее CanopyLiftFromPx; медленнее (и вверх) — " +
                 "растёт по прямой до y при скорости CanopyLiftToPx (оригинал: 0.04 и 0.85).")]
        public Vector2 CanopyGravity = new Vector2(0.04f, 0.85f);
        [Tooltip("С какой вертикальной скорости k начинает расти, px/с (оригинал: −300).")]
        public float CanopyLiftFromPx = -300f;
        [Tooltip("При какой вертикальной скорости k = CanopyGravity.y, px/с (оригинал: 100).")]
        public float CanopyLiftToPx = 100f;
        [Tooltip("Сопротивление купола включается, когда полная скорость больше этого (+ доля ветра ниже), px/с (оригинал: 68). " +
                 "Отсюда спуск без ветра ≈ 68 px/с.")]
        public float CanopyDragFromPx = 68f;
        [Tooltip("Доля ветра в игре, которая прибавляется к порогу: x — сразу после раскрытия, y — через CanopyDragWindTicks " +
                 "(оригинал: 0.055 и 0.195).")]
        public Vector2 CanopyDragWind = new Vector2(0.055f, 0.195f);
        [Tooltip("За сколько тиков доля ветра в пороге дорастает до y (оригинал: 75).")]
        public float CanopyDragWindTicks = 75f;
        [Tooltip("Сопротивление за тик: вертикаль × f. Пока по X быстрее CanopySlowSidePx: f от x сразу после раскрытия до y " +
                 "через CanopyDragTicks (оригинал: 0.975 и 0.955).")]
        public Vector2 CanopyDrag = new Vector2(0.975f, 0.955f);
        [Tooltip("За сколько тиков f опускается до CanopyDrag.y (оригинал: 30).")]
        public float CanopyDragTicks = 30f;
        [Tooltip("По X медленнее этого: f каждый тик уменьшается на CanopyDragStep, но не ниже CanopyDrag.y, px/с (оригинал: 65).")]
        public float CanopySlowSidePx = 65f;
        [Tooltip("На сколько уменьшается f за тик при медленном X (оригинал: 0.0095).")]
        public float CanopyDragStep = 0.0095f;
        [Tooltip("f в начале прыжка — с него считает медленный X (оригинал: 0.965).")]
        public float CanopyDragStart = 0.965f;
        [Tooltip("По X сопротивление чуть слабее: X × (f + это) (оригинал: 0.01).")]
        public float CanopyDragSideBonus = 0.01f;
        [Tooltip("Ветер под куполом: скорость по X += ветер в игре × это, за тик (оригинал: 0.0075 — в 10 раз сильнее, чем без купола).")]
        public float CanopyWindPerTick = 0.0075f;

        [Header("Посадка и удары")]
        [Tooltip("Встаёт на ноги, только если купол раскрыт и полная скорость не больше этого, px/с (оригинал: 110). " +
                 "Без купола любое касание — разбился.")]
        public float SoftLandingSpeedPx = 110f;
        [Tooltip("При ветре на уровне хватает и вертикальной скорости меньше SoftLandingSpeedPx (оригинал: так, ветер сносит по X).")]
        public bool WindyLandingByVertical = true;
        [Tooltip("Удар о стену быстрее этого (поперёк стены) — разбился, медленнее — скользит вдоль, px/с (оригинал: 0 — любое касание стены).")]
        public float WallCrashSpeedPx = 0f;
        [Tooltip("Предел скорости частей тела после падения (ragdoll), px/с (оригинал: 900).")]
        public float RagdollSpeedCapPx = 900f;

        [Header("Тело для столкновений (наше, м)")]
        [Tooltip("Ширина коробки тела, м.")]
        public float BodyWidth = 0.5f;
        [Tooltip("Высота коробки тела, м.")]
        public float BodyHeight = 1.8f;

        /// <summary>px → м.</summary>
        public float Meters(float px) => px / Mathf.Max(0.001f, PixelsPerMeter);

        public float Gravity => Meters(GravityPx);
        public float FallSpeedCap => Meters(FallSpeedCapPx);
        public float SoftLandingSpeed => Meters(SoftLandingSpeedPx);
        public float WallCrashSpeed => Meters(WallCrashSpeedPx);
        public float RagdollSpeedCap => Meters(RagdollSpeedCapPx);
    }
}
