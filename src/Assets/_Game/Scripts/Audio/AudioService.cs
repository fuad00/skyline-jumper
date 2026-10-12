using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Audio
{
    /// <summary>
    /// Звуки: музыка, вой ветра (по силе ветра уровня), свист воздуха (по скорости), шаги (пока бежит),
    /// разовые: прыжок, хлопок купола, посадка, крики, хруст костей.
    /// </summary>
    public sealed class AudioService
    {
        readonly SoundBank _bank;
        readonly AudioSource _music, _wind, _air, _steps, _oneShot;
        readonly System.Random _rng = new System.Random();
        bool _yelled;
        float _lastBones;

        public AudioService(GameObject host, SoundBank bank)
        {
            _bank = bank;
            _music = Loop(host, bank.Music, bank.MusicVolume);
            _wind = Loop(host, bank.WindLoop, 0f);
            _air = Loop(host, bank.AirLoop, 0f);
            _steps = Loop(host, bank.Footsteps, 0f);
            _oneShot = host.AddComponent<AudioSource>();
            _oneShot.playOnAwake = false;
            if (_music.clip != null) _music.Play();
        }

        static AudioSource Loop(GameObject host, AudioClip clip, float volume)
        {
            var s = host.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.volume = volume;
            return s;
        }

        /// <summary>Все звуки вкл/выкл (кнопка «звук» в меню).</summary>
        public void SetSound(bool on) => AudioListener.volume = on ? 1f : 0f;

        /// <summary>Музыка вкл/выкл (кнопка «музыка» в меню).</summary>
        public void SetMusic(bool on)
        {
            _music.mute = !on;
            if (on && _music.clip != null && !_music.isPlaying) _music.Play();
        }

        public void ResetRun()
        {
            _yelled = false;
            _air.volume = 0f;
            _steps.volume = 0f;
        }

        public void Update(JumpSession s, bool ragdoll, float dt)
        {
            float fx = _bank.EffectsVolume;
            Ensure(_wind, Mathf.Clamp01(Mathf.Abs(s.Level.Wind) / 6f) * 0.35f * fx, dt);

            float speed = s.Velocity.magnitude;
            float air = s.Phase == JumpPhase.Airborne ? Mathf.Clamp01(speed / 30f) * (1f - s.Canopy * 0.7f) : 0f;
            Ensure(_air, air * 0.8f * fx, dt);
            _air.pitch = Mathf.Lerp(0.8f, 1.3f, Mathf.Clamp01(speed / 40f));

            bool walking = s.Phase == JumpPhase.BackingUp || s.Phase == JumpPhase.Running;
            Ensure(_steps, walking ? 0.7f * fx : 0f, dt * 4f);
            _steps.pitch = s.Phase == JumpPhase.Running ? Mathf.Lerp(1f, 1.8f, Mathf.Clamp01(s.Velocity.x / 9f)) : 0.8f;

            // Panic yell: falling fast with no canopy.
            if (!_yelled && s.Phase == JumpPhase.Airborne && !s.CanopyDeployed && s.Velocity.y < -22f)
            {
                _yelled = true;
                PlayRandom(_bank.Yells, 0.9f);
            }
        }

        static void Ensure(AudioSource src, float volume, float dt)
        {
            if (src.clip == null) return;
            src.volume = Mathf.MoveTowards(src.volume, volume, dt * 2f);
            if (src.volume > 0.001f && !src.isPlaying) src.Play();
            else if (src.volume <= 0.001f && src.isPlaying) src.Stop();
        }

        public void OnEvent(JumpEvent e, JumpSession s)
        {
            switch (e)
            {
                case JumpEvent.Jumped:
                    Play(_bank.Jump, 0.9f);
                    break;
                case JumpEvent.Stumbled:
                    _yelled = true;
                    PlayRandom(_bank.Yells, 1f);
                    break;
                case JumpEvent.CanopyDeployed:
                    Play(_bank.CanopyOpen, 1f);
                    break;
                case JumpEvent.Landed:
                    PlayRandom(_bank.Landing, 0.9f);
                    if (s.Outcome == JumpOutcome.WellDone) Play(_bank.Success, 0.8f);
                    break;
                case JumpEvent.Crashed:
                    PlayRandom(_bank.Smash, 1f);
                    break;
            }
        }

        /// <summary>Удар части ragdoll — хруст, не чаще раза в 0.12 с.</summary>
        public void OnRagdollImpact(float speed)
        {
            if (speed < 4f || Time.unscaledTime - _lastBones < 0.12f) return;
            _lastBones = Time.unscaledTime;
            PlayRandom(_bank.Bones, Mathf.Clamp01(speed / 18f));
        }

        public void Click() => Play(_bank.Click, 0.7f);

        void Play(AudioClip clip, float volume)
        {
            if (clip != null) _oneShot.PlayOneShot(clip, volume * _bank.EffectsVolume);
        }

        void PlayRandom(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0) return;
            Play(clips[_rng.Next(clips.Length)], volume);
        }
    }
}
