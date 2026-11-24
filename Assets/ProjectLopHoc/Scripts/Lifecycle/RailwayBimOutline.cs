using System.Collections.Generic;
using UnityEngine;

/// <summary>Selection geometry for the lifecycle viewer, independent of BIM domain and source materials.</summary>
public sealed class RailwayBimOutline : MonoBehaviour
{
    public Material maskMaterial, outlineMaterial;

    internal static readonly HashSet<RailwayBimOutline> Active = new();
    internal readonly List<Renderer> Sources = new();
    Camera desktopCamera;
    RailwayBimVrPresentation vrPresentation;

    public int HighlightedRenderers
    {
        get
        {
            int count = 0;
            foreach (var source in Sources)
                if (IsRenderable(source)) count++;
            return count;
        }
    }

    internal void Configure(Camera desktop, RailwayBimVrPresentation vr)
    {
        desktopCamera = desktop;
        vrPresentation = vr;
    }

    internal bool RendersOn(Camera camera) =>
        camera != null && camera == (vrPresentation != null && vrPresentation.IsVr
            ? vrPresentation.Origin?.Camera : desktopCamera);

    public void Show(IEnumerable<Renderer> sources)
    {
        Clear();
        if (sources == null) return;
        foreach (var source in sources)
            if (SubmeshCount(source) > 0 && !Sources.Contains(source)) Sources.Add(source);
    }

    public void Clear() => Sources.Clear();

    internal static bool IsRenderable(Renderer source) =>
        source != null && source.enabled && source.gameObject.activeInHierarchy && !source.forceRenderingOff;

    internal bool HasVisibleSource(int layers)
    {
        foreach (var source in Sources)
            if (IsRenderable(source) && (layers & (1 << source.gameObject.layer)) != 0) return true;
        return false;
    }

    internal static int SubmeshCount(Renderer source)
    {
        Mesh mesh = source switch
        {
            SkinnedMeshRenderer skinned => skinned.sharedMesh,
            MeshRenderer renderer when renderer.TryGetComponent<MeshFilter>(out var filter) => filter.sharedMesh,
            _ => null
        };
        return mesh != null ? mesh.subMeshCount : 0;
    }

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);
}
