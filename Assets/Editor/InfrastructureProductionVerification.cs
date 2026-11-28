using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UttVr.BridgePilot;

static partial class SO201OriginalBridgeAudit
{
    const string InfraWebPath="Builds/UTTVR_Infrastructure_Production_Web";

    [MenuItem("Tools/UTTVR/Infrastructure/Verify saved production scenes")]
    static void VerifyInstalledInfrastructure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
        var report=ProdEvidence+"Geometry_Test_Results.csv";
        var rows=File.ReadAllLines(report).Where(line=>!line.StartsWith("VERIFY-",StringComparison.Ordinal)).ToList();
        var route=EditorSceneManager.OpenPreviewScene(Route);
        Scene bim=default;
        var paths=new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var manager=route.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            if(manager.zones.Count!=17)throw new InvalidOperationException("Route registry changed.");
            foreach(var item in manager.zones)
            {
                var scene=EditorSceneManager.OpenPreviewScene(item.scenePath);
                try
                {
                    var root=Root(scene);
                    var installed=root.Find(ProdInstallRoot);
                    if(installed==null)continue;
                    if(root.Cast<Transform>().Any(child=>child.name.StartsWith("TunnelMountainGroup_",StringComparison.Ordinal)||
                           child.name=="Experience_OverheadPortalSector"||child.name=="Experience_SubgradeReinforcedSlope"))
                        throw new InvalidOperationException("Legacy duplicate in "+item.zoneId);
                    var meshes=installed.GetComponentsInChildren<MeshFilter>(true);
                    int terrainOnly=root.GetComponentsInChildren<MeshFilter>(true).Count(filter=>filter.sharedMesh!=null&&
                        filter.sharedMesh.name.Contains("Pilot shoulder trim",StringComparison.Ordinal)&&
                        AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(ProdAssets,StringComparison.Ordinal));
                    if((meshes.Length==0&&terrainOnly==0)||meshes.Any(filter=>filter.sharedMesh==null||filter.name.Contains("Station",StringComparison.Ordinal)||
                            !AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(ProdAssets,StringComparison.Ordinal)))
                        throw new InvalidOperationException("Invalid production mesh "+item.zoneId);
                    paths.Add(item.scenePath);
                    rows.Add($"VERIFY-ZONE,{item.zoneId},{meshes.Length},,PASS,{Quoted($"Reopened production scene, {terrainOnly} local shoulder mesh replacements; no legacy route groups.")}");
                }
                finally{EditorSceneManager.ClosePreviewScene(scene);}
            }
            bim=EditorSceneManager.OpenPreviewScene(ProdBimScene);
            var viewer=bim.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<RailwayBimZoneViewer>(true)).Single();
            var loader=viewer.productionLoader;
            var groups=loader.occurrences.GroupBy(item=>(item.domain,item.chunk)).ToArray();
            int total=0;
            long peakMeshBytes=0;
            foreach(var group in groups)
            {
                string path=ProdAssets+"/Resources/"+loader.resourcePrefix+"/"+group.Key.domain+"_"+group.Key.chunk.ToString("D3")+".asset";
                var asset=AssetDatabase.LoadAssetAtPath<InfrastructureDetailChunk>(path);
                if(asset==null||asset.entries.Length!=group.Count()||
                    !asset.entries.Select(item=>item.assetId).ToHashSet(StringComparer.Ordinal).SetEquals(group.Select(item=>item.assetId))||
                    asset.entries.Any(item=>item.mesh==null||item.material==null||!ApprovedOccurrence(item)))
                    throw new InvalidOperationException("Invalid Detailed resource "+path);
                peakMeshBytes=Math.Max(peakMeshBytes,asset.entries.Select(item=>item.mesh).Distinct()
                    .Sum(mesh=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh)));
                total+=asset.entries.Length;
            }
            if(total!=loader.catalog.entries.Count(item=>item.IsElement)||loader.occurrences.Any(item=>item.mesh!=null||item.material!=null))
                throw new InvalidOperationException("Detailed route lookup retains geometry or does not match the catalog.");
            rows.Add($"VERIFY-DETAIL,Production chunks,{total},,PASS,{Quoted($"{groups.Length} separately loadable resources; largest chunk mesh memory {peakMeshBytes} bytes in Unity Editor.")}");
            VerifyProductionWorkbook(loader,rows);
            var dependencies=AssetDatabase.GetAllAssetPaths().Where(path=>path.StartsWith(ProdAssets+"/",StringComparison.Ordinal)&&path.EndsWith(".asset",StringComparison.Ordinal))
                .SelectMany(path=>AssetDatabase.GetDependencies(path,true)).Distinct().ToArray();
            if(dependencies.Any(path=>path.Contains("/Source/",StringComparison.OrdinalIgnoreCase)||path.StartsWith(PilotPath+"/PilotGeometry",StringComparison.Ordinal)||
                path.EndsWith(".ifc",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".blend",StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("An external research dependency is present.");
            rows.Add($"VERIFY-SOURCE,Production assets,0,0,PASS,{Quoted("Recursive dependencies of new packs contain no research imports or Pilot geometry container.")}");
            if(paths.Count<7)throw new InvalidOperationException("Too few route zones have installed candidate geometry.");
            File.WriteAllLines(report,rows);
            File.WriteAllText(ProdEvidence+"Editor_Production_Verified.txt",$"{paths.Count} route zones; {total} physical IDs; {peakMeshBytes} max Editor chunk mesh bytes.");
            Debug.Log($"Infrastructure saved scenes verified: {paths.Count} zones, {total} assets.");
        }
        finally
        {
            if(bim.IsValid())EditorSceneManager.ClosePreviewScene(bim);
            EditorSceneManager.ClosePreviewScene(route);
        }
    }

    static void VerifyProductionWorkbook(InfrastructureDetailedLoader loader,List<string> rows)
    {
        string temp=Path.GetFullPath("Temp/InfrastructureProduction/EditorChecks");
        Directory.CreateDirectory(temp);
        string token=Guid.NewGuid().ToString("N");
        string db=Path.Combine(temp,token+".db"), xlsx=Path.Combine(temp,token+".xlsx");
        var entry=loader.catalog.entries.First(item=>item.IsElement);
        using(var store=new BridgeInfrastructureStore(loader.catalog,db))
        {
            store.SaveTechnical(entry.assetId,"Kiểm thử vòng đời",
                new[]{new BridgeParameter{Name="Bề rộng",Value="25",Unit="cm"}});
            foreach(var stage in RailwayBimStages.All)
                store.SaveStage(entry.assetId,stage.key,"Mô phỏng","Kiểm thử "+stage.key,new Dictionary<string,string>());
            RailwayBimWorkbook.Write(xlsx,RailwayInfrastructureWorkbook.Export(store,entry));
            var imported=RailwayInfrastructureWorkbook.Read(xlsx,entry.assetId);
            imported.Single(sheet=>sheet.name=="Technical").rows[0]["Parameter:Bề rộng"]="28";
            store.ApplyInfrastructureWorkbook(entry.assetId,imported);
        }
        using(var reopened=new BridgeInfrastructureStore(loader.catalog,db))
        {
            if(reopened.Parameters(entry.assetId).Single(item=>item.Name=="Bề rộng").Value!="28"||
                reopened.Parameters(entry.assetId).Single(item=>item.Name=="Bề rộng").Unit!="cm"||
                RailwayBimStages.All.Any(stage=>reopened.Stage(entry.assetId,stage.key).Notes!="Kiểm thử "+stage.key))
                throw new InvalidOperationException("Six-stage workbook round-trip failed.");
        }
        rows.Add($"VERIFY-XLSX,Infrastructure SQLite and workbook,7,7,PASS,{Quoted("Technical and six lifecycle sheets saved, edited, imported and reopened in a disposable project Temp database.")}");
    }

    [MenuItem("Tools/UTTVR/Infrastructure/Build approved Web")]
    static void BuildInfrastructureProductionWeb()
    {
        if(!File.Exists(ProdEvidence+"Editor_Production_Verified.txt"))throw new InvalidOperationException("Production Editor verification must pass before a Web build.");
        var scenes=EditorBuildSettings.scenes.Where(scene=>scene.enabled).Select(scene=>scene.path).ToArray();
        if(scenes.Length==0||Path.GetFileNameWithoutExtension(scenes[0])!="Login"||!scenes.Contains(Route)||
            !scenes.Contains(ProdBimScene)||scenes.Any(path=>path.StartsWith(PilotPath,StringComparison.Ordinal)))
            throw new InvalidOperationException("The existing production build scene list is incorrect.");
        File.WriteAllText(ProdEvidence+"Web_Build_Status.txt","Building");
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes=scenes,locationPathName=InfraWebPath,target=BuildTarget.WebGL,targetGroup=BuildTargetGroup.WebGL,
            options=BuildOptions.Development,extraScriptingDefines=new[]{"BRIDGE_MODULAR_AUDIT","INFRA_PRODUCTION_AUDIT"}
        }).summary;
        File.WriteAllText(ProdEvidence+"Web_Build_Status.txt",$"{result.result}; {result.totalErrors} errors; {result.totalWarnings} warnings; {result.totalSize} bytes; {result.totalTime}");
        if(result.result!=BuildResult.Succeeded)throw new InvalidOperationException("Web build failed.");
    }
}
