using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>Все звуки игры. Пустое поле = тишина (игра работает и без звуков).</summary>
    [CreateAssetMenu(menuName = "Stickman Jumper/Sound Bank", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        [Header("Разбег и прыжок")]
        public AudioClip Footsteps;
        public AudioClip Jump;

        [Header("Полёт")]
        [Tooltip("Свист воздуха, громкость растёт со скоростью.")]
        public AudioClip AirLoop;
        [Tooltip("Вой ветра на фоне, громкость по силе ветра уровня.")]
        public AudioClip WindLoop;
        public AudioClip CanopyOpen;
        public AudioClip[] Landing = new AudioClip[0];

        [Header("Падения")]
        [Tooltip("Крики: при срыве с края и при падении без парашюта.")]
        public AudioClip[] Yells = new AudioClip[0];
        public AudioClip[] Bones = new AudioClip[0];
        public AudioClip[] Smash = new AudioClip[0];

        [Header("Итог и интерфейс")]
        public AudioClip Success;
        public AudioClip Click;
        public AudioClip Music;

        [Header("Громкость")]
        [Range(0f, 1f)] public float MusicVolume = 0.25f;
        [Range(0f, 1f)] public float EffectsVolume = 0.9f;
    }
}
