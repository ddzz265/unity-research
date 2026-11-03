namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// Arguments provided by the AuthorizationChanged event
    /// </summary>
    public struct VisionOSAuthorizationEventArgs
    {
        /// <summary>
        /// The type of authorization that was requested and/or has changed.
        /// </summary>
        public VisionOSAuthorizationType type { get; set; }

        /// <summary>
        /// The new status for this authorization type.
        /// </summary>
        public VisionOSAuthorizationStatus status { get; set; }
    }
}
