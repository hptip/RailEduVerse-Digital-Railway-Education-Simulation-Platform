using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

static partial class SO201OriginalBridgeAudit
{
    [MenuItem("Tools/UTTVR/Infrastructure/Read-only production preflight")]
    static void ReadOnlyProductionPreflight()
    {
        var candidate=EditorSceneManager.OpenPreviewScene(PilotPath+"/Infrastructure_G2_Pilot.unity");
        var route=EditorSceneManager.OpenPreviewScene(Route);
        try
        {
            var loader=candidate.GetRootGameObjects().Select(go=>go.GetComponent<InfrastructureDetailedLoader>()).Single(item=>item!=null);
            var manager=route.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var spline=manager.trainMover.splineRoute;
            var subset=loader.occurrences.Where(ApprovedOccurrence).ToArray();
            var counts=subset.GroupBy(item=>item.domain).Select(group=>$"{group.Key}={group.Count()}");
            var groups=subset.GroupBy(item=>(item.domain,item.chunk)).ToArray();
            var unique=subset.Select(item=>item.assetId).Distinct(StringComparer.Ordinal).Count();
            if(subset.Length!=unique||manager.zones.Count!=17||subset.Any(item=>item.mesh==null||item.material==null))
                throw new InvalidOperationException("Invalid candidate scope or source mesh binding.");
            if(subset.Any(item=>item.material.GetTexturePropertyNames().Any(key=>item.material.GetTexture(key)!=null)))
                throw new InvalidOperationException("A candidate texture was found.");
            int count=subset.Count(item=>item.visualBatch=="HIDDEN_IN_VISUAL");
            Debug.Log($"Production read-only preflight: {subset.Length}/{loader.occurrences.Length} approved IDs; "+
                $"{loader.occurrences.Length-subset.Length} excluded station records; {string.Join(", ",counts)}; {count} hidden layers; "+
                $"{groups.Length} planned Detailed resources; max {groups.Max(group=>group.Count())} placements in one; 17 runtime zones.");
            var rows=new List<string>();
            try
            {
                ValidateProductionGrade(manager,spline,subset,rows);
            }
            finally
            {
                foreach(string row in rows)Debug.Log(row);
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(route);
            EditorSceneManager.ClosePreviewScene(candidate);
        }
    }
}
