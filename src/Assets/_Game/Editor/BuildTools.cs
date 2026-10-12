using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StickmanJumper.EditorTools
{
    /// <summary>
    /// Тестовые сборки для себя (папка Builds/ в .gitignore). С заглушками оригинала — поэтому только для себя, никому не отправлять.
    /// Android, своему телефону: файл Builds/StickmanJumper-INTERNAL-TEST-…apk.
    ///   Unity -batchmode -quit -projectPath src -buildTarget Android -executeMethod StickmanJumper.EditorTools.BuildTools.BuildAndroidTest
    /// iOS, симулятору на этом Mac: Xcode-проект Builds/iOS-Simulator-INTERNAL-TEST (дальше xcodebuild → simctl).
    ///   Unity -batchmode -quit -projectPath src -buildTarget iOS -executeMethod StickmanJumper.EditorTools.BuildTools.BuildIosSimulatorTest
    /// </summary>
    public static class BuildTools
    {
        public const string BundleId = "com.stickmanjumper.game";
        public const string IosSimulatorFolder = "iOS-Simulator-INTERNAL-TEST";

        static string BuildsDir()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [MenuItem("Stickman Jumper/Build Android Test APK (internal, with placeholders)")]
        public static void BuildAndroidTest()
        {
            string dir = BuildsDir();
            string path = Path.Combine(dir, $"StickmanJumper-INTERNAL-TEST-{DateTime.Now:yyyyMMdd-HHmm}.apk");

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false; // plain .apk to sideload, not .aab
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.bundleVersion = "0.1.0";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            };
            Build(options);
        }

        /// <summary>
        /// iOS для симулятора на этом Mac (Apple Silicon): Development, SDK симулятора, arm64. Unity делает Xcode-проект;
        /// подписывать не нужно. Дальше: xcodebuild -sdk iphonesimulator → xcrun simctl install/launch.
        /// </summary>
        [MenuItem("Stickman Jumper/Build iOS Simulator Test (internal, with placeholders)")]
        public static void BuildIosSimulatorTest()
        {
            string path = Path.Combine(BuildsDir(), IosSimulatorFolder);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            // Test links stickmanjumper://level/<id> open a level at once (GameRoot.OpenDevLink, development builds only).
            PlayerSettings.iOS.iOSUrlSchemes = new[] { "stickmanjumper" };
            PlayerSettings.bundleVersion = "0.1.0";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.Development,
            };
            Build(options);
        }

        static void Build(BuildPlayerOptions options)
        {
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] {summary.result}: {options.locationPathName} ({summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0} s, {summary.totalErrors} errors)");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
