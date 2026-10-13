using System.Text.RegularExpressions;
using NUnit.Framework;
using StickmanJumper.Config;
using StickmanJumper.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.Tests
{
    /// <summary>Смена языка на ходу: подписи переписываются сразу, без перезапуска игры.</summary>
    public class LanguageTests
    {
        static readonly Regex Cyrillic = new Regex("[А-Яа-яЁё]");

        /// <summary>Конструктор карт строится один раз — после переключения на английский в нём не должно остаться русского.</summary>
        [Test]
        public void MapBuilder_SwitchedToEnglish_HasNoRussianLeft()
        {
            var before = UIText.Lang;
            var art = ScriptableObject.CreateInstance<ArtBank>();
            try
            {
                UIText.Lang = Language.Ru;
                var ui = new MapBuilderUI(art);
                ui.SetTools(MapBuilderUI.Selection.Piece, true, false);
                ui.SelectTab(4); // start, target, platform
                Assert.IsTrue(HasRussian(ui), "built in Russian, so some label must be Russian");

                UIText.Lang = Language.En;
                ui.ApplyLanguage();
                foreach (var screen in new[] { ui.ListScreen, ui.EditorScreen })
                    screen.Query<Label>().ForEach(l => Assert.IsFalse(Cyrillic.IsMatch(l.text ?? ""), $"«{l.text}» is still Russian"));
            }
            finally
            {
                UIText.Lang = before;
                Object.DestroyImmediate(art);
            }
        }

        static bool HasRussian(MapBuilderUI ui)
        {
            bool found = false;
            foreach (var screen in new[] { ui.ListScreen, ui.EditorScreen })
                screen.Query<Label>().ForEach(l => found |= Cyrillic.IsMatch(l.text ?? ""));
            return found;
        }
    }
}
