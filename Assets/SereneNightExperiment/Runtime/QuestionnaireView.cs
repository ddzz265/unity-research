using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class QuestionnaireView : MonoBehaviour
{
    public QuestionnaireContent[] questionnaires;
    public int selectedQuestionnaire;
    public Font font;
    public bool showOnStart = true;
    [NonSerialized] public bool useSettlingFeedback;
    public bool IsPrepared => panel != null;
    public bool IsVisible => panel && panel.gameObject.activeInHierarchy;
    public event Action Submitted;
    int index;
    bool submitted, showingEvaluationNotice;
    readonly List<HashSet<int>> choices = new();
    float[] scores;
    bool[] answered, skipped;
    RectTransform panel, content;
    CanvasGroup presentation;
    RoundedPanel surface;
    Mask unfoldingMask;
    public Vector2 PanelSize => panel ? panel.sizeDelta : Vector2.zero;
    public float PresentationAlpha => presentation ? presentation.alpha : 0f;
    Text status;
    QuestionnaireContent Content => questionnaires[selectedQuestionnaire];
    public static string ResponseDirectory {
        get {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Responses"));
#else
            return Path.Combine(Application.persistentDataPath, "Responses");
#endif
        }
    }
    IEnumerator Start()
    {
        if(!showOnStart)yield break;
        Prepare();
        if(!panel)yield break;
#if (UNITY_VISIONOS || UNITY_ANDROID) && !UNITY_EDITOR
        // Wait for a tracked camera before placing the panel in the world.
        while(true) {
            var head=UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            if(head.isValid && (!head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool tracked) || tracked))break;
            yield return null;
        }
#endif
        yield return null;
        Show();
    }
    public void Prepare() { Begin(); SetExpansion(1f); SetPresentation(1f); Canvas.ForceUpdateCanvases(); Hide(); }
    public void Hide() { if(panel)panel.gameObject.SetActive(false); }
    public void SetRenderCamera(Camera camera) {if(panel)panel.GetComponent<Canvas>().worldCamera=camera;}
    // Synchronous display for experimental T0: no extra frame, fade or recenter loop.
    public bool Show(float alpha = 1f, bool unfold = false)
    {
        if(!panel)Begin();
        var camera=Camera.main;
        if(!panel || !camera) {Debug.LogError("A main camera and valid questionnaire are required.");return false;}
        panel.SetParent(null,true);
        panel.SetPositionAndRotation(camera.transform.position+camera.transform.forward*2,
            Quaternion.LookRotation(camera.transform.forward,camera.transform.up));
        SetPresentation(alpha);
        SetExpansion(unfold?0f:1f);
        panel.gameObject.SetActive(true);
        return true;
    }
    public void SetPresentation(float alpha)
    {
        if(!presentation)return;
        presentation.alpha=Mathf.Clamp01(alpha);
        presentation.interactable=presentation.blocksRaycasts=presentation.alpha>=1f;
    }
    public void SetExpansion(float progress)
    {
        if(!panel)return;
        progress=Mathf.Clamp01(progress);
        // Match D_group_BMAH_inhale_demo.html: 1s circular growth + 2s morph,
        // with continuous velocity at the join. Keep the existing final template.
        const float referenceScale=620f/480f;
        float start=100f*referenceScale, grown=220f*referenceScale;
        float elapsed=progress*3f;
        float velocity=Mathf.Min(grown-start,(620f-grown)/2f);
        panel.sizeDelta=new Vector2(JoinedGrowth(elapsed,start,grown,1100f,velocity),
            JoinedGrowth(elapsed,start,grown,620f,velocity));
        surface.radius=Mathf.Lerp(Mathf.Min(panel.sizeDelta.x,panel.sizeDelta.y)/2f,28f,
            SmootherStep((elapsed-1f)/2f));
        var color=surface.color;
        color.a=JoinedGrowth(elapsed,.30f,.42f,1f,.12f);
        surface.color=color;
        surface.SetVerticesDirty();
        // Content stays at its final size and position; only the surrounding paper grows.
        foreach(RectTransform child in content) {
            var reveal=child.GetComponent<CanvasGroup>();
            if(!reveal)continue;
            float top=-child.anchoredPosition.y;
            float edge=Mathf.Max(Mathf.Abs(top-310f),Mathf.Abs(top+child.sizeDelta.y-310f));
            reveal.alpha=progress>=1f?1f:
                Mathf.Clamp01((panel.sizeDelta.y/2f-edge-2f*referenceScale)/(18f*referenceScale))*
                Mathf.Clamp01((panel.sizeDelta.x-1004f-8f*referenceScale)/(32f*referenceScale));
        }
        unfoldingMask.enabled=progress<1f;
        presentation.interactable=presentation.blocksRaycasts=progress>=1f && presentation.alpha>=1f;
    }
    static float SmootherStep(float t)
    {
        t=Mathf.Clamp01(t);
        return t*t*t*(t*(t*6f-15f)+10f);
    }
    static float JoinedGrowth(float elapsed,float first,float middle,float last,float velocity)
    {
        bool growing=elapsed<=1f;
        float duration=growing?1f:2f;
        float t=Mathf.Clamp01((elapsed-(growing?0f:1f))/duration);
        float t2=t*t,t3=t2*t,t4=t3*t,t5=t4*t;
        float tangent=growing?-4f*t3+7f*t4-3f*t5:t-6f*t3+8f*t4-3f*t5;
        return Mathf.Lerp(growing?first:middle,growing?middle:last,SmootherStep(t))+velocity*duration*tangent;
    }
    void OnDestroy() { if(panel)Destroy(panel.gameObject); }
    public void Begin()
    {
        if (questionnaires == null || questionnaires.Length == 0 || Content == null || Content.questionnaireQuestions.Count == 0) {
            Debug.LogError("Assign a nonempty QuestionnaireContent in the Inspector."); return;
        }
        choices.Clear();
        foreach (var q in Content.questionnaireQuestions) choices.Add(new HashSet<int>());
        scores = Content.questionnaireQuestions.Select(q => q.minScore).ToArray();
        answered = new bool[scores.Length]; skipped = new bool[scores.Length];
        index = 0; submitted = false; showingEvaluationNotice = false;
        if (!panel) {
            // Mouse pointers and XR tracked pointers require separate raycasters.
            var go = new GameObject("Questionnaire Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            go.layer=LayerMask.NameToLayer("UI");
            presentation=go.AddComponent<CanvasGroup>();
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main;
            panel = go.GetComponent<RectTransform>(); panel.sizeDelta = new Vector2(1100, 620);
            panel.localScale = Vector3.one * .0016f;
            surface=go.AddComponent<RoundedPanel>();surface.radius=28;
            surface.color = new Color(.96f, .975f, .93f, 1f);
            unfoldingMask=go.AddComponent<Mask>();unfoldingMask.showMaskGraphic=true;unfoldingMask.enabled=false;
            content=new GameObject("Questionnaire Content",typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(panel,false);content.gameObject.layer=go.layer;
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(.5f,.5f);
            content.sizeDelta=new Vector2(1100,620);
        }
        Draw();
    }
    RectTransform Box(string name, Transform parent, float x, float y, float w, float h)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0,1);
        rt.gameObject.layer=parent.gameObject.layer;
        if(parent==content)rt.gameObject.AddComponent<CanvasGroup>();
        rt.anchoredPosition = new Vector2(x,-y); rt.sizeDelta = new Vector2(w,h); return rt;
    }
    Text Label(string text, float x,float y,float w,float h,int size = 26, Transform parent = null)
    {
        var t = Box("Label", parent ? parent : content, x,y,w,h).gameObject.AddComponent<Text>();
        t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.text = text;
        t.fontSize = size; t.color = new Color(.16f,.3f,.25f); t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; return t;
    }
    Button Button(string text,float x,float y,float w,float h,Action action,bool selected=false,bool centered=false,bool settling=false)
    {
        var rt=Box("Button "+text,content,x,y,w,h);var image=rt.gameObject.AddComponent<RoundedPanel>();
        image.color=selected && !settling?new Color(.25f,.46f,.36f,1):new Color(.98f,.985f,.95f,1f);
        var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=image;var colors=b.colors;
        colors.highlightedColor=new Color(.8f,.93f,.84f);colors.pressedColor=new Color(.6f,.79f,.67f);
        colors.selectedColor=Color.white;b.colors=colors;
        if(settling) {
            b.transition=Selectable.Transition.None;
            rt.anchoredPosition3D=new Vector3(x,-y-(selected?6f:0f),selected?0f:-6f);
        }
        var label=Label(text,centered?0:18,0,centered?w:w-36,h,centered?34:24,rt);
        if(centered || text=="上一题" || text=="下一题")label.alignment=TextAnchor.MiddleCenter;
        if(selected && !settling)label.color=new Color(.98f,1,.96f);
        b.onClick.AddListener(()=>action());return b;
    }
    void Draw()
    {
        foreach(Transform child in content) {child.gameObject.SetActive(false);Destroy(child.gameObject);}
        if(submitted) {Label("已提交，感谢你的回答。",48,200,1004,100,38).alignment=TextAnchor.MiddleCenter;return;}
        if(showingEvaluationNotice) {
            Label("恭喜你完成了问卷，请结合刚刚你对问卷出现以及填写问卷的感受进行问卷评估。",
                90,185,920,250,34).alignment=TextAnchor.MiddleCenter;
            Button("继续",822,536,230,54,()=>{showingEvaluationNotice=false;index++;Draw();});
            return;
        }
        Label($"第 {index+1} / {scores.Length} 题",48,82,1004,36,22);
        var q=Content.questionnaireQuestions[index];
        var question=Label(q.question,48,134,1004,128,34);
        question.alignment=TextAnchor.MiddleCenter;
        question.resizeTextForBestFit=true;question.resizeTextMinSize=24;question.resizeTextMaxSize=34;
        if(q.choicesType==ChoicesType.Single || q.choicesType==ChoicesType.Multiple) {
            bool seven=q.choicesType==ChoicesType.Single && q.choiceLabels.SequenceEqual(Enumerable.Range(1,7).Select(n=>n.ToString()));
            float row=Mathf.Min(62,240f/Mathf.Max(1,q.choiceLabels.Count));
            for(int i=0;i<q.choiceLabels.Count;i++) {
                int option=i;bool on=choices[index].Contains(i);
                bool settling=useSettlingFeedback && seven;
                float width=seven?(1004-6*14)/7f:1004;
                Button(seven?q.choiceLabels[i]:(on?"[x] ":"[ ] ")+q.choiceLabels[i],
                    seven?48+i*(width+14):48,seven?286:260+i*row,width,seven?88:row-6,()=>{
                        if(settling && on)return;
                        if(q.choicesType==ChoicesType.Single)choices[index].Clear();
                        if(on && q.choicesType==ChoicesType.Multiple)choices[index].Remove(option);else choices[index].Add(option);
                        answered[index]=choices[index].Count>0;skipped[index]=false;Draw();
                        if(settling) {
                            var selected=content.Cast<Transform>().Last(t=>t.gameObject.activeSelf && t.name=="Button "+q.choiceLabels[option]);
                            StartCoroutine(SettleChoice((RectTransform)selected));
                        }
                    },on,seven,settling);
            }
            if(seven) {
                Label(q.labelForMinScore,48,391,490,48,22);
                Label(q.labelForMaxScore,562,391,490,48,22).alignment=TextAnchor.MiddleRight;
            }
        } else if(q.choicesType==ChoicesType.Slider) {
            Label(q.labelForMinScore,48,260,490,48,22);
            Label(q.labelForMaxScore,562,260,490,48,22).alignment=TextAnchor.MiddleRight;
            var rt=Box("Slider",content,68,320,964,64);var slider=rt.gameObject.AddComponent<Slider>();
            var bg=Box("Track",rt,0,26,964,12).gameObject.AddComponent<Image>();bg.color=new Color(.49f,.62f,.53f);
            var area=Box("Handle Area",rt,16,0,932,64);var handle=Box("Handle",area,0,0,32,64);
            var image=handle.gameObject.AddComponent<RoundedPanel>();image.color=new Color(.25f,.46f,.36f);
            slider.handleRect=handle;slider.targetGraphic=image;slider.direction=Slider.Direction.LeftToRight;
            slider.minValue=q.minScore;slider.maxValue=q.maxScore;slider.wholeNumbers=q.wholeNumber;slider.SetValueWithoutNotify(scores[index]);
            var value=Label(answered[index]?scores[index].ToString("0.##"):"请调整滑条或确认当前数值",48,395,650,48,25);
            slider.onValueChanged.AddListener(v=>{scores[index]=v;answered[index]=true;skipped[index]=false;value.text=v.ToString("0.##");});
            Button("确认当前数值",760,395,292,50,()=>{answered[index]=true;skipped[index]=false;value.text=scores[index].ToString("0.##");});
        }
        status=Label(answered[index]?"已选择，可以继续。":"请选择后继续。",48,464,1004,36,21);
        var prev=Button("上一题",48,536,230,54,()=>{index--;Draw();});prev.interactable=index>0;
        if(q.canSkipQuestion)Button("跳过",435,536,230,54,()=>{skipped[index]=true;Advance();});
        Button(index==scores.Length-1?"提交":"下一题",822,536,230,54,()=>{
            if(!answered[index]) {status.text="请先选择一个答案。";return;}
            skipped[index]=false;Advance();
        });
    }
    IEnumerator SettleChoice(RectTransform choice)
    {
        Vector3 end=choice.anchoredPosition3D, start=end+new Vector3(0,6f,-6f);
        double began=Time.realtimeSinceStartupAsDouble;
        while(choice && choice.gameObject.activeSelf) {
            float time=Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble-began)/.45));
            choice.anchoredPosition3D=Vector3.LerpUnclamped(start,end,SettlingEase(time));
            if(time>=1f)yield break;
            yield return null;
        }
    }
    // CSS cubic-bezier(.22,.61,.36,1), inverted on x to match the reference timing.
    static float SettlingEase(float time)
    {
        if(time<=0f || time>=1f)return time;
        float low=0f,high=1f;
        for(int i=0;i<16;i++) {
            float t=(low+high)/2f,s=1f-t;
            float x=3f*s*s*t*.22f+3f*s*t*t*.36f+t*t*t;
            if(x<time)low=t;else high=t;
        }
        float u=(low+high)/2f,v=1f-u;
        return 3f*v*v*u*.61f+3f*v*u*u+u*u*u;
    }
    void Advance()
    {
        if(index==scores.Length-1)Submit();
        else if(index==1 && Content.showEvaluationNoticeAfterQuestionTwo) {showingEvaluationNotice=true;Draw();}
        else {index++;Draw();}
    }
    static string Csv(string v)=>"\""+(v??"").Replace("\"","\"\"")+"\"";
    void Submit()
    {
        var csv=new StringBuilder("question,answer,skipped\r\n");
        for(int i=0;i<scores.Length;i++) {
            var q=Content.questionnaireQuestions[i];
            string answer=skipped[i]?"":q.choicesType==ChoicesType.Slider?scores[i].ToString(System.Globalization.CultureInfo.InvariantCulture):string.Join("; ",choices[i].OrderBy(x=>x).Select(x=>q.choiceLabels[x]));
            csv.AppendLine($"{Csv(q.question)},{Csv(answer)},{skipped[i]}");
        }
        try {
            Directory.CreateDirectory(ResponseDirectory);
            string path=Path.Combine(ResponseDirectory,Content.name+"_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N")+".csv");
            File.WriteAllText(path,csv.ToString(),new UTF8Encoding(true)); Debug.Log("Questionnaire saved: "+path);
            submitted=true; Draw();Submitted?.Invoke();
        } catch(Exception e) { status.text="保存失败，答案已保留，请再次提交。";Debug.LogException(e); }
    }
}
