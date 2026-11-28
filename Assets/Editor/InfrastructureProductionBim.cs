using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UttVr.BridgePilot;
using Object=UnityEngine.Object;

static partial class SO201OriginalBridgeAudit
{
    const string ProductionDetailResources="InfrastructureProduction";
    const string BimPrefabRoot="Assets/ExternalBIM/ZoneBased/Resources/RouteBimZones";
    const string ProductionSchema="UTTVR-G2-PRODUCTION-DETAILED";
    [Serializable] sealed class ProductionOrigin { public Vector3 sourceWorldOrigin; }

    static void InstallProductionBim(Scene pilot,InfrastructureDetailedLoader source,
        InfrastructureDetailedLoader.Occurrence[] subset,Dictionary<string,byte[]> saved)
    {
        var resources=ProdAssets+"/Resources/"+ProductionDetailResources;
        var byAsset=subset.ToDictionary(item=>item.assetId,StringComparer.Ordinal);
        var selected=new HashSet<string>(byAsset.Keys,StringComparer.Ordinal);
        var entries=source.catalog.entries.ToDictionary(entry=>entry.globalId,StringComparer.Ordinal);
        foreach(string asset in byAsset.Keys)
        {
            string parent=entries[asset].parentGlobalId;
            while(!string.IsNullOrEmpty(parent)&&selected.Add(parent))parent=entries[parent].parentGlobalId;
        }
        var catalog=ScriptableObject.CreateInstance<BridgeBimCatalog>();
        catalog.schema=ProductionSchema;
        catalog.modeledScale=1;
        catalog.entries=source.catalog.entries.Where(entry=>selected.Contains(entry.globalId)).ToArray();
        AssetDatabase.CreateAsset(catalog,ProdAssets+"/InfrastructureCatalog.asset");
        var lookup=new List<InfrastructureDetailedLoader.Occurrence>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var group in subset.GroupBy(item=>(item.domain,item.chunk)))
            {
                string path=resources+"/"+group.Key.domain+"_"+group.Key.chunk.ToString("D3")+".asset";
                var chunk=ScriptableObject.CreateInstance<InfrastructureDetailChunk>();
                AssetDatabase.CreateAsset(chunk,path);
                var meshes=new Dictionary<Mesh,Mesh>();
                var materials=new Dictionary<Material,Material>();
                var parts=new List<InfrastructureDetailedLoader.Occurrence>();
                foreach(var item in group)
                {
                    InfrastructureDetailedLoader.Occurrence Copy()=>new()
                    {
                        assetId=item.assetId,domain=item.domain,owner=item.owner,zone=item.zone,assembly=item.assembly,
                        sourceId=item.sourceId,visualBatch=item.visualBatch,chunk=item.chunk,chainage=item.chainage,placement=item.placement
                    };
                    if(!meshes.TryGetValue(item.mesh,out var mesh))
                    {
                        mesh=Object.Instantiate(item.mesh);mesh.name=item.mesh.name;
                        AssetDatabase.AddObjectToAsset(mesh,chunk);meshes.Add(item.mesh,mesh);
                    }
                    if(!materials.TryGetValue(item.material,out var material))
                    {
                        material=Object.Instantiate(item.material);material.name=item.material.name;
                        if(material.GetTexturePropertyNames().Any(key=>material.GetTexture(key)!=null))
                            throw new InvalidOperationException("Detailed material has an unapproved texture.");
                        AssetDatabase.AddObjectToAsset(material,chunk);materials.Add(item.material,material);
                    }
                    var actual=Copy();actual.mesh=mesh;actual.material=material;parts.Add(actual);
                    lookup.Add(Copy());
                }
                chunk.entries=parts.ToArray();
                EditorUtility.SetDirty(chunk);
                AssetDatabase.SaveAssetIfDirty(chunk);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        var scene=Open(ProdBimScene);
        saved.Add(ProdBimScene,File.ReadAllBytes(ProdBimScene));
        var viewer=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<RailwayBimZoneViewer>(true)).Single();
        if(viewer.productionLoader!=null)throw new InvalidOperationException("Existing production Detailed loader.");
        var node=new GameObject("Infrastructure Production Detailed");
        SceneManager.MoveGameObjectToScene(node,scene);
        node.transform.SetParent(viewer.transform,false);
        var loader=node.AddComponent<InfrastructureDetailedLoader>();
        loader.catalog=catalog;
        loader.occurrences=lookup.ToArray();
        loader.resourcePrefix=ProductionDetailResources;
        loader.modelOrigin=JsonUtility.FromJson<ProductionOrigin>(File.ReadAllText("Assets/ProjectLopHoc/Generated/QuanLyVongDoiTaiSan/SceneProvenance.json")).sourceWorldOrigin;
        viewer.productionLoader=loader;
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save BIM Scene.");
    }

    static void SyncProductionBimVisuals(List<(Scene scene,Transform root,string id)> installed,Dictionary<string,byte[]> saved)
    {
        foreach(var item in installed)
        {
            string path=BimPrefabRoot+"/"+item.id+".prefab";
            if(!File.Exists(path))continue;
            saved.Add(path,File.ReadAllBytes(path));
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var prod=item.root.Find(ProdInstallRoot);
                var copy=Object.Instantiate(prod.gameObject,prefab.transform,false);
                copy.name=ProdInstallRoot;
                foreach(var node in prefab.transform.Cast<Transform>().Where(node=>node.name.StartsWith("TunnelMountainGroup_",StringComparison.Ordinal)||
                    node.name=="Experience_OverheadPortalSector"||node.name=="Experience_SubgradeReinforcedSlope").ToArray())
                    Object.DestroyImmediate(node.gameObject);
                var legacyMasts=prefab.transform.Find("Catenary_SingleMasts");
                if(legacyMasts!=null)
                    foreach(var child in legacyMasts.Cast<Transform>().ToArray())
                    {
                        var source=item.root.Find("Catenary_SingleMasts/"+child.name)?.GetComponent<MeshFilter>();
                        if(source==null){Object.DestroyImmediate(child.gameObject);continue;}
                        if(child.TryGetComponent<MeshFilter>(out var filter))filter.sharedMesh=source.sharedMesh;
                    }
                var prefabWire=prefab.transform.Find("Continuous_Contact_Wire_Zone");
                if(prefabWire!=null)
                {
                    var source=item.root.Find("Continuous_Contact_Wire_Zone")?.GetComponent<MeshFilter>();
                    if(source==null)Object.DestroyImmediate(prefabWire.gameObject);
                    else prefabWire.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;
                }
                var offset=item.root.position-prefab.transform.position;
                foreach(var target in prefab.GetComponentsInChildren<MeshFilter>(true)
                    .Where(filter=>!filter.transform.IsChildOf(copy.transform)))
                {
                    var source=item.root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(filter=>filter.name==target.name&&
                        filter.sharedMesh!=null&&AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(ProdAssets,StringComparison.Ordinal)&&
                        (filter.transform.position-target.transform.position-offset).sqrMagnitude<.001f);
                    if(source!=null)target.sharedMesh=source.sharedMesh;
                }
                if(PrefabUtility.SaveAsPrefabAsset(prefab,path)==null)throw new IOException(path);
            }
            finally {PrefabUtility.UnloadPrefabContents(prefab);}
        }
    }

    static void WriteProductionRegistry(InfrastructureDetailedLoader.Occurrence[] subset,Dictionary<string,List<MeshFilter>> zoneVisuals)
    {
        var batches=zoneVisuals.SelectMany(zone=>zone.Value.Select(mesh=>(mesh.name,zone.Key)))
            .ToDictionary(item=>item.name,item=>item.Key,StringComparer.Ordinal);
        File.WriteAllLines(ProdEvidence+"Production_Asset_Registry.csv",
            new[]{"asset_id,domain,owner_zone,placement_zone,visual_zone,chunk,chainage,source_kind,source_ref,visual_batch,detailed_ref"}
            .Concat(subset.OrderBy(item=>item.assetId,StringComparer.Ordinal).Select(item=>FormattableString.Invariant(
                $"{Quoted(item.assetId)},{item.domain},{item.owner},{item.zone},{(batches.TryGetValue(item.visualBatch,out var zone)?zone:"")},C{item.chunk:D3},{item.chainage:F3},PARAMETRIC,{Quoted(item.sourceId)},{Quoted(item.visualBatch)},{ProductionDetailResources}/{item.domain}_{item.chunk:D3}"))));
    }
}
