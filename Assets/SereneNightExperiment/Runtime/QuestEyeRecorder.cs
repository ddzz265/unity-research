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
    public string SessionId=>Path.GetFileNameWithoutExtension(CsvPath);
    public string BreathStatus=>breath?.Status??"unavailable";
    public string BreathWriteError=>breath?.Error;
    public event Action<Sample> SampleUpdated;
    ExperimentGroupController experiment;
    SpatialControllerRays rig;
    Camera head;
    InputAction position,rotation,tracked,trackingState;
    struct CsvRow
    {
        public Sample sample;
        public int frame,question,transitionFrame,dropped;
        public ExperimentGroup group;
        public ExperimentPhase phase;
        public double transitionedAt;
        public bool panelVisible,uiInteractable,hasHead;
        public float panelAlpha,panelWidth,panelHeight,frameDelta;
        public Vector2 uv;
        public Vector3 trackingPosition,headPosition;
        public Quaternion trackingRotation,headRotation;
    }
    BlockingCollection<CsvRow> rows;
    Thread writer;
    BreathStreamRecorder breath;
    volatile string writeError;
    int droppedRows;
    bool paused,focused=true,stopped;
    double previousInput=-1,started;
    readonly StringBuilder line=new StringBuilder(1024);
    readonly char[] csvCharacters=new char[2048];
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
            rows=new BlockingCollection<CsvRow>(1024);
            writer=new Thread(()=>WriteRows(output)){IsBackground=true,Name="Serene Eye CSV"};writer.Start();
            Debug.Log("Eye tracking CSV: "+CsvPath);
        } catch(Exception e) {writeError=e.Message;Debug.LogError("Eye recording unavailable: "+e.Message);}
        if(writeError==null) {
            try {
                // Pair the clocks here; the UDP thread never reads Unity APIs.
                double before=BreathStreamRecorder.MonotonicSeconds;
                double app=Time.realtimeSinceStartupAsDouble;
                double after=BreathStreamRecorder.MonotonicSeconds;
                breath=new BreathStreamRecorder(CsvPath,app,(before+after)/2);
                Debug.Log("Physiology session: "+SessionId+"; respiration UDP port: "+BreathStreamRecorder.DefaultPort);
                if(breath.Error!=null)Debug.LogError("Respiration recording unavailable: "+breath.Error);
            } catch(Exception e) {Debug.LogError("Respiration recording unavailable: "+e.Message);}
        }
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
                    if(rows.TryTake(out var row,250))WriteRow(output,row);
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
        breath?.SetContext(Time.frameCount,(int)experiment.group,(int)experiment.Phase,
            view?view.CurrentQuestionIndex:-1,experiment.TransitionedAt,!paused&&focused);
        // Snapshot Unity state on the main thread. Formatting and file I/O stay on the writer.
        var row=new CsvRow {
            sample=latest,frame=Time.frameCount,group=experiment.group,phase=experiment.Phase,
            question=view?view.CurrentQuestionIndex:-1,transitionFrame=experiment.TransitionFrame,
            transitionedAt=experiment.TransitionedAt,panelVisible=view&&view.IsVisible,
            panelAlpha=view?view.PresentationAlpha:0,panelWidth=view?view.PanelSize.x:0,
            panelHeight=view?view.PanelSize.y:0,uiInteractable=view&&view.EyeUIInteractable,uv=uv,
            trackingPosition=p,trackingRotation=q,hasHead=head,
            headPosition=head?head.transform.position:Vector3.zero,
            headRotation=head?head.transform.rotation:Quaternion.identity,
            frameDelta=Time.unscaledDeltaTime,dropped=droppedRows
        };
        if(rows!=null && writeError==null && !rows.TryAdd(row))droppedRows++;
        SampleUpdated?.Invoke(latest);
    }
    void WriteRow(StreamWriter output,CsvRow row)
    {
        line.Clear();
        var sample=row.sample;
        Add(sample.time);Add(row.frame);Add(sample.inputTime);Add(sample.newInput?1:0);Add(sample.valid?1:0);Add(sample.status);
        Add(row.group.ToString());Add(row.phase.ToString());Add(row.question);
        Add(row.transitionFrame);Add(row.transitionedAt);Add(row.panelVisible?1:0);
        Add(row.panelAlpha);Add(row.panelWidth);Add(row.panelHeight);Add(row.uiInteractable?1:0);
        Add(sample.area);Add(row.uv.x);Add(row.uv.y);
        if(sample.valid) {Vec(row.trackingPosition);Quat(row.trackingRotation);Vec(sample.origin);Vec(sample.direction);} else for(int i=0;i<13;i++)Add("");
        if(row.hasHead) {Vec(row.headPosition);Quat(row.headRotation);} else for(int i=0;i<7;i++)Add("");
        Add(row.frameDelta);Add(row.dropped);
        int count=line.Length-1; // omit the final comma, as in the existing CSV schema
        line.CopyTo(0,csvCharacters,0,count);output.Write(csvCharacters,0,count);output.WriteLine();
    }
    void Add(string value) {line.Append(value).Append(',');}
    void Add(double value)
    {
        if(!double.IsNaN(value)&&!double.IsInfinity(value)) {
            Span<char> digits=stackalloc char[32];
            if(!value.TryFormat(digits,out int written,"R",Invariant))throw new InvalidOperationException("Eye CSV numeric formatting failed.");
            line.Append(digits.Slice(0,written));
        }
        line.Append(',');
    }
    void Vec(Vector3 v) {Add(v.x);Add(v.y);Add(v.z);}
    void Quat(Quaternion q) {Add(q.x);Add(q.y);Add(q.z);Add(q.w);}
    void OnApplicationFocus(bool value) {focused=value;UpdateBreathContext();}
    void OnApplicationPause(bool value) {paused=value;UpdateBreathContext();}
    void UpdateBreathContext()
    {
        if(!experiment)return;
        breath?.SetContext(Time.frameCount,(int)experiment.group,(int)experiment.Phase,
            experiment.questionnaire?experiment.questionnaire.CurrentQuestionIndex:-1,experiment.TransitionedAt,!paused&&focused);
    }
    void OnDisable() {StopRecording();}
    public void StopRecording()
    {
        if(stopped)return;stopped=true;
        breath?.Dispose();
        if(breath?.Error!=null)Debug.LogError("Respiration recording failed: "+breath.Error);
        position?.Dispose();rotation?.Dispose();tracked?.Dispose();trackingState?.Dispose();
        rows?.CompleteAdding();
        if(writer!=null && !writer.Join(2000))Debug.LogWarning("Eye CSV is still draining; abrupt process termination may lose buffered rows.");
        if(writeError!=null)Debug.LogError("Eye CSV write failed: "+writeError);
        if(droppedRows>0)Debug.LogWarning("Eye CSV queue dropped rows: "+droppedRows);
    }
    void OnApplicationQuit() {StopRecording();}
}
