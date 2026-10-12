using System;
using StickmanJumper.Config;
using StickmanJumper.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.UI
{
    /// <summary>
    /// Связывает Game.uxml. Тап в любом месте экрана (кроме кнопок) — это ход игрока. HUD: время, ветер, зум.
    /// Итог: заголовок, звёзды, метры до стрелки, лучшее, очки, «Ещё раз».
    /// </summary>
    public sealed class GameUI
    {
        readonly VisualElement _root, _hud, _summary, _windIcon;
        readonly Label _time, _wind, _hint;
        readonly Label _title, _distance, _best, _points, _record;
        readonly VisualElement _pointsLine;
        readonly VisualElement[] _stars = new VisualElement[3];
        readonly Button _zoom;
        readonly ArtBank _art;

        public event Action Tapped;
        public event Action RetryClicked;
        public event Action ZoomClicked;

        public bool SummaryVisible { get; private set; }

        public GameUI(VisualElement root, ArtBank art)
        {
            _art = art;
            _root = root.Q<VisualElement>("root");
            _hud = root.Q<VisualElement>("hud");
            _summary = root.Q<VisualElement>("summary");
            _time = root.Q<Label>("hud-time");
            _wind = root.Q<Label>("hud-wind");
            _hint = root.Q<Label>("hint");
            _title = root.Q<Label>("summary-title");
            _distance = root.Q<Label>("summary-distance");
            _best = root.Q<Label>("summary-best");
            _points = root.Q<Label>("summary-points");
            _pointsLine = root.Q<VisualElement>("points-line");
            _record = root.Q<Label>("summary-record");
            _zoom = root.Q<Button>("zoom");
            _windIcon = root.Q<VisualElement>("icon-wind");
            for (int i = 0; i < 3; i++) _stars[i] = root.Q<VisualElement>($"star-{i + 1}");

            root.Q<Label>("distance-label").text = UIText.DistanceLabel;
            root.Q<Label>("best-label").text = UIText.BestLabel;
            root.Q<Label>("points-label").text = UIText.PointsLabel;
            _record.text = UIText.NewBest;
            var retry = root.Q<Button>("retry");
            retry.text = UIText.Retry;
            retry.clicked += () => RetryClicked?.Invoke();
            _zoom.clicked += () => ZoomClicked?.Invoke();

            SetIcon(root.Q<VisualElement>("icon-time"), art.IconTime, "⏱");
            SetIcon(_windIcon, art.IconWind, "»");
            SetIcon(root.Q<VisualElement>("icon-zoom"), art.IconZoom, "+");

            // Any press that is not on a button is the player's tap.
            _root.RegisterCallback<PointerDownEvent>(e =>
            {
                if (IsOnButton(e.target as VisualElement)) return;
                Tapped?.Invoke();
            });

            HideSummary();
        }

        static void SetIcon(VisualElement e, Sprite sprite, string fallback)
        {
            if (e == null) return;
            if (sprite != null)
            {
                e.style.backgroundImage = new StyleBackground(sprite);
                return;
            }
            e.Add(new Label(fallback) { pickingMode = PickingMode.Ignore, style = { fontSize = 44, unityTextAlign = TextAnchor.MiddleCenter, flexGrow = 1 } });
        }

        static bool IsOnButton(VisualElement e)
        {
            for (; e != null; e = e.parent)
                if (e is Button) return true;
            return false;
        }

        public void SetHud(float timeLeft, float wind, bool visible, bool zoomOn)
        {
            _hud.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            _time.text = UIText.Seconds(timeLeft);
            _wind.text = UIText.Wind(wind);
            // Wind icon points right by default; flip it for wind blowing left.
            _windIcon.style.scale = new Scale(new Vector3(wind < 0f ? -1f : 1f, 1f, 1f));
            _zoom.EnableInClassList("btn-round--on", zoomOn);
        }

        public void SetHint(string text)
        {
            _hint.text = text;
            _hint.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void ShowSummary(JumpOutcome outcome, float distance, float best, bool newBest, int points, int stars)
        {
            _summary.style.display = DisplayStyle.Flex;
            SummaryVisible = true;
            _title.text = UIText.Title(outcome);
            _title.EnableInClassList("summary-title--fail", outcome != JumpOutcome.WellDone);
            _distance.text = UIText.Meters(distance);
            _best.text = UIText.Meters(best);
            _points.text = UIText.Points(points);
            _pointsLine.style.display = outcome == JumpOutcome.WellDone ? DisplayStyle.Flex : DisplayStyle.None;
            _record.style.display = newBest ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < 3; i++)
            {
                var sprite = i < stars ? _art.Star : _art.StarEmpty;
                _stars[i].style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground();
                _stars[i].style.unityBackgroundImageTintColor = sprite != null ? Color.white : (i < stars ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 1f, 1f, 0.2f));
            }
            SetHint(string.Empty);
        }

        public void HideSummary()
        {
            _summary.style.display = DisplayStyle.None;
            SummaryVisible = false;
        }
    }
}
