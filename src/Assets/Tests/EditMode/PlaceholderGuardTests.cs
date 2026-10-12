using NUnit.Framework;
using StickmanJumper.EditorTools;

namespace StickmanJumper.Tests
{
    public class PlaceholderGuardTests
    {
        [Test]
        public void Filter_KeepsOnlyQuarantinePaths()
        {
            var v = PlaceholderGuard.FilterPlaceholders(new[]
            {
                "Assets/_Game/Scenes/Game.unity",
                "Assets/_ThirdPartyPlaceholder/Sprites/head.png",
                "Assets/_ThirdPartyPlaceholder/Audio/yell1.ogg",
                "Assets/_ThirdPartyPlaceholderNot/x.png",
                "Assets/_Game/Art/Generated/Limb.png",
            });
            CollectionAssert.AreEqual(new[] { "Assets/_ThirdPartyPlaceholder/Audio/yell1.ogg", "Assets/_ThirdPartyPlaceholder/Sprites/head.png" }, v);
        }

        [Test]
        public void PlistParser_ReadsFrameRects()
        {
            const string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0""><dict>
  <key>frames</key><dict>
    <key>a.png</key><dict><key>frame</key><string>{{10,20},{30,40}}</string><key>rotated</key><false/></dict>
    <key>b.png</key><dict><key>frame</key><string>{{1,2},{3,4}}</string><key>rotated</key><true/></dict>
  </dict>
  <key>metadata</key><dict><key>format</key><integer>2</integer></dict>
</dict></plist>";
            var frames = PlaceholderImporter.ParseFrames(xml);
            Assert.AreEqual(new UnityEngine.RectInt(10, 20, 30, 40), frames["a.png"]);
            Assert.IsFalse(frames.ContainsKey("b.png"), "rotated frames are skipped");
        }

        /// <summary>
        /// Релизная проверка: КРАСНАЯ, пока сцены ссылаются на чужие заглушки (так и задумано).
        /// Обычный прогон её пропускает (категория ReleaseGuard), перед релизом гоняем отдельно.
        /// </summary>
        [Test, Category("ReleaseGuard")]
        public void BuildScenes_ReferenceNoPlaceholders()
        {
            var v = PlaceholderGuard.FindViolations();
            Assert.IsEmpty(v, $"Сцены сборки ссылаются на {v.Count} чужих файлов:\n" + string.Join("\n", v));
        }
    }
}
