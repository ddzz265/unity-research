using System;
using System.Linq;
using Unity.XR.CoreUtils.Capabilities;
using Unity.XR.CoreUtils.Capabilities.Editor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.VisionOS;

namespace UnityEditor.XR.VisionOS
{
    static class VisionOSEditorUtils
    {
        const string k_VisionOSLoaderTypeName = "UnityEngine.XR.VisionOS.VisionOSLoader";

        static void GetXRManagerSettings(out BuildTargetGroup buildTargetGroup, out XRManagerSettings manager)
        {
            buildTargetGroup = BuildPipeline.GetBuildTargetGroup(BuildTarget.VisionOS);
            var xrSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(buildTargetGroup);
            if (xrSettings == null)
            {
                manager = null;
                return;
            }

            manager = xrSettings.Manager;
        }

        internal static bool IsLoaderEnabled()
        {
            GetXRManagerSettings(out _, out var manager);
            if (manager == null)
                return false;

            var activeLoaders = manager.activeLoaders;
            return activeLoaders != null && activeLoaders.OfType<VisionOSLoader>().Any();
        }

        internal static void DisableLoader()
        {
            GetXRManagerSettings(out var visionOSBuildTargetGroup, out var manager);
            if (manager == null)
                return;

            if (!XRPackageMetadataStore.RemoveLoader(manager, k_VisionOSLoaderTypeName, visionOSBuildTargetGroup))
                Debug.LogError("Failed to disable Apple visionOS XR loader");
        }

        internal static void EnableLoader()
        {
            GetXRManagerSettings(out var visionOSBuildTargetGroup, out var manager);
            if (manager == null)
                return;

            if (!XRPackageMetadataStore.AssignLoader(manager, k_VisionOSLoaderTypeName, visionOSBuildTargetGroup))
                Debug.LogError("Failed to enable Apple visionOS XR loader");
        }

        internal static void UpdateSelectedCapabilityProfiles(VisionOSSettings.AppMode appMode)
        {
            switch (appMode)
            {
                case VisionOSSettings.AppMode.Windowed:
                    CapabilityProfileSelection.Clear();
                    CapabilityProfileSelection.Save();
                    break;
                case VisionOSSettings.AppMode.Metal:
                    UpdateSelectedCapabilityProfiles("PolySpatialMetalCapabilityProfile");
                    CapabilityProfileSelection.Save();
                    break;
                // TODO: LXR-3771 Capability profile for hybrid?
                case VisionOSSettings.AppMode.Hybrid:
                case VisionOSSettings.AppMode.RealityKit:
                    UpdateSelectedCapabilityProfiles("PolySpatialRealityKitCapabilityProfile");
                    CapabilityProfileSelection.Save();
                    break;
            }
        }

        static void UpdateSelectedCapabilityProfiles(string capabilityType)
        {
            CapabilityProfileSelection.Clear();
            foreach (var profileGuid in AssetDatabase.FindAssets($"t:{capabilityType}"))
            {
                var profilePath = AssetDatabase.GUIDToAssetPath(profileGuid);
                if (string.IsNullOrEmpty(profilePath))
                    continue;

                var profile = AssetDatabase.LoadAssetAtPath<CapabilityProfile>(profilePath);
                if (profile == null)
                    continue;

                CapabilityProfileSelection.Add(profile);
            }
        }
    }
}
