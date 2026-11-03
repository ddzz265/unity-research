namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// The phase of a VisionOS spatial pointer event.
    /// </summary>
    public enum VisionOSSpatialPointerPhase : byte
    {
        /// <summary>
        /// The default none state.
        /// </summary>
        None = 0,

        /// <summary>
        /// The state of the spatial pointer beginning.
        /// </summary>
        Began = 1,

        /// <summary>
        /// The state of the spatial pointer moving.
        /// </summary>
        Moved = 2,

        /// <summary>
        /// The state of the spatial pointer ending.
        /// </summary>
        Ended = 3,

        /// <summary>
        /// The state of the spatial pointer being cancelled.
        /// </summary>
        Cancelled = 4
    }
}
