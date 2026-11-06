using UnityEngine;
using UnityEngine.InputSystem.XR;

// Retain desktop preview and enable native head tracking on the headset.
[DefaultExecutionOrder(-300)]
public class VisionExperimentRig : MonoBehaviour
{
    public TrackedPoseDriver headPose;
    public Behaviour[] desktopControls;
    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if(headPose) {
            // Routes move the original parent. Keep the authored eye height when
            // OpenXR's device-relative pose replaces the camera's local position.
            var head=headPose.transform;
            var space=new GameObject("Quest Tracking Space").transform;
            space.SetParent(head.parent,false);space.localPosition=head.localPosition;
            head.SetParent(space,false);head.localPosition=Vector3.zero;
            var rays=GetComponent<SpatialControllerRays>();if(rays)rays.trackingOrigin=space;
        }
        var inputs=new System.Collections.Generic.List<UnityEngine.XR.XRInputSubsystem>();
        SubsystemManager.GetSubsystems(inputs);
        foreach(var input in inputs)if(input.running && !input.TrySetTrackingOriginMode(UnityEngine.XR.TrackingOriginModeFlags.Device))
            Debug.LogError("Quest device-relative tracking origin could not be selected; verify eye height before using the experiment.");
#endif
#if (UNITY_VISIONOS || UNITY_ANDROID) && !UNITY_EDITOR
        foreach(var control in desktopControls)if(control)control.enabled=false;
        if(headPose)headPose.enabled=true;
        Debug.Log($"XR device model: {SystemInfo.deviceModel}; requested platform: {Application.platform}");
#endif
    }
}
