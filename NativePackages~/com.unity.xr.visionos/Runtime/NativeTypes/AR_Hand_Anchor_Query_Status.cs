namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// Status enum values for the hand anchor at a specified timestamp from the provider.
    /// </summary>
    enum AR_Hand_Anchor_Query_Status
    {
        /// <summary>
        /// The hand anchor at the specified timestamp was successfully obtained.
        /// </summary>
        Success,

        /// <summary>
        /// The hand anchor at the specified timestamp failed to be obtained.
        /// </summary>
        Failure
    }
}
