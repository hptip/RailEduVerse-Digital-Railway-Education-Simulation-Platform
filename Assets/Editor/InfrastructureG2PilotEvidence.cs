using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UttVr.BridgePilot;
using Object=UnityEngine.Object;

static partial class SO201OriginalBridgeAudit
{
    [MenuItem("Tools/UTTVR/Infrastructure/Verify saved G2 Pilot")]
    static void VerifySavedPilot()
    {
        const string path=PilotPath+"/Infrastructure_G2_Pilot.unity";
        var scene=EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var roots=scene.GetRootGameObjects();
            var sceneMeshes=roots.SelectMany(root=>root.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            var context=roots.Single(root=>root.name.StartsWith("01",StringComparison.Ordinal)).transform;
            var candidate=context.Find("02 · Visual Pilot");
            var legacy=context.Find("Công trình trước Pilot");
            var details=roots.Single(root=>root.name.StartsWith("03",StringComparison.Ordinal));
            int count=details.GetComponentsInChildren<BridgeBimIdentity>(true).Length;
            var loader=roots.Select(root=>root.GetComponent<InfrastructureDetailedLoader>()).Single(item=>item!=null);
            bool pass=sceneMeshes.All(filter=>filter.sharedMesh!=null&&filter.TryGetComponent<MeshRenderer>(out var renderer)&&
                renderer.sharedMaterials.All(material=>material!=null))&&candidate.gameObject.activeSelf&&
                !details.activeSelf&&!legacy.gameObject.activeSelf&&
                details.GetComponent<BridgeBimAdapter>().catalog.entries.Count(entry=>entry.IsElement)==count&&
                loader.occurrences.All(item=>item.mesh!=null&&item.material!=null)&&
                loader.catalog.entries.Count(entry=>entry.IsElement)==loader.occurrences.Length&&loader.LoadedCount==0&&
                !EditorBuildSettings.scenes.Any(sceneRecord=>sceneRecord.path==path);
            string file=PilotEvidence+"Geometry_Test_Results.csv";
            var rows=File.ReadAllLines(file).Where(line=>!line.StartsWith("PILOT-REOPEN,",StringComparison.Ordinal)).ToList();
            rows.Add($"PILOT-REOPEN,{Quoted("Saved isolated Unity scene")},{(pass?0:1)},0,{(pass?"PASS":"FAIL")},"+
                Quoted($"{sceneMeshes.Length} persisted mesh references, {count} regression sample bindings and {loader.occurrences.Length} lazy placements; scene excluded from normal Build Settings."));
            File.WriteAllLines(file,rows);
            if(!pass)throw new InvalidOperationException("Saved G2 Pilot verification failed.");
            Debug.Log($"G2 Pilot reopen verified: {sceneMeshes.Length} meshes, {count} detailed bindings.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void SavePilot(RoutePilot p)
    {
        const string geometryPath=PilotPath+"/PilotGeometry.asset";
        var catalog=ScriptableObject.CreateInstance<BridgeBimCatalog>();
        catalog.schema="UTTVR-G2-PILOT-DETAILED";
        catalog.modeledScale=1;
        catalog.entries=p.catalog.ToArray();
        AssetDatabase.CreateAsset(catalog,geometryPath);
        foreach(var material in p.materials.Values)
            AssetDatabase.AddObjectToAsset(material,catalog);
        var full=ScriptableObject.CreateInstance<BridgeBimCatalog>();
        full.name="Infrastructure Detailed catalog";
        full.schema=catalog.schema;
        full.modeledScale=1;
        var entries=new Dictionary<string,BridgeBimEntry>(StringComparer.Ordinal);
        void Group(string id,string parent,string name,string kind)
        {
            if(!entries.ContainsKey(id))entries.Add(id,new BridgeBimEntry{globalId=id,parentGlobalId=parent,name=name,ifcClass=kind});
        }
        Group("G2-ROUTE",null,"Yên Tử – Hạ Long Xanh","Route");
        foreach(var occurrence in p.occurrences)
        {
            if(AssetDatabase.Contains(occurrence.mesh)&&AssetDatabase.GetAssetPath(occurrence.mesh)!=geometryPath)
            {
                if(!p.meshes.TryGetValue(occurrence.mesh,out var independent))
                {
                    independent=Object.Instantiate(occurrence.mesh);
                    p.meshes.Add(occurrence.mesh,independent);
                }
                occurrence.mesh=independent;
            }
            if(!AssetDatabase.Contains(occurrence.mesh))
            {
                occurrence.mesh.name="Detailed geometry · "+occurrence.assetId;
                AssetDatabase.AddObjectToAsset(occurrence.mesh,catalog);
            }
            string segment=occurrence.zone;
            string family=segment+"/"+occurrence.domain;
            string assembly=family+"/"+occurrence.assembly;
            Group(segment,"G2-ROUTE",segment,"Segment");
            Group(family,segment,occurrence.domain,"Domain");
            Group(assembly,family,occurrence.assembly,"Assembly");
            var size=occurrence.mesh.bounds.size;
            entries.Add(occurrence.assetId,new BridgeBimEntry{globalId=occurrence.assetId,assetId=occurrence.assetId,parentGlobalId=assembly,
                ifcClass=occurrence.domain,name=occurrence.sourceId,sourceName=occurrence.sourceId,modeledBounds=size,modeledStation=occurrence.chainage,
                propertyLines=new[]{FormattableString.Invariant($"PARAMETRIC | Owner {occurrence.owner} | Placement {occurrence.zone} | {occurrence.chainage:F3} m"),
                    "Sáu giai đoạn vòng đời mô phỏng · dữ liệu người dùng lưu riêng"}});
        }
        full.entries=entries.Values.ToArray();
        AssetDatabase.AddObjectToAsset(full,catalog);
        var loader=p.Node(null,"04 · Detailed Loader").gameObject.AddComponent<InfrastructureDetailedLoader>();
        loader.catalog=full;
        loader.occurrences=p.occurrences.ToArray();
        loader.visualRoot=p.candidate;
        loader.previewChunk=22;
        var totals=new Dictionary<string,(int renderers,long triangles,long vertices)>();
        foreach(var (key,parts) in p.batches.OrderBy(item=>item.Key.domain).ThenBy(item=>item.Key.chunk)
            .ThenBy(item=>item.Key.mat.name,StringComparer.Ordinal))
        {
            var mesh=new Mesh{name=RoutePilot.BatchName(key),indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(parts.Select(item=>new CombineInstance{mesh=item.mesh,transform=item.matrix}).ToArray());
            mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh,catalog);
            p.Visual(p.candidate,mesh.name,mesh,new[]{key.mat});
            var previous=totals.GetValueOrDefault(key.domain);
            totals[key.domain]=(previous.renderers+1,previous.triangles+mesh.GetIndexCount(0)/3,previous.vertices+mesh.vertexCount);
        }
        foreach(var filter in p.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MeshFilter>(true)))
            if(filter.sharedMesh!=null && !AssetDatabase.Contains(filter.sharedMesh))
                AssetDatabase.AddObjectToAsset(filter.sharedMesh,catalog);
        var adapter=p.details.gameObject.AddComponent<BridgeBimAdapter>();
        adapter.catalog=catalog;
        foreach(var (kind,data) in totals.OrderBy(item=>item.Key))
        {
            long tris=data.triangles, verts=data.vertices;
            int renderers=data.renderers;
            if(kind=="Tunnel")
            {
                renderers+=p.mountainRenderers;
                tris+=p.mountainTriangles;
                verts+=p.mountainVertices;
            }
            if(kind=="Subgrade")
            {
                renderers+=p.gradeRenderers;
                tris+=p.gradeTriangles;
                verts+=p.gradeVertices;
            }
            p.performance.Add(FormattableString.Invariant(
                $"{kind},{p.ids.Count(id=>id.StartsWith("G2-"+kind+"-",StringComparison.Ordinal))},{renderers},{tris},{verts},{verts*32+tris*12},0,0"));
        }
        p.Check("VIS-FINITE","Batched candidate meshes",p.candidate.GetComponentsInChildren<MeshFilter>().Count(filter=>
            filter.sharedMesh.vertexCount==0||filter.sharedMesh.vertices.Any(point=>!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(point.z))),0,
            "Post-bake scan of every renderer mesh");
        p.Check("VIS-COLLIDERS","Visual output",p.candidate.GetComponentsInChildren<Collider>().Length,0,"Detailed meshes have explicit colliders; visual geometry uses none");
        var samples=p.details.GetComponentsInChildren<BridgeBimIdentity>();
        p.Check("BIM-BINDINGS","Detailed sample",samples.Count(item=>item.GetComponent<MeshCollider>().sharedMesh!=item.GetComponent<MeshFilter>().sharedMesh),0,
            "Same sample mesh used for rendering and selection; identities taken from Pilot registry");
        ValidatePilotDetailed(p,adapter,samples);
        ValidateFinalDetailed(p,loader);
        var candidates=p.candidate.GetComponentsInChildren<MeshFilter>().Concat(p.details.GetComponentsInChildren<MeshFilter>()).ToArray();
        var references=candidates.SelectMany(filter=>filter.GetComponent<Renderer>().sharedMaterials).Distinct().ToArray();
        var invalid=candidates.Count(filter=>AssetDatabase.GetAssetPath(filter.sharedMesh)!=geometryPath)+
            references.Count(mat=>AssetDatabase.GetAssetPath(mat)!=geometryPath ||
                mat.GetTexturePropertyNames().Any(property=>mat.GetTexture(property)!=null));
        p.Check("SRC-CANDIDATE","Visual and Detailed candidate",invalid,0,"Direct filter mesh/material ownership and serialized texture slots; existing context evaluated separately");
        var resources=AssetDatabase.GetDependencies(geometryPath,true);
        var imported=resources.Where(path=>path.Contains("/Source/",StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ifc",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".blend",StringComparison.OrdinalIgnoreCase)||
            path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)).ToArray();
        p.Check("SRC-RECURSIVE","PilotGeometry.asset",imported.Length,0,imported.Length==0?
            "No IFC/FBX/BLEND or source folder in recursive Unity dependencies of the candidate catalog asset." : string.Join(" | ",imported));
        p.Record("SRC-02","Research sources","BLOCKED","Unverified external rights unchanged. Only PilotGeometry candidate can be considered for a limited exception; the copied scene context and its existing asset dependencies are outside the exception.");
        p.details.gameObject.SetActive(false);
        p.legacy.gameObject.SetActive(false);
        var light=p.Node(null,"Inspection key").gameObject.AddComponent<Light>();
        light.type=LightType.Directional;
        light.intensity=1.15f;
        light.transform.rotation=Quaternion.Euler(41,-132,0);
        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(full);
        AssetDatabase.SaveAssetIfDirty(catalog);
        if(!EditorSceneManager.SaveScene(p.scene,PilotPath+"/Infrastructure_G2_Pilot.unity"))
            throw new IOException("Pilot scene did not save.");
        foreach(var mesh in p.temporary)
            if(mesh!=null&&!AssetDatabase.Contains(mesh))Object.DestroyImmediate(mesh);
    }

    static void ValidateFinalDetailed(RoutePilot p,InfrastructureDetailedLoader loader)
    {
        int loaded=0;
        foreach(string domain in new[]{"Tunnel","Subgrade","OCL"})
        {
            var occurrence=loader.occurrences.First(item=>item.domain==domain);
            loader.Load(domain,occurrence.chunk);
            int expected=loader.occurrences.Count(item=>item.domain==domain&&item.chunk==occurrence.chunk);
            loaded=Math.Max(loaded,loader.LoadedCount);
            var identity=loader.GetComponentsInChildren<BridgeBimIdentity>().Single(item=>item.GlobalId==occurrence.assetId);
            var filter=identity.GetComponent<MeshFilter>();
            var collider=identity.GetComponent<MeshCollider>();
            var original=filter.sharedMesh;
            var initial=identity.transform.localPosition;
            p.Check("BIM-LOAD",domain,
                loader.LoadedCount==expected&&original==occurrence.mesh&&collider.sharedMesh==original&&
                identity.transform.localToWorldMatrix==occurrence.placement?0:1,0,"The requested chunk binds shared mesh, material and authored placement");
            p.Check("BIM-LAZY-SELECT",domain,loader.ActiveAdapter.SelectAsset(occurrence.assetId,false)&&
                loader.ActiveAdapter.Selected.assetId==occurrence.assetId?0:1,0,"Shared adapter selects the lazily created physical identity");
            loader.Explode(.75f);
            p.Check("BIM-LAZY-EXPLODE",domain,identity.transform.localPosition!=initial?0:1,0,"Explode changes only transient display pose");
            loader.Section(new Plane(Vector3.forward,collider.bounds.center));
            p.Check("BIM-LAZY-SECTION",domain,filter.sharedMesh!=original&&filter.sharedMesh==collider.sharedMesh?0:1,0,"Real geometry cut and matching pick collider");
            loader.Isolate(occurrence.assetId);
            p.Check("BIM-LAZY-ISOLATE",domain,loader.GetComponentsInChildren<MeshCollider>().Count(item=>item.enabled)==1?0:1,0,"Only isolated leaf retains its pick collider");
            loader.ResetView();
            p.Check("BIM-LAZY-RESET",domain,filter.sharedMesh==original&&identity.transform.localPosition==initial?0:1,0,"Original placement and canonical geometry restored");
            p.Check("BIM-LIFECYCLE",domain,Math.Abs(loader.LifecycleStages(occurrence.assetId).Count()-6),0,"Reuses six existing stage definitions");
        }
        loader.Unload();
        p.Check("BIM-UNLOAD","All test selections",loader.LoadedCount,0,"Only requested bindings are active; unload restores the Visual batches");
        p.Record("BIM-PEAK","Loaded instances","INFO",$"Largest simultaneously loaded test chunk {loaded} physical leaves.");
    }

    static void ValidatePilotDetailed(RoutePilot p,BridgeBimAdapter adapter,BridgeBimIdentity[] samples)
    {
        var part=samples.First(identity=>identity.GlobalId.Contains("-Open-P001-P0L-Foundation",StringComparison.Ordinal));
        var filter=part.GetComponent<MeshFilter>();
        var source=filter.sharedMesh;
        var identity=part.GlobalId;
        typeof(BridgeBimAdapter).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(adapter,null);
        p.Check("BIM-CATALOG","World-placed OCL foundation",
            adapter.SelectAsset(identity,false)&&adapter.Selected?.assetId==identity?0:1,0,"Shared BridgeBimAdapter and stable AssetID");
        using(var review=new InfraReview(p.details))
        {
            var initial=part.transform.localPosition;
            review.Explode(.72f);
            p.Check("BIM-EXPLODE","Review pose",part.transform.localPosition==initial?1:0,0,"Reused G1 explode view on Detailed samples; Visual batches are separate");
            review.Section(Vector3.forward,source.bounds.center.z);
            var clipped=filter.sharedMesh;
            bool cut=clipped!=null&&clipped!=source&&clipped.bounds.min.z>=source.bounds.center.z-.002f&&
                clipped.indexFormat==IndexFormat.UInt16 && part.GetComponent<MeshCollider>().sharedMesh==clipped;
            p.Check("BIM-SHARED-CLIP","Small-mesh compatibility",cut?0:1,0,"Shared SO201 clip helper keeps UInt16 for small parts and updates selection collider");
            review.Isolate(identity,true);
            var selected=part.GetComponent<MeshCollider>();
            var hit=selected.Raycast(new Ray(source.bounds.center+Vector3.up*(source.bounds.extents.y+1f),Vector3.down),out _,10f);
            p.Check("BIM-ISOLATE","Detailed picking",hit&&samples.Count(other=>other.GetComponent<MeshCollider>().enabled)==1?0:1,0,
                "Ray hits isolated physical mesh; all other Detailed colliders disabled");
            review.Reset();
            p.Check("BIM-RESET","Review pose and geometry",filter.sharedMesh==source&&part.transform.localPosition==initial&&selected.enabled?0:1,0,
                "Section, simulation pose and picking restored");
        }
        adapter.Clear();
    }

    static void CapturePilot(RoutePilot p,PilotSectors sectors)
    {
        void Shot(string name,float d,Vector3 eye,Vector3 target,float size,bool before=false,float nearClip=.05f)
        {
            p.legacy.gameObject.SetActive(before);
            p.candidate.gameObject.SetActive(!before);
            var pose=p.Frame(d);
            // The orthographic near plane must start above local grade even on its lowest ray.
            if(size>0&&nearClip<1f)eye.y=Mathf.Max(eye.y,size+5f);
            InfraShot(p.scene,name,p.context,pose.point+pose.rotation*eye,pose.point+pose.rotation*target,size,PilotEvidence+"Screenshots/",nearClip);
        }
        Shot("OCL_Open_Before",sectors.overheadStart+65,new Vector3(20,13,-31),new Vector3(0,2,0),24,true);
        Shot("OCL_Open",sectors.overheadStart+65,new Vector3(20,13,-31),new Vector3(0,2,0),24);
        var bridge=p.manager.fixedInfrastructure.Single(item=>item.kind=="Bridge");
        Shot("OCL_SO201",bridge.startChainage+255,new Vector3(44,28,-40),new Vector3(0,0,0),39);
        Shot("OCL_Station",(p.stationStart+p.stationEnd)*.5f,new Vector3(31,21,-38),new Vector3(0,1,0),37);
        foreach(var tunnel in p.manager.fixedInfrastructure.Where(item=>item.kind=="TunnelMountain"))
        {
            string name=tunnel.id.Replace("TunnelMountain_","");
            float start=tunnel.startChainage,end=tunnel.endChainage;
            Shot(name+"_Portal_Before",start,new Vector3(14,11,-44),new Vector3(0,4,11),25,true);
            Shot(name+"_Portal",start,new Vector3(15,17,-36),new Vector3(0,4,7),20);
            Shot(name+"_Exit",end,new Vector3(-13,16,37),new Vector3(0,4,-5),20);
            Shot(name+"_Mountain",start+(end-start)*.5f,new Vector3(195,260,-195),new Vector3(0,13,0),250);
            Shot(name+"_Cabin",start+35,new Vector3(p.spacing*.5f,2.75f,0),new Vector3(p.spacing*.5f,2.9f,28),0);
            Shot(name+"_Interior_OCL",start+95,new Vector3(0,4f,0),new Vector3(0,4.25f,32),0);
            Shot(name+"_Section",start+95,new Vector3(0,5f,-48),new Vector3(0,5f,0),12f,nearClip:48f);
        }
        Shot("Subgrade_Before",sectors.subgradeStart+76,new Vector3(24,13,-40),new Vector3(0,-.8f,0),24,true);
        Shot("Subgrade",sectors.subgradeStart+76,new Vector3(24,13,-40),new Vector3(0,-.8f,0),24);
        foreach(var band in p.config.subgradeBands.Take(3))
            Shot("Subgrade_"+band.kind,(band.from+band.to)*.5f,new Vector3(17,8,-24),new Vector3(0,-.8f,0),18);
        Shot("Subgrade_Transition",p.config.subgradeBands[1].to,new Vector3(25,10,-33),new Vector3(0,-1,0),29);
        var loader=p.scene.GetRootGameObjects().Select(go=>go.GetComponent<InfrastructureDetailedLoader>()).First(item=>item!=null);
        float detailStation=(p.config.subgradeBands[1].from+p.config.subgradeBands[1].to)*.5f+25f;
        loader.Load("Subgrade",Mathf.FloorToInt(detailStation/50f));
        loader.Explode(1.2f);
        var detailFrame=p.Frame(detailStation);
        InfraShot(p.scene,"Detailed_Pilot_Exploded",loader.transform,detailFrame.point+detailFrame.rotation*new Vector3(19,13,-25),
            detailFrame.point+Vector3.down,19,PilotEvidence+"Screenshots/");
        loader.Unload();
        p.legacy.gameObject.SetActive(false);
        p.candidate.gameObject.SetActive(true);
    }
}

[CustomEditor(typeof(InfrastructureDetailedLoader))]
sealed class InfrastructureDetailedLoaderInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var loader=(InfrastructureDetailedLoader)target;
        var active=loader.occurrences.Where(item=>item.chunk==loader.previewChunk&&item.domain==loader.previewDomain).ToArray();
        EditorGUILayout.LabelField("Cấu kiện trong chunk",active.Length.ToString());
        if(GUILayout.Button("Nạp Detailed"))loader.Load(loader.previewDomain,loader.previewChunk);
        using(new EditorGUI.DisabledScope(loader.LoadedCount==0 || active.Length==0))
        {
            if(GUILayout.Button("Tách cấu kiện"))loader.Explode(.8f);
            if(GUILayout.Button("Mặt cắt dọc"))
                loader.Section(new Plane(Vector3.right,active[0].placement.MultiplyPoint3x4(active[0].mesh.bounds.center)));
            if(GUILayout.Button("Cô lập cấu kiện đầu"))loader.Isolate(active[0].assetId);
            if(GUILayout.Button("Khôi phục hiển thị"))loader.ResetView();
            if(GUILayout.Button("Dỡ Detailed"))loader.Unload();
        }
        SceneView.RepaintAll();
    }
}
