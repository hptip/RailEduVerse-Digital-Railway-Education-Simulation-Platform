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
using Object = UnityEngine.Object;

static partial class SO201OriginalBridgeAudit
{
    const string FinalAssets = ExtendedRoot + "/Finalization";
    const string FinalBridgeObject = "Bridge_SO201_SevenSpans";
    const string FinalStageScene = FinalAssets + "/SO201_Route_Validation.unity";

    static void FinalizeExtendedBridge(bool install)
    {
        string validated = FinalReports + "Route_Geometry_Test.csv";
        if (!File.Exists(FinalReports + "7Spans_Interfaces.csv") ||
            File.ReadLines(FinalReports + "7Spans_Interfaces.csv").Any(line => line.EndsWith(",FAIL", StringComparison.Ordinal)))
            throw new InvalidOperationException("The seven-span candidate has failed its assembly checks.");
        if (install && (!File.Exists(validated) || File.ReadLines(validated).Any(line => line.EndsWith(",FAIL", StringComparison.Ordinal))))
            throw new InvalidOperationException("The placed candidate has failed its route geometry checks.");

        var previous = SceneManager.GetActiveScene();
        Scene stage = default;
        try
        {
            var route = Open(Route);
            var candidate = Open(FinalAssets + "/SO201_7_Spans.unity");
            var zones = Enumerable.Range(17, 3).ToDictionary(n => $"Zone_{n:D3}", n => Open(ZoneRoot + $"Zone_{n:D3}.unity"));
            var manager = route.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var chunks = route.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RailwayMicroChunkStreamer>(true)).Single().data.chunks;
            var spline = manager.trainMover.splineRoute;
            float routeLength = spline.CalculateLength();
            Vector3 Side(float d) => Vector3.Cross(Vector3.up, (Vector3)spline.EvaluateTangent(d / routeLength)).normalized;
            Vector3 Center(float d) => (Vector3)spline.EvaluatePosition(d / routeLength) - Side(d) * Spacing * .5f;

            var template = candidate.GetRootGameObjects().Single(node => node.name.StartsWith("SO 201 |", StringComparison.Ordinal));
            var filters = template.GetComponentsInChildren<MeshFilter>();
            var native = AssetDatabase.LoadAssetAtPath<GameObject>(Pilot).GetComponent<BridgeBimAdapter>();
            var originalCatalog = native.catalog;
            var originalParts = originalCatalog.entries.Where(entry => entry.IsElement).ToDictionary(entry => entry.Label);
            var shapes = filters.ToDictionary(filter => filter, filter =>
                ReadContactMesh(template.transform, filter.transform, filter.transform.parent.name + "-" + originalParts[filter.name].globalId));
            var pavement = shapes.Values.Where(shape => shape.label == "Pavement").ToArray();
            var deck = BoundsOf(pavement.SelectMany(shape => shape.vertices));
            var caps = shapes.Values.Where(shape => HasLabel(shape, "Pile cap")).OrderBy(shape => shape.bounds.center.z).ToArray();
            float start = 3694.0015f;
            float end = start + deck.size.z * ScaleFactor;
            float center = (start + end) * .5f;
            const float corridorStart = 3650.0015f, corridorEnd = 4770.0015f;
            var relevant = manager.zones.Where(zone => zones.ContainsKey(zone.zoneId)).ToArray();
            var sampleChunks = chunks.Where(chunk => chunk.end >= corridorStart - 10f && chunk.start <= corridorEnd + 10f).ToArray();
            float TrackBottom(RailwayMicroChunkData.Track track)
            {
                var mesh = Resources.Load<Mesh>(track.meshResource);
                return Corners(mesh.bounds).Min(v => (track.position + track.rotation * Vector3.Scale(v, track.scale)).y);
            }
            float railBottom = sampleChunks.SelectMany(chunk => new[] { chunk.left, chunk.right }).Min(TrackBottom);
            var pose = Quaternion.LookRotation(Vector3.ProjectOnPlane(Center(end) - Center(start), Vector3.up));
            var midpoint = (Center(start) + Center(end)) * .5f;
            midpoint.y = railBottom - deck.max.y * ScaleFactor - .025f;
            var pivot = midpoint - pose * new Vector3(deck.center.x, 0, deck.center.z) * ScaleFactor;
            var inverse = Quaternion.Inverse(pose);
            Vector3 Local(Vector3 world) => inverse * (world - pivot) / ScaleFactor;
            Vector3 World(Vector3 local) => pivot + pose * (local * ScaleFactor);
            float WorldHeight(float y) => pivot.y + y * ScaleFactor;
            float DeckAt(Vector3 point)
            {
                var local = Local(point);
                return WorldHeight(pavement.Max(shape => MeshSurface(shape, local.x, local.z).max));
            }

            string workspace = FinalAssets + "/Placed";
            Directory.CreateDirectory(workspace);
            AssetDatabase.Refresh();
            Mesh SaveBridgeMesh(Mesh mesh, string name)
            {
                string path = workspace + "/" + name + ".asset";
                var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (old == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
                EditorUtility.CopySerialized(mesh, old);
                Object.DestroyImmediate(mesh);
                return old;
            }

            var terrainRoots = zones.ToDictionary(pair => pair.Key, pair => Root(pair.Value).Find("Terrain" + pair.Key));
            var stagedTerrain = new Dictionary<string, Mesh>();
            var entries = caps.Select(cap => (z: cap.bounds.center.z * ScaleFactor, half: cap.bounds.extents.z * ScaleFactor + 6,
                y: WorldHeight(cap.bounds.min.y))).ToArray();
            float wetStart = entries[1].z - entries[1].half - 2f, wetEnd = entries[^2].z + entries[^2].half + 2f;
            float wetCenter = (wetStart + wetEnd) * .5f;
            float RiverEnvelope(float flow) => Mathf.Lerp(1f, .08f, Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(83f, 132f, Mathf.Abs(flow))));
            float firstDryEdge = entries[0].z + entries[0].half;
            float lastDryEdge = entries[^1].z - entries[^1].half;
            float waterY = caps.Skip(1).Take(caps.Length - 2).Max(cap => WorldHeight(cap.bounds.max.y)) + 3.1f;
            float Profile(float z)
            {
                for (int i = 1; i < entries.Length; i++)
                {
                    float left = entries[i-1].z + entries[i-1].half;
                    float right = entries[i].z - entries[i].half;
                    if (z <= right) return Mathf.SmoothStep(entries[i-1].y, entries[i].y, Mathf.InverseLerp(left, right, z));
                }
                return entries[^1].y;
            }
            float BedWeight(Vector3 local)
            {
                float x = local.x * ScaleFactor - deck.center.x * ScaleFactor;
                float z = local.z * ScaleFactor;
                float outer = Mathf.Max(Mathf.Abs(x) - 36f, entries[0].z - entries[0].half - z, z - entries[^1].z - entries[^1].half);
                // River runs across the route. Both adjacent terrain tiles use the same world-space profile.
                float width = (wetEnd - wetStart) * .5f * RiverEnvelope(x);
                float river = Mathf.Max(Mathf.Abs(x) - 133f, Mathf.Abs(z - wetCenter) - width - 3f);
                return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(outer, river) / 19f));
            }
            foreach (var pair in terrainRoots)
            {
                string suffix = pair.Key.Substring(5);
                var root = pair.Value ?? throw new InvalidOperationException("Missing terrain " + pair.Key);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/ProjectLopHoc/Generated/QuestRailway/TerrainMeshes/Terrain_{suffix}.asset");
                var copy = Object.Instantiate(mesh);
                var vertices = copy.vertices;
                var colors = copy.colors;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var p = root.TransformPoint(vertices[i]);
                    var local = Local(p);
                    float weight = BedWeight(local);
                    if (weight <= 0f) continue;
                    p.y = Mathf.Lerp(p.y, Profile(local.z * ScaleFactor), weight);
                    vertices[i] = root.InverseTransformPoint(p);
                    if (colors.Length == vertices.Length)
                        colors[i] = Color.Lerp(colors[i], new Color(.55f,.46f,.33f), weight);
                }
                copy.vertices = vertices;
                if (colors.Length == vertices.Length) copy.colors = colors;
                copy.RecalculateNormals();
                copy.RecalculateBounds();
                stagedTerrain.Add(pair.Key, SaveBridgeMesh(copy, pair.Key + "_Bed"));
            }

            if (SceneManager.GetSceneByPath(FinalStageScene).isLoaded)
                throw new InvalidOperationException("Close the route validation scene before running this task.");
            stage = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var model = Object.Instantiate(template);
            SceneManager.MoveGameObjectToScene(model, stage);
            model.name = FinalBridgeObject;
            model.transform.SetPositionAndRotation(pivot, pose);
            var terrainColliders = new List<MeshCollider>();
            var landscaped = new Dictionary<string, List<(string name, Mesh mesh, Material material, bool collider)>>();
            foreach (var zone in relevant)
            {
                var root = terrainRoots[zone.zoneId];
                var terrain = AddMesh(stage, null, root.name, stagedTerrain[zone.zoneId], root.GetComponent<Renderer>().sharedMaterial, root.position);
                terrain.transform.rotation = root.rotation;
                terrain.transform.localScale = root.lossyScale;
                var collider = terrain.AddComponent<MeshCollider>();
                collider.sharedMesh = stagedTerrain[zone.zoneId];
                terrainColliders.Add(collider);
                landscaped.Add(zone.zoneId, new());
            }
            Physics.SyncTransforms();
            float Ground(Vector3 point)
            {
                float y = float.NegativeInfinity;
                foreach (var collider in terrainColliders)
                    if (collider.Raycast(new Ray(new Vector3(point.x, railBottom + 80f, point.z), Vector3.down), out var hit, 220f))
                        y = Mathf.Max(y, hit.point.y);
                return y;
            }
            var ballastMat = AssetDatabase.LoadAssetAtPath<Material>(RailMaterial);
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterial);
            var railSupports = new List<MeshCollider>();
            void AddLandscape(string zone, string name, Mesh mesh, Material material, bool collider)
            {
                mesh = SaveBridgeMesh(mesh, zone + "_" + name);
                var obj = AddMesh(stage, null, name, mesh, material, pivot);
                if (collider || name == "SO201_Final_Track_Bed")
                {
                    var support = obj.AddComponent<MeshCollider>();
                    support.sharedMesh = mesh;
                    railSupports.Add(support);
                }
                landscaped[zone].Add((name, mesh, material, collider));
            }

            foreach (var zone in relevant)
            {
                foreach (var segment in new[] { (lo: corridorStart - 7f, hi: start + 2f, entry: true),
                             (lo: end - 2f, hi: corridorEnd + 7f, entry: false) })
                {
                    float lo = Mathf.Max(segment.lo, zone.startChainage);
                    float hi = Mathf.Min(segment.hi, zone.endChainage);
                    if (hi <= lo) continue;
                    var blend = MakeLoft(lo, hi, 2, Center, (station, p) =>
                    {
                        float distance = Mathf.Abs(station - (segment.entry ? start : end));
                        var side = Side(station);
                        var a = p - side * 5.05f;
                        var b = p + side * 5.05f;
                        a.y = b.y = railBottom - .035f;
                        float extra = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / 45f));
                        var left = p - side * Mathf.Lerp(7.5f, 21f, extra);
                        var right = p + side * Mathf.Lerp(7.5f, 21f, extra);
                        left.y = Ground(left) - .25f;
                        right.y = Ground(right) - .25f;
                        if (!float.IsFinite(left.y) || !float.IsFinite(right.y))
                            throw new InvalidOperationException("Approach is outside available terrain at " + station);
                        return new[] { left, a, b, right };
                    }, pivot, new Color(.42f,.42f,.30f));
                    AddLandscape(zone.zoneId, segment.entry ? "SO201_Final_Approach_Entry" : "SO201_Final_Approach_Exit", blend, ballastMat, true);
                }
                float railStart = Mathf.Max(start, zone.startChainage), railEnd = Mathf.Min(end, zone.endChainage);
                if (railEnd <= railStart) continue;
                var ballast = MakeLoft(railStart, railEnd, 1, Center, (station, p) =>
                {
                    var side = Side(station);
                    var a = p - side * 5.05f;
                    var b = p + side * 5.05f;
                    a.y = b.y = railBottom - .012f;
                    var left = p - side * 5.55f;
                    var right = p + side * 5.55f;
                    float leftDeck = DeckAt(left), rightDeck = DeckAt(right);
                    left.y = float.IsFinite(leftDeck) ? leftDeck - .02f : railBottom - .25f;
                    right.y = float.IsFinite(rightDeck) ? rightDeck - .02f : railBottom - .25f;
                    return new[] { left, a, b, right };
                }, pivot, new Color(.36f,.37f,.37f));
                AddLandscape(zone.zoneId, "SO201_Final_Track_Bed", ballast, ballastMat, false);
            }

            var river = MakeLoft(-132f, 132f, 2f,
                x => World(new Vector3(deck.center.x + x / ScaleFactor, 0, wetCenter / ScaleFactor)),
                (x, p) =>
                {
                    float envelope = RiverEnvelope(x);
                    float west = Mathf.Max(firstDryEdge + 1f, Mathf.Lerp(wetCenter, wetStart, envelope));
                    float east = Mathf.Min(lastDryEdge - 1f, Mathf.Lerp(wetCenter, wetEnd, envelope));
                    var a = p + pose * Vector3.forward * (west - wetCenter);
                    var b = p + pose * Vector3.forward * (east - wetCenter);
                    a.y = b.y = waterY;
                    return new[] { b, a };
                }, pivot, new Color(.11f,.31f,.30f));
            AddLandscape("Zone_018", "SO201_Final_River", river, waterMat, false);
            var waterCollider = stage.GetRootGameObjects().Single(node => node.name == "SO201_Final_River").AddComponent<MeshCollider>();
            waterCollider.sharedMesh = landscaped["Zone_018"].Single(item => item.name == "SO201_Final_River").mesh;
            Physics.SyncTransforms();

            // Check slices of the installed surfaces before replacing any existing scene objects.
            var check = new List<string> { "TestID,Target,Measured,Limit,Status" };
            void Check(string name, string target, float actual, float limit) => check.Add(FormattableString.Invariant(
                $"{name},{target},{actual:F5},{limit:F5},{(float.IsFinite(actual) && actual <= limit ? "PASS" : "FAIL")}"));
            Check("T01-Scale", "Bridge", (model.transform.lossyScale - Vector3.one * ScaleFactor).magnitude, .0001f);
            Check("T06-Cap-Count", "Bridge", Mathf.Abs(caps.Length - 8), 0);
            Check("T06-Pile-Count", "Bridge", Mathf.Abs(shapes.Values.Count(shape => HasLabel(shape, "Piles")) - 62), 0);
            float worstClearance = 0, worstTerrain = 0, worstObstruction = 0;
            var aboveDeck = shapes.Values.Where(shape => shape.bounds.max.y >= deck.max.y - .015f).ToArray();
            int missingDeck = 0;
            foreach (float d in Enumerable.Range(2, Mathf.FloorToInt(end - start) - 4).Select(offset => offset + start))
            {
                foreach (float x in new[] { -4.2f, -2.25f, 2.25f, 4.2f })
                {
                    var p = Center(d) + Side(d) * x;
                    float height = DeckAt(p);
                    if (!float.IsFinite(height)) missingDeck++;
                    worstClearance = Mathf.Max(worstClearance, height - railBottom);
                    worstTerrain = Mathf.Max(worstTerrain, Ground(p) - railBottom);
                    var local = Local(p);
                    float obstruction = aboveDeck.Max(shape => MeshSurface(shape, local.x, local.z).max);
                    worstObstruction = Mathf.Max(worstObstruction, WorldHeight(obstruction) - railBottom);
                }
            }
            Check("T05-Deck-Coverage", "Rail footprints", missingDeck, 0);
            Check("T05-Deck-Intrusion", "Rail footprints", worstClearance, .02f);
            Check("T05-Superstructure-Intrusion", "Both train paths", worstObstruction, .02f);
            float worstSupportGap = 0f;
            for (float d = corridorStart; d <= corridorEnd; d += 2f)
                foreach (float x in new[] { -4.2f, -2.25f, 2.25f, 4.2f })
                {
                    var point = Center(d) + Side(d) * x;
                    float surface = Ground(point);
                    var ray = new Ray(new Vector3(point.x, railBottom + 2f, point.z), Vector3.down);
                    foreach (var collider in railSupports)
                        if (collider.Raycast(ray, out var hit, 4f)) surface = Mathf.Max(surface, hit.point.y);
                    worstSupportGap = Mathf.Max(worstSupportGap, railBottom - surface);
                }
            Check("T05-Corridor-Support", "Both tracks throughout former 1120m bridge", worstSupportGap, .075f);
            Check("T07-Terrain-Intrusion", "Under both tracks", worstTerrain, .025f);
            Check("T07-Water-Clearance", "Rail and river", waterY - railBottom + 4f, 0f);
            foreach (var cap in caps)
            {
                float worstBed = 0f;
                foreach (float x in new[] { -.65f, 0f, .65f })
                    foreach (float z in new[] { -.65f, 0f, .65f })
                    {
                        var sample = World(cap.bounds.center + new Vector3(cap.bounds.extents.x * x, 0, cap.bounds.extents.z * z));
                        worstBed = Mathf.Max(worstBed, Mathf.Abs(Ground(sample) - WorldHeight(cap.bounds.min.y)));
                    }
                Check("T07-Bed-Cap", cap.id, worstBed, .16f);
                bool dry = cap == caps[0] || cap == caps[^1];
                Check(dry ? "T07-Dry-Abutment" : "T07-Submerged-Cap", cap.id,
                    dry ? waterY - WorldHeight(cap.bounds.min.y) : WorldHeight(cap.bounds.max.y) - waterY + .5f, 0);
                int waterMisses = 0;
                foreach (var vertex in cap.vertices)
                {
                    var world = World(vertex);
                    bool wet = waterCollider.Raycast(new Ray(new Vector3(world.x, waterY + 2f, world.z), Vector3.down), out _, 4f);
                    if (wet == dry) waterMisses++;
                }
                Check("T07-Water-Footprint", cap.id, waterMisses, 0);
            }
            foreach (var pile in shapes.Values.Where(shape => HasLabel(shape, "Piles")))
                Check("T07-Pile-Soil", pile.id, pile.vertices.Max(vertex =>
                {
                    Vector3 world = World(vertex);
                    return world.y - Ground(world);
                }), .16f);
            WriteBridgePrefabs(model, filters, shapes, originalCatalog, originalParts, native.highlightMaterial, workspace, center, deck, check);
            File.WriteAllLines(validated, check);
            bool pass = !check.Any(line => line.EndsWith(",FAIL", StringComparison.Ordinal));

            if (!install)
            {
                var shots = FinalReports + "Screenshots/Placed/";
                Directory.CreateDirectory(shots);
                foreach (var chunk in sampleChunks.Where(chunk => chunk.end >= start - 20 && chunk.start <= end + 20))
                    foreach (var track in new[] { chunk.left, chunk.right })
                    {
                        var rail = AddMesh(stage, null, "Track", Resources.Load<Mesh>(track.meshResource), track.materials[0], track.position);
                        rail.GetComponent<MeshRenderer>().sharedMaterials = track.materials;
                        rail.transform.rotation = track.rotation;
                        rail.transform.localScale = track.scale;
                    }
                RenderSandbox(stage, midpoint, Side(center), Center(start), Center(end), shots, 410f);
                if (!EditorSceneManager.SaveScene(stage, FinalStageScene)) throw new IOException(FinalStageScene);
                AssetDatabase.SaveAssets();
                Debug.Log($"Placed seven-span SO 201 geometry: {check.Count - 1} checks, {(pass ? "PASS" : "FAIL")}.");
                return;
            }
            if (!pass) throw new InvalidOperationException("The placed candidate failed its geometry recheck.");
            InstallFinalBridge(route, manager, zones, stagedTerrain, landscaped, workspace, start, end, pivot, pose);
        }
        finally
        {
            if (stage.IsValid() && stage.isLoaded) EditorSceneManager.CloseScene(stage, true);
            foreach (var scene in ownedScenes) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            ownedScenes.Clear();
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    static void WriteBridgePrefabs(GameObject model, MeshFilter[] templateFilters, Dictionary<MeshFilter, ContactMesh> shapes,
        BridgeBimCatalog original, Dictionary<string, BridgeBimEntry> source, Material highlight,
        string folder, float center, Bounds deck, List<string> checks)
    {
        var clone = Object.Instantiate(model);
        try
        {
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clone.transform.localScale = Vector3.one;
            string visualPath = folder + "/SO201_SevenSpan_Visual.prefab";
            if (PrefabUtility.SaveAsPrefabAsset(clone, visualPath) == null) throw new IOException(visualPath);
            var elements = source.ToDictionary(pair => pair.Key, pair => pair.Value);
            var entries = new List<BridgeBimEntry>();
            const string root = "SO201-EXTENDED";
            entries.Add(new BridgeBimEntry { globalId = root, name = "Cầu SO 201 · 7 nhịp", ifcClass = "IfcBridge" });
            foreach (Transform group in clone.transform)
                entries.Add(new BridgeBimEntry { globalId = root + "/" + group.name, parentGlobalId = root,
                    name = group.name, ifcClass = "IfcBridgePart" });
            var manifest = new List<string> { "AssetID,Occurrence,SourceGlobalId,MeshAsset,Station_m,WorldCenterX,WorldCenterY,WorldCenterZ" };
            var claimed = new HashSet<string>();
            var nodes = clone.GetComponentsInChildren<MeshFilter>();
            if (nodes.Length != templateFilters.Length) throw new InvalidOperationException("Detailed geometry differs from Visual.");
            for (int index = 0; index < nodes.Length; index++)
            {
                var filter = nodes[index];
                var originalEntry = elements[filter.name];
                var shape = shapes[templateFilters[index]];
                bool isOriginal = claimed.Add(originalEntry.globalId);
                string id = isOriginal ? originalEntry.globalId : root + "/" + shape.id;
                string assetId = isOriginal ? originalEntry.assetId : "BRIDGE-" + root + "-" + shape.id;
                var entry = JsonUtility.FromJson<BridgeBimEntry>(JsonUtility.ToJson(originalEntry));
                entry.globalId = id;
                entry.assetId = assetId;
                entry.parentGlobalId = root + "/" + filter.transform.parent.name;
                entry.sourceGlobalId = originalEntry.globalId;
                entry.sourceParentGlobalId = originalEntry.parentGlobalId;
                entry.sourceIfcClass = originalEntry.ifcClass;
                entry.sourceName = originalEntry.Label;
                entry.modeledStation = center + (shape.bounds.center.z - deck.center.z) * ScaleFactor;
                entry.modeledBounds = shape.bounds.size * ScaleFactor;
                if (!isOriginal)
                    entry.propertyLines = new[] { "Mô phỏng UTTVR | Cấu kiện lặp theo nhịp SO 201. Thuộc tính IFC là dữ liệu của cấu kiện nguồn." }
                        .Concat(entry.propertyLines ?? Array.Empty<string>()).ToArray();
                entries.Add(entry);
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                filter.gameObject.AddComponent<BridgeBimIdentity>().Configure(id, new Renderer[] { renderer });
                var world = model.transform.TransformPoint(shape.bounds.center);
                manifest.Add(FormattableString.Invariant($"{assetId},{shape.id},{originalEntry.globalId},{Quoted(AssetDatabase.GetAssetPath(filter.sharedMesh))},{entry.modeledStation:F4},{world.x:F4},{world.y:F4},{world.z:F4}"));
            }
            bool complete = claimed.SetEquals(source.Values.Select(entry => entry.globalId)) &&
                            entries.Select(entry => entry.globalId).Distinct().Count() == entries.Count &&
                            entries.Where(entry => entry.IsElement).Select(entry => entry.assetId).Distinct().Count() == nodes.Length;
            checks.Add($"T08-BIM-Identity,Visual-Detailed catalog,{(complete ? 0 : 1)},0,{(complete ? "PASS" : "FAIL")}");
            var catalog = Object.Instantiate(original);
            catalog.name = "SO201 Seven Span Catalog";
            catalog.modeledScale = ScaleFactor;
            catalog.modeledZone = "Zone_018";
            catalog.entries = entries.ToArray();
            string catalogPath = folder + "/SO201_SevenSpan_Catalog.asset";
            var old = AssetDatabase.LoadAssetAtPath<BridgeBimCatalog>(catalogPath);
            if (old == null) AssetDatabase.CreateAsset(catalog, catalogPath);
            else
            {
                EditorUtility.CopySerialized(catalog, old);
                Object.DestroyImmediate(catalog);
                catalog = old;
            }
            var adapter = clone.AddComponent<BridgeBimAdapter>();
            adapter.catalog = catalog;
            adapter.highlightMaterial = highlight;
            string detailedPath = folder + "/SO201_SevenSpan_Detailed.prefab";
            if (PrefabUtility.SaveAsPrefabAsset(clone, detailedPath) == null) throw new IOException(detailedPath);
            File.WriteAllLines(FinalReports + "Final_Bridge_Manifest.csv", manifest);
        }
        finally { Object.DestroyImmediate(clone); }
    }

    static void InstallFinalBridge(Scene route, RailwayZoneStreamingManager manager, Dictionary<string, Scene> zones,
        Dictionary<string, Mesh> terrain, Dictionary<string, List<(string name, Mesh mesh, Material material, bool collider)>> landscape,
        string folder, float start, float end, Vector3 pivot, Quaternion pose)
    {
        var bim = Open(BimScene);
        if (route.isDirty || bim.isDirty || zones.Values.Any(scene => scene.isDirty))
            throw new InvalidOperationException("Save unrelated scene edits before installing the bridge.");
        var viewer = bim.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RailwayBimZoneViewer>(true)).Single();
        var facility = viewer.facilities.Single(record => record.key == "Bridge_Main");
        var record = manager.fixedInfrastructure.Single(item => item.id == "Bridge_Main");
        var viewerOrigin = JsonUtility.FromJson<ViewerOrigin>(File.ReadAllText(
            "Assets/ProjectLopHoc/Generated/QuanLyVongDoiTaiSan/SceneProvenance.json")).sourceWorldOrigin;
        var visual = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/SO201_SevenSpan_Visual.prefab");
        var detailed = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/SO201_SevenSpan_Detailed.prefab");
        var catalog = AssetDatabase.LoadAssetAtPath<BridgeBimCatalog>(folder + "/SO201_SevenSpan_Catalog.asset");
        var covered = manager.zones.Where(zone => zone.endChainage > start && zone.startChainage < end)
            .Select(zone => zone.zoneId).ToArray();
        if (!covered.Contains("Zone_018") || visual == null || detailed == null || catalog == null)
            throw new InvalidOperationException("The bridge assets or streaming owner are missing.");

        foreach (var pair in zones)
        {
            var viewPath = ViewRoot + pair.Key + ".prefab";
            var viewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(viewPath);
            if (viewPrefab == null || Root(pair.Value).Find("Terrain" + pair.Key) == null ||
                viewPrefab.transform.Find("Terrain" + pair.Key) == null)
                throw new InvalidOperationException("Preflight failed for " + pair.Key);
        }
        void UpdateScene(string zoneId, Scene scene, Transform root, Vector3 origin, GameObject source)
        {
            var terrainRoot = root.Find("Terrain" + zoneId);
            terrainRoot.GetComponent<MeshFilter>().sharedMesh = terrain[zoneId];
            if (terrainRoot.TryGetComponent<MeshCollider>(out var collider)) collider.sharedMesh = terrain[zoneId];
            foreach (var child in root.Cast<Transform>().Where(child =>
                         child.name is "SO201_Approach_Entry" or "SO201_Approach_Exit" or "SO201_Track_Bed" or "SO201_River" or
                             "SO201_Bank_Entry" or "SO201_Bank_Exit" ||
                         child.name.StartsWith("SO201_FoundationSoil_", StringComparison.Ordinal) ||
                         child.name.StartsWith("SO201_Final_", StringComparison.Ordinal)).ToArray())
                Object.DestroyImmediate(child.gameObject);
            foreach (var item in landscape[zoneId])
            {
                var node = AddMesh(scene, root, item.name, item.mesh, item.material, pivot - origin);
                if (source == visual && item.collider) node.AddComponent<MeshCollider>().sharedMesh = item.mesh;
            }
            if (zoneId != "Zone_018") return;
            var branch = root.Find("FixedInfrastructure_Bridge");
            if (branch == null) throw new InvalidOperationException("Bridge ownership branch missing.");
            foreach (var child in branch.Cast<Transform>().Where(child =>
                         child.name == BridgeObject || child.name == FinalBridgeObject).ToArray())
                Object.DestroyImmediate(child.gameObject);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            model.name = FinalBridgeObject;
            model.transform.SetParent(branch, false);
            model.transform.SetPositionAndRotation(pivot - origin, pose);
            model.transform.localScale = Vector3.one * ScaleFactor;
        }

        foreach (var pair in zones)
        {
            UpdateScene(pair.Key, pair.Value, Root(pair.Value), Vector3.zero, visual);
            Save(pair.Value);
            string viewPath = ViewRoot + pair.Key + ".prefab";
            var content = PrefabUtility.LoadPrefabContents(viewPath);
            try
            {
                UpdateScene(pair.Key, content.scene, content.transform, viewerOrigin, detailed);
                if (PrefabUtility.SaveAsPrefabAsset(content, viewPath) == null) throw new IOException(viewPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(content); }
        }
        record.startChainage = start;
        record.endChainage = end;
        record.zoneIds = covered;
        facility.start = start;
        facility.end = end;
        facility.covered = covered;
        facility.bindings = new[] { "Zone_018/RuntimeZoneContent/FixedInfrastructure_Bridge/" + FinalBridgeObject };
        facility.label = "Cầu SO 201 · 7 nhịp";
        viewer.modularBridgeCatalog = catalog;
        Save(route);
        Save(bim);
        AssetDatabase.SaveAssets();
        File.WriteAllText(FinalReports + "Installed_Bridge.txt", FormattableString.Invariant(
            $"{FinalBridgeObject}; stations {start:F3}–{end:F3} m; {string.Join("|", covered)}; Visual and Detailed use the same generated mesh set."));
    }
}
