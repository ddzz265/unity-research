using UnityEngine;
using UnityEngine.InputSystem.XR;

// Retain the desktop camera for preview; native visionOS uses its tracked pose.
[DefaultExecutionOrder(-300)]
public class VisionExperimentRig : MonoBehaviour
{
    public TrackedPoseDriver headPose;
    public Behaviour[] desktopControls;
    void Awake()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        foreach(var control in desktopControls)if(control)control.enabled=false;
        headPose.enabled=true;
#endif
    }
}
