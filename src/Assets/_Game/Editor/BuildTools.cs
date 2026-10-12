using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StickmanJumper.EditorTools
{
    /// <summary>
    /// Тестовая сборка для своего телефона: Android, Development, файл Builds/StickmanJumper-INTERNAL-TEST-…apk
    /// (папка в .gitignore). С заглушками оригинала — поэтому только для себя, никому не отправлять.
    /// Без окна: Unity -batchmode -quit -projectPath src -buildTarget Android -executeMethod StickmanJumper.EditorTools.BuildTools.BuildAndroidTest
    /// </summary>
    public static class BuildTools
    {
        [MenuItem("Stickman Jumper/Build Android Test APK (internal, with placeholders)")]
        public static void BuildAndroidTest()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds"));
            Directory.CreateDirectory(dir);
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
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] {summary.result}: {path} ({summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0} s, {summary.totalErrors} errors)");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
