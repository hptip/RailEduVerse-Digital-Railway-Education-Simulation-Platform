using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UttVr.BridgePilot;


// Each row is rebound as it enters the ScrollRect. The model pool is managed by the controller.
sealed class RailwayBimTree
{
    const float RowHeight = 39;
    const int SearchLimit = 150;

    sealed class Node
    {
        public BimAsset Asset;
        public string Key;
        public string Label;
        public string SearchText;
        public string ZoneToOpen;
        public string BridgeGroup;
        public Action LoadChildren;
        public Node Parent;
        public int Depth;
        public readonly List<Node> Children = new List<Node>();
        public bool IsElement => Asset?.AssetType == "Component";
    }

    sealed class Row
    {
        public Button Button;
        public TMP_Text Label;
        public TMP_Text Icon;
        public Image Folder;
        public Image ElementMark;
        public RectTransform Guide;
        public Image[] Lines;
        public RailwayBimTooltip Tooltip;
        public Node Node;
    }

    readonly Dictionary<string, Node> assets = new Dictionary<string, Node>(StringComparer.Ordinal);
    readonly List<Node> orderedAssets = new List<Node>();
    readonly List<Node> searchableGroups = new List<Node>();
    readonly List<Node> roots = new List<Node>();
    readonly HashSet<string> expanded = new HashSet<string>(StringComparer.Ordinal);
    readonly List<Node> visible = new List<Node>();
    readonly List<Row> rows = new List<Row>();
    readonly Dictionary<string, BridgeBimEntry> bridgeByKey = new Dictionary<string, BridgeBimEntry>(StringComparer.Ordinal);
    readonly Dictionary<string, BridgeBimEntry> bridgeByAsset = new Dictionary<string, BridgeBimEntry>(StringComparer.Ordinal);
    readonly Dictionary<string, Node> bridgeNodes = new Dictionary<string, Node>(StringComparer.Ordinal);
    ILookup<string, BridgeBimEntry> bridgeChildren;
    Node bridgeRoot;
    readonly ScrollRect scroll;
    readonly Button template;
    readonly TMP_Text count;
    readonly TMP_Text hint;
    readonly Action<string, bool> select;
    string query = "", selected, lastClick;
    float clickTime = float.NegativeInfinity;
    int renderedStart = -1, renderedCount = -1;

    public RailwayBimTree(ScrollRect scroll, Button template, TMP_Text count, TMP_Text hint, Action<string, bool> select)
    {
        this.scroll = scroll;
        this.template = template;
        this.count = count;
        this.hint = hint;
        this.select = select;
        scroll.onValueChanged.AddListener(_ => Render());
    }

    public void Load(RailwayBimStore store, RailwayBimZoneViewer viewer)
    {
        if (roots.Count != 0) return;
        var records = store.Assets.Values.OrderBy(a => a.AssetID, StringComparer.Ordinal).ToArray();
        foreach (var record in records)
        {
            var node = new Node { Asset = record, Key = "asset:" + record.AssetID };
            assets.Add(record.AssetID, node);
            orderedAssets.Add(node);
        }
        var route = new Node { Key = "route:YTH", Label = "Tuyến Yên Tử – Hạ Long Xanh" };
        roots.Add(route);
        expanded.Add(route.Key);
        var zoneRecords = viewer != null ? viewer.zones : new[] { new RailwayBimZone
            { id = RailwayBimZoneViewer.DetailedZone, domains = new[] { "Đường ray" } } };
        for (int i = 0; i < zoneRecords.Length; i++)
        {
            var zone = zoneRecords[i];
            var root = new Node { Key = "zone:" + zone.id, Label = zone.Label(i), ZoneToOpen = zone.id };
            root.SearchText = root.Label;
            searchableGroups.Add(root);
            Attach(route, root);
            foreach (string domain in zone.domains)
            {
                var group = new Node { Key = root.Key + ":" + domain, Label = domain };
                Attach(root, group);
                if (domain == "Đường ray" && zone.id == RailwayBimZoneViewer.DetailedZone)
                {
                    foreach (var track in records.Where(a => a.AssetType == "Segment" && a.ZoneID == "Z021")
                        .GroupBy(a => a.TrackID).OrderByDescending(g => g.Key, StringComparer.Ordinal))
                    {
                        var branch = new Node { Key = "track:Z021:" + track.Key,
                            Label = RailwayBimVietnamese.Text(track.Key) + " (" + track.Key + ")" };
                        Attach(group, branch);
                        foreach (var record in track) Attach(branch, assets[record.AssetID]);
                    }
                    expanded.Add(root.Key);
                    expanded.Add(group.Key);
                    continue;
                }
                var works = viewer != null ? viewer.facilities.Where(f => f.domain == domain && f.covered.Contains(zone.id)).ToArray()
                    : Array.Empty<RailwayBimFacility>();
                foreach (var work in works)
                {
                    var catalog = work.key == "Bridge_Main" && zone.id == work.owner ? viewer.modularBridgeCatalog : null;
                    string label = work.label + (catalog != null ? "" :
                        work.owner == zone.id ? " · Chưa có hồ sơ" : " · Xem tại " + work.owner);
                    var node = new Node { Key = root.Key + ":" + work.key, Label = label,
                        SearchText = label + " " + work.key, ZoneToOpen = work.owner,
                        BridgeGroup = catalog != null ? "" : null };
                    Attach(group, node);
                    if (work.owner == zone.id) searchableGroups.Add(node);
                    if (catalog != null)
                    {
                        bridgeRoot = node;
                        bridgeChildren = catalog.entries.ToLookup(e => e.parentGlobalId ?? "", StringComparer.Ordinal);
                        foreach (var entry in catalog.entries)
                        {
                            bridgeByKey.Add(entry.globalId, entry);
                            if (entry.IsElement) bridgeByAsset.Add(entry.assetId, entry);
                        }
                        node.LoadChildren = () => LoadBridgeChildren("");
                    }
                }
                if (works.Length == 0) Attach(group, new Node { Key = group.Key + ":preview", Label = "Chưa có hồ sơ", ZoneToOpen = zone.id });
            }
        }
        foreach (var record in records.Where(a => a.AssetType != "Segment"))
        {
            if (record.ParentAssetID != null && assets.TryGetValue(record.ParentAssetID, out var parent))
                Attach(parent, assets[record.AssetID]);
        }
        foreach (var root in roots) SetDepth(root, 0);
        RefreshNames(store);
    }

    void LoadBridgeChildren(string key)
    {
        foreach (var entry in bridgeChildren[key]) GetBridgeNode(entry);
    }

    Node GetBridgeNode(BridgeBimEntry entry)
    {
        if (bridgeNodes.TryGetValue(entry.globalId, out var existing)) return existing;
        var parent = !string.IsNullOrEmpty(entry.parentGlobalId) && bridgeByKey.TryGetValue(entry.parentGlobalId, out var parentEntry)
            ? GetBridgeNode(parentEntry) : bridgeRoot;
        var node = new Node
        {
            Key = "bridge:" + entry.globalId, Label = entry.Label, SearchText = BridgeSearch(entry),
            BridgeGroup = entry.IsElement ? null : entry.globalId
        };
        if (entry.IsElement)
        {
            node.Asset = new BimAsset { AssetID = entry.assetId, AssetType = "Component",
                DisplayName = entry.Label, ComponentType = entry.ifcClass, ZoneID = "Z018" };
            assets.Add(entry.assetId, node);
        }
        else node.LoadChildren = () => LoadBridgeChildren(entry.globalId);
        bridgeNodes.Add(entry.globalId, node);
        Attach(parent, node);
        return node;
    }

    static string BridgeSearch(BridgeBimEntry entry) =>
        string.Join(" ", entry.Label, entry.assetId, entry.ifcClass, entry.sourceGlobalId, entry.sourceIfcClass);

    bool TryGetAsset(string id, out Node node)
    {
        if (assets.TryGetValue(id, out node)) return true;
        if (!bridgeByAsset.TryGetValue(id, out var entry)) return false;
        node = GetBridgeNode(entry);
        return true;
    }

    static void Attach(Node parent, Node child)
    {
        child.Parent = parent;
        child.Depth = parent.Depth + 1;
        parent.Children.Add(child);
    }

    static void SetDepth(Node parent, int depth)
    {
        parent.Depth = depth;
        foreach (var child in parent.Children) SetDepth(child, depth + 1);
    }

    public void RefreshNames(RailwayBimStore store, string id = null)
    {
        if (id != null)
        {
            if (assets.TryGetValue(id, out var node) && store.Assets.TryGetValue(id, out var record))
                UpdateName(node, record);
        }
        else foreach (var node in orderedAssets)
            if (store.Assets.TryGetValue(node.Asset.AssetID, out var record)) UpdateName(node, record);
        Rebuild();
    }

    static void UpdateName(Node node, BimAsset asset)
    {
        node.Asset = asset;
        node.Label = RailwayBimVietnamese.Name(asset);
        if (asset.ComponentType == "Rail" && (asset.DisplayName == "Ray_Right" || asset.DisplayName == "Ray_Left"))
            node.Label = (asset.DisplayName == "Ray_Right" ? "Thanh ray phải" : "Thanh ray trái") + " · " + Suffix(asset.AssetID);
        node.SearchText = string.Join("\n", asset.AssetID, asset.DisplayName, asset.ComponentType, node.Label,
            RailwayBimVietnamese.Text(asset.ComponentType));
    }

    static string Suffix(string id) => id.Substring(id.LastIndexOf('-') + 1);

    public string Location(string id, bool includeElement = true)
    {
        if (id == null || !TryGetAsset(id, out var node)) return "";
        var path = new Stack<string>();
        for (var cursor = includeElement ? node : node.Parent; cursor != null; cursor = cursor.Parent)
            path.Push(cursor.Label);
        return string.Join("  /  ", path);
    }

    public string Label(string id) => id != null && TryGetAsset(id, out var node) ? node.Label : "";

    public void Search(string value)
    {
        string next = value.Trim();
        if (query == next) return;
        query = next;
        scroll.StopMovement();
        scroll.content.anchoredPosition = Vector2.zero;
        Rebuild();
    }

    public bool SetSelection(string id)
    {
        bool changed = selected != id;
        selected = id;
        if (id != null && TryGetAsset(id, out var node))
            for (var cursor = node.Parent; cursor != null; cursor = cursor.Parent) changed |= expanded.Add(cursor.Key);
        if (!changed) return false;
        Rebuild();
        Reveal();
        return true;
    }

    public void Reveal()
    {
        if (selected == null || !assets.TryGetValue(selected, out var node)) return;
        int index = visible.IndexOf(node);
        float height = scroll.viewport.rect.height;
        if (index < 0 || height <= 0) return;
        float top = scroll.content.anchoredPosition.y;
        if (index * RowHeight < top) top = index * RowHeight;
        else if ((index + 1) * RowHeight > top + height) top = (index + 1) * RowHeight - height;
        SetTop(top);
        Render();
    }

    void SetTop(float top) => scroll.content.anchoredPosition = new Vector2(0,
        Mathf.Clamp(top, 0, Mathf.Max(0, visible.Count * RowHeight - scroll.viewport.rect.height)));

    void Rebuild()
    {
        renderedStart = -1;
        visible.Clear();
        if (query.Length == 0)
        {
            foreach (var root in roots) Append(root);
        }
        else
        {
            foreach (var node in SearchResults())
            {
                if (node.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                visible.Add(node);
                if (visible.Count == SearchLimit) break;
            }
        }
        count.text = query.Length == 0 ? $"{visible.Count:N0} mục đang mở" :
            $"{visible.Count:N0} kết quả" + (visible.Count == SearchLimit ? "  ·  Giới hạn 150" : "");
        scroll.content.sizeDelta = new Vector2(0, visible.Count * RowHeight);
        SetTop(scroll.content.anchoredPosition.y);
        Render();
    }

    IEnumerable<Node> SearchResults()
    {
        foreach (var group in searchableGroups) yield return group;
        foreach (var entry in bridgeByKey.Values)
            if (BridgeSearch(entry).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                yield return GetBridgeNode(entry);
        foreach (var node in orderedAssets) yield return node;
    }

    void Append(Node node)
    {
        visible.Add(node);
        if (!expanded.Contains(node.Key)) return;
        var load = node.LoadChildren;
        node.LoadChildren = null;
        load?.Invoke();
        foreach (var child in node.Children) Append(child);
    }

    void Click(Row row)
    {
        var node = row.Node;
        if (node == null) return;
        if (!node.IsElement)
        {
            long started = RailwayBimExplorer.BeginMetric();
            if (!expanded.Add(node.Key)) expanded.Remove(node.Key);
            if (query.Length > 0)
            {
                expanded.Add(node.Key);
                for (var cursor = node.Parent; cursor != null; cursor = cursor.Parent) expanded.Add(cursor.Key);
                query = "";
                CloseSearch?.Invoke();
            }
            if (node.BridgeGroup != null) select("bridge-view:" + node.BridgeGroup, true);
            else if (node.ZoneToOpen != null) select("view:" + node.ZoneToOpen, true);
            Rebuild();
            RailwayBimExplorer.Metric("ExpandOrCollapse", started, VisibleCount, PooledCount);
            return;
        }
        string id = node.Asset.AssetID;
        float now = Time.unscaledTime;
        bool focus = lastClick == id && now - clickTime < .35f;
        lastClick = id;
        clickTime = now;
        select(id, focus || bridgeByAsset.ContainsKey(id));
    }

    public event Action CloseSearch;
    public int PooledCount => rows.Count;
    public int VisibleCount => visible.Count;

    Row MakeRow()
    {
        var button = UnityEngine.Object.Instantiate(template, scroll.content);
        button.name = "BIMTreeRow";
        button.onClick.RemoveAllListeners();
        var label = button.GetComponentInChildren<TMP_Text>(true);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.richText = false;
        var icon = RailwayBimExplorer.Text("TreeIcon", button.transform, template, 17);
        var folder = RailwayBimExplorer.Decoration("Folder", button.transform, new Color32(55, 160, 244, 255));
        var tab = RailwayBimExplorer.Decoration("Folder tab", folder.transform, folder.color);
        RailwayBimExplorer.Top(tab.rectTransform, -2, 3, 0, 0, 7);
        var elementMark = RailwayBimExplorer.Decoration("Element", button.transform, new Color32(179, 202, 224, 255));
        elementMark.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        var guide = RailwayBimExplorer.Rect("HierarchyLines", button.transform);
        var lines = new Image[4];
        for (int i = 0; i < lines.Length; i++)
            lines[i] = RailwayBimExplorer.Decoration("Indent", guide, new Color32(47, 69, 91, 255));
        var row = new Row { Button = button, Label = label, Icon = icon, Folder = folder, ElementMark = elementMark, Guide = guide, Lines = lines,
            Tooltip = button.gameObject.AddComponent<RailwayBimTooltip>() };
        row.Tooltip.Target = hint;
        button.onClick.AddListener(() => Click(row));
        return row;
    }

    public void Render()
    {
        if (!scroll.isActiveAndEnabled || scroll.viewport.rect.height <= 0) return;
        int start = Mathf.Max(0, Mathf.FloorToInt(scroll.content.anchoredPosition.y / RowHeight) - 1);
        int capacity = Mathf.CeilToInt(scroll.viewport.rect.height / RowHeight) + 3;
        int shown = Mathf.Clamp(visible.Count - start, 0, capacity);
        if (renderedStart == start && renderedCount == shown) return;
        renderedStart = start;
        renderedCount = shown;
        for (int i = 0; i < shown; i++)
        {
            if (i == rows.Count) rows.Add(MakeRow());
            var row = rows[i];
            var node = visible[start + i];
            var rect = (RectTransform)row.Button.transform;
            row.Button.gameObject.SetActive(true);
            RailwayBimExplorer.Top(rect, (start + i) * RowHeight, RowHeight - 2);
            if (row.Node != node) row.Tooltip.Clear();
            row.Node = node;
            bool searching = query.Length > 0;
            int level = searching ? 0 : node.Depth;
            float indent = 7 + Mathf.Min(level, 8) * 19;
            RailwayBimExplorer.Fill(row.Label.rectTransform, indent + 38, 0, 5, 0);
            RailwayBimExplorer.Top(row.Icon.rectTransform, 0, RowHeight - 2, indent, 0, 30);
            row.Folder.gameObject.SetActive(!node.IsElement);
            if (!node.IsElement) RailwayBimExplorer.Top(row.Folder.rectTransform, 14, 11, indent + 16, 0, 15);
            row.ElementMark.gameObject.SetActive(node.IsElement);
            if (node.IsElement) RailwayBimExplorer.Top(row.ElementMark.rectTransform, 14, 8, indent + 13, 0, 8);
            RailwayBimExplorer.Fill(row.Guide);
            for (int j = 0; j < row.Lines.Length; j++)
            {
                row.Lines[j].gameObject.SetActive(j < level);
                if (j < level) RailwayBimExplorer.Top(row.Lines[j].rectTransform, 0, RowHeight, 14 + j * 19, 0, 1);
            }
            row.Icon.text = node.IsElement ? "" : expanded.Contains(node.Key) ? "−" : "+";
            row.Icon.color = new Color32(71, 175, 252, 255);
            row.Label.fontSize = searching ? 15 : 17;
            row.Label.fontStyle = node.IsElement ? FontStyles.Normal : FontStyles.Bold;
            row.Label.text = searching && node.Asset != null ? node.Label + "  ·  " + Location(node.Asset.AssetID, false) : node.Label;
            row.Tooltip.Hint = node.Asset == null ? node.Label : Location(node.Asset.AssetID) + "  ·  " + node.Asset.AssetID;
            RailwayBimExplorer.ColorButton(row.Button, selected != null && selected == node.Asset?.AssetID, true);
        }
        for (int i = shown; i < rows.Count; i++) rows[i].Button.gameObject.SetActive(false);
    }
}
