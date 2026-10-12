using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StickmanJumper.EditorTools
{
    /// <summary>
    /// Охрана: если хоть одна сцена из сборки (прямо или через настройки) ссылается на Assets/_ThirdPartyPlaceholder/,
    /// сборка падает. Тот же список проверяет тест ReleaseGuard. Чужие ассеты не должны уехать ни в стор, ни к издателю.
    /// </summary>
    public static class PlaceholderGuard
    {
        public const string Prefix = PlaceholderImporter.Folder + "/";

        /// <summary>Только пути из карантинной папки (чистая функция — для теста).</summary>
        public static List<string> FilterPlaceholders(IEnumerable<string> assetPaths) =>
            assetPaths.Where(p => p != null && p.Replace('\\', '/').StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(p => p)
                .ToList();

        public static IEnumerable<string> BuildScenePaths() =>
            EditorBuildSettings.scenes.Where(s => s.enabled && !string.IsNullOrEmpty(s.path)).Select(s => s.path);

        /// <summary>Все чужие файлы, которые утащит сборка.</summary>
        public static List<string> FindViolations() =>
            FilterPlaceholders(AssetDatabase.GetDependencies(BuildScenePaths().ToArray(), true));

        [MenuItem("Stickman Jumper/Check Build For Placeholders")]
        public static void Report()
        {
            var v = FindViolations();
            if (v.Count == 0) Debug.Log("[PlaceholderGuard] OK: build scenes reference no third-party placeholders.");
            else Debug.LogWarning($"[PlaceholderGuard] Build is BLOCKED: {v.Count} placeholder files are referenced:\n" + string.Join("\n", v));
        }
    }

    /// <summary>
    /// Валит любую сборку игрока, пока в ней есть заглушки. Единственное исключение (решено 2026-10-12):
    /// Development-сборка для теста на своём телефоне (BuildTools.BuildAndroidTest, файл «…-INTERNAL-TEST…»).
    /// Релизная сборка с заглушками не собирается никогда.
    /// </summary>
    public sealed class PlaceholderBuildBlocker : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var v = PlaceholderGuard.FindViolations();
            if (v.Count == 0) return;
            bool development = (report.summary.options & BuildOptions.Development) != 0;
            bool internalTest = report.summary.outputPath.Contains("INTERNAL-TEST");
            if (development && internalTest)
            {
                Debug.LogWarning($"[PlaceholderGuard] INTERNAL TEST build with {v.Count} third-party placeholders — only for your own device, never share it.");
                return;
            }
            throw new BuildFailedException(
                $"Сборка запрещена: сцены ссылаются на {v.Count} чужих файлов из {PlaceholderImporter.Folder}. " +
                "Замени их своими (список в reference/PLACEHOLDER_ASSETS.md).\n" + string.Join("\n", v.Take(20)));
        }
    }
}
