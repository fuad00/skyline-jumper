using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickmanJumper.Maps
{
    /// <summary>Деталь карты: картинка из палитры на своём месте. Всё в метрах, улица на высоте 0.</summary>
    [Serializable]
    public class MapPiece
    {
        [Tooltip("Id детали (PieceDef.Id).")]
        public string Art = string.Empty;
        [Tooltip("Центр картинки, м.")]
        public float X, Y;
        [Tooltip("Масштаб; минус по X — отражена.")]
        public float ScaleX = 2f, ScaleY = 2f;
        [Tooltip("Поворот, градусы против часовой.")]
        public float Rotation;
        [Tooltip("Слой: больше — ближе. При равном слое ближе та, что позже в списке.")]
        public int Layer;
        [Tooltip("Твёрдая: о неё разбиваются и на неё садятся. Нет — просто картинка (фон).")]
        public bool Solid = true;

        public Vector2 Position
        {
            get => new Vector2(X, Y);
            set { X = value.x; Y = value.y; }
        }

        public Vector2 Scale => new Vector2(ScaleX, ScaleY);

        public MapPiece Clone() => (MapPiece)MemberwiseClone();
    }

    /// <summary>
    /// Площадка карты: старт (полоса разбега), цель (флажок) или промежуточная платформа.
    /// X — левый конец, Y — высота поверхности (там стоят ступни), Width — длина.
    /// </summary>
    [Serializable]
    public class MapStrip
    {
        public float X, Y, Width;
        [Tooltip("С промежуточной платформы бежать влево (иначе вправо). Со старта всегда вправо.")]
        public bool Left;

        public MapStrip() { }

        public MapStrip(float x, float y, float width, bool left = false)
        {
            X = x;
            Y = y;
            Width = width;
            Left = left;
        }

        public float XMax => X + Width;
        public float CenterX => X + Width * 0.5f;

        public MapStrip Clone() => (MapStrip)MemberwiseClone();
    }

    /// <summary>
    /// Карта игрока: детали, старт, цель, промежуточные платформы и настройки прыжка. Хранится JSON-файлом на телефоне,
    /// делится коротким кодом (<see cref="MapCode"/>). Version — чтобы потом безопасно менять формат.
    /// </summary>
    [Serializable]
    public class MapData
    {
        public const int CurrentVersion = 1;
        public const float MinTime = 5f, MaxTime = 120f, MaxWind = 3f;

        public int Version = CurrentVersion;
        [Tooltip("Имя файла на телефоне (у каждой карты своё).")]
        public string Id = string.Empty;
        public string Name = string.Empty;
        [Tooltip("Когда меняли последний раз (секунды Unix) — для порядка в списке.")]
        public long Updated;

        [Tooltip("Лимит времени, сек.")]
        public float TimeLimit = 30f;
        [Tooltip("Ветер, м/с: плюс — вправо.")]
        public float Wind;
        public bool Night;

        public List<MapPiece> Pieces = new List<MapPiece>();
        public MapStrip Start = new MapStrip();
        public MapStrip Target = new MapStrip();
        public List<MapStrip> Platforms = new List<MapStrip>();

        [Tooltip("Отпечаток карты, когда автор сел на флажок (пусто — ещё не проходил). Поделиться можно только пройденной.")]
        public string VerifiedHash = string.Empty;
        [Tooltip("Получена от другого игрока по коду.")]
        public bool Imported;
        [Tooltip("Автор прошёл её перед тем, как поделиться (для полученных по коду).")]
        public bool AuthorVerified;

        /// <summary>Пройдена ли карта в её нынешнем виде (после любой правки — снова нет).</summary>
        public bool Verified => !string.IsNullOrEmpty(VerifiedHash) && VerifiedHash == MapCode.ContentHash(this);

        /// <summary>Ключ для сохранения рекорда карты.</summary>
        public string LevelId => "map:" + Id;

        public MapData Clone() => JsonUtility.FromJson<MapData>(JsonUtility.ToJson(this));
    }
}
