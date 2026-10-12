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
        [Tooltip("Тетрадный лист в клетку (фон, повторяется плиткой).")]
        public Sprite Paper;
        [Tooltip("Белая дымка у земли (сверху прозрачная).")]
        public Sprite FogBand;
        [Tooltip("Мягкий круглый свет (ночью — ореол за стикменом).")]
        public Sprite Glow;
        [Tooltip("Одна чёрточка пунктира от флажка цели вверх (повторяется плиткой).")]
        public Sprite Dash;
        public Sprite Star;
        public Sprite StarEmpty;

        [Header("Здания (по стилям: Office, Brick, Glass, Dark, Beige)")]
        public BuildingArt[] Buildings = new BuildingArt[5];

        [Header("Стикмен и парашют")]
        [Tooltip("Голова сбоку (один глаз).")]
        public Sprite Head;
        [Tooltip("Голова анфас (два глаза) — под куполом стикмен развёрнут к камере.")]
        public Sprite HeadFront;
        [Tooltip("Секции купола слева направо (встают дугой).")]
        public Sprite[] CanopyOpening = new Sprite[0];
        [Tooltip("Держатель строп над головой (стропы сходятся в его верх).")]
        public Sprite CanopyOpen;

        [Header("Анимации стикмена (из оригинала, заглушки; пусто — простой стикмен из палочек)")]
        [Tooltip("Стоит на крыше.")]
        public Presentation.CharacterClip AnimStand;
        [Tooltip("Идёт (пятится от края — та же ходьба, развёрнутая спиной к краю).")]
        public Presentation.CharacterClip AnimWalk;
        public Presentation.CharacterClip AnimRun;
        [Tooltip("Толчок с края и вытягивание в нырок.")]
        public Presentation.CharacterClip AnimJump;
        [Tooltip("Висит под куполом (купол со стропами — часть анимации).")]
        public Presentation.CharacterClip AnimParachute;
        [Tooltip("Хватается за голову (время вышло).")]
        public Presentation.CharacterClip AnimFastLanding;
        [Tooltip("Сел на флажок: купол опадает, радуется.")]
        public Presentation.CharacterClip AnimWins;
        [Tooltip("Сел мимо: купол опадает, грустит.")]
        public Presentation.CharacterClip AnimLoose;

        public bool HasCharacter => AnimStand != null && AnimWalk != null && AnimRun != null && AnimJump != null && AnimParachute != null;

        public Presentation.CharacterClip[] CharacterClips => new[] { AnimStand, AnimWalk, AnimRun, AnimJump, AnimParachute, AnimFastLanding, AnimWins, AnimLoose };

        [Header("Флажки и линейка")]
        [Tooltip("Красный флажок цели (опора спрайта — низ древка).")]
        public Sprite Flag;
        [Tooltip("Зелёный флажок: откуда стартовал прошлый разбег.")]
        public Sprite FlagLast;
        [Tooltip("Зелёный флажок: на эту промежуточную платформу уже сел.")]
        public Sprite FlagGreen;
        [Tooltip("Сигнальные столбики по краям цели (опора — низ): лампа выключена, зелёная (сел на цель), красная (провал).")]
        public Sprite SignalOff;
        public Sprite SignalGreen;
        public Sprite SignalRed;
        [Tooltip("Цифры 0–9 для меток метров над крышей.")]
        public Sprite[] Digits = new Sprite[0];
        [Tooltip("Стрелка вниз под цифрой метки.")]
        public Sprite MarkerArrow;

        [Header("Улица и фон")]
        public Sprite Skyline;
        public Sprite[] Clouds = new Sprite[0];
        public Sprite[] Trees = new Sprite[0];
        public Sprite[] Bushes = new Sprite[0];
        public Sprite Lamp;
        public Sprite Hydrant;

        [Header("Редактор карт")]
        [Tooltip("Детали для карт (сейчас — картинки уровней оригинала, заглушки). Пусто — редактор без деталей.")]
        public PieceCatalog Pieces;

        [Header("Иконки интерфейса")]
        public Sprite IconTime;
        public Sprite IconWind;
        public Sprite IconZoom;
        public Sprite IconPause;
        [Tooltip("Замок на закрытых этапах и уровнях.")]
        public Sprite IconLock;

        public BuildingArt Building(BuildingStyle style)
        {
            int i = (int)style;
            return Buildings != null && i >= 0 && i < Buildings.Length && Buildings[i] != null ? Buildings[i] : new BuildingArt();
        }
    }
}
