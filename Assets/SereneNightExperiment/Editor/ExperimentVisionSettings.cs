using System.IO;
using UnityEditor;
using UnityEngine;
#if UNITY_VISIONOS
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.VisionOS;
#endif
public static class ExperimentVisionSettings
{
    [MenuItem("Serene Night/Configure visionOS")]
    public static void ConfigureVisionOS()
    {
#if UNITY_VISIONOS
        var settings=VisionOSSettings.GetOrCreateSettings();settings.appMode=VisionOSSettings.AppMode.Metal;
        if(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(settings))) {
            Directory.CreateDirectory("Assets/XR");
            AssetDatabase.CreateAsset(settings,"Assets/XR/VisionOSSettings.asset");
        }
        VisionOSSettings.currentSettings=settings;
        PlayerSettings.VisionOS.targetOSVersionString="26.0";
        settings.metalImmersionStyle=VisionOSSettings.ImmersionStyle.Full;
        settings.worldSensingUsageDescription="Tracks spatial controllers to point at and answer the questionnaire.";
        EditorUtility.SetDirty(settings);
        var perBuild=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.VisionOS);
        if(perBuild==null) {
            Directory.CreateDirectory("Assets/XR");
            // Opening the XR settings asset creates the per-target container on supported editors.
            var all=AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            if(all.Length==0) {
                var container=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                Directory.CreateDirectory("Assets/XR");AssetDatabase.CreateAsset(container,"Assets/XR/XRGeneralSettings.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,container,true);
            }
            var general=ScriptableObject.CreateInstance<XRGeneralSettings>();
            var manager=ScriptableObject.CreateInstance<XRManagerSettings>();general.Manager=manager;
            AssetDatabase.CreateAsset(general,"Assets/XR/VisionOSGeneral.asset");AssetDatabase.AddObjectToAsset(manager,general);
            var containerAsset=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/XRGeneralSettings.asset");
            if(!containerAsset) containerAsset=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget")[0]));
            containerAsset.SetSettingsForBuildTarget(BuildTargetGroup.VisionOS,general);perBuild=general;
        }
        if(!perBuild.Manager) {
            perBuild.Manager=ScriptableObject.CreateInstance<XRManagerSettings>();
            AssetDatabase.AddObjectToAsset(perBuild.Manager,perBuild);EditorUtility.SetDirty(perBuild);
        }
        XRPackageMetadataStore.AssignLoader(perBuild.Manager,"UnityEngine.XR.VisionOS.VisionOSLoader",BuildTargetGroup.VisionOS);
        AssetDatabase.SaveAssets();
#else
        Debug.Log("Desktop preview configured. On Mac run prepare-mac.command, switch Build Profiles to visionOS, then Serene Night > Configure visionOS.");
#endif
    }
}
