using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UttVr.BridgePilot;
using Object = UnityEngine.Object;

public static class RailwayBimZoneBuild
{
    const string Route = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string Bim = "Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity";
    const string Derived = "Assets/ExternalBIM/ZoneBased";
    const string Output = Derived + "/Resources/RouteBimZones";
    const string Reports = "Documentation/BIMExpansion/ZoneBasedBIM";
    const string Job = "Temp/ZoneBasedBim";

    [Serializable] sealed class Provenance { public Vector3 sourceWorldOrigin; }
    [Serializable] sealed class Experience { public float overheadStart, overheadEnd, subgradeStart, subgradeEnd; }

    [InitializeOnLoadMethod]
    static void ObserveLocalJob() => EditorApplication.update += ReadLocalJob;

    static void ReadLocalJob()
    {
        string path = Job + "/request.txt";
        if (!File.Exists(path) || EditorApplication.isUpdating || EditorApplication.isCompiling ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        string action = File.ReadAllText(path).Trim();
        File.Delete(path);
        try
        {
            if (action == "prepare-and-web") PrepareAndWeb();
            if (action == "prepare") Prepare();
            if (action == "web") Web();
        }
        catch (Exception error) { File.WriteAllText(Job + "/Error.txt", error.ToString()); Debug.LogException(error); }
    }

    public static void Prepare()
    {
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Các scene đang mở có thay đổi chưa lưu.");
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory(Reports + "/Screenshots");
        Directory.CreateDirectory(Job);
        AssetDatabase.Refresh();

        var route = EditorSceneManager.OpenScene(Route, OpenSceneMode.Single);
        var manager = route.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
        var zones = manager.zones.OrderBy(z => z.startChainage).ToArray();
        var rails = route.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RailwayMicroChunkStreamer>(true)).Single().data;
        var spline = manager.trainMover.splineRoute;
        const string provenancePath = "Assets/ProjectLopHoc/Generated/QuanLyVongDoiTaiSan/SceneProvenance.json";
        Vector3 reference = JsonUtility.FromJson<Provenance>(File.ReadAllText(provenancePath)).sourceWorldOrigin;
        float length = spline.CalculateLength();

        var facilities = new List<RailwayBimFacility>();
        foreach (var work in manager.fixedInfrastructure)
        {
            if (work.kind == "Bridge" || work.kind == "TunnelMountain")
            {
                string path = work.kind == "Bridge" ? "FixedInfrastructure_Bridge/ExtendedBridgeQuest"
                    : "TunnelMountainGroup_" + work.id.Substring("TunnelMountain_".Length);
                facilities.Add(new RailwayBimFacility { key = work.id, label = work.kind == "Bridge" ? "Cầu chính" :
                    "Hầm " + work.id.Substring("TunnelMountain_".Length), domain = work.kind == "Bridge" ? "Cầu" : "Hầm",
                    owner = work.zoneId, covered = work.zoneIds.Append(work.zoneId).Distinct().ToArray(),
                    start = work.startChainage, end = work.endChainage, bindings = new[] { work.zoneId + "/RuntimeZoneContent/" + path } });
            }
        }

        var experience = JsonUtility.FromJson<Experience>(File.ReadAllText("Assets/ProjectLopHoc/Generated/QuestExperienceSectors/Manifest.json"));
        AddSector("OCL_Portal", "Đoạn cổng OCL", "OCL", experience.overheadStart, experience.overheadEnd,
            new[] { "Zone_016", "Zone_017" }, "Experience_OverheadPortalSector");
        AddSector("Subgrade_Bilateral", "Gia cố mái dốc hai bên", "Subgrade", experience.subgradeStart, experience.subgradeEnd,
            new[] { "Zone_010", "Zone_011" }, "Experience_SubgradeReinforcedSlope", "Zone_011");
        void AddSector(string key, string label, string domain, float start, float end, string[] cover, string binding, string owner = null)
        {
            facilities.Add(new RailwayBimFacility { key = key, label = label, domain = domain, owner = owner ?? cover[0],
                start = start, end = end, covered = cover, bindings = cover.Select(id => id + "/RuntimeZoneContent/" + binding).ToArray() });
        }
        foreach (var f in facilities)
            if (!f.covered.All(id => zones.Any(z => z.zoneId == id))) throw new InvalidOperationException("Unknown dependency " + f.key);

        var viewZones = new List<RailwayBimZone>();
        var evidence = new List<string>();
        var bridge = facilities.Single(f => f.key == "Bridge_Main");
        MeasureBridge(spline, length, rails, bridge, manager.trainMover, evidence);
        var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        foreach (var z in zones)
        {
            var zone = EditorSceneManager.OpenScene(z.scenePath, OpenSceneMode.Additive);
            var root = zone.GetRootGameObjects().Single(g => g.name == "RuntimeZoneContent");
            var channels = new List<string> { "Đường ray" };
            if (facilities.Any(f => f.domain == "Cầu" && f.covered.Contains(z.zoneId))) channels.Add("Cầu");
            if (facilities.Any(f => f.domain == "Hầm" && f.covered.Contains(z.zoneId))) channels.Add("Hầm");
            if (root.transform.Find("Continuous_Contact_Wire_Zone") != null) channels.Add("OCL");
            if (facilities.Any(f => f.domain == "Subgrade" && f.covered.Contains(z.zoneId))) channels.Add("Subgrade");
            foreach (var binding in facilities.SelectMany(f => f.bindings).Where(p => p.StartsWith(z.zoneId + "/RuntimeZoneContent/")))
                if (root.transform.Find(binding.Substring((z.zoneId + "/RuntimeZoneContent/").Length)) == null)
                    throw new InvalidOperationException("Missing saved binding: " + binding);
            Bounds bounds = new Bounds((Vector3)spline.EvaluatePosition(z.startChainage / length) - reference, Vector3.one * 20);
            for (int i = 1; i <= 40; i++)
                bounds.Encapsulate((Vector3)spline.EvaluatePosition(Mathf.Lerp(z.startChainage, z.endChainage, i / 40f) / length) - reference);
            bounds.Expand(new Vector3(80, 35, 80));
            var info = new RailwayBimZone { id = z.zoneId, source = z.scenePath, start = z.startChainage,
                end = z.endChainage, focus = bounds, domains = channels.ToArray() };
            viewZones.Add(info);
            if (z.zoneId != RailwayBimZoneViewer.DetailedZone)
                SaveVisualZone(root, scratch, rails, info, reference, evidence);
            if (z.zoneId == bridge.owner)
            {
                var bridgeRenderer = root.transform.Find("FixedInfrastructure_Bridge/ExtendedBridgeQuest").GetComponent<MeshRenderer>();
                evidence.Add(FormattableString.Invariant($"BridgeRendererBounds={bridgeRenderer.bounds.center:F4} {bridgeRenderer.bounds.size:F4}; materials={string.Join("|", bridgeRenderer.sharedMaterials.Select(m => m.name))}"));
                evidence.Add("Bridge transform " + bridgeRenderer.localToWorldMatrix);
            }
            EditorSceneManager.CloseScene(zone, true);
        }

        EditorSceneManager.OpenScene(Bim, OpenSceneMode.Single);
        var controller = Object.FindFirstObjectByType<RailwayBimController>();
        var current = controller.zoneViewer;
        if (current == null) current = controller.gameObject.AddComponent<RailwayBimZoneViewer>();
        current.controller = controller;
        current.zones = viewZones.ToArray();
        current.facilities = facilities.ToArray();
        current.detailedEnvironment = GameObject.Find("Zone_021_Editable").transform;
        controller.zoneViewer = current;
        if (controller.bridge != null) Object.DestroyImmediate(controller.bridge.gameObject);
        controller.bridge = null;
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        if (!EditorSceneManager.SaveScene(controller.gameObject.scene)) throw new IOException(Bim);

        SaveSandbox(bridge.covered, viewZones);
        WriteMapping(viewZones, facilities);
        File.WriteAllLines(Job + "/Geometry.txt", evidence);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Job + "/Status.txt", $"Prepared {viewZones.Count} zones, {facilities.Count} fixed records, 16 passive prefabs.");
    }

    static void SaveVisualZone(GameObject source, Scene scratch, RailwayMicroChunkData rails, RailwayBimZone zone,
        Vector3 reference, List<string> evidence)
    {
        var copy = Object.Instantiate(source);
        SceneManager.MoveGameObjectToScene(copy, scratch);
        copy.name = zone.id;
        foreach (var gate in copy.GetComponentsInChildren<RailwayZoneActivation>(true))
            foreach (var item in gate.chunks) if (item != null) item.SetActive(true);
        foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
        foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        copy.transform.position -= reference;
        var oldMeshes = source.GetComponentsInChildren<MeshFilter>(true);
        var newMeshes = copy.GetComponentsInChildren<MeshFilter>(true);
        if (oldMeshes.Length != newMeshes.Length) throw new InvalidOperationException("Mesh count differs");
        for (int i = 0; i < oldMeshes.Length; i++)
        {
            var a = oldMeshes[i];
            var b = newMeshes[i];
            if (a.sharedMesh != b.sharedMesh || !a.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(b.GetComponent<MeshRenderer>().sharedMaterials) ||
                (a.transform.position - reference - b.transform.position).sqrMagnitude > .000005f ||
                Quaternion.Angle(a.transform.rotation, b.transform.rotation) > .02f ||
                (a.transform.lossyScale - b.transform.lossyScale).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Source transform/reference differs: " + a.name);
        }

        var trackRoot = new GameObject("RuntimeRailGeometry").transform;
        trackRoot.SetParent(copy.transform, false);
        var chunks = rails.chunks.Where(c => c.end > zone.start + .01f && c.start < zone.end - .01f).ToArray();
        // Deterministic ownership avoids two instances if a rail chunk intersects a zone boundary.
        chunks = chunks.Where(c => c.start >= zone.start - .01f || zone.id == "Zone_010").ToArray();
        foreach (var chunk in chunks)
        {
            AddTrack(chunk.right, "Track_Right");
            AddTrack(chunk.left, "Track_Left");
        }
        void AddTrack(RailwayMicroChunkData.Track track, string name)
        {
            var mesh = Resources.Load<Mesh>(track.meshResource);
            if (mesh == null) throw new FileNotFoundException(track.meshResource);
            var go = new GameObject(name);
            go.transform.SetParent(trackRoot, false);
            go.transform.SetPositionAndRotation(track.position - reference, track.rotation);
            go.transform.localScale = track.scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = track.materials;
        }
        if (PrefabUtility.SaveAsPrefabAsset(copy, Output + "/" + zone.id + ".prefab") == null)
            throw new IOException(zone.id);
        evidence.Add(zone.id + ",same-source-meshes=" + oldMeshes.Length + ",rail-pairs=" + chunks.Length +
            ",active-renderers=" + copy.GetComponentsInChildren<Renderer>().Count(r => r.enabled));
        Object.DestroyImmediate(copy);
    }

    static void MeasureBridge(SplineContainer spline, float length, RailwayMicroChunkData rails, RailwayBimFacility bridge,
        TrainSplineMover train, List<string> lines)
    {
        lines.Add(FormattableString.Invariant($"Spline length={length:F5}; front height={train.frontHeightOffset:F5}; rear adjustment={train.rearYOffset:F5}; car spacing={train.carSpacing:F3}"));
        for (int i = 0; i <= 56; i++)
        {
            float d = Mathf.Lerp(bridge.start, bridge.end, i / 56f);
            float t = d / length;
            var p = (Vector3)spline.EvaluatePosition(t);
            var f = ((Vector3)spline.EvaluateTangent(t)).normalized;
            var other = p - Vector3.Cross(Vector3.up, f).normalized * 6.400022f;
            lines.Add(FormattableString.Invariant($"{d:F5}\t{p.x:F5}\t{p.y:F5}\t{p.z:F5}\t{f.x:F6}\t{f.y:F6}\t{f.z:F6}\t{other.x:F5}\t{other.y:F5}\t{other.z:F5}"));
        }
        foreach (var chunk in rails.chunks.Where(c => c.end > bridge.start && c.start < bridge.end))
            lines.Add(FormattableString.Invariant($"Rail chunk {chunk.start:F4}..{chunk.end:F4}: {chunk.right.meshResource}, {chunk.left.meshResource}"));
    }

    static void SaveSandbox(string[] covered, List<RailwayBimZone> zones)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var host = new GameObject("Bridge replacement sandbox");
        var sandbox = host.AddComponent<RailwayBridgeReplacementSandbox>();
        foreach (string id in covered)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/" + id + ".prefab");
            var zone = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (id == "Zone_018") sandbox.oldBridge = zone.transform.Find("FixedInfrastructure_Bridge/ExtendedBridgeQuest").gameObject;
        }
        var slot = new GameObject("NewBridge - awaiting validated railway geometry");
        slot.SetActive(false);
        var view = new GameObject("Bridge baseline camera").AddComponent<Camera>();
        view.farClipPlane = 2400;
        view.fieldOfView = 52;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = new Color(.64f, .75f, .82f);
        var bounds = zones.Single(z => z.id == "Zone_018").focus;
        var rotation = Quaternion.Euler(30, 30, 0);
        view.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * 1200, rotation);
        var sun = new GameObject("Bridge inspection light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.25f;
        sun.transform.rotation = Quaternion.Euler(52, -28, 0);
        EditorSceneManager.SaveScene(scene, Derived + "/Bridge_Replacement_Sandbox.unity");
    }

    static void WriteMapping(List<RailwayBimZone> zones, List<RailwayBimFacility> facilities)
    {
        static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        var data = new List<string> { "NodeKey,Label,Domain,AssetID,OwnerZoneID,CoveredZoneIDs,ChainageStart,ChainageEnd,GeometryBindings,ParentNodeKey" };
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            data.Add(FormattableString.Invariant($"zone:{z.id},{Q(z.Label(i))},Zone,,{z.id},{z.id},{z.start:R},{z.end:R},{Q(z.source)},route:YTH"));
        }
        foreach (var f in facilities)
            data.Add(FormattableString.Invariant($"{f.key},{Q(f.label)},{f.domain},{f.assetId},{f.owner},{Q(string.Join("|", f.covered))},{f.start:R},{f.end:R},{Q(string.Join("|", f.bindings))},{Q("zone:" + f.owner + ":" + f.domain)}"));
        File.WriteAllLines(Reports + "/Zone_Asset_Mapping.csv", data, new System.Text.UTF8Encoding(true));
    }

    public static void Web()
    {
        Directory.CreateDirectory(Job);
        var paths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (paths.Length == 0 || Path.GetFileNameWithoutExtension(paths[0]) != "Login" ||
            !paths.Contains(Bim) || !paths.Contains(Route)) throw new InvalidOperationException("Use the existing build scene list with Login first.");
        File.WriteAllLines(Job + "/WebScenes.txt", paths);
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = paths, locationPathName = "Builds/ZoneBasedBim_Web", target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL, options = BuildOptions.Development }).summary;
        File.WriteAllText(Job + "/WebStatus.txt", $"{result.result}; {result.totalErrors} errors; {result.totalWarnings} warnings; {result.totalSize} bytes; {result.totalTime}");
        if (result.result != BuildResult.Succeeded) throw new InvalidOperationException("Web build failed: " + result.result);
    }

    [MenuItem("Tools/UTTVR/Zone BIM/Prepare and build Web")]
    public static void PrepareAndWeb() { Prepare(); Web(); }
}
