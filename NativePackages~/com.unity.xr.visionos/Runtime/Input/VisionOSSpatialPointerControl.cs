using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace UnityEngine.XR.VisionOS.InputDevices
{
    using TouchPhase = UnityEngine.InputSystem.TouchPhase;

    /// <summary>
    /// Represents the input control state of a VisionOSSpatialPointerState.
    /// </summary>
    [InputControlLayout(displayName = "VisionOSSpatialPointerControl", stateType = typeof(VisionOSSpatialPointerState))]
    public class VisionOSSpatialPointerControl : InputControl<VisionOSSpatialPointerState>
    {
        /// <summary>
        /// The integer input control for the pointer's interaction ID.
        /// </summary>
        public IntegerControl interactionId { get; set; }

        /// <summary>
        /// The vector3 input control for the pointer's start ray origin.
        /// </summary>
        public Vector3Control startRayOrigin { get; set; }

        /// <summary>
        /// The vector3 input control for the pointer's start ray direction.
        /// </summary>
        public Vector3Control startRayDirection { get; set; }

        /// <summary>
        /// The quaternion input control for the pointer's start ray rotation.
        /// </summary>
        public QuaternionControl startRayRotation { get; set; }

        /// <summary>
        /// The quaternion input control for the pointer's interaction ray rotation.
        /// </summary>
        public QuaternionControl interactionRayRotation { get; set; }

        /// <summary>
        /// The vector3 input control for the pointer's input device position.
        /// </summary>
        public Vector3Control inputDevicePosition { get; set; }

        /// <summary>
        /// The quaternion input control for the pointer's input device rotation.
        /// </summary>
        public QuaternionControl inputDeviceRotation { get; set; }

        /// <summary>
        /// The integer input control for the VisionOSSpatialPointerKind.
        /// </summary>
        public IntegerControl kind { get; set; }

        /// <summary>
        /// The touch phase input control for the VisionOSSpatialPointerPhase.
        /// </summary>
        public TouchPhaseControl phase { get; set; }

        /// <summary>
        /// The integer input control for the pointer's tracking state.
        /// </summary>
        public IntegerControl trackingState { get; set; }

        /// <summary>
        /// The button input control for the pointer's tracked state.
        /// </summary>
        public ButtonControl isTracked { get; set; }

        /// <summary>
        /// Returns true if the touch phase has begun or currently moving, and false otherwise.
        /// </summary>
        public bool isInProgress
        {
            get
            {
                switch (phase.value)
                {
                    case TouchPhase.Began:
                    case TouchPhase.Moved:
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Creates a new VisionOSSpatialPointerControl.
        /// </summary>
        public VisionOSSpatialPointerControl()
        {
            m_StateBlock.format = VisionOSSpatialPointerState.Format;
        }

        /// <summary>
        /// Initializes the child controls of the VisionOSSpatialPointerControl.
        /// </summary>
        protected override void FinishSetup()
        {
            interactionId = GetChildControl<IntegerControl>("interactionId");
            startRayOrigin = GetChildControl<Vector3Control>("startRayOrigin");
            startRayDirection = GetChildControl<Vector3Control>("startRayDirection");
            startRayRotation = GetChildControl<QuaternionControl>("startRayRotation");
            interactionRayRotation = GetChildControl<QuaternionControl>("interactionRayRotation");
            inputDevicePosition = GetChildControl<Vector3Control>("inputDevicePosition");
            inputDeviceRotation = GetChildControl<QuaternionControl>("inputDeviceRotation");
            kind = GetChildControl<IntegerControl>("kind");
            phase = GetChildControl<TouchPhaseControl>("phase");
            trackingState = GetChildControl<IntegerControl>("trackingState");
            isTracked = GetChildControl<ButtonControl>("isTracked");

            base.FinishSetup();
        }

        /// <summary>
        /// Returns the unprocessed pointer state from the state pointer.
        /// </summary>
        /// <param name="statePtr">The VisionOSSpatialPointerState pointer to read the state from.</param>
        /// <returns>The VisionOSSpatialPointerState value referenced by the pointer.</returns>
        public override unsafe VisionOSSpatialPointerState ReadUnprocessedValueFromState(void* statePtr)
        {
            var valuePtr = (VisionOSSpatialPointerState*)((byte*)statePtr + (int)m_StateBlock.byteOffset);
            return *valuePtr;
        }

        // ReSharper disable once ParameterHidesMember
        /// <summary>
        /// Writes the value into the state pointer.
        /// </summary>
        /// <param name="value">The value to write into the pointer.</param>
        /// <param name="statePtr">The pointer that references the memory to write to.</param>
        public override unsafe void WriteValueIntoState(VisionOSSpatialPointerState value, void* statePtr)
        {
            var valuePtr = (VisionOSSpatialPointerState*)((byte*)statePtr + (int)m_StateBlock.byteOffset);
            UnsafeUtility.MemCpy(valuePtr, UnsafeUtility.AddressOf(ref value), UnsafeUtility.SizeOf<VisionOSSpatialPointerState>());
        }
    }
}
