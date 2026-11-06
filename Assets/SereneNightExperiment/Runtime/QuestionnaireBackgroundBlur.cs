using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Render world-space UI after the forest's post-processing, so C blurs only the background.
public sealed class QuestionnaireBackgroundBlur : MonoBehaviour
{
    Camera worldCamera, uiCamera;
    UniversalAdditionalCameraData worldData;
    int savedMask;
    Volume volume;
    VolumeProfile profile;
    DepthOfField depthOfField;
    public Camera UICamera=>uiCamera;
    public bool IsBlurred => depthOfField && depthOfField.mode.value!=DepthOfFieldMode.Off;

    public bool Initialize(Camera camera)
    {
        if(worldCamera)return true;
        if(!camera || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) {
            Debug.LogError("The experiment requires its Main Camera and Universal Render Pipeline.",this);
            return false;
        }
        worldData=camera.GetUniversalAdditionalCameraData();
        var stack=worldData.cameraStack;
        if(stack==null) {Debug.LogError("The forest renderer must support camera stacking.",this);return false;}
        worldCamera=camera;savedMask=camera.cullingMask;
        worldData.renderPostProcessing=true;worldData.requiresDepthTexture=true;
#if UNITY_ANDROID && !UNITY_EDITOR
        // Keep expensive world passes off during playback; enable only for C/F blur.
        worldData.antialiasing=AntialiasingMode.None;
        worldData.requiresColorTexture=false;
        camera.allowHDR=false;
#endif
        worldData.volumeLayerMask|=1;
        var go=new GameObject("Questionnaire UI Camera");
        go.transform.SetParent(camera.transform,false);
        uiCamera=go.AddComponent<Camera>();uiCamera.CopyFrom(camera);
        uiCamera.cullingMask=1<<LayerMask.NameToLayer("UI");
        var uiData=uiCamera.GetUniversalAdditionalCameraData();
        uiData.renderType=CameraRenderType.Overlay;
        uiData.renderPostProcessing=false;uiData.renderShadows=false;
        uiData.requiresDepthTexture=false;uiData.requiresColorTexture=false;
        uiData.antialiasing=AntialiasingMode.None;
        camera.cullingMask=savedMask & ~uiCamera.cullingMask;
        stack.Add(uiCamera);

        // A dedicated override changes only depth of field; existing forest grading stays intact.
        var blur=new GameObject("Experiment Background Blur");blur.transform.SetParent(transform,false);
        volume=blur.AddComponent<Volume>();volume.isGlobal=true;volume.priority=1000;volume.weight=1;
        // A Resources profile also preserves the Gaussian shader variant in player builds.
        volume.sharedProfile=Resources.Load<VolumeProfile>("ExperimentBackgroundBlur");
        profile=volume.profile;
        if(!profile || !profile.TryGet(out depthOfField)) {
            Debug.LogError("The ExperimentBackgroundBlur Resources profile is missing.",this);return false;
        }
        SetBlur(false);
        return true;
    }
    public void SetBlur(bool enabled)
    {
        if(depthOfField)depthOfField.mode.value=enabled?DepthOfFieldMode.Gaussian:DepthOfFieldMode.Off;
#if UNITY_ANDROID && !UNITY_EDITOR
        if(worldData) {worldData.renderPostProcessing=enabled;worldData.requiresDepthTexture=enabled;}
#endif
    }
    void LateUpdate()
    {
        if(!uiCamera || !worldCamera)return;
        uiCamera.fieldOfView=worldCamera.fieldOfView;
        uiCamera.nearClipPlane=worldCamera.nearClipPlane;uiCamera.farClipPlane=worldCamera.farClipPlane;
    }
    void OnDestroy()
    {
        if(worldCamera)worldCamera.cullingMask=savedMask;
        if(worldData && uiCamera)worldData.cameraStack?.Remove(uiCamera);
        if(uiCamera)Destroy(uiCamera.gameObject);
        if(volume)Destroy(volume.gameObject);
        if(profile) {
            foreach(var component in profile.components)if(component)Destroy(component);
            Destroy(profile);
        }
    }
}
