using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Head-direction dwell and the existing mouse/controller world-space UI share one completion path.
public sealed class ExposureTasks : MonoBehaviour
{
    public const float DwellSeconds=1.5f;
    public int CompletedCount {get;private set;}
    public string Status=>CompletedCount==3?"本阶段任务已完成，请继续观察。":$"任务 {CompletedCount}/3：凝视目标 1.5 秒，或点选目标";
    public RectTransform[] Targets {get;private set;}
    public int CurrentStage {get;private set;}=-1;
    public bool Active {get;private set;}
    ExposureSession session;
    Canvas canvas;
    TextMeshProUGUI[] labels;
    Image[] fills;
    readonly bool[,] completed=new bool[5,3];
    readonly float[,] times=new float[5,3];
    readonly string[,] methods=new string[5,3];
    static readonly string[][] Names={
        new[]{"观察左侧建筑","找到塔楼方向","观察右侧建筑"},
        new[]{"观察平台左侧","观察桥面方向","观察下方地面"},
        new[]{"观察升高后的左侧","观察远处建筑","观察升高后的下方"},
        new[]{"找到桥前段标记","找到桥中段标记","找到桥终点标记"},
        new[]{"观察高层左侧","观察远处天际线","完成最后一次下望"}};
    public static string LabelCharacters=>string.Concat(Array.ConvertAll(Names,n=>string.Concat(n)))+"✓ 已完成";
    float dwell;
    int focused=-1,level=-1;
    bool applicationPaused,hasFocus=true;
    ExperimentGroup group;

    public void Begin(ExposureSession owner)
    {
        session=owner;group=owner.controller.group;Array.Clear(completed,0,completed.Length);
        Array.Clear(times,0,times.Length);Array.Clear(methods,0,methods.Length);
        CurrentStage=-1;level=-1;Active=true;CompletedCount=0;ResetFocus();
        if(!canvas)Build();canvas.gameObject.SetActive(true);SyncStage();
    }
    void Build()
    {
        var go=new GameObject("Exposure Task Targets",typeof(RectTransform),typeof(Canvas),typeof(TrackedDeviceGraphicRaycaster));go.layer=5;
        canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
        canvas.worldCamera=session.controller.GetComponent<QuestionnaireBackgroundBlur>().UICamera;
        Targets=new RectTransform[3];labels=new TextMeshProUGUI[3];fills=new Image[3];
        for(int i=0;i<3;i++) {
            int index=i;
            var target=new GameObject("Task "+(i+1),typeof(RectTransform),typeof(Image),typeof(Button));target.layer=5;target.transform.SetParent(go.transform,false);
            Targets[i]=(RectTransform)target.transform;Targets[i].sizeDelta=new Vector2(340,115);Targets[i].localScale=Vector3.one*.004f;
            target.GetComponent<Image>().color=new Color(.03f,.19f,.23f,.95f);
            target.GetComponent<Button>().onClick.AddListener(()=>Select(index,"click"));
            var label=new GameObject("Task Label",typeof(RectTransform),typeof(TextMeshProUGUI));label.layer=5;label.transform.SetParent(target.transform,false);
            var rect=(RectTransform)label.transform;rect.sizeDelta=new Vector2(330,95);
            labels[i]=label.GetComponent<TextMeshProUGUI>();labels[i].font=session.LabelFont;labels[i].fontSize=30;labels[i].alignment=TextAlignmentOptions.Center;labels[i].color=Color.white;labels[i].raycastTarget=false;
            var bar=new GameObject("Dwell Progress",typeof(RectTransform),typeof(Image));bar.layer=5;bar.transform.SetParent(target.transform,false);
            var br=(RectTransform)bar.transform;br.anchorMin=new Vector2(0,0);br.anchorMax=new Vector2(0,0);br.pivot=new Vector2(0,0);br.anchoredPosition=Vector2.zero;br.sizeDelta=new Vector2(0,8);
            fills[i]=bar.GetComponent<Image>();fills[i].color=new Color(.2f,.9f,.65f);fills[i].raycastTarget=false;
        }
    }
    public void SyncStage()
    {
        if(!Active)return;
        int next=session.Stage, nextLevel=session.Elapsed>=270?1:0;
        bool changed=CurrentStage!=next;
        if(changed) {CurrentStage=next;CompletedCount=0;ResetFocus();}
        if(changed || level!=nextLevel) {
            level=nextLevel;
            Vector3 anchor=session.PositionAt(session.Elapsed);
            for(int i=0;i<3;i++) {
                Vector3 p=anchor+new Vector3((i-1)*3,1.6f,8);
                if(i==2)p.y=anchor.y+.1f;
                if(next==3) {
                    float z=Mathf.Lerp(session.high.z,session.bridgeEnd.z,(i+1)/3f);
                    p=new Vector3(session.high.x+(i-1)*Mathf.Max(3f,(z-session.high.z)*.2f),session.high.y+1.2f,z);
                }
                Targets[i].SetPositionAndRotation(p,Quaternion.identity);
                Targets[i].gameObject.SetActive(!completed[next,i]);
                labels[i].text=completed[next,i]?"✓ 已完成":$"{i+1}  {Names[next][i]}";
            }
        }
    }
    void LateUpdate()
    {
        if(!Active || !session || !session.head)return;
        // Preserve the task anchors; keep the text readable at a constant angular size.
        Vector3 eye=session.head.transform.position;
        foreach(var target in Targets) {
            if(!target.gameObject.activeSelf)continue;
            Vector3 direction=target.position-eye;
            target.localScale=Vector3.one*(.004f*Mathf.Clamp(direction.magnitude/8f,1f,8f));
            if(direction.sqrMagnitude>.001f)target.rotation=Quaternion.LookRotation(direction);
        }
    }
    bool CanInteract=>Active && session.Running && !session.Paused && session.controller.Phase==ExperimentPhase.Exposure &&
        !AudioListener.pause && !applicationPaused && hasFocus && Time.timeScale>0 && !IsTransfer;
    bool IsTransfer=>Mathf.Abs(session.Elapsed-60)<.5f || Mathf.Abs(session.Elapsed-180)<.5f || Mathf.Abs(session.Elapsed-270)<.5f || Mathf.Abs(session.Elapsed-540)<.5f;
    public void Select(int index,string method)
    {
        if(!CanInteract || index<0 || index>=3 || completed[CurrentStage,index])return;
        completed[CurrentStage,index]=true;times[CurrentStage,index]=session.Elapsed;methods[CurrentStage,index]=method;
        CompletedCount++;labels[index].text="✓ 已完成";Targets[index].gameObject.SetActive(false);ResetFocus();
    }
    public void ProcessGaze(Ray ray,float seconds)
    {
        if(!CanInteract) {ResetFocus();return;}
        int hit=-1;float nearest=float.MaxValue;
        for(int i=0;i<3;i++) {
            if(completed[CurrentStage,i])continue;
            var rect=Targets[i];var plane=new Plane(rect.forward,rect.position);
            if(plane.Raycast(ray,out float distance) && distance<nearest && rect.rect.Contains((Vector2)rect.InverseTransformPoint(ray.GetPoint(distance)))) {nearest=distance;hit=i;}
        }
        if(hit!=focused) {ResetFocus();focused=hit;}
        if(hit<0)return;
        dwell+=Mathf.Max(0,seconds);fills[hit].rectTransform.sizeDelta=new Vector2(340*Mathf.Clamp01(dwell/DwellSeconds),8);
        if(dwell>=DwellSeconds)Select(hit,"head_gaze");
    }
    void ResetFocus()
    {
        focused=-1;dwell=0;
        if(fills!=null)foreach(var fill in fills)if(fill)fill.rectTransform.sizeDelta=new Vector2(0,8);
    }
    void Update()
    {
        if(!Active)return;
        SyncStage();
#if !UNITY_VISIONOS || UNITY_EDITOR
        if(CanInteract && !UnityEngine.XR.XRSettings.isDeviceActive && Mouse.current!=null && Mouse.current.rightButton.isPressed) {
            var delta=Mouse.current.delta.ReadValue();var angles=session.head.transform.localEulerAngles;
            float pitch=Mathf.DeltaAngle(0,angles.x)-delta.y*.12f;
            session.head.transform.localRotation=Quaternion.Euler(Mathf.Clamp(pitch,-70,70),angles.y+delta.x*.12f,0);
        }
#endif
        ProcessGaze(new Ray(session.head.transform.position,session.head.transform.forward),Time.unscaledDeltaTime);
    }
    public void End(string outcome)
    {
        if(!Active)return;Active=false;canvas.gameObject.SetActive(false);ResetFocus();
        var csv=new StringBuilder("group,stage,task,completed,elapsed_seconds,input,outcome\r\n");
        for(int stage=0;stage<5;stage++)for(int i=0;i<3;i++)
            csv.AppendLine($"{group},{stage+1},{i+1},{completed[stage,i]},{times[stage,i].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)},{methods[stage,i]},{outcome}");
        try {
#if UNITY_EDITOR
            string folder="D:/Codexput/SereneNightResponses/ExposureTasks";
#else
            string folder=Path.Combine(Application.persistentDataPath,"ExposureTasks");
#endif
            Directory.CreateDirectory(folder);var path=Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N")+".csv");
            File.WriteAllText(path,csv.ToString(),new UTF8Encoding(true));Debug.Log("Exposure tasks saved: "+path);
        } catch(Exception e) {Debug.LogError("Exposure task results could not be saved: "+e.Message);}
    }
    void OnApplicationFocus(bool focus) {hasFocus=focus;if(!focus)ResetFocus();}
    void OnApplicationPause(bool paused) {applicationPaused=paused;ResetFocus();}
    void OnDestroy() {if(Active)End("interrupted");if(canvas)Destroy(canvas.gameObject);}
}

