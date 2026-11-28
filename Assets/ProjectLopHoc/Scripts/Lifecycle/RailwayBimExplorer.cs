using System;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Profiling;
using UnityEngine.UI;

// Web presentation of the existing dashboard; Android retains its serialized world-space layout.
sealed class RailwayBimExplorer
{
    public const string Empty = "Vui lòng chọn một cấu kiện trong tab Danh sách hoặc trên mô hình 3D.";
    const float PanelWidth = .38f;
    static readonly Color Background = new Color32(12, 24, 39, 255);
    static readonly Color Surface = new Color32(21, 38, 57, 255);
    static readonly Color Muted = new Color32(171, 195, 217, 255);

    readonly RailwayBimDashboard ui;
    readonly RectTransform panel, list, details, fields, breadcrumb, tools, cameraGuide;
    readonly TMP_Text count, hint, empty, selectedCaption;
    readonly RailwayBimTree tree;
    float searchAt = float.PositiveInfinity;
    bool fromTree;
    string lastSelection;
    Vector2 oldSize;
    ProfilerRecorder gcAllocations, canvasBatch;
    float nextSample;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void UttBimExplorerFillBrowser(int enabled);
#endif
    public static void FillBrowser(bool active)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        UttBimExplorerFillBrowser(active ? 1 : 0);
#endif
    }

    public RailwayBimExplorer(RailwayBimDashboard ui, Action<string, bool> select)
    {
        this.ui = ui;
        var canvas = ui.GetComponent<RailwayBimVrPresentation>().dashboardCanvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0;
        }
        var previousLayout = ui.leftNav.transform.parent.gameObject;
        panel = Rect("BIM Explorer", canvas.transform);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = new Vector2(PanelWidth, 1);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        var background = panel.gameObject.AddComponent<Image>();
        background.color = Background;
        background.raycastTarget = true;

        var header = Rect("Header", panel);
        Top(header, 0, 65);
        Decoration("Header background", header, Surface);
        var name = Text("Heading", header, ui.itemTemplate, 20);
        Top(name.rectTransform, 8, 26, 15, 208);
        name.text = "Quản lý vòng đời tài sản BIM";
        name.fontStyle = FontStyles.Bold;
        var subtitle = Text("Zone", header, ui.itemTemplate, 14);
        Top(subtitle.rectTransform, 35, 20, 15, 208);
        subtitle.color = Muted;
        subtitle.text = "Tuyến Yên Tử – Hạ Long Xanh";
        HeaderButton(ui.importButton, header, 108, "Nhập Excel");
        HeaderButton(ui.exportButton, header, 10, "Xuất Excel");

        var tabs = Rect("Explorer Tabs", panel);
        Top(tabs, 74, 43, 10, 10);
        ButtonRow(tabs, ui.tabs, new[] { "Danh sách", "Thông số", "Vòng đời" });

        list = Rect("Danh sách", panel);
        Fill(list, 10, 127, 10, 105);
        ui.search.transform.SetParent(list, false);
        Top((RectTransform)ui.search.transform, 0, 42, 0, 128);
        ui.search.textComponent.fontSize = 17;
        if (ui.search.placeholder is TMP_Text placeholder)
        {
            placeholder.fontSize = 17;
            placeholder.text = "Tìm theo tên, mã hoặc “hồ sơ mẫu”…";
        }
        count = Text("Tree count", list, ui.itemTemplate, 13);
        count.color = Muted;
        Top(count.rectTransform, 43, 27, 5);
        var treeScroll = ui.listContent.GetComponentInParent<ScrollRect>(true);
        treeScroll.transform.SetParent(list, false);
        Fill((RectTransform)treeScroll.transform, 0, 71);
        ConfigureTreeScroll(treeScroll);
        var profileFilter = UnityEngine.Object.Instantiate(ui.itemTemplate, list);
        profileFilter.name = "BIM Profile Filter";
        profileFilter.gameObject.SetActive(true);
        var filterRect = (RectTransform)profileFilter.transform;
        filterRect.anchorMin = filterRect.anchorMax = Vector2.one;
        filterRect.pivot = Vector2.one;
        filterRect.anchoredPosition = Vector2.zero;
        filterRect.sizeDelta = new Vector2(120, 42);

        details = Rect("Hồ sơ cấu kiện", panel);
        Fill(details, 10, 127, 10, 105);
        var crumb = ui.breadcrumb;
        crumb.transform.SetParent(details, false);
        breadcrumb = crumb.rectTransform;
        Top(breadcrumb, 0, 40, 5, 5);
        crumb.fontSize = 14;
        crumb.color = Muted;
        crumb.textWrappingMode = TextWrappingModes.Normal;
        crumb.overflowMode = TextOverflowModes.Ellipsis;

        ui.title.transform.SetParent(details, false);
        Top(ui.title.rectTransform, 42, 48, 5, 5);
        ui.title.fontSize = 21;
        ui.title.alignment = TextAlignmentOptions.MidlineLeft;
        ui.title.overflowMode = TextOverflowModes.Ellipsis;

        var stages = ui.lifecycleNavigation;
        stages.SetParent(details, false);
        Top(stages, 92, 38);
        ButtonRow(stages, ui.stageButtons, null);
        var stageViews = (RectTransform)ui.stageProfileButton.transform.parent;
        stageViews.SetParent(details, false);
        Top(stageViews, 135, 33);
        ButtonRow(stageViews, new[] { ui.stageProfileButton, ui.stageRecordsButton, ui.stageHistoryButton },
            new[] { "Hồ sơ", "Kiểm tra", "Nhật ký" });

        var infoScroll = ui.fieldsContent.GetComponentInParent<ScrollRect>(true);
        fields = (RectTransform)infoScroll.transform;
        fields.SetParent(details, false);
        Fill(fields, 0, 92, 0, 37);
        empty = Text("Empty selection", details, ui.itemTemplate, 19);
        empty.textWrappingMode = TextWrappingModes.Normal;
        Top(empty.rectTransform, 9, 80, 12, 10);
        empty.text = Empty;

        var actions = Rect("Hồ sơ · thao tác", details);
        Bottom(actions, 0, 33);
        var history = (RectTransform)ui.historyPreviousButton.transform.parent;
        history.SetParent(actions, false);
        Fill(history);
        ButtonRow(history, new[] { ui.historyPreviousButton, ui.historyNextButton }, new[] { "Mới hơn", "Cũ hơn" });
        foreach (var button in new[] { ui.saveButton, ui.addInspectionButton, ui.addMaintenanceButton })
        {
            button.transform.SetParent(actions, false);
            Fill((RectTransform)button.transform, 0, 0, 0, 0);
            ColorButton(button, false);
        }

        selectedCaption = Text("Selection", panel, ui.itemTemplate, 15);
        Bottom(selectedCaption.rectTransform, 80, 23, 14, 10);
        selectedCaption.overflowMode = TextOverflowModes.Ellipsis;
        tools = Rect("Lệnh", panel);
        Bottom(tools, 48, 29, 10, 10);
        if (ui.focusButton == null)
        {
            ui.focusButton = UnityEngine.Object.Instantiate(ui.itemTemplate, tools);
            ui.focusButton.gameObject.name = "FocusSelection";
            ui.focusButton.gameObject.SetActive(true);
        }
        ButtonRow(tools, new[] { ui.detailButton, ui.backButton, ui.focusButton, ui.hideButton, ui.resetButton },
            new[] { "Tách", "Mặt cắt", "Định vị", "Cô lập", "X-ray" });
        ui.status.transform.SetParent(panel, false);
        Bottom(ui.status.rectTransform, 2, 43, 13, 130);
        ui.status.fontSize = 13;
        ui.status.overflowMode = TextOverflowModes.Ellipsis;
        ui.status.textWrappingMode = TextWrappingModes.Normal;
        HeaderButton(ui.applyButton, panel, 10, "Xác nhận nhập");
        var apply = (RectTransform)ui.applyButton.transform;
        apply.anchorMin = apply.anchorMax = new Vector2(1, 0);
        apply.pivot = new Vector2(1, 0);
        apply.anchoredPosition = new Vector2(-10, 2);
        apply.sizeDelta = new Vector2(114, 24);

        hint = Text("Tooltip", panel, ui.itemTemplate, 14);
        hint.color = Color.white;
        hint.fontStyle = FontStyles.Bold;
        hint.textWrappingMode = TextWrappingModes.Normal;
        hint.rectTransform.SetAsLastSibling();
        Top(hint.rectTransform, 121, 43, 10, 10);
        var tooltipBackground = Decoration("Backplate", hint.transform, Surface);
        // Keep the text as the last graphic so the backplate is rendered below it.
        tooltipBackground.transform.SetParent(panel, false);
        tooltipBackground.transform.SetSiblingIndex(hint.transform.GetSiblingIndex());
        Top(tooltipBackground.rectTransform, 121, 43, 10, 10);
        var tooltipPlate = tooltipBackground.gameObject.AddComponent<RailwayBimTooltipPlate>();
        tooltipPlate.Target = hint;
        hint.gameObject.SetActive(false);
        for (var i = 0; i < ui.stageButtons.Length; i++)
        {
            string stage = RailwayBimStages.All[i].name;
            Label(ui.stageButtons[i], (i + 1).ToString(), 18);
            var tip = ui.stageButtons[i].gameObject.AddComponent<RailwayBimTooltip>();
            tip.Target = hint;
            tip.Hint = stage;
        }

        cameraGuide = Rect("Thao tác mô hình", canvas.transform);
        cameraGuide.anchorMin = cameraGuide.anchorMax = Vector2.one;
        cameraGuide.pivot = Vector2.one;
        cameraGuide.anchoredPosition = new Vector2(-16, -14);
        cameraGuide.sizeDelta = new Vector2(286, 48);
        Decoration("Camera guide background", cameraGuide, new Color32(14, 29, 45, 225));
        var guide = Text("Mouse help", cameraGuide, ui.itemTemplate, 13);
        Fill(guide.rectTransform, 10, 2, 8, 2);
        guide.textWrappingMode = TextWrappingModes.Normal;
        guide.text = "Kéo trái: xoay   ·   Kéo phải: dịch\nCuộn: zoom   ·   F: định vị   ·   Esc: bỏ chọn";

        previousLayout.SetActive(false);
        tree = new RailwayBimTree(treeScroll, ui.itemTemplate, count, hint, (id, focus) =>
        {
            fromTree = true;
            try { select(id, focus); }
            finally { fromTree = false; }
        }, scope =>
        {
            fromTree = true;
            try { ui.controller.SelectGroup(scope); }
            finally { fromTree = false; }
        });
        Label(profileFilter, "Tất cả", 14);
        ColorButton(profileFilter, false);
        profileFilter.onClick.RemoveAllListeners();
        profileFilter.onClick.AddListener(() =>
        {
            tree.CycleFilter();
            var text = tree.FilterLabel;
            Label(profileFilter, text, 14);
            ColorButton(profileFilter, text != "Tất cả");
            Audit("ProfileFilter," + text + "," + tree.VisibleCount);
        });
        tree.CloseSearch += () => ui.search.SetTextWithoutNotify("");
        ui.search.onValueChanged.AddListener(_ => searchAt = Time.unscaledTime + .16f);
        Show("General", null);
        if (Auditing)
        {
            gcAllocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            canvasBatch = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Canvas.BuildBatch");
        }
        FillBrowser(true);
        UpdateViewport();
    }

    static void ConfigureTreeScroll(ScrollRect scroll)
    {
        var content = scroll.content;
        foreach (var child in content.GetComponents<LayoutGroup>()) child.enabled = false;
        if (content.TryGetComponent<ContentSizeFitter>(out var fitter)) fitter.enabled = false;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.localScale = Vector3.one;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
    }

    public void Tick()
    {
        UpdateViewport();
        if (Auditing && ui.controller.Ready && Time.unscaledTime >= nextSample)
        {
            nextSample = Time.unscaledTime + 2;
            string allocation = gcAllocations.Valid && gcAllocations.Count > 0 ? gcAllocations.LastValue.ToString() : "unavailable";
            string rebuild = canvasBatch.Valid && canvasBatch.Count > 0 ? canvasBatch.LastValue.ToString() : "unavailable";
            Audit($"Frame,{Time.unscaledDeltaTime * 1000:F3},{tree.PooledCount},{allocation},{rebuild}");
        }
        if (Time.unscaledTime < searchAt) return;
        searchAt = float.PositiveInfinity;
        long started = BeginMetric();
        tree.Search(ui.search.text);
        Metric("Search", started, tree.VisibleCount, tree.PooledCount);
    }

    public void Dispose()
    {
        if (gcAllocations.Valid) gcAllocations.Dispose();
        if (canvasBatch.Valid) canvasBatch.Dispose();
        FillBrowser(false);
    }

    public void Refresh(string id)
    {
        tree.Load(ui.controller.Store, ui.controller.zoneViewer);
        id = ui.controller.GroupSelection?.Key ?? id;
        if (!fromTree && id != null && ui.search.text.Length > 0 && id != lastSelection)
        {
            ui.search.SetTextWithoutNotify("");
            tree.Search("");
        }
        tree.SetSelection(id);
        lastSelection = id;
        selectedCaption.text = ui.controller.GroupSelection is { } scope
            ? scope.Label + " · " + scope.AssetIds.Count.ToString("N0") + " cấu kiện"
            : id == null ? "Chọn một nhóm hoặc cấu kiện" : tree.Label(id) + "   ·   " + id;
        ui.breadcrumb.text = tree.Location(id);
        Show(ui.ActiveTab, id);
    }

    public void NamesChanged(string id = null) => tree.RefreshNames(ui.controller.Store, id);
    public string SelectionLabel(string id) => tree.Label(id);
    public string ProfileType(string id) => tree.ProfileType(id);

    public void Show(string tab, string id)
    {
        bool listing = tab == "General";
        bool selected = id != null || ui.controller.GroupSelection != null;
        bool life = tab == "Lifecycle" && id != null && ui.controller.GroupSelection == null;
        list.gameObject.SetActive(listing);
        details.gameObject.SetActive(!listing);
        empty.gameObject.SetActive(!selected);
        breadcrumb.gameObject.SetActive(selected);
        ui.title.gameObject.SetActive(selected);
        fields.gameObject.SetActive(selected);
        Fill(fields, 0, life ? 173 : 92, 0, 37);
        if (listing) tree?.Reveal();
    }

    void UpdateViewport()
    {
        var camera = ui.controller.cameraController.GetComponent<Camera>();
        bool visible = panel.gameObject.activeInHierarchy;
        Rect rect = visible ? new Rect(PanelWidth, 0, 1 - PanelWidth, 1) : new Rect(0, 0, 1, 1);
        if (camera.rect != rect) camera.rect = rect;
        if (oldSize == panel.rect.size) return;
        oldSize = panel.rect.size;
        tree?.Render();
    }

    static readonly bool Auditing = Application.absoluteURL.Contains("bimAudit=1");
    public static void Audit(string value)
    {
        if (Auditing) Debug.Log("[BIMTree] " + value);
    }
    public static double Milliseconds(long started) => (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
    public static long BeginMetric() => Auditing ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
    public static void Metric(string action, long started, int visible, int pool)
    {
        if (started != 0) Audit($"{action},{Milliseconds(started):F3},{visible},{pool}");
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static void Fill(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    public static void Top(RectTransform rect, float top, float height, float left = 0, float right = 0, float width = 0)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(width > 0 ? 0 : 1, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width > 0 ? width : -left - right, height);
    }

    static void Bottom(RectTransform rect, float bottom, float height, float left = 0, float right = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.right;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(left, bottom);
        rect.sizeDelta = new Vector2(-left - right, height);
    }

    public static Image Decoration(string name, Transform parent, Color color)
    {
        var rect = Rect(name, parent);
        Fill(rect);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static TMP_Text Text(string name, Transform parent, Button template, float size)
    {
        var rect = Rect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = template.GetComponentInChildren<TMP_Text>(true).font;
        text.fontSize = size;
        text.color = new Color32(235, 244, 255, 255);
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    static void HeaderButton(Button button, RectTransform parent, float offset, string text)
    {
        var rect = (RectTransform)button.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-offset, -15);
        rect.sizeDelta = new Vector2(92, 36);
        Label(button, text, 15);
        ColorButton(button, false);
    }

    static void ButtonRow(RectTransform parent, Button[] buttons, string[] labels)
    {
        if (parent.TryGetComponent<LayoutGroup>(out var layout)) layout.enabled = false;
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            var rect = (RectTransform)button.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2((float)i / buttons.Length, 0);
            rect.anchorMax = new Vector2((float)(i + 1) / buttons.Length, 1);
            rect.offsetMin = new Vector2(2, 0);
            rect.offsetMax = new Vector2(-2, 0);
            rect.localScale = Vector3.one;
            if (labels != null) Label(button, labels[i], 17);
            ColorButton(button, false);
        }
    }

    static void Label(Button button, string label, float size)
    {
        var text = button.GetComponentInChildren<TMP_Text>(true);
        text.text = label;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    public static void ColorButton(Button button, bool active, bool row = false)
    {
        var colors = button.colors;
        var image = button.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = true;
        colors.normalColor = active ? new Color32(9, 98, 176, 255) : row ? Background : Surface;
        colors.highlightedColor = active ? new Color32(18, 118, 207, 255) : new Color32(32, 65, 94, 255);
        colors.selectedColor = colors.normalColor;
        colors.pressedColor = new Color32(21, 86, 145, 255);
        button.colors = colors;
    }
}

sealed class RailwayBimTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public TMP_Text Target;
    public string Hint;
    public void OnPointerEnter(PointerEventData eventData) => Show();
    public void OnPointerExit(PointerEventData eventData) => Clear();
    public void OnSelect(BaseEventData eventData) => Show();
    public void OnDeselect(BaseEventData eventData) => Clear();
    void Show() { if (Target != null) { Target.text = Hint; Target.gameObject.SetActive(true); } }
    public void Clear() { if (Target != null && Target.text == Hint) Target.gameObject.SetActive(false); }
    void OnDisable() => Clear();
}

sealed class RailwayBimTooltipPlate : MonoBehaviour
{
    public TMP_Text Target;
    void LateUpdate() => GetComponent<Image>().enabled = Target != null && Target.gameObject.activeInHierarchy;
}
