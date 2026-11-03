using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils.Editor;
using UnityEditor.Rendering;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.VisionOS;
using UnityObject = UnityEngine.Object;

#if UNITY_HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace UnityEditor.XR.VisionOS
{
    /// <summary>
    /// Utility class for defining project validation rules and supporting test methods
    /// </summary>
    [InitializeOnLoad]
    static partial class VisionOSProjectValidation
    {
        /// <summary>
        /// Store a reference to the ARSession we create in CreateARSession so tests can clean it up.
        /// </summary>
        static ARSession s_ARSession;

        static VisionOSProjectValidation()
        {
            var rules = new List<BuildValidationRule>();
            foreach (var rule in GetAllRules())
            {
                rules.Add(rule.Rule);
            }

            BuildValidator.AddRules(k_VisionOSBuildTarget, rules.ToArray());
        }

        internal static IEnumerable<RuleTestContainer> GetAllRules()
        {
            foreach (var rule in GetValidationRules())
            {
                yield return rule;
            }

            foreach (var rule in GetHDRValidationRules())
            {
                yield return rule;
            }
        }

        static TierSettings GetTierSettings()
        {
            // Vision Pro is tier 2
            return EditorGraphicsSettings.GetTierSettings(BuildTargetGroup.VisionOS, GraphicsTier.Tier2);
        }

        static void SetTierSettings(TierSettings settings)
        {
            // Vision Pro is tier 2
            EditorGraphicsSettings.SetTierSettings(BuildTargetGroup.VisionOS, GraphicsTier.Tier2, settings);
        }

        static bool CheckAppMode(VisionOSSettings.AppMode mode)
        {
            return GetEditorSettings(out var editorSettings) && editorSettings.appMode == mode;
        }

        static bool AppModeSupportsMetal()
        {
            if (GetEditorSettings(out var editorSettings))
            {
                var appMode = editorSettings.appMode;
                return appMode is VisionOSSettings.AppMode.Metal or VisionOSSettings.AppMode.Hybrid;
            }

            return false;
        }

        static bool GetEditorSettings(out VisionOSSettings editorSettings)
        {
            editorSettings = VisionOSSettings.currentSettings;
            return editorSettings != null;
        }

#if INCLUDE_UNITY_XR_HANDS
        static bool GetRuntimeSettings(out VisionOSRuntimeSettings runtimeSettings)
        {
            runtimeSettings = VisionOSRuntimeSettings.GetOrCreate();
            return runtimeSettings != null;
        }
#endif

        static void CreateARSession()
        {
            var arSession = UnityObject.FindAnyObjectByType<ARSession>(FindObjectsInactive.Include);
            if (arSession != null)
                return;

            var newARSession = new GameObject("AR Session");
            s_ARSession = newARSession.AddComponent<ARSession>();
            Undo.RegisterCreatedObjectUndo(newARSession, "Create AR Session");
        }

#if UNITY_HAS_URP
        static void SetCamerasDepthTextureToEnabled()
        {
            if (UniversalRenderPipeline.asset == null)
                return;

#if UNITY_6000_4_OR_NEWER
            var cameras = UnityObject.FindObjectsByType<Camera>(FindObjectsInactive.Include);
#else
            var cameras = UnityObject.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif
            foreach (var camera in cameras)
            {
                var cameraData = camera.GetUniversalAdditionalCameraData();
                if (cameraData != null && !cameraData.requiresDepthTexture)
                {
                    Undo.RegisterCompleteObjectUndo(cameraData, "Enable Depth Texture");
                    cameraData.requiresDepthTexture = true;
                }
            }
        }

        static bool IsCamerasDepthTextureDisabled()
        {
            // Passes validation if no asset is set.
            if (UniversalRenderPipeline.asset == null)
                return true;

#if UNITY_6000_4_OR_NEWER
            var cameras = UnityObject.FindObjectsByType<Camera>(FindObjectsInactive.Include);
#else
            var cameras = UnityObject.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif
            foreach (var camera in cameras)
            {
                var cameraData = camera.GetUniversalAdditionalCameraData();
                if (cameraData != null && !cameraData.requiresDepthTexture)
                    return false;
            }

            return true;
        }

        static void ForEachRendererData(UniversalRenderPipelineAsset asset, Action<UnityObject> action)
        {
            // NB: Copy Depth Mode is not exposed on UniversalRenderer, so we need to snoop SerializedProperties to check for UniversalRendererData references.
            var serializedObject = new SerializedObject(asset);
            var rendererDataList = serializedObject.FindProperty(k_RendererDataListPropertyName);
            if (rendererDataList == null)
                return;

            var count = rendererDataList.arraySize;
            for (var i = 0; i < count; i++)
            {
                var rendererDataListElement = rendererDataList.GetArrayElementAtIndex(i);
                var rendererData = rendererDataListElement.objectReferenceValue;
                if (rendererData == null)
                    continue;

                action.Invoke(rendererData);
            }
        }

        static SerializedProperty GetCopyDepthModeProperty(UnityObject rendererData)
        {
            var serializedObject = new SerializedObject(rendererData);
            return serializedObject.FindProperty(k_CopyDepthModePropertyName);
        }

        static void SetCopyDepthMode(CopyDepthMode mode)
        {
            var asset = UniversalRenderPipeline.asset;
            if (asset == null)
                return;

            ForEachRendererData(asset, rendererData =>
            {
                var copyDepthModeProperty = GetCopyDepthModeProperty(rendererData);
                if (copyDepthModeProperty == null)
                    return;

                copyDepthModeProperty.intValue = (int)mode;
                copyDepthModeProperty.serializedObject.ApplyModifiedProperties();
            });
        }

        static bool IsDepthTextureModeNotAfterOpaques()
        {
            // Passes validation if no asset is set.
            var asset = UniversalRenderPipeline.asset;
            if (asset == null)
                return true;

            var foundInvalidRenderer = false;
            ForEachRendererData(asset, rendererData =>
            {
                var copyDepthModeProperty = GetCopyDepthModeProperty(rendererData);
                if (copyDepthModeProperty == null)
                    return;

                if (copyDepthModeProperty.intValue != (int)CopyDepthMode.AfterOpaques)
                    foundInvalidRenderer = true; // If any renderer's copy depth mode is not set to AfterOpaques, rendering issues can occur on visionOS hardware.
            });

            return !foundInvalidRenderer;
        }
#endif

        static bool AppSupportsMixedImmersion()
        {
            var settings = VisionOSSettings.currentSettings;
            if (settings == null)
                return false;

            var metalImmersionStyle = settings.metalImmersionStyle;
            return metalImmersionStyle == VisionOSSettings.ImmersionStyle.Automatic || metalImmersionStyle == VisionOSSettings.ImmersionStyle.Mixed;
        }

        static bool HasUrpAsset()
        {
#if UNITY_HAS_URP
            return UniversalRenderPipeline.asset != null;
#else
            return false;
#endif
        }

        static bool IsToneMappingEnabled()
        {
#if UNITY_HAS_URP
            var globalProfile = VolumeManager.instance?.globalDefaultProfile;
            //If there is no volume manager or global profile, we can't perform this check. Consider it passed so that we do not show a false positive.
            if (globalProfile == null)
                return true;

            var components = globalProfile.components;
            if (components == null)
                return true;

            foreach (var component in components)
            {
                if (component is not Tonemapping tonemapping)
                    continue;

                return tonemapping.mode.value != TonemappingMode.None;
            }

            // If no tone mapping component can be found, must be a custom URP or something went wrong, thus we cannot perform this check. Consider
            // it passed so that we do not show a false positive.
#endif

            // If URP is not installed, we should have skipped this check. Consider it passed just in case so that we do not show a false positive.
            return true;
        }

        // enabled is only used when URP package is present
        // ReSharper disable once UnusedParameter.Local
        static void SetToneMappingEnabled(bool enabled)
        {
#if UNITY_HAS_URP
            var globalProfile = VolumeManager.instance?.globalDefaultProfile;
            if (globalProfile == null)
                return;

            var components = globalProfile.components;
            if (components == null)
                return;

            foreach (var component in components)
            {
                if (component is not Tonemapping tonemapping)
                    continue;

                tonemapping.mode.value = enabled ? TonemappingMode.Neutral : TonemappingMode.None;
                return;
            }
#endif
        }

        static bool IsBloomHighQualityFilteringDisabled()
        {
#if UNITY_HAS_URP
            var volumeProfiles = Resources.FindObjectsOfTypeAll<VolumeProfile>();
            foreach (var volumeProfile in volumeProfiles)
            {
                if (volumeProfile == null)
                    continue;

                var components = volumeProfile.components;
                if (components == null)
                    continue;

                foreach (var component in components)
                {
                    if (component is not Bloom bloom)
                        continue;

                    if (bloom.highQualityFiltering.value)
                        return false;
                }
            }

            // If no bloom component can be found, must be a custom URP or something went wrong, thus we cannot perform this check. Consider
            // it passed so that we do not show a false positive.
#endif

            // If URP is not installed, we should have skipped this check. Consider it passed just in case so that we do not show a false positive.
            return true;
        }

        // enabled is only used when URP package is present
        // ReSharper disable once UnusedParameter.Local
        static void SetBloomHighQualityFilteringEnabled(bool enabled)
        {
#if UNITY_HAS_URP
            var volumeProfiles = Resources.FindObjectsOfTypeAll<VolumeProfile>();
            foreach (var volumeProfile in volumeProfiles)
            {
                if (volumeProfile == null)
                    continue;

                var components = volumeProfile.components;
                if (components == null)
                    return;

                foreach (var component in components)
                {
                    if (component is not Bloom bloom)
                        continue;

                    bloom.highQualityFiltering.value = enabled;
                    return;
                }
            }
#endif
        }

        static bool IsRenderPipelineUpscalingFilterAutomatic()
        {
#if UNITY_HAS_URP
            QualitySettings.GetRenderPipelineAssetsForPlatform(BuildTargetGroup.VisionOS.ToString(), out HashSet<UniversalRenderPipelineAsset> assets);
            foreach(var asset in assets)
            {
                var upscalingFiler = asset.upscalingFilter;
                if (upscalingFiler != UpscalingFilterSelection.Auto)
                    return false;
            }
#endif

            // If URP is not installed, we should have skipped this check. Consider it passed just in case so that we do not show a false positive.
            return true;
        }

#if UNITY_HAS_URP
        // enabled is only used when URP package is present
        // ReSharper disable once UnusedParameter.Local
        static void SetRenderPipelineUpscalingFilter(UpscalingFilterSelection upscalingFilter)
        {
            QualitySettings.GetRenderPipelineAssetsForPlatform(BuildTargetGroup.VisionOS.ToString(), out HashSet<UniversalRenderPipelineAsset> assets);
            foreach(var asset in assets)
            {
                asset.upscalingFilter = upscalingFilter;
            }
        }
#endif

        static bool IsUrpPostProcessingEnabled()
        {
#if UNITY_HAS_URP
            QualitySettings.GetRenderPipelineAssetsForPlatform(BuildTargetGroup.VisionOS.ToString(), out HashSet<UniversalRenderPipelineAsset> assets);
            foreach(var asset in assets)
            {
                var rendererData = asset.rendererDataList;
                if (rendererData == null)
                    return false;

                foreach (var rendererDatum in rendererData)
                {
                    if (rendererDatum is not UniversalRendererData universalRendererDatum)
                        continue;

                    return universalRendererDatum.postProcessData != null;
                }
            }
#endif

            // If URP is not installed, we should have already failed the URP asset check, but return false anyway
            return false;
        }

        static bool IsAlphaOutputEnabled()
        {
            return PlayerSettings.preserveFramebufferAlpha;
        }

        static void SetAlphaOutputEnabled(bool enabled)
        {
            PlayerSettings.preserveFramebufferAlpha = enabled;
        }

        static bool IsAlphaProcessingEnabled()
        {
#if UNITY_HAS_URP
            QualitySettings.GetRenderPipelineAssetsForPlatform(nameof(BuildTargetGroup.VisionOS), out HashSet<UniversalRenderPipelineAsset> assets);
            foreach(var asset in assets)
            {
                if (!asset.allowPostProcessAlphaOutput)
                    return false;
            }
#endif

            return true;
        }

        // urpPostProcessEnabled is only used when URP package is present
        // ReSharper disable once UnusedParameter.Local
        static void SetAlphaProcessingEnabled(bool urpPostProcessEnabled)
        {
#if UNITY_HAS_URP
            QualitySettings.GetRenderPipelineAssetsForPlatform(nameof(BuildTargetGroup.VisionOS), out HashSet<UniversalRenderPipelineAsset> assets);
            foreach(var asset in assets)
            {
                var serializedObject = new SerializedObject(asset);
                var allowAlphaOutputProperty = serializedObject.FindProperty("m_AllowPostProcessAlphaOutput");
                if (allowAlphaOutputProperty == null)
                    return;

                allowAlphaOutputProperty.boolValue = urpPostProcessEnabled;
                serializedObject.ApplyModifiedProperties();
            }
#endif
        }

        static bool SetVisionOSLoaderEnabled(bool enabled)
        {
            var visionOSLoaderGUIDs = AssetDatabase.FindAssets($"t:{nameof(VisionOSLoader)}");
            if (visionOSLoaderGUIDs.Length == 0)
                return false;

            var visionOSLoader = AssetDatabase.LoadAssetAtPath<VisionOSLoader>(AssetDatabase.GUIDToAssetPath(visionOSLoaderGUIDs[0]));
            if (visionOSLoader == null)
                return false;

            var visionOSXRSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(
                BuildPipeline.GetBuildTargetGroup(BuildTarget.VisionOS));

            if (visionOSXRSettings == null)
                return false;

            var manager = visionOSXRSettings.Manager;
            if (manager == null)
                return false;

            var result = enabled ? manager.TryAddLoader(visionOSLoader) : manager.TryRemoveLoader(visionOSLoader);
            if (result)
            {
                EditorUtility.SetDirty(manager);
                AssetDatabase.SaveAssetIfDirty(manager);
            }

            return true;
        }
    }
}
