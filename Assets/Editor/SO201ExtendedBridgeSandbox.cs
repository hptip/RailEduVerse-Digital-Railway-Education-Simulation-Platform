using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UttVr.BridgePilot;

static partial class SO201OriginalBridgeAudit
{
    const string ExtendedRoot = "Assets/ExternalBIM/SO201Extended";
    const string ExtendedReports = "Documentation/BIMExpansion/SO201ExtendedBridgeAndTrainCamera/";

    static void CreateExtendedSandbox() => CreateExtendedSandbox(3, true, false);

    static void CreateExtendedSandbox(int middleSpans, bool shortEnds, bool alignBearings)
    {
        int repeats = middleSpans - 1;
        int spanCount = middleSpans + (shortEnds ? 2 : 0);
        string output = alignBearings ? ExtendedRoot + "/Finalization" : ExtendedRoot;
        string scenePath = output + (alignBearings ? $"/SO201_{spanCount}_Spans.unity" : "/SO201_Extended_Prototype.unity");
        string pictures = alignBearings ? FinalReports + $"Screenshots/{spanCount}_Spans/" : ExtendedReports + "Before_After_Screenshots/";
        Directory.CreateDirectory(pictures);
        Directory.CreateDirectory(output);
        AssetDatabase.Refresh();
        if (SceneManager.GetSceneByPath(scenePath).isLoaded)
            throw new InvalidOperationException("Close the prototype scene before rebuilding it.");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var pilot = PrefabUtility.LoadPrefabContents(Pilot);
        try
        {
            var parts = ReadParts(pilot);
            Part Stem(string label) => parts.Single(part => part.entry.Label == label);
            float a = BoundsOf(Stem("Pier stem P2").vertices).center.z;
            float b = BoundsOf(Stem("Pier stem P3").vertices).center.z;
            float pitch = b - a;
            if (pitch < 30f || pitch > 38f)
                throw new InvalidOperationException("Unexpected SO 201 middle-span support spacing: " + pitch);
            var pavement = parts.Single(part => part.entry.ifcClass == "IfcPavement");
            var bounds = BoundsOf(pavement.vertices);
            float endA = PavementY(pavement, bounds.center.x, a + .005f);
            float endB = PavementY(pavement, bounds.center.x, b - .005f);
            if (!float.IsFinite(endA) || !float.IsFinite(endB))
                throw new InvalidOperationException("The pavement at a repeat boundary could not be measured.");
            float grade = (endB - endA) / (pitch - .01f);
            var identities = pilot.GetComponentsInChildren<BridgeBimIdentity>(true).ToDictionary(identity => identity.GlobalId);
            var inverse = Quaternion.Inverse(Quaternion.LookRotation(Forward));
            var map = new List<string> { "OccurrenceID,SourceGlobalId,SourceMeshLocalID,Part,Representation,Triangles" };
            var placements = new List<(string id, string label, Bounds limits)>();
            var assembly = new GameObject("SO 201 | native span prototype");
            SceneManager.MoveGameObjectToScene(assembly, scene);
            assembly.transform.localScale = Vector3.one * ScaleFactor;
            float length = (shortEnds ? bounds.size.z + repeats * pitch : middleSpans * pitch) * ScaleFactor;
            float centerZ = shortEnds ? bounds.center.z + repeats * pitch / 2f : a + middleSpans * pitch / 2f;
            assembly.transform.position = new Vector3(-bounds.center.x, -bounds.max.y, -centerZ) * ScaleFactor;
            Transform Group(string label)
            {
                var group = new GameObject(label).transform;
                group.SetParent(assembly.transform, false);
                return group;
            }

            var sections = new Dictionary<(long, float, float), Mesh>();
            var occurrenceByNode = new Dictionary<Transform, string>();
            Mesh SaveDerivedMesh(Mesh mesh, string file)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(file);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(mesh, file);
                    return mesh;
                }
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                return existing;
            }
            void Add(Part part, Transform group, float offset, float low = float.NegativeInfinity, float high = float.PositiveInfinity,
                bool levelSection = false, float verticalOffset = 0f)
            {
                var renderer = identities[part.entry.globalId].Renderers.Single();
                var source = renderer.GetComponent<MeshFilter>().sharedMesh;
                var id = group.name + "-" + part.entry.globalId;
                var partBounds = BoundsOf(part.vertices);
                if (partBounds.max.z < low || partBounds.min.z > high) return;
                Mesh mesh = source;
                bool clipped = partBounds.min.z < low - .001f || partBounds.max.z > high + .001f;
                bool derived = clipped || levelSection;
                if (derived)
                {
                    var key = (part.meshId, low, high);
                    if (!sections.TryGetValue(key, out mesh))
                    {
                        mesh = ClipExtendedSection(source, part.vertices, inverse * renderer.transform.rotation, low, high,
                            levelSection ? grade : 0f, a, b);
                        if (mesh != null)
                        {
                            mesh.name = id;
                            mesh = SaveDerivedMesh(mesh, output + "/" + id + ".asset");
                        }
                        sections.Add(key, mesh);
                    }
                    if (mesh == null) return;
                }
                var node = new GameObject(part.entry.Label, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                occurrenceByNode.Add(node, id);
                node.SetParent(group, false);
                node.localPosition = (derived ? Vector3.zero : inverse * renderer.transform.position) + new Vector3(0f, verticalOffset, offset);
                node.localRotation = derived ? Quaternion.identity : inverse * renderer.transform.rotation;
                node.localScale = derived ? Vector3.one : renderer.transform.lossyScale;
                node.GetComponent<MeshFilter>().sharedMesh = mesh;
                node.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                var placed = derived ? mesh.bounds : partBounds;
                placed.center += new Vector3(0f, verticalOffset, offset);
                placements.Add((id, part.entry.Label, placed));
                map.Add($"\"{id}\",{part.entry.globalId},{part.meshId},\"{part.entry.Label.Replace("\"", "\"\"")}\",{(derived ? "Derived from SO 201" : "Native SO 201")},{mesh.triangles.Length / 3}");
            }

            bool Support(Part part, string support) =>
                part.entry.Label.Split(' ').Contains(support);
            var superstructure = parts.Where(part => !new[] { "O1", "P2", "P3", "O4" }.Any(support => Support(part, support))).ToArray();
            if (shortEnds)
            {
                var inlet = Group("S01");
                foreach (var part in superstructure) Add(part, inlet, 0, high: a);
            }
            for (int i = 0; i <= repeats; i++)
            {
                var middle = Group($"S{i + 2:D2}");
                foreach (var part in superstructure) Add(part, middle, i * pitch, a, b, levelSection: true);
                var pier = Group($"P{i + 1:D2}");
                foreach (var part in parts.Where(part => Support(part, "P2")))
                    Add(part, pier, i * pitch);
            }
            if (shortEnds)
            {
                var outlet = Group($"S{repeats + 3:D2}");
                foreach (var part in superstructure) Add(part, outlet, repeats * pitch, b, verticalOffset: -grade * pitch);
            }
            foreach (var label in shortEnds ? new[] { "O1", "P3", "O4" } : new[] { "P3" })
            {
                var support = Group(label);
                foreach (var part in parts.Where(part => Support(part, label) && part.entry.Label != "Approach slab O1"))
                    Add(part, support, label == "O1" ? 0 : repeats * pitch, verticalOffset: label == "O1" ? 0f : -grade * pitch);
                // The IFC occurrence named O1 contains disconnected geometry at both banks.
                // Move each section with its own abutment.
                if (label == "O1")
                    Add(Stem("Approach slab O1"), support, 0f, high: a);
                if (label == "O4")
                    Add(Stem("Approach slab O1"), support, repeats * pitch, low: b, verticalOffset: -grade * pitch);
            }

            if (alignBearings)
            {
                AlignCandidateBearings(assembly.transform, occurrenceByNode, output, spanCount, SaveDerivedMesh);
                Vector3 focus = new Vector3(0f, -9f, 0f);
                RenderSandbox(scene, focus, Vector3.right, focus - Vector3.forward * length / 2f,
                    focus + Vector3.forward * length / 2f, pictures, length * .81f);
                if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new IOException(scenePath);
                AssetDatabase.SaveAssets();
                return;
            }

            float deckSeam = 0;
            float originalSeam = 0;
            int missing = 0;
            var samples = new List<string> { "LocalX_m,RawHeightDelta_m,LevelledHeightDelta_m" };
            foreach (float x in new[] { -4.1f, -2.3f, 0f, 2.3f, 4.1f })
            {
                float localX = bounds.center.x + x / ScaleFactor;
                float before = PavementY(pavement, localX, b - .005f);
                float after = PavementY(pavement, localX, a + .005f);
                if (float.IsNegativeInfinity(before) || float.IsNegativeInfinity(after)) missing++;
                else
                {
                    float delta = (before - after) * ScaleFactor;
                    float residual = delta - grade * (pitch - .01f) * ScaleFactor;
                    originalSeam = Mathf.Max(originalSeam, Mathf.Abs(delta));
                    deckSeam = Mathf.Max(deckSeam, Mathf.Abs(residual));
                    samples.Add(FormattableString.Invariant($"{x:F3},{delta:F5},{residual:F5}"));
                }
            }
            File.WriteAllLines(ExtendedReports + "Occurrence_Source_Mapping.csv", map);
            float span = (bounds.size.z + repeats * pitch) * ScaleFactor;
            float full = (bounds.size.z + 4 * pitch) * ScaleFactor;
            float pierTopMismatch = Mathf.Abs(BoundsOf(Stem("Pier stem P2").vertices).max.y -
                BoundsOf(Stem("Pier stem P3").vertices).max.y) * ScaleFactor;
            var message = FormattableString.Invariant(
                $"SO 201 source: {parts.Length} distinct elements. Prototype: {repeats + 3} spans, {map.Count - 1} occurrences, {span:F3} m of deck. Seven-span deck projection: {full:F3} m. Middle support spacing: {pitch * ScaleFactor:F3} m. Repeated deck joint before correction: {originalSeam:F3} m. After correcting the longitudinal grade: {deckSeam:F3} m residual; {missing} of 5 lateral measurement pairs missing. Native P2/P3 stem top elevation difference: {pierTopMismatch:F3} m. Placement gate: requires verification of all girder/bearing contacts and the route alignment.");
            File.WriteAllText(ExtendedReports + "Prototype_Measurements.txt", message);
            File.WriteAllLines(ExtendedReports + "Deck_Joint_Samples.csv", samples);
            var contacts = new List<string> { "PlinthOccurrence,GirderOccurrence,HorizontalOverlapX_m,HorizontalOverlapZ_m,GirderBoundsBottomMinusPlinthTop_m" };
            var girders = placements.Where(item => item.label.StartsWith("Girders", StringComparison.Ordinal)).ToArray();
            foreach (var plinth in placements.Where(item => item.label.StartsWith("Bearing plinths", StringComparison.Ordinal)))
            {
                bool found = false;
                foreach (var girder in girders)
                {
                    float overlapX = Mathf.Min(plinth.limits.max.x, girder.limits.max.x) - Mathf.Max(plinth.limits.min.x, girder.limits.min.x);
                    float overlapZ = Mathf.Min(plinth.limits.max.z, girder.limits.max.z) - Mathf.Max(plinth.limits.min.z, girder.limits.min.z);
                    if (overlapX <= 0f || overlapZ <= 0f) continue;
                    found = true;
                    float height = (girder.limits.min.y - plinth.limits.max.y) * ScaleFactor;
                    contacts.Add(FormattableString.Invariant($"{plinth.id},{girder.id},{overlapX * ScaleFactor:F4},{overlapZ * ScaleFactor:F4},{height:F4}"));
                }
                if (!found) contacts.Add($"{plinth.id},UNMATCHED,,,");
            }
            File.WriteAllLines(ExtendedReports + "Bearing_Bounds_Audit.csv", contacts);
            WriteExtendedRouteAlignment(full, bounds.size.x * ScaleFactor, a, b, parts);
            Vector3 center = new Vector3(0f, -9f, 0f);
            RenderSandbox(scene, center, Vector3.right, center - Vector3.forward * span / 2f,
                center + Vector3.forward * span / 2f, pictures, span * .81f);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log(message);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(pilot);
            if (previous.IsValid() && previous != scene) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void WriteExtendedRouteAlignment(float deckLength, float deckWidth, float supportA, float supportB, Part[] parts)
    {
        var route = SceneManager.GetSceneByPath(Route);
        bool opened = !route.isLoaded;
        if (opened) route = EditorSceneManager.OpenScene(Route, OpenSceneMode.Additive);
        try
        {
            var manager = route.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var spline = manager.trainMover.splineRoute;
            float length = spline.CalculateLength();
            Vector3 Center(float station)
            {
                float t = station / length;
                Vector3 side = Vector3.Cross(Vector3.up, (Vector3)spline.EvaluateTangent(t)).normalized;
                return (Vector3)spline.EvaluatePosition(t) - side * Spacing * .5f;
            }
            const float corridorStart = 3650.0015f, corridorEnd = 4770.0015f;
            float bestStart = 0f, bestDrift = float.PositiveInfinity;
            for (float start = corridorStart; start + deckLength <= corridorEnd; start += 2f)
            {
                Vector3 first = Center(start);
                Vector3 side = Vector3.Cross(Vector3.up, (Center(start + deckLength) - first).normalized);
                float drift = 0f;
                for (float offset = 0; offset < deckLength; offset += 1f)
                    drift = Mathf.Max(drift, Mathf.Abs(Vector3.Dot(side, Center(start + offset) - first)));
                if (drift >= bestDrift) continue;
                bestStart = start;
                bestDrift = drift;
            }
            var train = TrainBounds(manager.trainMover);
            float trainHalfWidth = Mathf.Max(Mathf.Abs(train.min.x), Mathf.Abs(train.max.x));
            float margin = deckWidth * .5f - Spacing * .5f - trainHalfWidth - bestDrift;
            File.WriteAllText(ExtendedReports + "Railway_Compatibility_Report.md", FormattableString.Invariant(
                $"# Tương thích với tuyến hiện hữu\n\nCửa sổ tim tuyến được lấy mẫu mỗi 1 m. Phương án dây cung thẳng tốt nhất trong phạm vi cầu cũ là chainage {bestStart:F3}–{bestStart + deckLength:F3} m, lệch ngang tối đa {bestDrift:F3} m. Theo chiều rộng bao mặt cầu {deckWidth:F3} m và tim đường 6,400022 m, bao ngang của tàu còn {margin:F3} m đến mép bounds. Đây là phép kiểm tra bao hình học. Mép sàn hữu dụng, gờ biên, gối, đáy ray và địa hình cần được đo riêng tại ứng viên đã căn chỉnh.\n"));
            var inventory = new List<string> { "GlobalId,SourceMeshLocalID,Name,MinX_m,MaxX_m,MinY_m,MaxY_m,MinZ_m,MaxZ_m,IntersectsNativeMiddleSpan" };
            foreach (var part in parts.OrderBy(part => part.entry.globalId))
            {
                var limits = BoundsOf(part.vertices);
                inventory.Add(FormattableString.Invariant($"{part.entry.globalId},{part.meshId},\"{part.entry.Label.Replace("\"", "\"\"")}\",{limits.min.x:F4},{limits.max.x:F4},{limits.min.y:F4},{limits.max.y:F4},{limits.min.z:F4},{limits.max.z:F4},{limits.max.z > supportA && limits.min.z < supportB}"));
            }
            File.WriteAllLines(ExtendedReports + "SO201_Component_Envelope.csv", inventory);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(route, true);
        }
    }

    readonly struct ClipVertex
    {
        public readonly Vector3 position, normal;
        public readonly Vector2 uv;
        public ClipVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            this.position = position;
            this.normal = normal;
            this.uv = uv;
        }
        public static ClipVertex AtPlane(ClipVertex a, ClipVertex b, float z)
        {
            float t = (z - a.position.z) / (b.position.z - a.position.z);
            return new ClipVertex(Vector3.LerpUnclamped(a.position, b.position, t),
                Vector3.LerpUnclamped(a.normal, b.normal, t).normalized, Vector2.LerpUnclamped(a.uv, b.uv, t));
        }
    }

    static Mesh ClipExtendedSection(Mesh source, Vector3[] positions, Quaternion rotation, float lo, float hi,
        float grade, float gradeStart, float gradeEnd)
    {
        var normals = source.normals;
        var uv = source.uv;
        var vertices = new List<Vector3>();
        var outputNormals = new List<Vector3>();
        var outputUv = new List<Vector2>();
        var indices = new List<int>[source.subMeshCount];
        var polygon = new List<ClipVertex>(6);
        var section = new List<ClipVertex>(6);
        void Cut(float plane, bool keepGreater)
        {
            if (float.IsInfinity(plane) || polygon.Count == 0) return;
            section.Clear();
            var previous = polygon[polygon.Count - 1];
            bool previousInside = keepGreater ? previous.position.z >= plane : previous.position.z <= plane;
            foreach (var vertex in polygon)
            {
                bool inside = keepGreater ? vertex.position.z >= plane : vertex.position.z <= plane;
                if (inside != previousInside) section.Add(ClipVertex.AtPlane(previous, vertex, plane));
                if (inside) section.Add(vertex);
                previous = vertex;
                previousInside = inside;
            }
            polygon.Clear();
            polygon.AddRange(section);
        }
        for (int submesh = 0; submesh < indices.Length; submesh++)
        {
            indices[submesh] = new List<int>();
            var triangles = source.GetTriangles(submesh);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                polygon.Clear();
                for (int j = 0; j < 3; j++)
                {
                    int sourceIndex = triangles[i + j];
                    polygon.Add(new ClipVertex(positions[sourceIndex],
                        normals.Length == positions.Length ? rotation * normals[sourceIndex] : Vector3.up,
                        uv.Length == positions.Length ? uv[sourceIndex] : Vector2.zero));
                }
                Cut(lo, true);
                Cut(hi, false);
                if (polygon.Count < 3) continue;
                int first = vertices.Count;
                foreach (var vertex in polygon)
                {
                    Vector3 position = vertex.position;
                    position.y -= grade * (Mathf.Clamp(position.z, gradeStart, gradeEnd) - gradeStart);
                    var normal = vertex.normal;
                    if (position.z > gradeStart && position.z < gradeEnd) normal.z += grade * normal.y;
                    vertices.Add(position);
                    outputNormals.Add(normal.normalized);
                    outputUv.Add(vertex.uv);
                }
                for (int k = 1; k + 1 < polygon.Count; k++)
                {
                    indices[submesh].Add(first);
                    indices[submesh].Add(first + k);
                    indices[submesh].Add(first + k + 1);
                }
            }
        }
        if (vertices.Count == 0) return null;
        var mesh = new Mesh
        {
            indexFormat = vertices.Count > ushort.MaxValue ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            subMeshCount = source.subMeshCount
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(outputNormals);
        mesh.SetUVs(0, outputUv);
        for (int i = 0; i < indices.Length; i++) mesh.SetTriangles(indices[i], i);
        mesh.RecalculateBounds();
        return mesh;
    }
}
