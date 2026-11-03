namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// Enumerates the different possible statuses of authorization requests.
    /// </summary>
    public enum VisionOSAuthorizationStatus
    {
        // Note: we currently assume these values match with the internal enum AR_Authorization_Status. This type is duplicated for our public API to give us
        // flexibility in case we need our public API to diverge from the platform API. AR_Authorization_Status needs to match exactly with the values returned
        // by the platform API.
        /// <summary>
        /// The user has not yet granted permission.
        /// </summary>
        NotDetermined,

        /// <summary>
        /// The user has explicitly granted permission.
        /// </summary>
        Allowed,

        /// <summary>
        /// The user has explicitly denied permission.
        /// </summary>
        Denied
    }
}
