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
        public string ZoneToOpen, Type;
        public int LeafCount, Profiles;
        public float Start, End;
        public bool HasProfile, HasTemplate;


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
    readonly Dictionary<string, Node> groups = new(StringComparer.Ordinal);
    readonly Action<RailwayBimSelectionScope> selectGroup;
    int filter;
    readonly ScrollRect scroll;
    readonly Button template;
    readonly TMP_Text count;
    readonly TMP_Text hint;
    readonly Action<string, bool> select;
    string query = "", selected, lastClick;
    float clickTime = float.NegativeInfinity;
    int renderedStart = -1, renderedCount = -1;

    public RailwayBimTree(ScrollRect scroll, Button template, TMP_Text count, TMP_Text hint, Action<string, bool> select, Action<RailwayBimSelectionScope> selectGroup = null)
    {
        this.scroll = scroll;
        this.template = template;
        this.count = count;
        this.hint = hint;
        this.select = select;
        this.selectGroup = selectGroup;
        scroll.onValueChanged.AddListener(_ => Render());
    }

    public void Load(RailwayBimStore store, RailwayBimZoneViewer viewer)
    {
        if (roots.Count != 0) return;
        var route = Group(null, "route:YTH", "Tuyến Yên Tử – Hạ Long Xanh");
        roots.Add(route);
        expanded.Add(route.Key);
        var records = store.Assets.Values.OrderBy(a => a.AssetID, StringComparer.Ordinal).ToArray();
        var sections = RailwayBimLogicalLayout.Sections.ToDictionary(section => section.Key,
            section => new Node { Key = "section:" + section.Key, Label = section.Label }, StringComparer.Ordinal);
        var byRailId = records.ToLookup(a => a.ParentAssetID ?? "", StringComparer.Ordinal);
        void AddRail(Node parent, BimAsset record)
        {
            var node = new Node { Asset = record, Key = record.AssetID, ZoneToOpen = RailwayBimZoneViewer.DetailedZone,
                Type = RailwayBimVietnamese.Text(record.ComponentType), Start = (float)record.ChainageStart, End = (float)record.ChainageEnd };
            assets.Add(record.AssetID, node);
            Attach(parent, node);
            UpdateName(node, record);
            if (node.IsElement) orderedAssets.Add(node);
            else foreach (var child in byRailId[record.AssetID]) AddRail(node, child);
        }

        var rail = sections["rail"];
        foreach (var track in records.Where(a => a.AssetType == "Segment" && a.ZoneID == "Z021")
            .GroupBy(a => a.TrackID).OrderByDescending(g => g.Key, StringComparer.Ordinal))
        {
            var branch = Group(rail, "track:" + track.Key, RailwayBimVietnamese.Text(track.Key) + " (" + track.Key + ")");
            foreach (var record in track) AddRail(branch, record);
        }
        expanded.Add(rail.Key);

        var infrastructure = viewer?.productionLoader?.catalog;
        if (infrastructure != null)
        {
            var entryById = infrastructure.entries.ToDictionary(e => e.globalId, StringComparer.Ordinal);
            foreach (var entry in infrastructure.entries.Where(e => e.IsElement))
            {
                if (!entryById.TryGetValue(entry.parentGlobalId, out var assembly) ||
                    !entryById.TryGetValue(assembly.parentGlobalId, out var domain)) continue;
                string zone = domain.parentGlobalId;
                var definition = RailwayBimLogicalLayout.ForInfrastructure(entry.assetId, zone);
                if (definition == null) continue;
                var section = sections[definition.Key];
                var domainNode = Group(section, entry.ifcClass, RailwayBimLogicalLayout.Domain(entry.ifcClass));
                var category = RailwayBimLogicalLayout.InfrastructureType(entry, assembly.Label);
                var typeNode = Group(domainNode, category, category);
                var batch = Group(typeNode, assembly.Label, assembly.Label);
                AddStructurePart(batch, entry, zone, category, InfrastructureLabel(entry));
            }
        }

        var bridge = viewer?.modularBridgeCatalog;
        if (bridge != null)
        {
            var bridgeNode = Group(sections["bridge"], "Bridge_Main", "Cầu SO 201 · 7 nhịp");
            var index = bridge.entries.ToDictionary(entry => entry.globalId, StringComparer.Ordinal);
            foreach (var entry in bridge.entries.Where(e => e.IsElement))
            {
                string category = RailwayBimLogicalLayout.BridgeType(entry);
                var type = Group(bridgeNode, category, category);
                if (index.TryGetValue(entry.parentGlobalId ?? "", out var parent) &&
                    !string.IsNullOrEmpty(parent.parentGlobalId))
                    type = Group(type, parent.globalId, BridgeLabel(parent));
                AddStructurePart(type, entry, RailwayBimZoneViewer.BridgeZone, category, BridgeLabel(entry));
            }
        }
        var existingProfiles = store.CompleteProfileIds().Concat(BridgeInfrastructureStore.CompleteUserProfileIds(infrastructure))
            .Concat(BridgeInfrastructureStore.CompleteUserProfileIds(bridge));
        foreach (string id in existingProfiles)
            if (assets.TryGetValue(id, out var leaf)) leaf.HasProfile = true;
        foreach (var definition in RailwayBimLogicalLayout.Sections)
        {
            var section = sections[definition.Key];
            if (section.Children.Count == 0) continue;
            AddGroup(section, route);
            AllocateProfiles(section);
        }
        SetDepth(route, 0);
        CountProfiles(route);
        Rebuild();
    }

    Node Group(Node parent, string suffix, string text)
    {
        string key = parent == null ? suffix : parent.Key + "/" + suffix;
        if (groups.TryGetValue(key, out var node)) return node;
        node = new Node { Key = key, Label = text, SearchText = text };
        AddGroup(node, parent);
        return node;
    }

    void AddGroup(Node node, Node parent)
    {
        node.SearchText ??= node.Label;
        groups.Add(node.Key, node);
        searchableGroups.Add(node);
        if (parent != null) Attach(parent, node);
    }

    void AddStructurePart(Node parent, BridgeBimEntry entry, string zone, string type, string label)
    {
        var id = entry.assetId;
        var node = new Node { Key = id, Label = label, Type = type, ZoneToOpen = zone,
            Start = entry.modeledStation, End = entry.modeledStation,
            Asset = new BimAsset { AssetID = id, AssetType = "Component", DisplayName = label, ComponentType = type },
            SearchText = string.Join(" ", label, type, id, zone, entry.Label, entry.sourceGlobalId, entry.ifcClass)
        };
        assets.Add(id, node);
        orderedAssets.Add(node);
        Attach(parent, node);
    }

    static string BridgeLabel(BridgeBimEntry entry)
    {
        if (RailwayBimSampleProfiles.TryGetName(entry.assetId, out var label)) return label;
        var code = entry.Label;
        if (entry.ifcClass == "IfcBridgePart" && code.Length == 3 && code[1] == '0' && char.IsDigit(code[2]))
            return (code[0] == 'S' ? "Nhịp " : code[0] == 'P' ? "Trụ " : "") + code;
        return code;
    }

    static string InfrastructureLabel(BridgeBimEntry entry)
    {
        if (RailwayBimSampleProfiles.TryGetName(entry.assetId, out var name)) return name;
        var label = entry.Label;
        int sourceZone = label.IndexOf("_Zone_", StringComparison.Ordinal);
        return sourceZone >= 0 && label.Length > sourceZone + 10 && label[sourceZone + 9] == '_'
            ? label.Substring(sourceZone + 10) : label;
    }

    static IEnumerable<Node> Leaves(Node node)
    {
        if (node.IsElement) yield return node;
        else foreach (var child in node.Children)
            foreach (var leaf in Leaves(child)) yield return leaf;
    }

    static void AllocateProfiles(Node section)
    {
        const int maximum = 150;
        var leaves = Leaves(section).ToArray();
        int count = 0;
        foreach (var leaf in leaves.Where(leaf => RailwayBimSampleProfiles.Contains(leaf.Key)))
        {
            leaf.HasProfile = true;
            leaf.HasTemplate = true;
            count++;
        }
        var queues = leaves.Where(leaf => !leaf.HasTemplate).GroupBy(leaf => leaf.Type ?? "")
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new Queue<Node>(group.OrderBy(leaf => leaf.Key, StringComparer.Ordinal))).ToArray();
        while (count < maximum && queues.Any(queue => queue.Count > 0))
            foreach (var queue in queues)
            {
                if (queue.Count == 0 || count == maximum) continue;
                var leaf = queue.Dequeue();
                leaf.HasProfile = leaf.HasTemplate = true;
                count++;
            }
    }

    static void CountProfiles(Node node)
    {
        if (node.IsElement)
        {
            node.LeafCount = 1;
            node.Profiles = node.HasProfile ? 1 : 0;
            return;
        }
        foreach (var child in node.Children)
        {
            CountProfiles(child);
            node.LeafCount += child.LeafCount;
            node.Profiles += child.Profiles;
        }
    }

    bool TryGetAsset(string id, out Node node) =>
        assets.TryGetValue(id, out node) || groups.TryGetValue(id, out node);

    public string ProfileType(string id) =>
        id != null && assets.TryGetValue(id, out var node) && node.HasTemplate ? node.Type : null;

    public void CycleFilter()
    {
        filter = (filter + 1) % 3;
        SetTop(0);
        Rebuild();
    }

    public string FilterLabel => filter == 1 ? "Có hồ sơ" : filter == 2 ? "Chưa có hồ sơ" : "Tất cả";

    bool PassesFilter(Node n) => filter == 0 || (filter == 1 ? n.Profiles > 0 : n.LeafCount > n.Profiles);

    void SelectScope(Node node)
    {
        var leaves = Leaves(node).ToArray();
        if (leaves.Length == 0) return;
        selectGroup?.Invoke(new RailwayBimSelectionScope(node.Key, node.Label, leaves.Select(leaf => leaf.Key),
            leaves.Select(leaf => leaf.ZoneToOpen).Where(zone => zone != null), node.Profiles,
            leaves.Min(leaf => leaf.Start), leaves.Max(leaf => leaf.End), node.Parent == null));
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
        if (selected == null || !TryGetAsset(selected, out var node)) return;
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
                if (!PassesFilter(node) || !Matches(node)) continue;
                visible.Add(node);
                if (visible.Count == SearchLimit) break;
            }
        }
        var route = roots.FirstOrDefault();
        string stats = route == null ? "" : $" · {route.Profiles:N0}/{route.LeafCount:N0} có hồ sơ";
        count.text = query.Length == 0 ? $"{visible.Count:N0} mục đang mở" + stats :
            $"{visible.Count:N0} kết quả" + (visible.Count == SearchLimit ? " · Giới hạn 150" : "") + stats;
        scroll.content.sizeDelta = new Vector2(0, visible.Count * RowHeight);
        SetTop(scroll.content.anchoredPosition.y);
        Render();
    }

    IEnumerable<Node> SearchResults() => searchableGroups.Concat(orderedAssets);

    bool Matches(Node node) => node.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
        node.HasTemplate && "hồ sơ mẫu ho so mau".IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

    void Append(Node node)
    {
        if (!PassesFilter(node)) return;
        visible.Add(node);
        if (!expanded.Contains(node.Key)) return;
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
            SelectScope(node);
            Rebuild();
            RailwayBimExplorer.Metric("ExpandOrCollapse", started, VisibleCount, PooledCount);
            return;
        }
        string id = node.Asset.AssetID;
        float now = Time.unscaledTime;
        bool focus = lastClick == id && now - clickTime < .35f;
        lastClick = id;
        clickTime = now;
        select(id, focus || id.StartsWith("G2-", StringComparison.Ordinal) || id.StartsWith("BRIDGE-", StringComparison.Ordinal));
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
            if (node.IsElement)
            {
                RailwayBimExplorer.Top(row.ElementMark.rectTransform, 14, 8, indent + 13, 0, 8);
                row.ElementMark.color = node.HasProfile
                    ? new Color32(245, 191, 81, 255) : new Color32(179, 202, 224, 255);
            }
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
            RailwayBimExplorer.ColorButton(row.Button, selected != null && selected == node.Key, true);
        }
        for (int i = shown; i < rows.Count; i++) rows[i].Button.gameObject.SetActive(false);
    }
}
