using UnityEngine;
using UnityEngine.InputSystem.XR;

// Retain desktop preview and enable native head tracking on the headset.
[DefaultExecutionOrder(-300)]
public class VisionExperimentRig : MonoBehaviour
{
    public TrackedPoseDriver headPose;
    public Behaviour[] desktopControls;
    bool xrConfigured;
#if UNITY_ANDROID && !UNITY_EDITOR
    UnityEngine.XR.XRDisplaySubsystem display;
    ExperimentGroupController experiment;
    bool mindfulnessScene;
    readonly System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem> displays=new();
    void OnEnable() {Application.onBeforeRender+=LatchMindfulnessHead;}
    void OnDisable() {Application.onBeforeRender-=LatchMindfulnessHead;}
    [BeforeRenderOrder(200)]
    void LatchMindfulnessHead()
    {
        if(!xrConfigured || !mindfulnessScene || !headPose || !headPose.enabled || !experiment ||
           experiment.Phase!=ExperimentPhase.Mindfulness ||
           SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Vulkan)return;
        if(display==null || !display.running) {
            display=null;displays.Clear();SubsystemManager.GetSubsystems(displays);
            foreach(var item in displays)if(item.running) {display=item;break;}
        }
        // URP records and patches its camera matrices; mark the tracked camera, never its route parent.
        if(display!=null)display.MarkTransformLateLatched(headPose.transform,UnityEngine.XR.XRDisplaySubsystem.LateLatchNode.Head);
    }
#endif
    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        ConfigureQuestTracking();
#endif
#if UNITY_VISIONOS && !UNITY_EDITOR
        foreach(var control in desktopControls)if(control)control.enabled=false;
        if(headPose)headPose.enabled=true;
#endif
    }
    void Update() {
#if !UNITY_VISIONOS
        if(!xrConfigured && UnityEngine.XR.XRSettings.isDeviceActive)ConfigureQuestTracking();
#endif
    }
    void ConfigureQuestTracking()
    {
        if(xrConfigured)return;
        xrConfigured=true;
#if UNITY_ANDROID && !UNITY_EDITOR
        experiment=FindAnyObjectByType<ExperimentGroupController>();
        mindfulnessScene=gameObject.scene.name=="Serene_Night_Experiment";
#endif
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

        foreach(var control in desktopControls)if(control)control.enabled=false;
        if(headPose) {
            headPose.trackingType=TrackedPoseDriver.TrackingType.RotationAndPosition;
            headPose.updateType=TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            headPose.enabled=true;
        }
        Debug.Log($"XR device model: {SystemInfo.deviceModel}; requested platform: {Application.platform}");
    }
}
