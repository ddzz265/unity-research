using System;

namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// Enumerates the types of authorization that apps can request on visionOS.
    /// </summary>
    [Flags]
    public enum VisionOSAuthorizationType
    {
        // Note: we currently assume these values match with the internal enum AR_Authorization_Type. This type is duplicated for our public API to give us
        // flexibility in case we need our public API to diverge from the platform API. AR_Authorization_Type needs to match exactly with the values returned
        // by the platform API.
        /// <summary>
        /// No authorization required.
        /// </summary>
        None = 0,

        /// <summary>
        /// Authorization type used when requesting hand tracking.
        /// </summary>
        HandTracking = 1 << 0,

        /// <summary>
        /// Authorization type used when requesting:
        /// - Plane detection
        /// - Scene reconstruction
        /// </summary>
        WorldSensing = 1 << 1,

        /// <summary>
        /// Authorization type used when requesting camera access.
        /// </summary>
        CameraAccess = 1 << 3
    }
}
