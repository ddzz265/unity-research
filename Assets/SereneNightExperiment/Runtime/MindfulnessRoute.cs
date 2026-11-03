using System;
using UnityEngine;

// Move the tracking origin; the tracked head's local pose remains untouched.
public sealed class MindfulnessRoute : MonoBehaviour
{
    public ExperimentGroupController controller;
    public Transform trackingOrigin;
    public Vector3[] waypoints;
    const int Samples=2048;
    float[] lengths;
    bool running;
    public float Length=>lengths==null?0f:lengths[Samples];

    public bool Begin()
    {
        if(!trackingOrigin || waypoints==null || waypoints.Length<2) {
            Debug.LogError("Assign the tracking origin and at least two route points.",this);return false;
        }
        lengths=new float[Samples+1];
        Vector3 previous=Sample(0f);
        for(int i=1;i<=Samples;i++) {
            Vector3 next=Sample(i/(float)Samples);
            lengths[i]=lengths[i-1]+Vector3.Distance(previous,next);previous=next;
        }
        if(Length<=.01f) {Debug.LogError("Mindfulness route has no length.",this);return false;}
        running=true;Apply(0f);return true;
    }
    void LateUpdate()
    {
        if(running && controller && controller.MindfulnessAudioPlaying)Apply(controller.MindfulnessProgress);
    }
    public void Stop(bool completed)
    {
        if(completed && running)Apply(1f);
        running=false;
    }
    public Vector3 Evaluate(float audioProgress)
    {
        if(lengths==null)return waypoints[0];
        float t=Mathf.Clamp01(audioProgress);
        // Five-second speed ramps, with steady travel between them.
        float seconds=controller && controller.mindfulnessAudio && controller.mindfulnessAudio.clip?controller.mindfulnessAudio.clip.length:544.2612f;
        float ramp=Mathf.Min(.25f,5f/seconds);
        float edge=Mathf.Min(t,1f-t);
        float fraction=edge<ramp?.5f*(edge-ramp/Mathf.PI*Mathf.Sin(Mathf.PI*edge/ramp))/(1f-ramp):(t-ramp*.5f)/(1f-ramp);
        if(t>1f-ramp)fraction=1f-fraction;
        float distance=Length*fraction;
        int index=Array.BinarySearch(lengths,distance);
        if(index>=0)return Sample(index/(float)Samples);
        index=Mathf.Clamp(~index,1,Samples);
        float blend=Mathf.InverseLerp(lengths[index-1],lengths[index],distance);
        return Sample((index-1+blend)/Samples);
    }
    void Apply(float progress) {trackingOrigin.position=Evaluate(progress);}
    Vector3 Sample(float progress)
    {
        float u=Mathf.Clamp01(progress)*(waypoints.Length-1);
        int i=Mathf.Min((int)u,waypoints.Length-2);u-=i;
        Vector3 a=waypoints[Mathf.Max(0,i-1)],b=waypoints[i],c=waypoints[i+1],d=waypoints[Mathf.Min(waypoints.Length-1,i+2)];
        return .5f*(2*b+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u);
    }
}
