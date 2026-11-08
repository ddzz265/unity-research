using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.OpenXR;

// Passive combined-eye observation. Never substitutes head pose or selects UI.
[DefaultExecutionOrder(2000)]
public sealed class QuestEyeRecorder : MonoBehaviour
{
    public const string EyePermission="com.oculus.permission.EYE_TRACKING";
    [Serializable] public struct Sample
    {
        public double time, inputTime;
        public bool valid, newInput;
        public Vector3 origin, direction;
        public string status, area;
    }
    [SerializeField] Sample latest;
    public Sample Latest=>latest;
    public string CsvPath {get;private set;}
    public string WriteError=>writeError;
    public int DroppedRows=>droppedRows;
    public event Action<Sample> SampleUpdated;
    ExperimentGroupController experiment;
    SpatialControllerRays rig;
    Camera head;
    InputAction position,rotation,tracked,trackingState;
    BlockingCollection<string> rows;
    Thread writer;
    volatile string writeError;
    int droppedRows;
    bool paused,focused=true,stopped;
    double previousInput=-1,started;
    readonly StringBuilder line=new StringBuilder(1024);
    static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;

    void Start()
    {
        experiment=GetComponent<ExperimentGroupController>();
        head=Camera.main;rig=FindAnyObjectByType<SpatialControllerRays>();
        position=Action("position");rotation=Action("rotation");
        tracked=Action("isTracked");trackingState=Action("trackingState");
#if UNITY_ANDROID && !UNITY_EDITOR
        if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(EyePermission))
            UnityEngine.Android.Permission.RequestUserPermission(EyePermission);
#endif
        started=Time.realtimeSinceStartupAsDouble;
#if UNITY_EDITOR
        string folder="D:/Codexput/SereneNightResponses/EyeTracking";
#else
        string folder=Path.Combine(Application.persistentDataPath,"EyeTracking");
#endif
        try {
            Directory.CreateDirectory(folder);
            CsvPath=Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N")+".csv");
            var output=new StreamWriter(CsvPath,false,new UTF8Encoding(true),65536);
            output.WriteLine("# schema=1;source=OpenXR_EXT_combined_eye_gaze;utc_start="+DateTime.UtcNow.ToString("O")+";app_start_seconds="+started.ToString("R",Invariant));
            output.WriteLine("app_seconds,frame,input_update_seconds,new_input,valid,status,group,phase,question,transition_frame,transition_app_seconds,panel_visible,panel_alpha,panel_width,panel_height,ui_interactable,area,panel_u,panel_v,tracking_x,tracking_y,tracking_z,tracking_qx,tracking_qy,tracking_qz,tracking_qw,world_x,world_y,world_z,world_dx,world_dy,world_dz,head_x,head_y,head_z,head_qx,head_qy,head_qz,head_qw,frame_dt,dropped_rows");
            output.Flush();
            rows=new BlockingCollection<string>(1024);
            writer=new Thread(()=>WriteRows(output)){IsBackground=true,Name="Serene Eye CSV"};writer.Start();
            Debug.Log("Eye tracking CSV: "+CsvPath);
        } catch(Exception e) {writeError=e.Message;Debug.LogError("Eye recording unavailable: "+e.Message);}
    }
    InputAction Action(string control)
    {
        var action=new InputAction("Eye "+control,InputActionType.Value,"<EyeGaze>/pose/"+control);
        action.Enable();return action;
    }
    void WriteRows(StreamWriter output)
    {
        try {
            using(output) {
                var timer=System.Diagnostics.Stopwatch.StartNew();
                while(!rows.IsCompleted) {
                    if(rows.TryTake(out var row,250))output.WriteLine(row);
                    if(timer.ElapsedMilliseconds>=1000) {output.Flush();timer.Restart();}
                }
                output.Flush();
            }
        } catch(Exception e) {writeError=e.Message;}
    }
    void LateUpdate()
    {
        if(stopped || position==null || !experiment)return;
        var view=experiment.questionnaire;
        var origin=rig?rig.trackingOrigin:null;
        bool permission=true;
#if UNITY_ANDROID && !UNITY_EDITOR
        permission=UnityEngine.Android.Permission.HasUserAuthorizedPermission(EyePermission);
#endif
        var device=position.activeControl?.device;
        string status=!permission?"permission_not_granted":paused||!focused?"app_inactive":
            !OpenXRRuntime.IsExtensionEnabled("XR_EXT_eye_gaze_interaction")?"extension_unavailable":
            device==null?"eye_device_unavailable":!origin||!head?"tracking_space_unavailable":"ok";
        bool valid=status=="ok" && tracked.ReadValue<float>()>.5f && (trackingState.ReadValue<int>()&3)==3;
        if(status=="ok" && !valid)status="tracking_invalid";
        double inputTime=device?.lastUpdateTime??-1;
        bool newInput=device!=null && inputTime!=previousInput;previousInput=inputTime;
        Vector3 p=Vector3.zero,world=Vector3.zero,direction=Vector3.zero;
        Quaternion q=Quaternion.identity;
        if(valid) {
            p=position.ReadValue<Vector3>();q=rotation.ReadValue<Quaternion>();
            world=origin.TransformPoint(p);direction=origin.rotation*q*Vector3.forward;
            if(!float.IsFinite(world.x)||!float.IsFinite(world.y)||!float.IsFinite(world.z)||
               !float.IsFinite(direction.x)||!float.IsFinite(direction.y)||!float.IsFinite(direction.z)||direction.sqrMagnitude<.5f) {
                valid=false;status="nonfinite_pose";
            } else direction.Normalize();
        }
        Vector2 uv=new Vector2(float.NaN,float.NaN);
        string area=valid?"scene":"invalid";
        if(valid && view && view.TryGetEyePanelUV(new Ray(world,direction),out uv))area="questionnaire_bounds";
        latest=new Sample{time=Time.realtimeSinceStartupAsDouble,inputTime=inputTime,newInput=newInput,
            valid=valid,status=status,area=area,origin=valid?world:Vector3.zero,direction=valid?direction:Vector3.zero};
        line.Clear();
        Add(latest.time);Add(Time.frameCount);Add(inputTime);Add(newInput?1:0);Add(valid?1:0);Add(status);
        Add(experiment.group.ToString());Add(experiment.Phase.ToString());Add(view?view.CurrentQuestionIndex:-1);
        Add(experiment.TransitionFrame);Add(experiment.TransitionedAt);Add(view&&view.IsVisible?1:0);
        Add(view?view.PresentationAlpha:0);Add(view?view.PanelSize.x:0);Add(view?view.PanelSize.y:0);Add(view&&view.EyeUIInteractable?1:0);
        Add(area);Add(uv.x);Add(uv.y);
        if(valid) {Vec(p);Quat(q);Vec(world);Vec(direction);} else for(int i=0;i<13;i++)Add("");
        if(head) {Vec(head.transform.position);Quat(head.transform.rotation);} else for(int i=0;i<7;i++)Add("");
        Add(Time.unscaledDeltaTime);Add(droppedRows);
        if(rows!=null && writeError==null && !rows.TryAdd(line.ToString().TrimEnd(',')))droppedRows++;
        SampleUpdated?.Invoke(latest);
    }
    void Add(string value) {line.Append(value).Append(',');}
    void Add(double value) {if(!double.IsNaN(value)&&!double.IsInfinity(value))line.Append(value.ToString("R",Invariant));line.Append(',');}
    void Vec(Vector3 v) {Add(v.x);Add(v.y);Add(v.z);}
    void Quat(Quaternion q) {Add(q.x);Add(q.y);Add(q.z);Add(q.w);}
    void OnApplicationFocus(bool value) {focused=value;}
    void OnApplicationPause(bool value) {paused=value;}
    void OnDisable() {StopRecording();}
    public void StopRecording()
    {
        if(stopped)return;stopped=true;
        position?.Dispose();rotation?.Dispose();tracked?.Dispose();trackingState?.Dispose();
        rows?.CompleteAdding();
        if(writer!=null && !writer.Join(2000))Debug.LogWarning("Eye CSV is still draining; abrupt process termination may lose buffered rows.");
        if(writeError!=null)Debug.LogError("Eye CSV write failed: "+writeError);
        if(droppedRows>0)Debug.LogWarning("Eye CSV queue dropped rows: "+droppedRows);
    }
    void OnApplicationQuit() {StopRecording();}
}
