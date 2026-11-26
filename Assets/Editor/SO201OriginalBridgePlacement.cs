using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UttVr.BridgePilot;
using Object = UnityEngine.Object;

static partial class SO201OriginalBridgeAudit
{
    const string OriginalRoot = "Assets/ExternalBIM/SO201Original";
    const string ZoneRoot = "Assets/ProjectLopHoc/Generated/RuntimeZones/";
    const string ViewRoot = "Assets/ExternalBIM/ZoneBased/Resources/RouteBimZones/";
    const string BimScene = "Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity";
    const string SandboxScene = OriginalRoot + "/SO201_Original_Sandbox.unity";
    const string Folder = OriginalRoot + "/Placement";
    const string SourceVisual = "Assets/ExternalBIM/BridgeDemo/Bridge_Visual.prefab";
    const string RailMaterial = "Assets/ProjectLopHoc/Generated/QuestRailway/RailwayFlatColour.mat";
    const string WaterMaterial = "Assets/ProjectLopHoc/Generated/EnvironmentVisualFix/Water_Opaque.mat";
    const string OriginalTerrain = "Assets/ProjectLopHoc/Generated/QuestRailway/TerrainMeshes/Terrain_018.asset";
    const string BridgeObject = "Bridge_SO201_Original";
    const float ScaleFactor = 2.4f;

    [Serializable] sealed class ViewerOrigin { public Vector3 sourceWorldOrigin; }

    static void PlaceOriginal(bool install)
    {
        if (install && !File.Exists(Job + "sandbox-pass.txt"))
            throw new InvalidOperationException("The original SO 201 has not passed the sandbox gate.");
        var row = File.ReadLines(Job + "geometry.txt").Skip(2).First().Split(',');
        float Number(int i) => float.Parse(row[i], CultureInfo.InvariantCulture);
        if (Number(0) != ScaleFactor || Number(4) > .04f || Number(8) != 0 || row[10] != "\"\"")
            throw new InvalidOperationException("The original SO 201 does not meet the track clearance gate.");
        float station = Number(1), start = Number(2), end = Number(3), railBottom = Number(6);

        var previous = SceneManager.GetActiveScene();
        var loadedSandbox = SceneManager.GetSceneByPath(SandboxScene);
        if (!install && loadedSandbox.isLoaded && loadedSandbox.isDirty)
            throw new InvalidOperationException("Sandbox has unsaved changes.");
        var route = Open(Route);
        if (!install && loadedSandbox.isLoaded)
        {
            if (SceneManager.sceneCount == 2) ownedScenes.Remove(route);
            EditorSceneManager.CloseScene(loadedSandbox, true);
            if (previous == loadedSandbox) previous = route;
        }
        var pilot = PrefabUtility.LoadPrefabContents(Pilot);
        var terrainZones = new Dictionary<string, Scene>();
        Scene sandbox = default;
        try
        {
            var manager = route.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var streamer = route.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RailwayMicroChunkStreamer>(true)).Single();
            var bridge = manager.fixedInfrastructure.Single(entry => entry.id == "Bridge_Main");
            var spline = manager.trainMover.splineRoute;
            var length = spline.CalculateLength();
            const float oldStart = 3650.0015f, oldEnd = 4770.0015f;
            Vector3 Center(float d) => (Vector3)spline.EvaluatePosition(d / length) -
                Vector3.Cross(Vector3.up, (Vector3)spline.EvaluateTangent(d / length)).normalized * Spacing * .5f;
            Vector3 Right(float d) => Vector3.Cross(Vector3.up, (Vector3)spline.EvaluateTangent(d / length)).normalized;

            var parts = ReadParts(pilot);
            var pavement = parts.Where(p => p.entry.ifcClass == "IfcPavement").ToArray();
            var deck = BoundsOf(pavement.SelectMany(p => p.vertices));
            var abutments = parts.Where(p => p.entry.Label.StartsWith("Abutment stem ", StringComparison.Ordinal))
                .Select(part => BoundsOf(part.vertices)).OrderBy(bounds => bounds.center.z).ToArray();
            if (abutments.Length != 2) throw new InvalidOperationException("Expected two native abutment stems.");
            float entryContact = station + (abutments[0].min.z - deck.center.z) * ScaleFactor + .20f;
            float exitContact = station + (abutments[1].max.z - deck.center.z) * ScaleFactor - .20f;
            var basis = Quaternion.LookRotation(Forward);
            var pose = Quaternion.LookRotation(Vector3.ProjectOnPlane(Center(end)-Center(start), Vector3.up));
            var pos = (Center(start) + Center(end)) * .5f;
            pos.y = railBottom - .025f - deck.max.y * ScaleFactor;
            var rotation = pose * Quaternion.Inverse(basis);
            var pivot = pos - pose * new Vector3(deck.center.x, 0, deck.center.z) * ScaleFactor;
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(SourceVisual);
            if (visual == null || visual.GetComponentsInChildren<MeshRenderer>(true).Length != 91 ||
                visual.GetComponentsInChildren<BridgeBimIdentity>(true).Length != 0 ||
                visual.GetComponentsInChildren<MeshCollider>(true).Length != 0)
                throw new InvalidOperationException("Visual prefab must contain exactly 91 renderers without BIM or colliders.");
            var native = pilot.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToHashSet();
            if (!native.SetEquals(visual.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)))
                throw new InvalidOperationException("The two representations do not share the same meshes.");

            foreach (var zone in manager.zones.Where(z => z.endChainage > oldStart-10 && z.startChainage < oldEnd+10))
                terrainZones.Add(zone.zoneId, Open(zone.scenePath));
            var colliders = terrainZones.Values.SelectMany(scene => Root(scene).GetComponentsInChildren<MeshCollider>(true))
                .Where(c => c.name.StartsWith("TerrainZone_", StringComparison.Ordinal)).ToArray();
            if (colliders.Length < 3) throw new InvalidOperationException("Terrain colliders for the approaches are missing.");
            Physics.SyncTransforms();
            float Ground(Vector3 point)
            {
                float y = -28f;
                var ray = new Ray(new Vector3(point.x, railBottom + 80, point.z), Vector3.down);
                foreach (var collider in colliders)
                    if (collider.Raycast(ray, out var hit, 220)) y = Mathf.Max(y, hit.point.y);
                return y;
            }

            var caps = parts.Where(p => p.entry.Label.StartsWith("Pile cap ", StringComparison.Ordinal))
                .Select(p => (label: p.entry.Label, meshId: p.meshId, bounds: BoundsOf(p.vertices.Select(v =>
                    new Vector3((v.x-deck.center.x)*ScaleFactor, pos.y+v.y*ScaleFactor, (v.z-deck.center.z)*ScaleFactor)))))
                .OrderBy(p => p.bounds.center.z).ToArray();
            var piers = parts.Where(p => p.entry.Label.StartsWith("Pier stem ", StringComparison.Ordinal))
                .Select(p => BoundsOf(p.vertices)).ToArray();
            if (caps.Length != 4 || piers.Length != 2) throw new InvalidOperationException("Missing native pile caps or pier stems.");
            var riverCaps = caps.Where(cap => cap.label is "Pile cap P2" or "Pile cap P3").ToArray();
            var piles = parts.Where(p => p.entry.ifcClass == "IfcPile").ToArray();
            if (piles.Length != 30) throw new InvalidOperationException("Expected thirty native piles.");
            var riverPiles = piles.Where(p => p.entry.Label.StartsWith("Piles P", StringComparison.Ordinal)).ToArray();
            if (riverPiles.Length != 16) throw new InvalidOperationException("Expected sixteen piles under piers P2 and P3.");
            var ground = Ground(Center(station));
            if (ground > railBottom - 4f)
                throw new InvalidOperationException($"The existing valley reaches the underside of the bridge: ground={ground:F2}.");

            float highestRiverCap = riverCaps.Max(cap => cap.bounds.max.y);
            float waterY = Mathf.Max(highestRiverCap + .55f,
                piers.Average(pier => pos.y + Mathf.Lerp(pier.min.y,pier.max.y,1f/3f)*ScaleFactor));
            if (waterY > railBottom - 4f) throw new InvalidOperationException("The water would reach the rail corridor.");
            float bedY = riverCaps.Min(cap => cap.bounds.min.y) - 1.2f;
            float firstCut = riverCaps.Min(cap => cap.bounds.min.z) - 6;
            float lastCut = riverCaps.Max(cap => cap.bounds.max.z) + 6;
            var riverCenter = Center(station);
            var riverAlong = Right(station);
            var riverAcross = pose * Vector3.forward;
            const float riverLength = 230f;
            float channelHalfWidth = riverCaps.Max(cap => Mathf.Max(Mathf.Abs(cap.bounds.min.z), Mathf.Abs(cap.bounds.max.z))) + 10f;
            if (caps.Except(riverCaps).Any(cap => cap.bounds.min.y <= waterY))
                throw new InvalidOperationException("The water level reaches an abutment cap.");
            float RiverBend(float d) => 5 * Mathf.Sin(d / 74f);
            float RiverWidth(float d) => channelHalfWidth * (1 + .08f * Mathf.Sin(d / 62f)) *
                Mathf.Lerp(1, .73f, Mathf.SmoothStep(0, 1, Mathf.Abs(d) / (riverLength * .5f)));
            var inversePose = Quaternion.Inverse(pose);
            float RiverbedWeight(Vector3 point)
            {
                var local = inversePose * (point - pos);
                float supportEdge = Mathf.Max(Mathf.Abs(local.x) - 27f, firstCut-local.z, local.z-lastCut);
                var relative = point - riverCenter;
                float flow = Vector3.Dot(relative, riverAlong);
                float across = Vector3.Dot(relative, riverAcross) - RiverBend(flow);
                float channelEdge = Mathf.Max(Mathf.Abs(flow) - riverLength * .5f, Mathf.Abs(across) - RiverWidth(flow)) - 4;
                float edge = Mathf.Min(supportEdge, channelEdge);
                return 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(edge / 17f));
            }
            float LoweredGround(Vector3 point, float height) => Mathf.Lerp(height, Mathf.Min(height, bedY), RiverbedWeight(point));
            Vector3 WorldAt(float x, float y, float z)
            {
                var point = pos + pose * new Vector3(x,0,z);
                point.y = y;
                return point;
            }
            if (!AssetDatabase.IsValidFolder(OriginalRoot)) AssetDatabase.CreateFolder("Assets/ExternalBIM", "SO201Original");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(OriginalRoot, "Placement");
            var terrain = colliders.Single(c => c.name == "TerrainZone_018");
            var originalTerrain = AssetDatabase.LoadAssetAtPath<Mesh>(OriginalTerrain);
            var carvedMesh = Object.Instantiate(originalTerrain);
            var vertices = carvedMesh.vertices;
            var colors = carvedMesh.colors;
            Color sand = new Color(.55f,.46f,.33f);
            for (int i = 0; i < vertices.Length; i++)
            {
                var point = terrain.transform.TransformPoint(vertices[i]);
                if (colors.Length == vertices.Length) colors[i] = Color.Lerp(colors[i], sand, RiverbedWeight(point));
                point.y = LoweredGround(point, point.y);
                vertices[i] = terrain.transform.InverseTransformPoint(point);
            }
            carvedMesh.vertices = vertices;
            if (colors.Length == vertices.Length) carvedMesh.colors = colors;
            carvedMesh.RecalculateNormals();
            carvedMesh.RecalculateBounds();
            const string riverbedPath = Folder + "/Zone_018_SO201_RiverbedTerrain.asset";
            var savedTerrain = AssetDatabase.LoadAssetAtPath<Mesh>(riverbedPath);
            if (savedTerrain != null)
            {
                savedTerrain.vertices = carvedMesh.vertices;
                savedTerrain.normals = carvedMesh.normals;
                savedTerrain.colors = carvedMesh.colors;
                savedTerrain.RecalculateBounds();
                EditorUtility.SetDirty(savedTerrain);
                Object.DestroyImmediate(carvedMesh);
            }
            else { AssetDatabase.CreateAsset(carvedMesh, riverbedPath); savedTerrain = carvedMesh; }
            AssetDatabase.SaveAssetIfDirty(savedTerrain);
            void ApplyRiverbed(Transform root)
            {
                var node = root.Find("TerrainZone_018");
                if (node == null) throw new InvalidOperationException("Zone terrain binding missing in " + root.name);
                node.GetComponent<MeshFilter>().sharedMesh = savedTerrain;
                if (node.TryGetComponent<MeshCollider>(out var collider)) collider.sharedMesh = savedTerrain;
            }

            var groups = new Dictionary<string, List<(string name, Mesh mesh, Material material, bool collider)>>();
            foreach (var pair in terrainZones)
            {
                string id = pair.Key;
                var zone = manager.zones.Single(z => z.zoneId == id);
                groups.Add(id, new());
                var fillMat = AssetDatabase.LoadAssetAtPath<Material>(RailMaterial);
                foreach (var segment in new[] { (lo: oldStart-5, hi: entryContact, side: "Entry"), (lo: exitContact, hi: oldEnd+5, side: "Exit") })
                {
                    float lo = Mathf.Max(segment.lo, zone.startChainage);
                    float hi = Mathf.Min(segment.hi, zone.endChainage);
                    if (lo >= hi) continue;
                    var mesh = MakeLoft(lo, hi, 3, Center, (d,p) =>
                    {
                        float top = railBottom - .035f;
                        var right = Right(d);
                        const float shoulder = 5f;
                        float contact = segment.side == "Entry" ? entryContact : exitContact;
                        float taper = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Abs(d - contact) / 43f));
                        if (d >= start && d <= end)
                        {
                            var local = Quaternion.Inverse(pose) * (p - pos);
                            float road = pavement.Max(part => PavementY(part, local.x / ScaleFactor + deck.center.x,
                                local.z / ScaleFactor + deck.center.z));
                            if (!float.IsNegativeInfinity(road))
                            {
                                float overlap = Mathf.Clamp01(Mathf.Min(d - start, end - d) / 4f);
                                top = Mathf.Lerp(top, Mathf.Min(top, pos.y + road * ScaleFactor - .24f), overlap);
                            }
                        }
                        var leftEdge = p - right * shoulder;
                        var rightEdge = p + right * shoulder;
                        var leftToe = p - right * Mathf.Lerp(8, Mathf.Clamp(7 + (top - Ground(p - right * 35)) * 1.25f, 12, 55), taper);
                        var rightToe = p + right * Mathf.Lerp(8, Mathf.Clamp(7 + (top - Ground(p + right * 35)) * 1.25f, 12, 55), taper);
                        leftEdge.y = rightEdge.y = top;
                        leftToe.y = Ground(leftToe) - .4f;
                        rightToe.y = Ground(rightToe) - .4f;
                        var leftSlope = Vector3.Lerp(leftEdge,leftToe,.27f);
                        var rightSlope = Vector3.Lerp(rightEdge,rightToe,.27f);
                        leftSlope.y = Mathf.Lerp(leftEdge.y,leftToe.y,.44f);
                        rightSlope.y = Mathf.Lerp(rightEdge.y,rightToe.y,.44f);
                        return new[] { leftToe, leftSlope, leftEdge, rightEdge, rightSlope, rightToe };
                    }, pivot, new Color(.40f,.39f,.29f), sectionColors: new[] {
                        new Color(.43f,.50f,.28f), new Color(.46f,.45f,.32f), new Color(.38f,.36f,.29f),
                        new Color(.38f,.36f,.29f), new Color(.46f,.45f,.32f), new Color(.43f,.50f,.28f) });
                    groups[id].Add(("SO201_Approach_" + segment.side, mesh, fillMat, true));
                }
            }

            var ballast = MakeLoft(start, end, 1, Center, (d,p) =>
            {
                var right = Right(d);
                var offsets = new[] { -5.6f, -4.8f, -1.6f, -.8f, .8f, 1.6f, 4.8f, 5.6f };
                var section = new Vector3[offsets.Length];
                for (int i = 0; i < offsets.Length; i++)
                {
                    section[i] = p + right * offsets[i];
                    var local = Quaternion.Inverse(pose) * (section[i] - pos);
                    float y = pavement.Max(part => PavementY(part, local.x / ScaleFactor + deck.center.x, local.z / ScaleFactor + deck.center.z));
                    section[i].y = i is 1 or 2 or 5 or 6 ? railBottom - .03f :
                        float.IsNegativeInfinity(y) ? railBottom - .20f : Mathf.Min(railBottom - .05f, pos.y + y*ScaleFactor + .015f);
                }
                return section;
            }, pivot, new Color(.43f,.41f,.37f), skip: 3);
            groups["Zone_018"].Add(("SO201_Track_Bed", ballast, AssetDatabase.LoadAssetAtPath<Material>(RailMaterial), false));

            Vector3 River(float distance) => riverCenter + riverAlong * distance +
                riverAcross * RiverBend(distance);
            Vector3 RiverEdge(float distance, Vector3 center, float side)
            {
                var point = center + riverAcross * (side * RiverWidth(distance));
                point.y = waterY;
                return point;
            }
            var water = MakeLoft(-riverLength * .5f, riverLength * .5f, 5, River, (d,p) =>
                new[] { RiverEdge(d,p,1), RiverEdge(d,p,-1) }, pivot, Color.white);
            groups["Zone_018"].Add(("SO201_River", water, AssetDatabase.LoadAssetAtPath<Material>(WaterMaterial), false));
            foreach (var cap in caps)
            {
                var b = cap.bounds;
                string support = cap.label.Split(' ').Last();
                var pilePositions = piles.Where(p => p.entry.Label.StartsWith("Piles " + support + " ", StringComparison.Ordinal))
                    .SelectMany(p => p.vertices).Select(v =>
                        new Vector3((v.x-deck.center.x)*ScaleFactor, b.min.y, (v.z-deck.center.z)*ScaleFactor));
                foreach (var point in pilePositions) b.Encapsulate(point);
                b.Expand(new Vector3(.6f,0,.6f));
                bool inRiver = riverCaps.Any(c => c.label == cap.label);
                float slope = inRiver ? 5f : 10f;
                float Surface(float x, float z)
                {
                    float edge = Mathf.Max(Mathf.Abs(x-b.center.x)-b.extents.x, Mathf.Abs(z-b.center.z)-b.extents.z);
                    var basePoint = WorldAt(x,0,z);
                    float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(edge/slope));
                    float natural = LoweredGround(basePoint, Ground(basePoint)) - .09f;
                    return Mathf.Lerp(b.min.y + .02f, natural, blend);
                }
                Vector3[] rows(float z, Vector3 p)
                {
                    float xMin = b.min.x, xMax = b.max.x;
                    return new[]{xMin-slope,xMin-slope*.5f,xMin,xMax,xMax+slope*.5f,xMax+slope}
                        .Select(x => WorldAt(x, Surface(x,z), z)).ToArray();
                }
                var mesh = MakeLoft(b.min.z-slope,b.max.z+slope,.5f,z=>WorldAt(0,0,z),rows,pivot,sand);
                groups["Zone_018"].Add(("SO201_FoundationSoil_" + support,mesh,
                    AssetDatabase.LoadAssetAtPath<Material>(RailMaterial),false));
            }
            foreach (float side in new[] { -1f, 1f })
            {
                var bank = MakeLoft(-riverLength * .5f, riverLength * .5f, 5, River, (d,p) =>
                {
                    var edge = RiverEdge(d,p,side);
                    var crown = edge + riverAcross * side * 7;
                    var toe = edge + riverAcross * side * 23;
                    edge.y -= .07f;
                    crown.y = Mathf.Max(waterY + .75f, Ground(crown) + .05f);
                    toe.y = Ground(toe) - .15f;
                    return side < 0 ? new[] {edge,crown,toe} : new[] {toe,crown,edge};
                }, pivot, new Color(.39f,.43f,.30f));
                groups["Zone_018"].Add((side < 0 ? "SO201_Bank_Entry" : "SO201_Bank_Exit", bank,
                    AssetDatabase.LoadAssetAtPath<Material>(RailMaterial), false));
            }

            foreach (var pair in groups)
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    var item = pair.Value[i];
                    string path = Folder + "/" + pair.Key + "_" + item.name + ".asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved != null)
                    {
                        saved.Clear();
                        saved.indexFormat = item.mesh.indexFormat;
                        saved.vertices = item.mesh.vertices;
                        saved.colors = item.mesh.colors;
                        saved.normals = item.mesh.normals;
                        saved.SetTriangles(item.mesh.triangles, 0);
                        EditorUtility.SetDirty(saved);
                        Object.DestroyImmediate(item.mesh);
                    }
                    else { AssetDatabase.CreateAsset(item.mesh, path); saved = item.mesh; }
                    AssetDatabase.SaveAssetIfDirty(saved);
                    pair.Value[i] = (item.name, saved, item.material, item.collider);
                }

            GameObject Place(Scene s, Transform parent, GameObject source, Vector3 origin)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, s);
                model.name = BridgeObject;
                model.transform.SetParent(parent, false);
                model.transform.SetPositionAndRotation(pivot - origin, rotation);
                model.transform.localScale = Vector3.one * ScaleFactor;
                return model;
            }
            void Surround(Scene s, Transform parent, string zone, Vector3 origin)
            {
                foreach (var item in groups[zone])
                {
                    var previousMesh = parent != null ? parent.Find(item.name) : null;
                    if (previousMesh != null) Object.DestroyImmediate(previousMesh.gameObject);
                    var node = AddMesh(s, parent, item.name, item.mesh, item.material, pivot - origin);
                    if (item.collider && origin == Vector3.zero) node.AddComponent<MeshCollider>().sharedMesh = item.mesh;
                }
            }

            if (!install)
            {
                sandbox = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var model = Place(sandbox, null, visual, Vector3.zero);
                if (model.GetComponentsInChildren<MeshRenderer>(true).Length != 91 || model.transform.localScale != Vector3.one * ScaleFactor)
                    throw new InvalidOperationException("Sandbox instance differs from the 91-piece Visual.");
                foreach (var key in groups.Keys) Surround(sandbox, null, key, Vector3.zero);
                var riverObject = sandbox.GetRootGameObjects().Single(node => node.name == "SO201_River");
                var waterCollider = riverObject.AddComponent<MeshCollider>();
                waterCollider.sharedMesh = riverObject.GetComponent<MeshFilter>().sharedMesh;
                var placedMeshById = model.GetComponentsInChildren<MeshFilter>(true).ToDictionary(filter =>
                {
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.sharedMesh, out string _, out long id))
                        throw new InvalidOperationException("Visual mesh lost native asset identity.");
                    return id;
                });
                Physics.SyncTransforms();
                foreach (long nativeId in riverPiles.Select(p => p.meshId).Concat(riverCaps.Select(cap => cap.meshId)))
                {
                    var filter = placedMeshById[nativeId];
                    foreach (var vertex in filter.sharedMesh.vertices)
                    {
                        var point = filter.transform.TransformPoint(vertex);
                        if (point.y + .5f >= waterY)
                            throw new InvalidOperationException($"A native pile or cap is too close to the water surface: {nativeId}.");
                        point.y = waterY + 3;
                        if (!waterCollider.Raycast(new Ray(point, Vector3.down), out _, 6))
                            throw new InvalidOperationException($"River surface does not cover foundation mesh {nativeId}.");
                    }
                }
                foreach (var cap in riverCaps)
                {
                    var center = WorldAt(cap.bounds.center.x,cap.bounds.center.y,cap.bounds.center.z);
                    foreach (float side in new[]{-1f,1f})
                    {
                        var eye = Center(station) + Right(station)*125*side + Vector3.up*66;
                        var sight = center-eye;
                        if (!waterCollider.Raycast(new Ray(eye,sight.normalized),out _,sight.magnitude))
                            throw new InvalidOperationException($"Above-water camera can see {cap.label} without crossing the river surface.");
                    }
                }
                foreach (var cap in caps.Except(riverCaps))
                {
                    var filter = placedMeshById[cap.meshId];
                    foreach (var vertex in filter.sharedMesh.vertices)
                    {
                        var point = filter.transform.TransformPoint(vertex);
                        point.y = waterY + 3;
                        if (waterCollider.Raycast(new Ray(point, Vector3.down), out _, 6))
                            throw new InvalidOperationException($"The river extends beneath the dry {cap.label}.");
                    }
                }
                Object.DestroyImmediate(waterCollider);
                foreach (var cap in caps)
                {
                    string support = cap.label.Split(' ').Last();
                    var soil = sandbox.GetRootGameObjects().Single(node => node.name == "SO201_FoundationSoil_" + support);
                    var soilCollider = soil.AddComponent<MeshCollider>();
                    soilCollider.sharedMesh = soil.GetComponent<MeshFilter>().sharedMesh;
                    Physics.SyncTransforms();
                    var b = cap.bounds;
                    foreach (float x in new[]{b.min.x,b.center.x,b.max.x})
                        foreach (float z in new[]{b.min.z,b.center.z,b.max.z})
                        {
                            var point = WorldAt(x,b.min.y+2,z);
                            if (!soilCollider.Raycast(new Ray(point,Vector3.down),out var hit,4) ||
                                Mathf.Abs(hit.point.y-b.min.y) > .035f)
                                throw new InvalidOperationException($"Foundation soil gap at {cap.label}.");
                        }
                    foreach (var part in piles.Where(p => p.entry.Label.StartsWith("Piles " + support + " ", StringComparison.Ordinal)))
                    {
                        var filter = placedMeshById[part.meshId];
                        foreach (var vertex in filter.sharedMesh.vertices)
                        {
                            var point = filter.transform.TransformPoint(vertex);
                            var origin = new Vector3(point.x, b.min.y + 3, point.z);
                            if (!soilCollider.Raycast(new Ray(origin,Vector3.down), out var hit, 4) || point.y > hit.point.y + .035f)
                                throw new InvalidOperationException($"Pile vertex is outside the soil at {part.entry.Label}.");
                        }
                    }
                    Object.DestroyImmediate(soilCollider);
                }
                foreach (var source in colliders)
                    AddMesh(sandbox, null, source.name, source == terrain ? savedTerrain : source.sharedMesh,
                        source.GetComponent<Renderer>().sharedMaterial, source.transform.position);
                foreach (var chunk in streamer.data.chunks.Where(c => c.end >= start-45 && c.start <= end+45))
                    foreach (var track in new[]{chunk.left,chunk.right})
                    {
                        var instance = AddMesh(sandbox, null, "Track", Resources.Load<Mesh>(track.meshResource),
                            track.materials[0],track.position);
                        instance.GetComponent<MeshRenderer>().sharedMaterials = track.materials;
                        instance.transform.rotation = track.rotation;
                        instance.transform.localScale = track.scale;
                    }
                RenderSandbox(sandbox, Center(station), Right(station), Center(start), Center(end));
                if (!EditorSceneManager.SaveScene(sandbox, SandboxScene))
                    throw new IOException("Failed to save sandbox.");
                File.WriteAllText(Job + "sandbox-pass.txt", FormattableString.Invariant(
                    $"91 native DAE meshes; {ScaleFactor:F2} uniform; station {start:F3}–{end:F3}; abutment contacts {entryContact:F3} and {exitContact:F3}; river {riverLength:F1} m long, {channelHalfWidth * 2:F1} m wide; water Y={waterY:F3}; highest midriver cap Y={highestRiverCap:F3}; sand bed Y={bedY:F3}; pier submergence fractions={string.Join("|", piers.Select(p=>Mathf.Clamp01((waterY-pos.y-p.min.y*ScaleFactor)/(p.size.y*ScaleFactor)).ToString("F3",CultureInfo.InvariantCulture)))}; P2/P3 caps submerged from both sides; dry O1/O4 caps; all 30 piles buried."));
                return;
            }

            var viewerOrigin = JsonUtility.FromJson<ViewerOrigin>(File.ReadAllText(
                "Assets/ProjectLopHoc/Generated/QuanLyVongDoiTaiSan/SceneProvenance.json")).sourceWorldOrigin;
            var authoredCatalog = pilot.GetComponent<BridgeBimAdapter>().catalog;
            var placedCatalog = Object.Instantiate(authoredCatalog);
            placedCatalog.name = "SO201 Original Railway Layout";
            placedCatalog.modeledScale = ScaleFactor;
            placedCatalog.modeledZone = "Zone_018";
            placedCatalog.modeledForward = Forward;
            var partById = parts.ToDictionary(p => p.entry.globalId);
            foreach (var entry in placedCatalog.entries.Where(e => e.IsElement))
            {
                var bounds = BoundsOf(partById[entry.globalId].vertices);
                entry.modeledBounds = bounds.size * ScaleFactor;
                entry.modeledStation = Mathf.Clamp(station + (bounds.center.z - deck.center.z) * ScaleFactor, oldStart, oldEnd);
            }
            const string catalogPath = Folder + "/SO201_Original_Railway_Catalog.asset";
            var oldCatalog = AssetDatabase.LoadAssetAtPath<BridgeBimCatalog>(catalogPath);
            if (oldCatalog != null) { EditorUtility.CopySerialized(placedCatalog, oldCatalog); Object.DestroyImmediate(placedCatalog); placedCatalog = oldCatalog; }
            else AssetDatabase.CreateAsset(placedCatalog, catalogPath);

            foreach (var pair in terrainZones)
            {
                var root = Root(pair.Value);
                if (pair.Key == "Zone_018")
                {
                    ApplyRiverbed(root);
                    var branch = root.Find("FixedInfrastructure_Bridge");
                    var platform = branch.GetComponent<RailwayBridgeVisualPlatform>();
                    if (platform != null) Object.DestroyImmediate(platform);
                    foreach (var child in branch.Cast<Transform>().Where(IsPreviousBridgeInstance).ToArray())
                        Object.DestroyImmediate(child.gameObject);
                    Place(pair.Value, branch, visual, Vector3.zero);
                }
                Surround(pair.Value, root, pair.Key, Vector3.zero);
                Save(pair.Value);

                var viewPath = ViewRoot + pair.Key + ".prefab";
                var rootView = PrefabUtility.LoadPrefabContents(viewPath);
                try
                {
                    if (pair.Key == "Zone_018")
                    {
                        ApplyRiverbed(rootView.transform);
                        var branch = rootView.transform.Find("FixedInfrastructure_Bridge");
                        if (branch == null) throw new InvalidOperationException("Detailed bridge branch missing.");
                        foreach (var child in branch.Cast<Transform>().Where(IsPreviousBridgeInstance).ToArray())
                            Object.DestroyImmediate(child.gameObject);
                        var instance = Place(rootView.scene, branch, AssetDatabase.LoadAssetAtPath<GameObject>(Pilot), viewerOrigin);
                        instance.GetComponent<BridgeBimAdapter>().catalog = placedCatalog;
                    }
                    Surround(rootView.scene, rootView.transform, pair.Key, viewerOrigin);
                    if (PrefabUtility.SaveAsPrefabAsset(rootView, viewPath) == null) throw new IOException(viewPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(rootView); }
            }

            bridge.startChainage = start;
            bridge.endChainage = end;
            bridge.zoneIds = new[] { "Zone_018" };
            Save(route);
            var bim = Open(BimScene);
            var viewer = bim.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RailwayBimZoneViewer>(true)).Single();
            viewer.modularBridgeCatalog = placedCatalog;
            var facility = viewer.facilities.Single(f => f.key == "Bridge_Main");
            facility.start = start;
            facility.end = end;
            facility.covered = new[] { "Zone_018" };
            facility.bindings = new[] { "Zone_018/RuntimeZoneContent/FixedInfrastructure_Bridge/" + BridgeObject };
            facility.label = "Cầu chính SO 201";
            Save(bim);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Job + "installed.txt", File.ReadAllText(Job + "sandbox-pass.txt"));
        }
        finally
        {
            if (sandbox.IsValid() && sandbox.isLoaded) EditorSceneManager.CloseScene(sandbox, true);
            foreach (var scene in ownedScenes) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            ownedScenes.Clear();
            PrefabUtility.UnloadPrefabContents(pilot);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    static readonly List<Scene> ownedScenes = new();
    static bool IsPreviousBridgeInstance(Transform child) =>
        child.name is BridgeObject or "Bridge_YTH_Visual" or "Legacy_Modular_Android_Fallback" or
            "ExtendedBridgeQuest" or "BridgeValleyWaterQuest" or "SO201_Full_Detailed" or "Bridge_YTH_Detailed";
    static Scene Open(string path)
    {
        var scene = SceneManager.GetSceneByPath(path);
        if (scene.isDirty) throw new InvalidOperationException("Scene has unsaved edits: " + path);
        if (scene.isLoaded) return scene;
        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        ownedScenes.Add(scene);
        return scene;
    }
    static Transform Root(Scene scene) => scene.GetRootGameObjects().Single(g => g.name == "RuntimeZoneContent").transform;

    static void AuditInstalledBridge()
    {
        try
        {
            var scenes = new[]{Open(Route),Open(ZoneRoot+"Zone_017.unity"),Open(ZoneRoot+"Zone_018.unity"),Open(ZoneRoot+"Zone_019.unity")};
            var installed = Root(scenes[2]).Find("FixedInfrastructure_Bridge/" + BridgeObject);
            var bridges = scenes.SelectMany(s => s.GetRootGameObjects())
                .SelectMany(root => root.GetComponentsInChildren<MeshFilter>())
                .Where(f => f.sharedMesh != null && AssetDatabase.GetAssetPath(f.sharedMesh) == Dae).ToArray();
            var visual = installed.GetComponentsInChildren<MeshRenderer>();
            if (bridges.Length != 91 || visual.Length != 91 || bridges.Any(f => !f.transform.IsChildOf(installed)))
                throw new InvalidOperationException($"Expected precisely one active source model, found {bridges.Length} native mesh renderers.");
            var bounds = visual[0].bounds;
            foreach (var renderer in visual) bounds.Encapsulate(renderer.bounds);
            string[] roots = { Route, ZoneRoot+"Zone_017.unity", ZoneRoot+"Zone_018.unity", ZoneRoot+"Zone_019.unity",
                BimScene,ViewRoot+"Zone_017.prefab",ViewRoot+"Zone_018.prefab",ViewRoot+"Zone_019.prefab" };
            var legacy = AssetDatabase.GetDependencies(roots, true).Where(path =>
                Path.GetFileName(path).StartsWith("SO201_Full_", StringComparison.Ordinal) ||
                Path.GetFileName(path).StartsWith("Bridge_YTH_Visual", StringComparison.Ordinal) ||
                Path.GetFileName(path) is "ExtendedBridge.asset" or "ExistingBridgeConcrete.asset" or "FixedInfrastructure_Bridge_Combined.asset" or "BridgeWater.asset").ToArray();
            if (legacy.Length > 0) throw new InvalidOperationException("Legacy bridge references: " + string.Join(", ",legacy));
            var overlap = scenes.SelectMany(s => s.GetRootGameObjects()).SelectMany(root => root.GetComponentsInChildren<MeshRenderer>())
                .Where(renderer => renderer.enabled && !renderer.transform.IsChildOf(installed) && bounds.Intersects(renderer.bounds))
                .Select(renderer => renderer.gameObject.scene.name + "/" + renderer.name + "|" +
                    AssetDatabase.GetAssetPath(renderer.GetComponent<MeshFilter>()?.sharedMesh));
            var viewer = AssetDatabase.LoadAssetAtPath<GameObject>(ViewRoot+"Zone_018.prefab");
            if (viewer.GetComponentsInChildren<BridgeBimAdapter>(true).Length != 1 ||
                viewer.GetComponentsInChildren<BridgeBimIdentity>(true).Length != 91)
                throw new InvalidOperationException("BIM viewer does not contain exactly one detailed model.");
            File.WriteAllLines(Job+"overlap.txt",
                new[]{ "PASS: one Visual (91 native meshes), one Detailed in BIM, no transitive legacy bridge references.",
                    "Other active renderer bounds intersecting the bridge envelope:" }.Concat(overlap));
        }
        finally
        {
            foreach (var scene in ownedScenes) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
            ownedScenes.Clear();
        }
    }

    static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException(scene.path);
    }

    static Mesh MakeLoft(float start, float end, float step, Func<float,Vector3> center,
        Func<float,Vector3,Vector3[]> section, Vector3 origin, Color color, int skip = -1, Color[] sectionColors = null)
    {
        int rows = Mathf.CeilToInt((end-start)/step);
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        for (int row = 0; row <= rows; row++)
        {
            float d = Mathf.Lerp(start, end, row/(float)rows);
            var line = section(d, center(d));
            int count = line.Length;
            foreach (var point in line) vertices.Add(point-origin);
            if (row == 0) continue;
            for (int i = 0; i+1 < count; i++)
            {
                if (i == skip) continue;
                int a=(row-1)*count+i, b=a+count;
                indices.AddRange(new[]{a,b,a+1,a+1,b,b+1});
            }
        }
        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices,0);
        mesh.colors = sectionColors == null ? Enumerable.Repeat(color,vertices.Count).ToArray() :
            Enumerable.Range(0, vertices.Count).Select(i => sectionColors[i % sectionColors.Length]).ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

    static GameObject AddMesh(Scene scene, Transform parent, string name, Mesh mesh, Material material, Vector3 position)
    {
        var node = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        SceneManager.MoveGameObjectToScene(node, scene);
        node.transform.SetParent(parent, false);
        node.transform.position = position;
        node.GetComponent<MeshFilter>().sharedMesh = mesh;
        node.GetComponent<MeshRenderer>().sharedMaterial = material;
        return node;
    }

    static void RenderSandbox(Scene scene, Vector3 center, Vector3 side, Vector3 first, Vector3 last)
    {
        var light = new GameObject("Sun", typeof(Light));
        SceneManager.MoveGameObjectToScene(light, scene);
        light.transform.rotation = Quaternion.Euler(43, -65, 0);
        light.GetComponent<Light>().type = LightType.Directional;
        var views = new[]
        {
            ("SO201_Sandbox",center + side * 125 + Vector3.up * 66, center),
            ("SO201_Entry",first + side * 38 + Vector3.up * 12, first + (center-first)*.3f),
            ("SO201_Exit",last - side * 38 + Vector3.up * 12, last + (center-last)*.3f)
        };
        var camera = new GameObject("Inspection Camera",typeof(Camera)).GetComponent<Camera>();
        SceneManager.MoveGameObjectToScene(camera.gameObject,scene);
        camera.farClipPlane = 2000;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.48f,.64f,.77f);
        var target = new RenderTexture(1280, 720, 24);
        var layers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => (node:t.gameObject, layer:t.gameObject.layer)).ToArray();
        var old = RenderTexture.active;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.scene = scene;
            camera.cullingMask = 1 << 31;
            foreach (var item in layers) item.node.layer = 31;
            foreach (var (name, position, focus) in views)
            {
                camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(focus-position));
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,1280,720),0,0);
                image.Apply();
                File.WriteAllBytes(Reports + "Screenshots/" + name + ".png",image.EncodeToPNG());
            }
        }
        finally
        {
            foreach (var item in layers) item.node.layer = item.layer;
            RenderTexture.active=old;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }
}
