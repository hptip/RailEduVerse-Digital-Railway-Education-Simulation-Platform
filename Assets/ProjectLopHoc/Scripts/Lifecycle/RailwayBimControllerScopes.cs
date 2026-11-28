using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UttVr.BridgePilot;

public sealed partial class RailwayBimController
{
    internal RailwayBimSelectionScope GroupSelection { get; private set; }
    internal RailwayBimViewModes ViewModes { get; } = new();
    readonly RailwayBimInteriorVisibility interiors = new();
    const RailwayBimViewMode RevealsInterior = RailwayBimViewMode.Xray | RailwayBimViewMode.Exploded |
        RailwayBimViewMode.Section | RailwayBimViewMode.Isolation;
    internal bool HasViewSelection => GroupSelection != null || SelectedID != null || SelectedBridge != null;
    internal bool CanExplodeSelection => GroupSelection == null &&
        (SelectedBridge?.IsElement == true || SelectedBridge == null && Level == 2 && SelectedID != null);
    readonly Dictionary<(int mesh, Vector3 scale), RailwayBimQuantities.Result> measured = new();

    internal bool TrySelectedQuantities(out RailwayBimQuantities.Result result)
    {
        result = default;
        IEnumerable<Renderer> sources = SelectedBridge != null ? bridge.SelectedRenderers :
            SelectedID != null ? Visible(SelectedID)?.GetComponentsInChildren<Renderer>() : null;
        var filters = sources?.OfType<MeshRenderer>().Select(r => r.GetComponent<MeshFilter>())
            .Where(f => f != null).Distinct().ToArray();
        if (filters == null || filters.Length != 1) return false;
        var filter = filters[0];
        var mesh = ViewModes.CanonicalMesh(filter);
        if (mesh == null || !mesh.isReadable || mesh.vertexCount > 100_000) return false;
        var scale = filter.transform.lossyScale;
        var key = (mesh.GetInstanceID(), scale);
        if (!measured.TryGetValue(key, out result))
        {
            result = RailwayBimQuantities.Measure(mesh, scale);
            measured.Add(key, result);
        }
        return result.SurfaceArea > 0;
    }

    internal void SelectGroup(RailwayBimSelectionScope scope)
    {
        ReleaseViewEffects();
        RestoreIsolation();
        bridge?.Clear();
        if (ViewingDetailedZone && Level > 0) SetOverview();
        SelectedID = null;
        GroupSelection = scope;
        zoneViewer?.PrepareGroupView();
        zoneViewer?.OpenScope(scope);
        zoneViewer?.RefineGroupView(scope);
        RefreshScopeView();
        RailwayBimExplorer.Audit($"BimScope,{scope.Key},{scope.AssetIds.Count},{string.Join("|", scope.Zones)}");
        Changed?.Invoke();
    }

    internal void ToggleBimMode(RailwayBimViewMode mode)
    {
        if (!HasViewSelection || mode == RailwayBimViewMode.Exploded && !CanExplodeSelection) return;
        ViewModes.Toggle(mode);
        ApplyViewModes();
        RailwayBimExplorer.Audit($"BimMode,{mode},{ViewModes.IsActive(mode)},{GroupSelection?.Key ?? SelectedBridge?.assetId ?? SelectedID},{outline.HighlightedRenderers}");
        Changed?.Invoke();
    }

    internal void ReleaseViewEffects() { ViewModes.RestoreGeometry(); interiors.Restore(); }
    internal void ReleaseZoneEffects() { ViewModes.ReleaseZone(); interiors.Reset(); }
    internal void RefreshScopeView() => ApplyViewModes();

    void ApplyViewModes()
    {
        ReleaseViewEffects();
        if (!CanExplodeSelection && ViewModes.IsActive(RailwayBimViewMode.Exploded))
            ViewModes.Toggle(RailwayBimViewMode.Exploded);
        bool inspecting = ViewModes.IsActive(RevealsInterior);
        if (inspecting && zoneViewer?.productionLoader?.SetVisualPresentation(false) == true)
            RailwayBimExplorer.Audit($"BimDepthLOD,{GroupSelection?.Key},Detailed");
        var selected = ScopeRenderers();
        var candidates = CandidateRenderers().ToArray();
        if (!inspecting && (ActiveInfrastructure != null || SelectedBridge != null || GroupSelection != null || Level == 2))
        {
            var solids = candidates.Where(renderer => renderer.GetComponentInParent<BridgeBimIdentity>() != null ||
                Level == 2 && detailRoot != null && renderer.transform.IsChildOf(detailRoot)).ToArray();
            interiors.Cull(solids, GroupSelection == null ? selected : Array.Empty<Renderer>());
            interiors.UpdateCamera(cameraController.transform.position);
        }
        ViewModes.Apply(cameraController, selected, candidates);
        cameraController.SetClippingContext(candidates);
        outline.Show(selected);
    }

    void LateUpdate()
    {
        if (!UttWebPresentation.Active || cameraController == null) return;
        interiors.UpdateCamera(cameraController.transform.position);
        var loader = zoneViewer?.productionLoader;
        if (GroupSelection == null || loader == null || loader.LoadedCount == 0 || ViewModes.IsActive(RevealsInterior)) return;
        float distant = Mathf.Max(48, cameraController.FocusBounds.size.magnitude * 2f);
        float distance = cameraController.current.distance;
        if (loader.SetVisualPresentation(distance > distant || loader.ShowingVisual && distance > distant * .72f))
        {
            RailwayBimExplorer.Audit($"BimDepthLOD,{GroupSelection.Key},{(loader.ShowingVisual ? "Visual" : "Detailed")}");
            ApplyViewModes();
        }
    }

    IEnumerable<Renderer> CandidateRenderers()
    {
        IEnumerable<Renderer> From(Transform root) => root == null
            ? Array.Empty<Renderer>() : root.GetComponentsInChildren<MeshRenderer>(false);
        var rail = From(overviewRoot).Concat(From(blockRoot)).Concat(From(detailRoot));
        if (zoneViewer == null) return rail.Where(RailwayBimOutline.IsRenderable);
        var context = zoneViewer.VisibleZones.SelectMany(zone => From(zone.transform))
            .Concat(From(zoneViewer.productionLoader?.transform)).Concat(From(zoneViewer.detailedEnvironment));
        return rail.Concat(context).Distinct().Where(RailwayBimOutline.IsRenderable);
    }

    Renderer[] ScopeRenderers()
    {
        if (GroupSelection == null)
        {
            var current = SelectedBridge != null ? bridge.SelectedRenderers : SelectedID != null
                ? Visible(SelectedID)?.GetComponentsInChildren<Renderer>() : null;
            return current?.Where(RailwayBimOutline.IsRenderable).Distinct().ToArray() ?? Array.Empty<Renderer>();
        }
        var ids = GroupSelection.AssetIds;
        var batches = zoneViewer?.productionLoader?.occurrences
            .Where(part => ids.Contains(part.assetId) && part.visualBatch != "HIDDEN_IN_VISUAL")
            .Select(part => part.visualBatch).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>();
        var bridgeKeys = zoneViewer?.modularBridgeCatalog?.entries.Where(e => e.IsElement && ids.Contains(e.assetId))
            .Select(e => e.globalId).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>();
        var effectiveIds = new HashSet<string>(ids, StringComparer.Ordinal);
        foreach (string id in ids)
            if (Store.Assets.TryGetValue(id, out var asset))
            {
                if (!string.IsNullOrEmpty(asset.SegmentID)) effectiveIds.Add(asset.SegmentID);
                if (!string.IsNullOrEmpty(asset.BlockID)) effectiveIds.Add(asset.BlockID);
            }
        return CandidateRenderers().Where(renderer =>
        {
            if (batches.Contains(renderer.name) && renderer.transform.parent?.name == "Infrastructure_Production_v1")
                return true;
            var identity = renderer.GetComponentInParent<BridgeBimIdentity>();
            if (identity != null) return ids.Contains(identity.GlobalId) || bridgeKeys.Contains(identity.GlobalId);
            var rail = renderer.GetComponentInParent<BimAssetProxy>();
            return rail != null && !string.IsNullOrEmpty(rail.assetID) && effectiveIds.Contains(rail.assetID);
        }).ToArray();
    }

    bool TryScopeBounds(IReadOnlyList<Renderer> selected, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var renderer in selected)
        {
            if (!found) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            found = true;
        }
        if (GroupSelection != null && zoneViewer != null && (!found || GroupSelection.WholeRoute))
            foreach (var zone in zoneViewer.zones.Where(z => GroupSelection.Zones.Contains(z.id)))
            {
                if (!found) bounds = zone.focus;
                else bounds.Encapsulate(zone.focus);
                found = true;
            }
        return found;
    }
}
