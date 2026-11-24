using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UttVr.BridgePilot;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SO201RailwayCandidate
{
    const string Work = "Temp/BridgeSO201Route/";
    const string Reports = "Documentation/BIMExpansion/SO201RailwayReuse/";
    const string Output = "Assets/ExternalBIM/SO201RouteCandidate/";
    const string Pilot = "Assets/ExternalBIM/BridgeDemo/BridgeBIM_Demo.prefab";
    const string Dae = "Assets/ExternalBIM/BridgeDemo/Models/Bridge_SO201.dae";
    const string Route = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string Zone = "Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_018.unity";
    const string Modular = "Assets/ExternalBIM/BridgeModular/Bridge_YTH_Visual.prefab";
    const string FloorMaterial = "Assets/ProjectLopHoc/Generated/QuestRailway/RailwayFlatColour.mat";
    const float Span = 17.5f, Width = 14f, RailSpacing = 6.400022f, FoundationBottom = -18f;
    const float SupportOffset = 4.10f;
    const int SampleFirstBay = 31, SampleBayCount = 2;

    const string Slab = "1rynT78KU8V45XMmnyMFss", Diaphragm = "00TgNFCH9HGoplDgAwRwDO";
    const string Stem = "0Fx3Ykz9Zoo5T0r0sZtWPp", Cap = "2T12tBKsv4vL2Pc9ijqqSu";
    const string Blinding = "0qpx6Ycve5tLEkuyiBy94y";
    static readonly string[] Seats = { "0vpwLjckQCzsA1Wvzwc8EX", "3RkpYxGpfcYT2I1t_zwJ_Z" };
    static readonly string[] Girders = { "1HfwXIbME2yQxwO10KnFqY", "1M2Dt6zBVBF$17xO1ZrlWq",
        "2jnx$V1Bq1aR$OfEx$ULOH", "35_$at319vYpeinIpxhdix", "28v_6QK2bjMvQbMN44eCdk" };
    static readonly Vector3 SourceForward = new Vector3(-.99351375f, 0, .11371204f);

    sealed class SourcePart
    {
        public BridgeBimEntry entry;
        public MeshFilter filter;
        public MeshRenderer renderer;
        public Bounds physical;
        public long fileId;
        public int triangles;
    }

    sealed class Occurrence
    {
        public string id, role, group, sourceId, path;
        public long fileId;
        public int triangles;
        public float station;
    }

    static SO201RailwayCandidate() => EditorApplication.update += Dispatch;

    static void Dispatch()
    {
        if (!File.Exists(Work + "request.txt") || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        string action = File.ReadAllText(Work + "request.txt").Trim();
        File.Delete(Work + "request.txt");
        try
        {
            if (action == "sample") Create(false);
            else if (action == "full") Create(true);
            else if (action == "refit-sample") Create(false,true);
            else if (action == "refit-full") Create(true,true);
            else if (action == "pictures") Recapture();
            else if (action == "optimize") OptimizeVisual();
            else if (action == "web") BuildCandidate();
        }
        catch (Exception error)
        {
            File.WriteAllText(Work + action + "-error.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/UTTVR/Bridge SO201/Create railway sandbox")]
    public static void Sample() => Create(false);

    static void Create(bool full, bool refit = false)
    {
        Directory.CreateDirectory(Reports + "Screenshots");
        Directory.CreateDirectory(Work);
        if (full && !File.Exists(Reports + "Sample_Measurements.txt"))
            throw new InvalidOperationException("Validate the sandbox sample first.");
        if (!AssetDatabase.IsValidFolder(Output.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets/ExternalBIM", "SO201RouteCandidate");
        if (!AssetDatabase.IsValidFolder(Output + "Meshes"))
            AssetDatabase.CreateFolder(Output.TrimEnd('/'), "Meshes");
        string label = full ? "Full" : "Sample";
        if (!refit && AssetDatabase.LoadAssetAtPath<GameObject>(Output + "SO201_" + label + "_Source.prefab") != null)
            throw new InvalidOperationException("The candidate already exists. Preserve the saved evidence before regenerating.");
        var previous = SceneManager.GetActiveScene();
        var route = SceneManager.GetSceneByPath(Route);
        bool openedRoute = !route.isLoaded;
        if (openedRoute) route = EditorSceneManager.OpenScene(Route, OpenSceneMode.Additive);
        var pilot = PrefabUtility.LoadPrefabContents(Pilot);
        var sandbox = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(sandbox);
        try
        {
            var authoredCatalog = pilot.GetComponent<BridgeBimAdapter>().catalog;
            var sourceBasis = Quaternion.LookRotation(SourceForward);
            var sourceInverse = Quaternion.Inverse(sourceBasis);
            var parts = LoadSources(pilot, sourceInverse);
            if (parts.Count != 91 || parts.Values.Sum(p => p.triangles) != 36099)
                throw new InvalidOperationException("The SO 201 source differs from the verified 91-mesh import.");
            WriteInventory(parts);
            var manager = route.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var railway = route.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RailwayMicroChunkStreamer>(true)).Single().data;
            var bridge = manager.fixedInfrastructure.Single(entry => entry.id == "Bridge_Main");
            var spline = manager.trainMover.splineRoute;
            float length = spline.CalculateLength();
            const int totalBays = 64;
            if (Mathf.Abs(bridge.endChainage - bridge.startChainage - Span * totalBays) > .005f)
                throw new InvalidOperationException("The authored route no longer matches the 1,120 m bridge footprint.");
            int first = full ? 0 : SampleFirstBay, count = full ? totalBays : SampleBayCount;
            float start = bridge.startChainage + first * Span;
            float end = start + count * Span;

            Vector3 Position(float d) => spline.EvaluatePosition(d / length);
            Vector3 Right(float d) => Vector3.Cross(Vector3.up, (Vector3)spline.EvaluateTangent(d / length)).normalized;
            Vector3 Center(float d) => Position(d) - Right(d) * (RailSpacing / 2);
            Vector3 origin = Center((start + end) / 2);
            var railChunks = railway.chunks.Where(c => c.end > start && c.start < end).ToArray();
            var railMeshes = railChunks.SelectMany(c => new[] { c.right, c.left })
                .Select(t => (Track: t, Mesh: Resources.Load<Mesh>(t.meshResource))).ToArray();
            float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
            foreach (var pair in railMeshes)
            {
                if (pair.Mesh == null) throw new IOException(pair.Track.meshResource);
                var toWorld = Matrix4x4.TRS(pair.Track.position, pair.Track.rotation, pair.Track.scale);
                float bottom = Corners(pair.Mesh.bounds).Min(v => toWorld.MultiplyPoint3x4(v).y);
                lowest = Mathf.Min(lowest, bottom);
                highest = Mathf.Max(highest, bottom);
            }
            float top = lowest - .002f;
            var sourceRoot = new GameObject("SO201_" + label + "_Source");
            var bindings = new List<Occurrence>();
            var targetById = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var groups = new List<BridgeBimEntry>();
            var flat = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterial);
            float peakChordOffset = 0;
            float peakNativeTransformError = 0;
            float nativeHighest = float.NegativeInfinity;
            float seam = 0;
            Vector3[] priorEnd = null;
            Vector3 Placed(float station, float lateral, float y)
            {
                var v = Center(station) + Right(station) * lateral;
                v.y = y;
                return v - origin;
            }
            Quaternion Yaw(float station) => Quaternion.LookRotation(Vector3.Cross(Right(station), Vector3.up));
            Transform Group(string key, string title)
            {
                var group = new GameObject(title).transform;
                group.SetParent(sourceRoot.transform, false);
                groups.Add(new BridgeBimEntry { globalId = key, name = title, propertyLines = Array.Empty<string>() });
                return group;
            }

            void TrackPart(Transform group, GameObject go, string key, string role, string groupId,
                float station, SourcePart src = null)
            {
                string id = "YTH-SO201-" + key;
                string path = group.name + "/" + go.name;
                if (!targetById.TryAdd(id, go)) throw new InvalidOperationException("Duplicate occurrence " + id);
                bindings.Add(new Occurrence { id=id, role=role, group=groupId, station=station, path=path,
                    sourceId=src?.entry.globalId, fileId=src?.fileId ?? 0,
                    triangles=src?.triangles ?? Triangles(go.GetComponent<MeshFilter>().sharedMesh) });
            }

            void Native(Transform group, string key, string name, string role, string groupId,
                float station, float lateral, float targetTop, Quaternion yaw, string sourceId)
            {
                var src = parts[sourceId];
                var pivot = new Vector3(src.physical.center.x, src.physical.max.y, src.physical.center.z);
                var rotation = yaw * sourceInverse;
                var target = Placed(station, lateral, targetTop);
                var go = new GameObject(name);
                go.transform.SetParent(group, false);
                go.transform.SetPositionAndRotation(target + rotation * (src.filter.transform.position - sourceBasis * pivot),
                    rotation * src.filter.transform.rotation);
                go.transform.localScale = src.filter.transform.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = src.filter.sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = src.renderer.sharedMaterials;
                foreach (var vertex in src.filter.sharedMesh.vertices)
                {
                    var expected = target + rotation * (src.filter.transform.TransformPoint(vertex) - sourceBasis * pivot);
                    var actual = go.transform.TransformPoint(vertex);
                    peakNativeTransformError = Mathf.Max(peakNativeTransformError, (actual-expected).magnitude);
                    nativeHighest = Mathf.Max(nativeHighest, actual.y + origin.y);
                }
                TrackPart(group, go, key, role, groupId, station, src);
            }

            void Added(Transform group, string key, string name, string role, string groupId, float station, Mesh mesh)
            {
                var go = new GameObject(name);
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = flat;
                TrackPart(group, go, key, role, groupId, station);
            }

            Mesh Member(string key, float a, float b, float lateral, float width, float bottom, float crown, Color color)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt((b-a) / 1.75f));
                Vector3[] Ring(float d) => new[]
                {
                    Placed(d,lateral-width/2,bottom), Placed(d,lateral-width/2,crown),
                    Placed(d,lateral+width/2,crown),  Placed(d,lateral+width/2,bottom)
                };
                var rings = Enumerable.Range(0,n+1).Select(i => Ring(Mathf.Lerp(a,b,i/(float)n))).ToArray();
                if (key.EndsWith("-CAP", StringComparison.Ordinal))
                {
                    if (priorEnd != null)
                        for (int k=0;k<4;k++) seam = Mathf.Max(seam,Vector3.Distance(priorEnd[k],rings[0][k]));
                    priorEnd = rings[n];
                }
                return MeshAsset(label + "_" + key, rings, color);
            }

            Mesh Support(string key, float station, float lateral, float width, float depth, float bottom, float crown, Color color)
            {
                var center = Placed(station,lateral,bottom);
                var right = Right(station);
                var forward = Vector3.Cross(right, Vector3.up);
                Vector3[] Ring(float z)
                {
                    var p=center+forward*z;
                    return new[] { p-right*(width/2), p-right*(width/2)+Vector3.up*(crown-bottom),
                        p+right*(width/2)+Vector3.up*(crown-bottom), p+right*(width/2) };
                }
                return MeshAsset(label + "_" + key, new[] {Ring(-depth/2),Ring(depth/2)}, color);
            }

            var concrete = new Color(.56f,.54f,.51f);
            for (int i=first; i<first+count; i++)
            {
                string bay = $"B{i+1:00}";
                string groupId = "so201/" + bay;
                var group = Group(groupId, $"Nhịp {i+1:00}");
                float a = bridge.startChainage + i*Span, b = bridge.startChainage + (i+1)*Span, mid=(a+b)/2;
                var chord = Center(b)-Center(a);
                var yaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(chord, Vector3.up));
                for (int j=0; j<=10; j++)
                {
                    float t=j/10f;
                    peakChordOffset=Mathf.Max(peakChordOffset,Vector3.Distance(Center(Mathf.Lerp(a,b,t)),Vector3.Lerp(Center(a),Center(b),t)));
                }
                Native(group,bay+"-SLAB","Tấm bê tông SO 201","SLAB",groupId,mid,0,top-.06f,yaw,Slab);
                float girderTop = top-.06f-parts[Slab].physical.size.y+.075f;
                for (int j=0;j<Girders.Length;j++)
                    Native(group,$"{bay}-GIRDER-{j+1}",$"Dầm SO 201 {j+1}","GIRDER",groupId,mid,(j-2)*2.45f,girderTop,yaw,Girders[j]);
                Added(group,bay+"-CAP","Sàn ray đôi","RAIL_DECK",groupId,mid,Member(bay+"-CAP",a,b,0,Width,top-.28f,top,concrete));
                foreach (int side in new[] {-1,1})
                {
                    string key=bay+(side<0?"-GUARD-L":"-GUARD-R");
                    Added(group,key,side<0?"Lan can trái":"Lan can phải","GUARD",groupId,mid,
                        Member(key,a,b,side*6.83f,.14f,top,top+1.05f,new Color(.64f,.65f,.66f)));
                }
            }

            for (int i=first;i<=first+count;i++)
            {
                string pier = $"P{i:00}", groupId = "so201/" + pier;
                var group = Group(groupId, $"Gối đỡ {i:00}");
                float station = bridge.startChainage + i*Span;
                var yaw = Yaw(station);
                float diaphragmTop=top-.06f, headTop=diaphragmTop-parts[Diaphragm].physical.size.y+.018f;
                float headBottom=headTop-.72f;
                // End diaphragm and cap beam remain inside the owner bridge footprint.
                float endShift = i==0 ? 1.05f : i==totalBays ? -1.05f : 0;
                Native(group,pier+"-DIAPHRAGM","Dầm ngang SO 201","DIAPHRAGM",groupId,station+endShift,0,diaphragmTop,yaw,Diaphragm);
                Added(group,pier+"-HEAD","Xà mũ ray đôi","CROSSHEAD",groupId,station,
                    Support(pier+"-HEAD",station+endShift,0,13.4f,2.05f,headBottom,headTop,concrete));
                foreach (int side in new[]{-1,1})
                {
                    string key=pier+(side<0?"-L":"-R");
                    float x=side*SupportOffset;
                    for (int j=0;j<Seats.Length;j++)
                        Native(group,$"{key}-PLINTH-{j+1}",$"Bệ gối SO 201 {side} {j+1}","PLINTH",groupId,
                            station+endShift,x+(j==0?2.075f:-2.075f),headBottom+.005f,yaw,Seats[j]);
                    float stemTop=headBottom-parts[Seats[0]].physical.size.y+.012f;
                    float stemBottom=stemTop-parts[Stem].physical.size.y;
                    float capTop=FoundationBottom+parts[Cap].physical.size.y;
                    Native(group,key+"-STEM",$"Thân trụ SO 201 {side}","STEM",groupId,station+endShift,x,stemTop,yaw,Stem);
                    Native(group,key+"-FOUND",$"Đài móng SO 201 {side}","PILE_CAP",groupId,station+endShift,x,capTop,yaw,Cap);
                    Native(group,key+"-BLIND",$"Bê tông lót SO 201 {side}","BLINDING",groupId,station+endShift,x,FoundationBottom,yaw,Blinding);
                    Added(group,key+"-SHAFT",$"Đoạn nối thân trụ {side}","SHAFT_EXTENSION",groupId,station,
                        Support(key+"-SHAFT",station+endShift,x,1.73f,1.47f,capTop-.015f,stemBottom+.025f,concrete));
                }
            }

            float clearance = TrainClearance(manager.trainMover, start, end, Position, Center, Right);
            float foundationGap = 2*SupportOffset - parts[Cap].physical.size.x;
            float blindingGap = 2*SupportOffset - parts[Blinding].physical.size.x;
            if (seam > .003f || peakChordOffset > .12f || clearance < 1f || foundationGap < .08f || blindingGap < .08f ||
                float.IsInfinity(lowest) || highest-lowest > .04f || nativeHighest > top-.01f || peakNativeTransformError > .003f)
                throw new InvalidOperationException(FormattableString.Invariant(
                    $"Geometry gate failed: seam={seam}, chord={peakChordOffset}, clearance={clearance}, footing gap={foundationGap}, blinding gap={blindingGap}, rail={lowest}..{highest}, source maxY={nativeHighest}, mesh registration={peakNativeTransformError}."));

            SavePresentations(sourceRoot, sandbox, origin, label, bindings, groups, parts, authoredCatalog,
                pilot.GetComponent<BridgeBimAdapter>().highlightMaterial, out var detailed);
            Object.DestroyImmediate(sourceRoot);
            WriteBindings(label, bindings);
            if (!full) SaveSample(sandbox, detailed, railMeshes, origin, Center((bridge.startChainage+bridge.endChainage)/2)+Right((bridge.startChainage+bridge.endChainage)/2)*(RailSpacing/2),
                Yaw((start+end)/2));
            AssetDatabase.SaveAssets();
            int nativeTris=bindings.Where(x=>x.fileId!=0).Sum(x=>x.triangles);
            int totalTris=bindings.Sum(x=>x.triangles);
            File.WriteAllText(Reports + label + "_Measurements.txt", FormattableString.Invariant(
                $"PASS geometry; start_m={start:F4}; end_m={end:F4}; spans={count}; supports={count+1}; source_occurrences={bindings.Count(x=>x.fileId!=0)}; all_occurrences={bindings.Count}; original_triangles={nativeTris}; all_triangles={totalTris}; deck_width_m={Width:F4}; track_centers_m={RailSpacing:F6}; rail_mesh_pairs={railChunks.Length}; rail_bottom_min_m={lowest:F5}; rail_bottom_max_m={highest:F5}; deck_top_m={top:F5}; native_top_max_m={nativeHighest:F5}; seam_error_m={seam:F6}; max_chord_error_m={peakChordOffset:F5}; train_side_clearance_m={clearance:F4}; footing_gap_m={foundationGap:F4}; blinding_gap_m={blindingGap:F4}; max_rigid_vertex_error_m={peakNativeTransformError:F6}; origin={origin.x:R}|{origin.y:R}|{origin.z:R}"));
            File.WriteAllText(Work + label + "-ready.txt", "Completed and verified");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(sandbox,true);
            PrefabUtility.UnloadPrefabContents(pilot);
            if (openedRoute) EditorSceneManager.CloseScene(route,true);
        }
    }

    static Dictionary<string,SourcePart> LoadSources(GameObject pilot, Quaternion inverse)
    {
        var catalog = pilot.GetComponent<BridgeBimAdapter>().catalog;
        var entries = catalog.entries.ToDictionary(x=>x.globalId,StringComparer.Ordinal);
        var sources = new Dictionary<string,SourcePart>(StringComparer.Ordinal);
        foreach (var id in pilot.GetComponentsInChildren<BridgeBimIdentity>(true))
        {
            var renderer=(MeshRenderer)id.Renderers.Single();
            var filter=renderer.GetComponent<MeshFilter>();
            var mesh=filter.sharedMesh;
            if (AssetDatabase.GetAssetPath(mesh) != Dae ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string _,out long fileId))
                throw new InvalidOperationException("Not a SO 201 subasset: " + id.GlobalId);
            var points=mesh.vertices.Select(v=>inverse*filter.transform.TransformPoint(v)).ToArray();
            var bounds=new Bounds(points[0],Vector3.zero);
            foreach(var point in points) bounds.Encapsulate(point);
            sources.Add(id.GlobalId,new SourcePart { entry=entries[id.GlobalId], renderer=renderer, filter=filter,
                physical=bounds, fileId=fileId, triangles=Triangles(mesh) });
        }
        if (sources.Values.Select(p=>p.fileId).Distinct().Count()!=sources.Count)
            throw new InvalidOperationException("Expected a distinct DAE mesh for each IFC product.");
        return sources;
    }

    static IEnumerable<Vector3> Corners(Bounds bounds)
    {
        var e=bounds.extents;
        for(int i=0;i<8;i++)
            yield return bounds.center+new Vector3((i&1)==0?-e.x:e.x,(i&2)==0?-e.y:e.y,(i&4)==0?-e.z:e.z);
    }

    static float TrainClearance(TrainSplineMover train, float start, float end, Func<float,Vector3> route,
        Func<float,Vector3> center, Func<float,Vector3> right)
    {
        var body = new List<Vector3>();
        foreach (var car in new[] {train.frontCar,train.rearCar})
        {
            var toFrame=Matrix4x4.TRS(car.position,car.rotation,Vector3.one).inverse;
            foreach(var renderer in car.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled))
            {
                var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if(mesh!=null)body.AddRange(Corners(mesh.bounds).Select(v=>toFrame.MultiplyPoint3x4(renderer.localToWorldMatrix.MultiplyPoint3x4(v))));
            }
        }
        if(body.Count==0)throw new InvalidOperationException("Train envelope not found.");
        var envelope=new Bounds(body[0],Vector3.zero);
        foreach(var point in body)envelope.Encapsulate(point);
        float margin=float.PositiveInfinity;
        for (float d=start;d<=end+.01f;d+=.5f)
        {
            var side=right(d);
            var forward=Vector3.Cross(side,Vector3.up);
            foreach(float track in new[] {0f,-RailSpacing})
                foreach(var corner in Corners(envelope))
                {
                    float s=d+corner.z;
                    var p=route(d)+side*(track+corner.x)+forward*corner.z;
                    margin=Mathf.Min(margin,6.76f-Mathf.Abs(Vector3.Dot(p-center(s),right(s))));
                }
        }
        return margin;
    }

    static int Triangles(Mesh mesh) =>
        Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(int)mesh.GetIndexCount(i)/3);

    static Mesh MeshAsset(string name, IReadOnlyList<Vector3[]> rings, Color color)
    {
        var pts=new List<Vector3>();
        var tris=new List<int>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int i=pts.Count;
            pts.AddRange(new[]{a,b,c,d});
            tris.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
        }
        for(int q=0;q<rings.Count-1;q++)
            for(int j=0;j<4;j++)
                Quad(rings[q][j],rings[q+1][j],rings[q+1][(j+1)%4],rings[q][(j+1)%4]);
        var a=rings[0];var b=rings[rings.Count-1];
        Quad(a[0],a[1],a[2],a[3]);Quad(b[3],b[2],b[1],b[0]);
        var mesh=new Mesh{name=name};
        mesh.SetVertices(pts);
        mesh.SetTriangles(tris,0);
        mesh.SetColors(Enumerable.Repeat(color,pts.Count).ToList());
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        string path=Output+"Meshes/"+name+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null)AssetDatabase.CreateAsset(mesh,path);
        else { EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing; }
        return mesh;
    }

    static void SavePresentations(GameObject source, Scene scene, Vector3 origin, string label,
        IReadOnlyList<Occurrence> bindings, IEnumerable<BridgeBimEntry> groups, Dictionary<string,SourcePart> parts,
        BridgeBimCatalog pilot, Material highlight, out GameObject detailed)
    {
        var prefab=PrefabUtility.SaveAsPrefabAsset(source,Output+$"SO201_{label}_Source.prefab");
        if(prefab==null)throw new IOException("Could not save candidate source.");
        GameObject Preview(string variant)
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            instance.name=$"SO201_{label}_{variant}";
            return instance;
        }
        var visual=Preview("Visual");
        if(PrefabUtility.SaveAsPrefabAsset(visual,Output+visual.name+".prefab")==null)throw new IOException(visual.name);
        Object.DestroyImmediate(visual);
        var catalog=ScriptableObject.CreateInstance<BridgeBimCatalog>();
        catalog.schema="YTH-SO201-DERIVED";
        catalog.sourceSha256=pilot.sourceSha256;
        catalog.entries=groups.Concat(bindings.Select(o=>
        {
            var refEntry=o.sourceId!=null?parts[o.sourceId].entry:null;
            return new BridgeBimEntry { globalId=o.id, assetId=o.id, parentGlobalId=o.group, ifcClass=o.role,
                name=o.path.Substring(o.path.LastIndexOf('/')+1), description=refEntry?.description,
                propertyLines=new[]
                {
                    refEntry==null?"Hình học bổ sung cho mô hình tuyến":$"IFC nguồn: {refEntry.ifcClass} · {refEntry.Label} · {o.sourceId}",
                    FormattableString.Invariant($"Lý trình theo mô hình: {o.station:F3} m"),
                    "Hồ sơ kỹ thuật và vòng đời của cấu kiện trên tuyến: Chưa có dữ liệu"
                } };
        })).ToArray();
        string catalogPath=Output+$"SO201_{label}_Catalog.asset";
        var savedCatalog=AssetDatabase.LoadAssetAtPath<BridgeBimCatalog>(catalogPath);
        if(savedCatalog==null)AssetDatabase.CreateAsset(catalog,catalogPath);
        else
        {
            EditorUtility.CopySerialized(catalog,savedCatalog);
            Object.DestroyImmediate(catalog);
            catalog=savedCatalog;
        }
        detailed=Preview("Detailed");
        var adapter=detailed.AddComponent<BridgeBimAdapter>();
        adapter.catalog=catalog;
        adapter.highlightMaterial=highlight;
        foreach(var row in bindings)
        {
            var target=detailed.transform.Find(row.path).gameObject;
            var filter=target.GetComponent<MeshFilter>();
            target.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            target.AddComponent<BridgeBimIdentity>().Configure(row.id,new Renderer[]{target.GetComponent<MeshRenderer>()});
            if(row.fileId!=0 && (AssetDatabase.GetAssetPath(filter.sharedMesh)!=Dae ||
               !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.sharedMesh,out string _,out long id) || id!=row.fileId))
                throw new InvalidOperationException("Lost source provenance at " + row.id);
        }
        if(PrefabUtility.SaveAsPrefabAssetAndConnect(detailed,Output+detailed.name+".prefab",InteractionMode.AutomatedAction)==null)
            throw new IOException(detailed.name);
        File.WriteAllText(Reports+label+"_Placement.txt",FormattableString.Invariant(
            $"World origin XYZ: {origin.x:R}, {origin.y:R}, {origin.z:R}; source prefab root uses local offsets in route axes."));
    }

    static string Csv(params object[] row) => string.Join(",",row.Select(x=>"\""+(Convert.ToString(x,CultureInfo.InvariantCulture)??"").Replace("\"","\"\"")+"\""));

    static void WriteInventory(Dictionary<string,SourcePart> items)
    {
        var lines=new List<string>{"SourceGlobalId,IfcClass,Name,ParentGlobalId,UnityMesh,DAEFileId,Triangles,Length_m,Width_m,Height_m,Disposition,Explanation"};
        foreach(var part in items.Values.OrderBy(p=>p.entry.ifcClass).ThenBy(p=>p.entry.Label,StringComparer.Ordinal))
        {
            var e=part.entry;
            var size=part.physical.size;
            var selected=e.globalId==Slab || e.globalId==Diaphragm || e.globalId==Stem || e.globalId==Cap ||
                e.globalId==Blinding || Seats.Contains(e.globalId) || Girders.Contains(e.globalId);
            string status,reason;
            if(selected){status="Sử dụng trực tiếp";reason="Subasset DAE giữ nguyên, đặt rigid theo nhịp hoặc gối.";}
            else if(e.ifcClass is "IfcPavement" or "IfcEarthworksFill" or "IfcCovering" or "IfcPipeSegment" ||
                e.Label.Contains("Approach slab") || e.Label.Contains("Cornices"))
            {status="Không phù hợp";reason="Mặt đường bộ, lớp phụ hoặc chi tiết mố cần thiết kế riêng cho ray đôi.";}
            else if(e.ifcClass=="IfcPile")
            {status="Cần chỉnh";reason="Cọc tiềm năng ở dưới nền, cần địa chất và bố trí nhóm cọc; chưa có trong ứng viên.";}
            else if(e.ifcClass=="IfcWall" || e.Label.Contains("O1") || e.Label.Contains("O4"))
            {status="Cần chỉnh";reason="Cấu kiện mố tiềm năng, cần hiệu chỉnh tiếp giáp địa hình ở hai đầu.";}
            else
            {status="Cần chỉnh";reason="Biến thể nhịp hoặc trụ khác; cần xác nhận tiết diện và lắp ghép trước khi thay.";}
            lines.Add(Csv(e.globalId,e.ifcClass,e.Label,e.parentGlobalId,part.filter.sharedMesh.name,part.fileId,part.triangles,
                size.z.ToString("F4",CultureInfo.InvariantCulture),size.x.ToString("F4",CultureInfo.InvariantCulture),
                size.y.ToString("F4",CultureInfo.InvariantCulture),status,reason));
        }
        File.WriteAllLines(Reports+"SO201_91_Components.csv",lines);
    }

    static void WriteBindings(string label,IEnumerable<Occurrence> bindings)
    {
        var rows=new[]{"AssetID,Role,Parent,Station_m,SourceGlobalId,MeshAsset,MeshLocalFileID,Triangles,Geometry"}
            .Concat(bindings.Select(x=>Csv(x.id,x.role,x.group,x.station.ToString("F3",CultureInfo.InvariantCulture),
                x.sourceId,x.fileId!=0?Dae:"",x.fileId!=0?x.fileId.ToString(CultureInfo.InvariantCulture):"",x.triangles,
                x.fileId!=0?"DAE_DIRECT":"NEW_RAIL_ADAPTATION")));
        File.WriteAllLines(Reports+label+"_Occurrences.csv",rows);
    }

    static void SaveSample(Scene scene, GameObject candidate, (RailwayMicroChunkData.Track Track, Mesh Mesh)[] rails,
        Vector3 origin,Vector3 baselineOrigin,Quaternion yaw)
    {
        var originalPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Modular);
        var original=(GameObject)PrefabUtility.InstantiatePrefab(originalPrefab,scene);
        original.name="Cầu tham chiếu · tắt khi xem SO 201";
        original.transform.position=baselineOrigin-origin;
        var axis=yaw*Vector3.forward;
        for(int i=0;i<original.transform.childCount;i++)
        {
            var child=original.transform.GetChild(i);
            var renderers=child.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length==0 || Mathf.Abs(Vector3.Dot(renderers[0].bounds.center,axis))>24)child.gameObject.SetActive(false);
        }
        original.SetActive(false);
        var context=new GameObject("Ray đôi hiện hữu · bản tham chiếu");
        foreach(var rail in rails)
        {
            var go=new GameObject(rail.Mesh.name);
            go.transform.SetParent(context.transform,false);
            go.transform.SetPositionAndRotation(rail.Track.position-origin,rail.Track.rotation);
            go.transform.localScale=rail.Track.scale;
            go.AddComponent<MeshFilter>().sharedMesh=rail.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials=rail.Track.materials;
        }
        var key=new GameObject("Directional Light").AddComponent<Light>();
        key.type=LightType.Directional;
        key.intensity=1.5f;
        key.transform.rotation=Quaternion.Euler(38,-36,0);
        RenderSettings.ambientLight=new Color(.62f,.63f,.67f);
        var camera=new GameObject("Camera").AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.24f,.30f,.38f);
        camera.fieldOfView=50;
        camera.farClipPlane=400;
        CaptureSample(scene,candidate,original,camera,yaw);
        if(!EditorSceneManager.SaveScene(scene,Output+"SO201_Railway_Sandbox.unity"))
            throw new IOException("Could not save the separate sandbox.");
    }

    static void Recapture()
    {
        string path=Output+"SO201_Railway_Sandbox.unity";
        var previous=SceneManager.GetActiveScene();
        var scene=SceneManager.GetSceneByPath(path);
        bool opened=!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var candidate=scene.GetRootGameObjects().Single(g=>g.name=="SO201_Sample_Detailed");
            var original=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("Cầu tham chiếu",StringComparison.Ordinal));
            var camera=scene.GetRootGameObjects().Select(g=>g.GetComponent<Camera>()).Single(c=>c!=null);
            var floors=candidate.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Sàn ray đôi").ToArray();
            var axis=Vector3.ProjectOnPlane(floors[1].bounds.center-floors[0].bounds.center,Vector3.up);
            CaptureSample(scene,candidate,original,camera,Quaternion.LookRotation(axis));
            File.WriteAllText(Work+"pictures-ready.txt","Isolated comparison captured");
        }
        finally
        {
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(opened)EditorSceneManager.CloseScene(scene,true);
        }
    }

    static void CaptureSample(Scene scene,GameObject candidate,GameObject original,Camera camera,Quaternion yaw)
    {
        var axis=yaw*Vector3.forward;
        var right=yaw*Vector3.right;
        void Capture(string name,Vector3 offset,Vector3 look)
        {
            camera.transform.position=offset;
            camera.transform.LookAt(look);
            var rt=new RenderTexture(1440,900,24);
            rt.Create();
            var previous=RenderTexture.active;
            Texture2D image=null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;
                image=new Texture2D(1440,900,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1440,900),0,0);
                image.Apply();
                File.WriteAllBytes(Reports+"Screenshots/"+name+".png",image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;
                if(image!=null)Object.DestroyImmediate(image);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
        var diagonal=right*50-axis*26+Vector3.up*18;
        var aim=Vector3.down*9;
        var lift=Vector3.up*1400;
        var roots=scene.GetRootGameObjects().Where(g=>g!=camera.gameObject && g.GetComponent<Light>()==null).ToArray();
        foreach(var root in roots)root.transform.position+=lift;
        try
        {
            Capture("SO201_Side",diagonal+lift,aim+lift);
            Capture("SO201_Top",right*18-axis*14+Vector3.up*42+lift,lift);
            Capture("SO201_Support",right*24+Vector3.up*4-axis*9+lift,lift+Vector3.down*12);
            Capture("SO201_Girders",right*11+Vector3.down*5-axis*7+lift,lift+Vector3.down*1.6f);
            candidate.SetActive(false);original.SetActive(true);
            Capture("Modular_Same_View",diagonal+lift,aim+lift);
        }
        finally
        {
            original.SetActive(false);candidate.SetActive(true);
            foreach(var root in roots)root.transform.position-=lift;
            camera.transform.position=diagonal;camera.transform.LookAt(aim);
        }
    }

    static void BuildCandidate()
    {
        string full=Reports+"Full_Measurements.txt";
        if(!File.Exists(full))throw new InvalidOperationException("Validate the full candidate before building.");
        var origin=File.ReadAllText(full).Split(new[]{"origin="},StringSplitOptions.None).Last().Split('|')
            .Select(s=>float.Parse(s,CultureInfo.InvariantCulture)).ToArray();
        string folder="Web_"+Guid.NewGuid().ToString("N");
        string staging=AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(Output.TrimEnd('/'),folder));
        string zone=staging+"/Zone_018.unity", route=staging+"/YenTuHaLongXanh.unity";
        File.WriteAllText(Work+"web.txt","Building");
        try
        {
            if(string.IsNullOrEmpty(staging) || !AssetDatabase.CopyAsset(Zone,zone) || !AssetDatabase.CopyAsset(Route,route))
                throw new IOException("Could not stage isolated scenes.");
            Change(zone,scene=>
            {
                var structure=scene.GetRootGameObjects().Single(g=>g.name=="RuntimeZoneContent").transform.Find("FixedInfrastructure_Bridge");
                var original=structure.Find("Bridge_YTH_Visual");
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"SO201_Full_Visual_Optimized.prefab")
                    ?? AssetDatabase.LoadAssetAtPath<GameObject>(Output+"SO201_Full_Visual.prefab");
                if(original==null||prefab==null)throw new InvalidOperationException("Missing bridge variant.");
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                instance.name=original.name;
                instance.transform.SetParent(structure,false);
                instance.transform.position=new Vector3(origin[0],origin[1],origin[2]);
                Object.DestroyImmediate(original.gameObject);
            });
            File.Copy(zone,Work+"Trial_Zone_018.unity",true);
            Change(route,scene=>
            {
                var streaming=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
                streaming.zones.Single(z=>z.zoneId=="Zone_018").scenePath=zone;
            });
            var paths=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            paths[Array.IndexOf(paths,Zone)]=zone;
            paths[Array.IndexOf(paths,Route)]=route;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=paths,locationPathName="Builds/SO201_Railway_Trial",target=BuildTarget.WebGL,targetGroup=BuildTargetGroup.WebGL,
                options=BuildOptions.Development,extraScriptingDefines=new[]{"BRIDGE_MODULAR_AUDIT"}
            }).summary;
            File.WriteAllText(Work+"web.txt",$"{report.result}; {report.totalErrors} errors; {report.totalWarnings} warnings; {report.totalSize} bytes; {report.totalTime}");
            if(report.result!=BuildResult.Succeeded)throw new InvalidOperationException("Candidate Web build failed.");
        }
        finally { if(!string.IsNullOrEmpty(staging))AssetDatabase.DeleteAsset(staging); }
    }

    static void OptimizeVisual()
    {
        string measurements=File.ReadAllText(Reports+"Full_Measurements.txt");
        if(!measurements.Contains("blinding_gap_m="))throw new InvalidOperationException("Refit the foundations first.");
        var input=PrefabUtility.LoadPrefabContents(Output+"SO201_Full_Visual.prefab");
        var output=new GameObject("SO201_Full_Visual_Optimized");
        SceneManager.MoveGameObjectToScene(output,input.scene);
        var csv=new List<string>{"Group,SourcePrefab,SourceRenderers,Materials,Triangles,DerivedMesh"};
        int index=0, triangleCount=0;
        float boundsError=0;
        try
        {
            for(int n=0;n<input.transform.childCount;n++)
            {
                var group=input.transform.GetChild(n);
                var filters=group.GetComponentsInChildren<MeshFilter>();
                var authoredBounds=filters[0].GetComponent<MeshRenderer>().bounds;
                foreach(var f in filters)authoredBounds.Encapsulate(f.GetComponent<MeshRenderer>().bounds);
                var anchor=authoredBounds.center;
                var suffix=$"RenderGroup_{++index:000}";
                var go=new GameObject(group.name);
                go.transform.SetParent(output.transform,false);
                go.transform.localPosition=anchor;
                var positions=new List<Vector3>();
                var normals=new List<Vector3>();
                var colors=new List<Color>();
                var uv=new List<Vector2>();
                var materials=new List<Material>();
                var faces=new List<List<int>>();
                foreach(var f in filters)
                {
                    var m=f.sharedMesh;
                    var renderer=f.GetComponent<MeshRenderer>();
                    var slots=renderer.sharedMaterials;
                    var transform=f.transform.localToWorldMatrix;
                    if(transform.determinant<=0||m.subMeshCount!=slots.Length)
                        throw new InvalidOperationException("Unexpected winding or material slots in "+f.name);
                    int offset=positions.Count;
                    positions.AddRange(m.vertices.Select(p=>transform.MultiplyPoint3x4(p)-anchor));
                    var normalMatrix=transform.inverse.transpose;
                    normals.AddRange(m.normals.Select(p=>normalMatrix.MultiplyVector(p).normalized));
                    if(normals.Count!=positions.Count)throw new InvalidOperationException("Missing normal data in "+f.name);
                    var sourceColors=m.colors;
                    colors.AddRange(sourceColors.Length==m.vertexCount?sourceColors:Enumerable.Repeat(Color.white,m.vertexCount));
                    var sourceUv=m.uv;
                    uv.AddRange(sourceUv.Length==m.vertexCount?sourceUv:Enumerable.Repeat(Vector2.zero,m.vertexCount));
                    for(int sub=0;sub<m.subMeshCount;sub++)
                    {
                        if(m.GetTopology(sub)!=MeshTopology.Triangles || slots[sub]==null)
                            throw new InvalidOperationException("Unsupported render topology in "+f.name);
                        int slot=materials.IndexOf(slots[sub]);
                        if(slot<0)
                        {
                            slot=materials.Count;
                            materials.Add(slots[sub]);
                            faces.Add(new List<int>());
                        }
                        faces[slot].AddRange(m.GetIndices(sub).Select(i=>i+offset));
                    }
                }
                var combined=new Mesh {name=suffix,indexFormat=positions.Count>ushort.MaxValue?IndexFormat.UInt32:IndexFormat.UInt16};
                combined.SetVertices(positions);
                combined.SetNormals(normals);
                combined.SetColors(colors);
                combined.SetUVs(0,uv);
                combined.subMeshCount=faces.Count;
                for(int i=0;i<faces.Count;i++)combined.SetIndices(faces[i],MeshTopology.Triangles,i,false);
                combined.RecalculateBounds();
                int actual=Triangles(combined), expected=filters.Sum(f=>Triangles(f.sharedMesh));
                if(actual!=expected)throw new InvalidOperationException("Triangles changed in "+group.name);
                Vector3 minimum=Vector3.one*float.PositiveInfinity, maximum=Vector3.one*float.NegativeInfinity;
                foreach(var point in positions)
                {
                    minimum=Vector3.Min(minimum,point);
                    maximum=Vector3.Max(maximum,point);
                }
                boundsError=Mathf.Max(boundsError,(combined.bounds.min-minimum).magnitude,(combined.bounds.max-maximum).magnitude);
                triangleCount+=actual;
                string path=Output+"Meshes/"+suffix+".asset";
                var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(previous==null)AssetDatabase.CreateAsset(combined,path);
                else {EditorUtility.CopySerialized(combined,previous);Object.DestroyImmediate(combined);combined=previous;}
                go.AddComponent<MeshFilter>().sharedMesh=combined;
                go.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();
                csv.Add(Csv(group.name,Output+"SO201_Full_Source.prefab",filters.Length,materials.Count,actual,path));
            }
            if(index!=129||triangleCount!=491794||boundsError>.0001f)
                throw new InvalidOperationException("Grouped Visual changed the verified geometry.");
            if(PrefabUtility.SaveAsPrefabAsset(output,Output+"SO201_Full_Visual_Optimized.prefab")==null)
                throw new IOException("Could not save the grouped Visual.");
            AssetDatabase.SaveAssets();
            File.WriteAllLines(Reports+"Visual_Group_Provenance.csv",csv);
            File.WriteAllText(Work+"optimize-ready.txt",FormattableString.Invariant(
                $"groups={index}; triangles={triangleCount}; max_bounds_error_m={boundsError:F7}"));
        }
        finally
        {
            Object.DestroyImmediate(output);
            PrefabUtility.UnloadPrefabContents(input);
        }
    }

    static void Change(string path,Action<Scene> action)
    {
        var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            action(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException(path);
        }
        finally { EditorSceneManager.CloseScene(scene,true); }
    }
}
