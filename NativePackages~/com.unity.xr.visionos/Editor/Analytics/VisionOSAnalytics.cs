#if ENABLE_CLOUD_SERVICES_ANALYTICS

namespace UnityEditor.XR.VisionOS.Analytics
{
    /// <summary>
    /// The entry point class to send XR VisionOS analytics data.
    /// </summary>
    static class VisionOSAnalytics
    {
        internal const string VendorKey = "unity.xr.visionos";
        internal const string PackageName = "com.unity.xr.visionos";
        internal static readonly string PackageVersion = PackageManager.PackageInfo.FindForPackageName(PackageName).version;
        internal static VisionOSPlayModeUsageEvent PlayModeUsageEvent { get; } = new();
    }
}
#endif //ENABLE_CLOUD_SERVICES_ANALYTICS
