using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.UI;

// A fixed 660-second guided route through the imported tower. Head pose is never overwritten.
public sealed class ExposureSession : MonoBehaviour
{
    public ExperimentGroupController controller;
    public Transform trackingOrigin;
    public Camera head;
    public Font font;
    public Vector3 ground, low, middle, high, bridgeEnd, summit;
    public const float Duration=660f;
    double elapsedSeconds;
    public float Elapsed=>(float)elapsedSeconds;
    public bool Paused {get;private set;}
    public bool Running {get;private set;}
    public int Stage=>Elapsed<60?0:Elapsed<180?1:Elapsed<360?2:Elapsed<540?3:4;
    Canvas guidance, curtain;
    Text label, pauseLabel;
    Image black;
    public ExposureTasks Tasks {get;private set;}
    public AcrophobiaVR.Navigation.TierManager Navigation {get;private set;}
    AcrophobiaVR.Navigation.ScreenFader originalFader;
    Vector3 headOffset;
    static readonly float[] Jumps={60,180,270,540};
    static readonly string[] Titles={"熟悉环境","低层高空适应","逐渐升高","玻璃桥挑战","高空体验"};
    static readonly string[] Instructions={"观察周围，熟悉塔楼环境。","保持自然呼吸，观察平台及远处。","即将分段升高，请保持舒适姿势。","沿玻璃桥缓慢前行，可自由转头观察。","观察高空环境，体验即将结束。"};

    public bool Begin()
    {
        if(!trackingOrigin || !head || !font || !controller)return false;
        elapsedSeconds=0;Paused=false;Running=true;
        headOffset=head.transform.position-trackingOrigin.position;
        headOffset.y=0;
        if(!guidance)BuildUI();
        guidance.gameObject.SetActive(true);curtain.gameObject.SetActive(true);
        Apply();
        if(!Tasks)Tasks=gameObject.AddComponent<ExposureTasks>();
        Tasks.Begin(this);return true;
    }
    public bool Advance(float seconds)
    {
        if(!Running || Paused)return false;
        if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)return false;
        elapsedSeconds=System.Math.Min(Duration,elapsedSeconds+seconds);Apply();
        if(elapsedSeconds<Duration)return false;
        Stop();return true;
    }
    public Vector3 PositionAt(float seconds)
    {
        if(seconds<60)return ground;
        if(seconds<180)return low;
        if(seconds<270)return middle;
        if(seconds<360)return high;
        if(seconds<540)return Vector3.Lerp(high,bridgeEnd,Mathf.SmoothStep(0,1,(seconds-360)/180f));
        return summit;
    }
    void Apply()
    {
        int tier=Elapsed<60?-1:Elapsed<180?0:Elapsed<270?1:Elapsed<540?2:3;
        if(tier<0 || (Elapsed>=360 && Elapsed<540))trackingOrigin.position=PositionAt(Elapsed)-headOffset;
        if(tier>=0 && Navigation.CurrentTierIndex!=tier)Navigation.GoToTier(tier);
        float opacity=0;
        foreach(float t in Jumps)opacity=Mathf.Max(opacity,Mathf.Clamp01(1-Mathf.Abs(Elapsed-t)/.5f));
        originalFader.ApplyOpacity(opacity);
        if(Tasks)Tasks.SyncStage();
        label.text=$"{Titles[Stage]}  ·  {Mathf.FloorToInt(Elapsed/60):00}:{Mathf.FloorToInt(Elapsed%60):00} / 11:00\n"+
            (Elapsed>=640?"体验即将结束，随后进入本组问卷流程。":Tasks?Tasks.Status:Instructions[Stage]);
        pauseLabel.text=Paused?"继续":"暂停";
    }
    [ContextMenu("Pause or Resume Exposure")]
    public void TogglePause() {if(!Running)return;Paused=!Paused;Apply();}
    [ContextMenu("Abort Exposure")]
    public void Abort()
    {
        if(!Running)return;
        Navigation.Forfeit();
    }
    public void Stop()
    {
        if(Tasks)Tasks.End(Elapsed>=Duration?"completed":"interrupted");
        Running=false;Paused=false;
        if(guidance)guidance.gameObject.SetActive(false);
        if(curtain)curtain.gameObject.SetActive(false);
    }
    void Update()
    {
        if(Running && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)Abort();
    }
    void LateUpdate()
    {
        if(!Running || !guidance)return;
        guidance.transform.SetPositionAndRotation(head.transform.position+head.transform.forward*2-head.transform.up*.48f,head.transform.rotation);
    }
    Canvas MakeCanvas(string name)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Canvas));go.layer=5;
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
        canvas.worldCamera=controller.GetComponent<QuestionnaireBackgroundBlur>().UICamera;
        go.transform.localScale=Vector3.one*.002f;
        return canvas;
    }
    void BuildUI()
    {
        guidance=MakeCanvas("Exposure Guidance");guidance.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        ((RectTransform)guidance.transform).sizeDelta=new Vector2(650,150);
        var bg=guidance.gameObject.AddComponent<Image>();bg.color=new Color(.025f,.04f,.06f,.88f);
        label=TextAt(guidance.transform,new Vector2(0,25),new Vector2(630,85),22);
        pauseLabel=ButtonAt("暂停",-90,TogglePause);
        var exitButton=ButtonAt("结束体验",90,()=>{}).transform.parent.GetComponent<Button>();
        curtain=MakeCanvas("Exposure Transfer Fade");curtain.sortingOrder=100;
        curtain.transform.SetParent(head.transform,false);curtain.transform.localPosition=new Vector3(0,0,.12f);
        ((RectTransform)curtain.transform).sizeDelta=new Vector2(2000,2000);
        black=curtain.gameObject.AddComponent<Image>();black.raycastTarget=false;
        originalFader=curtain.gameObject.AddComponent<AcrophobiaVR.Navigation.ScreenFader>();originalFader.fadeImage=black;
        Navigation=gameObject.AddComponent<AcrophobiaVR.Navigation.TierManager>();
        Navigation.xrOrigin=trackingOrigin;Navigation.xrCamera=head.transform;
        Navigation.onTierChanged=new UnityEngine.Events.UnityEvent<int>();
        Navigation.onForfeit=new UnityEngine.Events.UnityEvent();
        Navigation.onForfeit.AddListener(()=>{if(Tasks)Tasks.End("aborted");controller.AbortExposure();});
        // A single clock owns fading; source TierManager owns tier placement and safe exit.
        Navigation.screenFader=null;
        foreach(var point in new[]{low,middle,high,summit}) {
            var anchor=new GameObject("Original Tier Anchor").transform;anchor.SetParent(transform,false);anchor.position=point;
            Navigation.tierAnchors.Add(anchor);
        }
        var safe=new GameObject("Original Safe Anchor").transform;safe.SetParent(transform,false);safe.position=ground;Navigation.safeRoomAnchor=safe;
        var panel=guidance.gameObject.AddComponent<AcrophobiaVR.UI.TowerUIPanel>();panel.enabled=false;
        panel.tierManager=Navigation;panel.forfeitButton=exitButton;
        Navigation.ConfigurePanels(panel);panel.enabled=true;
    }
    Text TextAt(Transform parent,Vector2 position,Vector2 size,int fontSize)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.layer=5;go.transform.SetParent(parent,false);
        var rt=(RectTransform)go.transform;rt.sizeDelta=size;rt.anchoredPosition=position;
        var text=go.GetComponent<Text>();text.font=font;text.fontSize=fontSize;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;
        return text;
    }
    Text ButtonAt(string title,float x,UnityEngine.Events.UnityAction action)
    {
        var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button));go.layer=5;go.transform.SetParent(guidance.transform,false);
        var rt=(RectTransform)go.transform;rt.sizeDelta=new Vector2(150,42);rt.anchoredPosition=new Vector2(x,-48);
        go.GetComponent<Image>().color=new Color(.14f,.23f,.28f,1);
        go.GetComponent<Button>().onClick.AddListener(action);
        var text=TextAt(go.transform,Vector2.zero,new Vector2(150,42),20);text.text=title;return text;
    }
    void OnDestroy()
    {
        if(guidance)Destroy(guidance.gameObject);
        if(curtain)Destroy(curtain.gameObject);
    }
}
