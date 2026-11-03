using System;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.VisionOS;
using UnityObject = UnityEngine.Object;

#if UNITY_HAS_URP
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
#endif

namespace UnityEditor.XR.VisionOS
{
    // Methods and variables used only in tests
    static partial class VisionOSProjectValidation
    {
        const string k_TestUsageDescription = "This is a test usage description.";

        // Used in testing setup and teardown to store project state in SetUp so it can be reset in TearDown
        static bool s_LoaderWasEnabled;
        static VisionOSSettings.AppMode s_PreviousAppMode;
        static ColorSpace s_PreviousColorSpace;
        static string s_PreviousUsageDescription;
        static bool s_SplashScreenWasEnabled;
        static Camera s_TestCamera;
        static bool s_RenderGraphCompatibilityModeWasEnabled;
        static bool s_InitializeHandTrackingWasEnabled;
        static bool s_HdrWasEnabled;
        static int s_PreviousMSAASampleCount;
        static string s_PreviousTargetOSVersion;

#if UNITY_HAS_URP
        static readonly Dictionary<UnityObject, int> k_PreviousCopyDepthModes = new();
        static readonly Dictionary<UnityObject, UpscalingFilterSelection> k_PreviousQualityFilters = new();
#endif

        static object s_PreviousColorGamuts;
        static bool s_AllowHDRDisplaySupportWasEnabled;
        static bool s_ToneMappingWasEnabled;
        static bool s_BloomHighQualityFilteringWasEnabled;
        static TierSettings s_PreviousTierSettings;
        static VisionOSSettings.ImmersionStyle s_PreviousImmersionStyle;
        static bool s_AlphaOutputWasEnabled;
        static bool s_UrpPostProcessAlphaOutputWasEnabled;

        static string GetEditorSettingsIfExists(out VisionOSSettings settings)
        {
            settings = VisionOSSettings.currentSettings;
            if (settings == null)
                return "Validation test failed: VisionOSSettings is null.";

            return null;
        }

        static string GetRuntimeSettingsIfExists(out VisionOSRuntimeSettings settings)
        {
            settings = VisionOSRuntimeSettings.GetOrCreate();
            if (settings == null)
                return "Validation test failed: VisionOSRuntimeSettings is null.";

            return null;
        }

        static string SetVisionOSLoaderEnabledForTests(bool enabled)
        {
            return SetVisionOSLoaderEnabled(enabled) ? null : $"Validation test failed: Failed to {(enabled ? "enable" : "disable")} XR Loader";
        }

        static void DestroyARSessionIfNewSessionExists()
        {
            if (s_ARSession == null)
                return;

            UnityObject.DestroyImmediate(s_ARSession.gameObject);
        }

        static string CheckForLeakedARSession()
        {
            // Check if an AR Session has "leaked" from another test. We can't revive it if we destroy it, so this is just an outright fail
            if (UnityObject.FindAnyObjectByType<ARSession>(FindObjectsInactive.Include) != null)
                return "Validation test failed: an AR Session component already exists.";

            return null;
        }

#if UNITY_HAS_URP
        static string CreateTestCameraWithNoDepthTexture()
        {
            s_TestCamera = new GameObject("ProjectValidationTestCamera").AddComponent<Camera>();
            var cameraData = s_TestCamera.GetUniversalAdditionalCameraData();
            if (cameraData != null)
            {
                cameraData.requiresDepthTexture = false;
            }
            else
            {
                // TODO: Clean up this error
                return "Could not get camera data.";
            }

            return null;
        }

        static string DestroyTestCamera()
        {
            if (s_TestCamera == null)
                return "Camera validation test failed: new camera isn't set after running test.";

            UnityObject.DestroyImmediate(s_TestCamera.gameObject);
            return null;
        }

        static void StoreRenderPipelineUpscalingFilterSettings()
        {
            QualitySettings.GetRenderPipelineAssetsForPlatform(BuildTargetGroup.VisionOS.ToString(), out HashSet<UniversalRenderPipelineAsset> assets);
            foreach(var asset in assets)
            {
                var upscalingFilter = asset.upscalingFilter;
                k_PreviousQualityFilters[asset] = upscalingFilter;
            }
        }

        static void RestoreRenderPipelineUpscalingFilterSettings()
        {
            QualitySettings.GetRenderPipelineAssetsForPlatform(BuildTargetGroup.VisionOS.ToString(), out HashSet<UniversalRenderPipelineAsset> assets);
            foreach(var asset in assets)
            {
                asset.upscalingFilter = k_PreviousQualityFilters[asset];
            }
        }

        static void StoreCopyDepthModeSettings()
        {
            var asset = UniversalRenderPipeline.asset;
            if (asset == null)
                return;

            ForEachRendererData(asset, rendererData =>
            {
                var copyDepthModeProperty = GetCopyDepthModeProperty(rendererData);
                if (copyDepthModeProperty == null)
                    return;

                k_PreviousCopyDepthModes[rendererData] = copyDepthModeProperty.intValue;
            });
        }

        static void RestoreCopyDepthModes()
        {
            var asset = UniversalRenderPipeline.asset;
            if (asset == null)
                return;

            ForEachRendererData(asset, rendererData =>
            {
                var copyDepthModeProperty = GetCopyDepthModeProperty(rendererData);
                if (copyDepthModeProperty == null)
                    return;

                copyDepthModeProperty.intValue = k_PreviousCopyDepthModes[rendererData];
                copyDepthModeProperty.serializedObject.ApplyModifiedProperties();
            });
        }
#endif
    }
}
