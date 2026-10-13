using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StickmanJumper.EditorTools
{
    /// <summary>
    /// Сборки (папка Builds/ в .gitignore).
    /// Тестовые, Development — для себя: Android на свой телефон (Builds/StickmanJumper-INTERNAL-TEST-…apk) и iOS в симулятор
    /// на этом Mac (Xcode-проект Builds/iOS-Simulator-INTERNAL-TEST, дальше xcodebuild → simctl).
    ///   Unity -batchmode -quit -projectPath src -buildTarget Android -executeMethod StickmanJumper.EditorTools.BuildTools.BuildAndroidTest
    ///   Unity -batchmode -quit -projectPath src -buildTarget iOS -executeMethod StickmanJumper.EditorTools.BuildTools.BuildIosSimulatorTest
    /// Релизные — для GitHub Releases: APK и Xcode-проект под настоящий iPhone (из него неподписанный .ipa).
    /// Всё сразу: scripts/build-release.sh → Builds/Release/.
    /// </summary>
    public static class BuildTools
    {
        public const string BundleId = "com.stickmanjumper.game";
        public const string IosSimulatorFolder = "iOS-Simulator-INTERNAL-TEST";
        public const string IosDeviceFolder = "iOS-Device-Release";
        /// <summary>Версия игры (в сборках и в имени файлов релиза).</summary>
        public const string Version = "0.1.0";
        public const string ReleaseName = "SkylineJumper";

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

            PrepareAndroid();
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

            PrepareIos(iOSSdkVersion.SimulatorSDK);
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            // Test links stickmanjumper://level/<id> open a level at once (GameRoot.OpenDevLink, development builds only).
            PlayerSettings.iOS.iOSUrlSchemes = new[] { "stickmanjumper" };

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

        /// <summary>
        /// Релизный APK для GitHub: Builds/Release/SkylineJumper-&lt;версия&gt;.apk. Без своего ключа Unity подписывает его
        /// отладочным — ставится на любой телефон «из неизвестных источников»; для Google Play нужен будет свой ключ.
        /// Картинки оригинала в нём — с разрешения Djinnworks (см. <see cref="PlaceholderGuard.PermittedRelease"/>).
        /// </summary>
        [MenuItem("Stickman Jumper/Build Release APK")]
        public static void BuildAndroidRelease()
        {
            string dir = Path.Combine(BuildsDir(), "Release");
            Directory.CreateDirectory(dir);
            PrepareAndroid();
            Build(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = Path.Combine(dir, $"{ReleaseName}-{Version}.apk"),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            }, permitted: true);
        }

        /// <summary>
        /// Релиз под настоящий iPhone: Xcode-проект Builds/iOS-Device-Release. Подписи нет — scripts/build-release.sh
        /// собирает его xcodebuild'ом без подписи и упаковывает в .ipa (ставится через Sideloadly / AltStore своим Apple ID).
        /// </summary>
        [MenuItem("Stickman Jumper/Build Release iOS (Xcode project)")]
        public static void BuildIosRelease()
        {
            PrepareIos(iOSSdkVersion.DeviceSDK);
            PlayerSettings.iOS.iOSUrlSchemes = new string[0]; // the test links are for internal builds only
            Build(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = Path.Combine(BuildsDir(), IosDeviceFolder),
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            }, permitted: true);
        }

        static void PrepareAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false; // plain .apk to sideload, not .aab
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.bundleVersion = Version;
        }

        static void PrepareIos(iOSSdkVersion sdk)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.iOS.sdkVersion = sdk;
            PlayerSettings.bundleVersion = Version;
        }

        static void Build(BuildPlayerOptions options, bool permitted)
        {
            PlaceholderGuard.PermittedRelease = permitted;
            try
            {
                Build(options);
            }
            finally
            {
                PlaceholderGuard.PermittedRelease = false;
            }
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
