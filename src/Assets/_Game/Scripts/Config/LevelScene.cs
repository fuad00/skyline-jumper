using System;
using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>
    /// Уровень, расставленный картинками (как в редакторе оригинала): каждая картинка на своём месте, отдельно —
    /// прямоугольники столкновений, полоса разбега и платформа-цель. Ассет создаёт импорт уровней оригинала (заглушки).
    /// Всё в метрах, улица на высоте 0. Уровни, где в оригинале прыгают влево, отзеркалены — у нас всегда вправо.
    /// </summary>
    public sealed class LevelScene : ScriptableObject
    {
        [Serializable]
        public struct Picture
        {
            public Sprite Sprite;
            [Tooltip("Центр картинки, м.")]
            public Vector2 Position;
            [Tooltip("Масштаб (картинки импортированы 44 px на метр); минус — отражена.")]
            public Vector2 Scale;
            [Tooltip("Поворот, градусы против часовой.")]
            public float Rotation;
            [Tooltip("Слой из редактора оригинала: больше — ближе.")]
            public int Layer;
            [Range(0f, 1f)] public float Alpha;
            [Tooltip("Особая роль: LightSprite (ночью светлеет рядом со светом), Bird, Helicopter, Glass или пусто.")]
            public string Kind;
            [Tooltip("Ночью всегда освещена (облака оригинала).")]
            public bool AlwaysLit;
            [Tooltip("Твёрдая (её столкновения — в Solids). Нужно, чтобы открыть уровень в редакторе карт.")]
            public bool Solid;
        }

        [Serializable]
        public struct LightSource
        {
            public Vector2 Position;
            [Tooltip("До этого расстояния освещено полностью, м.")]
            public float RadiusFull;
            [Tooltip("Дальше этого — темно, м.")]
            public float RadiusFade;
        }

        /// <summary>
        /// Подсказка, стоящая в уровне (как в оригинале): надпись на своём месте, появляется в нужной фазе (и, если задано,
        /// когда ступни стикмена в области Trigger), через ShowDelay; WaitForTouch — игра замирает до тапа. Тап её убирает.
        /// </summary>
        [Serializable]
        public struct Tutorial
        {
            [Tooltip("Имя надписи в уровне (текст на языке игры — UIText.Tutorial по «уровень:имя»).")]
            public string Key;
            [Tooltip("Текст из файла уровня (если перевода нет).")]
            public string Text;
            [Tooltip("Центр надписи, м.")]
            public Vector2 Position;
            [Tooltip("Высота строки, м.")]
            public float LineHeight;
            [Tooltip("Фаза стикмена, как в оригинале: 0 — стоит, 1 — пятится, 2 — бежит, 3 — летит, 4 — под куполом, 8 — бежит по промежуточной платформе.")]
            public int RequiredState;
            [Tooltip("Показать через столько секунд после того, как совпали фаза (и область), с.")]
            public float ShowDelay;
            [Tooltip("Остановить игру и ждать тапа (тап и уберёт надпись, и сделает своё дело).")]
            public bool WaitForTouch;
            [Tooltip("Показать, только когда ступни внутри этой области, м (ширина 0 — где угодно).")]
            public Rect Trigger;
        }

        public Picture[] Pictures = new Picture[0];
        [Tooltip("Твёрдые прямоугольники (здания, краны, мосты…), м.")]
        public Rect[] Solids = new Rect[0];
        [Tooltip("Полоса разбега на стартовой крыше.")]
        public Rect Runway;
        [Tooltip("Платформа-цель (флажок — в её середине).")]
        public Rect Platform;
        [Tooltip("Промежуточные платформы: сел — следующий разбег отсюда (полоса, как у разбега).")]
        public Rect[] InterPlatforms = new Rect[0];
        [Tooltip("С промежуточной платформы бежать влево (иначе вправо).")]
        public bool[] InterLeft = new bool[0];
        [Tooltip("Источники света ночью (кроме самого стикмена).")]
        public LightSource[] Lights = new LightSource[0];
        [Tooltip("Подсказки этого уровня (у оригинала — только там, где появляется новое).")]
        public Tutorial[] Tutorials = new Tutorial[0];
        [Tooltip("Обучающий уровень (оригинал: guidedLevel): тапы работают, только пока видна подсказка.")]
        public bool Guided;
        [Tooltip("Размер уровня, м.")]
        public Vector2 Size;
        public bool Mirrored;
        public bool Night;
        public bool Earthquakes;
        [Tooltip("Ветер из файла оригинала (как есть, в его единицах).")]
        public float WindRaw;
        [Tooltip("Ограничение скорости падения из файла оригинала (как есть), 0 — нет.")]
        public float MaxFlySpeedYRaw;
        public Vector2 CameraStart, CameraEnd, CameraZoom;
    }
}
