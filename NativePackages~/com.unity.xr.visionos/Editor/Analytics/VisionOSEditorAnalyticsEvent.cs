// Add DEBUG_VISIONOS_ANALYTICS to the scripting defines to debug analytics events, you can also use the Analytics Debugger window
#if ENABLE_CLOUD_SERVICES_ANALYTICS
using Unity.XR.CoreUtils.Editor.Analytics;
using UnityEngine.Analytics;

#if DEBUG_VISIONOS_ANALYTICS
using UnityEngine;
#endif

namespace UnityEditor.XR.VisionOS.Analytics
{
    abstract class VisionOSEditorAnalyticsEvent<T> : EditorAnalyticsEvent<T> where T : struct, IAnalytic.IData
    {
        protected const int k_MaxEventPerHour = 1000;
        protected const int k_MaxItems = 1000;

        protected override AnalyticsResult SendToAnalyticsServer(T parameter)
        {
            var result = EditorAnalytics.SendAnalytic(this);

#if DEBUG_VISIONOS_ANALYTICS
            Debug.Log($"[{GetType().Name}] parameter {JsonUtility.ToJson(parameter)} sent with status {result}.");
#endif
            return result;
        }

        protected override AnalyticsResult RegisterWithAnalyticsServer() => AnalyticsResult.Ok;
    }
}
#endif //ENABLE_CLOUD_SERVICES_ANALYTICS
