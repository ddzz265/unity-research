using System;
using System.IO;
using System.Linq;
using BreathingOrbDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class ExperimentSceneSetup
{
    public const string ScenePath="Assets/Scenes/Serene_Night_Experiment.unity";
    const string AssetsRoot="Assets/SereneNightExperiment";
    public const string MindfulnessAudioPath=AssetsRoot+"/Audio/Mandarin_MindfulBreathing.mp3";

    [MenuItem("Serene Night/Create Experiment Scene")]
    public static void Create()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before setup.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        Directory.CreateDirectory("Assets/Scenes");
        if(!File.Exists(ScenePath) && !AssetDatabase.CopyAsset("Assets/mindful-forests.unity",ScenePath))
            throw new Exception("Could not copy the original forest scene.");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var camera=Camera.main;
        if(!camera)throw new Exception("Forest scene requires its original Main Camera.");
        var orb=UnityEngine.Object.FindAnyObjectByType<BreathingOrb>(FindObjectsInactive.Include);
        if(!orb)throw new Exception("Existing breathing orb was not found.");
        var sensor=orb.GetComponent<RealtimeBreathOrb>();
        if(sensor) {sensor.enabled=false;sensor.showStatus=false;}
        orb.enabled=false;orb.paused=true;orb.showControls=false;orb.gameObject.SetActive(false);
        // Preserve the small in-scene sensor orb's size when using automatic pacing.
        if(sensor) {orb.minimumDiameter=sensor.minimumDiameter;orb.maximumDiameter=sensor.maximumDiameter;}
        EditorUtility.SetDirty(orb);if(sensor)EditorUtility.SetDirty(sensor);

        var desktop=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .Where(b=>b && (b.GetType().FullName=="IdyllicFantasyNature.CameraMovement" || b.GetType().FullName=="IdyllicFantasyNature.PlayerMovement"))
            .Cast<Behaviour>().ToArray();
        var controller=UnityEngine.Object.FindAnyObjectByType<ExperimentGroupController>();
        if(!controller)controller=new GameObject("Experiment Group Controller").AddComponent<ExperimentGroupController>();
        var view=UnityEngine.Object.FindAnyObjectByType<QuestionnaireView>();
        if(!view)view=new GameObject("Experiment Questionnaire").AddComponent<QuestionnaireView>();
        view.showOnStart=false;
        view.questionnaires=AssetDatabase.FindAssets("t:QuestionnaireContent",new[]{AssetsRoot+"/Questionnaires"})
            .Select(g=>AssetDatabase.LoadAssetAtPath<QuestionnaireContent>(AssetDatabase.GUIDToAssetPath(g)))
            .OrderBy(q=>q.name=="Likert7"?0:1).ToArray();
        view.selectedQuestionnaire=0;
        view.font=AssetDatabase.LoadAssetAtPath<Font>(AssetsRoot+"/Fonts/NotoSansCJKsc-Regular.otf");
        controller.group=ExperimentGroup.A;controller.startOnPlay=true;
        controller.breathingOrb=orb.gameObject;controller.questionnaire=view;
        controller.guidanceAudio=Array.Empty<AudioSource>();controller.desktopControls=desktop;
        ConfigureMindfulness(controller);
        MindfulnessRouteSetup.Configure(controller);

        // Keep the old orb and ambience silent during the spoken mindfulness session.
        var ambience=UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include)
            .FirstOrDefault(s=>s.name=="AudioManager");
        if(!ambience)ambience=new GameObject("Water Ambience").AddComponent<AudioSource>();
        ambience.Stop();ambience.clip=null;ambience.loop=false;ambience.playOnAwake=false;ambience.enabled=false;
        controller.ambienceAudio=ambience;EditorUtility.SetDirty(ambience);

        if(!UnityEngine.Object.FindAnyObjectByType<XRInteractionManager>())new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
        var events=UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        if(!events)events=new GameObject("XR EventSystem").AddComponent<EventSystem>();
        foreach(var module in events.GetComponents<BaseInputModule>())module.enabled=module is XRUIInputModule;
        var ui=events.GetComponent<XRUIInputModule>();if(!ui)ui=events.gameObject.AddComponent<XRUIInputModule>();
        ui.enableMouseInput=true;ui.enableXRInput=true;
        var bridge=UnityEngine.Object.FindAnyObjectByType<SpatialControllerRays>();
        if(!bridge)bridge=new GameObject("PS VR2 Sense Rays").AddComponent<SpatialControllerRays>();
        bridge.trackingOrigin=camera.transform.parent;
        bridge.leftRay=bridge.leftRay?bridge.leftRay:CreateRay("Left PS VR2 Sense",bridge.transform);
        bridge.rightRay=bridge.rightRay?bridge.rightRay:CreateRay("Right PS VR2 Sense",bridge.transform);

        var pose=camera.GetComponent<TrackedPoseDriver>();if(!pose)pose=camera.gameObject.AddComponent<TrackedPoseDriver>();
        pose.positionInput=new InputActionProperty(new InputAction("Head Position",InputActionType.Value,"<XRHMD>/centerEyePosition"));
        pose.rotationInput=new InputActionProperty(new InputAction("Head Rotation",InputActionType.Value,"<XRHMD>/centerEyeRotation"));
        pose.trackingStateInput=new InputActionProperty(new InputAction("Head Tracking State",InputActionType.Value,"<XRHMD>/trackingState"));
        pose.ignoreTrackingState=false;pose.enabled=false;
        var rig=bridge.GetComponent<VisionExperimentRig>();if(!rig)rig=bridge.gameObject.AddComponent<VisionExperimentRig>();
        rig.headPose=pose;rig.desktopControls=desktop;
        foreach(var obj in new UnityEngine.Object[]{controller,view,bridge,pose,rig,ui})EditorUtility.SetDirty(obj);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        // Preserve previous scene entries; add the experiment as the first enabled entry.
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}
            .Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();
        AssetDatabase.SaveAssets();Selection.activeGameObject=controller.gameObject;
        Debug.Log($"SERENE_EXPERIMENT_SETUP_OK: mindfulness audio then the original A-F questionnaire rules. Camera={camera.transform.position}, origin={bridge.trackingOrigin?.position}");
    }
    public static void ConfigureMindfulness(ExperimentGroupController controller)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(MindfulnessAudioPath);
        if(!clip)throw new InvalidOperationException("Import the Mandarin mindfulness audio before setting up the experiment.");
        var audio=controller.mindfulnessAudio;
        if(!audio)audio=controller.gameObject.AddComponent<AudioSource>();
        audio.Stop();audio.clip=clip;audio.enabled=true;audio.playOnAwake=false;
        audio.loop=false;audio.pitch=1f;audio.spatialBlend=0f;audio.volume=1f;
        controller.mindfulnessAudio=audio;
        EditorUtility.SetDirty(audio);EditorUtility.SetDirty(controller);
    }
    static XRRayInteractor CreateRay(string name,Transform parent)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        var ray=go.AddComponent<XRRayInteractor>();ray.enableUIInteraction=true;ray.maxRaycastDistance=8;
        var line=go.AddComponent<LineRenderer>();
        var material=AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot+"/RayMaterial.mat");
        if(!material) {
            material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material,AssetsRoot+"/RayMaterial.mat");
        }
        line.sharedMaterial=material;
        go.AddComponent<XRInteractorLineVisual>().lineWidth=.004f;
        return ray;
    }
}
