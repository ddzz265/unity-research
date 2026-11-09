using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public sealed class MusicForestSession : MonoBehaviour
{
    public double Duration=>music && music.clip?(double)music.clip.samples/music.clip.frequency:0;
    public AudioSource music;
    public Transform trackingOrigin;
    public Vector3[] route;
    public Font font;
    public bool Running {get;private set;}
    public double Elapsed {get;private set;}
    public bool Failed {get;private set;}
    bool paused;
    float[] routeLengths;
    GameObject notice;
    public bool Begin()
    {
        Stop();Elapsed=0;Failed=false;
        if(!music || !music.clip || !trackingOrigin || route==null || route.Length<2) {
            Failed=true;ShowMissingMusic();Debug.LogError("Music forest requires the specified Pixabay MP3 and a valid route.",this);return false;
        }
        if(notice)notice.SetActive(false);
        music.enabled=true;music.loop=false;music.playOnAwake=false;music.pitch=1;music.spatialBlend=0;music.ignoreListenerPause=false;music.volume=1;
        routeLengths=new float[route.Length];
        for(int i=1;i<route.Length;i++)routeLengths[i]=routeLengths[i-1]+Vector3.Distance(route[i-1],route[i]);
        music.Play();Running=true;paused=false;Apply();return true;
    }
    public bool Advance(double seconds)
    {
        if(!Running || paused || seconds<0 || double.IsNaN(seconds) || double.IsInfinity(seconds))return false;
        if(!music || !music.isActiveAndEnabled) {
            Failed=true;Stop();Debug.LogError("Music playback stopped unexpectedly; session not completed.",this);return false;
        }
        if(!music.isPlaying) {
            if(Elapsed+.15>=Duration) {Elapsed=Duration;Apply();Stop();return true;}
            Failed=true;Stop();Debug.LogError("Music playback stopped before its end; session not completed.",this);return false;
        }
        Elapsed=Math.Min(Duration,Elapsed+seconds);Apply();
        // The waveform's natural playback end triggers the questionnaire, even at a low frame rate.
        return false;
    }
    void Apply()
    {
        // Slow out-and-back route: no forced head rotation and no jump at the turnaround.
        float u=(float)(Elapsed/Duration);float distance=(1-Mathf.Cos(u*2*Mathf.PI))*.5f;
        float travelled=distance*routeLengths[routeLengths.Length-1];
        int next=Array.BinarySearch(routeLengths,travelled);if(next<0)next=~next;
        next=Mathf.Clamp(next,1,route.Length-1);
        trackingOrigin.position=Vector3.Lerp(route[next-1],route[next],Mathf.InverseLerp(routeLengths[next-1],routeLengths[next],travelled));
    }
    public void SetPaused(bool value)
    {
        if(paused==value)return;paused=value;
        if(!Running || !music)return;
        if(value)music.Pause();else music.UnPause();
    }
    public void Stop() {Running=false;if(music)music.Stop();}
    void ShowMissingMusic()
    {
        if(notice) {notice.SetActive(true);return;}
        var camera=Camera.main;if(!camera)return;
        notice=new GameObject("Music setup notice",typeof(RectTransform),typeof(Canvas));notice.layer=5;
        var canvas=notice.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=GetComponent<QuestionnaireBackgroundBlur>()?.UICamera;
        notice.transform.SetParent(camera.transform,false);notice.transform.localPosition=new Vector3(0,0,2);notice.transform.localScale=Vector3.one*.002f;
        var go=new GameObject("Notice",typeof(RectTransform),typeof(Text));go.layer=5;go.transform.SetParent(notice.transform,false);
        ((RectTransform)go.transform).sizeDelta=new Vector2(750,160);
        var text=go.GetComponent<Text>();text.font=font;text.fontSize=28;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;
        text.text="指定背景音乐尚未导入\n请提供 Pixabay 原曲 MP3 后开始体验";
    }
    void OnDisable() {Stop();}
    void Update()
    {
#if !UNITY_VISIONOS || UNITY_EDITOR
        if(Running && !paused && !UnityEngine.XR.XRSettings.isDeviceActive && Mouse.current!=null && Mouse.current.rightButton.isPressed && Camera.main) {
            var delta=Mouse.current.delta.ReadValue();var head=Camera.main.transform;var angles=head.localEulerAngles;
            head.localRotation=Quaternion.Euler(Mathf.Clamp(Mathf.DeltaAngle(0,angles.x)-delta.y*.12f,-70,70),angles.y+delta.x*.12f,0);
        }
#endif
    }
    void OnDestroy() {if(notice)Destroy(notice);}
}
