#if ENABLE_CLOUD_SERVICES_ANALYTICS
using System;
using UnityEngine;
using UnityEngine.Analytics;

namespace UnityEditor.XR.VisionOS.Analytics
{
    /// <summary>
    /// Editor event used to send editor usage <see cref="VisionOSAnalytics"/> data.
    /// Only accepts <see cref="VisionOSPlayModeUsageEvent.Payload"/> parameters.
    /// </summary>
    [AnalyticInfo(k_EventName, VisionOSAnalytics.VendorKey, k_EventVersion, k_MaxEventPerHour, k_MaxItems)]
    class VisionOSPlayModeUsageEvent : VisionOSEditorAnalyticsEvent<VisionOSPlayModeUsageEvent.Payload>
    {
        const string k_EventName = "xrvisionos_playmode_usage";
        const int k_EventVersion = 2;

        [Serializable]
        internal struct Payload : IAnalytic.IData
        {
            internal const string NotInstalledState = "NotInstalled";
            internal const string ActivatedState = "Activated";
            internal const string DeactivatedState = "Deactivated";
            internal const string UndefinedMode = "Undefined";

            [SerializeField]
            internal string PlaymodeState;

            [SerializeField]
            internal string ActiveBuildTarget;

            [SerializeField]
            internal string PolySpatialRuntimeState;

            [SerializeField]
            internal string XRManagementState;

            [SerializeField]
            internal string[] ActiveXRLoaders;

            [SerializeField]
            internal string AppMode;

            [SerializeField]
            internal string VRImmersionStyle;

            [SerializeField]
            internal string MRImmersionStyle;

            [SerializeField]
            internal string package;

            [SerializeField]
            internal string package_ver;
        }

        internal VisionOSPlayModeUsageEvent()
        {
        }
    }
}
#endif //ENABLE_CLOUD_SERVICES_ANALYTICS
