    using System.Runtime.InteropServices;
    using UnityEngine;
    using UnityEngine.InputSystem.Layouts;
    using UnityEngine.InputSystem.LowLevel;
    using UnityEngine.InputSystem.Utilities;

    namespace UnityEngine.XR.VisionOS.InputDevices
    {
        /// <summary>
        /// The input state of a visionOS spatial pointer.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = SizeInBytes)]
        public struct VisionOSSpatialPointerState : IInputStateTypeInfo
        {
            const int k_SizeOfVector3 = sizeof(float) * 3;
            const int k_SizeOfQuaternion = sizeof(float) * 4;
            const int k_InteractionIDOffset = 0;
            const int k_StartRayOriginOffset = k_InteractionIDOffset + sizeof(int);
            const int k_StartRayDirectionOffset = k_StartRayOriginOffset + k_SizeOfVector3;
            const int k_StartRayRotationOffset = k_StartRayDirectionOffset + k_SizeOfVector3;
            const int k_InteractionRayRotationOffset = k_StartRayRotationOffset + k_SizeOfQuaternion;
            const int k_InputDevicePositionOffset = k_InteractionRayRotationOffset + k_SizeOfQuaternion;
            const int k_InputDeviceRotationOffset = k_InputDevicePositionOffset + k_SizeOfVector3;
            const int k_ModifierKeysOffset = k_InputDeviceRotationOffset + k_SizeOfQuaternion;
            const int k_KindIdOffset = k_ModifierKeysOffset + sizeof(ushort);
            const int k_PhaseIdOffset = k_KindIdOffset + sizeof(byte);
            const int k_IsTrackedOffset = k_PhaseIdOffset + sizeof(byte);
            const int k_TrackingStateOffset = k_IsTrackedOffset + sizeof(bool);

            /// <summary>
            /// The layout name of the spatial pointer state struct.
            /// </summary>
            public const string LayoutName = "VisionOSSpatialPointer";

            /// <summary>
            /// The size in bytes of the spatial pointer state struct.
            /// </summary>
            public const int SizeInBytes = k_TrackingStateOffset + sizeof(int);

            /// <summary>
            /// The four character format of the spatial pointer state.
            /// </summary>
            public static FourCC Format => new('V', 'O', 'P', 'S');

            /// <summary>
            /// The interaction ID of the spatial pointer state.
            /// </summary>
            [InputControl(displayName = "Interaction ID", layout = "Integer", synthetic = true, dontReset = true)]
            [FieldOffset(k_InteractionIDOffset)]
            public int interactionId;

            /// <summary>
            /// The start ray origin of the spatial pointer state.
            /// </summary>
            [InputControl(displayName = "Start Ray Origin", noisy = true, dontReset = true)]
            [FieldOffset(k_StartRayOriginOffset)]
            public Vector3 startRayOrigin;

            /// <summary>
            /// The start ray direction of the spatial pointer state.
            /// </summary>
            [InputControl(displayName = "Start Ray Direction", noisy = true, dontReset = true)]
            [FieldOffset(k_StartRayDirectionOffset)]
            public Vector3 startRayDirection;

            /// <summary>
            /// The start ray rotation of the spatial pointer state.
            /// </summary>
            [InputControl(displayName = "Start Ray Rotation", noisy = true, dontReset = true)]
            [FieldOffset(k_StartRayRotationOffset)]
            public Quaternion startRayRotation;

            /// <summary>
            /// The interaction ray rotation of the spatial pointer state.
            /// </summary>
            [InputControl(displayName = "Interaction Ray Rotation", noisy = true, dontReset = true)]
            [FieldOffset(k_InteractionRayRotationOffset)]
            public Quaternion interactionRayRotation;

            /// <summary>
            /// The input device position of the spatial pointer state.
            /// </summary>
            [InputControl(displayName = "Input Device Position", noisy = true, dontReset = true)]
            [FieldOffset(k_InputDevicePositionOffset)]
            public Vector3 inputDevicePosition;

            /// <summary>
            /// The input device rotation of the spatial pointer state.
            /// </summary>
            [InputControl(displayName = "Input Device Rotation", noisy = true, dontReset = true)]
            [FieldOffset(k_InputDeviceRotationOffset)]
            public Quaternion inputDeviceRotation;

            /// <summary>
            /// The modifier key states of the spatial pointer state.
            /// </summary>
            [InputControl(displayName = "Modifier Keys", layout = "Integer", synthetic = true)]
            [FieldOffset(k_ModifierKeysOffset)]
            public ushort modifierKeys;

            /// <summary>
            /// The byte representation of the spatial pointer's VisionOSSpatialPointerKind.
            /// </summary>
            [InputControl(name = "kind", displayName = "Kind", layout = "Integer", synthetic = true)]
            [FieldOffset(k_KindIdOffset)]
            public byte kindId;

            /// <summary>
            /// The byte representation of the spatial pointer's phase Id.
            /// </summary>
            [InputControl(name = "phase", displayName = "Phase", layout = "TouchPhase", synthetic = true)]
            [FieldOffset(k_PhaseIdOffset)]
            public byte phaseId;

            /// <summary>
            /// The tracking status of the spatial pointer state.
            /// </summary>
            [InputControl(name = "isTracked", displayName = "IsTracked", layout = "Button", synthetic = true)]
            [FieldOffset(k_IsTrackedOffset)]
            public bool isTracked;

            /// <summary>
            /// The input tracking state of the spatial pointer state.
            /// </summary>
            [InputControl(name = "trackingState", displayName = "TrackingState", layout = "Integer", synthetic = true)]
            [FieldOffset(k_TrackingStateOffset)]
            public InputTrackingState trackingState;

            /// <summary>
            /// Represents the pointer event kind of the pointer state.
            /// </summary>
            public VisionOSSpatialPointerKind Kind
            {
                get => (VisionOSSpatialPointerKind)kindId;
                set => kindId = (byte)value;
            }

            /// <summary>
            /// Determines if the specified modifier key is pressed.
            /// </summary>
            /// <param name="key">The key to query it's pressed state.</param>
            /// <returns>A boolean that represents the pressed state.</returns>
            public bool IsModifierKeyPressed(VisionOSSpatialPointerModifierKeys key)
            {
                return (modifierKeys & (ushort)key) != 0;
            }

            /// <summary>
            /// The four character code representing the input events of the native visionOS backend.
            /// </summary>
            public FourCC format => Format;

            /// <summary>
            /// Manually sets the state of a specific modifier key.
            /// </summary>
            /// <param name="state">The state in which to update the modifier key.</param>
            /// <param name="modifierKey">The modifier key to update.</param>
            public void SetModifierKey(bool state, ushort modifierKey)
            {
                if (state)
                    modifierKeys |= modifierKey;
                else
                    modifierKeys &= (ushort)~modifierKey;
            }

            /// <summary>
            /// The phase of the spatial pointer event.
            /// </summary>
            public VisionOSSpatialPointerPhase phase
            {
                get => (VisionOSSpatialPointerPhase)phaseId;
                set => phaseId = (byte)value;
            }

            /// <summary>
            /// Returns true if the state is none, ended or cancelled.
            /// </summary>
            public bool isNoneEndedOrCanceled
            {
                get
                {
                    switch (phase)
                    {
                        case VisionOSSpatialPointerPhase.None:
                        case VisionOSSpatialPointerPhase.Ended:
                        case VisionOSSpatialPointerPhase.Cancelled:
                            return true;
                        default:
                            return false;
                    }
                }
            }
        }
    }
