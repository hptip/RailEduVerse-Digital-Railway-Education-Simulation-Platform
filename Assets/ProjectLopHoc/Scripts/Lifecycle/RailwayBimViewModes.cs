using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

[Flags]
internal enum RailwayBimViewMode { None = 0, Exploded = 1, Section = 2, Isolation = 4, Xray = 16 }

internal sealed class RailwayBimViewModes
{
    readonly Dictionary<Renderer, bool> hidden = new();
    readonly Dictionary<Collider, bool> colliderStates = new();
    readonly Dictionary<MeshCollider, Mesh> colliderMeshes = new();
    readonly Dictionary<MeshFilter, Mesh> filterMeshes = new();
    readonly Dictionary<Transform, Vector3> positions = new();
    readonly Dictionary<Renderer, Material[]> originalMaterials = new();
    readonly Dictionary<Material, Material> xrayMaterials = new();
    readonly List<MeshCollider> temporaryColliders = new();
    readonly List<Mesh> temporaryMeshes = new();
    Shader xrayShader;

    public RailwayBimViewMode Active { get; private set; }
    public bool IsActive(RailwayBimViewMode mode) => (Active & mode) != 0;
    public Mesh CanonicalMesh(MeshFilter filter)
    {
        if (filter.TryGetComponent<MeshCollider>(out var collider) &&
            (collider.hideFlags & HideFlags.DontSave) == 0)
        {
            var original = colliderMeshes.TryGetValue(collider, out var before) ? before : collider.sharedMesh;
            if (original != null) return original;
        }
        return filterMeshes.TryGetValue(filter, out var source) ? source : filter.sharedMesh;
    }
    public void Toggle(RailwayBimViewMode mode) => Active ^= mode;

    public void Reset()
    {
        RestoreGeometry();
        Active = RailwayBimViewMode.None;
    }

    public void ReleaseZone() => RestoreGeometry();

    public void Apply(RailwayBimCamera camera, IReadOnlyList<Renderer> selected, IEnumerable<Renderer> visible)
    {
        if (selected.Count == 0) return;
        var renderBounds = selected[0].bounds;
        for (int i = 1; i < selected.Count; i++) renderBounds.Encapsulate(selected[i].bounds);
        var selection = selected.ToHashSet();
        if (IsActive(RailwayBimViewMode.Isolation))
            foreach (var renderer in visible)
                if (!selection.Contains(renderer)) Hide(renderer);
        if (IsActive(RailwayBimViewMode.Exploded))
        {
            float distance = Mathf.Clamp(renderBounds.size.magnitude * .035f, .12f, 5f);
            var assemblies = new Dictionary<Transform, (bool whole, Bounds bounds)>();
            foreach (var renderer in selected)
            {
                var target = renderer.transform;
                var center = renderer.bounds.center;
                var parent = target.parent;
                if (parent != null && selected.Count > 2)
                {
                    if (!assemblies.TryGetValue(parent, out var assembly))
                    {
                        var siblings = parent.GetComponentsInChildren<MeshRenderer>(false)
                            .Where(RailwayBimOutline.IsRenderable).ToArray();
                        assembly.whole = siblings.Length > 1 && siblings.Length < selected.Count &&
                            siblings.All(selection.Contains) && parent.GetComponent<Renderer>() == null;
                        if (assembly.whole)
                        {
                            assembly.bounds = siblings[0].bounds;
                            foreach (var sibling in siblings) assembly.bounds.Encapsulate(sibling.bounds);
                        }
                        assemblies.Add(parent, assembly);
                    }
                    if (assembly.whole) { target = parent; center = assembly.bounds.center; }
                }
                if (!positions.TryAdd(target, target.localPosition)) continue;
                var direction = (center - renderBounds.center).normalized;
                target.position += (direction.sqrMagnitude < .01f ? Vector3.up : direction) * distance;
            }
        }
        if (IsActive(RailwayBimViewMode.Section))
        {
            var plane = new Plane(camera.transform.forward, renderBounds.center);
            foreach (var renderer in selected) Section(renderer, plane);
            Physics.SyncTransforms();
        }
        if (IsActive(RailwayBimViewMode.Xray))
            foreach (var renderer in selected)
            {
                if (renderer.forceRenderingOff || originalMaterials.ContainsKey(renderer)) continue;
                var materials = renderer.sharedMaterials;
                originalMaterials.Add(renderer, materials);
                renderer.sharedMaterials = materials.Select(Xray).ToArray();
            }
    }

    void SaveCollider(Collider value)
    {
        if (value != null) colliderStates.TryAdd(value, value.enabled);
    }

    void Hide(Renderer renderer)
    {
        if (renderer == null) return;
        hidden.TryAdd(renderer, renderer.forceRenderingOff);
        renderer.forceRenderingOff = true;
        foreach (var collider in renderer.GetComponents<Collider>())
        {
            SaveCollider(collider);
            collider.enabled = false;
        }
    }

    void Section(Renderer renderer, Plane plane)
    {
        if (!renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) return;
        var box = renderer.bounds;
        var n = plane.normal;
        float radius = Vector3.Dot(box.extents, new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z)));
        float center = plane.GetDistanceToPoint(box.center);
        if (center >= radius) return;
        if (center <= -radius) { Hide(renderer); return; }
        if (!filter.sharedMesh.isReadable) return;
        var section = RailwayBimMeshSection.Cut(filter.sharedMesh, renderer.localToWorldMatrix, plane);
        if (section == null) { Hide(renderer); return; }
        temporaryMeshes.Add(section);
        filterMeshes.Add(filter, filter.sharedMesh);
        filter.sharedMesh = section;
        var colliders = renderer.GetComponents<Collider>();
        bool needsProxy = false;
        foreach (var collider in colliders)
        {
            if (!collider.enabled || collider.isTrigger) continue;
            SaveCollider(collider);
            if (collider is MeshCollider meshCollider && !meshCollider.convex)
            {
                colliderMeshes.Add(meshCollider, meshCollider.sharedMesh);
                meshCollider.sharedMesh = section;
            }
            else
            {
                collider.enabled = false;
                needsProxy = true;
            }
        }
        if (!needsProxy) return;
        var temporary = renderer.gameObject.AddComponent<MeshCollider>();
        temporary.hideFlags = HideFlags.DontSave;
        temporary.sharedMesh = section;
        temporaryColliders.Add(temporary);
    }

    Material Xray(Material source)
    {
        if (source == null) return null;
        if (xrayMaterials.TryGetValue(source, out var found)) return found;
        if (xrayShader == null) xrayShader = Resources.Load<Shader>("RailwayBimXray");
        if (xrayShader == null) return source;
        var variant = new Material(xrayShader);
        variant.name = source.name + " · X-ray BIM";
        variant.hideFlags = HideFlags.DontSave;
        var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
            source.HasProperty("_Color") ? source.GetColor("_Color") : new Color(.65f, .82f, 1);
        color.a = .24f;
        variant.SetColor("_BaseColor", color);
        xrayMaterials.Add(source, variant);
        return variant;
    }

    public void RestoreGeometry()
    {
        foreach (var item in temporaryColliders)
            if (item != null) { item.enabled = false; item.sharedMesh = null; Release(item); }
        foreach (var item in filterMeshes)
            if (item.Key != null) item.Key.sharedMesh = item.Value;
        foreach (var item in colliderMeshes)
            if (item.Key != null) item.Key.sharedMesh = item.Value;
        foreach (var item in colliderStates)
            if (item.Key != null) item.Key.enabled = item.Value;
        foreach (var item in positions)
            if (item.Key != null) item.Key.localPosition = item.Value;
        foreach (var item in hidden)
            if (item.Key != null) item.Key.forceRenderingOff = item.Value;
        foreach (var item in originalMaterials)
            if (item.Key != null) item.Key.sharedMaterials = item.Value;
        foreach (var mesh in temporaryMeshes) Release(mesh);
        foreach (var material in xrayMaterials.Values) Release(material);
        bool moved = positions.Count != 0 || colliderStates.Count != 0;
        temporaryColliders.Clear();
        temporaryMeshes.Clear();
        filterMeshes.Clear();
        colliderMeshes.Clear();
        colliderStates.Clear();
        positions.Clear();
        hidden.Clear();
        originalMaterials.Clear();
        xrayMaterials.Clear();
        if (moved) Physics.SyncTransforms();
    }

    static void Release(Object value)
    {
        if (Application.isPlaying) Object.Destroy(value);
        else Object.DestroyImmediate(value);
    }
}
