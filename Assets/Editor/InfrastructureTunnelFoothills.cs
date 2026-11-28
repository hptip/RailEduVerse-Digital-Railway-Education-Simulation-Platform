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
    const string FoothillRoot = "Tunnel_Foothill_Transitions";
    const string FoothillFolder = ProdAssets + "/TunnelFoothills";
    const float InnerToe = 62f;
    const float ApproachFade = 36f;

    [MenuItem("Tools/UTTVR/Infrastructure/Blend tunnel foothills")]
    static void BlendTunnelFoothills()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Tunnel foothills can only be generated in Edit Mode.");
        var route = EditorSceneManager.OpenPreviewScene(Route);
        Scene design = default, workspace = default;
        var previews = new List<Scene>();
        var patches = new List<(RailwayZoneStreamingManager.ZoneRecord zone, Mesh mesh, Material material)>();
        try
        {
            var manager = route.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var facilities = manager.fixedInfrastructure.Where(item => item.kind == "TunnelMountain").ToArray();
            if (facilities.Length != 2) throw new InvalidOperationException("Expected the two approved tunnel facilities.");
            design = EditorSceneManager.OpenPreviewScene(InfraScene);
            workspace = EditorSceneManager.NewPreviewScene();
            var pilot = new RoutePilot(workspace, manager, design, null);
            var ridges = route.GetRootGameObjects().Single(root => root.name == "RailwayMountainBackdrop")
                .GetComponentsInChildren<MeshFilter>().Where(HasGeometry).Select(ReadSurface).ToArray();
            var report = new List<string>();
            foreach (var facility in facilities)
            {
                var start = facility.startChainage - ApproachFade;
                var end = facility.endChainage + ApproachFade;
                var zones = manager.zones.Where(zone => zone.startChainage < end && zone.endChainage > start).ToArray();
                if (zones.Any(zone => !facility.zoneIds.Contains(zone.zoneId) ||
                    SceneManager.GetSceneByPath(zone.scenePath).isDirty))
                    throw new InvalidOperationException($"The work extends outside the loaded facility or touches a dirty scene: {facility.id}.");
                var roots = new List<Transform>();
                foreach (var zone in zones)
                {
                    var scene = EditorSceneManager.OpenPreviewScene(zone.scenePath);
                    previews.Add(scene);
                    roots.Add(Root(scene));
                }
                var filters = roots.SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true))
                    .Where(HasGeometry).ToArray();
                var massif = filters.Where(filter => filter.name.StartsWith(facility.id + "_Ground", StringComparison.Ordinal)
                    && char.IsDigit(filter.name[^1])).ToArray();
                if (massif.Length < 10 || ridges.Length == 0)
                    throw new InvalidOperationException($"Missing mountain geometry for {facility.id}.");
                var hills = massif.Select(ReadSurface).ToArray();
                var ground = filters.Where(filter => Under(filter, name =>
                    name.StartsWith("TerrainZone_", StringComparison.Ordinal) || name == "TerrainTransitions"))
                    .Select(ReadSurface).ToArray();
                var roads = filters.Where(filter => Under(filter, name => name == "Roads")).Select(ReadSurface).ToArray();
                var material = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/ProjectLopHoc/Generated/QuestRailway/GrassGround.mat");
                if (material == null) throw new InvalidOperationException("Missing the route's grass material.");
                foreach (int side in new[] { -1, 1 })
                {
                    var rows = zones.SelectMany(zone =>
                    {
                        float a = Mathf.Max(start, zone.startChainage), b = Mathf.Min(end, zone.endChainage);
                        int intervals = Mathf.CeilToInt((b - a) / 8f);
                        return Enumerable.Range(0, intervals + 1).Select(i => Mathf.Lerp(a, b, i / (float)intervals));
                    }).Distinct().OrderBy(value => value).ToArray();
                    var feet = rows.Select(d => FindRidgeToe(pilot, ridges, ground, d, side)).ToArray();
                    float lift = 0f, maxJoin = 0f;
                    const int across = 46;
                    var vertices = new Vector3[rows.Length][];
                    for (int i = 0; i < rows.Length; i++)
                    {
                        float d = rows[i];
                        // Smooth the scanned ridge toe along the trail so adjacent cross-sections cannot form spikes.
                        int lo = Mathf.Max(0, i - 3), hi = Mathf.Min(feet.Length - 1, i + 3);
                        float outer = feet.Skip(lo).Take(hi - lo + 1).Average();
                        float endDistance = Mathf.Min(d - start, end - d);
                        var near = pilot.World(d, side * InnerToe, 0);
                        float nearHeight = Height(pilot, ground, near);
                        nearHeight = Mathf.Max(nearHeight, pilot.Surface(hills, near));
                        vertices[i] = new Vector3[across + 1];
                        for (int col = 0; col <= across; col++)
                        {
                            float u = col / (float)across;
                            var point = pilot.World(d, side * Mathf.Lerp(InnerToe, outer, u), 0);
                            float groundHeight = Height(pilot, ground, point);
                            float existing = Mathf.Max(groundHeight,
                                Mathf.Max(pilot.Surface(hills, point), pilot.Surface(ridges, point)));
                            float shoulder = Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * u)), 1.35f);
                            float nearSlope = Mathf.InverseLerp(0, .48f, u);
                            float relief = Mathf.Clamp((nearHeight - groundHeight) * .38f + 14f, 14f, 32f);
                            float contour = Mathf.PerlinNoise(point.x / 148f, point.z / 101f);
                            float detail = Mathf.PerlinNoise(point.x / 43f, point.z / 48f);
                            float inset = shoulder * (25f + contour * 37f);
                            float fade = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(inset, inset + 112f, endDistance));
                            float fill = (relief * .62f + contour * 17f + detail * 5f) * shoulder +
                                Mathf.Max(0, nearHeight - groundHeight)
                                * (1 - Mathf.SmoothStep(0, 1, nearSlope)) * shoulder * .55f;
                            float roadWeight = RoadClearance(roads, point);
                            float target = groundHeight - .35f + fill * fade * roadWeight;
                            // Sink the perimeter under the existing surfaces. The patch crosses them only at a slope.
                            float perimeter = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(u, 1 - u) / .18f));
                            point.y = Mathf.Lerp(existing - .35f, Mathf.Max(groundHeight - .35f, target),
                                perimeter * Mathf.SmoothStep(0, 1, Mathf.Clamp01(fade * 3f)));
                            if (!float.IsFinite(point.y)) throw new InvalidOperationException("Invalid foothill vertex.");
                            if (col == 0 || col == across) maxJoin = Mathf.Max(maxJoin, point.y - existing);
                            lift = Mathf.Max(lift, point.y - existing);
                            vertices[i][col] = point;
                        }
                    }
                    var normals = vertices.Select((row, i) => row.Select((_, col) =>
                    {
                        var along = vertices[Mathf.Min(i + 1, rows.Length - 1)][col] -
                            vertices[Mathf.Max(i - 1, 0)][col];
                        var lateral = row[Mathf.Min(col + 1, across)] - row[Mathf.Max(col - 1, 0)];
                        return side * Vector3.Cross(along, lateral).normalized;
                    }).ToArray()).ToArray();
                    foreach (var zone in zones)
                    {
                        var local = vertices.Where((_, i) => rows[i] >= zone.startChainage && rows[i] <= zone.endChainage)
                            .SelectMany(row => row).ToArray();
                        if (local.Length < (across + 1) * 2) throw new InvalidOperationException("Missing Zone boundary row.");
                        var triangles = new List<int>();
                        for (int index = across + 1; index < local.Length; index++)
                        {
                            if (index % (across + 1) == 0) continue;
                            int a = index - across - 2, b = a + 1, c = index - 1;
                            triangles.AddRange(side > 0 ? new[] { a, c, b, b, c, index } : new[] { a, b, c, b, index, c });
                        }
                        var mesh = InfraMesh(local, triangles);
                        mesh.normals = normals.Where((_, i) => rows[i] >= zone.startChainage && rows[i] <= zone.endChainage)
                            .SelectMany(row => row).ToArray();
                        mesh.name = $"{facility.id}_{(side < 0 ? "Left" : "Right")}_{zone.zoneId}";
                        patches.Add((zone, mesh, material));
                    }
                    if (lift < 10f || maxJoin > .001f) throw new InvalidOperationException("Foothill profile did not close the gap.");
                    report.Add(FormattableString.Invariant($"{facility.id} side {side}: ridge toe {feet.Min():F0}–{feet.Max():F0} m; maximum visible rise {lift:F1} m; inner toe {InnerToe:F0} m; {rows.Length} shared rows."));
                }
            }
            WriteFoothills(patches);
            File.WriteAllLines("Temp/RailwayTunnelFoothills_Verified.txt", report);
        }
        finally
        {
            foreach (var patch in patches)
                if (!EditorUtility.IsPersistent(patch.mesh)) Object.DestroyImmediate(patch.mesh);
            foreach (var scene in previews) EditorSceneManager.ClosePreviewScene(scene);
            if (workspace.IsValid()) EditorSceneManager.ClosePreviewScene(workspace);
            if (design.IsValid()) EditorSceneManager.ClosePreviewScene(design);
            EditorSceneManager.ClosePreviewScene(route);
        }
    }

    static bool HasGeometry(MeshFilter filter) => filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0;

    static bool Under(MeshFilter filter, Func<string, bool> matches)
    {
        for (var node = filter.transform; node != null; node = node.parent)
            if (matches(node.name)) return true;
        return false;
    }

    static float Height(RoutePilot pilot, ContactMesh[] ground, Vector3 point)
    {
        float height = pilot.Surface(ground, point);
        return float.IsFinite(height) ? height : point.y - 2f;
    }

    static float FindRidgeToe(RoutePilot pilot, ContactMesh[] ridges, ContactMesh[] ground, float d, int side)
    {
        const float limit = 800f;
        for (float x = 240; x <= limit; x += 10)
        {
            var sample = pilot.World(d, side * x, 0);
            if (pilot.Surface(ridges, sample) >= Height(pilot, ground, sample) + 10f)
                return Mathf.Min(limit, x + 34f);
        }
        return limit;
    }

    static float RoadClearance(ContactMesh[] roads, Vector3 point)
    {
        const float padding = 38f;
        var xz = new Vector2(point.x, point.z);
        float squared = padding * padding;
        foreach (var road in roads)
        {
            var bounds = road.bounds;
            if (point.x < bounds.min.x - padding || point.x > bounds.max.x + padding ||
                point.z < bounds.min.z - padding || point.z > bounds.max.z + padding) continue;
            if (MeshSurface(road, point.x, point.z).hit) return 0;
            for (int i = 0; i < road.triangles.Length; i += 3)
            {
                var a = road.vertices[road.triangles[i]];
                var b = road.vertices[road.triangles[i + 1]];
                var c = road.vertices[road.triangles[i + 2]];
                squared = Mathf.Min(squared, Edge(a, b));
                squared = Mathf.Min(squared, Edge(b, c));
                squared = Mathf.Min(squared, Edge(c, a));
            }
            if (squared <= 16 * 16) return 0;
        }
        return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(16f, padding, Mathf.Sqrt(squared)));

        float Edge(Vector3 a, Vector3 b)
        {
            var start = new Vector2(a.x, a.z);
            var direction = new Vector2(b.x - a.x, b.z - a.z);
            var fraction = Mathf.Clamp01(Vector2.Dot(xz - start, direction) /
                Mathf.Max(.0001f, direction.sqrMagnitude));
            return (xz - start - direction * fraction).sqrMagnitude;
        }
    }

    static void WriteFoothills(List<(RailwayZoneStreamingManager.ZoneRecord zone, Mesh mesh, Material material)> patches)
    {
        if (!AssetDatabase.IsValidFolder(FoothillFolder))
            AssetDatabase.CreateFolder(ProdAssets, "TunnelFoothills");
        foreach (var group in patches.GroupBy(patch => patch.zone))
        {
            var zone = group.Key;
            var scene = SceneManager.GetSceneByPath(zone.scenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(zone.scenePath, OpenSceneMode.Additive);
            try
            {
                var root = Root(scene);
                var existing = root.Find(FoothillRoot);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);
                var holder = new GameObject(FoothillRoot);
                SceneManager.MoveGameObjectToScene(holder, scene);
                holder.transform.SetParent(root, false);
                foreach (var patch in group)
                {
                    var path = $"{FoothillFolder}/{patch.mesh.name}.asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh != null)
                    {
                        EditorUtility.CopySerialized(patch.mesh, mesh);
                        EditorUtility.SetDirty(mesh);
                        AssetDatabase.SaveAssetIfDirty(mesh);
                    }
                    else
                    {
                        mesh = patch.mesh;
                        AssetDatabase.CreateAsset(mesh, path);
                    }
                    var node = new GameObject(mesh.name);
                    SceneManager.MoveGameObjectToScene(node, scene);
                    node.transform.SetParent(holder.transform, false);
                    node.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = node.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = patch.material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + zone.zoneId);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
