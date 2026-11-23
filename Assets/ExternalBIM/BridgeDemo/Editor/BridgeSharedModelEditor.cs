using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UttVr.BridgePilot
{
    public static class BridgeSharedModelEditor
    {
        const string Area = "Assets/ExternalBIM/BridgeDemo/";
        const string Source = Area + "BridgeBIM_Demo.prefab";
        const string Detailed = Area + "Bridge_Detailed.prefab";
        const string Visual = Area + "Bridge_Visual.prefab";
        const string Trial = Area + "Testing/Bridge_Visual_Trial.unity";
        const string Boot = Area + "Testing/Bridge_Shared_WebAudit.unity";
        const string BIM = "Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity";
        const string Route = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
        const string TempPath = "Temp/BridgeShared/";
        const string Output = "Builds/BridgeShared_Web";

        static string Hash(string path)
        {
            using var input = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }

        static Dictionary<string, string> Snapshot(IEnumerable<string> inputs) => inputs
            .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal)
            .ToDictionary(p => p, Hash);

        static string[] ProtectedPaths(IEnumerable<string> zones) =>
            new[]
            {
                Area + "Models/Bridge_SO201.dae", Area + "Models/Bridge_SO201.dae.meta", Source,
                Area + "BridgeBIM_Catalog.asset", Route,
                "Assets/ProjectLopHoc/Scenes/Showroom.unity", "Assets/ProjectLopHoc/Scenes/Login.unity",
                "Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_018.unity",
                "Assets/StreamingAssets/RailwayLifecycle/Initial.db",
                "Assets/ProjectLopHoc/Scripts/RailwayZoneStreamingManager.cs",
                "Assets/BuildProfiles/UTTVR Web.asset", "ProjectSettings/EditorBuildSettings.asset",
                "Packages/manifest.json", "Packages/packages-lock.json",
                "UserData/QuanLyVongDoiTaiSan.db",
                "F:/BIM_Workspace/BridgeDemo/original/demo-bridge-so201.ifc"
            }.Concat(zones)
             .Concat(Directory.GetFiles(Area + "Materials", "*.mat"))
             .Concat(AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") &&
                (p.EndsWith("/TrainSplineMover.cs") || p.EndsWith("/SceneQuickMenu.cs") || p.EndsWith("/TrainPassengerCarrier.cs"))))
             .Where(File.Exists).ToArray();

        static Bounds GeometryBounds(GameObject root)
        {
            var all = root.GetComponentsInChildren<MeshRenderer>();
            var bounds = all[0].bounds;
            foreach (var renderer in all) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        public static void Prepare()
        {
            Directory.CreateDirectory(TempPath);
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Close or save the open scene before preparing the bridge.");
            var route = EditorSceneManager.OpenScene(Route, OpenSceneMode.Single);
            string[] zones = ResolveZonePaths(route);

            var before = Snapshot(ProtectedPaths(zones));
            File.WriteAllLines(TempPath + "Protected.tsv", before.Select(p => p.Value + "\t" + p.Key));
            File.WriteAllLines(TempPath + "ZonePaths.txt", zones);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (source == null) throw new FileNotFoundException(Source);
            var empty = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var detail = SaveDetailed(source, empty);
            var visual = SaveVisual(detail);
            VerifyPrefabs(detail, visual);
            SaveTrial(visual);
            SaveBoot();
            SaveBim(detail);
            AssetDatabase.SaveAssets();

            var after = Snapshot(before.Keys);
            foreach (var item in before)
                if (item.Value != after[item.Key]) throw new InvalidOperationException("Protected asset changed: " + item.Key);
            File.WriteAllText(TempPath + "Prepare.txt", $"Saved and validated; {zones.Length} Zone paths; {before.Count} protected SHA-256 checks.");
        }

        static string[] ResolveZonePaths(Scene route)
        {
            var managers = route.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).ToArray();
            if (managers.Length != 1) throw new InvalidOperationException("Expected one railway zone manager.");
            var zones = managers[0].zones.Select(z => z.scenePath).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToArray();
            foreach (string zone in zones)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(zone) == null) throw new FileNotFoundException(zone);
            return zones;
        }

        static GameObject SaveDetailed(GameObject source, Scene scene)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            root.name = "Bridge_Detailed";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Detailed);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject SaveVisual(GameObject detail)
        {
            var root = new GameObject("Bridge_Visual");
            var filters = detail.GetComponentsInChildren<MeshFilter>();
            // Preserve relative TRS (including import orientation and scale), even when a child has no renderer.
            var copies = new Dictionary<Transform, Transform> { [detail.transform] = root.transform };
            Transform CopyTransform(Transform source)
            {
                if (copies.TryGetValue(source, out var existing)) return existing;
                var target = new GameObject("Node " + copies.Count.ToString("D3")).transform;
                target.SetParent(CopyTransform(source.parent), false);
                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
                copies.Add(source, target);
                return target;
            }
            foreach (var filter in filters)
            {
                var original = filter.GetComponent<MeshRenderer>();
                var target = CopyTransform(filter.transform).gameObject;
                target.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = target.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = original.sharedMaterials;
                renderer.shadowCastingMode = original.shadowCastingMode;
                renderer.receiveShadows = original.receiveShadows;
                renderer.lightProbeUsage = original.lightProbeUsage;
                renderer.reflectionProbeUsage = original.reflectionProbeUsage;
                renderer.renderingLayerMask = original.renderingLayerMask;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Visual);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void VerifyPrefabs(GameObject detail, GameObject visual)
        {
            var want = detail.GetComponentsInChildren<MeshFilter>();
            var have = visual.GetComponentsInChildren<MeshFilter>();
            var adapter = detail.GetComponent<BridgeBimAdapter>();
            var ids = detail.GetComponentsInChildren<BridgeBimIdentity>();
            if (adapter == null || want.Length != 91 || have.Length != 91 || ids.Length != 91 ||
                detail.GetComponentsInChildren<MeshCollider>().Length != 91 ||
                !ids.Select(i => i.GlobalId).ToHashSet().SetEquals(adapter.catalog.entries.Where(e => e.IsElement).Select(e => e.globalId)))
                throw new InvalidOperationException("Bridge components or IFC identity count differ.");
            if (visual.GetComponentsInChildren<Component>(true).Any(c => !(c is Transform || c is MeshFilter || c is MeshRenderer)))
                throw new InvalidOperationException("Bridge_Visual includes a non-render component.");
            var remaining = new List<MeshFilter>(have);
            foreach (var mesh in want)
            {
                var material = mesh.GetComponent<MeshRenderer>().sharedMaterials;
                var match = remaining.Find(h => mesh.sharedMesh == h.sharedMesh &&
                    h.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(material) &&
                    MatrixError(mesh.transform.localToWorldMatrix, h.transform.localToWorldMatrix) < 0.0001f);
                if (match == null || AssetDatabase.GetAssetPath(mesh.sharedMesh) != Area + "Models/Bridge_SO201.dae")
                    throw new InvalidOperationException("Shared mesh, materials or transform differ for " + mesh.name);
                remaining.Remove(match);
            }
            var a = GeometryBounds(detail);
            var b = GeometryBounds(visual);
            if ((a.center - b.center).sqrMagnitude > 1e-8f || (a.size - b.size).sqrMagnitude > 1e-8f)
                throw new InvalidOperationException("Prefab bounds differ.");
            File.WriteAllText(TempPath + "PrefabValidation.txt",
                $"91 exact mesh and ordered material reference matches; 91 local-to-world matches; bounds {a.size:F5}; visual metadata/colliders 0.");
        }

        static float MatrixError(Matrix4x4 a, Matrix4x4 b)
        {
            float max = 0;
            for (int i = 0; i < 16; i++) max = Mathf.Max(max, Mathf.Abs(a[i] - b[i]));
            return max;
        }

        static void SaveTrial(GameObject prefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bridge = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            bridge.transform.position = new Vector3(0, 9000, 0); // isolated additive inspection space, far outside the operating line
            var bounds = GeometryBounds(bridge);
            var camera = new GameObject("Bridge Visual Audit Camera").AddComponent<Camera>();
            camera.fieldOfView = 48;
            camera.depth = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(68, 99, 128, 255);
            camera.farClipPlane = 260;
            var rotation = Quaternion.Euler(22, 25, 0);
            float distance = RailwayBimCamera.FitDistance(bounds, rotation, 48, 1.6f, .82f, .88f);
            camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
            var light = new GameObject("Bridge Visual Audit Light").AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 10500f;
            light.range = 155f;
            light.transform.position = bounds.center + new Vector3(5, 42, -30);
            if (!EditorSceneManager.SaveScene(scene, Trial)) throw new IOException(Trial);
        }

        static void SaveBoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Local shared bridge audit").AddComponent<BridgeSharedWebAudit>();
            if (!EditorSceneManager.SaveScene(scene, Boot)) throw new IOException(Boot);
        }

        static void SaveBim(GameObject detail)
        {
            var scene = EditorSceneManager.OpenScene(BIM, OpenSceneMode.Single);
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RailwayBimController>(true)).Single();
            if (controller.bridge == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(detail, scene);
                var bounds = controller.catalog.segments[0].bounds;
                foreach (var segment in controller.catalog.segments) bounds.Encapsulate(segment.bounds);
                instance.transform.position = new Vector3(bounds.center.x, bounds.max.y + 5, bounds.min.z - 100);
                controller.bridge = instance.GetComponent<BridgeBimAdapter>();
                EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException(BIM);
        }

        public static void BuildWeb()
        {
            Directory.CreateDirectory(TempPath);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Set the isolated build target to WebGL.");
            var route = SceneManager.GetSceneByPath(Route);
            bool opened = !route.IsValid() || !route.isLoaded;
            if (opened) route = EditorSceneManager.OpenScene(Route, OpenSceneMode.Additive);
            string[] zones;
            try { zones = ResolveZonePaths(route); }
            finally { if (opened) EditorSceneManager.CloseScene(route, true); }
            var scenes = new[] { Boot, BIM, Route, Trial }.Concat(zones).ToArray();
            File.WriteAllText(TempPath + "BuildStatus.txt", "Building");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = Output, target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL, options = BuildOptions.Development
            }).summary;
            File.WriteAllText(TempPath + "BuildStatus.txt", $"{result.result}; {result.totalErrors} errors; {result.totalWarnings} warnings; {result.totalSize} bytes; {result.totalTime}");
            if (result.result != BuildResult.Succeeded) throw new InvalidOperationException("Shared Bridge Web build " + result.result);
        }

        public static void PrepareAndBuild()
        {
            Prepare();
            BuildWeb();
        }
    }
}
