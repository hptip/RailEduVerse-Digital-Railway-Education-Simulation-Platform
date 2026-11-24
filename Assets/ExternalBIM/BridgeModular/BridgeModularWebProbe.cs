#if BRIDGE_MODULAR_AUDIT
using System;
using System.Collections;
using System.Collections.Generic;
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

    ProfilerRecorder draws, memory, triangles, mainThread;

    sealed class Window
    {
        public readonly string label;
        public readonly float chainage;
        public readonly List<float> frame = new List<float>();
        public readonly List<long> draw = new List<long>(), triangle = new List<long>(), used = new List<long>(), cpu = new List<long>();
        public Window(string label, float chainage) { this.label = label; this.chainage = chainage; }
    }

    static readonly (string Label, float Chainage)[] Stations =
    {
        ("cabin-entry", 3700), ("overhead-middle", 4080), ("outside-middle", 4300),
        ("cabin-middle", 4560), ("cabin-exit", 4710)
    };

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
        triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        var windows = Stations.Select(x => new Window(x.Label, x.Chainage)).ToArray();
        int missing = 0;
        float next = 3400, max = start;
        bool overhead = false, right = false, cabin = false;
        bool timed = Application.absoluteURL.Contains("bridgePerf=1");
        double addedAt = 0;
        SceneManager.sceneLoaded += MeasureLoad;
        SceneManager.sceneUnloaded += MeasureUnload;
        MeshRenderer[] renderers = Array.Empty<MeshRenderer>();
        GameObject cachedBridge = null;
        void MeasureLoad(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Zone_018") return;
            addedAt = Time.realtimeSinceStartupAsDouble;
            Debug.Log(FormattableString.Invariant($"[BridgeYTH] ZoneAdded,{train.currentDistanceMeters:F2}"));
        }
        void MeasureUnload(Scene scene)
        {
            if (scene.name != "Zone_018") return;
            Debug.Log(FormattableString.Invariant($"[BridgeYTH] ZoneRemoved,{train.currentDistanceMeters:F2}"));
        }
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
            if (timed)
            {
                if (cachedBridge == null && SceneManager.GetSceneByName("Zone_018").isLoaded)
                {
                    cachedBridge = GameObject.Find("Bridge_YTH_Visual");
                    if (cachedBridge != null)
                    {
                        renderers = cachedBridge.GetComponentsInChildren<MeshRenderer>();
                        LogStructure(renderers, d, addedAt);
                    }
                }
                foreach (var window in windows)
                {
                    if (d < window.chainage - 25f || d > window.chainage + 25f) continue;
                    if (window.frame.Count == 0) LogCamera(window.label, d, view.ViewIndex, renderers);
                    window.frame.Add(Time.unscaledDeltaTime * 1000);
                    window.draw.Add(Counter(draws));
                    window.triangle.Add(Counter(triangles));
                    window.used.Add(Counter(memory));
                    window.cpu.Add(Counter(mainThread));
                }
            }
            if (d >= 3650.01f && d <= 4770 && !Loaded()) missing++;
            if (d >= next)
            {
                next += 100;
                var bridge = GameObject.Find("Bridge_YTH_Visual");
                int activeRenderers=bridge==null?0:bridge.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled && r.gameObject.activeInHierarchy);
                Debug.Log(FormattableString.Invariant($"[BridgeYTH] Sample,{d:F2},{train.currentSpeedKmh:F2},{activeRenderers},{Counter(draws)},{Counter(memory)},{Time.unscaledDeltaTime*1000:F2}"));
            }
            yield return null;
        }
        while (player.isDriving && !train.IsStopped && Time.realtimeSinceStartup < deadline + 30)
        {
            train.SetVRControlInput(Vector2.down);
            yield return null;
        }
        train.SetVRControlInput(Vector2.zero);
        SceneManager.sceneLoaded -= MeasureLoad;
        SceneManager.sceneUnloaded -= MeasureUnload;
        foreach (var window in windows.Where(x => x.frame.Count > 0))
            Debug.Log(FormattableString.Invariant(
                $"[BridgeYTH] Performance,{window.label},{window.chainage:F0},{window.frame.Count},{Percentile(window.frame,.5):F3},{Percentile(window.frame,.95):F3},{Percentile(window.frame,.99):F3},{Median(window.draw)},{Median(window.triangle)},{Median(window.used)},{Median(window.cpu)}"));
        Debug.Log(FormattableString.Invariant($"[BridgeYTH] Result,{start:F2},{max:F2},{train.currentDistanceMeters:F2},missingFrames={missing},overhead={overhead},right={right},cabin={cabin},bridgeLoaded={Loaded()}"));
    }

    static double Percentile(List<float> values, double p)
    {
        values.Sort();
        return values[Math.Max(0, (int)Math.Ceiling(values.Count*p) - 1)];
    }

    static long Median(List<long> values)
    {
        var sorted = values.Where(value => value >= 0).OrderBy(value => value).ToArray();
        return sorted.Length == 0 ? -1 : sorted[sorted.Length / 2];
    }

    static void LogStructure(MeshRenderer[] renderers, float distance, double addedAt)
    {
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        int submeshes = 0;
        long tris = 0, vertices = 0;
        var materials = new HashSet<Material>();
        foreach (var renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            submeshes += mesh.subMeshCount;
            vertices += mesh.vertexCount;
            for (int sub = 0; sub < mesh.subMeshCount; sub++) tris += mesh.GetIndexCount(sub) / 3;
            foreach (var material in renderer.sharedMaterials) materials.Add(material);
        }
        double observedMs = addedAt > 0 ? (Time.realtimeSinceStartupAsDouble - addedAt) * 1000 : -1;
        Debug.Log(FormattableString.Invariant(
            $"[BridgeYTH] Structure,{distance:F2},{renderers.Length},{vertices},{tris},{materials.Count},{submeshes},{bounds.center.x:F4},{bounds.center.y:F4},{bounds.center.z:F4},{bounds.size.x:F4},{bounds.size.y:F4},{bounds.size.z:F4},{observedMs:F2}"));
    }

    static void LogCamera(string label, float distance, int index, MeshRenderer[] renderers)
    {
        var camera = Camera.main;
        if (camera == null) return;
        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
        int inFrustum = 0;
        long trianglesInFrustum = 0;
        foreach (var renderer in renderers)
        {
            if (!GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) continue;
            inFrustum++;
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            for (int sub = 0; sub < mesh.subMeshCount; sub++) trianglesInFrustum += mesh.GetIndexCount(sub) / 3;
        }
        Debug.Log(FormattableString.Invariant(
            $"[BridgeYTH] Camera,{label},{distance:F2},{index},{inFrustum},{trianglesInFrustum}"));
    }

    static bool Loaded() => SceneManager.GetSceneByName("Zone_018").isLoaded && GameObject.Find("Bridge_YTH_Visual") != null;
    static long Counter(ProfilerRecorder recorder) => recorder.Valid && recorder.Count>0 ? recorder.LastValue : -1;
    void OnDestroy()
    {
        if(draws.Valid) draws.Dispose();
        if(memory.Valid) memory.Dispose();
        if(triangles.Valid) triangles.Dispose();
        if(mainThread.Valid) mainThread.Dispose();
    }
}
#endif
