using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Generated rail meshes may carry an overlapping module from consecutive source
// chunks. Keep the first owner only when all three world vertices and winding
// match on the millimetre grid. Partial overlap stays for the geometric audit.
internal static class RailwayMicroChunkOwnership
{
    const string ResourcesPath = "Assets/ProjectLopHoc/Generated/QuestRailway/Resources/";
    const string RoutePath = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";

    [MenuItem("Tools/UTTVR/BIM Viewer/Measure Exact Rail Overlap")]
    public static void Measure() => Run(false);

    [MenuItem("Tools/UTTVR/BIM Viewer/Remove Exact Rail Overlap")]
    public static void Remove() => Run(true);

    static void Run(bool apply)
    {
        var scene = EditorSceneManager.OpenPreviewScene(RoutePath);
        try
        {
            var streamer = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<RailwayMicroChunkStreamer>(true)).Single();
            var result = Process(streamer.data, apply);
            Debug.Log($"[BIM Viewer] Exact rail ownership: {(apply ? "applied" : "measured")}, " +
                $"{result.faces} duplicate faces in {result.meshes} mesh resources.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    public static (int faces, int meshes) Process(RailwayMicroChunkData data, bool apply)
    {
        var chunks = data.chunks.OrderBy(chunk => chunk.start).ToArray();
        var changes = new Dictionary<Mesh, List<int>[]>();
        int faces = 0;
        for (int side = 0; side < 2; side++)
        {
            HashSet<(Vector3Int, Vector3Int, Vector3Int, Color32, Color32, Color32)> previous = null;
            RailwayMicroChunkData.Track priorTrack = null;
            float lastEnd = float.NegativeInfinity;
            foreach (var c in chunks)
            {
                var track = side == 0 ? c.right : c.left;
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ResourcesPath + track.meshResource + ".asset");
                if (mesh == null || !mesh.isReadable) throw new InvalidOperationException(track.meshResource);
                bool contiguous = Mathf.Abs(lastEnd - c.start) < .01f && priorTrack.materials.SequenceEqual(track.materials);
                if (!contiguous) previous = null;
                var matrix = Matrix4x4.TRS(track.position, track.rotation, track.scale);
                var points = mesh.vertices.Select(p => Vector3Int.RoundToInt(matrix.MultiplyPoint3x4(p) * 1000)).ToArray();
                var colors = mesh.colors32;
                Color32 ColorAt(int i) => colors.Length == points.Length ? colors[i] : default;
                var current = new HashSet<(Vector3Int, Vector3Int, Vector3Int, Color32, Color32, Color32)>();
                var output = new List<int>[mesh.subMeshCount];
                int removed = 0;
                for (int sub = 0; sub < output.Length; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidOperationException(mesh.name);
                    var indices = mesh.GetIndices(sub);
                    var keep = output[sub] = new List<int>(indices.Length);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        int a = indices[i], b = indices[i + 1], e = indices[i + 2];
                        if (Less(points[b], points[a]) && !Less(points[e], points[b])) (a, b, e) = (b, e, a);
                        else if (Less(points[e], points[a]) && Less(points[e], points[b])) (a, b, e) = (e, a, b);
                        var key = (points[a], points[b], points[e], ColorAt(a), ColorAt(b), ColorAt(e));
                        if (mesh.subMeshCount == 1 && previous != null && previous.Contains(key))
                        {
                            removed++;
                            continue;
                        }
                        current.Add(key);
                        keep.Add(indices[i]);
                        keep.Add(indices[i + 1]);
                        keep.Add(indices[i + 2]);
                    }
                }
                if (removed > 0)
                {
                    if (output.All(buffer => buffer.Count == 0)) throw new InvalidOperationException("Entire rail chunk would be removed: " + mesh.name);
                    changes.Add(mesh, output);
                    faces += removed;
                }
                previous = mesh.subMeshCount == 1 ? current : null;
                priorTrack = track;
                lastEnd = c.end;
            }
        }
        if (apply)
            foreach (var item in changes)
            {
                for (int sub = 0; sub < item.Value.Length; sub++)
                    item.Key.SetIndices(item.Value[sub], MeshTopology.Triangles, sub, true);
                EditorUtility.SetDirty(item.Key);
                AssetDatabase.SaveAssetIfDirty(item.Key);
            }
        return (faces, changes.Count);
    }

    static bool Less(Vector3Int a, Vector3Int b) =>
        a.x < b.x || a.x == b.x && (a.y < b.y || a.y == b.y && a.z < b.z);
}
