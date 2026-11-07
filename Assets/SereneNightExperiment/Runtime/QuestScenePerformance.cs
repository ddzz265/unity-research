using UnityEngine;
using UnityEngine.SceneManagement;

// Apply the same mobile scene budget to every experimental group; desktop assets stay unchanged.
public static class QuestScenePerformance
{
    static readonly string[] VegetationNames={"tree","bush","grass","foliage","fern","shrub","reed","waterplant","pine"};
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        SceneManager.sceneLoaded-=ApplyScene;
        SceneManager.sceneLoaded+=ApplyScene;
#endif
    }
    static void ApplyScene(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="Serene_Night_Experiment" && scene.name!="Serene_Night_Exposure" && scene.name!="Serene_Night_Music")return;
        foreach(var root in scene.GetRootGameObjects())foreach(var terrain in root.GetComponentsInChildren<Terrain>(true)) {
            terrain.drawInstanced=true;
            terrain.heightmapPixelError=Mathf.Max(terrain.heightmapPixelError,10);
            terrain.detailObjectDistance=Mathf.Min(terrain.detailObjectDistance,45);
            terrain.detailObjectDensity=Mathf.Min(terrain.detailObjectDensity,.65f);
            terrain.treeDistance=Mathf.Min(terrain.treeDistance,180);
            terrain.treeBillboardDistance=Mathf.Min(terrain.treeBillboardDistance,35);
        }
        int vegetationLayer=LayerMask.NameToLayer("QuestVegetation");
        if(vegetationLayer<0)return;
        foreach(var root in scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
            if(renderer.gameObject.layer!=0 || renderer.GetComponent<Collider>() || renderer.GetComponentInParent<Canvas>())continue;
            if(IsVegetation(renderer.transform))renderer.gameObject.layer=vegetationLayer;
        }
        foreach(var root in scene.GetRootGameObjects())foreach(var camera in root.GetComponentsInChildren<Camera>(true)) {
            var distances=camera.layerCullDistances;distances[vegetationLayer]=90;camera.layerCullDistances=distances;
        }

    }
    static bool IsVegetation(Transform item)
    {
        for(var current=item;current;current=current.parent) {
            var name=current.name;
            foreach(var word in VegetationNames)
                if(name.IndexOf(word,System.StringComparison.OrdinalIgnoreCase)>=0)return true;
        }
        return false;
    }
}
