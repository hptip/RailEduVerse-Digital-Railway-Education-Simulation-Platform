using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UttVr.BridgePilot
{
    public sealed class BridgePilotView : MonoBehaviour
    {
        public BridgeBimAdapter adapter;
        public Camera view;
        public Bounds bridgeBounds;
        public Font interfaceFont;
        const float PanelRatio = .37f;
        readonly Dictionary<string, List<BridgeBimEntry>> children = new Dictionary<string, List<BridgeBimEntry>>(StringComparer.Ordinal);
        readonly HashSet<string> expanded = new HashSet<string>(StringComparer.Ordinal);
        readonly string[] tabs = { "Danh sách", "Thông số", "Vòng đời" };
        GUIStyle small, heading, row, selectedRow, tabStyle, activeTab, searchStyle;
        Vector2 scroll, pointerDown;
        Vector3 pivot;
        float yaw = 27, pitch = 23, distance;
        int tab;
        bool pressedInView, dragging, audit;
        string filter = "";
        ProfilerRecorder memory;
        float nextSample;

        void Start()
        {
            foreach (var entry in adapter.catalog.entries)
            {
                if (!children.TryGetValue(entry.parentGlobalId, out var siblings))
                    children.Add(entry.parentGlobalId, siblings = new List<BridgeBimEntry>());
                siblings.Add(entry);
                if (!entry.IsElement) expanded.Add(entry.globalId);
            }
            adapter.SelectionChanged += RevealSelection;
            view.rect = new Rect(PanelRatio, 0, 1 - PanelRatio, 1);
            ResetView();
            RailwayBimExplorer.FillBrowser(true);
            audit = Application.absoluteURL.Contains("bridgeAudit=1");
            if (audit) memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            Debug.Log($"[BridgePilot] Ready: {adapter.catalog.entries.Length} IFC nodes; {adapter.catalog.entries.Count(e => e.IsElement)} selectable assets.");
        }

        void RevealSelection(BridgeBimEntry entry)
        {
            for (var cursor = entry; cursor != null && adapter.TryGetGlobalId(cursor.parentGlobalId, out var parent); cursor = parent)
                expanded.Add(parent.globalId);
        }

        void ResetView()
        {
            pivot = bridgeBounds.center;
            yaw = 27;
            pitch = 23;
            distance = RailwayBimCamera.FitDistance(bridgeBounds, Quaternion.Euler(pitch, yaw, 0),
                view.fieldOfView, view.aspect, .82f, .88f);
            ApplyView();
        }

        void Focus()
        {
            if (!adapter.TryGetSelectedBounds(out var bounds)) return;
            pivot = bounds.center;
            distance = Mathf.Clamp(bounds.size.magnitude * 1.35f, 2.5f, 130);
            ApplyView();
            if (audit) Debug.Log($"[BridgePilot] Focus,{adapter.Selected.assetId},{pivot.x:F3},{pivot.y:F3},{pivot.z:F3}");
        }

        void ApplyView()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            view.transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
        }

        void Update()
        {
            if (audit && Time.unscaledTime >= nextSample)
            {
                nextSample = Time.unscaledTime + 2;
                string bytes = memory.Valid && memory.Count > 0 ? memory.LastValue.ToString() : "unavailable";
                Debug.Log(FormattableString.Invariant($"[BridgePilot] Sample,{Time.unscaledDeltaTime * 1000:F3},{bytes},{GC.GetTotalMemory(false)}"));
            }
            var mouse = Mouse.current;
            if (mouse == null) return;
            var position = mouse.position.ReadValue();
            bool inside = view.pixelRect.Contains(position);
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) adapter.Clear();
            if (inside && Keyboard.current?.fKey.wasPressedThisFrame == true) Focus();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressedInView = inside;
                dragging = false;
                pointerDown = position;
            }
            if (inside && mouse.leftButton.isPressed && pressedInView && (position - pointerDown).sqrMagnitude > 25)
            {
                dragging = true;
                var delta = mouse.delta.ReadValue();
                yaw += delta.x * .18f;
                pitch = Mathf.Clamp(pitch - delta.y * .18f, -70, 85);
                ApplyView();
            }
            if (inside && mouse.scroll.ReadValue().y != 0)
            {
                distance = Mathf.Clamp(distance * Mathf.Exp(-mouse.scroll.ReadValue().y / 900f), 1.5f, 260);
                ApplyView();
            }
            if (!mouse.leftButton.wasReleasedThisFrame) return;
            bool pick = inside && pressedInView && !dragging;
            pressedInView = false;
            if (!pick || !adapter.Pick(view.ScreenPointToRay(position))) return;
            if (tab == 0)
            {
                filter = "";
                int selectedIndex = VisibleEntries("").TakeWhile(entry => entry != adapter.Selected).Count();
                scroll.y = Mathf.Max(0, (selectedIndex - 2) * 32);
            }
            if (audit) Debug.Log($"[BridgePilot] ModelPick,{adapter.Selected.assetId},{adapter.HighlightedCount}");
        }

        void OnGUI()
        {
            if (small == null) ConfigureStyles();
            float width = Screen.width * PanelRatio;
            Paint(new Rect(0, 0, width, Screen.height), new Color32(12, 24, 39, 255));
            GUI.Label(new Rect(16, 12, width - 32, 32), "Cầu BIM · SO 201", heading);
            GUI.Label(new Rect(16, 47, width - 32, 20), $"{adapter.catalog.schema}   /   {adapter.catalog.entries.Count(e => e.IsElement)} cấu kiện", small);
            float tabWidth = (width - 30) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
                if (GUI.Button(new Rect(12 + i * tabWidth, 78, tabWidth - 3, 36), tabs[i], i == tab ? activeTab : tabStyle))
                    if (tab != i) { tab = i; scroll = Vector2.zero; }

            GUILayout.BeginArea(new Rect(12, 126, width - 24, Screen.height - 180));
            if (tab == 0)
            {
                GUILayout.Label("Tìm theo tên, loại IFC hoặc mã", small);
                filter = GUILayout.TextField(filter, searchStyle, GUILayout.Height(28));
            }
            scroll = GUILayout.BeginScrollView(scroll);
            if (tab == 0)
            {
                if (string.IsNullOrWhiteSpace(filter)) DrawChildren("", 0);
                else foreach (var entry in adapter.catalog.entries.Where(e => e.IsElement && Matches(e))) DrawEntry(entry, 0);
            }
            else if (adapter.Selected == null) GUILayout.Label("Chọn một cấu kiện từ cây hoặc mô hình 3D.", small);
            else if (tab == 1) DrawProperties(adapter.Selected);
            else
            {
                GUILayout.Label(adapter.Selected.Label, heading);
                GUILayout.Label("Chưa có dữ liệu vòng đời.", small);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (GUI.Button(new Rect(12, Screen.height - 44, (width - 32) * .5f, 32), "Toàn cảnh", tabStyle)) ResetView();
            GUI.enabled = adapter.Selected != null;
            if (GUI.Button(new Rect(width * .5f + 4, Screen.height - 44, (width - 32) * .5f, 32), "Định vị", tabStyle)) Focus();
            GUI.enabled = true;
            GUI.Label(new Rect(width + 16, 12, Screen.width - width - 24, 24), "Kéo trái để xoay · Cuộn để zoom · F để định vị · Esc để bỏ chọn", small);
        }

        bool Matches(BridgeBimEntry entry) => entry.Label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
            entry.ifcClass.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || entry.assetId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

        IEnumerable<BridgeBimEntry> VisibleEntries(string id)
        {
            if (!children.TryGetValue(id, out var siblings)) yield break;
            foreach (var entry in siblings)
            {
                yield return entry;
                if (!expanded.Contains(entry.globalId)) continue;
                foreach (var child in VisibleEntries(entry.globalId)) yield return child;
            }
        }

        void DrawChildren(string id, int depth)
        {
            if (!children.TryGetValue(id, out var siblings)) return;
            foreach (var entry in siblings)
            {
                DrawEntry(entry, depth);
                if (expanded.Contains(entry.globalId)) DrawChildren(entry.globalId, depth + 1);
            }
        }

        void DrawEntry(BridgeBimEntry entry, int depth)
        {
            string icon = entry.IsElement ? "·" : expanded.Contains(entry.globalId) ? "−" : "+";
            string text = new string(' ', depth * 3) + icon + "  " + entry.Label;
            if (!GUILayout.Button(text, adapter.Selected == entry ? selectedRow : row, GUILayout.Height(28))) return;
            if (entry.IsElement && adapter.SelectAsset(entry.assetId))
            {
                Focus();
                if (audit) Debug.Log($"[BridgePilot] TreePick,{entry.assetId},{adapter.HighlightedCount}");
            }
            else if (!expanded.Add(entry.globalId)) expanded.Remove(entry.globalId);
        }

        void DrawProperties(BridgeBimEntry entry)
        {
            GUILayout.Label(entry.Label, heading);
            GUILayout.Label("IFC class  " + entry.ifcClass, small);
            GUILayout.Label("GlobalId  " + entry.globalId, small);
            GUILayout.Label("AssetID  " + entry.assetId, small);
            GUILayout.Label("Parent GlobalId  " + entry.parentGlobalId, small);
            if (!string.IsNullOrWhiteSpace(entry.predefinedType)) GUILayout.Label("PredefinedType  " + entry.predefinedType, small);
            if (!string.IsNullOrWhiteSpace(entry.description)) GUILayout.Label("Description  " + entry.description, small);
            if (!string.IsNullOrWhiteSpace(entry.tag)) GUILayout.Label("Tag  " + entry.tag, small);
            GUILayout.Space(12);
            GUILayout.Label("Property Sets", heading);
            if (entry.propertyLines.Length == 0) GUILayout.Label("Chưa có dữ liệu.", small);
            else foreach (string line in entry.propertyLines) GUILayout.Label(line, small);
        }

        void ConfigureStyles()
        {
            small = new GUIStyle(GUI.skin.label) { font = interfaceFont, fontSize = 14, wordWrap = true };
            small.normal.textColor = new Color32(205, 222, 238, 255);
            heading = new GUIStyle(small) { fontSize = 22, fontStyle = FontStyle.Bold };
            row = new GUIStyle(GUI.skin.button) { font = interfaceFont, alignment = TextAnchor.MiddleLeft, fontSize = 13, clipping = TextClipping.Clip };
            selectedRow = new GUIStyle(row); selectedRow.normal.textColor = new Color32(102, 198, 255, 255);
            tabStyle = new GUIStyle(GUI.skin.button) { font = interfaceFont, fontSize = 14 };
            activeTab = new GUIStyle(tabStyle) { fontStyle = FontStyle.Bold }; activeTab.normal.textColor = selectedRow.normal.textColor;
            searchStyle = new GUIStyle(GUI.skin.textField) { font = interfaceFont, fontSize = 14 };
        }

        static void Paint(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void OnDestroy()
        {
            if (adapter != null) adapter.SelectionChanged -= RevealSelection;
            if (memory.Valid) memory.Dispose();
            RailwayBimExplorer.FillBrowser(false);
        }
    }
}
