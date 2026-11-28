using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

internal static class RailwayRailbedGrounding
{
    const string Route = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string MeshFolder = "Assets/ProjectLopHoc/Generated/QuestRailway/Resources/";
    const string BimProvenance = "Assets/ProjectLopHoc/Generated/QuanLyVongDoiTaiSan/SceneProvenance.json";
    const string BimSupport = "Assets/ProjectLopHoc/Generated/RailwayLifecycleBim/ExistingGirderSupportGeometry.asset";
    const float GaugeBetweenCentres = 6.400022f;
    const float CellSize = 32;
    const float ContactTolerance = .025f;
    const float MaxBridgePlinthHeight = 1.6f;
    static readonly Color32 Earth = new(112, 107, 93, 255);
    static readonly Color32 BridgeConcrete = new(144, 144, 139, 255);
    static readonly Color32 TunnelConcrete = new(103, 110, 115, 255);
    static readonly Color32 SlabColor = new(60, 60, 60, 255);

    enum GroundKind { Land, Tunnel, Bridge, Slab }

    readonly struct Face
    {
        public readonly Vector3 A, B, C;
        public readonly GroundKind Kind;
        public Face(Vector3 a, Vector3 b, Vector3 c, GroundKind kind)
        { A = a; B = b; C = c; Kind = kind; }
    }

    sealed class Ground
    {
        readonly Dictionary<Vector2Int, List<Face>> buckets = new();

        public void Add(MeshFilter filter, GroundKind kind)
        {
            Add(filter.sharedMesh, filter.transform.localToWorldMatrix, kind);
        }

        public void Add(Mesh mesh, Matrix4x4 world, GroundKind kind)
        {
            if (mesh == null || !mesh.isReadable) return;
            var vertices = mesh.vertices.Select(world.MultiplyPoint3x4).ToArray();
            var colors = kind == GroundKind.Slab ? mesh.colors32 : null;
            foreach (int submesh in Enumerable.Range(0, mesh.subMeshCount))
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                var indices = mesh.GetTriangles(submesh);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    if (colors != null && (!colors[indices[i]].Equals(SlabColor) || !colors[indices[i + 1]].Equals(SlabColor) ||
                        !colors[indices[i + 2]].Equals(SlabColor))) continue;
                    var a = vertices[indices[i]];
                    var b = vertices[indices[i + 1]];
                    var c = vertices[indices[i + 2]];
                    var n = Vector3.Cross(b - a, c - a);
                    if (Mathf.Abs(n.y) < Mathf.Max(.000001f, n.magnitude * .25f)) continue;
                    var min = Cell(Vector3.Min(a, Vector3.Min(b, c)));
                    var max = Cell(Vector3.Max(a, Vector3.Max(b, c)));
                    var face = new Face(a, b, c, kind);
                    for (int x = min.x; x <= max.x; x++)
                        for (int z = min.y; z <= max.y; z++)
                        {
                            var key = new Vector2Int(x, z);
                            if (!buckets.TryGetValue(key, out var list)) buckets.Add(key, list = new List<Face>());
                            list.Add(face);
                        }
                }
            }
        }

        static Vector2Int Cell(Vector3 p) => new(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));

        public float Height(Vector3 p, float ceiling, GroundKind kind)
        {
            if (!buckets.TryGetValue(Cell(p), out var faces)) return float.NegativeInfinity;
            float height = float.NegativeInfinity;
            foreach (var face in faces)
            {
                if (face.Kind != kind) continue;
                var a = face.A;
                var b = face.B - a;
                var c = face.C - a;
                var delta = p - a;
                float determinant = b.x * c.z - b.z * c.x;
                float u = (delta.x * c.z - delta.z * c.x) / determinant;
                float v = (b.x * delta.z - b.z * delta.x) / determinant;
                if (u < -.0001f || v < -.0001f || u + v > 1.0001f) continue;
                float y = a.y + u * b.y + v * c.y;
                if (y <= ceiling) height = Mathf.Max(height, y);
            }
            return height;
        }
    }

    sealed class Patch
    {
        public Mesh Mesh;
        public int SourceVertices;
        public readonly List<Vector3> Vertices = new();
        public readonly List<int> Indices = new();
        public readonly List<Color32> Colors = new();
    }

    [Serializable]
    sealed class BimExtent
    {
        public Vector3 sourceWorldOrigin;
        public float startChainage, endChainage;
    }

    [MenuItem("Tools/UTTVR/BIM Viewer/Check Full Route Railbed")]
    public static void Check() => Bake(false);

    [MenuItem("Tools/UTTVR/BIM Viewer/Bake Full Route Railbed")]
    public static void Apply() => Bake(true);

    public static void UpdateBimSupport()
    {
        var extent = JsonUtility.FromJson<BimExtent>(File.ReadAllText(BimProvenance));
        var support = AssetDatabase.LoadAssetAtPath<Mesh>(BimSupport);
        if (support == null || support.subMeshCount != 1 || extent.endChainage <= extent.startChainage)
            throw new InvalidOperationException("Unexpected standalone Z021 BIM support layout.");
        var patch = new Patch { Mesh = support, SourceVertices = SourceVertexCount(support) };
        var preview = EditorSceneManager.OpenPreviewScene(Route);
        try
        {
            var registry = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RailwayMicroChunkStreamer>(true)).Single().data;
            var chunks = registry.chunks.Where(chunk => chunk.end > extent.startChainage + .001f && chunk.start < extent.endChainage - .001f).ToArray();
            foreach (var track in chunks.SelectMany(chunk => new[] { chunk.right, chunk.left }))
            {
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + track.meshResource + ".asset");
                var sourceCount = SourceVertexCount(source);
                var indices = source.GetTriangles(0);
                var basis = patch.SourceVertices + patch.Vertices.Count;
                var matrix = Matrix4x4.TRS(track.position - extent.sourceWorldOrigin, track.rotation, track.scale);
                patch.Vertices.AddRange(source.vertices.Skip(sourceCount).Select(matrix.MultiplyPoint3x4));
                patch.Colors.AddRange(source.colors32.Skip(sourceCount));
                patch.Indices.AddRange(indices.Where(index => index >= sourceCount).Select(index => basis + index - sourceCount));
            }
            if (patch.Indices.Count == 0 || patch.Indices.Count % 3 != 0)
                throw new InvalidOperationException("The Z021 route support has not been baked.");

            var colors = support.colors32;
            var positions = support.vertices;
            int removed = 0;
            bool Keep(int a, int b, int c)
            {
                bool IsWhite(int index) => colors[index].Equals(new Color32(255, 255, 255, 255));
                if (!IsWhite(a) || !IsWhite(b) || !IsWhite(c)) return true;
                if (Mathf.Max(positions[a].y, positions[b].y, positions[c].y) > .03f)
                    throw new InvalidOperationException("Unexpected white non-deck face in the Z021 support mesh.");
                removed++;
                return false;
            }
            Save(patch, Keep);
            File.WriteAllText("Temp/RailwayRailbedBim.txt", $"Z021: {chunks.Length * 2} shared profiles, {removed} legacy deck triangles removed, {patch.Indices.Count / 3} new support triangles.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    static void Bake(bool apply)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Use Edit Mode for railway ground checks.");
        var preview = EditorSceneManager.OpenPreviewScene(Route);
        try
        {
            var roots = preview.GetRootGameObjects();
            var manager = roots.SelectMany(root => root.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var registry = roots.SelectMany(root => root.GetComponentsInChildren<RailwayMicroChunkStreamer>(true)).Single().data;
            var ground = new Ground();
            foreach (var zone in manager.zones)
            {
                var source = EditorSceneManager.OpenPreviewScene(zone.scenePath);
                try
                {
                    foreach (var filter in source.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true)))
                        if (filter.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled && filter.gameObject.activeInHierarchy)
                        {
                            var name = filter.name;
                            if (name == "Pavement" || name.StartsWith("Deck with crossbeams", StringComparison.Ordinal))
                                ground.Add(filter, GroundKind.Bridge);
                            else if (name.StartsWith("Tunnel_C", StringComparison.Ordinal) && name.Contains("Tunnel lining concrete"))
                                ground.Add(filter, GroundKind.Tunnel);
                            else if (name.StartsWith("TerrainZone_", StringComparison.Ordinal) ||
                                filter.transform.parent?.name == "TerrainTransitions" ||
                                name.Contains("Lớp nền trên") || name.StartsWith("Subgrade shoulders", StringComparison.Ordinal) ||
                                name.StartsWith("TunnelMountain_", StringComparison.Ordinal) && name.Contains("_Ground") && !name.EndsWith("_Vegetation", StringComparison.Ordinal))
                                ground.Add(filter, GroundKind.Land);
                        }
                }
                finally { EditorSceneManager.ClosePreviewScene(source); }
            }

            var patches = new List<Patch>();
            var rows = new List<string> { "asset,start_m,end_m,old_gap_max_m,new_gap_max_m,bridge_rows,tunnel_rows,unverified_ground_rows,extra_triangles" };
            var spline = manager.trainMover.splineRoute;
            int missing = 0;
            foreach (var track in registry.chunks.SelectMany(chunk => new[] { chunk.right, chunk.left }))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + track.meshResource + ".asset");
                ground.Add(mesh, Matrix4x4.TRS(track.position, track.rotation, track.scale), GroundKind.Slab);
            }
            foreach (var chunk in registry.chunks.OrderBy(chunk => chunk.start))
                foreach (bool right in new[] { true, false })
                {
                    var track = right ? chunk.right : chunk.left;
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + track.meshResource + ".asset");
                    if (mesh == null || mesh.subMeshCount != 1 || !mesh.isReadable)
                        throw new InvalidOperationException("Unexpected track visual " + track.meshResource);
                    var patch = new Patch { Mesh = mesh, SourceVertices = SourceVertexCount(mesh) };
                    var inverse = Matrix4x4.TRS(track.position, track.rotation, track.scale).inverse;
                    var result = Generate(patch, inverse, spline, registry.length, manager, ground, chunk.start, chunk.end, right);
                    missing += result.missing;
                    patches.Add(patch);
                    rows.Add(FormattableString.Invariant($"{mesh.name},{chunk.start:F4},{chunk.end:F4},{result.before:F4},{result.after:F4},{result.bridge},{result.tunnel},{result.missing},{patch.Indices.Count / 3}"));
                }

            var report = apply ? "Temp/RailwayRailbedApplied.csv" : "Temp/RailwayRailbedPreview.csv";
            File.WriteAllLines(report, rows);
            if (missing != 0) throw new InvalidOperationException($"No ground surface verified for {missing} railbed sections. See {report}.");
            if (apply) foreach (var patch in patches) Save(patch);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    static (float before, float after, int bridge, int tunnel, int missing) Generate(Patch patch, Matrix4x4 inverse, SplineContainer spline,
        float length, RailwayZoneStreamingManager manager, Ground ground, float start, float end, bool rightTrack)
    {
        if (end - start < .001f) return default;
        var bridge = manager.fixedInfrastructure.Single(work => work.kind == "Bridge");
        var tunnels = manager.fixedInfrastructure.Where(work => work.kind == "TunnelMountain").ToArray();
        int segments = Mathf.Max(1, Mathf.CeilToInt((end - start) / 3));
        var rows = new List<(Vector3[] points, float gap, GroundKind kind)>();
        int bridgeRows = 0, tunnelRows = 0, missing = 0;
        float before = 0, after = 0;
        for (int i = 0; i <= segments; i++)
        {
            float d = Mathf.Lerp(start, end, i / (float)segments);
            Vector3 center = spline.EvaluatePosition(Mathf.Clamp01(d / length));
            Vector3 tangent = spline.EvaluateTangent(Mathf.Clamp01(d / length));
            var side = Vector3.Cross(Vector3.up, tangent).normalized;
            var direction = Vector3.ProjectOnPlane(tangent, Vector3.up).normalized;
            if (!rightTrack) center -= side * GaugeBetweenCentres;
            var slab = ground.Height(center, center.y + .04f, GroundKind.Slab);
            float top = (float.IsFinite(slab) ? slab : center.y) + .006f;
            float ceiling = top + ContactTolerance;
            float land = ground.Height(center, ceiling, GroundKind.Land);
            if (!float.IsFinite(land) && d < .025f)
                land = ground.Height(center + direction, ceiling, GroundKind.Land);
            float baseY = land;
            GroundKind kind = GroundKind.Land;
            if (d >= bridge.startChainage && d <= bridge.endChainage)
            {
                float floor = ground.Height(center, ceiling, GroundKind.Bridge);
                if (!float.IsFinite(floor))
                {
                    float near = ground.Height(center - direction * 1.25f, ceiling, GroundKind.Bridge);
                    float far = ground.Height(center + direction * 1.25f, ceiling, GroundKind.Bridge);
                    if (float.IsFinite(near) && float.IsFinite(far) && Mathf.Abs(near - far) < .25f) floor = (near + far) * .5f;
                    else if (d - bridge.startChainage < 8f)
                        floor = ground.Height(center + direction * 3f, ceiling, GroundKind.Bridge);
                    else if (bridge.endChainage - d < 8f)
                        floor = ground.Height(center - direction * 3f, ceiling, GroundKind.Bridge);
                }
                if (float.IsFinite(floor) && top - floor <= MaxBridgePlinthHeight)
                {
                    baseY = floor;
                    kind = GroundKind.Bridge;
                    bridgeRows++;
                }
                else if (top - baseY > MaxBridgePlinthHeight)
                {
                    missing++;
                    Debug.LogWarning($"Unverified bridge-deck contact at {d:F3} m ({(rightTrack ? "R" : "L")}), floor {floor:F3}, terrain {baseY:F3}.");
                    baseY = top;
                }
            }
            if (kind == GroundKind.Land && tunnels.Any(work => d >= work.startChainage && d <= work.endChainage))
            {
                float floor = ground.Height(center, ceiling, GroundKind.Tunnel);
                if (float.IsFinite(floor)) { baseY = floor; kind = GroundKind.Tunnel; tunnelRows++; }
            }
            if (!float.IsFinite(baseY))
            {
                missing++;
                Debug.LogWarning($"Unverified land contact at {d:F3} m ({(rightTrack ? "R" : "L")}).");
                baseY = top;
            }
            float gap = Mathf.Max(0, top - baseY);
            before = Mathf.Max(before, gap);
            float outside = kind == GroundKind.Land ? Mathf.Clamp(1.45f + 1.8f * gap, 2.35f, 65f) : 1.58f;
            float inside = kind == GroundKind.Land ? GaugeBetweenCentres * .5f : 1.58f;
            float left = rightTrack ? -inside : -outside;
            float right = rightTrack ? outside : inside;
            var points = new[] { center + side * left, center - side * 1.32f, center + side * 1.45f, center + side * right };
            points[1].y = points[2].y = top;
            foreach (int edge in new[] { 0, 3 })
            {
                float y = ground.Height(points[edge], ceiling, kind);
                // Deck and tunnel shoulders remain on the approved structure even when
                // a side sample is outside a tapered support surface.
                float bottom = kind == GroundKind.Land && float.IsFinite(y) ? y : baseY;
                points[edge].y = Mathf.Min(top - .03f, bottom - (kind == GroundKind.Land ? .045f : .012f));
                if (kind == GroundKind.Land && edge == (rightTrack ? 0 : 3))
                {
                    // Both tracks meet at the same centreline instead of leaving a
                    // trench between two high embankments. Match this shared border
                    // in world coordinates even when measured slab thickness varies.
                    points[edge].y = center.y - .12f;
                }
            }
            rows.Add((points, gap, kind));
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            foreach (var point in row.points)
            {
                patch.Vertices.Add(inverse.MultiplyPoint3x4(point));
                patch.Colors.Add(row.kind == GroundKind.Bridge ? BridgeConcrete : row.kind == GroundKind.Tunnel ? TunnelConcrete : Earth);
            }
            if (i == 0) continue;
            float gap = Mathf.Max(row.gap, rows[i - 1].gap);
            if (gap <= ContactTolerance) { after = Mathf.Max(after, gap); continue; }
            int startVertex = patch.SourceVertices + (i - 1) * 4;
            for (int side = 0; side < 3; side++)
            {
                int a = startVertex + side;
                patch.Indices.AddRange(new[] { a, a + 4, a + 1, a + 1, a + 4, a + 5 });
            }
        }
        return (before, after, bridgeRows, tunnelRows, missing);
    }

    static int SourceVertexCount(Mesh mesh)
    {
        var colors = mesh.colors32;
        if (colors.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount ||
            mesh.GetVertexAttributes().Any(a => a.attribute != UnityEngine.Rendering.VertexAttribute.Position &&
                a.attribute != UnityEngine.Rendering.VertexAttribute.Normal && a.attribute != UnityEngine.Rendering.VertexAttribute.Color))
            throw new InvalidOperationException("Unexpected vertex streams in " + mesh.name);
        bool GroundColor(Color32 color) => color.Equals(Earth) || color.Equals(BridgeConcrete) || color.Equals(TunnelConcrete);
        int index = Array.FindIndex(colors, GroundColor);
        if (index == -1) return colors.Length;
        if (colors.Skip(index).Any(color => !GroundColor(color)))
            throw new InvalidOperationException("Existing support geometry has lost its trailing vertex range in " + mesh.name);
        return index;
    }

    static void Save(Patch patch, Func<int, int, int, bool> keepSourceTriangle = null)
    {
        var mesh = patch.Mesh;
        if (patch.Indices.Count == 0 && patch.SourceVertices == mesh.vertexCount) return;
        var indices = mesh.GetTriangles(0);
        var retained = new List<int>(indices.Length + patch.Indices.Count);
        for (int i = 0; i < indices.Length; i += 3)
            if (indices[i] < patch.SourceVertices && indices[i + 1] < patch.SourceVertices && indices[i + 2] < patch.SourceVertices &&
                (keepSourceTriangle == null || keepSourceTriangle(indices[i], indices[i + 1], indices[i + 2])))
                retained.AddRange(new[] { indices[i], indices[i + 1], indices[i + 2] });
        var vertices = mesh.vertices.Take(patch.SourceVertices).Concat(patch.Vertices).ToArray();
        var colors = mesh.colors.Take(patch.SourceVertices).Concat(patch.Colors.Select(color => (Color)color)).ToArray();
        var normals = mesh.normals.Take(patch.SourceVertices).Concat(Enumerable.Repeat(Vector3.up, patch.Vertices.Count)).ToArray();
        for (int i = 0; i < patch.Indices.Count; i += 3)
        {
            int a = patch.Indices[i], b = patch.Indices[i + 1], c = patch.Indices[i + 2];
            var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).normalized;
            normals[a] += normal; normals[b] += normal; normals[c] += normal;
        }
        for (int i = patch.SourceVertices; i < normals.Length; i++) normals[i].Normalize();
        retained.AddRange(patch.Indices);
        mesh.SetVertices(vertices);
        if (vertices.Length > ushort.MaxValue) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetColors(colors);
        mesh.SetNormals(normals);
        mesh.SetTriangles(retained, 0, false);
        // The previous source retains unused Deck Girder vertices. Bounds must
        // follow submitted triangles, not those inactive source vertices.
        var bounds = new Bounds(vertices[retained[0]], Vector3.zero);
        foreach (int index in retained) bounds.Encapsulate(vertices[index]);
        mesh.bounds = bounds;
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);
    }
}
