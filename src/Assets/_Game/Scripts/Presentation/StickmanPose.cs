using StickmanJumper.Config;
using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>
    /// Поза стикмена в системе тела «лицом вправо» (влево — зеркалим целиком). Углы в градусах: 0 — кость смотрит вниз,
    /// 90 — вперёд, 180 — вверх. Бедро/плечо и ступни — абсолютные углы, колени/локти — сгиб относительно бедра/плеча.
    /// </summary>
    public struct StickmanPose
    {
        public float Body;        // наклон всего тела вокруг таза
        public float PelvisX, PelvisY; // таз относительно точки между ступнями, м
        public float Torso;       // 180 = прямо вверх, меньше — наклон вперёд
        public float Head;        // наклон головы относительно тела
        public float ArmFront, ElbowFront, ArmBack, ElbowBack;
        public float LegFront, KneeFront, LegBack, KneeBack;
        public float FootFront, FootBack; // 90 = ступня горизонтально, носок вперёд
        public float HeadLook;    // поворот только картинки головы (куда смотрит глаз), не её положения
        public float FrontView;   // 1 — развёрнут к камере (голова анфас), 0 — в профиль

        public static StickmanPose Lerp(StickmanPose a, StickmanPose b, float t)
        {
            return new StickmanPose
            {
                Body = Mathf.LerpAngle(a.Body, b.Body, t),
                PelvisX = Mathf.Lerp(a.PelvisX, b.PelvisX, t),
                PelvisY = Mathf.Lerp(a.PelvisY, b.PelvisY, t),
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
                FootFront = Mathf.LerpAngle(a.FootFront, b.FootFront, t),
                FootBack = Mathf.LerpAngle(a.FootBack, b.FootBack, t),
                HeadLook = Mathf.LerpAngle(a.HeadLook, b.HeadLook, t),
                FrontView = Mathf.Lerp(a.FrontView, b.FrontView, t),
            };
        }

        // ------------------------------------------------------------------ helpers

        public static Vector2 Dir(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), -Mathf.Cos(r));
        }

        static float AngleOf(Vector2 v) => Mathf.Atan2(v.x, -v.y) * Mathf.Rad2Deg;

        /// <summary>Двухзвенная нога: бедро и сгиб колена, чтобы ступня попала в точку. Колено сгибается вперёд.</summary>
        public static void LegIK(Vector2 hip, Vector2 foot, float upper, float lower, out float thigh, out float knee)
        {
            Vector2 d = foot - hip;
            float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(upper - lower) + 0.01f, upper + lower - 0.0005f);
            float baseA = AngleOf(d);
            float cos = (upper * upper + dist * dist - lower * lower) / (2f * upper * dist);
            float alpha = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;
            thigh = baseA + alpha;
            Vector2 kneePos = hip + Dir(thigh) * upper;
            Vector2 ankle = hip + d.normalized * dist;
            knee = Mathf.DeltaAngle(thigh, AngleOf(ankle - kneePos));
        }

        static StickmanPose Feet(StickmanPose p, StickmanConfig c, Vector2 front, Vector2 back)
        {
            var hip = new Vector2(p.PelvisX, p.PelvisY);
            LegIK(hip, front + Vector2.up * c.AnkleHeight, c.UpperLegLength, c.LowerLegLength, out p.LegFront, out p.KneeFront);
            LegIK(hip, back + Vector2.up * c.AnkleHeight, c.UpperLegLength, c.LowerLegLength, out p.LegBack, out p.KneeBack);
            return p;
        }

        // ------------------------------------------------------------------ poses

        /// <summary>Стоит: лёгкое дыхание, ступни на ширине плеч.</summary>
        public static StickmanPose Stand(float t, StickmanConfig c)
        {
            float breath = Mathf.Sin(t * 2.2f);
            var p = new StickmanPose
            {
                PelvisY = c.UpperLegLength + c.LowerLegLength + c.AnkleHeight - 0.03f + breath * 0.008f,
                Torso = 180f - 2f,
                Head = 4f + breath * 2f,
                ArmFront = 10f + breath * 2f, ElbowFront = 14f,
                ArmBack = -8f - breath * 2f, ElbowBack = 12f,
                FootFront = 90f, FootBack = 90f,
            };
            return Feet(p, c, new Vector2(0.12f, 0f), new Vector2(-0.1f, 0f));
        }

        /// <summary>Длина двойного шага для скорости (её же используем, чтобы ступни не скользили).</summary>
        public static float Stride(float speed, StickmanConfig c)
        {
            float run = Run01(speed);
            return Mathf.Lerp(c.WalkStride, Mathf.Clamp(c.RunStrideBase + c.RunStridePerSpeed * speed, c.RunStrideBase, c.RunStrideMax), run);
        }

        static float Run01(float speed) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.2f, 4.8f, speed));

        /// <summary>
        /// Ходьба/бег. phase 0..1 — доля двойного шага (растёт на скорость/шаг). Опорная ступня неподвижна относительно
        /// крыши, маховая идёт по дуге, таз качается, руки машут навстречу ногам.
        /// </summary>
        public static StickmanPose Locomotion(float phase, float speed, StickmanConfig c)
        {
            float run = Run01(speed);
            Gait(speed, c, out float stride, out float duty, out float lift);
            float stance = duty * stride;
            float legLen = c.UpperLegLength + c.LowerLegLength + c.AnkleHeight;

            Vector2 front = FootPath(phase, duty, stance, lift, out float frontPitch);
            Vector2 back = FootPath(phase + 0.5f, duty, stance, lift, out float backPitch);

            // Pelvis bob: walking vaults over the stance leg (high at mid-stance), running compresses into it.
            float bob = Mathf.Cos((phase - duty * 0.5f) * Mathf.PI * 4f);
            float pelvisY = legLen - Mathf.Lerp(0.035f, 0.075f, run) + bob * Mathf.Lerp(0.022f, -0.04f, run);

            float swingAmp = Mathf.Lerp(24f, 70f, run);
            float frontNorm = front.x / Mathf.Max(0.01f, stance * 0.5f);
            float backNorm = back.x / Mathf.Max(0.01f, stance * 0.5f);
            var p = new StickmanPose
            {
                PelvisX = Mathf.Lerp(0f, 0.08f, run),
                PelvisY = pelvisY,
                Torso = 180f - Mathf.Lerp(3f, 20f, run) - bob * Mathf.Lerp(0.5f, 2.5f, run),
                Head = Mathf.Lerp(2f, 14f, run),
                // Opposite arm to leg: front leg forward → front arm back.
                ArmFront = -Mathf.Clamp(frontNorm, -1.3f, 1.3f) * swingAmp + Mathf.Lerp(4f, 12f, run),
                ArmBack = -Mathf.Clamp(backNorm, -1.3f, 1.3f) * swingAmp + Mathf.Lerp(4f, 12f, run),
                ElbowFront = Mathf.Lerp(16f, 86f, run) - Mathf.Clamp(frontNorm, -1f, 1f) * Mathf.Lerp(4f, 14f, run),
                ElbowBack = Mathf.Lerp(16f, 86f, run) - Mathf.Clamp(backNorm, -1f, 1f) * Mathf.Lerp(4f, 14f, run),
                FootFront = frontPitch,
                FootBack = backPitch,
            };
            return Feet(p, c, front, back);
        }

        static void Gait(float speed, StickmanConfig c, out float stride, out float duty, out float lift)
        {
            float run = Run01(speed);
            stride = Stride(speed, c);
            duty = Mathf.Lerp(0.62f, 0.36f, run);
            lift = Mathf.Lerp(0.09f, 0.34f, run) * Mathf.Clamp01(speed / 1.5f);
        }

        /// <summary>Где передняя ступня относительно таза на этой фазе шага (для тестов «ступня не скользит»).</summary>
        public static Vector2 FootPosition(float phase, float speed, StickmanConfig c)
        {
            Gait(speed, c, out float stride, out float duty, out float lift);
            return FootPath(phase, duty, duty * stride, lift, out _);
        }

        /// <summary>Доля шага, когда ступня стоит на крыше.</summary>
        public static float Duty(float speed) => Mathf.Lerp(0.62f, 0.36f, Run01(speed));

        /// <summary>Траектория одной ступни за двойной шаг (x относительно таза, y над крышей) и наклон ступни.</summary>
        static Vector2 FootPath(float phase, float duty, float stance, float lift, out float pitch)
        {
            float u = Mathf.Repeat(phase, 1f);
            if (u < duty)
            {
                float k = u / duty;
                pitch = 90f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, k)) * 38f; // heel lifts before toe-off
                return new Vector2(stance * (0.5f - k), 0f);
            }
            float t = (u - duty) / (1f - duty);
            float e = t * t * (3f - 2f * t);
            float y = lift * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.8f));
            pitch = Mathf.Lerp(52f, 104f, e); // toe down after push-off → toe up before heel strike
            return new Vector2(Mathf.Lerp(-stance * 0.5f, stance * 0.5f, e), y);
        }

        /// <summary>Толчок с края: маховая нога вверх, толчковая вытянута назад, руки вперёд-вверх.</summary>
        public static StickmanPose Takeoff(StickmanConfig c)
        {
            return new StickmanPose
            {
                Body = -8f,
                PelvisY = c.UpperLegLength + c.LowerLegLength,
                Torso = 180f - 14f,
                Head = 14f,
                ArmFront = 150f, ElbowFront = 25f,
                ArmBack = 115f, ElbowBack = 40f,
                LegFront = 78f, KneeFront = -95f,
                LegBack = -28f, KneeBack = -18f,
                FootFront = 70f, FootBack = 40f,
            };
        }

        /// <summary>Направление полёта в системе углов позы.</summary>
        public static float TravelAngle(Vector2 velocity) => velocity.sqrMagnitude < 0.01f ? 180f : AngleOf(velocity);

        /// <summary>
        /// Свободное падение как в оригинале: «супермен». Руки вытянуты вперёд за голову, тело и ноги прямые,
        /// вся фигура развёрнута по направлению полёта (сначала почти горизонтально, потом головой вниз).
        /// </summary>
        public static StickmanPose Dive(Vector2 velocity, float t, StickmanConfig c)
        {
            float wobble = Mathf.Sin(t * 5.3f) * 2.5f;
            float flutter = Mathf.Sin(t * 17f) * 3f;
            return new StickmanPose
            {
                Body = TravelAngle(velocity) - 180f + wobble,
                PelvisY = c.UpperLegLength + c.LowerLegLength + c.AnkleHeight,
                Torso = 180f,
                Head = 0f,
                HeadLook = 80f, // the eye looks ahead along the flight
                // Arms reach forward under the big head (not through it), so the hands lead the flight.
                ArmFront = 138f, ElbowFront = 10f + flutter,
                ArmBack = 146f, ElbowBack = 7f - flutter,
                LegFront = 4f + flutter * 0.6f, KneeFront = -8f,
                LegBack = -3f - flutter * 0.6f, KneeBack = -12f,
                FootFront = 22f, FootBack = 16f, // toes pointed, trailing
            };
        }

        /// <summary>Висит под куполом лицом к камере, руки вверх к держателю строп, ноги вместе и болтаются.</summary>
        public static StickmanPose Hang(float t, float sway, StickmanConfig c)
        {
            float dangle = Mathf.Sin(t * 2.1f) * 5f;
            return new StickmanPose
            {
                Body = sway,
                PelvisY = c.UpperLegLength + c.LowerLegLength + c.AnkleHeight,
                Torso = 180f,
                FrontView = 1f,
                ArmFront = 152f, ElbowFront = 16f,
                ArmBack = 208f, ElbowBack = -16f,
                LegFront = 5f + dangle, KneeFront = -2f,
                LegBack = -5f + dangle, KneeBack = 2f,
                FootFront = 40f, FootBack = -40f,
            };
        }

        /// <summary>Присед при касании (k 0..1 — глубина), руки вперёд для равновесия.</summary>
        public static StickmanPose Crouch(float k, StickmanConfig c)
        {
            float legLen = c.UpperLegLength + c.LowerLegLength + c.AnkleHeight;
            var p = new StickmanPose
            {
                PelvisX = -0.06f * k,
                PelvisY = legLen - 0.03f - 0.32f * k,
                Torso = 180f - 28f * k,
                Head = 10f * k,
                ArmFront = 30f + 50f * k, ElbowFront = 30f,
                ArmBack = 20f + 40f * k, ElbowBack = 30f,
                FootFront = 90f, FootBack = 90f,
            };
            return Feet(p, c, new Vector2(0.2f, 0f), new Vector2(-0.16f, 0f));
        }

        /// <summary>Радуется: прыжки на месте с руками вверх.</summary>
        public static StickmanPose Cheer(float t, StickmanConfig c)
        {
            float hop = Mathf.Abs(Mathf.Sin(t * 5.5f));
            float wave = Mathf.Sin(t * 11f) * 14f;
            var p = new StickmanPose
            {
                PelvisY = c.UpperLegLength + c.LowerLegLength + c.AnkleHeight - 0.05f + hop * 0.16f,
                Torso = 180f + 3f,
                Head = -8f,
                ArmFront = 150f + wave, ElbowFront = 18f,
                ArmBack = 210f - wave, ElbowBack = -18f,
                FootFront = 80f + hop * 20f, FootBack = 80f + hop * 20f,
            };
            float y = hop * 0.16f;
            return Feet(p, c, new Vector2(0.16f, y), new Vector2(-0.14f, y));
        }

        /// <summary>Пожимает плечами (сел мимо или вышло время).</summary>
        public static StickmanPose Shrug(float t, StickmanConfig c)
        {
            var p = Stand(t, c);
            p.Head = -14f + Mathf.Sin(t * 2f) * 4f;
            p.ArmFront = 40f; p.ElbowFront = 100f;
            p.ArmBack = -40f; p.ElbowBack = -100f;
            return p;
        }
    }
}
