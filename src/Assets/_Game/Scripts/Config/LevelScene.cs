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
            [Tooltip("Особая роль: Bird, Helicopter, Glass или пусто.")]
            public string Kind;
        }

        public Picture[] Pictures = new Picture[0];
        [Tooltip("Твёрдые прямоугольники (здания, краны, мосты…), м.")]
        public Rect[] Solids = new Rect[0];
        [Tooltip("Полоса разбега на стартовой крыше.")]
        public Rect Runway;
        [Tooltip("Платформа-цель (флажок — в её середине).")]
        public Rect Platform;
        [Tooltip("Промежуточные платформы (в оригинале на них садятся и прыгают дальше; у нас пока просто крыши).")]
        public Rect[] InterPlatforms = new Rect[0];
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
