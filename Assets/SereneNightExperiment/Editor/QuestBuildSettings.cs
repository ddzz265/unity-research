using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

// Run after installing Android Build Support. Does not rebuild or replace scenes.
public sealed class QuestBuildSettings : IPreprocessBuildWithReport
{
    const string Folder="Assets/XR";
    public int callbackOrder=>-900;

    [MenuItem("Serene Night/Configure Quest Pro (Android)")]
    public static void Configure()
    {
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))
            throw new InvalidOperationException("Install Android Build Support (SDK, NDK and OpenJDK) for this Unity version, then run Configure Quest Pro again.");
        Directory.CreateDirectory(Folder);
        if(!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,out XRGeneralSettingsPerBuildTarget container)) {
            container=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(Folder+"/XRGeneralSettings.asset");
            if(!container) {
                container=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(container,Folder+"/XRGeneralSettings.asset");
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,container,true);
        }
        if(!container.HasSettingsForBuildTarget(BuildTargetGroup.Android))container.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
        if(!container.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))container.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var general=container.SettingsForBuildTarget(BuildTargetGroup.Android);
        general.InitManagerOnStart=true;
        if(!XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",BuildTargetGroup.Android))
            throw new InvalidOperationException("Could not assign the Android OpenXR loader.");
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if(!settings)throw new InvalidOperationException("Open Project Settings > XR Plug-in Management > OpenXR once, then configure Quest Pro again.");
        settings.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced;
        var quest=settings.GetFeature<MetaQuestFeature>();
        var touch=settings.GetFeature<OculusTouchControllerProfile>();
        var pro=settings.GetFeature<MetaQuestTouchProControllerProfile>();
        if(!quest || !touch || !pro)throw new InvalidOperationException("Required Quest OpenXR features are missing.");
        quest.enabled=touch.enabled=pro.enabled=true;
        // Quest Pro's manifest device identifier is cambria (Unity MetaQuestFeature).
        var serialized=new SerializedObject(quest);
        var devices=serialized.FindProperty("targetDevices");
        bool found=false;
        for(int i=0;i<devices.arraySize;i++) {
            var device=devices.GetArrayElementAtIndex(i);
            if(device.FindPropertyRelative("manifestName").stringValue!="cambria")continue;
            device.FindPropertyRelative("enabled").boolValue=true;found=true;
        }
        if(!found)throw new InvalidOperationException("Quest Pro is missing from the supported device list.");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
        PlayerSettings.colorSpace=ColorSpace.Linear;
        PlayerSettings.Android.optimizedFramePacing=true;
        EditorUtility.SetDirty(container);EditorUtility.SetDirty(general);EditorUtility.SetDirty(general.Manager);
        EditorUtility.SetDirty(settings);EditorUtility.SetDirty(quest);EditorUtility.SetDirty(touch);EditorUtility.SetDirty(pro);
        AssetDatabase.SaveAssets();
        Debug.Log("QUEST_CONFIGURATION_SAVED: OpenXR, Quest Pro, Touch Pro/Touch, ARM64 IL2CPP, Vulkan, single pass. Switch Build Profiles to Android and run Project Validation before building.");
    }

    [MenuItem("Serene Night/Configure Quest Pro Link (Windows)")]
    public static void ConfigureLink()
    {
        if(!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,out XRGeneralSettingsPerBuildTarget container)) {
            container=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(Folder+"/XRGeneralSettings.asset");
            if(!container){container=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();AssetDatabase.CreateAsset(container,Folder+"/XRGeneralSettings.asset");}
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,container,true);
        }
        if(!container.HasSettingsForBuildTarget(BuildTargetGroup.Standalone))container.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Standalone);
        if(!container.HasManagerSettingsForBuildTarget(BuildTargetGroup.Standalone))container.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);
        var general=container.SettingsForBuildTarget(BuildTargetGroup.Standalone);general.InitManagerOnStart=true;
        if(!XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",BuildTargetGroup.Standalone))throw new InvalidOperationException("Could not assign the Windows OpenXR loader.");
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
        var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        settings.GetFeature<OculusTouchControllerProfile>().enabled=true;
        settings.GetFeature<MetaQuestTouchProControllerProfile>().enabled=true;
        EditorUtility.SetDirty(container);EditorUtility.SetDirty(general);EditorUtility.SetDirty(general.Manager);
        EditorUtility.SetDirty(settings);EditorUtility.SetDirty(settings.GetFeature<OculusTouchControllerProfile>());EditorUtility.SetDirty(settings.GetFeature<MetaQuestTouchProControllerProfile>());
        AssetDatabase.SaveAssets();
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if(report.summary.platform!=BuildTarget.Android)return;
        var general=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        bool loader=false;
        if(general && general.Manager)foreach(var entry in general.Manager.activeLoaders)loader|=entry is OpenXRLoader;
        if(!loader || !general.InitManagerOnStart || !settings || !settings.GetFeature<MetaQuestFeature>() ||
            !settings.GetFeature<MetaQuestFeature>().enabled || !settings.GetFeature<MetaQuestTouchProControllerProfile>() ||
            !settings.GetFeature<MetaQuestTouchProControllerProfile>().enabled)
            throw new BuildFailedException("Run Serene Night > Configure Quest Pro (Android) before building the headset APK.");
    }
}
