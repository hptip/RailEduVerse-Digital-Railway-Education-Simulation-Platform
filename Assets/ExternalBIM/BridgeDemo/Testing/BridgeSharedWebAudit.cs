using System;
using System.Collections;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace UttVr.BridgePilot
{
    // This component is referenced exclusively by the dedicated local Web audit scene.
    public sealed class BridgeSharedWebAudit : MonoBehaviour
    {
        const string Bim = "QuanLyVongDoiTaiSan";
        const string Route = "YenTuHaLongXanh";
        const string Trial = "Bridge_Visual_Trial";
        ProfilerRecorder batches, memory;
        float nextSample;
        Scene managed;
        Scene visual;
        bool switching;
        bool isRoute;
        TrainSplineMover train;

        IEnumerator Start()
        {
            var bootScene = gameObject.scene;
            DontDestroyOnLoad(gameObject);
            isRoute = Application.absoluteURL.Contains("view=route") || Application.absoluteURL.Contains("view=visual");
            var start = Time.realtimeSinceStartupAsDouble;
            string name = isRoute ? Route : Bim;
            yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
            managed = SceneManager.GetSceneByName(name);
            SceneManager.SetActiveScene(managed);
            yield return SceneManager.UnloadSceneAsync(bootScene);

            if (isRoute)
            {
                train = managed.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TrainSplineMover>(true)).FirstOrDefault();
                if (Application.absoluteURL.Contains("view=visual")) yield return ToggleVisual();
                if (Application.absoluteURL.Contains("drive=1")) StartCoroutine(DriveThroughZone());
            }
            else
            {
                var controller = managed.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RailwayBimController>()).Single();
                float timeout = Time.realtimeSinceStartup + 12;
                while (!controller.Ready && Time.realtimeSinceStartup < timeout) yield return null;
                if (!controller.Ready) { Debug.LogError("[SharedBridge] BIM initialization timed out"); yield break; }
                Debug.Log($"[SharedBridge] BIM ready,{controller.bridge.GetComponentsInChildren<BridgeBimIdentity>().Length},{controller.Store.Assets.Count}");
            }
            try
            {
                batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            }
            catch (ArgumentException error) { Debug.LogWarning(error.Message); }
            nextSample = Time.unscaledTime + .5f;
            Debug.Log(FormattableString.Invariant($"[SharedBridge] Ready,{name},{(Time.realtimeSinceStartupAsDouble - start) * 1000:F0},{SceneManager.sceneCount}"));
        }

        IEnumerator ToggleVisual()
        {
            switching = true;
            if (visual.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(visual);
                visual = default;
                Debug.Log("[SharedBridge] Visual unloaded");
            }
            else
            {
                yield return SceneManager.LoadSceneAsync(Trial, LoadSceneMode.Additive);
                visual = SceneManager.GetSceneByName(Trial);
                Debug.Log("[SharedBridge] Visual ready," + Renderers(visual) + "," +
                    visual.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Collider>().Length) + "," +
                    visual.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<BridgeBimAdapter>().Length));
            }
            switching = false;
        }

        IEnumerator DriveThroughZone()
        {
            var seat = managed.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TrainSeatInteractable>())
                .FirstOrDefault(s => s.trainMover == train);
            if (seat == null) { Debug.LogWarning("[SharedBridge] Missing driver seat for route sample"); yield break; }
            FirstPersonController driver = null;
            var deadline = Time.unscaledTime + 45;
            while (driver == null && Time.unscaledTime < deadline)
            {
                driver = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None)
                    .FirstOrDefault(p => p.Object != null && p.Object.IsValid && p.HasStateAuthority);
                if (driver == null) yield return null;
            }
            if (driver == null) { Debug.LogWarning("[SharedBridge] No local driver for route sample"); yield break; }
            seat.RequestOccupy(driver);
            yield return new WaitForSecondsRealtime(.7f);
            if (!driver.isDriving) { Debug.LogWarning("[SharedBridge] Could not occupy route driver seat"); yield break; }
            train.AdjustSpeedLevel(4);
            var start = train.currentDistanceMeters;
            deadline = Time.unscaledTime + 100;
            while (driver.isDriving && Time.unscaledTime < deadline && train.currentDistanceMeters < start + 630)
            {
                train.SetVRControlInput(Vector2.up);
                yield return null;
            }
            while (driver.isDriving && Time.unscaledTime < deadline && !train.IsStopped)
            {
                train.SetVRControlInput(Vector2.down);
                yield return null;
            }
            train.SetVRControlInput(Vector2.zero);
            Debug.Log(FormattableString.Invariant($"[SharedBridge] Route travel,{start:F1},{train.currentDistanceMeters:F1}"));
        }

        static int Renderers(Scene scene) => scene.GetRootGameObjects()
            .Sum(r => r.GetComponentsInChildren<Renderer>().Count(x => x.enabled));

        void Update()
        {
            if (isRoute && !switching && Keyboard.current?.f9Key.wasPressedThisFrame == true)
                StartCoroutine(ToggleVisual());
            if (nextSample == 0 || Time.unscaledTime < nextSample) return;
            nextSample += 2;
            int renderers = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt).Where(s => s.isLoaded).Sum(Renderers);
            string draws = batches.Valid && batches.Count > 0 ? batches.LastValue.ToString() : "unavailable";
            string bytes = memory.Valid && memory.Count > 0 ? memory.LastValue.ToString() : "unavailable";
            Debug.Log(FormattableString.Invariant($"[SharedBridge] Sample,{Time.unscaledDeltaTime * 1000:F3},{renderers},{draws},{bytes},{SceneManager.sceneCount},{(train != null ? train.currentDistanceMeters : -1):F1}"));
        }

        void OnDestroy()
        {
            if (batches.Valid) batches.Dispose();
            if (memory.Valid) memory.Dispose();
        }
    }
}
