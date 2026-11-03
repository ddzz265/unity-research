#if INCLUDE_UNITY_INPUT_SYSTEM
// ENABLE_VR is not defined on Game Core but the assembly is available with limited features when the XR module is enabled.
#if UNITY_INPUT_SYSTEM_ENABLE_XR && (ENABLE_VR || UNITY_GAMECORE) && !UNITY_FORCE_INPUTSYSTEM_XR_OFF
#define USE_XR_INPUT
#endif

using System;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARSubsystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.XR.VisionOS.InputDevices
{
    using InputSystem = UnityEngine.InputSystem.InputSystem;

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    class InputLayoutLoader
    {
#if UNITY_EDITOR
        static InputLayoutLoader()
        {
            RegisterLayouts();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterLayouts()
        {
#if USE_XR_INPUT
            InputSystem.RegisterLayout<XRHMD>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct("(visionOS_HMD)")
                );

            InputSystem.RegisterLayout<HandheldARInputDevice>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct("(visionOS_HandheldARDevice)")
            );
#endif

            InputSystem.RegisterLayout<VisionOSSpatialPointerControl>(name: VisionOSSpatialPointerState.LayoutName);
            InputSystem.RegisterLayout<VisionOSSpatialPointerDevice>();
        }
    }
}
#endif
