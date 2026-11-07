using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

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
    readonly List<AcrophobiaVR.UI.TowerUIPanel> floorPanels = new();
    TMP_FontAsset floorFont;
    Image black;
    public ExposureTasks Tasks {get;private set;}
    public AcrophobiaVR.Navigation.TierManager Navigation {get;private set;}
    AcrophobiaVR.Navigation.ScreenFader originalFader;
    Vector3 headOffset;
    static readonly float[] Jumps={60,180,270,540};


    public bool Begin()
    {
        if(!trackingOrigin || !head || !font || !controller)return false;
        elapsedSeconds=0;Paused=false;Running=true;
        headOffset=head.transform.position-trackingOrigin.position;
        headOffset.y=0;
        if(!curtain)BuildUI();
        foreach(var panel in floorPanels)panel.gameObject.SetActive(false);
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
        guidance.gameObject.SetActive(tier<0);
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
        foreach(var panel in floorPanels)panel.gameObject.SetActive(false);
        if(curtain)curtain.gameObject.SetActive(false);
    }
    void Update()
    {
        if(Running && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)Abort();
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
        floorFont=TMP_FontAsset.CreateFontAsset(font);
        floorFont.TryAddCharacters("地面层第共顶（）0123456789 /",out _);
        // Original TierUIBuilder layout: fixed beside each anchor, 400 x 220 at 0.0015 scale.
        guidance=BuildFloorPanel(safe,"地面层",false).GetComponent<Canvas>();
        foreach(var anchor in Navigation.tierAnchors)
            floorPanels.Add(BuildFloorPanel(anchor,"",true).GetComponent<AcrophobiaVR.UI.TowerUIPanel>());
        Navigation.ConfigurePanels(floorPanels);
    }
    GameObject BuildFloorPanel(Transform anchor,string title,bool tier)
    {
        var canvas=MakeCanvas(tier?"TierUIPanel_"+floorPanels.Count:"GroundLevelPanel");
        canvas.transform.SetParent(anchor,false);
        canvas.transform.localPosition=new Vector3(.9f,1.2f,0);
        canvas.transform.localRotation=Quaternion.Euler(0,180,0);
        canvas.transform.localScale=Vector3.one*.0015f;
        ((RectTransform)canvas.transform).sizeDelta=new Vector2(400,220);
        canvas.gameObject.AddComponent<CanvasScaler>();
        var bg=canvas.gameObject.AddComponent<Image>();bg.color=new Color(.05f,.05f,.05f,.85f);bg.raycastTarget=false;
        var go=new GameObject("TierLabel",typeof(RectTransform));go.layer=5;go.transform.SetParent(canvas.transform,false);
        var rt=(RectTransform)go.transform;rt.anchoredPosition=new Vector2(0,70);rt.sizeDelta=new Vector2(360,50);
        var label=go.AddComponent<TextMeshProUGUI>();label.font=floorFont;label.text=title;label.fontSize=28;
        label.alignment=TextAlignmentOptions.Center;label.color=Color.white;label.raycastTarget=false;
        if(tier) {
            canvas.gameObject.SetActive(false);
            var panel=canvas.gameObject.AddComponent<AcrophobiaVR.UI.TowerUIPanel>();
            panel.tierManager=Navigation;panel.tierLabel=label;
        }
        return canvas.gameObject;
    }

    void OnDestroy()
    {
        if(guidance)Destroy(guidance.gameObject);
        foreach(var panel in floorPanels)if(panel)Destroy(panel.gameObject);
        if(curtain)Destroy(curtain.gameObject);
        if(floorFont) {
            foreach(var texture in floorFont.atlasTextures)if(texture)Destroy(texture);
            if(floorFont.material)Destroy(floorFont.material);Destroy(floorFont);
        }
    }
}
