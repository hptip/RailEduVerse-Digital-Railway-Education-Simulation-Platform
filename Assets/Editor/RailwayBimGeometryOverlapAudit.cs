using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Audits what can render together. Equal triangles and coplanar overlap are evidence;
// AABB overlap and a shared edge alone do not imply a duplicate or a volume collision.
internal static partial class RailwayBimGeometryOverlapAudit
{
    const float Tolerance = .002f;
    const float MinimumArea = .000001f;
    const string RoutePath = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string BimPath = "Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity";
    const string Prefabs = "Assets/ExternalBIM/ZoneBased/Resources/RouteBimZones/";
    const string Rails = "Assets/ProjectLopHoc/Generated/QuestRailway/Resources/";

    sealed class Surface
    {
        public string Name, Zone, Identity;
        public Mesh Mesh;
        public Matrix4x4 Matrix;
        public Bounds Bounds;
        public Triangle[] Triangles;
        public Node Tree;
        public bool? Closed;
    }

    struct Triangle
    {
        public Vector3 A, B, C, Normal, Center;
        public Bounds Bounds;
        public int Submesh, Offset;
    }

    sealed class Node
    {
        public Bounds Bounds;
        public int First, End;
        public Node Left, Right;
    }

    struct Overlap
    {
        public int SameNormal, Opposed, Crossings;
        public float Area;
    }

    [MenuItem("Tools/UTTVR/BIM Viewer/Audit Production Geometry")]
    public static void Run()
    {
        var route = EditorSceneManager.OpenPreviewScene(RoutePath);
        var bim = EditorSceneManager.OpenPreviewScene(BimPath);
        var rows = new List<string> { "view,classification,first,second,count,area_m2" };
        try
        {
            var manager = Objects(route).OfType<RailwayZoneStreamingManager>().Single();
            var streamer = Objects(route).OfType<RailwayMicroChunkStreamer>().Single();
            var viewer = Objects(bim).OfType<RailwayBimZoneViewer>().Single();
            var routeVisual = new List<Surface>();
            var bimVisual = new List<Surface>();
            AddScene(routeVisual, route, "Base");
            AddScene(bimVisual, bim, "Base");
            foreach (var zone in manager.zones)
            {
                var scene = EditorSceneManager.OpenPreviewScene(zone.scenePath);
                try { AddScene(routeVisual, scene, zone.zoneId); }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + zone.zoneId + ".prefab");
                if (prefab != null) AddFilters(bimVisual, prefab.GetComponentsInChildren<MeshFilter>(true), zone.zoneId, prefab.transform);
            }
            foreach (var chunk in streamer.data.chunks)
            {
                foreach (var track in new[] { chunk.right, chunk.left })
                {
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Rails + track.meshResource + ".asset");
                    Add(routeVisual, mesh, Matrix4x4.TRS(track.position, track.rotation, track.scale),
                        track.meshResource, "MicroChunk");
                }
            }

            var logical = viewer.facilities.Select(f => f.covered).Concat(RailwayBimLogicalLayout.Sections
                .Select(s => s.Zones)).Select(z => new HashSet<string>(z, StringComparer.Ordinal)).ToArray();
            Audit("YenTuHaLongXanh · Visual", routeVisual, (_, _) => true, rows);
            Audit("QuanLyVongDoiTaiSan · Visual", bimVisual,
                (a, b) => a.Zone == b.Zone || (a.Zone != "Base" && b.Zone != "Base" &&
                    logical.Any(f => f.Contains(a.Zone) && f.Contains(b.Zone))), rows);
            AuditBatches(viewer.productionLoader, bimVisual, rows);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(bim);
            EditorSceneManager.ClosePreviewScene(route);
        }
        var report = "Documentation/InfrastructureDesign/Evidence/Validation_Results.csv";
        File.WriteAllLines(report, rows);
        Debug.Log($"BIM Geometry Overlap Audit wrote {rows.Count - 1} observations to {report}");
    }

    static IEnumerable<Component> Objects(Scene scene) =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true));

    static void AddScene(List<Surface> output, Scene scene, string zone)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (!root.activeSelf) continue;
            AddFilters(output, root.GetComponentsInChildren<MeshFilter>(), zone);
        }
    }

    static void AddFilters(List<Surface> output, IEnumerable<MeshFilter> filters, string zone, Transform instanceRoot = null)
    {
        foreach (var f in filters)
            if (VisibleInInstance(f.transform, instanceRoot) && f.TryGetComponent<MeshRenderer>(out var renderer) &&
                renderer.enabled && !renderer.forceRenderingOff)
                Add(output, f.sharedMesh, f.transform.localToWorldMatrix, f.name, zone);
    }

    static bool VisibleInInstance(Transform target, Transform instanceRoot)
    {
        if (instanceRoot == null) return target.gameObject.activeInHierarchy;
        for (var t = target; t != null && t != instanceRoot; t = t.parent)
            if (!t.gameObject.activeSelf) return false;
        return true;
    }

    static void Add(List<Surface> output, Mesh mesh, Matrix4x4 matrix, string name, string zone)
    {
        if (mesh == null) return;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId);
        var b = mesh.bounds;
        var world = new Bounds(matrix.MultiplyPoint3x4(b.min), Vector3.zero);
        for (int i = 1; i < 8; i++)
            world.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y,
                (i & 4) == 0 ? b.min.z : b.max.z)));
        world.Expand(Tolerance);
        output.Add(new Surface { Zone = zone, Name = name, Mesh = mesh, Matrix = matrix, Bounds = world,
            Identity = string.IsNullOrEmpty(guid) ? $"instance:{mesh.GetInstanceID()}" : $"{guid}:{localId}" });
    }

    static void Audit(string view, List<Surface> surfaces, Func<Surface, Surface, bool> simultaneous, List<string> rows)
    {
        surfaces.Sort((a, b) => a.Bounds.min.x.CompareTo(b.Bounds.min.x));
        int pairs = 0, withoutTriangles = 0;
        for (int i = 0; i < surfaces.Count; i++)
        {
            var a = surfaces[i];
            for (int j = i + 1; j < surfaces.Count && surfaces[j].Bounds.min.x <= a.Bounds.max.x; j++)
            {
                var b = surfaces[j];
                if (!simultaneous(a, b) || !a.Bounds.Intersects(b.Bounds)) continue;
                pairs++;
                if (a.Identity == b.Identity && SameMatrix(a.Matrix, b.Matrix))
                {
                    Row(rows, view, "OBJECT_DUPLICATE", Label(a), Label(b), 1, 0);
                    continue;
                }
                Prepare(a);
                Prepare(b);
                if (a.Triangles.Length > 0 && a.Triangles.Length == b.Triangles.Length &&
                    (a.Bounds.center - b.Bounds.center).sqrMagnitude < Tolerance * Tolerance &&
                    (a.Bounds.size - b.Bounds.size).sqrMagnitude < Tolerance * Tolerance &&
                    Equivalent(a.Triangles, b.Triangles))
                {
                    Row(rows, view, "MESH_DUPLICATE", Label(a), Label(b), a.Triangles.Length, 0);
                    continue;
                }
                ComparePair(view, a, b, rows);
            }
            Prepare(a);
            if (a.Triangles.Length == 0) withoutTriangles++;
            else ComparePair(view, a, a, rows);
            // X sweep has finished comparing this surface. Release its temporary tree.
            a.Tree = null;
            a.Triangles = null;
        }
        Row(rows, view, "SURFACES", "", "", surfaces.Count, 0);
        Row(rows, view, "BOUNDS_CANDIDATES", "", "", pairs, 0);
        Row(rows, view, "READ_ONLY_IMPORTED_MESH", "", "", surfaces.Count(s => !s.Mesh.isReadable), 0);
        Row(rows, view, "WITHOUT_RENDER_TRIANGLES", "", "", withoutTriangles, 0);
    }

    static string Label(Surface surface) => surface.Zone + "/" + surface.Name;

    static bool SameMatrix(Matrix4x4 a, Matrix4x4 b)
    {
        for (int i = 0; i < 16; i++)
            if (Mathf.Abs(a[i] - b[i]) > .00001f) return false;
        return true;
    }

    static void ComparePair(string view, Surface a, Surface b, List<string> rows)
    {
        if (a.Tree == null || b.Tree == null) return;
        var overlap = new Overlap();
        IntersectTrees(a, a.Tree, b, b.Tree, ref overlap);
        if (overlap.SameNormal > 0)
            Row(rows, view, "COPLANAR_SAME_NORMAL", Label(a), Label(b), overlap.SameNormal, overlap.Area);
        if (overlap.Opposed > 0)
            Row(rows, view, "OPPOSING_CONTACT_REVIEW", Label(a), Label(b), overlap.Opposed, 0);
        if (overlap.Crossings > 0)
            Row(rows, view, "CROSSING_SURFACES_REVIEW", Label(a), Label(b), overlap.Crossings, 0);
        if (!ReferenceEquals(a, b) && overlap.Crossings > 0 && TryVolumeWitness(a, b))
            Row(rows, view, "CLOSED_VOLUME_OVERLAP_REVIEW", Label(a), Label(b), 1, 0);
    }

    static void Prepare(Surface s)
    {
        if (s.Triangles != null) return;
        using var meshData = MeshUtility.AcquireReadOnlyMeshData(s.Mesh);
        var data = meshData[0];
        if (data.vertexCount == 0 || !data.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Position))
        {
            s.Triangles = Array.Empty<Triangle>();
            return;
        }
        using var positions = new NativeArray<Vector3>(data.vertexCount, Allocator.TempJob);
        data.GetVertices(positions);
        var vertices = new Vector3[positions.Length];
        for (int i = 0; i < vertices.Length; i++) vertices[i] = s.Matrix.MultiplyPoint3x4(positions[i]);
        var tris = new List<Triangle>();
        bool wide = data.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32;
        var ix32 = wide ? data.GetIndexData<uint>() : default;
        var ix16 = wide ? default : data.GetIndexData<ushort>();
        int IndexAt(int i) => wide ? (int)ix32[i] : ix16[i];
        for (int part = 0; part < data.subMeshCount; part++)
        {
            var submesh = data.GetSubMesh(part);
            if (submesh.topology != MeshTopology.Triangles) continue;
            for (int i = 0; i < submesh.indexCount; i += 3)
            {
                int index = submesh.indexStart + i;
                var a = vertices[IndexAt(index) + submesh.baseVertex];
                var b = vertices[IndexAt(index + 1) + submesh.baseVertex];
                var c = vertices[IndexAt(index + 2) + submesh.baseVertex];
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-14f) continue;
                var bounds = new Bounds(a, Vector3.zero);
                bounds.Encapsulate(b);
                bounds.Encapsulate(c);
                bounds.Expand(Tolerance);
                tris.Add(new Triangle { A = a, B = b, C = c, Normal = normal.normalized, Submesh = part, Offset = i,
                    Center = (a + b + c) / 3, Bounds = bounds });
            }
        }
        s.Triangles = tris.ToArray();
        if (s.Triangles.Length > 0) s.Tree = Partition(s.Triangles, 0, s.Triangles.Length);
    }

    static Node Partition(Triangle[] triangles, int start, int end)
    {
        var bounds = triangles[start].Bounds;
        for (int i = start + 1; i < end; i++) bounds.Encapsulate(triangles[i].Bounds);
        var node = new Node { Bounds = bounds, First = start, End = end };
        if (end - start <= 12) return node;
        int axis = bounds.size.x > bounds.size.y
            ? (bounds.size.x > bounds.size.z ? 0 : 2)
            : (bounds.size.y > bounds.size.z ? 1 : 2);
        Array.Sort(triangles, start, end - start, Comparer<Triangle>.Create((a, b) => a.Center[axis].CompareTo(b.Center[axis])));
        int middle = (start + end) / 2;
        node.Left = Partition(triangles, start, middle);
        node.Right = Partition(triangles, middle, end);
        return node;
    }

    static void IntersectTrees(Surface a, Node left, Surface b, Node right, ref Overlap result)
    {
        if (!left.Bounds.Intersects(right.Bounds) || ReferenceEquals(a, b) && left.First >= right.End) return;
        if (left.Left == null && right.Left == null)
        {
            for (int i = left.First; i < left.End; i++)
                for (int j = right.First; j < right.End; j++)
                    if (!ReferenceEquals(a, b) || j > i)
                        Test(a.Triangles[i], b.Triangles[j], ref result);
        }
        else if (right.Left == null || left.Left != null && left.End - left.First >= right.End - right.First)
        {
            IntersectTrees(a, left.Left, b, right, ref result);
            IntersectTrees(a, left.Right, b, right, ref result);
        }
        else
        {
            IntersectTrees(a, left, b, right.Left, ref result);
            IntersectTrees(a, left, b, right.Right, ref result);
        }
    }

    static void Test(Triangle a, Triangle b, ref Overlap result)
    {
        if (!a.Bounds.Intersects(b.Bounds)) return;
        float dot = Vector3.Dot(a.Normal, b.Normal);
        if (Mathf.Abs(dot) >= .9999f &&
            Mathf.Abs(Vector3.Dot(a.Normal, b.A - a.A)) <= Tolerance &&
            Mathf.Abs(Vector3.Dot(a.Normal, b.B - a.A)) <= Tolerance &&
            Mathf.Abs(Vector3.Dot(a.Normal, b.C - a.A)) <= Tolerance)
        {
            float area = SharedArea(a, b);
            if (area <= MinimumArea) return;
            if (dot < 0) result.Opposed++;
            else { result.SameNormal++; result.Area += area; }
        }
        else if (Crosses(a.A, a.B, b) || Crosses(a.B, a.C, b) || Crosses(a.C, a.A, b) ||
            Crosses(b.A, b.B, a) || Crosses(b.B, b.C, a) || Crosses(b.C, b.A, a))
            result.Crossings++;
    }

    static bool Crosses(Vector3 from, Vector3 to, Triangle t)
    {
        float a = Vector3.Dot(t.Normal, from - t.A), b = Vector3.Dot(t.Normal, to - t.A);
        if ((a >= 0) == (b >= 0) || Mathf.Abs(a) < Tolerance || Mathf.Abs(b) < Tolerance) return false;
        var p = Vector3.LerpUnclamped(from, to, a / (a - b));
        return Vector3.Dot(Vector3.Cross(t.B - t.A, p - t.A), t.Normal) > MinimumArea &&
            Vector3.Dot(Vector3.Cross(t.C - t.B, p - t.B), t.Normal) > MinimumArea &&
            Vector3.Dot(Vector3.Cross(t.A - t.C, p - t.C), t.Normal) > MinimumArea;
    }

    static bool TryVolumeWitness(Surface a, Surface b)
    {
        var min = Vector3.Max(a.Bounds.min, b.Bounds.min);
        var max = Vector3.Min(a.Bounds.max, b.Bounds.max);
        var size = max - min;
        if (size.x <= Tolerance * 2 || size.y <= Tolerance * 2 || size.z <= Tolerance * 2) return false;
        a.Closed ??= IsClosed(a.Triangles);
        if (!a.Closed.Value) return false;
        b.Closed ??= IsClosed(b.Triangles);
        if (!b.Closed.Value) return false;
        bool Both(Vector3 point) => Inside(a, point) && Inside(b, point);
        var overlap = new Bounds((min + max) * .5f, size);
        if (Both(overlap.center)) return true;
        foreach (var part in new[] { a, b })
        {
            int checkedPoints = 0;
            foreach (var triangle in part.Triangles)
            {
                if (!overlap.Contains(triangle.Center)) continue;
                var delta = triangle.Normal * (Tolerance * 2);
                if (Both(triangle.Center - delta) || Both(triangle.Center + delta)) return true;
                if (++checkedPoints == 16) break;
            }
        }
        return false;
    }

    static bool IsClosed(Triangle[] triangles)
    {
        var edges = new Dictionary<(Vector3Int, Vector3Int), (int count, int direction)>();
        foreach (var t in triangles)
        {
            var a = Vector3Int.RoundToInt(t.A * 1000);
            var b = Vector3Int.RoundToInt(t.B * 1000);
            var c = Vector3Int.RoundToInt(t.C * 1000);
            if (a == b || b == c || c == a || !Advance(a, b) || !Advance(b, c) || !Advance(c, a)) return false;
        }
        return edges.Count > 0 && edges.Values.All(value => value.count == 2 && value.direction == 0);

        bool Advance(Vector3Int a, Vector3Int b)
        {
            int direction = Compare(a, b) < 0 ? 1 : -1;
            var key = direction == 1 ? (a, b) : (b, a);
            var prior = edges.GetValueOrDefault(key);
            if (prior.count >= 2) return false;
            edges[key] = (prior.count + 1, prior.direction + direction);
            return true;
        }
    }

    static bool Inside(Surface surface, Vector3 point)
    {
        if (!surface.Bounds.Contains(point)) return false;
        var ray = new Ray(point, new Vector3(.748f, .382f, .541f).normalized);
        var distances = new List<float>();
        Cast(surface.Tree);
        distances.Sort();
        int crossings = 0;
        float previous = float.NegativeInfinity;
        foreach (float distance in distances)
        {
            if (distance <= .0001f) return false; // Intersection lies on the surface.
            if (distance - previous > .0001f) crossings++;
            previous = distance;
        }
        return crossings % 2 != 0;

        void Cast(Node node)
        {
            if (node == null || !node.Bounds.IntersectRay(ray)) return;
            if (node.Left != null) { Cast(node.Left); Cast(node.Right); return; }
            for (int i = node.First; i < node.End; i++)
            {
                var t = surface.Triangles[i];
                var edge1 = t.B - t.A;
                var edge2 = t.C - t.A;
                var p = Vector3.Cross(ray.direction, edge2);
                float det = Vector3.Dot(edge1, p);
                if (Mathf.Abs(det) < 1e-10f) continue;
                var delta = ray.origin - t.A;
                float u = Vector3.Dot(delta, p) / det;
                if (u < 0 || u > 1) continue;
                var q = Vector3.Cross(delta, edge1);
                float v = Vector3.Dot(ray.direction, q) / det;
                if (v < 0 || u + v > 1) continue;
                float d = Vector3.Dot(edge2, q) / det;
                if (d >= 0) distances.Add(d);
            }
        }
    }

    static float SharedArea(Triangle a, Triangle b)
    {
        var u = (a.B - a.A).normalized;
        var v = Vector3.Cross(a.Normal, u);
        Vector2 Project(Vector3 p) => new(Vector3.Dot(p - a.A, u), Vector3.Dot(p - a.A, v));
        Span<Vector2> clip = stackalloc Vector2[3] { Vector2.zero, Project(a.B), Project(a.C) };
        Span<Vector2> input = stackalloc Vector2[8];
        Span<Vector2> output = stackalloc Vector2[8];
        input[0] = Project(b.A); input[1] = Project(b.B); input[2] = Project(b.C);
        int count = 3;
        for (int edge = 0; edge < 3 && count > 0; edge++)
        {
            var origin = clip[edge];
            var direction = clip[(edge + 1) % 3] - origin;
            int nextCount = 0;
            for (int i = 0; i < count; i++)
            {
                var p = input[i];
                var q = input[(i + 1) % count];
                float left = Cross(direction, p - origin), right = Cross(direction, q - origin);
                if (left >= 0) output[nextCount++] = p;
                if ((left >= 0) != (right >= 0)) output[nextCount++] = Vector2.LerpUnclamped(p, q, left / (left - right));
            }
            output[..nextCount].CopyTo(input);
            count = nextCount;
        }
        float area = 0;
        for (int i = 0; i < count; i++) area += Cross(input[i], input[(i + 1) % count]);
        return Mathf.Abs(area) * .5f;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static bool Equivalent(Triangle[] a, Triangle[] b)
    {
        var keys = new Dictionary<(Vector3Int, Vector3Int, Vector3Int), int>();
        foreach (var triangle in a)
        {
            var key = Key(triangle);
            keys[key] = keys.GetValueOrDefault(key) + 1;
        }
        foreach (var triangle in b)
        {
            var key = Key(triangle);
            if (!keys.TryGetValue(key, out int count) || count == 0) return false;
            keys[key] = count - 1;
        }
        return true;
    }

    static (Vector3Int, Vector3Int, Vector3Int) Key(Triangle t)
    {
        var a = Vector3Int.RoundToInt(t.A * 1000);
        var b = Vector3Int.RoundToInt(t.B * 1000);
        var c = Vector3Int.RoundToInt(t.C * 1000);
        if (Compare(a, b) > 0) (a, b) = (b, a);
        if (Compare(b, c) > 0) (b, c) = (c, b);
        if (Compare(a, b) > 0) (a, b) = (b, a);
        return (a, b, c);
    }

    static int Compare(Vector3Int a, Vector3Int b) =>
        a.x != b.x ? a.x.CompareTo(b.x) : a.y != b.y ? a.y.CompareTo(b.y) : a.z.CompareTo(b.z);

    static void AuditBatches(InfrastructureDetailedLoader loader, List<Surface> visual, List<string> rows)
    {
        foreach (var group in loader.occurrences.Where(o => !string.IsNullOrEmpty(o.visualBatch) &&
            o.visualBatch != "HIDDEN_IN_VISUAL").GroupBy(o => o.visualBatch))
        {
            var owners = visual.Where(s => s.Name == group.Key).ToArray();
            if (owners.Length != 1) Row(rows, "BIM switch", "VISUAL_OWNER_COUNT_REVIEW", group.Key, "", owners.Length, 0);
            if (group.Select(o => (o.domain, o.chunk)).Distinct().Count() > 1)
                Row(rows, "BIM switch", "VISUAL_BATCH_CROSSES_DETAIL_CHUNK", group.Key, "", group.Count(), 0);
        }
        Row(rows, "BIM switch", "DETAIL_RECORDS", "", "", loader.occurrences.Length, 0);
    }

    static void Row(List<string> rows, string scene, string classification, string a, string b, int count, float area)
    {
        string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        rows.Add(string.Join(",", Quote(scene), classification, Quote(a), Quote(b), count,
            area.ToString("F6", CultureInfo.InvariantCulture)));
    }
}
