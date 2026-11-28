using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class RailwayBimInteriorVisibility
{
    private readonly Dictionary<Renderer, (bool original, Renderer owner)> suppressed = new();
    private readonly Dictionary<Mesh, Plane[]> convexMeshes = new();

    internal int HiddenCount => suppressed.Count;

    internal void Restore()
    {
        foreach (var item in suppressed)
            if (item.Key != null) item.Key.forceRenderingOff = item.Value.original;
        suppressed.Clear();
    }

    internal void Cull(IReadOnlyList<Renderer> renderers, IReadOnlyCollection<Renderer> keepVisible)
    {
        Restore();
        var opaque = renderers.Where(IsOpaque).Select(renderer => (renderer, bounds: renderer.bounds)).ToArray();
        var possibleContainers = opaque.Where(item => ContainerPlanes(Surface(item.renderer)).Length > 0).ToArray();
        foreach (var item in opaque)
        {
            if (keepVisible.Contains(item.renderer)) continue;
            foreach (var shell in possibleContainers)
            {
                if (shell.renderer == item.renderer || !StrictlyContains(shell.bounds, item.bounds)) continue;
                var transform = shell.renderer.worldToLocalMatrix;
                var planes = ContainerPlanes(Surface(shell.renderer));
                var min = item.bounds.min;
                var max = item.bounds.max;
                bool contained = true;
                for (int corner = 0; corner < 8 && contained; corner++)
                {
                    var position = transform.MultiplyPoint3x4(new Vector3((corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z));
                    foreach (var plane in planes)
                        if (plane.GetDistanceToPoint(position) >= -.002f) { contained = false; break; }
                }
                if (!contained) continue;
                suppressed.Add(item.renderer, (item.renderer.forceRenderingOff, shell.renderer));
                item.renderer.forceRenderingOff = true;
                break;
            }
        }
    }

    internal void UpdateCamera(Vector3 eye)
    {
        foreach (var item in suppressed)
            if (item.Key != null)
            {
                var owner = item.Value.owner;
                item.Key.forceRenderingOff = item.Value.original || owner != null && owner.enabled &&
                    owner.gameObject.activeInHierarchy && !owner.bounds.Contains(eye);
            }
    }

    internal void Reset()
    {
        Restore();
        convexMeshes.Clear();
    }

    static Mesh Surface(Renderer renderer) => renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;

    static bool StrictlyContains(Bounds outer, Bounds inner)
    {
        var margin = Vector3.one * .002f;
        return outer.Contains(inner.min - margin) && outer.Contains(inner.max + margin);
    }

    static bool IsOpaque(Renderer renderer) => RailwayBimOutline.IsRenderable(renderer) &&
        renderer.localToWorldMatrix.determinant > 0 && renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(material => material != null &&
            material.renderQueue < 2450 && material.GetTag("RenderType", false) == "Opaque" &&
            !material.IsKeywordEnabled("_ALPHATEST_ON") &&
            (!material.HasProperty("_AlphaClip") || material.GetFloat("_AlphaClip") == 0) &&
            (!material.HasProperty("_ZWrite") || material.GetFloat("_ZWrite") > 0) &&
            (!material.HasProperty("_Cull") || material.GetFloat("_Cull") == 2));

    Plane[] ContainerPlanes(Mesh mesh)
    {
        if (mesh == null) return System.Array.Empty<Plane>();
        if (convexMeshes.TryGetValue(mesh, out var cached)) return cached;
        if (convexMeshes.Count >= 512) convexMeshes.Clear();
        return convexMeshes[mesh] = ConvexClosedPlanes(mesh);
    }

    static Plane[] ConvexClosedPlanes(Mesh mesh)
    {
        var none = System.Array.Empty<Plane>();
        if (!mesh.isReadable || mesh.subMeshCount != 1 || mesh.GetIndexCount(0) > 288 ||
            mesh.GetTopology(0) != MeshTopology.Triangles) return none;
        var vertices = mesh.vertices;
        var indices = mesh.GetTriangles(0);
        var welded = new Dictionary<Vector3Int, int>();
        var edges = new Dictionary<(int, int), int>();
        var planes = new List<Plane>();
        var pivot = mesh.bounds.center;
        for (int i = 0; i < indices.Length; i += 3)
        {
            var a = vertices[indices[i]];
            var b = vertices[indices[i + 1]];
            var c = vertices[indices[i + 2]];
            int ia = Weld(a), ib = Weld(b), ic = Weld(c);
            if (ia == ib || ib == ic || ic == ia) return none;
            AddEdge(ia, ib); AddEdge(ib, ic); AddEdge(ic, ia);
            var normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.sqrMagnitude < .9f) return none;
            if (Vector3.Dot(normal, pivot - a) > 0) return none;
            var plane = new Plane(normal, a);
            if (vertices.Any(vertex => plane.GetDistanceToPoint(vertex) > .0002f)) return none;
            planes.Add(plane);
        }
        return planes.Count >= 4 && edges.Values.All(count => count == 2) ? planes.ToArray() : none;

        int Weld(Vector3 vertex)
        {
            var key = Vector3Int.RoundToInt(vertex * 10000);
            if (!welded.TryGetValue(key, out int index)) welded.Add(key, index = welded.Count);
            return index;
        }

        void AddEdge(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            edges[key] = edges.GetValueOrDefault(key) + 1;
        }
    }
}
