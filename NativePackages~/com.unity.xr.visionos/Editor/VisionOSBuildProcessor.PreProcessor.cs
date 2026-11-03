using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityEditor.XR.VisionOS
{
    static partial class VisionOSBuildProcessor
    {
        class Preprocessor : IPreprocessBuildWithReport
        {
            const string k_PreCompiledLibraryName = "libUnityVisionOS.a";
            const string k_MetalMainSwiftFile = "UnityMetalMainApp.swift";

            public int callbackOrder => 0;

            void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
            {
                if (report.summary.platform != BuildTarget.VisionOS)
                    return;

                DisableSplashScreenIfEnabled();
                SetRuntimePluginCopyDelegate();
                RestoreARMWorkaround(report.summary.outputPath);

#if UNITY_VISIONOS
                if (!VisionOSEditorUtils.IsLoaderEnabled())
                    return;

                var settings = VisionOSSettings.currentSettings;
                var appMode = VisionOSSettings.AppMode.Metal;
                if (settings != null)
                    appMode = settings.appMode;

                if (appMode == VisionOSSettings.AppMode.Windowed)
                {
                    Debug.LogWarning("The Apple visionOS XR loader is not supported when building a visionOS Windowed application. It will be disabled for this " +
                        "build and re-enabled afterward. You may need to manually re-enable the loader in XR Plugin Management settings if this build fails.");

                    s_LoaderWasEnabled = true;
                    VisionOSEditorUtils.DisableLoader();
                    return;
                }

                if (appMode is VisionOSSettings.AppMode.RealityKit or VisionOSSettings.AppMode.Hybrid)
                {
#if !UNITY_HAS_POLYSPATIAL_VISIONOS
                    throw new BuildFailedException("Mixed Reality app mode requires the PolySpatial visionOS support package");
#else
                    // TODO: Figure out how to report this warning only if Unbounded is set as the default
                    //if (settings.initialVolumeCameraConfiguration?.Mode != VolumeCamera.PolySpatialVolumeCameraMode.Unbounded)
                    Debug.Log("Notice: an Unbounded volume configuration is required for ARKit features when building for Mixed Reality");
#endif
                }
#endif
            }

            static void DisableSplashScreenIfEnabled()
            {
#if UNITY_VISIONOS
                s_SplashScreenWasEnabled = PlayerSettings.SplashScreen.show;
                if (!s_SplashScreenWasEnabled)
                    return;

                Debug.LogWarning("The Unity splash screen is not supported on visionOS. It will be disabled for this build and re-enabled afterward. " +
                    "You may need to manually re-enable the splash screen in Player Settings if this build fails.");

                PlayerSettings.SplashScreen.show = false;
#endif
            }

            static void SetRuntimePluginCopyDelegate()
            {
                var allPlugins = PluginImporter.GetAllImporters();
                foreach (var plugin in allPlugins)
                {
                    if (!plugin.isNativePlugin)
                        continue;

                    // Process pre-compiled library separately. Exactly one version should always be included in the build
                    // regardless of whether the loader is enabled. Otherwise, builds will fail in the linker stage
                    if (plugin.assetPath.Contains(k_PreCompiledLibraryName))
                    {
                        plugin.SetIncludeInBuildDelegate(ShouldIncludePreCompiledLibraryInBuild);
                        continue;
                    }

                    foreach (var pluginName in k_SwiftTrampolineFiles)
                    {
                        if (plugin.assetPath.Contains(pluginName))
                        {
                            plugin.SetIncludeInBuildDelegate(ShouldIncludeSourcePluginsInBuild);
                            break;
                        }
                    }
                }
            }

            static bool ShouldIncludeSourcePluginsInBuild(string path)
            {
                // PoySpatial will replace UnityMetalMainApp.swift
                var settings = VisionOSSettings.currentSettings;
                var appMode = VisionOSSettings.AppMode.Metal;
                if (settings != null)
                    appMode = settings.appMode;

                var mixedRealitySupported = appMode is VisionOSSettings.AppMode.RealityKit or VisionOSSettings.AppMode.Hybrid;
                if (mixedRealitySupported && path.Contains(k_MetalMainSwiftFile))
                    return false;

                return appMode != VisionOSSettings.AppMode.Windowed;
            }

            static bool ShouldIncludePreCompiledLibraryInBuild(string path)
            {
#if UNITY_VISIONOS
                // Exclude libraries that don't match the target SDK
                if (PlayerSettings.VisionOS.sdkVersion == VisionOSSdkVersion.Device)
                {
                    if (path.Contains("Simulator"))
                        return false;
                }
                else
                {
                    if (path.Contains("Device"))
                        return false;
                }

                return true;
#else
                return false;
#endif
            }

            static void RestoreARMWorkaround(string outputPath)
            {
#if UNITY_VISIONOS
                // For append builds, we need to restore the original command line so that the Unity
                // build process doesn't see it as missing and add a duplicate to replace it.
                var xcodeProjectPath = GetXcodeProjectPath(outputPath);
                if (!File.Exists(xcodeProjectPath))
                    return;

                var projContents = File.ReadAllText(xcodeProjectPath);

                projContents = projContents.Replace(k_ARMWorkaroundReplacement, k_ARMWorkaroundOriginal);
                projContents = projContents.Replace(k_ARMWorkaroundReplacementAlt, k_ARMWorkaroundOriginalAlt);

                File.WriteAllText(xcodeProjectPath, projContents);
#endif
            }
        }
    }
}
