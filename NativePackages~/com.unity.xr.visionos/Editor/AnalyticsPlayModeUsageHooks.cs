#if ENABLE_CLOUD_SERVICES_ANALYTICS
using System.Linq;
using UnityEditor.XR.Management;

#if UNITY_HAS_POLYSPATIAL
using Unity.PolySpatial;
#endif

namespace UnityEditor.XR.VisionOS.Analytics
{
    /// <summary>
    /// Class that contains the XR VisionOS analytics PlayMode usage hooks.
    /// This class listen to PlayMode changes, build the PlayMode payload and send it to the analytics server.
    /// </summary>
    [InitializeOnLoad]
    static class AnalyticsPlayModeUsageHooks
    {
        static AnalyticsPlayModeUsageHooks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange != PlayModeStateChange.EnteredPlayMode)
                return;

            var payload = GetPayload(PlayModeStateChange.EnteredPlayMode);
            VisionOSAnalytics.PlayModeUsageEvent.Send(payload);
        }

        static VisionOSPlayModeUsageEvent.Payload GetPayload(PlayModeStateChange playModeStateChange)
        {
            var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;

            var payload = new VisionOSPlayModeUsageEvent.Payload()
            {
                PlaymodeState = playModeStateChange.ToString(),
                ActiveBuildTarget = activeBuildTarget.ToString(),
                PolySpatialRuntimeState = VisionOSPlayModeUsageEvent.Payload.DeactivatedState,
                XRManagementState = VisionOSPlayModeUsageEvent.Payload.NotInstalledState,
                ActiveXRLoaders = new string[0],
                AppMode = VisionOSPlayModeUsageEvent.Payload.UndefinedMode,
                VRImmersionStyle = VisionOSPlayModeUsageEvent.Payload.UndefinedMode,
                MRImmersionStyle = VisionOSPlayModeUsageEvent.Payload.UndefinedMode,
                package = VisionOSAnalytics.PackageName,
                package_ver = VisionOSAnalytics.PackageVersion
            };

#if UNITY_HAS_POLYSPATIAL
            if (PolySpatialRuntime.Enabled)
                payload.PolySpatialRuntimeState = VisionOSPlayModeUsageEvent.Payload.ActivatedState;
#endif

            var group = BuildPipeline.GetBuildTargetGroup(activeBuildTarget);
            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
            var isXRManagementActive = generalSettings != null && generalSettings.InitManagerOnStart;
            if (isXRManagementActive)
            {
                payload.XRManagementState = VisionOSPlayModeUsageEvent.Payload.ActivatedState;
                if (generalSettings.Manager != null)
                {
                    payload.ActiveXRLoaders = generalSettings.Manager.activeLoaders
                        .Where(l => l != null)
                        .Select(l => l.GetType().Name)
                        .ToArray();
                }
            }
            else
            {
                payload.XRManagementState = VisionOSPlayModeUsageEvent.Payload.DeactivatedState;
            }

            if (VisionOSSettings.currentSettings != null)
            {
                payload.AppMode = VisionOSSettings.currentSettings.appMode.ToString();
                payload.VRImmersionStyle = VisionOSSettings.currentSettings.metalImmersionStyle.ToString();
                payload.MRImmersionStyle = VisionOSSettings.currentSettings.realityKitImmersionStyle.ToString();
            }

            return payload;
        }
    }
}
#endif // ENABLE_CLOUD_SERVICES_ANALYTICS
