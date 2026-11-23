#if BRIDGE_MODULAR_AUDIT
using System;
using System.Collections;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BridgeModularWebProbe : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (!Application.absoluteURL.Contains("bridgeDrive=1")) return;
        SceneManager.sceneLoaded += (scene, _) =>
        {
            if (scene.name != "YenTuHaLongXanh") return;
            var gameObject = new GameObject("Local bridge Web validation");
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            gameObject.AddComponent<BridgeModularWebProbe>();
        };
    }

    ProfilerRecorder draws, memory;

    IEnumerator Start()
    {
        var seat = FindFirstObjectByType<TrainSeatInteractable>();
        var view = FindFirstObjectByType<TrainExteriorView>();
        if (seat == null || view == null) { Debug.LogError("[BridgeYTH] Missing seat or exterior camera"); yield break; }
        FirstPersonController player = null;
        float deadline = Time.realtimeSinceStartup + 45;
        while (player == null && Time.realtimeSinceStartup < deadline)
        {
            player = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.Object != null && p.Object.IsValid && p.HasStateAuthority);
            if (player == null) yield return null;
        }
        if (player == null) { Debug.LogError("[BridgeYTH] No offline driver"); yield break; }
        seat.RequestOccupy(player);
        yield return new WaitForSecondsRealtime(.8f);
        if (!player.isDriving) { Debug.LogError("[BridgeYTH] Seat request failed"); yield break; }
        var train = seat.trainMover;
        train.AdjustSpeedLevel(4);
        var start = train.currentDistanceMeters;
        Debug.Log(FormattableString.Invariant($"[BridgeYTH] DriveStart,{start:F2}"));
        draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        int missing = 0;
        float next = 3400, max = start;
        bool overhead = false, right = false, cabin = false;
        deadline = Time.realtimeSinceStartup + 180;
        while (player.isDriving && Time.realtimeSinceStartup < deadline && train.currentDistanceMeters < 5170)
        {
            train.SetVRControlInput(Vector2.up);
            float d = train.currentDistanceMeters;
            max = Mathf.Max(max, d);
            if (d > 3910 && !overhead)
            {
                view.ToggleView();
                overhead = view.IsExterior && view.ViewIndex==0;
                if (overhead) Debug.Log("[BridgeYTH] Exterior overhead camera");
            }
            if (d > 4230 && overhead && !right)
            {
                view.ToggleView();
                right = view.IsExterior && view.ViewIndex==1;
                if(right)Debug.Log("[BridgeYTH] Exterior right camera");
            }
            if (d > 4520 && right && !cabin)
            {
                view.ToggleView();
                view.ToggleView();
                cabin=!view.IsExterior;
                if(cabin)Debug.Log("[BridgeYTH] Restored cabin camera");
            }
            if (d >= 3650.01f && d <= 4770 && !Loaded()) missing++;
            if (d >= next)
            {
                next += 100;
                var bridge = GameObject.Find("Bridge_YTH_Visual");
                int renderers=bridge==null?0:bridge.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled && r.gameObject.activeInHierarchy);
                Debug.Log(FormattableString.Invariant($"[BridgeYTH] Sample,{d:F2},{train.currentSpeedKmh:F2},{renderers},{Counter(draws)},{Counter(memory)},{Time.unscaledDeltaTime*1000:F2}"));
            }
            yield return null;
        }
        while (player.isDriving && !train.IsStopped && Time.realtimeSinceStartup < deadline + 30)
        {
            train.SetVRControlInput(Vector2.down);
            yield return null;
        }
        train.SetVRControlInput(Vector2.zero);
        Debug.Log(FormattableString.Invariant($"[BridgeYTH] Result,{start:F2},{max:F2},{train.currentDistanceMeters:F2},missingFrames={missing},overhead={overhead},right={right},cabin={cabin},bridgeLoaded={Loaded()}"));
    }

    static bool Loaded() => SceneManager.GetSceneByName("Zone_018").isLoaded && GameObject.Find("Bridge_YTH_Visual") != null;
    static long Counter(ProfilerRecorder recorder) => recorder.Valid && recorder.Count>0 ? recorder.LastValue : -1;
    void OnDestroy() { if(draws.Valid)draws.Dispose(); if(memory.Valid)memory.Dispose(); }
}
#endif
