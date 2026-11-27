using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UttVr.BridgePilot;

[InitializeOnLoad]
static partial class SO201OriginalBridgeAudit
{
    const string Job = "Temp/SO201Original/";
    const string Reports = "Documentation/BIMExpansion/SO201OriginalBridgeAndControls/";
    const string Route = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string Pilot = "Assets/ExternalBIM/BridgeDemo/Bridge_Detailed.prefab";
    const string Dae = "Assets/ExternalBIM/BridgeDemo/Models/Bridge_SO201.dae";
    const float Spacing = 6.400022f;
    static readonly Vector3 Forward = new Vector3(-.99351375f, 0, .11371204f);

    sealed class Part
    {
        public BridgeBimEntry entry;
        public long meshId;
        public int triangles;
        public Vector3[] vertices;
        public int[] indices;
    }

    static SO201OriginalBridgeAudit() => EditorApplication.update += Watch;

    static void Watch()
    {
        string request = Job + "request.txt";
        if (!File.Exists(request) || EditorApplication.isUpdating || EditorApplication.isCompiling ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        string action = File.ReadAllText(request).Trim();
        File.Delete(request);
        try
        {
            if (action == "audit") Audit();
            if (action == "web") Build();
            if (action == "sandbox") PlaceOriginal(false);
            if (action == "install") PlaceOriginal(true);
            if (action == "overlap") AuditInstalledBridge();
            if (action == "extended-sandbox") CreateExtendedSandbox();
            if (action == "extended-web") Build(true);
            if (action == "finalization-baseline") AuditFinalizationBaseline();
            if (action == "finalization-two") CreateExtendedSandbox(2, false, true);
            if (action == "finalization-five") CreateExtendedSandbox(3, true, true);
            if (action == "finalization-seven") CreateExtendedSandbox(5, true, true);
            if (action == "finalization-stage") FinalizeExtendedBridge(false);
            if (action == "finalization-install") FinalizeExtendedBridge(true);
            if (action == "finalization-web") Build(true, true);
        }
        catch (Exception error)
        {
            File.WriteAllText(Job + action + "-error.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    static void Audit()
    {
        Directory.CreateDirectory(Reports);
        var route = SceneManager.GetSceneByPath(Route);
        bool opened = !route.isLoaded;
        if (opened) route = EditorSceneManager.OpenScene(Route, OpenSceneMode.Additive);
        var pilot = PrefabUtility.LoadPrefabContents(Pilot);
        try
        {
            var parts = ReadParts(pilot);
            if (parts.Length != 91 || parts.Sum(part => part.triangles) != 36099)
                throw new InvalidOperationException("Expected 91 distinct native meshes and 36,099 triangles.");
            WriteMapping(parts);
            var roots = route.GetRootGameObjects();
            var manager = roots.SelectMany(root => root.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var chunks = roots.SelectMany(root => root.GetComponentsInChildren<RailwayMicroChunkStreamer>(true)).Single().data.chunks;
            var mover = manager.trainMover;
            var spline = mover.splineRoute;
            var length = spline.CalculateLength();
            var zone = manager.zones.Single(entry => entry.zoneId == "Zone_018");
            var pavement = parts.Where(part => part.entry.ifcClass == "IfcPavement").ToArray();
            var deck = BoundsOf(pavement.SelectMany(part => part.vertices));
            var envelope = TrainBounds(mover);
            var results = new List<string>
            {
                FormattableString.Invariant($"91 meshes, 36,099 triangles. Pavement original width={deck.size.x:F3}; length={deck.size.z:F3}; y={deck.min.y:F3}..{deck.max.y:F3}. Train frame={envelope.min:F3}..{envelope.max:F3}."),
                "Scale,Center,Start,End,ChordOffset,TrainSweepHalfWidth,RailBottom,DeckY,UnsupportedSamples,MaxDeckGap,InterferingParts"
            };
            Vector3 Position(float d) => spline.EvaluatePosition(d / length);
            Vector3 Side(float d) => Vector3.Cross(Vector3.up, (Vector3)spline.EvaluateTangent(d / length)).normalized;
            Vector3 Mid(float d) => Position(d) - Side(d) * Spacing * .5f;

            foreach (float scale in new[] { ScaleFactor, 2.2f, 1.8f }.Distinct())
            {
                float half = deck.size.z * scale / 2;
                var options = new List<(float error, float station, Vector3 origin, Quaternion rotation)>();
                for (float center = zone.startChainage + half + 20; center < zone.endChainage - half - 20; center += 5)
                {
                    var from = Mid(center - half);
                    var to = Mid(center + half);
                    var rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(to - from, Vector3.up));
                    var midpoint = (from + to) * .5f;
                    var right = rotation * Vector3.right;
                    var max = 0f;
                    for (float d = center - half; d <= center + half; d += 2)
                        max = Mathf.Max(max, Mathf.Abs(Vector3.Dot(Mid(d) - midpoint, right)));
                    options.Add((max, center, midpoint, rotation));
                }
                foreach (var option in options.OrderBy(item => item.error).Take(3))
                {
                    float start = option.station - half, end = option.station + half;
                    var railBottom = chunks.Where(c => c.end > start && c.start < end)
                        .SelectMany(c => new[] { c.right, c.left })
                        .Min(track =>
                        {
                            var mesh = Resources.Load<Mesh>(track.meshResource);
                            return Corners(mesh.bounds).Min(v => (track.position + track.rotation * Vector3.Scale(v, track.scale)).y);
                        });
                    float sweep = Spacing * .5f + Mathf.Max(Mathf.Abs(envelope.min.x), Mathf.Abs(envelope.max.x)) + option.error;
                    var origin = option.origin;
                    origin.y = railBottom - deck.max.y * scale - .025f;
                    var inverse = Quaternion.Inverse(option.rotation);
                    var deckTop = railBottom - .025f;
                    var missing = 0;
                    float gap = 0;
                    for (float offset = -half + 2; offset < half - 2; offset += 2)
                    {
                        float d = option.station + offset;
                        var local = inverse * (Mid(d) - origin);
                        foreach (float x in new[] {local.x - Spacing*.5f - .9f, local.x - Spacing*.5f + .9f, local.x + Spacing*.5f - .9f, local.x + Spacing*.5f + .9f})
                        {
                            float y = pavement.Max(part => PavementY(part, x / scale + deck.center.x, local.z / scale + deck.center.z));
                            if (float.IsNegativeInfinity(y)) missing++;
                            else gap = Mathf.Max(gap, railBottom - (origin.y + y * scale));
                        }
                    }
                    var obstructions = parts.Except(pavement).Where(part =>
                        Intersects(part, deck.center, scale, sweep + .1f, (railBottom - origin.y) + .01f,
                            (railBottom - origin.y) + envelope.size.y + .4f)).Select(part => part.entry.ifcClass + ":" + part.entry.globalId);
                    results.Add(FormattableString.Invariant($"{scale:F2},{option.station:F3},{start:F3},{end:F3},{option.error:F3},{sweep:F3},{railBottom:F3},{deckTop:F3},{missing},{gap:F3},\"{string.Join("|", obstructions)}\""));
                }
            }
            File.WriteAllLines(Job + "geometry.txt", results);
            File.WriteAllLines(Job + "foundations.txt", parts.Where(part => part.entry.Label.StartsWith("Pile cap ", StringComparison.Ordinal) ||
                part.entry.Label.StartsWith("Pier stem ", StringComparison.Ordinal) || part.entry.ifcClass == "IfcPile")
                .Select(part =>
                {
                    var bounds = BoundsOf(part.vertices);
                    return FormattableString.Invariant($"{part.entry.Label}|{bounds.min.y:F3}|{bounds.max.y:F3}|{bounds.center.z - deck.center.z:F3}");
                }));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(pilot);
            if (opened) EditorSceneManager.CloseScene(route, true);
        }
    }

    static Part[] ReadParts(GameObject root)
    {
        var inverse = Quaternion.Inverse(Quaternion.LookRotation(Forward));
        var entries = root.GetComponent<BridgeBimAdapter>().catalog.entries.ToDictionary(entry => entry.globalId);
        var parts = root.GetComponentsInChildren<BridgeBimIdentity>(true).Select(identity =>
        {
            var filter = identity.Renderers.Single().GetComponent<MeshFilter>();
            var mesh = filter.sharedMesh;
            if (AssetDatabase.GetAssetPath(mesh) != Dae ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string _, out long localId))
                throw new InvalidOperationException("Not a native DAE mesh: " + identity.GlobalId);
            var indices = mesh.triangles;
            return new Part
            {
                entry = entries[identity.GlobalId], meshId = localId, triangles = indices.Length / 3,
                vertices = mesh.vertices.Select(v => inverse * filter.transform.TransformPoint(v)).ToArray(),
                indices = indices
            };
        }).ToArray();
        if (parts.Select(item => item.meshId).Distinct().Count() != parts.Length)
            throw new InvalidOperationException("Source mesh IDs are not unique.");
        return parts;
    }

    static float PavementY(Part part, float x, float z)
    {
        float y = float.NegativeInfinity;
        for (int t = 0; t < part.indices.Length; t += 3)
        {
            var a = part.vertices[part.indices[t]];
            var b = part.vertices[part.indices[t+1]] - a;
            var c = part.vertices[part.indices[t+2]] - a;
            float det = b.x * c.z - c.x * b.z;
            if (Mathf.Abs(det) < .000001f) continue;
            float u = ((x-a.x)*c.z - (z-a.z)*c.x) / det;
            float v = (b.x*(z-a.z) - b.z*(x-a.x)) / det;
            if (u >= -.0001f && v >= -.0001f && u + v <= 1.0001f) y = Mathf.Max(y, a.y + u*b.y + v*c.y);
        }
        return y;
    }

    static bool Intersects(Part part, Vector3 center, float scale, float halfWidth, float minY, float maxY)
    {
        for (int t = 0; t < part.indices.Length; t += 3)
        {
            var a = part.vertices[part.indices[t]];
            var b = part.vertices[part.indices[t+1]];
            var c = part.vertices[part.indices[t+2]];
            if (Mathf.Max(a.y,b.y,c.y)*scale < minY || Mathf.Min(a.y,b.y,c.y)*scale > maxY ||
                (Mathf.Min(a.x,b.x,c.x)-center.x)*scale > halfWidth ||
                (Mathf.Max(a.x,b.x,c.x)-center.x)*scale < -halfWidth) continue;
            return true;
        }
        return false;
    }

    static Bounds TrainBounds(TrainSplineMover train)
    {
        return BoundsOf(new[] { train.frontCar, train.rearCar }.SelectMany(car =>
        {
            var inverse = Matrix4x4.TRS(car.position, car.rotation, Vector3.one).inverse;
            return car.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled)
                .SelectMany(r => Corners(r.localBounds).Select(v => inverse.MultiplyPoint3x4(r.localToWorldMatrix.MultiplyPoint3x4(v))));
        }));
    }

    static Bounds BoundsOf(IEnumerable<Vector3> points)
    {
        using var items = points.GetEnumerator();
        if (!items.MoveNext()) throw new InvalidOperationException("No geometry for bounds.");
        var bounds = new Bounds(items.Current, Vector3.zero);
        while (items.MoveNext()) bounds.Encapsulate(items.Current);
        return bounds;
    }

    static IEnumerable<Vector3> Corners(Bounds bounds)
    {
        for (int i = 0; i < 8; i++)
            yield return bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1,(i & 2) == 0 ? -1 : 1,(i & 4) == 0 ? -1 : 1));
    }

    static void WriteMapping(IEnumerable<Part> parts)
    {
        string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        var rows = parts.OrderBy(part => part.entry.assetId).Select(part =>
            string.Join(",", Csv(part.entry.assetId), Csv(part.entry.globalId), Csv(part.entry.parentGlobalId),
                Csv(part.entry.ifcClass), Csv(part.entry.Label), Csv(Dae), part.meshId.ToString(), part.triangles.ToString()));
        File.WriteAllLines(Reports + "IFC_Asset_Mapping.csv",
            new[] { "AssetID,GlobalId,ParentGlobalId,IfcClass,Name,MeshAsset,MeshLocalFileID,Triangles" }.Concat(rows));
    }

    static void Build(bool extendedAudit = false, bool finalization = false)
    {
        string statusPath = Job + (finalization ? "finalization-web.txt" : extendedAudit ? "extended-web.txt" : "web.txt");
        if (finalization && (!File.Exists(FinalReports + "Installed_Bridge.txt") ||
            !File.ReadAllText(ZoneRoot + "Zone_018.unity").Contains(FinalBridgeObject, StringComparison.Ordinal)))
            throw new InvalidOperationException("The build scene does not contain the installed seven-span bridge.");
        File.WriteAllText(statusPath, "Building");
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length == 0 || Path.GetFileNameWithoutExtension(scenes[0]) != "Login" || !scenes.Contains(Route))
            throw new InvalidOperationException("Unexpected Web scene list.");
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes, locationPathName = finalization ? "Builds/UTTVR_SO201_SevenSpan_Web" :
                extendedAudit ? "Builds/UTTVR_SO201_Extended_Web" : "Builds/UTTVR_SO201_Original_Controls",
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL, options = BuildOptions.Development,
            extraScriptingDefines = new[] { "BRIDGE_MODULAR_AUDIT" }
        }).summary;
        File.WriteAllText(statusPath,
            $"{result.result}; {result.totalErrors} errors; {result.totalWarnings} warnings; {result.totalSize} bytes; {result.totalTime}");
        if (result.result != BuildResult.Succeeded) throw new InvalidOperationException("Web build failed.");
    }
}
