using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class MindfulnessRouteSetup
{
    // Lake bank, flower slope, woodland trail and northern ridge.
    static readonly Vector2[] Plan={new Vector2(169.991f,166.095f),new Vector2(171,177),new Vector2(170,186),new Vector2(165,193),new Vector2(174,204),new Vector2(188,216),new Vector2(202,231),new Vector2(206,246),new Vector2(202,257),new Vector2(192,265),new Vector2(180,270),new Vector2(165,270),new Vector2(150,269),new Vector2(136,267)};
    public static void Configure(ExperimentGroupController controller)
    {
        var camera=Camera.main;
        if(!camera || !camera.transform.parent)throw new InvalidOperationException("The route requires the existing tracked camera parent.");
        var land=Terrain.activeTerrains.First(t=>t.name=="LandTerrain");
        var origin=camera.transform.parent;
        float offset=origin.position.y-land.SampleHeight(origin.position)-land.transform.position.y;
        var route=controller.GetComponent<MindfulnessRoute>();
        if(!route)route=controller.gameObject.AddComponent<MindfulnessRoute>();
        route.controller=controller;route.trackingOrigin=origin;
        route.waypoints=BuildWalkableRoute(origin,land,offset);
        route.waypoints[0]=origin.position;
        controller.mindfulnessRoute=route;
        EditorUtility.SetDirty(route);EditorUtility.SetDirty(controller);
    }
    static Vector3[] BuildWalkableRoute(Transform origin,Terrain land,float offset)
    {
        var bounds=new Bounds(new Vector3(185,55,220),new Vector3(150,150,130));
        var sources=new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds,~0,NavMeshCollectGeometry.PhysicsColliders,0,
            new List<NavMeshBuildMarkup>{new NavMeshBuildMarkup{root=origin,ignoreFromBuild=true}},sources);
        // Exclude the imported hollow/overhanging meshes that admit interior navmesh paths.
        foreach(var collider in UnityEngine.Object.FindObjectsByType<Collider>()
                    .Where(c=>c.name.StartsWith("Rock_Big_03",StringComparison.Ordinal) || c.name.StartsWith("Rock_Small_02",StringComparison.Ordinal))) {
            sources.Add(new NavMeshBuildSource {shape=NavMeshBuildSourceShape.Box,area=1,
                transform=Matrix4x4.TRS(collider.bounds.center,Quaternion.identity,Vector3.one),
                size=collider.bounds.size+new Vector3(1,0,1)});
        }
        var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=1f;settings.agentHeight=1.8f;
        settings.overrideVoxelSize=true;settings.voxelSize=.2f;
        var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
        if(!data)throw new InvalidOperationException("Could not build the temporary route navigation mesh.");
        var instance=NavMesh.AddNavMeshData(data);
        try {
            var filter=new NavMeshQueryFilter {agentTypeID=settings.agentTypeID,areaMask=NavMesh.AllAreas};
            var targets=new[]{Plan[0],Plan[3],Plan[5],Plan[7],Plan[10],Plan[11]};
            var corners=new List<Vector3>();Vector3 previous=Vector3.zero;
            foreach(var target in targets) {
                var p=new Vector3(target.x,0,target.y);p.y=land.SampleHeight(p)+land.transform.position.y;
                if(!NavMesh.SamplePosition(p,out var hit,10f,filter))throw new InvalidOperationException("No walkable route point near "+p);
                if(corners.Count==0)corners.Add(hit.position);
                else {
                    var path=new NavMeshPath();
                    if(!NavMesh.CalculatePath(previous,hit.position,filter,path) || path.status!=NavMeshPathStatus.PathComplete)
                        throw new InvalidOperationException("The scenic route has a disconnected path near "+p);
                    corners.AddRange(path.corners.Skip(1));
                }
                previous=hit.position;
            }
            var dense=new List<Vector3>{corners[0]};
            for(int i=1;i<corners.Count;i++) {
                int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(corners[i-1],corners[i])));
                for(int j=1;j<=count;j++)dense.Add(Vector3.Lerp(corners[i-1],corners[i],j/(float)count));
            }
            for(int pass=0;pass<4;pass++) {
                var copy=dense.ToArray();
                for(int i=1;i<dense.Count-1;i++) {
                    Vector3 candidate=(copy[i-1]+2*copy[i]+copy[i+1])*.25f;
                    if(NavMesh.SamplePosition(candidate,out var hit,.6f,filter) &&
                       !NavMesh.Raycast(copy[i-1],hit.position,out _,filter) && !NavMesh.Raycast(hit.position,copy[i+1],out _,filter))dense[i]=hit.position;
                }
            }
            return dense.Select(p=>new Vector3(p.x,Mathf.Max(p.y,land.SampleHeight(p)+land.transform.position.y)+offset,p.z)).ToArray();
        } finally {NavMesh.RemoveNavMeshData(instance);UnityEngine.Object.DestroyImmediate(data);}
    }
}
