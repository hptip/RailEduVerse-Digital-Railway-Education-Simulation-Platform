using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal static partial class RailwayBimGeometryOverlapAudit
{
    // The discovery audit also reports near-coplanar faces at 2 mm. Repair uses a
    // stricter plane test and subtracts only area covered by an earlier owner.
    const float RepairPlaneTolerance = .00035f;
    const float FragmentArea = .00000002f;
    const string VisualPacks = "Assets/ProjectLopHoc/Generated/InfrastructureProduction/";
    const string TemporaryRollback = "Temp/BimViewerV2Rollback/";

    [MenuItem("Tools/UTTVR/BIM Viewer/Repair Rail Coplanar Ownership")]
    public static void RepairRailCoplanar()
    {
        var scene = EditorSceneManager.OpenPreviewScene(RoutePath);
        try
        {
            var streamer = Objects(scene).OfType<RailwayMicroChunkStreamer>().Single();
            RepairRailCoplanar(streamer.data);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    public static void RepairRailCoplanar(RailwayMicroChunkData registry)
    {
        ValidateRepairMath();
        int altered = 0;
        int surfaces = 0;
        double removed = 0;
        var sections = registry.chunks.OrderBy(chunk => chunk.start).ToArray();
        for (int side = 0; side < 2; side++)
        {
            Surface previous = null;
            RailwayMicroChunkData.Track previousTrack = null;
            float previousEnd = float.NegativeInfinity;
            foreach (var section in sections)
            {
                var track = side == 0 ? section.right : section.left;
                var candidates = new List<Surface>(1);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Rails + track.meshResource + ".asset");
                Add(candidates, mesh, Matrix4x4.TRS(track.position, track.rotation, track.scale), track.meshResource, "MicroChunk");
                if (candidates.Count != 1 || !mesh.isReadable) throw new InvalidOperationException("Missing readable rail: " + track.meshResource);
                var current = candidates[0];
                if (previous != null && Mathf.Abs(previousEnd - section.start) < .001f &&
                    previousTrack.materials.SequenceEqual(track.materials) && previous.Bounds.Intersects(current.Bounds))
                {
                    var change = SubtractCovered(current, new[] { previous });
                    if (change.faces > 0)
                    {
                        SaveGeneratedMesh(mesh);
                        altered++;
                        surfaces += change.faces;
                        removed += change.area;
                        current.Tree = null;
                        current.Triangles = null;
                    }
                }
                previous = current;
                previousTrack = track;
                previousEnd = section.end;
            }
        }
        Debug.Log(FormattableString.Invariant(
            $"[BIM Geometry] Rail ownership repaired {altered} meshes, {surfaces} affected triangles, {removed:F4} m² of double-drawn surfaces."));
    }

    [MenuItem("Tools/UTTVR/BIM Viewer/Repair Duplicate Rail Surfaces Within Chunks")]
    public static void RepairRailInternal()
    {
        var route = EditorSceneManager.OpenPreviewScene(RoutePath);
        try { RepairRailInternal(Objects(route).OfType<RailwayMicroChunkStreamer>().Single().data); }
        finally { EditorSceneManager.ClosePreviewScene(route); }
    }

    public static void RepairRailInternal(RailwayMicroChunkData registry)
    {
        ValidateRepairMath();
        const string verification = "Temp/BimViewerV2_RailSelfAfter.csv";
        var rows = new List<string> { "view,classification,first,second,count,area_m2" };
        int count = 0, checkedTracks = 0;
        double area = 0;
        foreach (var track in registry.chunks.SelectMany(chunk => new[] { chunk.right, chunk.left }))
        {
            var path = Rails + track.meshResource + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null || !mesh.isReadable) throw new InvalidOperationException("Missing readable rail: " + track.meshResource);
            var items = new List<Surface>(1);
            Add(items, mesh, Matrix4x4.TRS(track.position, track.rotation, track.scale), track.meshResource, "MicroChunk");
            var surface = items[0];
            var result = SubtractCovered(surface, items, earlierTrianglesOnly: true);
            if (result.faces > 0)
            {
                SaveGeneratedMesh(mesh);
                count++;
                area += result.area;
                surface.Tree = null;
                surface.Triangles = null;
            }
            Prepare(surface);
            ComparePair("YenTuHaLongXanh · Final internal rail", surface, surface, rows);
            if (++checkedTracks % 32 == 0)
                File.WriteAllText("Temp/BimViewerV2_RailSelfProgress.txt", $"{checkedTracks}; {count}; {area:F3}");
        }
        File.WriteAllLines(verification, rows);
        File.WriteAllText("Temp/BimViewerV2_RailSelfProgress.txt", $"Done; {checkedTracks}; {count}; {area:F3}");
        Debug.Log(FormattableString.Invariant($"[BIM Geometry] Internal rail ownership: {count} meshes, {area:F4} m² removed."));
    }

    [MenuItem("Tools/UTTVR/BIM Viewer/Repair Facility Coplanar Ownership")]
    public static void RepairFacilities()
    {
        ValidateRepairMath();
        var route = EditorSceneManager.OpenPreviewScene(RoutePath);
        try
        {
            var manager = Objects(route).OfType<RailwayZoneStreamingManager>().Single();
            var sources = new List<Surface>();
            var targets = manager.zones.Where(record => new[] { "Zone_011", "Zone_012", "Zone_013",
                "Zone_014", "Zone_015", "Zone_018" }.Contains(record.zoneId)).ToArray();
            foreach (var zone in targets)
            {
                var scene = EditorSceneManager.OpenPreviewScene(zone.scenePath);
                try { AddScene(sources, scene, zone.zoneId); }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }

            var lining = sources.Where(s => s.Name.StartsWith("Tunnel_C", StringComparison.Ordinal) &&
                s.Name.Contains("Tunnel lining concrete")).OrderBy(s => s.Name, StringComparer.Ordinal).ToArray();
            int owned = 0;
            double ownedArea = 0;
            void RepairPack(Surface surface, Surface[] winner)
            {
                if (winner.Length == 0 || !surface.Mesh.isReadable) return;
                string path = AssetDatabase.GetAssetPath(surface.Mesh);
                if (!path.StartsWith(VisualPacks, StringComparison.Ordinal)) return;
                var safeCopy = Object.Instantiate(surface.Mesh);
                safeCopy.name = surface.Mesh.name;
                var staged = CopySurface(surface, safeCopy);
                try
                {
                    var result = SubtractCovered(staged, winner);
                    if (result.faces == 0) return;
                    BackupBeforeChange(path);
                    EditorUtility.CopySerialized(safeCopy, surface.Mesh);
                    SaveGeneratedMesh(surface.Mesh);
                    surface.Triangles = null; surface.Tree = null;
                    owned++;
                    ownedArea += result.area;
                }
                finally { Object.DestroyImmediate(safeCopy); }
            }
            foreach (var surface in lining)
                RepairPack(surface, lining.Where(owner => string.CompareOrdinal(owner.Name, surface.Name) < 0 &&
                    owner.Bounds.Intersects(surface.Bounds)).ToArray());
            var topLayer = sources.Where(s => s.Name.StartsWith("Subgrade_C", StringComparison.Ordinal) &&
                s.Name.Contains("Lớp nền trên")).ToArray();
            foreach (var surface in sources.Where(s => s.Name.StartsWith("Subgrade_C", StringComparison.Ordinal) &&
                s.Name.Contains("Subgrade weathered concrete")))
                RepairPack(surface, topLayer.Where(owner => owner.Bounds.Intersects(surface.Bounds)).ToArray());
            Debug.Log(FormattableString.Invariant(
                $"[BIM Geometry] Tunnel/Subgrade copied visual packs: {owned} meshes, {ownedArea:F4} m² removed."));

            foreach (var zone in targets)
                RepairBorrowedZoneVisuals(zone.scenePath, zone.zoneId, sources.Where(s => s.Zone == zone.zoneId).ToArray());
        }
        finally { EditorSceneManager.ClosePreviewScene(route); }
    }

    [MenuItem("Tools/UTTVR/BIM Viewer/Repair Subsumed Bridge Deck Faces")]
    public static void RepairSubsumedBridgeFaces()
    {
        const string zone = "Zone_018";
        const string path = "Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_018.unity";
        foreach (bool reverse in new[] { false, true })
        {
            var sources = new List<Surface>();
            var preview = EditorSceneManager.OpenPreviewScene(path);
            try { AddScene(sources, preview, zone); }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            RepairBorrowedZoneVisuals(path, zone, sources.ToArray(), true, reverse);
        }
    }

    [MenuItem("Tools/UTTVR/BIM Viewer/Repair Remaining Facility Seams")]
    public static void RepairRemainingFacilitySeams()
    {
        const string verification = "Temp/BimViewerV2_FinalSeams.csv";
        var rows = new List<string> { "view,classification,first,second,count,area_m2" };
        var route = EditorSceneManager.OpenPreviewScene(RoutePath);
        try
        {
            var manager = Objects(route).OfType<RailwayZoneStreamingManager>().Single();
            foreach (var zone in manager.zones.Where(zone => zone.zoneId is "Zone_012" or "Zone_013" or
                "Zone_014" or "Zone_015" or "Zone_018"))
            {
                var sources = new List<Surface>();
                var scene = EditorSceneManager.OpenPreviewScene(zone.scenePath);
                try { AddScene(sources, scene, zone.zoneId); }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
                RepairBorrowedZoneVisuals(zone.scenePath, zone.zoneId, sources.ToArray(), fixedPriority: true);
                sources.Clear();
                var modified = EditorSceneManager.OpenPreviewScene(zone.scenePath);
                try { AddScene(sources, modified, zone.zoneId); }
                finally { EditorSceneManager.ClosePreviewScene(modified); }
                CheckPrioritySeams("YenTuHaLongXanh · Final seams", sources, rows);
                sources.Clear();
                var copy = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + zone.zoneId + ".prefab");
                if (copy == null) throw new InvalidOperationException("Missing BIM visual prefab " + zone.zoneId);
                AddFilters(sources, copy.GetComponentsInChildren<MeshFilter>(true), zone.zoneId, copy.transform);
                CheckPrioritySeams("QuanLyVongDoiTaiSan · Final seams", sources, rows);
            }
            File.WriteAllLines(verification, rows);
        }
        finally { EditorSceneManager.ClosePreviewScene(route); }
    }

    [MenuItem("Tools/UTTVR/BIM Viewer/Repair Internal Facility Batches")]
    public static void RepairInternalFacilityBatches()
    {
        const string verification = "Temp/BimViewerV2_FacilitySelfAfter.csv";
        var rows = new List<string> { "view,classification,first,second,count,area_m2" };
        var route = EditorSceneManager.OpenPreviewScene(RoutePath);
        int count = 0;
        double area = 0;
        try
        {
            var manager = Objects(route).OfType<RailwayZoneStreamingManager>().Single();
            foreach (var zone in manager.zones.Where(zone => zone.zoneId is "Zone_011" or "Zone_012" or
                "Zone_013" or "Zone_014" or "Zone_015" or "Zone_016"))
            {
                var sources = new List<Surface>();
                var scene = EditorSceneManager.OpenPreviewScene(zone.scenePath);
                try { AddScene(sources, scene, zone.zoneId); }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
                foreach (var surface in sources.Where(source => source.Mesh.isReadable &&
                    (source.Name.StartsWith("Subgrade_C", StringComparison.Ordinal) ||
                    source.Name.StartsWith("Tunnel_C", StringComparison.Ordinal))))
                {
                    string path = AssetDatabase.GetAssetPath(surface.Mesh);
                    if (!path.StartsWith(VisualPacks, StringComparison.Ordinal)) continue;
                    Prepare(surface);
                    var result = SubtractCovered(surface, new[] { surface }, earlierTrianglesOnly: true);
                    if (result.faces > 0)
                    {
                        BackupBeforeChange(path);
                        SaveGeneratedMesh(surface.Mesh);
                        count++;
                        area += result.area;
                        surface.Tree = null;
                        surface.Triangles = null;
                        Prepare(surface);
                    }
                    int start = rows.Count;
                    ComparePair("Both scenes · Final internal facilities", surface, surface, rows);
                    if (rows.Count == start) Row(rows, "Both scenes · Final internal facilities", "NO_OVERLAP_OR_CROSSING",
                        Label(surface), Label(surface), 1, 0);
                }
            }
            File.WriteAllLines(verification, rows);
            Debug.Log(FormattableString.Invariant($"[BIM Geometry] Internal facility ownership: {count} meshes, {area:F4} m² removed."));
        }
        finally { EditorSceneManager.ClosePreviewScene(route); }
    }

    static void CheckPrioritySeams(string view, List<Surface> sources, List<string> rows)
    {
        bool bridge = sources.Any(s => s.Zone == "Zone_018");
        var targets = sources.Where(s => bridge ? s.Name == "Deck with crossbeams 7" :
            s.Mesh.name.StartsWith("TerrainTransition_Chunk_", StringComparison.Ordinal));
        var owners = sources.Where(s => bridge ? s.Name == "Pavement" :
            s.Name.StartsWith("TunnelMountain_", StringComparison.Ordinal) && s.Name.Contains("_Ground") &&
            !s.Name.EndsWith("_Vegetation", StringComparison.Ordinal)).ToArray();
        foreach (var target in targets)
            foreach (var owner in owners)
            {
                if (!target.Bounds.Intersects(owner.Bounds)) continue;
                Prepare(target);
                Prepare(owner);
                int start = rows.Count;
                ComparePair(view, target, owner, rows);
                if (rows.Count == start) Row(rows, view, "NO_OVERLAP_OR_CROSSING", Label(target), Label(owner), 1, 0);
            }
    }

    public static void RepairOccludedRailPairs(RailwayMicroChunkData registry, string[] laterResources)
    {
        var wanted = laterResources.ToHashSet(StringComparer.Ordinal);
        var sections = registry.chunks.OrderBy(chunk => chunk.start).ToArray();
        double removed = 0;
        int affected = 0;
        for (int i = 1; i < sections.Length; i++)
        {
            if (Mathf.Abs(sections[i - 1].end - sections[i].start) > .001f) continue;
            foreach (int side in new[] { 0, 1 })
            {
                var earlier = side == 0 ? sections[i - 1].right : sections[i - 1].left;
                var later = side == 0 ? sections[i].right : sections[i].left;
                if (!wanted.Contains(later.meshResource) || !earlier.materials.SequenceEqual(later.materials)) continue;
                var pair = new List<Surface>(2);
                foreach (var track in new[] { earlier, later })
                    Add(pair, AssetDatabase.LoadAssetAtPath<Mesh>(Rails + track.meshResource + ".asset"),
                        Matrix4x4.TRS(track.position, track.rotation, track.scale), track.meshResource, "MicroChunk");
                foreach (int target in new[] { 1, 0 })
                {
                    var surface = pair[target];
                    var result = SubtractCovered(surface, new[] { pair[1 - target] }, true);
                    if (result.faces == 0) continue;
                    SaveGeneratedMesh(surface.Mesh);
                    surface.Triangles = null; surface.Tree = null;
                    removed += result.area;
                    affected++;
                }
            }
        }
        Debug.Log(FormattableString.Invariant($"[BIM Geometry] Residual rail occlusion: {affected} meshes, {removed:F4} m² removed."));
    }

    static Surface CopySurface(Surface source, Mesh mesh) =>
        new() { Name = source.Name, Zone = source.Zone, Identity = source.Identity, Mesh = mesh,
            Matrix = source.Matrix, Bounds = source.Bounds };

    static void BackupBeforeChange(string path)
    {
        string destination = TemporaryRollback + path;
        if (File.Exists(destination)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(path, destination);
    }

    static void RepairBorrowedZoneVisuals(string scenePath, string zone, Surface[] sources,
        bool bridgeWearingSurface = false, bool reverseBridgePair = false, bool fixedPriority = false)
    {
        bool bridge = zone == "Zone_018";
        string top = reverseBridgePair ? "Deck with crossbeams 7" : "Pavement";
        string lower = reverseBridgePair ? "Pavement" : "Deck with crossbeams 7";
        var owners = sources.Where(s => s.Mesh.isReadable && (bridge ? s.Name == top :
            s.Name.StartsWith("TunnelMountain_", StringComparison.Ordinal) && s.Name.Contains("_Ground") &&
            !s.Name.EndsWith("_Vegetation", StringComparison.Ordinal))).ToArray();
        var candidates = sources.Where(s => s.Mesh.isReadable && (bridge ? s.Name == lower :
            s.Mesh.name.StartsWith("TerrainTransition_Chunk_", StringComparison.Ordinal) ||
            AssetDatabase.GetAssetPath(s.Mesh).Contains("TerrainTransition_Chunk_"))).ToArray();
        if (owners.Length == 0 || candidates.Length == 0) return;
        var zoneScene = SceneManager.GetSceneByPath(scenePath);
        if (zoneScene.isLoaded && zoneScene.isDirty)
            throw new InvalidOperationException("Save outstanding edits before repairing geometry in " + zone);
        bool openForRepair = !zoneScene.isLoaded;
        if (openForRepair) zoneScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        string prefabPath = Prefabs + zone + ".prefab";
        GameObject bimRoot = null;
        try
        {
            bimRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            var routeRoot = zoneScene.GetRootGameObjects().Single(g => g.name == "RuntimeZoneContent").transform;
            var originDelta = routeRoot.position - bimRoot.transform.position;
            var sceneFilters = routeRoot.GetComponentsInChildren<MeshFilter>(true);
            var bimFilters = bimRoot.GetComponentsInChildren<MeshFilter>(true);
            string packPath = VisualPacks + zone + ".asset";
            var pack = AssetDatabase.LoadMainAssetAtPath(packPath);
            if (pack == null) throw new InvalidOperationException("Missing production visual pack " + zone);
            int count = 0;
            double area = 0;
            foreach (var surface in candidates)
            {
                var matching = owners.Where(owner => owner.Bounds.Intersects(surface.Bounds)).ToArray();
                if (matching.Length == 0) continue;
                var sceneMatch = sceneFilters.Where(f => f.name == surface.Name && f.sharedMesh == surface.Mesh &&
                    SameMatrix(f.transform.localToWorldMatrix, surface.Matrix)).ToArray();
                var position = (Vector3)surface.Matrix.GetColumn(3) - originDelta;
                var bimMatch = bimFilters.Where(f => f.name == surface.Name && f.sharedMesh == surface.Mesh &&
                    (f.transform.position - position).sqrMagnitude < .001f).ToArray();
                if (sceneMatch.Length != 1 || bimMatch.Length != 1)
                {
                    Debug.LogWarning($"[BIM Geometry] Preserve borrowed geometry without an unambiguous scene/prefab match: {zone}/{surface.Name}");
                    continue;
                }
                var mesh = Object.Instantiate(surface.Mesh);
                var patch = CopySurface(surface, mesh);
                var change = SubtractCovered(patch, matching, bridgeWearingSurface || fixedPriority, fixedPriority);
                if (change.faces == 0) { Object.DestroyImmediate(mesh); continue; }
                if (count == 0)
                {
                    BackupBeforeChange(scenePath);
                    BackupBeforeChange(prefabPath);
                    BackupBeforeChange(packPath);
                }
                const string ownershipSuffix = " · Visual ownership";
                if (AssetDatabase.GetAssetPath(surface.Mesh) == packPath &&
                    surface.Mesh.name.EndsWith(ownershipSuffix, StringComparison.Ordinal))
                {
                    mesh.name = surface.Mesh.name;
                    EditorUtility.CopySerialized(mesh, surface.Mesh);
                    Object.DestroyImmediate(mesh);
                    SaveGeneratedMesh(surface.Mesh);
                }
                else
                {
                    mesh.name = surface.Mesh.name + ownershipSuffix;
                    AssetDatabase.AddObjectToAsset(mesh, pack);
                    sceneMatch[0].sharedMesh = mesh;
                    bimMatch[0].sharedMesh = mesh;
                }
                // Preserve original collision and BIM identity. Only the duplicated
                // rendered face is removed from these two production representations.
                EditorUtility.SetDirty(sceneMatch[0]);
                count++;
                area += change.area;
            }
            if (count == 0) return;
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssetIfDirty(pack);
            if (!EditorSceneManager.SaveScene(zoneScene) || PrefabUtility.SaveAsPrefabAsset(bimRoot, prefabPath) == null)
                throw new IOException("Could not save the visual-only repair for " + zone);
            Debug.Log(FormattableString.Invariant($"[BIM Geometry] {zone} borrowed visual repair: {count} meshes, {area:F4} m² removed."));
        }
        finally
        {
            if (bimRoot != null) PrefabUtility.UnloadPrefabContents(bimRoot);
            if (openForRepair && zoneScene.IsValid()) EditorSceneManager.CloseScene(zoneScene, true);
        }
    }

    static void SaveGeneratedMesh(Mesh mesh)
    {
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);
    }

    public static void ValidateRepairMath()
    {
        Mesh TriangleMesh(float x, float z)
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(x, 0, z), new Vector3(x + 2, 0, z), new Vector3(x, 2, z) };
            mesh.triangles = new[] { 0, 1, 2 };
            return mesh;
        }
        var first = TriangleMesh(0, 0);
        var cover = TriangleMesh(1, 0);
        var contact = TriangleMesh(2, 0);
        var separated = TriangleMesh(1, .001f);
        try
        {
            var surfaces = new List<Surface>();
            foreach (var mesh in new[] { first, cover, contact, separated })
                Add(surfaces, mesh, Matrix4x4.identity, "test", "test");
            if (SubtractCovered(surfaces[0], new[] { surfaces[2], surfaces[3] }).faces != 0)
                throw new InvalidOperationException("Touching or separated geometry must remain.");
            var result = SubtractCovered(surfaces[0], new[] { surfaces[1] });
            if (result.faces != 1 || Math.Abs(result.area - .5) > .00001)
                throw new InvalidOperationException("Partial coplanar polygon test failed.");
            surfaces[0].Triangles = null; surfaces[0].Tree = null;
            if (SubtractCovered(surfaces[0], new[] { surfaces[1] }).faces != 0)
                throw new InvalidOperationException("Rail ownership repair is not idempotent for the tested joint.");
        }
        finally
        {
            foreach (var mesh in new[] { first, cover, contact, separated }) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    static (int faces, double area) SubtractCovered(Surface target, IReadOnlyList<Surface> owners,
        bool nearPlane = false, bool fixedPriority = false, bool earlierTrianglesOnly = false)
    {
        Prepare(target);
        foreach (var owner in owners) Prepare(owner);
        var replacements = new Dictionary<(int submesh, int offset), List<Vector3>>();
        var cover = new List<Vector3[]>();
        double removed = 0;
        foreach (var triangle in target.Triangles)
        {
            cover.Clear();
            foreach (var owner in owners) FindCover(owner, owner.Tree, triangle, cover, nearPlane, fixedPriority, earlierTrianglesOnly);
            if (cover.Count == 0) continue;
            var x = (triangle.B - triangle.A).normalized;
            var y = Vector3.Cross(triangle.Normal, x);
            Vector2 Project(Vector3 p) => new(Vector3.Dot(p - triangle.A, x), Vector3.Dot(p - triangle.A, y));
            var baseB = Project(triangle.B);
            var baseC = Project(triangle.C);
            var first = new[] { Vector2.zero, baseB, baseC };
            var pieces = new List<Vector2[]> { first };
            foreach (var layer in cover)
            {
                if (pieces.Count == 0) break;
                var next = new List<Vector2[]>();
                var mask = layer.Select(Project).ToArray();
                foreach (var piece in pieces) Subtract(piece, mask, next);
                pieces = next;
            }
            double before = PolygonArea(first);
            double remaining = pieces.Sum(PolygonArea);
            double change = before - remaining;
            if (change < MinimumArea) continue;
            if (remaining < -FragmentArea || remaining > before + FragmentArea)
                throw new InvalidOperationException("Invalid overlap ownership polygon: " + target.Name);
            var output = new List<Vector3>();
            float det = Cross(baseB, baseC);
            Vector3 Weight(Vector2 point)
            {
                float u = Cross(point, baseC) / det;
                float v = Cross(baseB, point) / det;
                return new Vector3(1 - u - v, u, v);
            }
            foreach (var piece in pieces)
                for (int i = 1; i + 1 < piece.Length; i++)
                {
                    if (Mathf.Abs(Cross(piece[i] - piece[0], piece[i + 1] - piece[0])) <= FragmentArea) continue;
                    output.Add(Weight(piece[0]));
                    output.Add(Weight(piece[i]));
                    output.Add(Weight(piece[i + 1]));
                }
            replacements.Add((triangle.Submesh, triangle.Offset), output);
            removed += change;
        }
        if (replacements.Count > 0) Rewrite(target.Mesh, replacements);
        return (replacements.Count, removed);
    }

    static void FindCover(Surface owner, Node tree, Triangle target, List<Vector3[]> output,
        bool nearPlane, bool fixedPriority, bool earlierTrianglesOnly)
    {
        if (tree == null || !tree.Bounds.Intersects(target.Bounds)) return;
        if (tree.Left != null)
        {
            FindCover(owner, tree.Left, target, output, nearPlane, fixedPriority, earlierTrianglesOnly);
            FindCover(owner, tree.Right, target, output, nearPlane, fixedPriority, earlierTrianglesOnly);
            return;
        }
        for (int i = tree.First; i < tree.End; i++)
        {
            var t = owner.Triangles[i];
            if (earlierTrianglesOnly && (t.Submesh != target.Submesh || t.Offset >= target.Offset)) continue;
            if (!t.Bounds.Intersects(target.Bounds) ||
                Vector3.Dot(t.Normal, target.Normal) < (nearPlane ? .9999f : .999999f)) continue;
            if (!nearPlane && (Mathf.Abs(Vector3.Dot(target.Normal, t.A - target.A)) > RepairPlaneTolerance ||
                Mathf.Abs(Vector3.Dot(target.Normal, t.B - target.A)) > RepairPlaneTolerance ||
                Mathf.Abs(Vector3.Dot(target.Normal, t.C - target.A)) > RepairPlaneTolerance)) continue;
            if (SharedArea(target, t) <= MinimumArea) continue;
            var mask = new[] { t.A, t.B, t.C };
            if (nearPlane)
            {
                // Test signed separation only in the overlapping polygon. A long
                // owner triangle may extend past the tolerance away from the joint.
                mask = ClipPlane(ClipPlane(mask, fixedPriority ? -Tolerance : -RepairPlaneTolerance, true), Tolerance, false);
                if (mask.Length >= 3) output.Add(mask);
            }
            else output.Add(mask);
        }

        Vector3[] ClipPlane(Vector3[] input, float edge, bool positive)
        {
            var clipped = new List<Vector3>(input.Length + 1);
            for (int i = 0; i < input.Length; i++)
            {
                var a = input[i];
                var b = input[(i + 1) % input.Length];
                float da = Vector3.Dot(target.Normal, a - target.A) - edge;
                float db = Vector3.Dot(target.Normal, b - target.A) - edge;
                bool keepA = positive ? da >= 0 : da <= 0;
                bool keepB = positive ? db >= 0 : db <= 0;
                if (keepA) clipped.Add(a);
                if (keepA != keepB) clipped.Add(Vector3.LerpUnclamped(a, b, da / (da - db)));
            }
            return clipped.ToArray();
        }
    }

    static void Subtract(Vector2[] from, Vector2[] mask, List<Vector2[]> output)
    {
        var inside = from;
        var outside = new List<Vector2[]>(3);
        for (int side = 0; side < mask.Length && inside.Length >= 3; side++)
        {
            var start = mask[side];
            var edge = mask[(side + 1) % mask.Length] - start;
            var outer = Clip(inside, start, edge, false);
            if (PolygonArea(outer) > FragmentArea) outside.Add(outer);
            inside = Clip(inside, start, edge, true);
        }
        if (PolygonArea(inside) <= FragmentArea) output.Add(from);
        else output.AddRange(outside);
    }

    static Vector2[] Clip(Vector2[] points, Vector2 start, Vector2 edge, bool positive)
    {
        var output = new List<Vector2>(points.Length + 1);
        for (int i = 0; i < points.Length; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Length];
            float aDistance = Cross(edge, a - start);
            float bDistance = Cross(edge, b - start);
            bool before = positive ? aDistance >= 0 : aDistance <= 0;
            bool after = positive ? bDistance >= 0 : bDistance <= 0;
            if (before) output.Add(a);
            if (before != after) output.Add(Vector2.LerpUnclamped(a, b, aDistance / (aDistance - bDistance)));
        }
        return output.ToArray();
    }

    static double PolygonArea(Vector2[] points)
    {
        if (points.Length < 3) return 0;
        double area = 0;
        for (int i = 1; i + 1 < points.Length; i++) area += Cross(points[i] - points[0], points[i + 1] - points[0]);
        return Math.Abs(area * .5);
    }

    static void Rewrite(Mesh mesh, Dictionary<(int submesh, int offset), List<Vector3>> replacements)
    {
        var vertices = mesh.vertices.ToList();
        var normals = mesh.normals.ToList();
        var colors = mesh.colors.ToList();
        var tangents = mesh.tangents.ToList();
        var uv = new List<Vector4>[8];
        for (int i = 0; i < uv.Length; i++)
        {
            uv[i] = new List<Vector4>();
            mesh.GetUVs(i, uv[i]);
        }
        var submeshes = new List<int>[mesh.subMeshCount];
        for (int s = 0; s < submeshes.Length; s++)
        {
            if (mesh.GetTopology(s) != MeshTopology.Triangles) throw new InvalidOperationException("Non-triangle geometry in " + mesh.name);
            var indices = mesh.GetIndices(s);
            var output = submeshes[s] = new List<int>(indices.Length);
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (!replacements.TryGetValue((s, i), out var newVertices))
                {
                    output.Add(a); output.Add(b); output.Add(c);
                    continue;
                }
                foreach (var w in newVertices)
                {
                    output.Add(vertices.Count);
                    vertices.Add(vertices[a] * w.x + vertices[b] * w.y + vertices[c] * w.z);
                    if (normals.Count > 0) normals.Add((normals[a] * w.x + normals[b] * w.y + normals[c] * w.z).normalized);
                    if (colors.Count > 0) colors.Add(colors[a] * w.x + colors[b] * w.y + colors[c] * w.z);
                    if (tangents.Count > 0) tangents.Add(tangents[a] * w.x + tangents[b] * w.y + tangents[c] * w.z);
                    foreach (var channel in uv)
                        if (channel.Count > 0) channel.Add(channel[a] * w.x + channel[b] * w.y + channel[c] * w.z);
                }
            }
        }
        if (submeshes.All(list => list.Count == 0)) throw new InvalidOperationException("No geometry would remain in " + mesh.name);
        mesh.SetVertices(vertices);
        if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
        if (normals.Count > 0) mesh.SetNormals(normals);
        if (colors.Count > 0) mesh.SetColors(colors);
        if (tangents.Count > 0) mesh.SetTangents(tangents);
        for (int i = 0; i < uv.Length; i++)
            if (uv[i].Count > 0) mesh.SetUVs(i, uv[i]);
        mesh.subMeshCount = submeshes.Length;
        for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i, false);
        mesh.RecalculateBounds();
    }
}
