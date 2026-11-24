using System;
using System.Collections.Generic;
using UnityEngine;

namespace UttVr.BridgePilot
{
    // Owns the bridge selection and exposes data for the same three presentation tabs.
    public sealed class BridgeBimAdapter : MonoBehaviour
    {
        public BridgeBimCatalog catalog;
        public Material highlightMaterial;

        readonly Dictionary<string, BridgeBimEntry> byGlobalId = new Dictionary<string, BridgeBimEntry>(StringComparer.Ordinal);
        readonly Dictionary<string, BridgeBimEntry> byAssetId = new Dictionary<string, BridgeBimEntry>(StringComparer.Ordinal);
        readonly Dictionary<string, BridgeBimIdentity> bindings = new Dictionary<string, BridgeBimIdentity>(StringComparer.Ordinal);
        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

        public BridgeBimEntry Selected { get; private set; }
        public int HighlightedCount => originalMaterials.Count;
        public event Action<BridgeBimEntry> SelectionChanged;
        public bool TryGetGlobalId(string id, out BridgeBimEntry entry) => byGlobalId.TryGetValue(id, out entry);

        void Awake()
        {
            foreach (var entry in catalog.entries)
            {
                byGlobalId.Add(entry.globalId, entry);
                if (entry.IsElement) byAssetId.Add(entry.assetId, entry);
            }
            foreach (var identity in GetComponentsInChildren<BridgeBimIdentity>(true))
                bindings.Add(identity.GlobalId, identity);
            if (bindings.Count != byAssetId.Count) throw new InvalidOperationException("Bridge catalog and geometry binding counts differ.");
        }

        public bool SelectAsset(string assetId, bool useMaterialHighlight = true) =>
            assetId != null && byAssetId.TryGetValue(assetId, out var entry) && Select(entry, useMaterialHighlight);

        public bool Owns(BridgeBimIdentity identity) => identity != null &&
            bindings.TryGetValue(identity.GlobalId, out var bound) && bound == identity;

        public bool Pick(Ray ray) => TryPick(ray, out var entry) && Select(entry, true);

        public bool TryPick(Ray ray, out BridgeBimEntry entry)
        {
            entry = null;
            if (!Physics.Raycast(ray, out var hit, 2000, ~0, QueryTriggerInteraction.Ignore)) return false;
            var identity = hit.collider.GetComponentInParent<BridgeBimIdentity>();
            return Owns(identity) && byGlobalId.TryGetValue(identity.GlobalId, out entry);
        }

        bool Select(BridgeBimEntry entry, bool useMaterialHighlight)
        {
            if (!bindings.TryGetValue(entry.globalId, out var binding)) return false;
            bool changed = !ReferenceEquals(Selected, entry);
            if (!changed && useMaterialHighlight == (originalMaterials.Count != 0)) return true;
            RestoreMaterials();
            Selected = entry;
            if (useMaterialHighlight) ApplyMaterials(binding.Renderers);
            if (changed) SelectionChanged?.Invoke(entry);
            return true;
        }

        void ApplyMaterials(IEnumerable<Renderer> renderers)
        {
            foreach (var renderer in renderers)
            {
                var original = renderer.sharedMaterials;
                originalMaterials.Add(renderer, original);
                var selected = new Material[original.Length];
                Array.Fill(selected, highlightMaterial);
                renderer.sharedMaterials = selected;
            }
        }

        public bool TryGetSelectedBounds(out Bounds bounds)
        {
            bounds = default;
            if (Selected == null || !bindings.TryGetValue(Selected.globalId, out var binding) || binding.Renderers.Length == 0)
                return false;
            bounds = binding.Renderers[0].bounds;
            foreach (var renderer in binding.Renderers) bounds.Encapsulate(renderer.bounds);
            return true;
        }

        public IEnumerable<Renderer> FocusRenderers(string groupId)
        {
            foreach (var entry in catalog.entries)
            {
                if (!entry.IsElement || (groupId != null && entry.parentGlobalId != groupId) ||
                    !bindings.TryGetValue(entry.globalId, out var identity)) continue;
                foreach (var renderer in identity.Renderers)
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                        yield return renderer;
            }
        }

        public IEnumerable<Renderer> SelectedRenderers =>
            Selected != null && bindings.TryGetValue(Selected.globalId, out var identity)
                ? identity.Renderers : Array.Empty<Renderer>();

        public Vector3 RailwayDirection()
        {
            Renderer first = null, last = null;
            foreach (var entry in catalog.entries)
            {
                if ((entry.ifcClass != "deck" && entry.ifcClass != "RAIL_DECK") ||
                    !bindings.TryGetValue(entry.globalId, out var identity) ||
                    identity.Renderers.Length == 0) continue;
                var renderer = identity.Renderers[0];
                first ??= renderer;
                last = renderer;
            }
            return first != null && last != null ? last.bounds.center - first.bounds.center : transform.forward;
        }

        public void Clear()
        {
            if (Selected == null) return;
            RestoreMaterials();
            Selected = null;
            SelectionChanged?.Invoke(null);
        }

        void RestoreMaterials()
        {
            foreach (var item in originalMaterials)
                if (item.Key != null) item.Key.sharedMaterials = item.Value;
            originalMaterials.Clear();
        }

        void OnDestroy() => RestoreMaterials();
    }
}
