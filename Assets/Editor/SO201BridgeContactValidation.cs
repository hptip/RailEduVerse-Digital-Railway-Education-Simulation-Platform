using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UttVr.BridgePilot;

static partial class SO201OriginalBridgeAudit
{
    const string FinalReports = "Documentation/BIMExpansion/SO201Finalization/";
    const float ContactTolerance = .035f;

    [Serializable] sealed class GeometryInventory
    {
        public string sourcePath, sourceSha256;
        public float worldScale;
        public GeometryAsset[] elements;
    }

    [Serializable] sealed class GeometryAsset
    {
        public string globalId, parentId, ifcClass, label, mesh, meshLocalId;
        public string[] materials;
        public Matrix4x4 localTransform, bridgeTransform;
        public Bounds bridgeBounds;
        public Vector3[] vertices;
        public int[] triangles;
    }

    sealed class ContactMesh
    {
        public string id, label, family;
        public Vector3[] vertices;
        public int[] triangles;
        public Bounds bounds;
    }

    readonly struct SurfaceSample
    {
        public readonly float min, max;
        public bool hit => min <= max;
        public SurfaceSample(float lower, float upper) { min = lower; max = upper; }
    }

    static SurfaceSample MeshSurface(ContactMesh mesh, float x, float z)
    {
        float lower = float.PositiveInfinity, upper = float.NegativeInfinity;
        if (x < mesh.bounds.min.x || x > mesh.bounds.max.x || z < mesh.bounds.min.z || z > mesh.bounds.max.z)
            return new SurfaceSample(lower, upper);
        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            Vector3 a = mesh.vertices[mesh.triangles[i]];
            Vector3 b = mesh.vertices[mesh.triangles[i + 1]] - a;
            Vector3 c = mesh.vertices[mesh.triangles[i + 2]] - a;
            float determinant = b.x * c.z - c.x * b.z;
            if (Mathf.Abs(determinant) < 1e-8f) continue;
            float u = ((x - a.x) * c.z - (z - a.z) * c.x) / determinant;
            float v = (b.x * (z - a.z) - b.z * (x - a.x)) / determinant;
            if (u < -1e-5f || v < -1e-5f || u + v > 1.00001f) continue;
            float y = a.y + u * b.y + v * c.y;
            lower = Mathf.Min(lower, y);
            upper = Mathf.Max(upper, y);
        }
        return new SurfaceSample(lower, upper);
    }

    // The projected overlap only defines where to sample. Measurements use the actual triangles.
    static float[] SampleInterface(ContactMesh lower, ContactMesh upper, int resolution = 11)
    {
        float x0 = Mathf.Max(lower.bounds.min.x, upper.bounds.min.x);
        float x1 = Mathf.Min(lower.bounds.max.x, upper.bounds.max.x);
        float z0 = Mathf.Max(lower.bounds.min.z, upper.bounds.min.z);
        float z1 = Mathf.Min(lower.bounds.max.z, upper.bounds.max.z);
        if (x1 <= x0 || z1 <= z0) return Array.Empty<float>();
        var gaps = new List<float>();
        for (int xIndex = 0; xIndex < resolution; xIndex++)
        {
            float x = Mathf.Lerp(x0, x1, (xIndex + .5f) / resolution);
            for (int zIndex = 0; zIndex < resolution; zIndex++)
            {
                float z = Mathf.Lerp(z0, z1, (zIndex + .5f) / resolution);
                var a = MeshSurface(lower, x, z);
                var b = MeshSurface(upper, x, z);
                if (a.hit && b.hit) gaps.Add(b.min - a.max);
            }
        }
        return gaps.ToArray();
    }

    static ContactMesh ContactPart(string id, string label, string family, Vector3[] vertices, int[] triangles)
        => new ContactMesh { id = id, label = label, family = family, vertices = vertices,
            triangles = triangles, bounds = BoundsOf(vertices) };

    static string SupportLabel(string name)
        => name.Split(' ').FirstOrDefault(word => word == "O1" || word == "O4" || word == "P2" || word == "P3") ?? "";

    static bool HasLabel(ContactMesh part, string prefix) => part.label.StartsWith(prefix, StringComparison.Ordinal);

    static void WriteContacts(List<string> rows, string version, ContactMesh[] parts, float units)
    {
        void Record(string kind, ContactMesh lower, ContactMesh upper, float[] samples, bool designedJoint)
        {
            if (samples.Length == 0) return;
            var gaps = samples.Select(value => value * units).OrderBy(value => value).ToArray();
            int touching = gaps.Count(gap => Mathf.Abs(gap) <= ContactTolerance);
            string status = designedJoint ? (touching != 0 ? "PASS" : "FAIL") : "OBSERVED";
            rows.Add(FormattableString.Invariant(
                $"{version},{kind},{Quoted(lower.label)},{Quoted(upper.label)},{lower.id},{upper.id},{lower.bounds.center.z * units:F4},{gaps.Length},{touching},{gaps[0]:F5},{gaps[gaps.Length / 2]:F5},{gaps[^1]:F5},{ContactTolerance:F3},{status}"));
        }
        void Test(string kind, ContactMesh lower, IEnumerable<ContactMesh> uppers, bool designedJoint)
        {
            bool found = false;
            foreach (var upper in uppers)
            {
                var gaps = SampleInterface(lower, upper);
                if (gaps.Length == 0) continue;
                found = true;
                Record(kind, lower, upper, gaps, designedJoint);
            }
            if (!found && designedJoint)
                rows.Add(FormattableString.Invariant($"{version},{kind},{Quoted(lower.label)},,{lower.id},,{lower.bounds.center.z * units:F4},0,0,,,,{ContactTolerance:F3},FAIL"));
        }
        foreach (var plinth in parts.Where(part => HasLabel(part, "Bearing plinths")))
        {
            Test("T04-Plinth-Crossbeam", plinth, parts.Where(part => HasLabel(part, "Deck with crossbeams")), false);
            Test("T04-Plinth-Girder", plinth, parts.Where(part => HasLabel(part, "Girders")), false);
        }
        foreach (var stem in parts.Where(part => HasLabel(part, "Pier stem") || HasLabel(part, "Abutment stem")))
        {
            if (HasLabel(stem, "Abutment stem"))
                Test("T04-Abutment-Crossbeam", stem, parts.Where(part => HasLabel(part, "Deck with crossbeams")), false);
            foreach (var plinth in parts.Where(part => part.family == stem.family && HasLabel(part, "Bearing plinths")))
                Test("T04-Stem-Plinth", stem, new[] { plinth }, !HasLabel(stem, "Abutment stem"));
            foreach (var cap in parts.Where(part => part.family == stem.family && HasLabel(part, "Pile cap")))
            {
                if (HasLabel(stem, "Pier stem"))
                {
                    // The import deliberately omits the closing face under the pier stem.
                    // Its lower rim is covered by the cap, so an underside ray there hits the head.
                    var rim = stem.vertices.Where(point => point.y - stem.bounds.min.y <= .01f).Distinct().ToArray();
                    var gaps = rim.Select(point =>
                    {
                        var surface = MeshSurface(cap, point.x, point.z);
                        return surface.hit ? point.y - surface.max : float.PositiveInfinity;
                    }).ToArray();
                    if (gaps.Length == 0 || gaps.Any(gap => !float.IsFinite(gap) || Mathf.Abs(gap * units) > ContactTolerance))
                        rows.Add(FormattableString.Invariant($"{version},T04-Cap-Stem-Rim,{Quoted(cap.label)},{Quoted(stem.label)},{cap.id},{stem.id},{cap.bounds.center.z * units:F4},{gaps.Length},0,,,,{ContactTolerance:F3},FAIL"));
                    else Record("T04-Cap-Stem-Rim", cap, stem, gaps, true);
                }
                else Test("T04-Cap-Stem", cap, new[] { stem }, true);
                var bedding = parts.Where(part => part.family == stem.family && HasLabel(part, "Blinding concrete")).ToArray();
                foreach (var pile in parts.Where(part => part.family == stem.family && HasLabel(part, "Piles")))
                {
                    Test("T04-Pile-Cap", pile, new[] { cap }, !HasLabel(stem, "Pier stem"));
                    if (HasLabel(stem, "Pier stem")) Test("T04-Pile-Blinding", pile, bedding, true);
                }
                foreach (var concrete in bedding)
                    Test("T04-Blinding-Cap", concrete, new[] { cap }, true);
            }
        }
    }

    static string Quoted(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    static ContactMesh ReadContactMesh(Transform bridge, Transform node, string id)
    {
        var mesh = node.GetComponent<MeshFilter>().sharedMesh;
        var toCommon = bridge.worldToLocalMatrix * node.localToWorldMatrix;
        return ContactPart(id, node.name, node.parent.name,
            mesh.vertices.Select(toCommon.MultiplyPoint3x4).ToArray(), mesh.triangles);
    }

    static void AlignCandidateBearings(Transform bridge, Dictionary<Transform, string> occurrences, string output, int spans,
        Func<Mesh, string, Mesh> saveDerivedMesh)
    {
        var crossbeams = occurrences.Where(entry => entry.Key.name.StartsWith("Deck with crossbeams", StringComparison.Ordinal))
            .Select(entry => ReadContactMesh(bridge, entry.Key, entry.Value)).ToArray();
        string id = spans + "Spans";
        var contacts = new List<string> { "Version,TestID,Lower,Upper,LowerID,UpperID,LocalStation_m,Samples,WithinTolerance,MinimumGap_m,MedianGap_m,MaximumGap_m,Tolerance_m,Status" };
        var fitRows = new List<string> { "Occurrence,Support,MedianAdjustment_m" };
        bool fit = true;
        foreach (var group in bridge.Cast<Transform>())
        {
            var stem = group.Cast<Transform>().SingleOrDefault(node => node.name.StartsWith("Pier stem", StringComparison.Ordinal) ||
                node.name.StartsWith("Abutment stem", StringComparison.Ordinal));
            if (stem == null) continue;
            bool pier = stem.name.StartsWith("Pier stem", StringComparison.Ordinal);
            var stemMesh = ReadContactMesh(bridge, stem, occurrences[stem]);
            var plinths = group.Cast<Transform>().Where(node => node.name.StartsWith("Bearing plinths", StringComparison.Ordinal)).ToArray();
            var headOffsets = new List<float>();
            foreach (var plinth in plinths)
            {
                var original = ReadContactMesh(bridge, plinth, occurrences[plinth]);
                var interfaces = crossbeams.Select(beam => (beam, gaps: SampleInterface(original, beam)))
                    .Where(item => item.gaps.Length > 5 && item.gaps.Min() > -.15f)
                    .OrderBy(item => item.gaps.Min()).ToArray();
                if (interfaces.Length == 0)
                {
                    fit = false;
                    contacts.Add(FormattableString.Invariant($"{id},T04-Bearing-Fit,{Quoted(plinth.name)},,{occurrences[plinth]},,{original.bounds.center.z * ScaleFactor:F4},0,0,,,,{ContactTolerance:F3},FAIL"));
                    continue;
                }
                var selected = interfaces[0];
                var distances = selected.gaps.OrderBy(value => value).ToArray();
                float shift = distances[distances.Length / 2];
                if (Mathf.Abs(shift) > .5f)
                    throw new InvalidOperationException("Unmatched bearing interface: " + occurrences[plinth]);
                var stemGap = SampleInterface(stemMesh, original).OrderBy(value => value).ToArray();
                if (pier && stemGap.Length == 0)
                    throw new InvalidOperationException("No head surface under " + occurrences[plinth]);
                if (pier) headOffsets.Add(shift + stemGap[stemGap.Length / 2]);
                // Preserve the original joint between the abutment and its bearing seat.
                // Extend the native plinth, keeping its bottom face fixed on that seat.
                if (pier) plinth.localPosition += Vector3.up * shift;
                else if (shift > 0f)
                    AdjustBearingShape(bridge, plinth, original, shift, false, output, saveDerivedMesh);
                var placed = ReadContactMesh(bridge, plinth, occurrences[plinth]);
                var final = SampleInterface(placed, selected.beam).Select(value => value * ScaleFactor).OrderBy(value => value).ToArray();
                int covered = final.Count(value => Mathf.Abs(value) <= ContactTolerance);
                bool passed = final.Length > 0 && covered == final.Length;
                fit &= passed;
                if (final.Length != 0)
                    contacts.Add(FormattableString.Invariant($"{id},T04-Bearing-Fit,{Quoted(plinth.name)},{Quoted(selected.beam.label)},{occurrences[plinth]},{selected.beam.id},{placed.bounds.center.z * ScaleFactor:F4},{final.Length},{covered},{final[0]:F5},{final[final.Length/2]:F5},{final[^1]:F5},{ContactTolerance:F3},{(passed ? "PASS" : "FAIL")}"));
                fitRows.Add(FormattableString.Invariant($"{occurrences[plinth]},{group.name},{shift * ScaleFactor:F5}"));
            }
            if (pier && headOffsets.Count == plinths.Length && headOffsets.Count != 0)
            {
                float shift = headOffsets.Average();
                AdjustBearingShape(bridge, stem, stemMesh, shift, true, output, saveDerivedMesh);
                fitRows.Add(FormattableString.Invariant($"{occurrences[stem]},{group.name},{shift * ScaleFactor:F5}"));
            }
        }
        var parts = occurrences.Select(entry => ReadContactMesh(bridge, entry.Key, entry.Value)).ToArray();
        WriteContacts(contacts, id, parts, ScaleFactor);
        ValidateDeckJoints(contacts, id, parts);
        File.WriteAllLines(FinalReports + id + "_Interfaces.csv", contacts);
        File.WriteAllLines(FinalReports + id + "_Adjustments.csv", fitRows);
        var manifest = new List<string> { "Occurrence,Part,Group,MeshPath,CenterX_m,CenterY_m,CenterZ_m" };
        foreach (var entry in occurrences)
        {
            var item = parts.Single(part => part.id == entry.Value);
            Vector3 center = item.bounds.center * ScaleFactor;
            string meshPath = AssetDatabase.GetAssetPath(entry.Key.GetComponent<MeshFilter>().sharedMesh);
            manifest.Add(FormattableString.Invariant($"{entry.Value},{Quoted(item.label)},{item.family},{Quoted(meshPath)},{center.x:F5},{center.y:F5},{center.z:F5}"));
        }
        File.WriteAllLines(FinalReports + id + "_Manifest.csv", manifest);
        Debug.Log($"SO 201 {spans} spans: {parts.Length} occurrences, bearing face fit {(fit ? "PASS" : "FAIL")}.");
    }

    static void AdjustBearingShape(Transform bridge, Transform node, ContactMesh shape, float shift, bool pier, string output,
        Func<Mesh, string, Mesh> saveDerivedMesh)
    {
        var filter = node.GetComponent<MeshFilter>();
        var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
        var fromCommon = node.worldToLocalMatrix * bridge.localToWorldMatrix;
        if (pier && Mathf.Abs(shift) > .65f) throw new InvalidOperationException("Excessive shaft correction: " + node.name);
        float bottom = shape.bounds.min.y + (pier ? shape.bounds.size.y * .20f : 0f);
        float top = shape.bounds.min.y + (pier ? shape.bounds.size.y * .64f : shape.bounds.size.y);
        mesh.vertices = shape.vertices.Select(point =>
        {
            point.y += shift * Mathf.InverseLerp(bottom, top, point.y);
            return fromCommon.MultiplyPoint3x4(point);
        }).ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.name = node.parent.name + "-" + node.name.Replace(' ', '_');
        filter.sharedMesh = saveDerivedMesh(mesh, output + "/" + mesh.name + ".asset");
    }

    static void ValidateDeckJoints(List<string> rows, string version, ContactMesh[] parts)
    {
        var sections = parts.Where(part => part.label == "Pavement").OrderBy(part => part.bounds.center.z).ToArray();
        for (int index = 1; index < sections.Length; index++)
        {
            var left = sections[index - 1];
            var right = sections[index];
            float seam = (left.bounds.max.z + right.bounds.min.z) * .5f;
            float distance = (right.bounds.min.z - left.bounds.max.z) * ScaleFactor;
            Bounds deck = left.bounds;
            var delta = new List<float>();
            foreach (float offset in new[] { -4.1f, -2.3f, 0f, 2.3f, 4.1f })
            {
                var a = MeshSurface(left, deck.center.x + offset / ScaleFactor, left.bounds.max.z - .003f);
                var b = MeshSurface(right, deck.center.x + offset / ScaleFactor, right.bounds.min.z + .003f);
                if (a.hit && b.hit) delta.Add((b.max - a.max) * ScaleFactor);
            }
            delta.Sort();
            const float deckTolerance = .025f;
            int touching = delta.Count(value => Mathf.Abs(value) <= deckTolerance);
            bool pass = touching == 5 && Mathf.Abs(distance) <= deckTolerance;
            rows.Add(FormattableString.Invariant($"{version},T03-Deck-Vertical,{Quoted(left.label)},{Quoted(right.label)},{left.id},{right.id},{seam * ScaleFactor:F4},{delta.Count},{touching},{(delta.Count == 0 ? float.NaN : delta[0]):F5},,{(delta.Count == 0 ? float.NaN : delta[^1]):F5},{deckTolerance:F3},{(pass ? "PASS" : "FAIL")}"));
            rows.Add(FormattableString.Invariant($"{version},T03-Deck-Longitudinal,{Quoted(left.label)},{Quoted(right.label)},{left.id},{right.id},{seam * ScaleFactor:F4},1,{(pass ? 1 : 0)},{distance:F5},,,{deckTolerance:F3},{(Mathf.Abs(distance) <= deckTolerance ? "PASS" : "FAIL")}"));
        }
    }

    static void AuditFinalizationBaseline()
    {
        Directory.CreateDirectory(FinalReports);
        var prototype = Open(ExtendedRoot + "/SO201_Extended_Prototype.unity");
        var zone = Open(ZoneRoot + "Zone_018.unity");
        var pilot = PrefabUtility.LoadPrefabContents(Pilot);
        try
        {
            var parts = ReadParts(pilot);
            var source = parts.Select(part => ContactPart(part.entry.globalId, part.entry.Label,
                SupportLabel(part.entry.Label), part.vertices, part.indices)).ToArray();
            var ids = pilot.GetComponentsInChildren<BridgeBimIdentity>(true).ToDictionary(item => item.GlobalId);
            var inverse = Quaternion.Inverse(Quaternion.LookRotation(Forward));
            var data = new GeometryInventory
            {
                sourcePath = Dae,
                sourceSha256 = BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(Dae))).Replace("-", ""),
                worldScale = ScaleFactor,
                elements = parts.OrderBy(part => part.entry.globalId).Select(part =>
                {
                    var renderer = ids[part.entry.globalId].Renderers.Single();
                    var t = renderer.transform;
                    return new GeometryAsset
                    {
                        globalId = part.entry.globalId, parentId = part.entry.parentGlobalId, ifcClass = part.entry.ifcClass,
                        label = part.entry.Label, mesh = Dae, meshLocalId = part.meshId.ToString(),
                        materials = renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray(),
                        localTransform = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale),
                        bridgeTransform = Matrix4x4.Rotate(inverse) * t.localToWorldMatrix,
                        bridgeBounds = BoundsOf(part.vertices),
                        vertices = part.vertices,
                        triangles = part.indices
                    };
                }).ToArray()
            };
            File.WriteAllText(FinalReports + "SO201_SourceGeometry.json", JsonUtility.ToJson(data));

            var productionRoot = Root(zone).Find("FixedInfrastructure_Bridge/" + BridgeObject);
            var byMesh = parts.ToDictionary(part => part.meshId);
            var production = productionRoot.GetComponentsInChildren<MeshFilter>().Select(filter =>
            {
                var mesh = filter.sharedMesh;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string _, out long meshId);
                var part = byMesh[meshId];
                var toCommon = Matrix4x4.Rotate(inverse) * productionRoot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                return ContactPart(part.entry.globalId, part.entry.Label, SupportLabel(part.entry.Label),
                    mesh.vertices.Select(toCommon.MultiplyPoint3x4).ToArray(), mesh.triangles);
            }).ToArray();
            var previewRoot = prototype.GetRootGameObjects().Single(root => root.name.StartsWith("SO 201 |", StringComparison.Ordinal)).transform;
            var preview = previewRoot.GetComponentsInChildren<MeshFilter>().Select(filter =>
            {
                var mesh = filter.sharedMesh;
                var group = filter.transform.parent.name;
                var toCommon = previewRoot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                return ContactPart(group + "/" + filter.name, filter.name, group,
                    mesh.vertices.Select(toCommon.MultiplyPoint3x4).ToArray(), mesh.triangles);
            }).ToArray();
            var rows = new List<string> { "Version,TestID,Lower,Upper,LowerID,UpperID,LocalStation_m,Samples,WithinTolerance,MinimumGap_m,MedianGap_m,MaximumGap_m,Tolerance_m,Status" };
            WriteContacts(rows, "Source", source, ScaleFactor);
            WriteContacts(rows, "Production214", production, productionRoot.lossyScale.y);
            WriteContacts(rows, "Prototype5Before", preview, previewRoot.lossyScale.y);
            File.WriteAllLines(FinalReports + "Geometry_Test_Results.csv", rows);
            Debug.Log($"SO 201 triangle baseline: {rows.Count - 1} interface rows, {production.Length} production meshes, {preview.Length} prototype occurrences.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(pilot);
            foreach (var scene in ownedScenes) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            ownedScenes.Clear();
        }
    }
}
