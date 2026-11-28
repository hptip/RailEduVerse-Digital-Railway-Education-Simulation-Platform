using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UttVr.BridgePilot;

[Serializable]
public sealed class RailwayBimZone
{
    public string id, source;
    public float start, end;
    public Bounds focus;
    public string[] domains;
    public string Label(int index) => $"Đoạn tuyến {index + 1:00} ({id})";
}

[Serializable]
public sealed class RailwayBimFacility
{
    public string key, label, domain, owner, assetId;
    public string[] covered, bindings;
    public float start, end;
}

public sealed class RailwayBimZoneViewer : MonoBehaviour
{
    public const string DetailedZone = "Zone_021";
    public const string BridgeZone = "Zone_018";
    public RailwayBimZone[] zones;
    public RailwayBimFacility[] facilities;
    public BridgeBimCatalog modularBridgeCatalog;
    public InfrastructureDetailedLoader productionLoader;
    string pendingBridgeAsset, pendingBridgeGroup, activeBridgeGroup;
    InfrastructureDetailedLoader.Occurrence pendingInfrastructure;
    bool focusInfrastructure;
    string[] requestedScope;
    bool requestedFocus;

    internal IEnumerable<GameObject> VisibleZones => loaded.Values;

    internal void PrepareGroupView()
    {
        if (productionLoader == null || productionLoader.LoadedCount == 0) return;
        productionLoader.Unload();
        releaseAssets = true;
        controller.bridge = loaded.TryGetValue(BridgeZone, out var bridgeZone)
            ? bridgeZone.GetComponentInChildren<BridgeBimAdapter>() : null;
    }

    internal void RefineGroupView(RailwayBimSelectionScope scope)
    {
        if (scope == null || productionLoader == null || scope.AssetIds.Count == 0 ||
            scope.AssetIds.Count > 450 || !scope.AssetIds.All(id => id.StartsWith("G2-", StringComparison.Ordinal))) return;
        var parts = productionLoader.occurrences.Where(part => scope.AssetIds.Contains(part.assetId)).ToArray();
        if (parts.Length != scope.AssetIds.Count) return;
        var chunks = parts.Select(part => (part.domain, part.chunk)).ToHashSet();
        if (chunks.Count > 3 || parts.Any(part => !loaded.ContainsKey(part.zone)) ||
            productionLoader.occurrences.Count(part => chunks.Contains((part.domain, part.chunk))) > 450) return;
        if (productionLoader.LoadedCount == 0 || !productionLoader.HasChunks(chunks))
            productionLoader.LoadChunks(chunks, loaded.Values.Select(zone => zone.transform.Find("Infrastructure_Production_v1")));
        controller.bridge = productionLoader.ActiveAdapter;
    }

    internal void OpenScope(RailwayBimSelectionScope scope)
    {
        // The scope keeps all IDs. The route-level view reuses whichever zone is
        // already visible rather than loading the entire 17-scene environment.
        if (scope.WholeRoute || scope.Zones.Length == 0) return;
        var desired = scope.Zones.Where(zone => zones.Any(z => z.id == zone)).ToArray();
        if (desired.Length == 0) return;
        string anchor = desired.Contains(ActiveZone) ? ActiveZone : desired[0];
        OpenInternal(anchor, false, desired);
    }

    public bool OpenInfrastructureAsset(string assetId,bool focus)
    {
        if(productionLoader==null||string.IsNullOrEmpty(assetId)||!assetId.StartsWith("G2-",StringComparison.Ordinal))return false;
        var part=productionLoader.occurrences.FirstOrDefault(item=>item.assetId==assetId);
        if(part==null)return false;
        if(ActiveZone==part.zone&&requested==part.zone)
        {
            BindInfrastructure(part,focus);
            return true;
        }
        Open(part.zone,false);
        pendingInfrastructure=part;
        focusInfrastructure=focus;
        return true;
    }

    void BindInfrastructure(InfrastructureDetailedLoader.Occurrence part,bool focus)
    {
        var current=productionLoader;
        current.SetVisualPresentation(false);
        if(current.LoadedCount==0||!current.HasChunks(new[] { (part.domain, part.chunk) }))
        {
            controller.ReleaseViewEffects();
            current.Load(part.domain, part.chunk, loaded.Values.Select(zone =>
                zone.transform.Find("Infrastructure_Production_v1")));
            current.previewDomain=part.domain;
            current.previewChunk=part.chunk;
        }
        controller.bridge=current.ActiveAdapter;
        controller.SelectBridge(part.assetId,focus);
    }

    public bool OpenBridgeAsset(string assetId)
    {
        if (modularBridgeCatalog == null || !modularBridgeCatalog.entries.Any(e => e.assetId == assetId)) return false;
        Open(BridgeZone, false);
        if (ActiveZone == BridgeZone && loaded.TryGetValue(BridgeZone, out var zone))
        {
            controller.ReleaseViewEffects();
            productionLoader?.Unload();
            controller.bridge = zone.GetComponentInChildren<BridgeBimAdapter>();
            return controller.SelectBridge(assetId, true);
        }
        pendingBridgeAsset = assetId;
        return true;
    }

    public void OpenBridgeGroup(string group)
    {
        if (modularBridgeCatalog == null || (group.Length != 0 &&
            !modularBridgeCatalog.entries.Any(entry => !entry.IsElement && entry.globalId == group))) return;
        Open(BridgeZone, false);
        if (ActiveZone == BridgeZone) FocusBridgeGroup(group);
        else pendingBridgeGroup = group;
    }

    void FocusBridgeGroup(string group)
    {
        activeBridgeGroup = group;
        controller.FocusBridge(group.Length == 0 ? "Bridge_Main" : group, group.Length == 0 ? null : group,
            group.Length == 0 ? RailwayBimCamera.BridgeFrame.Structure : RailwayBimCamera.BridgeFrame.Group);
    }

    public void Refocus()
    {
        if (ActiveZone == BridgeZone && activeBridgeGroup != null) FocusBridgeGroup(activeBridgeGroup);
        else Open(ActiveZone);
    }
    public Transform detailedEnvironment;
    public RailwayBimController controller;
    public string ActiveZone { get; private set; } = DetailedZone;

    readonly Dictionary<string, GameObject> loaded = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    string requested = DetailedZone;
    Coroutine transition;
    bool releaseAssets;
    float originalFogStart, originalFogEnd;
    bool savedViewSettings;

    void UseOverviewDistance(bool overview)
    {
        if (overview && !savedViewSettings)
        {
            savedViewSettings = true;
            originalFogStart = RenderSettings.fogStartDistance;
            originalFogEnd = RenderSettings.fogEndDistance;
            RenderSettings.fogStartDistance = Mathf.Max(originalFogStart, 2000);
            RenderSettings.fogEndDistance = Mathf.Max(originalFogEnd, 3200);
        }
        else if (!overview && savedViewSettings)
        {
            RenderSettings.fogStartDistance = originalFogStart;
            RenderSettings.fogEndDistance = originalFogEnd;
            savedViewSettings = false;
        }
    }

    public void Open(string zoneId, bool focus = true)
    {
        OpenInternal(zoneId, focus, null);
    }

    void OpenInternal(string zoneId, bool focus, string[] scope)
    {
        if (!zones.Any(x => x.id == zoneId)) return;
        pendingBridgeAsset = null;
        pendingBridgeGroup = null;
        activeBridgeGroup = null;
        pendingInfrastructure=null;
        requested = zoneId;
        requestedFocus = focus;
        requestedScope = scope;
        if (zoneId == DetailedZone)
        {
            Commit(zoneId, Array.Empty<string>(), focus);
            return;
        }

        if (transition != null) return;
        if (ActiveZone == zoneId && !NeedsScopeLoad())
        {
            if (focus) FocusZone(zoneId);
            return;
        }
        transition = StartCoroutine(Load());
    }

    bool NeedsScopeLoad() => requestedScope != null && requestedScope.Any(zone => !loaded.ContainsKey(zone));

    string[] Dependencies(string id) => facilities.Where(x => x.covered.Contains(id))
        .SelectMany(x => x.covered).Concat(requestedScope ?? Array.Empty<string>())
        .Append(id).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();

    IEnumerator Load()
    {
        // Always yield before any commit, so a synchronous completion cannot leave a stale Coroutine handle.
        yield return null;
        while (requested != ActiveZone || NeedsScopeLoad())
        {
            string target = requested;
            string[] wanted = Dependencies(target);
            var incoming = new Dictionary<string, GameObject>();
            bool failed = false;
            foreach (string id in wanted)
            {
                if (loaded.ContainsKey(id)) continue;
                var request = Resources.LoadAsync<GameObject>("RouteBimZones/" + id);
                yield return request;
                if (requested != target) break;
                if (!(request.asset is GameObject prefab))
                {
                    failed = true;
                    controller.ReportZone("Thiếu hình học của " + id);
                    break;
                }
                // An inactive parent prevents a partially loaded zone from becoming visible.
                if (staging == null)
                {
                    staging = new GameObject("Zone staging").transform;
                    staging.SetParent(transform, false);
                    staging.gameObject.SetActive(false);
                }
                incoming.Add(id, Instantiate(prefab, staging));
            }

            if (failed || requested != target)
            {
                foreach (var instance in incoming.Values) Destroy(instance);
                if (failed && requested == target) { requested = ActiveZone; requestedScope = null; }
                if (incoming.Count > 0) releaseAssets = true;
                continue;
            }

            foreach (var entry in incoming)
            {
                entry.Value.SetActive(false);
                entry.Value.transform.SetParent(transform, false);
                loaded.Add(entry.Key, entry.Value);
            }
            Commit(target, wanted, requestedFocus);
        }
        transition = null;
        if (releaseAssets)
        {
            releaseAssets = false;
            yield return null;
            if (transition == null) Resources.UnloadUnusedAssets();
        }
    }

    Transform staging;

    void Commit(string id, IReadOnlyCollection<string> wanted, bool focus)
    {
        if (ActiveZone == id && id == DetailedZone)
        {
            if (focus && id == DetailedZone) controller.FocusDetailedZone();
            return;
        }
        controller.ReleaseZoneEffects();
        if(productionLoader!=null&&productionLoader.LoadedCount>0)
        {
            productionLoader.Unload();
            releaseAssets=true;
        }
        foreach (string old in loaded.Keys.Where(key => !wanted.Contains(key)).ToArray())
        {
            loaded[old].SetActive(false);
            Destroy(loaded[old]);
            loaded.Remove(old);
            releaseAssets = true;
        }
        foreach (var instance in loaded.Values) instance.SetActive(true);
        ActiveZone = id;
        controller.bridge = loaded.TryGetValue(BridgeZone, out var bridgeZone)
            ? bridgeZone.GetComponentInChildren<BridgeBimAdapter>() : null;
        UseOverviewDistance(id != DetailedZone);
        controller.SetZonePresentation(id == DetailedZone);
        bool focusedInfrastructure=pendingInfrastructure!=null&&pendingInfrastructure.zone==id;
        if(focusedInfrastructure)
        {
            var part=pendingInfrastructure;
            pendingInfrastructure=null;
            BindInfrastructure(part,focusInfrastructure);
        }
        if (id == DetailedZone)
        {
            if (focus) controller.FocusDetailedZone();
            if (transition == null && releaseAssets)
            {
                releaseAssets = false;
                StartCoroutine(ReleaseAfterFrame());
            }
        }
        else if (pendingBridgeGroup != null && id == BridgeZone)
            FocusBridgeGroup(pendingBridgeGroup);
        else if (focus && pendingBridgeAsset == null && !focusedInfrastructure) FocusZone(id);
        int renders = loaded.Values.Sum(x => x.GetComponentsInChildren<Renderer>().Count(r => r.enabled));
        controller.ReportZone(id + " · " + (id == DetailedZone ? "hồ sơ ray hiện có" : "hình học tuyến"));
        RailwayBimExplorer.Audit($"ZoneReady,{id},{loaded.Count},{renders},{string.Join("|", wanted)}");
        pendingBridgeGroup = null;
        if (id == BridgeZone && pendingBridgeAsset != null)
        {
            string asset = pendingBridgeAsset;
            pendingBridgeAsset = null;
            controller.SelectBridge(asset, true);
        }
        RefineGroupView(controller.GroupSelection);
        controller.RefreshScopeView();
    }

    void FocusZone(string id)
    {
        if (id == BridgeZone && controller.FocusBridge(id, null, RailwayBimCamera.BridgeFrame.Zone)) return;
        var bounds = zones.First(x => x.id == id).focus;
        foreach (var facility in facilities.Where(x => x.covered.Contains(id)))
            foreach (string zoneId in facility.covered)
                bounds.Encapsulate(zones.First(x => x.id == zoneId).focus);
        controller.FocusZone(bounds);
    }

    IEnumerator ReleaseAfterFrame()
    {
        yield return null;
        if (transition == null) Resources.UnloadUnusedAssets();
    }
}
