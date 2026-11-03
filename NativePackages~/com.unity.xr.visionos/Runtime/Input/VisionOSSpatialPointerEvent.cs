using System.Runtime.InteropServices;

namespace UnityEngine.XR.VisionOS.InputDevices
{
    /// <summary>
    /// Struct to represent data for a visionOS spatial pointer event.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 60)]
    public struct VisionOSSpatialPointerEvent
    {
        /// <summary>
        /// The interaction Id of the spatial pointer event.
        /// </summary>
        [FieldOffset(0)]
        public int interactionId;

        /// <summary>
        /// The ray origin of the spatial pointer event.
        /// </summary>
        [FieldOffset(4)] // 0+4
        public Vector3 rayOrigin;

        /// <summary>
        /// The ray direction of the spatial pointer event.
        /// </summary>
        [FieldOffset(16)] // 4+12
        public Vector3 rayDirection;

        /// <summary>
        /// The input device position of the spatial pointer event.
        /// </summary>
        [FieldOffset(28)] // 16+12
        public Vector3 inputDevicePosition;

        /// <summary>
        /// The input device rotation of the spatial pointer event.
        /// </summary>
        [FieldOffset(40)] // 28+12
        public Quaternion inputDeviceRotation;

        /// <summary>
        /// The modifier key states of the spatial pointer event.
        /// </summary>
        [FieldOffset(56)] // 40+16
        public VisionOSSpatialPointerModifierKeys modifierKeys;

        /// <summary>
        /// The spatial pointer kind of the spatial pointer event.
        /// </summary>
        [FieldOffset(58)] // 56+2
        public VisionOSSpatialPointerKind kind;

        /// <summary>
        /// The pointer phase of the spatial pointer event.
        /// </summary>
        [FieldOffset(59)] // 58+1
        public VisionOSSpatialPointerPhase phase;
    }
}
