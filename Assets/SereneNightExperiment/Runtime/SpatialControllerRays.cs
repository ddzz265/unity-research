using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
#if UNITY_VISIONOS
using Apple.visionOS.SpatialController;
#endif

// Platform tracking supplies aim poses; XRI handles hover, click and slider dragging.
[DefaultExecutionOrder(-100)]
public class SpatialControllerRays : MonoBehaviour
{
    public XRRayInteractor leftRay, rightRay;
    public Transform trackingOrigin;
    void Awake()
    {
        foreach(var ray in new[]{leftRay,rightRay}) {
            if(!ray)continue;
            ray.enableUIInteraction=true;
            ray.uiPressInput.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;
            ray.gameObject.SetActive(false);
        }
#if UNITY_VISIONOS && !UNITY_EDITOR
        AccessoryTracking.Init();
#endif
    }

#if !UNITY_VISIONOS
    sealed class QuestHand
    {
        readonly UnityEngine.InputSystem.InputAction position, rotation, tracked, trigger;
        public QuestHand(string hand)
        {
            string path="<XRController>{"+hand+"}/";
            position=new UnityEngine.InputSystem.InputAction("Aim Position",UnityEngine.InputSystem.InputActionType.Value,binding:path+"pointerPosition");
            rotation=new UnityEngine.InputSystem.InputAction("Aim Rotation",UnityEngine.InputSystem.InputActionType.Value,binding:path+"pointerRotation");
            tracked=new UnityEngine.InputSystem.InputAction("Tracked",UnityEngine.InputSystem.InputActionType.Value,binding:path+"isTracked");
            trigger=new UnityEngine.InputSystem.InputAction("UI Trigger",UnityEngine.InputSystem.InputActionType.Value,binding:path+"trigger");
            position.Enable();rotation.Enable();tracked.Enable();trigger.Enable();
        }
        public void Apply(XRRayInteractor ray,Transform origin)
        {
            if(!ray)return;
            bool valid=tracked.ReadValue<float>()>.5f && position.activeControl!=null && rotation.activeControl!=null;
            if(!valid) {ray.uiPressInput.QueueManualState(false,0);ray.gameObject.SetActive(false);return;}
            Vector3 p=position.ReadValue<Vector3>();Quaternion q=rotation.ReadValue<Quaternion>();
            ray.transform.SetPositionAndRotation(origin?origin.TransformPoint(p):p,origin?origin.rotation*q:q);
            ray.gameObject.SetActive(true);
            float value=trigger.ReadValue<float>();ray.uiPressInput.QueueManualState(value>.5f,value);
        }
        public void Dispose() {position.Dispose();rotation.Dispose();tracked.Dispose();trigger.Dispose();}
    }
    QuestHand questLeft,questRight;
    UnityEngine.InputSystem.InputAction abortExposure;
    ExperimentGroupController experiment;
    void OnEnable()
    {
        questLeft=new QuestHand("LeftHand");questRight=new QuestHand("RightHand");
        experiment=FindAnyObjectByType<ExperimentGroupController>();
        abortExposure=new UnityEngine.InputSystem.InputAction("Abort Exposure",UnityEngine.InputSystem.InputActionType.Button,binding:"<XRController>{LeftHand}/secondaryButton");
        abortExposure.Enable();
        // The exposure bridge has task markers up to 50 metres ahead.
        if(leftRay)leftRay.maxRaycastDistance=60;
        if(rightRay)rightRay.maxRaycastDistance=60;
    }
    void Update() {
        questLeft?.Apply(leftRay,trackingOrigin);questRight?.Apply(rightRay,trackingOrigin);
        if(abortExposure!=null && abortExposure.WasPressedThisFrame() && experiment && experiment.Phase==ExperimentPhase.Exposure)
            experiment.exposureSession.Abort();
    }
    void OnDisable()
    {
        abortExposure?.Dispose();abortExposure=null;
        questLeft?.Dispose();questRight?.Dispose();questLeft=questRight=null;
        foreach(var ray in new[]{leftRay,rightRay})if(ray) {ray.uiPressInput.QueueManualState(false,0);ray.gameObject.SetActive(false);}
    }
#endif

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
