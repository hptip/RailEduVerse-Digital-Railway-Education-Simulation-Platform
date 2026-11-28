using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

internal static class RailwayTrackOnlyVisuals
{
    const string RailPrefab = "Assets/ProjectLopHoc/Prefabs/EBS_Rail_2000_Prefab.prefab";
    const string DeckPrefab = "Assets/ProjectLopHoc/Prefabs/Deck Girder.prefab";
    const string Route = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string MeshFolder = "Assets/ProjectLopHoc/Generated/QuestRailway/Resources/";

    static readonly Color32 DeckColor = new(255, 255, 255, 255);
    static readonly Color32 SlabColor = new(60, 60, 60, 255);

    internal static HashSet<Material> DeckMaterials()
    {
        var rail = AssetDatabase.LoadAssetAtPath<GameObject>(RailPrefab);
        var deck = AssetDatabase.LoadAssetAtPath<GameObject>(DeckPrefab);
        if (rail == null || deck == null) throw new InvalidOperationException("Missing EBS track or Deck Girder source.");
        var slab = rail.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "Mesh_12" &&
            f.sharedMesh != null && f.sharedMesh.name == "Mesh_52");
        var slabMaterial = slab.GetComponent<MeshRenderer>().sharedMaterial;
        var deckMaterials = deck.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).ToHashSet();
        var allRenderers = rail.GetComponentsInChildren<MeshRenderer>(true);
        bool FromDeck(MeshRenderer renderer)
        {
            var instance = PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
            return instance != null && AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(instance)) == DeckPrefab;
        }
        var nestedMaterials = allRenderers.Where(FromDeck).SelectMany(renderer => renderer.sharedMaterials).ToHashSet();
        var retainedMaterials = allRenderers.Where(renderer => !FromDeck(renderer)).SelectMany(renderer => renderer.sharedMaterials);
        if (deckMaterials.Count == 0 || deckMaterials.Contains(null) || deckMaterials.Contains(slabMaterial) ||
            !nestedMaterials.SetEquals(deckMaterials) || !BakeColor(slabMaterial).Equals(SlabColor) ||
            deckMaterials.Any(mat => !BakeColor(mat).Equals(DeckColor)) ||
            retainedMaterials.Any(mat => deckMaterials.Contains(mat) || BakeColor(mat).Equals(DeckColor)))
            throw new InvalidOperationException("Deck Girder palette changed. Verify source ownership before stripping a baked chunk.");
        return deckMaterials;
    }

    static Color32 BakeColor(Material material)
    {
        Color color = Color.gray;
        if (material != null)
        {
            if (material.HasProperty("_BaseColor")) color = material.GetColor("_BaseColor");
            else if (material.HasProperty("_Color")) color = material.GetColor("_Color");
        }
        color.a = 1;
        return color;
    }

    [MenuItem("Tools/UTTVR/BIM Viewer/Preview Track Without Deck Girder")]
    public static void Validate() => Run(false);

    [MenuItem("Tools/UTTVR/BIM Viewer/Apply Track Without Deck Girder")]
    public static void Apply() => Run(true);

    static void Run(bool apply)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Use Edit Mode to prepare rail visuals.");
        DeckMaterials();
        var scene = EditorSceneManager.OpenPreviewScene(Route);
        try
        {
            var data = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RailwayMicroChunkStreamer>(true)).Single().data;
            var meshes = data.chunks.SelectMany(chunk => new[] { chunk.right, chunk.left }).Select(track =>
            {
                if (track.materials.Length != 1 || track.materials[0]?.name != "RailwayFlatColour")
                    throw new InvalidOperationException("Unexpected baked material for " + track.meshResource);
                return AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + track.meshResource + ".asset") ??
                    throw new FileNotFoundException(track.meshResource);
            }).Distinct().ToArray();

            // Validate the entire registry before saving the first asset. The same
            // mesh IDs are referenced by the runtime pool and the BIM zone prefabs.
            var rows = new List<string> { "mesh,retained_triangles,deck_triangles_removed,slab_triangles_retained" };
            var prepared = new List<(Mesh mesh, int[] triangles, Bounds bounds)>();
            foreach (var mesh in meshes)
            {
                var (triangles, bounds, deck, slab) = WithoutDeck(mesh);
                rows.Add($"{mesh.name},{triangles.Length / 3},{deck},{slab}");
                if (apply && deck > 0) prepared.Add((mesh, triangles, bounds));
            }
            if (apply)
                foreach (var item in prepared)
                {
                    item.mesh.SetTriangles(item.triangles, 0, false);
                    item.mesh.bounds = item.bounds;
                    EditorUtility.SetDirty(item.mesh);
                    AssetDatabase.SaveAssetIfDirty(item.mesh);
                }
            File.WriteAllLines(apply ? "Temp/RailwayTrackOnlyApplied.csv" : "Temp/RailwayTrackOnlyPreview.csv", rows);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static (int[] indices, Bounds bounds, int deck, int slab) WithoutDeck(Mesh mesh)
    {
        if (!mesh.isReadable || mesh.subMeshCount != 1 || mesh.GetTopology(0) != MeshTopology.Triangles)
            throw new InvalidOperationException("Unexpected rail mesh topology in " + mesh.name);
        var vertices = mesh.vertices;
        var colors = mesh.colors32;
        if (colors.Length != vertices.Length) throw new InvalidOperationException("Missing baked source colours in " + mesh.name);
        var input = mesh.GetTriangles(0);
        var output = new List<int>(input.Length);
        int deck = 0, slab = 0;
        var bounds = new Bounds();
        for (int i = 0; i < input.Length; i += 3)
        {
            int a = input[i], b = input[i + 1], c = input[i + 2];
            var color = colors[a];
            if (DeckColor.Equals(color) || DeckColor.Equals(colors[b]) || DeckColor.Equals(colors[c]))
            {
                if (!color.Equals(colors[b]) || !color.Equals(colors[c]) ||
                    Mathf.Max(vertices[a].y, vertices[b].y, vertices[c].y) > .03f)
                    throw new InvalidOperationException("Ambiguous Deck Girder face in " + mesh.name);
                deck++;
                continue;
            }
            if (SlabColor.Equals(color) && color.Equals(colors[b]) && color.Equals(colors[c])) slab++;
            if (output.Count == 0) bounds = new Bounds(vertices[a], Vector3.zero);
            bounds.Encapsulate(vertices[a]);
            bounds.Encapsulate(vertices[b]);
            bounds.Encapsulate(vertices[c]);
            output.Add(a); output.Add(b); output.Add(c);
        }
        if (output.Count == 0 || slab == 0) throw new InvalidOperationException("Missing retained rail or Mesh_12 slab in " + mesh.name);
        return (output.ToArray(), bounds, deck, slab);
    }
}
