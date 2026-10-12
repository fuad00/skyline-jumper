using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Core;
using UnityEngine;

namespace StickmanJumper.Gameplay
{
    /// <summary>
    /// Подсказки уровня, как в оригинале (TutorialText): каждая ждёт своей фазы стикмена (и, если задано, когда его ступни
    /// (середина тела) окажутся в области), потом задержку — и появляется. С WaitForTouch игра замирает до тапа. Тап убирает подсказку и
    /// делает своё обычное дело (отойти, побежать, прыгнуть, раскрыть купол). На обучающем уровне (Guided) тапы без
    /// подсказки на экране не действуют. Новая попытка — новый объект: подсказки показываются снова.
    /// </summary>
    public sealed class TutorialRunner
    {
        readonly IReadOnlyList<LevelScene.Tutorial> _items;
        readonly bool[] _shown, _triggered;
        readonly float[] _delay;

        public TutorialRunner(IReadOnlyList<LevelScene.Tutorial> items, bool guided)
        {
            _items = items ?? new LevelScene.Tutorial[0];
            Guided = guided;
            _shown = new bool[_items.Count];
            _triggered = new bool[_items.Count];
            _delay = new float[_items.Count];
            for (int i = 0; i < _items.Count; i++) _delay[i] = _items[i].ShowDelay;
        }

        public IReadOnlyList<LevelScene.Tutorial> Items => _items;
        public bool Guided { get; }
        /// <summary>Какая подсказка сейчас на экране (−1 — никакой).</summary>
        public int Active { get; private set; } = -1;
        /// <summary>Игра стоит и ждёт тапа.</summary>
        public bool Holding { get; private set; }
        /// <summary>Подсказка появилась или ушла — для картинки.</summary>
        public event System.Action<int> Shown, Hidden;

        /// <summary>Фаза стикмена в номерах оригинала (Player.state): 0 стоит, 1 пятится, 2 бежит, 3 летит, 4 купол, 8 бег с платформы.</summary>
        public static int OriginalState(JumpSession s) => s.Phase switch
        {
            JumpPhase.Ready => 0,
            JumpPhase.BackingUp => 1,
            JumpPhase.AtBackWall => 1,
            JumpPhase.Running => s.RunwayIndex > 0 ? 8 : 2,
            JumpPhase.Airborne => s.CanopyDeployed ? 4 : 3,
            JumpPhase.Crashed => 5,
            JumpPhase.Landed => 6,
            _ => -1,
        };

        /// <summary>Вызывать каждый кадр, пока игра не стоит (dt — игровое время).</summary>
        public void Update(JumpSession s, float dt)
        {
            if (Holding) return;
            int state = OriginalState(s);
            for (int i = 0; i < _items.Count; i++)
            {
                if (_shown[i]) continue;
                var t = _items[i];
                if (t.RequiredState != state) continue;
                if (t.Trigger.width > 0f && !_triggered[i])
                {
                    // The original checks the character's anchor; ours stands at the feet — take the middle of the body.
                    var body = new Vector2(s.Feet.x, s.Feet.y + s.Flight.BodyHeight * 0.5f);
                    if (!t.Trigger.Contains(body)) continue;
                    _triggered[i] = true;
                }
                _delay[i] -= dt;
                if (_delay[i] >= 0f) continue;
                _shown[i] = true;
                if (Active >= 0) Hide();
                Active = i;
                Holding = t.WaitForTouch;
                Shown?.Invoke(i);
            }
        }

        /// <summary>Тап игрока. true — тап идёт в игру (как обычно); false — на обучающем уровне без подсказки он не действует.</summary>
        public bool OnTap()
        {
            if (Active >= 0)
            {
                Hide();
                return true;
            }
            return !Guided;
        }

        void Hide()
        {
            int i = Active;
            Active = -1;
            Holding = false;
            Hidden?.Invoke(i);
        }
    }
}
