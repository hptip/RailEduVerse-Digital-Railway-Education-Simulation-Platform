using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal static class RailwayBimMeshSection
{
    struct Vertex
    {
        public Vector3 Position, Normal;
        public Vector2 UV;
        public Vector4 Tangent;
        public Color Color;

        public static Vertex Lerp(Vertex a, Vertex b, float t) => new()
        {
            Position = Vector3.LerpUnclamped(a.Position, b.Position, t),
            Normal = Vector3.LerpUnclamped(a.Normal, b.Normal, t).normalized,
            UV = Vector2.LerpUnclamped(a.UV, b.UV, t),
            Tangent = Vector4.LerpUnclamped(a.Tangent, b.Tangent, t),
            Color = Color.LerpUnclamped(a.Color, b.Color, t)
        };
    }

    public static Mesh Cut(Mesh mesh, Matrix4x4 localToWorld, Plane plane)
    {
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var uv = mesh.uv;
        var tangents = mesh.tangents;
        var colors = mesh.colors;
        var data = new Vertex[vertices.Length];
        var distance = new float[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            distance[i] = plane.GetDistanceToPoint(localToWorld.MultiplyPoint3x4(vertices[i]));
            data[i] = new Vertex { Position = vertices[i], Normal = normals.Length == vertices.Length ? normals[i] : Vector3.up,
                UV = uv.Length == vertices.Length ? uv[i] : Vector2.zero,
                Tangent = tangents.Length == vertices.Length ? tangents[i] : new Vector4(1, 0, 0, 1),
                Color = colors.Length == vertices.Length ? colors[i] : Color.white };
        }
        var output = new List<Vertex>();
        var submeshes = new List<int>[mesh.subMeshCount];
        var face = new Vertex[4];
        for (int sub = 0; sub < submeshes.Length; sub++)
        {
            var triangles = submeshes[sub] = new List<int>();
            if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
            var indices = mesh.GetIndices(sub);
            for (int i = 0; i < indices.Length; i += 3)
            {
                int count = 0;
                for (int edge = 0; edge < 3; edge++)
                {
                    int a = indices[i + edge], b = indices[i + (edge + 1) % 3];
                    if (distance[a] >= 0) face[count++] = data[a];
                    if ((distance[a] >= 0) != (distance[b] >= 0))
                        face[count++] = Vertex.Lerp(data[a], data[b], distance[a] / (distance[a] - distance[b]));
                }
                if (count < 3) continue;
                int start = output.Count;
                for (int v = 0; v < count; v++) output.Add(face[v]);
                for (int v = 1; v + 1 < count; v++)
                {
                    triangles.Add(start);
                    triangles.Add(start + v);
                    triangles.Add(start + v + 1);
                }
            }
        }
        if (output.Count == 0) return null;
        var section = new Mesh { name = mesh.name + " · mặt cắt BIM", hideFlags = HideFlags.DontSave,
            indexFormat = output.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        var positions = new List<Vector3>(output.Count);
        var normalList = new List<Vector3>(output.Count);
        var uvList = new List<Vector2>(output.Count);
        var tangentList = new List<Vector4>(output.Count);
        var colorList = new List<Color>(output.Count);
        foreach (var v in output)
        {
            positions.Add(v.Position);
            normalList.Add(v.Normal);
            uvList.Add(v.UV);
            tangentList.Add(v.Tangent);
            colorList.Add(v.Color);
        }
        section.SetVertices(positions);
        section.SetNormals(normalList);
        section.SetUVs(0, uvList);
        section.SetTangents(tangentList);
        section.SetColors(colorList);
        section.subMeshCount = submeshes.Length;
        for (int sub = 0; sub < submeshes.Length; sub++) section.SetTriangles(submeshes[sub], sub, false);
        section.RecalculateBounds();
        return section;
    }
}
