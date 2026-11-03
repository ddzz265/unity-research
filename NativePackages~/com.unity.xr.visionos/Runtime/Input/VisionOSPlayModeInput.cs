#if UNITY_EDITOR && UNITY_VISIONOS
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#if INCLUDE_UNITY_POLYSPATIAL
using Unity.PolySpatial;
using Unity.PolySpatial.Internals;
#endif

#if INCLUDE_UNITY_XRI
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif
#endif

namespace UnityEngine.XR.VisionOS.InputDevices
{
    /// <summary>
    /// Handles visionOS Play Mode Input by translating mouse input into VisionOSSpatialPointerEvents.
    /// </summary>
    public class VisionOSPlayModeInput : MonoBehaviour
    {
        [SerializeField]
        Transform m_XROrigin;

        [SerializeField]
        bool m_DrawDebugRay;

        [SerializeField]
        bool m_EnableSecondaryPointer;

#if UNITY_EDITOR && UNITY_VISIONOS
        bool m_WasInteracting;
        Ray m_InteractionRay;
        Vector3 m_StartRayOrigin;
        Vector3 m_StartRayDirection;
        Vector2 m_PreviousMousePosition;

#if INCLUDE_UNITY_XRI
        readonly List<XRUIInputModule> m_PreviouslyEnabledModules = new();

        void OnEnable()
        {
            // Disable mouse input on input modules in order to prevent doubling up of events
            m_PreviouslyEnabledModules.Clear();
#if UNITY_6000_4_OR_NEWER
            foreach (var inputModule in FindObjectsByType<XRUIInputModule>(FindObjectsInactive.Include))
#else
            foreach (var inputModule in FindObjectsByType<XRUIInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
#endif
            {
                if (inputModule.enableMouseInput)
                {
                    m_PreviouslyEnabledModules.Add(inputModule);
                    inputModule.enableMouseInput = false;
                }
            }
        }

        void OnDisable()
        {
            // Re-enable mouse input on input modules if it was previously enabled
            foreach (var module in m_PreviouslyEnabledModules)
            {
                if (module != null)
                    module.enableMouseInput = true;
            }

            m_PreviouslyEnabledModules.Clear();
        }
#endif

#if INCLUDE_UNITY_POLYSPATIAL
        // Unless otherwise determined, assume that we are running in metal mode
        // when PSL package is installed. Only change that if PSL tells us otherwise.
        bool m_IsMetalMode = true;

        void Awake()
        {
            var simulation = PolySpatialCore.UnitySimulation;
            if (simulation == null)
                return;

            simulation.VolumeCamerasChanged += UpdateMetalMode;
            UpdateMetalMode();
        }

        void UpdateMetalMode()
        {
            var simulation = PolySpatialCore.UnitySimulation;
            if (simulation == null)
                return;

            m_IsMetalMode = simulation.IsMetalMode;
        }
#endif

        void Update()
        {
#if INCLUDE_UNITY_POLYSPATIAL
            if (!m_IsMetalMode)
            {
                CancelCurrentInteractionIfNeeded();
                return;
            }
#endif

            var mouse = Mouse.current;
            if (mouse == null)
            {
                CancelCurrentInteractionIfNeeded();
                return;
            }

            var inputCamera = Camera.main;
            if (inputCamera == null)
                inputCamera = FindAnyObjectByType<Camera>();

            if (inputCamera == null)
            {
                CancelCurrentInteractionIfNeeded();
                return;
            }

            var mousePosition = mouse.position.ReadValue();

            // Reject clicks outside of game view
            if (mousePosition.x < 0 || mousePosition.y < 0 || mousePosition.x > Screen.width || mousePosition.y > Screen.height)
            {
                CancelCurrentInteractionIfNeeded();
                return;
            }

            m_InteractionRay= inputCamera.ScreenPointToRay(mousePosition);

            var leftButton = mouse.leftButton;
            var rayOrigin = m_InteractionRay.origin;
            var rayDirection = m_InteractionRay.direction;
            if (m_DrawDebugRay)
            {
                var direction = rayDirection * inputCamera.farClipPlane;
                var color = leftButton.isPressed ? Color.red : Color.blue;
                var duration = leftButton.wasPressedThisFrame ? 3f : 0.05f;
                Debug.DrawRay(rayOrigin, direction, color, duration);
            }

            VisionOSSpatialPointerPhase phase;
            if (leftButton.wasPressedThisFrame)
            {
                phase = VisionOSSpatialPointerPhase.Began;
                m_StartRayOrigin = rayOrigin;
                m_StartRayDirection = rayDirection;
                m_WasInteracting = true;
            }
            else if (leftButton.wasReleasedThisFrame)
            {
                phase = VisionOSSpatialPointerPhase.Ended;
                m_WasInteracting = false;
            }
            else if (leftButton.isPressed)
            {
                // Do not send events if mouse hasn't moved to more accurately represent device behavior
                if (mousePosition == m_PreviousMousePosition)
                    return;

                phase = VisionOSSpatialPointerPhase.Moved;
            }
            else
            {
                return;
            }

            SendInputEvent(m_InteractionRay, phase);

            m_PreviousMousePosition = mousePosition;
        }

        void CancelCurrentInteractionIfNeeded()
        {
            if (!m_WasInteracting)
                return;

            // Send a final Ended command, otherwise input can get "stuck"
            SendInputEvent(m_InteractionRay, VisionOSSpatialPointerPhase.Ended);

            m_WasInteracting = false;
        }

        void SendInputEvent(Ray ray, VisionOSSpatialPointerPhase phase)
        {
            // Create a fake input device pose position roughly at arm's length along the ray
            var inputDevicePosition = ray.GetPoint(0.5f);

            // Transform input into local camera space to more accurately reflect platform input
            var worldToLocal = m_XROrigin.transform.worldToLocalMatrix;
            var startRayOrigin = worldToLocal.MultiplyPoint(m_StartRayOrigin);
            var startRayDirection = worldToLocal.MultiplyVector(m_StartRayDirection);
            inputDevicePosition = worldToLocal.MultiplyPoint(inputDevicePosition);
            var rayDirection = worldToLocal.MultiplyVector(ray.direction);
            var pointerEvent = new VisionOSSpatialPointerEvent
            {
                interactionId = 0,
                phase = phase,
                inputDevicePosition = inputDevicePosition,
                inputDeviceRotation = Quaternion.LookRotation(rayDirection),
                rayOrigin = startRayOrigin,
                rayDirection = startRayDirection,
                kind = VisionOSSpatialPointerKind.IndirectPinch
            };

            VisionOSSpatialPointerEventListener.OnInputEvent(pointerEvent);

            if (m_EnableSecondaryPointer)
            {
                pointerEvent.interactionId = 1;
                VisionOSSpatialPointerEventListener.OnInputEvent(pointerEvent);
            }
        }
#endif
    }
}
