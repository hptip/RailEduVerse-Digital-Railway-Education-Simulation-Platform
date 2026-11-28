using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UttVr.BridgePilot;
using Object = UnityEngine.Object;

static partial class SO201OriginalBridgeAudit
{
    const string ElectrificationRoot = "OCL_Route_Completion";
    const string TerminalTerrain = "Station_Terrain_Backdrop";
    const string ElectrificationAssets = ProdAssets + "/RouteElectrification";
    static readonly string[] LegacyCatenary = {
        "Catenary_SingleMasts", "Continuous_Contact_Wire_Zone", "Experience_OverheadPortalSector"
    };

    [MenuItem("Tools/UTTVR/Infrastructure/Complete route electrification")]
    static void CompleteRouteElectrification()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Route electrification requires Edit Mode.");
        var preview = EditorSceneManager.OpenPreviewScene(Route);
        Scene review = default, work = default;
        RoutePilot pilot = null;
        var inputs = new List<Scene>();
        var baked = new List<Mesh>();
        try
        {
            var manager = preview.GetRootGameObjects().SelectMany(g =>
                g.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var zones = manager.zones.ToArray();
            if (zones.Length != 17 || zones.Any(z => SceneManager.GetSceneByPath(z.scenePath).isDirty))
                throw new InvalidOperationException("Expected the 17 route zones with no unsaved changes.");
            review = EditorSceneManager.OpenPreviewScene(InfraScene);
            work = EditorSceneManager.NewPreviewScene();
            pilot = new RoutePilot(work, manager, review, null);
            foreach (var zone in zones) inputs.Add(EditorSceneManager.OpenPreviewScene(zone.scenePath));
            var roots = inputs.Select(Root).ToArray();
            var grounds = roots.Select((root, i) =>
            {
                var terrain = root.Find("Terrain" + zones[i].zoneId);
                var transitions = root.Find("TerrainTransitions");
                return root.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f => f.sharedMesh != null && f.sharedMesh.vertexCount > 0 && (f.transform.IsChildOf(terrain) ||
                        transitions != null && f.transform.IsChildOf(transitions) ||
                        f.name.StartsWith("Subgrade", StringComparison.Ordinal) ||
                        f.name.StartsWith("SO201_Final_Approach_", StringComparison.Ordinal)))
                    .Select(ReadSurface).ToArray();
            }).ToArray();
            var firstStation = ReadTerminal(pilot, roots[0].Find("Station_Start_TrainRide"), 0, -1);
            var lastStation = ReadTerminal(pilot, roots[^1].Find("Station_End_TrainRide"), pilot.length, 1);
            var stationStart = firstStation.terminal;
            var stationEnd = lastStation.terminal;
            var sectors = JsonUtility.FromJson<PilotSectors>(
                File.ReadAllText("Assets/ProjectLopHoc/Generated/QuestExperienceSectors/Manifest.json"));
            var bridge = manager.fixedInfrastructure.Single(item => item.kind == "Bridge");
            var covered = new List<(float start, float end)> {
                (sectors.overheadStart, sectors.overheadEnd),
                (bridge.startChainage + 10f, bridge.endChainage - 10f)
            };
            covered.AddRange(manager.fixedInfrastructure.Where(item => item.kind == "TunnelMountain")
                .Select(item => (item.startChainage - 33f, item.endChainage + 33f)));
            covered.Sort((a, b) => a.start.CompareTo(b.start));
            if (stationStart >= covered[0].start || stationEnd <= covered[^1].end ||
                covered.Zip(covered.Skip(1), (a, b) => a.end >= b.start).Any(x => x))
                throw new InvalidOperationException("Unexpected OCL ownership intervals.");

            var registry = File.ReadAllLines(ProdEvidence + "Production_Asset_Registry.csv");
            if (registry.Count(row => row.Contains("-WireL-Contact\",OCL,")) < 60 ||
                roots.Sum(root => root.GetComponentsInChildren<MeshFilter>(true).Count(f => f.name.StartsWith("OCL_"))) < 200)
                throw new InvalidOperationException("Existing OCL batches needed at interfaces are missing.");

            var open = pilot.templates.Single(item => item.Key.EndsWith("_OpenTrack", StringComparison.Ordinal)).Value;
            var portal = pilot.templates.Single(item => item.Key.EndsWith("_Station", StringComparison.Ordinal)).Value;
            var batches = new Dictionary<(int zone, int chunk, Material material), List<Mesh>>();
            var report = new List<string>();
            var stationLine = (start: stationStart, end: stationEnd);
            var allGround = grounds.SelectMany(x => x).ToArray();
            int spans = 0, supports = 0;
            float worstDeviation = 0, maxFoundationDepth = 0;

            void Batch(float distance, Material material, Mesh mesh)
            {
                int index = Array.FindLastIndex(zones, zone => zone.startChainage <= distance);
                var key = (index, Mathf.FloorToInt(distance / 125f), material);
                if (!batches.TryGetValue(key, out var list)) batches[key] = list = new List<Mesh>();
                list.Add(mesh);
            }

            Vector3 Attachment(float distance, int side, bool messenger) =>
                pilot.World(distance, side * pilot.spacing * .5f - .105f, messenger ? pilot.messenger : pilot.wire);

            void AddSupport(float distance, bool terminal)
            {
                var parts = (terminal ? portal : open).Where(part => part.slot.StartsWith("P0", StringComparison.Ordinal)).ToArray();
                var shifts = new Dictionary<string, (float top, float rise)>();
                foreach (var part in parts.Where(x => x.slot.EndsWith("-Foundation", StringComparison.Ordinal)))
                {
                    var mesh = pilot.Warp(part, distance, -10.5f);
                    var bounds = mesh.bounds;
                    var floor = bounds.min.y;
                    var hits = mesh.vertices.Where(p => p.y < floor + .001f).Distinct()
                        .Select(point => pilot.Surface(allGround, point)).Where(float.IsFinite).ToArray();
                    if (hits.Length == 0)
                        throw new InvalidOperationException($"No ground beneath {part.slot} at {distance:F2} m.");
                    float rise = Mathf.Max(0, hits.Max() + .06f - bounds.max.y);
                    var bottom = Mathf.Min(floor, hits.Min() - .03f);
                    maxFoundationDepth = Mathf.Max(maxFoundationDepth, bounds.max.y + rise - bottom);
                    shifts.Add(part.slot.Substring(0, 3), (bounds.max.y, rise));
                    mesh.vertices = mesh.vertices.Select(v =>
                        new Vector3(v.x, v.y < floor + .001f ? bottom : v.y + rise, v.z)).ToArray();
                    mesh.RecalculateBounds();
                    Batch(distance, part.material, mesh);
                }
                foreach (var part in parts.Where(x => !x.slot.EndsWith("-Foundation", StringComparison.Ordinal)))
                {
                    var mesh = pilot.Warp(part, distance, -10.5f);
                    if (shifts.TryGetValue(part.slot.Substring(0, 3), out var foot) && foot.rise > 0)
                    {
                        float top = mesh.bounds.max.y;
                        if (part.slot.EndsWith("-Mast", StringComparison.Ordinal))
                        {
                            if (foot.top + foot.rise > top - 1f)
                                throw new InvalidOperationException($"Ground obstructs the mast at {distance:F2} m.");
                            mesh.vertices = mesh.vertices.Select(v => v + Vector3.up * foot.rise *
                                (1f - Mathf.InverseLerp(foot.top, top - .65f, v.y))).ToArray();
                            mesh.RecalculateNormals();
                        }
                        else if (part.slot.Contains("-Anchor") || part.slot.Contains("-Nut") || part.slot.EndsWith("-BasePlate"))
                            mesh.vertices = mesh.vertices.Select(v => v + Vector3.up * foot.rise).ToArray();
                        mesh.RecalculateBounds();
                    }
                    Batch(distance, part.material, mesh);
                }
                supports++;
            }

            void AddSpan(float a, float b)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    var tag = side < 0 ? "WireL" : "WireR";
                    Vector3 Wire(float t, bool heavy) =>
                        Vector3.Lerp(Attachment(a, side, heavy), Attachment(b, side, heavy), t)
                        - Vector3.up * (heavy ? 1.12f * t * (1f - t) : 0);
                    void Put(string slot, Mesh mesh)
                    {
                        pilot.temporary.Add(mesh);
                        Batch((a + b) / 2f, open.Single(part => part.slot == slot).material, mesh);
                    }
                    Put(tag + "-Contact", InfraPipe(new[] { Wire(0, false), Wire(1, false) }, .016f));
                    Put(tag + "-Messenger", InfraPipe(Enumerable.Range(0, 17)
                        .Select(i => Wire(i / 16f, true)).ToArray(), .017f));
                    for (int drop = 1; drop <= 5; drop++)
                    {
                        float t = drop / 6f;
                        var top = Wire(t, true);
                        Put(tag + "-Dropper" + drop, InfraPipe(new[] { Wire(t, false), top }, .011f));
                        var along = (Attachment(b, side, true) - Attachment(a, side, true)).normalized * .046f;
                        Put(tag + "-Clamp" + drop, InfraCollar(top - along, top + along, .024f, .039f));
                    }
                }
                spans++;
            }

            float Deviation(float a, float b)
            {
                float maximum = 0;
                foreach (int side in new[] { -1, 1 })
                    for (int i = 1; i < 8; i++)
                    {
                        float t = i / 8f;
                        var line = Vector3.Lerp(Attachment(a, side, false), Attachment(b, side, false), t);
                        maximum = Mathf.Max(maximum, Vector3.Distance(line, Attachment(Mathf.Lerp(a, b, t), side, false)));
                    }
                return maximum;
            }

            float from = stationLine.start;
            AddSupport(from, true);
            foreach (var range in covered.Append((start: stationLine.end, end: stationLine.end)))
            {
                int count = Mathf.Max(1, Mathf.CeilToInt((range.start - from) / 42f));
                void Connect(float a, float b)
                {
                    float offset = Deviation(a, b);
                    if (offset > .22f && b - a > 9f)
                    {
                        float mid = (a + b) * .5f;
                        AddSupport(mid, false);
                        Connect(a, mid);
                        Connect(mid, b);
                        return;
                    }
                    if (offset > .22f) throw new InvalidOperationException($"Wire deviates {offset:F3} m at {a:F2} m.");
                    worstDeviation = Mathf.Max(worstDeviation, offset);
                    AddSpan(a, b);
                }
                float previous = from;
                for (int i = 1; i <= count; i++)
                {
                    var current = Mathf.Lerp(from, range.start, i / (float)count);
                    if (i < count) AddSupport(current, false);
                    Connect(previous, current);
                    previous = current;
                }
                report.Add(FormattableString.Invariant($"New spans: {from:F3}–{range.start:F3} m; adjacent existing OCL: {range.start:F3}–{range.end:F3} m."));
                from = range.end;
            }
            AddSupport(stationLine.end, true);

            var generated = new List<(int zone, string name, Material material, Mesh mesh)>();
            foreach (var (key, meshes) in batches)
            {
                var name = $"OCL_Continuous_C{key.chunk:D3}_{key.material.name}";
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(meshes.Select(source => new CombineInstance
                    { mesh = source, transform = Matrix4x4.identity }).ToArray(), true, false);
                baked.Add(mesh);
                generated.Add((key.zone, name, key.material, mesh));
            }
            foreach (var (station, index) in new[] { (firstStation, 0), (lastStation, zones.Length - 1) })
            {
                var mesh = MakeTerminalTerrain(pilot, station, grounds[index]);
                mesh.name = TerminalTerrain;
                baked.Add(mesh);
                var ground = roots[index].Find("Terrain" + zones[index].zoneId).GetComponentInChildren<MeshRenderer>(true);
                generated.Add((index, TerminalTerrain, ground.sharedMaterial, mesh));
                report.Add(FormattableString.Invariant(
                    $"{zones[index].zoneId}: station edge {station.rear:F2} m from spline end; OCL terminates at {station.terminal:F2} m. Terrain behind station; crest {mesh.bounds.max.y:F2} m."));
            }

            InstallElectrification(zones, generated);
            report.Insert(0, FormattableString.Invariant(
                $"{zones.Length} active zones; {supports} new supports, {spans} new two-track spans, {batches.Count} batches. Max chord deviation {worstDeviation:F3} m; maximum foundation depth {maxFoundationDepth:F2} m."));
            File.WriteAllLines("Temp/RailwayElectrification_Verified.txt", report);
        }
        finally
        {
            foreach (var mesh in baked) if (!EditorUtility.IsPersistent(mesh)) Object.DestroyImmediate(mesh);
            if (pilot != null) foreach (var mesh in pilot.temporary) Object.DestroyImmediate(mesh);
            foreach (var scene in inputs) EditorSceneManager.ClosePreviewScene(scene);
            if (work.IsValid()) EditorSceneManager.ClosePreviewScene(work);
            if (review.IsValid()) EditorSceneManager.ClosePreviewScene(review);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static ContactMesh ReadSurface(MeshFilter filter) => ContactPart(filter.name, filter.name, "",
        filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray(), filter.sharedMesh.triangles);

    static (float endpoint, int outward, float rear, float terminal, float halfWidth, float roof) ReadTerminal(
        RoutePilot pilot, Transform station, float endpoint, int outward)
    {
        var pose = pilot.Frame(endpoint);
        var vertices = station.GetComponentsInChildren<MeshFilter>().SelectMany(filter =>
            filter.sharedMesh.vertices.Select(filter.transform.TransformPoint)).Select(point =>
                Quaternion.Inverse(pose.rotation) * (point - pose.point)).ToArray();
        float rear = vertices.Max(p => outward * p.z);
        float face = endpoint + (outward < 0 ? vertices.Max(p => p.z) + 3f : vertices.Min(p => p.z) - 3f);
        return (endpoint, outward, rear, face, vertices.Max(p => Mathf.Abs(p.x)), vertices.Max(p => p.y) + pose.point.y);
    }

    static Mesh MakeTerminalTerrain(RoutePilot pilot,
        (float endpoint, int outward, float rear, float terminal, float halfWidth, float roof) station, ContactMesh[] ground)
    {
        var pose = pilot.Frame(station.endpoint);
        const int rows = 20, cols = 34;
        const float depth = 92f, groundApron = 20f;
        float halfWidth = station.halfWidth + 38;
        var vertices = new List<Vector3>((rows + 1) * (cols + 1));
        var colors = new List<Color32>(vertices.Capacity);
        var indices = new List<int>();
        for (int row = 0; row <= rows; row++)
        {
            float run = (depth + groundApron) * row / rows;
            float u = Mathf.Clamp01((run - groundApron) / depth);
            for (int col = 0; col <= cols; col++)
            {
                float v = col * 2f / cols - 1f;
                var point = pose.point + pose.rotation * new Vector3(v * halfWidth, 0,
                    station.outward * (station.rear + 6f - groundApron + run));
                var baseY = pilot.Surface(ground, point);
                if (!float.IsFinite(baseY)) baseY = pose.point.y - .6f;
                if (run <= groundApron)
                    baseY = Mathf.Min(baseY, pose.point.y - .6f);
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - Mathf.Abs(v)) / .36f));
                float longitudinal = Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * u)), 1.3f);
                var height = Mathf.Max(15, station.roof + 7 - baseY) * edge * longitudinal *
                    (.90f + .10f * Mathf.Cos(v * 7f - u * 3f));
                point.y = baseY - .08f + height;
                vertices.Add(point);
                colors.Add(Color.Lerp(new Color(.36f, .45f, .26f), new Color(.28f, .37f, .22f),
                    Mathf.Clamp01(height / 18f)));
                if (row == 0 || col == 0) continue;
                int d = vertices.Count - 1, c = d - 1, b = d - cols - 1, a = b - 1;
                indices.AddRange(station.outward > 0 ? new[] {a,c,b,b,c,d} : new[] {a,b,c,b,d,c});
            }
        }
        var mesh = InfraMesh(vertices, indices);
        mesh.SetColors(colors);
        if (mesh.bounds.max.y < station.roof + 2)
            throw new InvalidOperationException("Terminal scenery does not cover the station roof sightline.");
        return mesh;
    }

    static void InstallElectrification(RailwayZoneStreamingManager.ZoneRecord[] zones,
        List<(int zone, string name, Material material, Mesh mesh)> generated)
    {
        if (!AssetDatabase.IsValidFolder(ElectrificationAssets))
            AssetDatabase.CreateFolder(ProdAssets, "RouteElectrification");
        var previous = zones.ToDictionary(zone => zone.scenePath, zone => File.ReadAllBytes(zone.scenePath));
        var owned = new List<Scene>();
        try
        {
            for (int i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                var scene = SceneManager.GetSceneByPath(zone.scenePath);
                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(zone.scenePath, OpenSceneMode.Additive);
                    owned.Add(scene);
                }
                var root = Root(scene);
                var contents = generated.Where(item => item.zone == i).ToArray();
                var packPath = $"{ElectrificationAssets}/{zone.zoneId}.asset";
                var pack = AssetDatabase.LoadAssetAtPath<BridgeBimCatalog>(packPath);
                if (pack == null && contents.Length > 0)
                {
                    pack = ScriptableObject.CreateInstance<BridgeBimCatalog>();
                    pack.name = zone.zoneId + " Route Visuals";
                    AssetDatabase.CreateAsset(pack, packPath);
                }
                var savedMeshes = AssetDatabase.LoadAllAssetsAtPath(packPath).OfType<Mesh>().ToDictionary(mesh => mesh.name);
                foreach (var name in LegacyCatenary.Append(ElectrificationRoot))
                    if (root.Find(name) is { } old && old != null) Object.DestroyImmediate(old.gameObject);
                if (contents.Length > 0)
                {
                    var holder = new GameObject(ElectrificationRoot);
                    SceneManager.MoveGameObjectToScene(holder, scene);
                    holder.transform.SetParent(root, false);
                    foreach (var item in contents)
                    {
                        if (savedMeshes.TryGetValue(item.name, out var mesh)) EditorUtility.CopySerialized(item.mesh, mesh);
                        else
                        {
                            mesh = item.mesh;
                            AssetDatabase.AddObjectToAsset(mesh, pack);
                        }
                        var node = new GameObject(item.name);
                        SceneManager.MoveGameObjectToScene(node, scene);
                        node.transform.SetParent(holder.transform, false);
                        node.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = node.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = item.material;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
                foreach (var activation in root.GetComponentsInChildren<RailwayZoneActivation>(true))
                    activation.chunks = activation.chunks.Where(chunk => chunk != null).ToArray();
                if (pack != null)
                {
                    EditorUtility.SetDirty(pack);
                    AssetDatabase.SaveAssetIfDirty(pack);
                }
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Save failed: " + zone.zoneId);
                if (owned.Remove(scene)) EditorSceneManager.CloseScene(scene, true);
            }
        }
        catch
        {
            foreach (var scene in owned) if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            foreach (var entry in previous) File.WriteAllBytes(entry.Key, entry.Value);
            AssetDatabase.Refresh();
            throw;
        }
    }
}
