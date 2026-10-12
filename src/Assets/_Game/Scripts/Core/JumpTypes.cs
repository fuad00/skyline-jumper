namespace StickmanJumper.Core
{
    /// <summary>Фаза прыжка. Порядок как в оригинале: стоит → пятится (тап 1) → бежит (тап 2) → в воздухе (тап 3) → парашют (тап 4).</summary>
    public enum JumpPhase
    {
        /// <summary>Стоит у края, ждёт первого тапа.</summary>
        Ready,
        /// <summary>Пятится назад, набирая длину разбега.</summary>
        BackingUp,
        /// <summary>Упёрся в дальний край крыши, ждёт тапа «бежать».</summary>
        AtBackWall,
        /// <summary>Бежит к краю с ускорением.</summary>
        Running,
        /// <summary>В воздухе: свободное падение или под куполом.</summary>
        Airborne,
        /// <summary>Сел на ноги (на цель или мимо).</summary>
        Landed,
        /// <summary>Разбился или сорвался с края — дальше ragdoll.</summary>
        Crashed,
        /// <summary>Вышло время.</summary>
        TimeUp
    }

    /// <summary>Итог прыжка — четыре экрана оригинала.</summary>
    public enum JumpOutcome { None, WellDone, Died, MissedPlatform, TimesUp }

    /// <summary>Что случилось за шаг — для звуков, камеры и картинки.</summary>
    public enum JumpEvent
    {
        StartedBackingUp,
        ReachedBackWall,
        StartedRunning,
        Jumped,
        /// <summary>Проспал край и сорвался.</summary>
        Stumbled,
        CanopyDeployed,
        /// <summary>Тап под куполом: купол оторван, дальше свободное падение.</summary>
        CanopyCut,
        /// <summary>Скользнул по стене, не разбившись.</summary>
        Grazed,
        Landed,
        Crashed,
        TimeUp
    }

    public static class JumpPhaseExtensions
    {
        /// <summary>Прыжок закончен (итог известен).</summary>
        public static bool IsFinished(this JumpPhase phase) =>
            phase == JumpPhase.Landed || phase == JumpPhase.Crashed || phase == JumpPhase.TimeUp;

        /// <summary>Стикмен ещё на крыше.</summary>
        public static bool IsOnRoof(this JumpPhase phase) =>
            phase == JumpPhase.Ready || phase == JumpPhase.BackingUp || phase == JumpPhase.AtBackWall || phase == JumpPhase.Running;
    }
}
