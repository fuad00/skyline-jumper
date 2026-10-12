using System;
using UnityEngine;

namespace StickmanJumper.Config
{
    [Serializable]
    public class BuildingArt
    {
        [Tooltip("Повторяющийся кусок фасада (тянется вверх плиткой).")]
        public Sprite Body;
        [Tooltip("Первый этаж (внизу).")]
        public Sprite Ground;
        [Tooltip("Бортик крыши (наверху).")]
        public Sprite Roof;
        [Tooltip("Цвет, если спрайтов нет.")]
        public Color Fallback = new Color(0.45f, 0.5f, 0.56f);
    }

    /// <summary>
    /// Все картинки игры в одном месте. Пустое поле = игра рисует простую замену сама (поэтому работает и без заглушек).
    /// Заглушки из оригинала лежат в Assets/_ThirdPartyPlaceholder/ и перечислены в reference/PLACEHOLDER_ASSETS.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Art Bank", fileName = "ArtBank")]
    public class ArtBank : ScriptableObject
    {
        [Header("Свои картинки (генерирует ProjectSetup)")]
        [Tooltip("Материал для всех спрайтов (URP Sprite-Unlit).")]
        public Material SpriteMaterial;
        public Sprite White;
        public Sprite Circle;
        [Tooltip("Капсула для рук, ног и тела (9-slice).")]
        public Sprite Limb;
        public Sprite SkyGradient;
        public Sprite Star;
        public Sprite StarEmpty;

        [Header("Здания (по стилям: Office, Brick, Glass, Dark, Beige)")]
        public BuildingArt[] Buildings = new BuildingArt[5];

        [Header("Стикмен и парашют")]
        public Sprite Head;
        [Tooltip("Кадры раскрытия купола по порядку.")]
        public Sprite[] CanopyOpening = new Sprite[0];
        [Tooltip("Полностью раскрытый купол со стропами.")]
        public Sprite CanopyOpen;

        [Header("Цель")]
        public Sprite Arrow;
        public Sprite Flag;

        [Header("Улица и фон")]
        public Sprite Skyline;
        public Sprite[] Clouds = new Sprite[0];
        public Sprite[] Trees = new Sprite[0];
        public Sprite[] Bushes = new Sprite[0];
        public Sprite Lamp;
        public Sprite Hydrant;

        [Header("Иконки интерфейса")]
        public Sprite IconTime;
        public Sprite IconWind;
        public Sprite IconZoom;

        public BuildingArt Building(BuildingStyle style)
        {
            int i = (int)style;
            return Buildings != null && i >= 0 && i < Buildings.Length && Buildings[i] != null ? Buildings[i] : new BuildingArt();
        }
    }
}
