namespace UnityEngine.XR.VisionOS.InputDevices
{
    /// <summary>
    /// Represents the kind of visionOS spatial pointer event.
    /// </summary>
    public enum VisionOSSpatialPointerKind : byte
    {
        /// <summary>
        /// The event type of a poke interaction.
        /// </summary>
        Touch = 0,
        /// <summary>
        /// The event type of a direct pinch gesture.
        /// </summary>
        DirectPinch = 1,
        /// <summary>
        /// The event type of a indirect pinch, triggered by a gaze plus pinch gesture.
        /// </summary>
        IndirectPinch = 2,
        /// <summary>
        /// The event type of a pointer.
        /// </summary>
        Pointer = 3,
    }
}
