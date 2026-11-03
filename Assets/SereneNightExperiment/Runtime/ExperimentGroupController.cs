using System;
using System.Collections;
using System.Threading;
using UnityEngine;

public enum ExperimentGroup { A, B, C, D, E, F }
public enum ExperimentPhase { Ready=0, Mindfulness=1, Questionnaire=2, Completed=3, InvalidSetup=5, Transitioning=6, WaitingForInhale=7, Exposure=8, Aborted=9, Music=10 }

[DefaultExecutionOrder(-200)]
public sealed class ExperimentGroupController : MonoBehaviour
{
    public ExperimentGroup group=ExperimentGroup.A;
    public bool startOnPlay=true;
    public QuestionnaireView questionnaire;
    public AudioSource mindfulnessAudio;
    public ExposureSession exposureSession;
    public MusicForestSession musicSession;
    public MindfulnessRoute mindfulnessRoute;
    public Behaviour[] desktopControls=Array.Empty<Behaviour>();
    // Retain existing scene references solely to disable the old guidance objects.
    [HideInInspector] public GameObject breathingOrb;
    [HideInInspector] public AudioSource[] guidanceAudio=Array.Empty<AudioSource>();
    [HideInInspector] public AudioSource ambienceAudio;
    [SerializeField] ExperimentPhase phase=ExperimentPhase.Ready;
    QuestionnaireBackgroundBlur background;
    ExperimentGroup runningGroup;
    bool UsesInhale => runningGroup==ExperimentGroup.D || runningGroup==ExperimentGroup.E || runningGroup==ExperimentGroup.F;
    int pendingInhalation;
    Coroutine playbackRoutine;
    bool audioStarted, applicationPaused;
    double playbackEndsAt, pausedAt;
    public const float TransitionSeconds=3f;
    public ExperimentPhase Phase=>phase;
    public double TransitionedAt {get;private set;}
    public int TransitionFrame {get;private set;}=-1;
    public bool MindfulnessAudioPlaying=>phase==ExperimentPhase.Mindfulness && audioStarted && !applicationPaused && !AudioListener.pause && mindfulnessAudio && mindfulnessAudio.isPlaying;
    public float MindfulnessProgress=>!mindfulnessAudio || !mindfulnessAudio.clip?0f:
        Mathf.Clamp01((float)(1d-(playbackEndsAt-AudioSettings.dspTime)/((double)mindfulnessAudio.clip.samples/mindfulnessAudio.clip.frequency)));

    void Awake()
    {
        DisableOldGuidance();
        if(questionnaire) {questionnaire.showOnStart=false;questionnaire.Hide();}
    }
    void Start() {if(startOnPlay)StartExperiment();}
    [ContextMenu("Start Experiment (Play mode)")]
    public void StartExperiment()
    {
        if(!Application.isPlaying)return;
        if(mindfulnessRoute)mindfulnessRoute.Stop(false);
        if(exposureSession)exposureSession.Stop();
        if(musicSession)musicSession.Stop();
        if(playbackRoutine!=null) {StopCoroutine(playbackRoutine);playbackRoutine=null;}
        if(mindfulnessAudio)mindfulnessAudio.Stop();
        audioStarted=false;
        DisableOldGuidance();
        if(background)background.SetBlur(false);
        if(questionnaire) {questionnaire.showOnStart=false;questionnaire.Hide();}
        Interlocked.Exchange(ref pendingInhalation,0);
        TransitionedAt=0;TransitionFrame=-1;
        if(!questionnaire || !Enum.IsDefined(typeof(ExperimentGroup),group) || (!exposureSession && !musicSession && (!mindfulnessAudio || !mindfulnessAudio.clip))) {
            phase=ExperimentPhase.InvalidSetup;Debug.LogError("Assign a valid questionnaire, experiment group and mindfulness audio clip.",this);return;
        }
        runningGroup=group;
        if(!background)background=gameObject.AddComponent<QuestionnaireBackgroundBlur>();
        if(!background.Initialize(Camera.main)) {phase=ExperimentPhase.InvalidSetup;return;}
        questionnaire.gameObject.SetActive(true);
        questionnaire.Submitted-=OnQuestionnaireSubmitted;questionnaire.Submitted+=OnQuestionnaireSubmitted;
        questionnaire.useSettlingFeedback=runningGroup==ExperimentGroup.E;
        questionnaire.Prepare();
        questionnaire.SetRenderCamera(background.UICamera);
        if(!questionnaire.IsPrepared) {phase=ExperimentPhase.InvalidSetup;return;}
        foreach(var control in desktopControls)if(control)control.enabled=false;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        if(musicSession) {phase=musicSession.Begin()?ExperimentPhase.Music:ExperimentPhase.InvalidSetup;return;}
        if(exposureSession) {
            phase=exposureSession.Begin()?ExperimentPhase.Exposure:ExperimentPhase.InvalidSetup;
            return;
        }
        phase=ExperimentPhase.Mindfulness;
        playbackRoutine=StartCoroutine(PlayMindfulness());
    }
    IEnumerator PlayMindfulness()
    {
        var audio=mindfulnessAudio;
        audio.enabled=true;audio.playOnAwake=false;audio.loop=false;audio.pitch=1f;
        audio.spatialBlend=0f;audio.ignoreListenerPause=false;
        var clip=audio.clip;
        if(clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
        while(clip.loadState==AudioDataLoadState.Loading || applicationPaused)yield return null;
        if(clip.loadState!=AudioDataLoadState.Loaded) {
            phase=ExperimentPhase.InvalidSetup;Debug.LogError("Mindfulness audio could not be loaded; questionnaire remains hidden.",this);yield break;
        }
        audio.Play();
        playbackEndsAt=AudioSettings.dspTime+(double)clip.samples/clip.frequency;
        audioStarted=true;
        if(mindfulnessRoute && !mindfulnessRoute.Begin()) {
            audio.Stop();audioStarted=false;phase=ExperimentPhase.InvalidSetup;yield break;
        }
        Debug.Log($"Group {runningGroup}: mindfulness started ({clip.length:F3}s); questionnaire hidden.",this);
    }
    void FinishTherapy()
    {
        if(!exposureSession && !musicSession && mindfulnessRoute)mindfulnessRoute.Stop(true);
        audioStarted=false;
        // Discard every inhale recorded during therapy, including this frame.
        Interlocked.Exchange(ref pendingInhalation,0);
        if(UsesInhale) {
            phase=ExperimentPhase.WaitingForInhale;
            Debug.Log($"Group {runningGroup}: therapy ended; waiting for the next first inhalation.",this);
        } else ShowQuestionnaire();
    }
    void OnApplicationPause(bool paused)
    {
        if(applicationPaused==paused)return;
        applicationPaused=paused;
        if(musicSession)musicSession.SetPaused(paused);
        if(phase!=ExperimentPhase.Mindfulness || !audioStarted || !mindfulnessAudio)return;
        if(paused) {pausedAt=AudioSettings.dspTime;mindfulnessAudio.Pause();}
        else {playbackEndsAt+=AudioSettings.dspTime-pausedAt;mindfulnessAudio.UnPause();}
    }
    // A belt adapter may call this from any thread. Unity objects change on the next Update.
    // Report an inhale onset, not every sample above a threshold.
    [ContextMenu("Simulate First Inhalation (Play mode)")]
    public void NotifyInhalation() {Interlocked.Exchange(ref pendingInhalation,1);}
    void Update()
    {
        bool inhale=Interlocked.Exchange(ref pendingInhalation,0)!=0;
        if(phase==ExperimentPhase.Music) {
            if(!musicSession || !musicSession.isActiveAndEnabled) {phase=ExperimentPhase.InvalidSetup;return;}
            if(!applicationPaused && !AudioListener.pause && Time.timeScale>0) {
                if(musicSession.Advance(Time.unscaledDeltaTime))FinishTherapy();
                else if(musicSession.Failed)phase=ExperimentPhase.InvalidSetup;
            }
            return;
        }
        if(phase==ExperimentPhase.Exposure) {
            if(!exposureSession || !exposureSession.isActiveAndEnabled) {phase=ExperimentPhase.InvalidSetup;return;}
            if(!applicationPaused && !AudioListener.pause && Time.timeScale>0 && exposureSession.Advance(Time.unscaledDeltaTime))FinishTherapy();
            return;
        }
        if(phase==ExperimentPhase.Mindfulness) {
            if(!audioStarted || applicationPaused || AudioListener.pause)return;
            if(!mindfulnessAudio || !mindfulnessAudio.enabled || !mindfulnessAudio.gameObject.activeInHierarchy) {
                phase=ExperimentPhase.InvalidSetup;Debug.LogError("Mindfulness audio was disabled; questionnaire remains hidden.",this);return;
            }
            if(mindfulnessAudio.isPlaying)return;
            // A premature Stop or playback failure must not count as a completed therapy.
            AudioSettings.GetDSPBufferSize(out int bufferLength,out int bufferCount);
            double tolerance=Math.Max(.1,2.0*bufferLength*bufferCount/AudioSettings.outputSampleRate);
            if(AudioSettings.dspTime+tolerance<playbackEndsAt) {
                phase=ExperimentPhase.InvalidSetup;Debug.LogError("Mindfulness audio stopped before its end; questionnaire remains hidden.",this);return;
            }
            FinishTherapy();
            return;
        }
        if(phase==ExperimentPhase.WaitingForInhale && inhale)ShowQuestionnaire();
        if(phase!=ExperimentPhase.Transitioning)return;
        float progress=Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble-TransitionedAt)/TransitionSeconds));
        if(UsesInhale)questionnaire.SetExpansion(progress);
        else questionnaire.SetPresentation(progress);
        if(progress>=1f) {
            phase=ExperimentPhase.Questionnaire;
            Debug.Log($"Group {runningGroup}: 3-second transition complete; questionnaire interaction enabled.",this);
        }
    }
    void ShowQuestionnaire()
    {
        bool blur=runningGroup==ExperimentGroup.C || runningGroup==ExperimentGroup.F;
        background.SetBlur(blur);
        bool fade=runningGroup==ExperimentGroup.B || runningGroup==ExperimentGroup.C;
        if(!questionnaire.Show(fade?0f:1f,UsesInhale)) {phase=ExperimentPhase.InvalidSetup;return;}
        TransitionedAt=Time.realtimeSinceStartupAsDouble;TransitionFrame=Time.frameCount;
        phase=runningGroup==ExperimentGroup.A?ExperimentPhase.Questionnaire:ExperimentPhase.Transitioning;
        Debug.Log($"Group {runningGroup}: questionnaire {(runningGroup==ExperimentGroup.A?"shown immediately":UsesInhale?"expanding from a circle for 3s":"fading in for 3s")}; background {(blur?"blurred":"clear")}.",this);
    }
    void DisableOldGuidance()
    {
        if(breathingOrb)breathingOrb.SetActive(false);
        foreach(var audio in guidanceAudio)Silence(audio);
        if(!ambienceAudio)ambienceAudio=GameObject.Find("AudioManager")?.GetComponent<AudioSource>();
        Silence(ambienceAudio);
    }
    static void Silence(AudioSource audio)
    {
        if(!audio)return;
        audio.Stop();audio.playOnAwake=false;audio.enabled=false;
    }
    public void AbortExposure()
    {
        if(phase!=ExperimentPhase.Exposure)return;
        exposureSession.Stop();phase=ExperimentPhase.Aborted;
        Interlocked.Exchange(ref pendingInhalation,0);
        Debug.Log("Exposure aborted; not recorded as completed and questionnaire remains hidden.",this);
    }
    void OnQuestionnaireSubmitted() {if(phase==ExperimentPhase.Questionnaire)phase=ExperimentPhase.Completed;}
    void OnDisable() {if(exposureSession)exposureSession.Stop();
        if(musicSession)musicSession.Stop();if(mindfulnessRoute)mindfulnessRoute.Stop(false);if(mindfulnessAudio)mindfulnessAudio.Stop();audioStarted=false;}
    void OnDestroy() {if(questionnaire)questionnaire.Submitted-=OnQuestionnaireSubmitted;}
}
