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
            // Generic mesh shaders do not implement terrain's instanced vertex layout.
            if(scene.name=="Serene_Night_Exposure" && terrain.materialTemplate &&
               terrain.materialTemplate.shader.name!="Universal Render Pipeline/Terrain/Lit" &&
               terrain.terrainData.terrainLayers.Length==0)
                terrain.gameObject.AddComponent<QuestTerrainSurface>().Configure(terrain);
            terrain.drawInstanced=terrain.materialTemplate &&
                terrain.materialTemplate.shader.name=="Universal Render Pipeline/Terrain/Lit";
            terrain.heightmapPixelError=Mathf.Max(terrain.heightmapPixelError,10);
            terrain.detailObjectDistance=Mathf.Min(terrain.detailObjectDistance,30);
            terrain.detailObjectDensity=Mathf.Min(terrain.detailObjectDensity,.45f);
            terrain.treeMaximumFullLODCount=Mathf.Min(terrain.treeMaximumFullLODCount,24);
            terrain.treeDistance=Mathf.Min(terrain.treeDistance,140);
            terrain.treeBillboardDistance=Mathf.Min(terrain.treeBillboardDistance,25);
        }
        if(scene.name=="Serene_Night_Exposure") {
            // Imported crowds must not animate and skin when culled from both eyes.
            foreach(var root in scene.GetRootGameObjects()) {
                foreach(var animator in root.GetComponentsInChildren<Animator>(true)) {
                    animator.cullingMode=AnimatorCullingMode.CullCompletely;CullSmallActor(animator.gameObject);
                }
                foreach(var animation in root.GetComponentsInChildren<Animation>(true)) {
                    animation.cullingType=AnimationCullingType.BasedOnRenderers;CullSmallActor(animation.gameObject);
                }
                foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=false;
            }
        }
        int vegetationLayer=LayerMask.NameToLayer("QuestVegetation");
        if(vegetationLayer<0)return;
        foreach(var root in scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
            // Shared city meshes and materials can use instanced draws instead of one draw per object.
            if(SystemInfo.supportsInstancing)foreach(var material in renderer.sharedMaterials)
                if(material && material.shader && material.shader.isSupported)material.enableInstancing=true;
            if(renderer.gameObject.layer!=0 || renderer.GetComponent<Collider>() || renderer.GetComponentInParent<Canvas>())continue;
            if(IsVegetation(renderer.transform))renderer.gameObject.layer=vegetationLayer;
        }
        foreach(var root in scene.GetRootGameObjects())foreach(var camera in root.GetComponentsInChildren<Camera>(true)) {
            var distances=camera.layerCullDistances;distances[vegetationLayer]=70;camera.layerCullDistances=distances;
        }

    }
    static void CullSmallActor(GameObject actor)
    {
        if(actor.GetComponentInParent<LODGroup>())return;
        var renderers=actor.GetComponentsInChildren<Renderer>(true);
        if(renderers.Length==0)return;
        // Tiny distant crowds do not need full animation/skinning. Keep buildings and height cues.
        var lod=actor.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.None;
        lod.SetLODs(new[]{new LOD(.01f,renderers)});lod.RecalculateBounds();
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

// Convert the imported mesh-style asphalt to a real terrain layer without modifying disk assets.
sealed class QuestTerrainSurface : MonoBehaviour
{
    Terrain terrain;
    TerrainData data;
    Material original, material;
    TerrainLayer layer;
    TerrainLayer[] originalLayers;
    int originalAlphaResolution;
    public void Configure(Terrain target)
    {
        var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if(!shader || !shader.isSupported)return;
        terrain=target;data=target.terrainData;original=target.materialTemplate;
        originalLayers=data.terrainLayers;
        originalAlphaResolution=data.alphamapResolution;
        layer=new TerrainLayer();
        layer.diffuseTexture=original.HasProperty("_BaseMap")?original.GetTexture("_BaseMap") as Texture2D:null;
        layer.normalMapTexture=original.HasProperty("_BumpMap")?original.GetTexture("_BumpMap") as Texture2D:null;
        var scale=original.HasProperty("_BaseMap")?original.GetTextureScale("_BaseMap"):Vector2.one;
        layer.tileSize=new Vector2(data.size.x/Mathf.Max(.001f,Mathf.Abs(scale.x)),data.size.z/Mathf.Max(.001f,Mathf.Abs(scale.y)));
        layer.smoothness=0;layer.metallic=0;
        if(original.HasProperty("_BaseColor"))layer.diffuseRemapMax=original.GetColor("_BaseColor");
        data.terrainLayers=new[]{layer};
        // These imported grounds have no painted layers. A uniform layer needs only a tiny map.
        data.alphamapResolution=16;
        var weights=new float[16,16,1];
        for(int z=0;z<16;z++)for(int x=0;x<16;x++)weights[z,x,0]=1;
        data.SetAlphamaps(0,0,weights);
        material=new Material(shader){enableInstancing=true};terrain.materialTemplate=material;
    }
    void OnDestroy()
    {
        if(terrain)terrain.materialTemplate=original;
        if(data && originalLayers!=null) {data.terrainLayers=originalLayers;data.alphamapResolution=originalAlphaResolution;}
        if(material)Destroy(material);
        if(layer)Destroy(layer);
    }
}
