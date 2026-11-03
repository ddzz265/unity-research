using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace UnityEngine.XR.VisionOS.InputDevices
{
    using InputDevice = UnityEngine.InputSystem.InputDevice;
    using InputSystem = UnityEngine.InputSystem.InputSystem;

    /// <summary>
    /// Receives serialized PolySpatialInput events and feeds them to the InputSystem
    /// </summary>
    internal class VisionOSSpatialPointerEventListener
    {
        readonly Dictionary<int, VisionOSSpatialPointerEventState> m_Inputs = new();
        readonly Dictionary<int, Vector3> m_StartInputDevicePositions = new();
        readonly VisionOSSpatialPointerDevice m_PointerDevice;
        static readonly Lazy<VisionOSSpatialPointerEventListener> k_Instance = new(() => new VisionOSSpatialPointerEventListener());

        struct QueuedEvent
        {
            public VisionOSSpatialPointerEvent Pointer;
        }

        // Store in class so can pass by ref easily
        class VisionOSSpatialPointerEventState
        {
            public readonly Queue<QueuedEvent> EventQueue = new();
            public VisionOSSpatialPointerPhase LastQueuedPhase;
            public VisionOSSpatialPointerState Pointer;
            // public TouchState touch;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        unsafe delegate void VisionOSInputEventCallback(int eventCount, void* eventsPtr);

        [MonoPInvokeCallback(typeof(VisionOSInputEventCallback))]
        static unsafe void OnInputEvent(int eventCount, void* eventsPtr)
        {
            // MonoPInvokeCallback methods will leak exceptions and cause crashes; always use a try/catch in these methods
            try
            {
                var inputEvents = NativeApi.Utilities.ConvertExistingDataToNativeArrayWithSafetyHandle<VisionOSSpatialPointerEvent>(
                    eventsPtr,
                    eventCount,
                    Allocator.None);

                k_Instance.Value.OnInputEvents(inputEvents);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal static void OnInputEvent(VisionOSSpatialPointerEvent @event)
        {
            k_Instance.Value.QueueEvent(@event);
        }

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static unsafe void SetupNativeCallback()
        {
            SetUpInputEventHandler(OnInputEvent);
        }

        [DllImport("__Internal", EntryPoint = "UnityVisionOS_SetUpInputEventHandler")]
        static extern void SetUpInputEventHandler(VisionOSInputEventCallback callback);
#endif

        public VisionOSSpatialPointerEventListener()
        {
            AddDevice(out VisionOSSpatialPointerDevice inputDevice);
            m_PointerDevice = inputDevice;
            m_PointerDevice.eventListener = this;

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += EditorApplicationOnplayModeStateChanged;
#endif
        }

#if UNITY_EDITOR
        static void EditorApplicationOnplayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingPlayMode)
                return;

            var devices = InputSystem.devices;
            for (var i = devices.Count - 1; i > -1; i--)
            {
                if (InputSystem.devices[i].name.Contains("VisionOSSpatialPointerDevice"))
                {
                    InputSystem.RemoveDevice(InputSystem.devices[i]);
                }
            }
        }
#endif

        static void AddDevice<T>(out T device) where T : InputDevice
        {
            var deviceName = typeof(T).ToString();
            device = InputSystem.AddDevice<T>(deviceName);
            if (device == null)
            {
                Debug.LogError($"Failed to create {deviceName}.");
            }
        }

        VisionOSSpatialPointerEventState GetPointerId(int rawId)
        {
            if (m_Inputs.TryGetValue(rawId, out var input))
            {
                return input;
            }

            var newId = m_Inputs.Count + 1; // touch id must be non-zero for actual touch, so +1
            var inputEvent = new VisionOSSpatialPointerEventState
            {
                Pointer = new VisionOSSpatialPointerState
                {
                    interactionId = newId
                }
            };

            m_Inputs.Add(rawId, inputEvent);
            return inputEvent;
        }

        void OnInputEvents(NativeArray<VisionOSSpatialPointerEvent> events)
        {
            foreach (var pointerEvent in events)
            {
                QueueEvent(pointerEvent);
            }
        }

        void QueueEvent(VisionOSSpatialPointerEvent inputEvent)
        {
            var state = GetPointerId(inputEvent.interactionId);

            switch (inputEvent.phase, state.LastQueuedPhase)
            {
                case (VisionOSSpatialPointerPhase.Began, VisionOSSpatialPointerPhase.Moved):
                case (VisionOSSpatialPointerPhase.Began, VisionOSSpatialPointerPhase.Began):
                    // Reality kit does not explicitly send cancel or ended if a particular input event stops.
                    // Currently appears to be for situation where input may have an error, out of sight or you click
                    // and move your so quickly, or multiple times fast, that it doesn't consider the prior input even
                    // to officially 'End' or 'Cancel', it just stops. So we must manually infer here.
                    var cancelledInputEvent = inputEvent;
                    cancelledInputEvent.phase = VisionOSSpatialPointerPhase.Cancelled;
                    state.EventQueue.Enqueue(new QueuedEvent {Pointer=cancelledInputEvent});
                    break;
                case (VisionOSSpatialPointerPhase.Moved, VisionOSSpatialPointerPhase.Cancelled):
                case (VisionOSSpatialPointerPhase.Moved, VisionOSSpatialPointerPhase.Ended):
                case (VisionOSSpatialPointerPhase.Moved, VisionOSSpatialPointerPhase.None):
                    // Reality kit does not have it's own explicit 'Began' phase so sometimes it must be inferred.
                    // The swift code attempts to infer began phase if the id unique for each input event changes
                    // but currently input events don't always change to a new unique id when they should have a began phase.
                    var beganInputEvent = inputEvent;
                    beganInputEvent.phase = VisionOSSpatialPointerPhase.Began;
                    state.EventQueue.Enqueue(new QueuedEvent {Pointer=beganInputEvent});
                    break;
            }

            // Multiple events may arrive from realitykit in a single update cycle and due to the way Unity
            // InputSystem.QueueStateEvent works, setting multiple events in one cycle may overwrite a prior one
            // and it also won't enable it to proliferate through the system for things like EnhancedTouch to poll.
            // So we have to manage our own additional queue to send them one at a time.
            state.LastQueuedPhase = inputEvent.phase;
            state.EventQueue.Enqueue(new QueuedEvent {Pointer = inputEvent});
        }

        internal void ProcessEventQueue()
        {
            // Right now this isn't more than two values, it should never be that many, so looping through shouldn't be an issue
            foreach (var state in m_Inputs.Values)
            {
                if (state.EventQueue.TryDequeue(out var inputEvent))
                {
                    ProcessEvent(state.Pointer, inputEvent.Pointer);
                }
            }
        }

        void ProcessEvent(VisionOSSpatialPointerState pointerState, VisionOSSpatialPointerEvent inputEvent)
        {
            var phase = inputEvent.phase;
            var inputDevicePosition = inputEvent.inputDevicePosition;
            var interactionId = pointerState.interactionId;
            if (phase == VisionOSSpatialPointerPhase.Began)
                m_StartInputDevicePositions[interactionId] = inputDevicePosition;

            // Compute interaction ray rotation based on device position
            var rayOrigin = inputEvent.rayOrigin;
            var rayDirection = inputEvent.rayDirection;
            if (m_StartInputDevicePositions.TryGetValue(interactionId, out var startInputDevicePosition))
            {
                // Calculate start position at arbitrary distance, roughly within arms' reach
                var gazeRay = new Ray(rayOrigin, rayDirection);
                var startPosition = gazeRay.GetPoint(0.5f);

                // Update current position based on distance inputDevicePosition has moved
                var currentPosition = startPosition + (inputDevicePosition - startInputDevicePosition);
                var interactionRayDirection = Vector3.Normalize(currentPosition - rayOrigin);
                pointerState.interactionRayRotation = interactionRayDirection == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(interactionRayDirection);
            }

            switch (phase)
            {
                case VisionOSSpatialPointerPhase.Cancelled:
                case VisionOSSpatialPointerPhase.Ended:
                case VisionOSSpatialPointerPhase.None:
                    m_StartInputDevicePositions.Remove(interactionId);
                    break;
            }

            pointerState.phaseId = (byte)phase;
            pointerState.startRayOrigin = inputEvent.rayOrigin;
            pointerState.startRayDirection = rayDirection;
            pointerState.startRayRotation = rayDirection == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(rayDirection);
            pointerState.kindId = (byte)inputEvent.kind;
            pointerState.modifierKeys = (ushort)inputEvent.modifierKeys;
            pointerState.inputDevicePosition = inputEvent.inputDevicePosition;
            pointerState.inputDeviceRotation = inputEvent.inputDeviceRotation;
            var isTracked = phase == VisionOSSpatialPointerPhase.Began || phase == VisionOSSpatialPointerPhase.Moved;
            pointerState.isTracked = isTracked;
            pointerState.trackingState = isTracked ? InputTrackingState.Position | InputTrackingState.Rotation : InputTrackingState.None;
            InputSystem.QueueStateEvent(m_PointerDevice, pointerState);
        }
    }
}
