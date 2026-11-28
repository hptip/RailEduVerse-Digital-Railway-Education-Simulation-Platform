#if INFRA_PRODUCTION_AUDIT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

// Enabled only for the local development build and explicit test URL.
public sealed class InfrastructureProductionWebProbe : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if(!Application.absoluteURL.Contains("infraDrive=1"))return;
        SceneManager.sceneLoaded+=(scene,_) =>
        {
            if(scene.name!="YenTuHaLongXanh")return;
            var probe=new GameObject("Local infrastructure drive test");
            SceneManager.MoveGameObjectToScene(probe,scene);
            probe.AddComponent<InfrastructureProductionWebProbe>();
        };
    }

    IEnumerator Start()
    {
        float deadline=Time.realtimeSinceStartup+60;
        FirstPersonController player=null;
        TrainSeatInteractable seat=null;
        while(Time.realtimeSinceStartup<deadline)
        {
            player=FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None)
                .FirstOrDefault(item=>item.Object!=null&&item.Object.IsValid&&item.HasStateAuthority);
            seat=FindFirstObjectByType<TrainSeatInteractable>();
            if(player!=null&&seat!=null)break;
            yield return null;
        }
        if(player==null||seat==null){Debug.LogError("[InfraDrive] Missing local player or seat");yield break;}
        seat.RequestOccupy(player);
        yield return new WaitForSecondsRealtime(1f);
        if(!player.isDriving){Debug.LogError("[InfraDrive] Driver seat rejected");yield break;}
        var train=seat.trainMover;
        var view=FindFirstObjectByType<TrainExteriorView>();
        var streaming=FindFirstObjectByType<RailwayZoneStreamingManager>();
        var rail=FindFirstObjectByType<RailwayMicroChunkStreamer>();
        bool full=Application.absoluteURL.Contains("infraFull=1");
        var targets=new[]{(350f,"Subgrade"),(1140f,"Forest_02"),(1480f,"Tunnel_02_Interior"),
            (2230f,"Forest_01"),(2650f,"Tunnel_01_Interior"),(3140f,"OCL_Open"),(3920f,"SO201"),(4255f,"SO201_Exit")};
        if(full)targets=targets.Concat(new[]{(5075f,"Zone_020"),(5575f,"Zone_021"),(6075f,"Zone_022"),
            (6575f,"Zone_023"),(7075f,"Zone_024"),(7575f,"Zone_025"),(7900f,"Zone_026")}).ToArray();
        float finish=full?train.totalRouteLength-5:4900;
        int next=0,maxRenderers=0,missing=0;
        float nextSample=355,speedPeak=0;
        var frames=new List<float>();
        var prior=new HashSet<string>();
        var loaded=new HashSet<string>();
        var unloaded=new HashSet<string>();
        using var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");
        using var triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");
        using var memory=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"Total Used Memory");
        float start=train.currentDistanceMeters;
        bool fast=false;
        deadline=Time.realtimeSinceStartup+360;
        while(player.isDriving&&Time.realtimeSinceStartup<deadline&&train.currentDistanceMeters<finish)
        {
            var d=train.currentDistanceMeters;
            if(d>=442&&!fast)
            {
                train.AdjustSpeedLevel(4);
                fast=true;
            }
            train.SetVRControlInput(Vector2.up);
            speedPeak=Mathf.Max(speedPeak,train.currentSpeedKmh);
            frames.Add(Time.unscaledDeltaTime*1000);
            if(d>=nextSample)
            {
                var now=new HashSet<string>();
                for(int index=0;index<SceneManager.sceneCount;index++)
                {
                    var scene=SceneManager.GetSceneAt(index);
                    if(scene.isLoaded&&scene.name.StartsWith("Zone_",StringComparison.Ordinal))now.Add(scene.name);
                }
                foreach(var zone in now.Except(prior)){loaded.Add(zone);Debug.Log($"[InfraDrive] Load,{zone},{d:F2}");}
                foreach(var zone in prior.Except(now)){unloaded.Add(zone);Debug.Log($"[InfraDrive] Unload,{zone},{d:F2}");}
                prior=now;
                var required=streaming.zones.Last(item=>item.startChainage<=d);
                if(!now.Contains(required.zoneId)){missing++;Debug.LogError($"[InfraDrive] Missing zone {required.zoneId} at {d:F2}");}
                maxRenderers=Math.Max(maxRenderers,FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(item=>item.enabled));
                nextSample=d+50;
            }
            if(next<targets.Length&&d>=targets[next].Item1)
            {
                if(view!=null)
                {
                    if(next is 0 or 3 or 5 or 6 || next >= 8)
                    {
                        if(!view.IsExterior)view.ToggleView();
                        if(next==6)for(int i=0;i<4&&(!view.IsExterior||view.ViewIndex!=3);i++)view.ToggleView();
                    }
                    else for(int i=0;i<5&&view.IsExterior;i++)view.ToggleView();
                }
                Debug.Log(FormattableString.Invariant($"[InfraDrive] View,{targets[next].Item2},{d:F2},{train.currentSpeedKmh:F2},{(view!=null?view.ViewIndex:-1)},{(draws.Valid?draws.LastValue:-1)},{(triangles.Valid?triangles.LastValue:-1)},{(memory.Valid?memory.LastValue:-1)}"));
                next++;
            }
            yield return null;
        }
        train.SetVRControlInput(Vector2.zero);
        frames.Sort();
        float Quantile(float percentile)=>frames.Count==0?0:frames[Mathf.Clamp(Mathf.CeilToInt(frames.Count*percentile)-1,0,frames.Count-1)];
        Debug.Log(FormattableString.Invariant($"[InfraDrive] End,start={start:F2},last={train.currentDistanceMeters:F2},speed={speedPeak:F2},p95={Quantile(.95f):F2},p99={Quantile(.99f):F2},renderers={maxRenderers},loaded={loaded.Count},unloaded={unloaded.Count},missing={missing},windows={next}"));
        if(full&&rail!=null)Debug.Log($"[InfraDrive] Rail pool peak={rail.PeakActiveChunks}, missing essential frames={rail.MissingEssentialFrames}");
    }
}
#endif
