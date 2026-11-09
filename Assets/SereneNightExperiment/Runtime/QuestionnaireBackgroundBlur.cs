using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Render world-space UI after the forest's post-processing, so C blurs only the background.
public sealed class QuestionnaireBackgroundBlur : MonoBehaviour
{
    Camera worldCamera, uiCamera;
    UniversalAdditionalCameraData worldData;
    int savedMask, savedVolumeMask;
    bool savedPostProcessing, savedDepthTexture, uiRequested;
    SpatialControllerRays controllerRays;
    float blurEnd, clearEnd;
    double blurStarted;
    bool transitioning;
    const float BlurSeconds=3f;
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
        savedPostProcessing=worldData.renderPostProcessing;
        savedDepthTexture=worldData.requiresDepthTexture;
        savedVolumeMask=worldData.volumeLayerMask;
        controllerRays=FindAnyObjectByType<SpatialControllerRays>();
        worldData.renderPostProcessing=true;worldData.requiresDepthTexture=false;
#if UNITY_ANDROID && !UNITY_EDITOR
        // Keep grading constant; only the depth copy and Gaussian pass follow C/F blur.
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
        blurEnd=depthOfField.gaussianEnd.value;
        clearEnd=Mathf.Max(camera.farClipPlane,blurEnd)*1000f;
        SetBlur(false);
        return true;
    }
    public void SetUIVisible(bool visible)
    {
        uiRequested=visible;
        UpdateUIVisibility();
    }
    static bool RayVisible(UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor ray)
        =>ray && ray.gameObject.activeInHierarchy;
    void UpdateUIVisibility()
    {
        if(!uiCamera || !worldData)return;
        // Tracked controller rays also need the clear overlay while the questionnaire is hidden.
        bool visible=uiRequested || (controllerRays &&
            (RayVisible(controllerRays.leftRay) || RayVisible(controllerRays.rightRay)));
        var stack=worldData.cameraStack;
        if(visible) {if(!stack.Contains(uiCamera))stack.Add(uiCamera);}
        else stack.Remove(uiCamera);
        uiCamera.enabled=visible;
    }
    public void SetBlur(bool enabled)
    {
        if(!depthOfField)return;
        if(enabled && IsBlurred)return;
        transitioning=enabled;
        blurStarted=Time.realtimeSinceStartupAsDouble;
        depthOfField.gaussianEnd.value=enabled?clearEnd:blurEnd;
        depthOfField.mode.value=enabled?DepthOfFieldMode.Gaussian:DepthOfFieldMode.Off;
        // Never toggle all post effects here: that also toggles the forest's colour grading.
        if(worldData)worldData.requiresDepthTexture=savedDepthTexture || enabled;
    }
    void LateUpdate()
    {
        if(transitioning && depthOfField) {
            float t=Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble-blurStarted)/BlurSeconds));
            float eased=t*t*(3f-2f*t);
            // Logarithmic focus ramp begins with negligible CoC, avoiding a half-resolution blur pop.
            depthOfField.gaussianEnd.value=Mathf.Exp(Mathf.Lerp(Mathf.Log(clearEnd),Mathf.Log(blurEnd),eased));
            if(t>=1f) {depthOfField.gaussianEnd.value=blurEnd;transitioning=false;}
        }
        UpdateUIVisibility();
        if(!uiCamera || !worldCamera)return;
        uiCamera.fieldOfView=worldCamera.fieldOfView;
        uiCamera.nearClipPlane=worldCamera.nearClipPlane;uiCamera.farClipPlane=worldCamera.farClipPlane;
    }
    void OnDestroy()
    {
        if(worldCamera)worldCamera.cullingMask=savedMask;
        if(worldData) {
            if(uiCamera)worldData.cameraStack?.Remove(uiCamera);
            worldData.renderPostProcessing=savedPostProcessing;
            worldData.requiresDepthTexture=savedDepthTexture;
            worldData.volumeLayerMask=savedVolumeMask;
        }
        if(uiCamera)Destroy(uiCamera.gameObject);
        if(volume)Destroy(volume.gameObject);
        if(profile) {
            foreach(var component in profile.components)if(component)Destroy(component);
            Destroy(profile);
        }
    }
}
