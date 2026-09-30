#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine.InputSystem;

[InitializeOnLoad]
public class TemplateAssetPostprocessor : AssetPostprocessor
{
    const int PostProcessOrder = 0;
    public override int GetPostprocessOrder() => PostProcessOrder;

    static TemplateAssetPostprocessor()
    {
        AndroidSets();
        EditorApplication.delayCall += EnableSimulateTouchFromMouseOrPen;
        EditorApplication.delayCall += TemplateCloudEnvironment.EnsureProductionIfCloudLinked;
        CloudProjectSettingsEventManager.instance.projectStateChanged += TemplateCloudEnvironment.EnsureProductionIfCloudLinked;
        CloudProjectSettingsEventManager.instance.projectRefreshed += TemplateCloudEnvironment.EnsureProductionIfCloudLinked;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            TemplateCloudEnvironment.EnsureProductionIfCloudLinked();
    }

    public static void AndroidSets()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android,
                BuildTarget.Android
            );
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        }


        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.Android.runWithoutFocus = true;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
        PlayerSettings.stripEngineCode = true;

        UnityEngine.Debug.Log("[TemplateAssetPostprocessor] Android settings applied");
    }

    static void EnableSimulateTouchFromMouseOrPen()
    {
        try
        {
            var settingsType = typeof(InputSystem).Assembly.GetType("UnityEngine.InputSystem.InputEditorUserSettings")
                ?? typeof(InputSystem).Assembly.GetType("UnityEngine.InputSystem.Editor.InputEditorUserSettings");
            var simulateTouch = settingsType?.GetProperty(
                "simulateTouch",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (simulateTouch == null || simulateTouch.PropertyType != typeof(bool))
            {
                UnityEngine.Debug.LogWarning(
                    "[TemplateAssetPostprocessor] Could not find Input Debugger simulateTouch setting.");
                return;
            }

            if (simulateTouch.GetValue(null) is true)
                return;

            simulateTouch.SetValue(null, true);
            UnityEngine.Debug.Log(
                "[TemplateAssetPostprocessor] Simulate Touch Input From Mouse or Pen enabled");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning(
                "[TemplateAssetPostprocessor] Could not enable Simulate Touch Input From Mouse or Pen: " +
                ex.Message);
        }
    }

    private static bool OnPreGeneratingCSProjectFiles()
    {
        AndroidSets();
        EnableSimulateTouchFromMouseOrPen();
        return true;
    }

    void OnPreprocessTexture()
    {
        var importer = assetImporter as TextureImporter;

        if (importer.importSettingsMissing is false)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.CompressedLQ;
        importer.maxTextureSize = 1024;
    }
}
#endif