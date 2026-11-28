using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class RailwayBimQuantities
{
    internal readonly struct Result
    {
        public readonly double SurfaceArea, Volume;
        public readonly bool Closed;
        public Result(double surfaceArea, double volume, bool closed)
        {
            SurfaceArea = surfaceArea;
            Volume = volume;
            Closed = closed;
        }
    }

    public static Result Measure(Mesh mesh, Vector3 scale)
    {
        var vertices = mesh.vertices;
        var origin = mesh.bounds.center;
        var points = new Vector3Int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = Vector3.Scale(vertices[i] - origin, scale);
            points[i] = Vector3Int.RoundToInt(vertices[i] * 10000);
        }
        var edges = new Dictionary<(Vector3Int, Vector3Int), int>();
        var pairs = new Dictionary<(Vector3Int, Vector3Int), int>();
        double area = 0, signedVolume = 0;
        bool consistent = true;
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
            var indices = mesh.GetIndices(sub);
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                var cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (cross.sqrMagnitude <= 1e-14f) continue;
                area += cross.magnitude * .5;
                signedVolume += Vector3.Dot(vertices[a], cross) / 6.0;
                if (!consistent) continue;
                consistent = Edge(points[a], points[b]) && Edge(points[b], points[c]) && Edge(points[c], points[a]);
            }
        }
        bool closed = consistent && edges.Count > 0 && pairs.Values.All(value => value == 2) &&
            edges.Values.All(value => value == 0);
        return new Result(area, closed ? Math.Abs(signedVolume) : 0, closed);

        bool Edge(Vector3Int a, Vector3Int b)
        {
            if (a == b) return false;
            bool forward = a.x < b.x || a.x == b.x && (a.y < b.y || a.y == b.y && a.z < b.z);
            var key = forward ? (a, b) : (b, a);
            int count = pairs.GetValueOrDefault(key);
            if (count >= 2) return false;
            pairs[key] = count + 1;
            edges[key] = edges.GetValueOrDefault(key) + (forward ? 1 : -1);
            return true;
        }
    }
}
