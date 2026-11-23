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
    public RailwayBimZone[] zones;
    public RailwayBimFacility[] facilities;
    public BridgeBimCatalog modularBridgeCatalog;
    string pendingBridgeAsset;

    public bool OpenBridgeAsset(string assetId)
    {
        if (modularBridgeCatalog == null || !modularBridgeCatalog.entries.Any(e => e.assetId == assetId)) return false;
        Open("Zone_018", false);
        pendingBridgeAsset = assetId;
        return true;
    }
    public Transform detailedEnvironment;
    public RailwayBimController controller;
    public string ActiveZone { get; private set; } = DetailedZone;

    readonly Dictionary<string, GameObject> loaded = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    string requested = DetailedZone;
    Coroutine transition;
    bool releaseAssets;
    float originalFar, originalFogStart, originalFogEnd;
    bool savedViewSettings;

    void UseOverviewDistance(bool overview)
    {
        var camera = controller.cameraController.GetComponent<Camera>();
        if (overview && !savedViewSettings)
        {
            savedViewSettings = true;
            originalFar = camera.farClipPlane;
            originalFogStart = RenderSettings.fogStartDistance;
            originalFogEnd = RenderSettings.fogEndDistance;
            camera.farClipPlane = Mathf.Max(originalFar, 3200);
            RenderSettings.fogStartDistance = Mathf.Max(originalFogStart, 2000);
            RenderSettings.fogEndDistance = Mathf.Max(originalFogEnd, 3200);
        }
        else if (!overview && savedViewSettings)
        {
            camera.farClipPlane = originalFar;
            RenderSettings.fogStartDistance = originalFogStart;
            RenderSettings.fogEndDistance = originalFogEnd;
            savedViewSettings = false;
        }
    }

    public void Open(string zoneId, bool focus = true)
    {
        if (!zones.Any(x => x.id == zoneId)) return;
        pendingBridgeAsset = null;
        requested = zoneId;
        if (zoneId == DetailedZone)
        {
            Commit(zoneId, Array.Empty<string>(), focus);
            return;
        }

        if (transition != null) return;
        if (ActiveZone == zoneId)
        {
            if (focus) controller.FocusZone(zones.First(z => z.id == zoneId).focus);
            return;
        }
        transition = StartCoroutine(Load());
    }

    string[] Dependencies(string id) => facilities.Where(x => x.covered.Contains(id))
        .SelectMany(x => x.covered).Append(id).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();

    IEnumerator Load()
    {
        // Always yield before any commit, so a synchronous completion cannot leave a stale Coroutine handle.
        yield return null;
        while (requested != ActiveZone)
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
                if (failed && requested == target) requested = ActiveZone;
                if (incoming.Count > 0) releaseAssets = true;
                continue;
            }

            foreach (var entry in incoming)
            {
                entry.Value.SetActive(false);
                entry.Value.transform.SetParent(transform, false);
                loaded.Add(entry.Key, entry.Value);
            }
            Commit(target, wanted, true);
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
        if (ActiveZone == id)
        {
            if (focus && id == DetailedZone) controller.FocusDetailedZone();
            return;
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
        controller.bridge = loaded.TryGetValue("Zone_018", out var bridgeZone)
            ? bridgeZone.GetComponentInChildren<BridgeBimAdapter>() : null;
        UseOverviewDistance(id != DetailedZone);
        controller.SetZonePresentation(id == DetailedZone);
        if (id == DetailedZone)
        {
            if (focus) controller.FocusDetailedZone();
            if (transition == null && releaseAssets)
            {
                releaseAssets = false;
                StartCoroutine(ReleaseAfterFrame());
            }
        }
        else if (focus)
        {
            var bounds = zones.First(x => x.id == id).focus;
            foreach (var facility in facilities.Where(x => x.covered.Contains(id)))
                foreach (string zoneId in facility.covered)
                    bounds.Encapsulate(zones.First(x => x.id == zoneId).focus);
            controller.FocusZone(bounds);
        }
        int renders = loaded.Values.Sum(x => x.GetComponentsInChildren<Renderer>().Count(r => r.enabled));
        controller.ReportZone(id + " · " + (id == DetailedZone ? "hồ sơ ray hiện có" : "hình học tuyến"));
        RailwayBimExplorer.Audit($"ZoneReady,{id},{loaded.Count},{renders},{string.Join("|", wanted)}");
        if (id == "Zone_018" && pendingBridgeAsset != null)
        {
            string asset = pendingBridgeAsset;
            pendingBridgeAsset = null;
            controller.SelectBridge(asset, true);
        }
    }

    IEnumerator ReleaseAfterFrame()
    {
        yield return null;
        if (transition == null) Resources.UnloadUnusedAssets();
    }
}
