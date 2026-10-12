using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>
    /// Поза стикмена: углы в градусах в системе тела. Угол 0 — кость смотрит вниз, 90 — вперёд (вправо), 180 — вверх.
    /// Нижние кости (предплечья, голени) задаются сгибом относительно верхних.
    /// </summary>
    public struct StickmanPose
    {
        public float Body;        // наклон всего тела
        public float Torso;       // 180 = прямо вверх
        public float Head;        // наклон головы относительно тела
        public float ArmFront, ElbowFront, ArmBack, ElbowBack;
        public float LegFront, KneeFront, LegBack, KneeBack;
        public float Bob;         // подпрыгивание таза, м

        public static StickmanPose Stand => new StickmanPose
        {
            Torso = 180f, ArmFront = 8f, ElbowFront = 12f, ArmBack = -8f, ElbowBack = 10f,
            LegFront = 5f, LegBack = -5f,
        };

        public static StickmanPose Lerp(StickmanPose a, StickmanPose b, float t)
        {
            return new StickmanPose
            {
                Body = Mathf.LerpAngle(a.Body, b.Body, t),
                Torso = Mathf.LerpAngle(a.Torso, b.Torso, t),
                Head = Mathf.LerpAngle(a.Head, b.Head, t),
                ArmFront = Mathf.LerpAngle(a.ArmFront, b.ArmFront, t),
                ElbowFront = Mathf.LerpAngle(a.ElbowFront, b.ElbowFront, t),
                ArmBack = Mathf.LerpAngle(a.ArmBack, b.ArmBack, t),
                ElbowBack = Mathf.LerpAngle(a.ElbowBack, b.ElbowBack, t),
                LegFront = Mathf.LerpAngle(a.LegFront, b.LegFront, t),
                KneeFront = Mathf.LerpAngle(a.KneeFront, b.KneeFront, t),
                LegBack = Mathf.LerpAngle(a.LegBack, b.LegBack, t),
                KneeBack = Mathf.LerpAngle(a.KneeBack, b.KneeBack, t),
                Bob = Mathf.Lerp(a.Bob, b.Bob, t),
            };
        }

        /// <summary>Шаг (бег или ходьба). phase в радианах, amount 0..1 — размах.</summary>
        public static StickmanPose Stride(float phase, float amount, float lean)
        {
            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);
            return new StickmanPose
            {
                Torso = 180f - lean,
                Head = lean * 0.4f,
                LegFront = s * 42f * amount,
                KneeFront = -Mathf.Max(0f, -c) * 75f * amount - 8f * amount,
                LegBack = -s * 42f * amount,
                KneeBack = -Mathf.Max(0f, c) * 75f * amount - 8f * amount,
                ArmFront = -s * 45f * amount + 5f,
                ElbowFront = 25f + 55f * amount,
                ArmBack = s * 45f * amount - 5f,
                ElbowBack = 25f + 55f * amount,
                Bob = Mathf.Abs(c) * 0.07f * amount,
            };
        }

        /// <summary>Свободное падение: руки-мельницы, ноги «велосипед». Смешно и видно скорость.</summary>
        public static StickmanPose Flail(float t, float panic)
        {
            float a = Mathf.Lerp(0.5f, 1f, panic);
            return new StickmanPose
            {
                Body = -18f + Mathf.Sin(t * 1.7f) * 10f * a,
                Torso = 180f + Mathf.Sin(t * 3f) * 8f,
                Head = Mathf.Sin(t * 5f) * 12f * a,
                ArmFront = 140f + Mathf.Sin(t * 11f) * 45f * a,
                ElbowFront = Mathf.Sin(t * 13f + 1f) * 40f,
                ArmBack = -140f + Mathf.Sin(t * 10f + 2f) * 45f * a,
                ElbowBack = Mathf.Sin(t * 12f) * 40f,
                LegFront = 25f + Mathf.Sin(t * 12f) * 35f * a,
                KneeFront = -45f - Mathf.Cos(t * 12f) * 35f * a,
                LegBack = -10f + Mathf.Sin(t * 12f + Mathf.PI) * 35f * a,
                KneeBack = -45f - Mathf.Cos(t * 12f + Mathf.PI) * 35f * a,
            };
        }

        /// <summary>Висит под куполом, держится за стропы, ноги болтаются.</summary>
        public static StickmanPose Hang(float t, float sway)
        {
            return new StickmanPose
            {
                Body = sway,
                Torso = 180f,
                ArmFront = 160f, ElbowFront = 15f,
                ArmBack = -160f, ElbowBack = -15f,
                LegFront = 10f + Mathf.Sin(t * 2.1f) * 9f, KneeFront = -10f,
                LegBack = -4f + Mathf.Sin(t * 2.1f + 1.3f) * 9f, KneeBack = -14f,
            };
        }

        /// <summary>Радуется после точной посадки.</summary>
        public static StickmanPose Cheer(float t)
        {
            float wave = Mathf.Sin(t * 9f) * 12f;
            return new StickmanPose
            {
                Torso = 180f,
                Head = 10f,
                ArmFront = 140f + wave, ElbowFront = 20f,
                ArmBack = -140f - wave, ElbowBack = -20f,
                LegFront = 14f, LegBack = -14f,
                Bob = Mathf.Abs(Mathf.Sin(t * 6f)) * 0.12f,
            };
        }

        /// <summary>Пожимает плечами (сел мимо или вышло время).</summary>
        public static StickmanPose Shrug(float t)
        {
            return new StickmanPose
            {
                Torso = 180f,
                Head = -12f + Mathf.Sin(t * 2f) * 4f,
                ArmFront = 55f, ElbowFront = 95f,
                ArmBack = -55f, ElbowBack = -95f,
                LegFront = 6f, LegBack = -6f,
            };
        }
    }
}
