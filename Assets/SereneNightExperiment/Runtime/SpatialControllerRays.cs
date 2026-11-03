using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
#if UNITY_VISIONOS
using Apple.visionOS.SpatialController;
#endif

// Apple supplies tracking; XRI handles hover, click and slider dragging.
[DefaultExecutionOrder(-100)]
public class SpatialControllerRays : MonoBehaviour
{
    public XRRayInteractor leftRay, rightRay;
    public Transform trackingOrigin;
    void Awake()
    {
        foreach(var ray in new[]{leftRay,rightRay}) {
            ray.uiPressInput.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;
            ray.gameObject.SetActive(false);
        }
#if UNITY_VISIONOS && !UNITY_EDITOR
        AccessoryTracking.Init();
#endif
    }
#if UNITY_VISIONOS
    void Update()
    {
        if(Application.isEditor)return;
        bool left=false,right=false;
        foreach(var accessory in AccessoryTracking.GetConnectedAccessories()) {
            bool isLeft=accessory.inherentChirality==AccessoryChirality.Left;
            if(!isLeft && accessory.inherentChirality!=AccessoryChirality.Right) continue;
            var ray=isLeft?leftRay:rightRay;
            var state=AccessoryTracking.PollController(accessory.source.uniqueId);
            var anchor=AccessoryTracking.PredictAnchor(accessory.source.uniqueId,SpatialControllerUtils.GetPredictAnchorTime());
            if(anchor==null || !anchor.isTracked) continue;
            if(!SpatialControllerUtils.ApplyAccessoryAnchorTransform(ray.transform,anchor,Accessory.LocationName.aim,ARKitCoordinateSpace.Correction.None)) continue;
            if(trackingOrigin)ray.transform.SetPositionAndRotation(
                trackingOrigin.TransformPoint(ray.transform.position),
                trackingOrigin.rotation*ray.transform.rotation);
            ray.gameObject.SetActive(true);
            float trigger=0;
            foreach(var key in new[]{ControllerInputName.ButtonTrigger,isLeft?ControllerInputName.ButtonLeftTrigger:ControllerInputName.ButtonRightTrigger})
                if(state.input.buttons.TryGetValue(key,out var button)) trigger=Mathf.Max(trigger,button.value);
            ray.uiPressInput.QueueManualState(trigger>.5f,trigger);
            if(isLeft)left=true;else right=true;
        }
        ReleaseIfLost(leftRay,left);ReleaseIfLost(rightRay,right);
    }
    static void ReleaseIfLost(XRRayInteractor ray,bool tracked)
    {
        if(tracked)return;
        ray.uiPressInput.QueueManualState(false,0);
        ray.gameObject.SetActive(false);
    }
    void OnDestroy() { if(!Application.isEditor)AccessoryTracking.Destroy(); }
#endif
}
