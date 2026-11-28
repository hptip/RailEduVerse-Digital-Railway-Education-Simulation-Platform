using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UttVr.BridgePilot;
using Object=UnityEngine.Object;

static partial class SO201OriginalBridgeAudit
{
    const string ProdEvidence=InfraEvidence+"Production/";
    const string ProdAssets="Assets/ProjectLopHoc/Generated/InfrastructureProduction";
    const string ProdRollback=InfraKit+"Rollback/G2_Production_PreChange_20261127.zip";
    const string ProdBimScene="Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity";
    const string ProdInstallRoot="Infrastructure_Production_v1";
    [Serializable] sealed class InfraApproval
    {
        public bool approved;
        public string candidate_fingerprint,candidate_manifest,user_confirmation;
        public string[] excluded;
    }
    static string InfraHash(string path)
    {
        using var sha=SHA256.Create();
        using var stream=File.OpenRead(path);
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }

    static bool ApprovedOccurrence(InfrastructureDetailedLoader.Occurrence occurrence)=>
        !occurrence.assetId.StartsWith("G2-OCL-Station-",StringComparison.Ordinal);

    [MenuItem("Tools/UTTVR/Infrastructure/Integrate approved production")]
    static void IntegrateInfrastructureProduction()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Edit Mode is required.");
        var approval=JsonUtility.FromJson<InfraApproval>(File.ReadAllText(InfraEvidence+"Approval.json"));
        if(!File.Exists(ProdRollback)||!approval.approved||!approval.excluded.Contains("OCL-Station")||
            approval.candidate_manifest!="Production_Candidate_Manifest.csv"||
            InfraHash(InfraEvidence+approval.candidate_manifest)!=approval.candidate_fingerprint)
            throw new InvalidOperationException("Frozen approval or rollback is missing.");
        foreach(var line in File.ReadLines(InfraEvidence+approval.candidate_manifest).Skip(1))
        {
            var fields=line.Split("\",\"");
            if(InfraHash(fields[0].Trim('"'))!=fields[1].Trim('"'))
                throw new InvalidOperationException("Candidate changed after approval: "+fields[0]);
        }
        Directory.CreateDirectory(ProdEvidence+"Screenshots");
        Directory.CreateDirectory(ProdAssets+"/Resources/InfrastructureProduction");
        AssetDatabase.Refresh();
        var rows=new List<string>{"test_id,subject,value,limit,status,method"};
        void Record(string id,string subject,string status,string details)=>
            rows.Add($"{id},{Quoted(subject)},,,{status},{Quoted(details)}");
        var planned=new List<(Scene scene,Transform root,string id)>();
        var saved=new Dictionary<string,byte[]>();
        Scene pilot=default;
        try
        {
            var route=Open(Route);
            var manager=route.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var spline=manager.trainMover.splineRoute;
            float length=spline.CalculateLength();
            Vector3 Position(float d)=>spline.EvaluatePosition(Mathf.Clamp01(d/length));
            Vector3 Forward(float d)=>Vector3.ProjectOnPlane((Vector3)spline.EvaluateTangent(Mathf.Clamp01(d/length)),Vector3.up).normalized;
            var points=Enumerable.Range(0,Mathf.CeilToInt(length/4f)+1).Select(i=>Position(i*4f)).ToArray();
            float Project(Vector3 position,float min,float max)
            {
                int start=Mathf.Max(0,Mathf.FloorToInt((min-40)/4f)),end=Mathf.Min(points.Length-1,Mathf.CeilToInt((max+40)/4f));
                int best=start;
                float distance=float.PositiveInfinity;
                for(int sample=start;sample<=end;sample++)
                {
                    var delta=Vector3.ProjectOnPlane(position-points[sample],Vector3.up);
                    if(delta.sqrMagnitude<distance){distance=delta.sqrMagnitude;best=sample;}
                }
                float d=best*4;
                return d+Vector3.Dot(position-Position(d),Forward(d));
            }
            string ZoneAt(float d)=>manager.zones.Last(zone=>zone.startChainage<=d+.01f).zoneId;
            pilot=EditorSceneManager.OpenPreviewScene(PilotPath+"/Infrastructure_G2_Pilot.unity");
            var loader=pilot.GetRootGameObjects().Select(go=>go.GetComponent<InfrastructureDetailedLoader>()).Single(item=>item!=null);
            var subset=loader.occurrences.Where(ApprovedOccurrence).ToArray();
            if(subset.Length==loader.occurrences.Length||subset.Select(item=>item.assetId).Distinct().Count()!=subset.Length)
                throw new InvalidOperationException("Station exclusion or stable ID check failed.");
            var byBatch=subset.Where(item=>item.visualBatch!="HIDDEN_IN_VISUAL").ToLookup(item=>item.visualBatch,StringComparer.Ordinal);
            var context=pilot.GetRootGameObjects().Single(go=>go.name.StartsWith("01",StringComparison.Ordinal)).transform;
            var finalVisual=context.Find("02 · Visual Pilot").GetComponentsInChildren<MeshFilter>();
            var zoneVisuals=new Dictionary<string,List<MeshFilter>>();
            void Add(string zone,MeshFilter filter)
            {
                if(!zoneVisuals.TryGetValue(zone,out var items))zoneVisuals[zone]=items=new();
                items.Add(filter);
            }
            foreach(var filter in finalVisual)
            {
                var entries=byBatch[filter.name].ToArray();
                if(entries.Length>0)
                {
                    float center=(entries[0].chunk+.5f)*50f;
                    Add(ZoneAt(center),filter);
                    continue;
                }
                if(filter.name.StartsWith("Subgrade shoulders",StringComparison.Ordinal))
                {
                    Add(ZoneAt(float.Parse(filter.name.Split(' ').Last(),System.Globalization.CultureInfo.InvariantCulture)),filter);
                    continue;
                }
                if(!filter.name.StartsWith("TunnelMountain_",StringComparison.Ordinal))continue; // Station batches have no approved record.
                var item=manager.fixedInfrastructure.Single(work=>filter.name.StartsWith(work.id+"_Ground",StringComparison.Ordinal));
                int chunk=int.Parse(filter.name.Split(new[]{"_Ground"},StringSplitOptions.None)[1].Substring(0,2));
                Add(ZoneAt(item.startChainage+(chunk+.5f)*50f),filter);
            }
            if(manager.zones.Count!=17||zoneVisuals.Count<7)throw new InvalidOperationException("Unexpected production zone mapping.");
            var cuts=new List<(float start,float end)>{(3000,3500)};
            foreach(var work in manager.fixedInfrastructure)
            {
                if(work.kind=="Bridge")cuts.Add((work.startChainage+10,work.endChainage-10));
                if(work.kind=="TunnelMountain")cuts.Add((work.startChainage-33,work.endChainage+33));
            }
            ValidateProductionGrade(manager,spline,subset,rows);
            var metrics=new List<string>{"zone,visual_renderers,triangles,mesh_memory_bytes,removed_legacy_objects,retrimmed_meshes"};
            var productionBatches=new HashSet<string>(StringComparer.Ordinal);
            foreach(var zone in manager.zones)
            {
                var source=context.Find(zone.zoneId);
                var replacements=source==null ? Array.Empty<MeshFilter>() : source.GetComponentsInChildren<MeshFilter>()
                    .Where(filter=>filter.sharedMesh.name.Contains("Pilot shoulder trim")||filter.sharedMesh.name.StartsWith("PilotTerrain_")).ToArray();
                bool touches=zoneVisuals.TryGetValue(zone.zoneId,out var models);
                if(!touches&&replacements.Length==0)continue;
                var scene=Open(zone.scenePath);
                var root=Root(scene);
                if(root.Find(ProdInstallRoot)!=null)throw new InvalidOperationException("Existing production install. Roll back before replacing the frozen version.");
                saved.Add(zone.scenePath,File.ReadAllBytes(zone.scenePath));
                var holder=new GameObject(ProdInstallRoot);
                SceneManager.MoveGameObjectToScene(holder,scene);
                holder.transform.SetParent(root,false);
                var pack=ScriptableObject.CreateInstance<BridgeBimCatalog>();
                pack.name=zone.zoneId+" Visual Meshes";
                var packPath=ProdAssets+"/"+zone.zoneId+".asset";
                AssetDatabase.CreateAsset(pack,packPath);
                var materialCopies=new Dictionary<Material,Material>();
                Material Local(Material sourceMaterial)
                {
                    if(materialCopies.TryGetValue(sourceMaterial,out var mat))return mat;
                    mat=Object.Instantiate(sourceMaterial);
                    mat.name=sourceMaterial.name;
                    if(mat.GetTexturePropertyNames().Any(key=>mat.GetTexture(key)!=null))
                        throw new InvalidOperationException("Unexpected candidate texture "+mat.name);
                    materialCopies.Add(sourceMaterial,mat);
                    AssetDatabase.AddObjectToAsset(mat,pack);
                    return mat;
                }
                Mesh Store(Mesh original)
                {
                    var mesh=Object.Instantiate(original);
                    mesh.name=original.name;
                    AssetDatabase.AddObjectToAsset(mesh,pack);
                    return mesh;
                }
                foreach(var filter in models??Enumerable.Empty<MeshFilter>())
                {
                    var node=new GameObject(filter.name);
                    SceneManager.MoveGameObjectToScene(node,scene);
                    node.transform.SetParent(holder.transform,false);
                    node.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);
                    node.transform.localScale=filter.transform.lossyScale;
                    node.AddComponent<MeshFilter>().sharedMesh=Store(filter.sharedMesh);
                    var renderer=node.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials=filter.GetComponent<MeshRenderer>().sharedMaterials.Select(Local).ToArray();
                    renderer.shadowCastingMode=ShadowCastingMode.Off;
                    productionBatches.Add(filter.name);
                }
                int trims=0,removed=0;
                foreach(var filter in replacements)
                {
                    var targets=root.GetComponentsInChildren<MeshFilter>(true).Where(candidate=>candidate.name==filter.name&&
                        (candidate.transform.position-filter.transform.position).sqrMagnitude<.0001f).ToArray();
                    if(targets.Length!=1)throw new InvalidOperationException("Terrain mapping ambiguous: "+zone.zoneId+" "+filter.name);
                    var target=targets[0];
                    var clipped=Store(filter.sharedMesh);
                    var collider=target.GetComponent<MeshCollider>();
                    if(collider!=null&&collider.sharedMesh==target.sharedMesh)collider.sharedMesh=clipped;
                    target.sharedMesh=clipped;
                    trims++;
                }
                var obsolete=root.Cast<Transform>().Where(node=>node.name.StartsWith("TunnelMountainGroup_",StringComparison.Ordinal)||
                    node.name=="Experience_OverheadPortalSector"||node.name=="Experience_SubgradeReinforcedSlope").ToArray();
                foreach(var node in obsolete){Object.DestroyImmediate(node.gameObject);removed++;}
                var catenary=root.Find("Catenary_SingleMasts");
                if(catenary!=null)
                    foreach(var node in catenary.Cast<Transform>().ToArray())
                    {
                        var filter=node.GetComponent<MeshFilter>();
                        if(filter==null||filter.sharedMesh==null)continue;
                        var change=TrimLegacyInfrastructure(filter,point=>Project(point,zone.startChainage,zone.endChainage),cuts);
                        if(change==null)continue;
                        if(change.vertexCount==0){Object.DestroyImmediate(node.gameObject);removed++;}
                        else{filter.sharedMesh=Store(change);trims++;}
                        Object.DestroyImmediate(change);
                    }
                var wire=root.Find("Continuous_Contact_Wire_Zone")?.GetComponent<MeshFilter>();
                if(wire!=null)
                {
                    var change=TrimLegacyInfrastructure(wire,point=>Project(point,zone.startChainage,zone.endChainage),cuts);
                    if(change!=null)
                    {
                        if(change.vertexCount==0){Object.DestroyImmediate(wire.gameObject);removed++;}
                        else{wire.sharedMesh=Store(change);trims++;}
                        Object.DestroyImmediate(change);
                    }
                }
                foreach(var activation in root.GetComponentsInChildren<RailwayZoneActivation>(true))
                    activation.chunks=activation.chunks.Where(chunk=>chunk!=null).ToArray();
                var meshFilters=holder.GetComponentsInChildren<MeshFilter>();
                metrics.Add($"{zone.zoneId},{meshFilters.Length},{meshFilters.Sum(filter=>filter.sharedMesh.triangles.Length/3)},"+
                    $"{meshFilters.Sum(filter=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(filter.sharedMesh))},{removed},{trims}");
                AssetDatabase.SaveAssetIfDirty(pack);
                planned.Add((scene,root,zone.zoneId));
                Record("ZONE-INSTALL",zone.zoneId,"PASS",$"{meshFilters.Length} new renderers; {removed} obsolete objects; {trims} local mesh overrides.");
            }
            if(productionBatches.Count==0||subset.Any(item=>item.visualBatch!="HIDDEN_IN_VISUAL"&&!productionBatches.Contains(item.visualBatch)))
                throw new InvalidOperationException("Candidate-to-Visual batch mismatch.");
            InstallProductionBim(pilot,loader,subset,saved);
            SyncProductionBimVisuals(planned,saved);
            foreach(var target in planned)
                if(!EditorSceneManager.SaveScene(target.scene))
                    throw new IOException("Could not save "+target.id);
            RailwayBimGeometryOverlapAudit.RepairFacilities();
            RailwayBimGeometryOverlapAudit.RepairSubsumedBridgeFaces();
            RailwayBimGeometryOverlapAudit.RepairRemainingFacilitySeams();
            RailwayBimGeometryOverlapAudit.RepairInternalFacilityBatches();
            WriteProductionRegistry(subset,zoneVisuals);
            File.WriteAllLines(ProdEvidence+"Performance_Comparison.csv",metrics);
            Record("OCL-GA","Station 0–51.897 m","DEFERRED","Candidate station meshes and dependent IDs excluded. Existing OCL retained.");
            Record("SRC-02","Independent candidate geometry","EXCEPTION","User approved the independent parametric subset; external research rights not verified.");
            Record("OCL-03","Electrification","EXCEPTION","Visual only. Physical pantograph contact NOT VERIFIED.");
            Record("STREAM-RUNTIME","Actual train movement","NOT TESTED","The editor install does not exercise runtime loading.");
            File.WriteAllLines(ProdEvidence+"Geometry_Test_Results.csv",rows);
            CaptureProductionSamples(planned,spline,length);
            Debug.Log($"Infrastructure Production installed: {subset.Length} physical IDs and {planned.Count} updated route zones.");
        }
        catch
        {
            foreach(var opened in ownedScenes.Where(scene=>saved.ContainsKey(scene.path)).ToArray())
                if(opened.isLoaded)EditorSceneManager.CloseScene(opened,true);
            foreach(var prior in saved)File.WriteAllBytes(prior.Key,prior.Value);
            AssetDatabase.Refresh();
            throw;
        }
        finally
        {
            if(pilot.IsValid()&&pilot.isLoaded)EditorSceneManager.ClosePreviewScene(pilot);
            foreach(var opened in ownedScenes)if(opened.IsValid()&&opened.isLoaded)EditorSceneManager.CloseScene(opened,true);
            ownedScenes.Clear();
        }
    }
}
