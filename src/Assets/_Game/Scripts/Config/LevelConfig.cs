using System;
using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>Внешний вид здания (набор спрайтов). Влияет только на картинку, не на физику.</summary>
    public enum BuildingStyle { Office = 0, Brick = 1, Glass = 2, Dark = 3, Beige = 4 }

    [Serializable]
    public struct BuildingSpec
    {
        [Tooltip("Левый край здания, м (край стартовой крыши обычно = 0).")]
        public float X;
        [Tooltip("Ширина здания, м.")]
        public float Width;
        [Tooltip("Высота крыши над улицей, м (1 этаж ≈ 1 м).")]
        public float Height;
        public BuildingStyle Style;

        public BuildingSpec(float x, float width, float height, BuildingStyle style)
        {
            X = x;
            Width = width;
            Height = height;
            Style = style;
        }
    }

    /// <summary>
    /// Один уровень: откуда прыгаем, куда надо сесть, ветер, лимит времени, пороги звёзд.
    /// Ось X вправо, Y вверх, улица на высоте 0. 1 единица = 1 метр.
    /// </summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Level Config", fileName = "Level")]
    public class LevelConfig : ScriptableObject
    {
        [Tooltip("Имя уровня на экране итога.")]
        public string Title = "Уровень 1";
        [Tooltip("Ключ для сохранения рекорда. Не меняй после релиза.")]
        public string Id = "easy1";

        [Header("Уровень оригинала (если задан — здания ниже не используются)")]
        [Tooltip("Картинки, столкновения, разбег и цель из уровня оригинала (заглушки).")]
        public LevelScene Scene;

        [Header("Здания")]
        [Tooltip("Здание, с крыши которого прыгаем. Прыгаем вправо, с его правого края.")]
        public BuildingSpec Start = new BuildingSpec(-16.3f, 16.3f, 60f, BuildingStyle.Office);
        [Tooltip("Здание с красным флажком — сюда надо сесть.")]
        public BuildingSpec Target = new BuildingSpec(17f, 18.2f, 18f, BuildingStyle.Brick);
        [Tooltip("Остальные здания (о них тоже можно разбиться).")]
        public BuildingSpec[] Others = new BuildingSpec[0];

        [Header("Цель")]
        [Tooltip("Где красный флажок на крыше цели: 0 = левый край крыши, 1 = правый.")]
        [Range(0f, 1f)] public float ArrowPosition = 0.5f;

        [Header("Помехи")]
        [Tooltip("Ветер, м/с. Плюс — дует вправо, минус — влево.")]
        public float Wind = 1.5f;
        [Tooltip("Самый сильный ветер в игре (полная полоска на панели ветра), м/с.")]
        public float WindMax = 6f;
        [Tooltip("Лимит времени, сек. Считается с начала разбега (пока пятится — не считается), как в оригинале.")]
        public float TimeLimit = 30f;
        [Tooltip("Своя предельная скорость падения без купола, м/с (0 — общая из FlightConfig). В оригинале так ускоряли падение на самых высоких уровнях.")]
        public float MaxFallSpeed;

        [Header("Заставка в меню (считается автоматически: Stickman Jumper → Recompute Menu Demo Plans)")]
        [Tooltip("Разбег, с которым автопилот садится точно на флажок. NaN — уровень в заставку не берём.")]
        public float DemoRunUp = float.NaN;
        [Tooltip("На какой высоте автопилот раскрывает купол, м.")]
        public float DemoOpenY = float.NaN;

        [Header("Звёзды")]
        [Tooltip("2 звезды: сел не дальше этого от флажка, м.")]
        public float TwoStarDistance = 2f;
        [Tooltip("3 звезды: сел не дальше этого от флажка, м …")]
        public float ThreeStarDistance = 0.75f;
        [Tooltip("… и уложился в это время, сек.")]
        public float ThreeStarTime = 15f;
    }
}
