using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Android player settings for Abduction. The same values are already committed in ProjectSettings/ProjectSettings.asset;
/// this re-applies them before any build (including Unity Build Automation, via the pre-export method
/// "BuildAndroid.PreExport") so a cloud build never depends on manual editor setup.
/// Local batch build:  Unity -batchmode -quit -projectPath . -executeMethod BuildAndroid.Build -logFile -
/// Optional signing via env: HB_KS_PATH, HB_KS_PASS, HB_KEY_ALIAS, HB_KEY_PASS. Without them the APK is debug-signed.
/// </summary>
public static class BuildAndroid
{
    public const string Version = "1.0.0";
    public const int VersionCode = 1;
    public const string PackageId = "com.krabbiergames.abduction";
    public const int TargetApi = 36;
    const string ScenePath = "Assets/Scenes/Main.unity";

    static string Env(string k) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? null : v; }

    [MenuItem("Abduction/Apply Android Settings")]
    public static void ApplySettings()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.companyName = "KrabbierGames";
        PlayerSettings.productName = "Abduction";
        PlayerSettings.SetApplicationIdentifier(android, PackageId);
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.Android.bundleVersionCode = VersionCode;
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.SplashScreen.show = false;
        EditorUserBuildSettings.buildAppBundle = false;
        if (EditorBuildSettings.scenes.Length == 0 && File.Exists(ScenePath))
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        var ks = Env("HB_KS_PATH");
        if (ks != null && File.Exists(ks))
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ks;
            PlayerSettings.Android.keystorePass = Env("HB_KS_PASS");
            PlayerSettings.Android.keyaliasName = Env("HB_KEY_ALIAS") ?? "abduction";
            PlayerSettings.Android.keyaliasPass = Env("HB_KEY_PASS") ?? Env("HB_KS_PASS");
        }
    }

    /// <summary>Unity Build Automation pre-export hook (Advanced settings > Pre-export method name).</summary>
    public static void PreExport() => ApplySettings();

    public static void Build()
    {
        ApplySettings();
        var outPath = Env("HB_APK_OUT") ?? "Build/Abduction-arm64.apk";
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath }, locationPathName = outPath, target = BuildTarget.Android, options = BuildOptions.None
        });
        Debug.Log("Build result: " + report.summary.result);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
