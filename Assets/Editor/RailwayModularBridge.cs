using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using UttVr.BridgePilot;
using Object = UnityEngine.Object;

public static class RailwayModularBridge
{
    const string Dir = "Assets/ExternalBIM/BridgeModular/";
    const string Temp = "Temp/BridgeModular/";
    const string Source = Dir + "Bridge_YTH_Source.prefab";
    const string Visual = Dir + "Bridge_YTH_Visual.prefab";
    const string Detailed = Dir + "Bridge_YTH_Detailed.prefab";
    public const string CatalogPath = Dir + "Bridge_YTH_Catalog.asset";
    const string Zone = "Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_018.unity";
    const string Route = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string Viewer = "Assets/ExternalBIM/ZoneBased/Resources/RouteBimZones/Zone_018.prefab";
    const string Bim = "Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity";
    const string Sandbox = "Assets/ExternalBIM/ZoneBased/Bridge_Replacement_Sandbox.unity";
    const string BridgePath = "RuntimeZoneContent/FixedInfrastructure_Bridge";
    const string BridgeKey = "YTH-BRIDGE-MAIN";
    const string Provenance = "Assets/ProjectLopHoc/Generated/QuanLyVongDoiTaiSan/SceneProvenance.json";

    [Serializable] sealed class Settings
    {
        public float length, deckStep, pierSpacing, width, deckDepth, pierBaseY, railSpacing, seamSampleStep;
    }

    [Serializable] sealed class ViewOrigin { public Vector3 sourceWorldOrigin; }
    [Serializable] sealed class Placement { public Vector3 world, viewer; }

    [InitializeOnLoadMethod]
    static void Watch() => EditorApplication.update += Dispatch;

    static void Dispatch()
    {
        string file = Temp + "request.txt";
        if (!File.Exists(file) || EditorApplication.isUpdating || EditorApplication.isCompiling ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        string requested = File.ReadAllText(file).Trim();
        File.Delete(file);
        try
        {
            if (requested == "prepare") Prepare();
            if (requested == "web") Web();
            if (requested == "font-and-web") { ApplyBimFont(); Web(); }
            if (requested == "install-and-web") { Install(); Web(); }
            if (requested == "prepare-and-web") PrepareAndWeb();
        }
        catch (Exception error) { File.WriteAllText(Temp + "error.txt", error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("Tools/UTTVR/Bridge YTH/Generate shared prefabs")]
    public static void Prepare()
    {
        Directory.CreateDirectory(Temp);
        Directory.CreateDirectory(Dir + "Meshes");
        AssetDatabase.Refresh();
        foreach (string path in new[] { Zone, Viewer, Bim, Sandbox })
            if (!File.Exists(Temp + "Before_" + Path.GetFileName(path)))
                File.Copy(path, Temp + "Before_" + Path.GetFileName(path));

        var initialActive = SceneManager.GetActiveScene();
        var route = SceneManager.GetSceneByPath(Route);
        bool openedRoute = !route.isLoaded;
        if (openedRoute) route = EditorSceneManager.OpenScene(Route, OpenSceneMode.Additive);
        var manager = route.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
        var fixedBridge = manager.fixedInfrastructure.Single(f => f.id == "Bridge_Main");
        var rails = route.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RailwayMicroChunkStreamer>(true)).Single().data;
        var settings = JsonUtility.FromJson<Settings>(File.ReadAllText(Dir + "Bridge_YTH_Config.json"));
        float start = fixedBridge.startChainage, end = start + settings.length;
        if (Mathf.Abs(end - fixedBridge.endChainage) > .01f || settings.pierSpacing <= 0 || settings.deckStep <= 0 ||
            settings.seamSampleStep <= 0 || settings.deckDepth <= 0 || settings.width < settings.railSpacing + 6 ||
            Mathf.Abs(settings.railSpacing - 6.400022f) > .0001f)
            throw new InvalidOperationException("Cấu hình chưa khớp với footprint, ray đôi và registry đã duyệt.");
        var spline = manager.trainMover.splineRoute;
        float routeLength = spline.CalculateLength();
        Vector3 Position(float d) => spline.EvaluatePosition(d / routeLength);
        Vector3 Side(float d) => Vector3.Cross(Vector3.up, (Vector3)spline.EvaluateTangent(d / routeLength)).normalized;
        Vector3 origin = Position((start + end) / 2);
        Vector3 reference = JsonUtility.FromJson<ViewOrigin>(File.ReadAllText(Provenance)).sourceWorldOrigin;

        float railMin = float.MaxValue, railMax = float.MinValue, covered = start;
        int railPairs = 0;
        foreach (var chunk in rails.chunks.Where(c => c.end > start + .005f && c.start < end - .005f).OrderBy(c => c.start))
        {
            if (chunk.start > covered + .02f) throw new InvalidOperationException("Gián đoạn dữ liệu ray tại " + covered);
            covered = chunk.end;
            railPairs++;
            foreach (var track in new[] { chunk.right, chunk.left })
            {
                var mesh = Resources.Load<Mesh>(track.meshResource);
                if (mesh == null) throw new FileNotFoundException(track.meshResource);
                var matrix = Matrix4x4.TRS(track.position, track.rotation, track.scale);
                float bottom = float.MaxValue;
                for (int i = 0; i < 8; i++)
                {
                    var a = mesh.bounds;
                    var e = a.extents;
                    var p = matrix.MultiplyPoint3x4(a.center + Vector3.Scale(e,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    bottom = Mathf.Min(bottom, p.y);
                }
                railMin = Mathf.Min(railMin, bottom);
                railMax = Mathf.Max(railMax, bottom);
            }
        }
        if (covered < end - .02f || railMax - railMin > .04f)
            throw new InvalidOperationException($"Kiểm tra đỡ hai ray thất bại: {railMin}, {railMax}, {covered}.");
        float top = railMin - .002f;
        if (settings.pierBaseY >= top - settings.deckDepth - 2)
            throw new InvalidOperationException("Đáy trụ không đủ thấp.");

        var bodyRoots = new[] { manager.trainMover.frontCar, manager.trainMover.rearCar }.Where(t => t != null);
        var trainRenderers = bodyRoots.SelectMany(car => car.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.enabled && r.GetComponent<MeshFilter>()?.sharedMesh != null)
            .Select(r => (Car: car, Renderer: r))).ToArray();
        var body = new Bounds();
        if (trainRenderers.Length == 0) throw new InvalidOperationException("Thiếu mesh toa để kiểm tra biên ngang.");
        bool first = true;
        foreach (var renderer in trainRenderers)
        {
            var mesh = renderer.Renderer.GetComponent<MeshFilter>().sharedMesh;
            var m = Matrix4x4.TRS(renderer.Car.position,renderer.Car.rotation,Vector3.one).inverse
                * renderer.Renderer.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var e = mesh.bounds.extents;
                var corner = m.MultiplyPoint3x4(mesh.bounds.center + Vector3.Scale(e,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (first) { body = new Bounds(corner, Vector3.zero); first = false; }
                else body.Encapsulate(corner);
            }
        }
        // Keep the edge bars on the outer shoulders of the deck.
        float outerMargin = (settings.width - settings.railSpacing) / 2 - .28f;
        float trainHalf = Mathf.Max(Mathf.Abs(body.min.x), Mathf.Abs(body.max.x));
        float dynamicMargin = float.MaxValue;
        // Test both track axes using the train renderer envelope sampled through the curve.
        // This is a model clearance check, not a design clearance certificate.
        for (float d = start; d <= end + .01f; d += 2)
        {
            var f = ((Vector3)spline.EvaluateTangent(d / routeLength)).normalized;
            var right = Side(d);
            var pivot = Position(d);
            foreach (float track in new[] {0f, -settings.railSpacing})
                foreach (float z in new[] {body.min.z, body.center.z, body.max.z})
                    foreach (float x in new[] {body.min.x, body.max.x})
                    {
                        float section = Mathf.Clamp(d + z,0,routeLength);
                        var deckCenter = Position(section) - Side(section)*(settings.railSpacing/2);
                        var corner = pivot + right*(track+x) + f*z;
                        float lateral = Mathf.Abs(Vector3.Dot(corner-deckCenter,Side(section)));
                        dynamicMargin = Mathf.Min(dynamicMargin,settings.width/2-.23f-lateral);
                    }
        }
        if (trainHalf > outerMargin || dynamicMargin < .05f)
            throw new InvalidOperationException($"Bounds toa {trainHalf:F3} m, biên ngang còn {dynamicMargin:F3} m.");

        var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var root = new GameObject("Bridge_YTH_Source");
        SceneManager.MoveGameObjectToScene(root, scratch);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectLopHoc/Generated/QuestRailway/RailwayFlatColour.mat");
        var highlightMaterial = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalBIM/BridgeDemo/BridgeBIM_Demo.prefab")
            .GetComponent<BridgeBimAdapter>().highlightMaterial;
        var entries = new List<BridgeBimEntry>();
        var binds = new Dictionary<string, string>();
        int spanCount = 0, pierCount = 0;
        var concrete = new Color(.45f,.46f,.43f);
        var steel = new Color(.61f,.64f,.64f);
        for (float d = start; d < end - .005f; d = start + ++spanCount * settings.deckStep)
        {
            float next = Mathf.Min(end, start + (spanCount + 1) * settings.deckStep);
            string groupId = "span-" + StationKey(d);
            var group = Group($"Nhịp {spanCount + 1:00}", groupId);
            Segment("Bản mặt cầu", "deck", group, groupId, d, next, 0, settings.width, top-settings.deckDepth, top, concrete);
            Segment("Dầm đỡ A", "girder-a", group, groupId, d, next, settings.railSpacing / 2, 1.25f, top-settings.deckDepth-1.1f, top-settings.deckDepth, concrete);
            Segment("Dầm đỡ B", "girder-b", group, groupId, d, next, -settings.railSpacing / 2, 1.25f, top-settings.deckDepth-1.1f, top-settings.deckDepth, concrete);
            Segment("Lan can A", "rail-a", group, groupId, d, next, settings.width / 2 - .16f, .14f, top, top+1.05f, steel);
            Segment("Lan can B", "rail-b", group, groupId, d, next, -settings.width / 2 + .16f, .14f, top, top+1.05f, steel);
        }
        var cube = UnitBox();
        float pierTop = top - settings.deckDepth - 1.1f;
        for (float d = start + settings.pierSpacing / 2; d < end; d = start + settings.pierSpacing / 2 + ++pierCount * settings.pierSpacing)
        {
            string groupId = "pier-" + StationKey(d);
            var group = Group($"Cụm trụ {pierCount + 1:00}", groupId);
            Box("Xà mũ trụ", "cap", group, groupId, d, 0, pierTop - .45f, new Vector3(settings.railSpacing + 2.8f,.9f,2.7f));
            foreach (int side in new[] { -1, 1 })
            {
                string suffix = side < 0 ? "b" : "a";
                float x = side * settings.railSpacing / 2;
                float height = pierTop - .9f - settings.pierBaseY;
                Box("Thân trụ " + suffix.ToUpperInvariant(), "column-" + suffix, group, groupId, d, x,
                    settings.pierBaseY + height / 2, new Vector3(1.5f, height, 1.65f));
                Box("Bệ trụ " + suffix.ToUpperInvariant(), "footing-" + suffix, group, groupId, d, x,
                    settings.pierBaseY - .4f, new Vector3(2.8f, .8f, 3f));
            }
        }
        Transform Group(string label, string key)
        {
            var t = new GameObject(label).transform;
            t.SetParent(root.transform, false);
            entries.Add(new BridgeBimEntry { globalId = key, name = label, propertyLines = Array.Empty<string>() });
            return t;
        }

        void AddPart(string label, string kind, Transform group, string parent, float a, float b,
            Mesh mesh, Vector3 pos, Quaternion rotation, Vector3 scale, string size)
        {
            var go = new GameObject(label);
            go.transform.SetParent(group, false);
            go.transform.SetLocalPositionAndRotation(pos, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            string key = BridgeKey + "-" + kind.ToUpperInvariant() + "-" + StationKey(a);
            string path = group.name + "/" + go.name;
            binds.Add(key, path);
            entries.Add(new BridgeBimEntry { globalId = key, assetId = key, name = label, parentGlobalId = parent, ifcClass = kind,
                propertyLines = new[] { size, FormattableString.Invariant($"Lý trình mô hình: {a:F3}–{b:F3} m") } });
        }

        void Segment(string label, string kind, Transform group, string parent, float a, float b,
            float offset, float width, float bottom, float crown, Color color)
        {
            Vector3 center = Position((a + b) / 2) - origin;
            int n = Mathf.CeilToInt((b - a) / settings.seamSampleStep);
            var rings = new Vector3[n + 1][];
            for (int i = 0; i <= n; i++)
            {
                float d = i == 0 ? a : i == n ? b : Mathf.Lerp(a,b,i/(float)n);
                Vector3 p = Position(d) - origin - center, side = Side(d);
                Vector3 left = p + side*(offset - width/2), right = p + side*(offset + width/2);
                rings[i] = new[] { new Vector3(left.x,bottom-origin.y-center.y,left.z), new Vector3(left.x,crown-origin.y-center.y,left.z),
                    new Vector3(right.x,crown-origin.y-center.y,right.z), new Vector3(right.x,bottom-origin.y-center.y,right.z) };
            }
            var mesh = BarMesh(rings, color);
            mesh = SaveMesh(mesh, kind + "_" + StationKey(a));
            AddPart(label, kind, group, parent, a,b,mesh,center,Quaternion.identity,Vector3.one,
                FormattableString.Invariant($"Dài {b-a:F2} × rộng {width:F2} × cao {crown-bottom:F2} m"));
        }

        void Box(string label, string kind, Transform group, string parent, float d, float x, float y, Vector3 dimensions)
        {
            Vector3 p = Position(d) + Side(d)*x;
            p.y = y;
            var q = Quaternion.LookRotation(spline.EvaluateTangent(d / routeLength),Vector3.up);
            AddPart(label,kind,group,parent,d,d,cube,p-origin,q,dimensions,
                FormattableString.Invariant($"Rộng {dimensions.x:F2} × cao {dimensions.y:F2} × sâu {dimensions.z:F2} m"));
        }

        var generated = PrefabUtility.SaveAsPrefabAsset(root, Source);
        Object.DestroyImmediate(root);
        SavePresentation(generated, scratch, Visual, null, null, null);
        var catalog = AssetDatabase.LoadAssetAtPath<BridgeBimCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BridgeBimCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.schema = "YTH-MODULAR-SIMULATION";
        catalog.sourceSha256 = BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(Dir+"Bridge_YTH_Config.json"))).Replace("-","");
        catalog.entries = entries.ToArray();
        EditorUtility.SetDirty(catalog);
        SavePresentation(generated, scratch, Detailed, catalog, highlightMaterial, binds);
        AssetDatabase.SaveAssets();
        ValidatePair(generated, binds.Count);
        float seam=VerifyDeckSeams(generated);
        string result = FormattableString.Invariant($"Spans={spanCount}; paired piers={pierCount}; elements={binds.Count}; rail chunks={railPairs}; rail-bottom-range={railMin:F4}..{railMax:F4}; deck-top={top:F4}; train-local-envelope={body.size:F3}; lateral-margin={dynamicMargin:F3}; max-deck-seam={seam:F6}; bridge-start={start:F4}; bridge-end={end:F4}");
        File.WriteAllText(Temp + "geometry.txt", result);
        File.WriteAllLines(Temp + "parts.tsv", entries.Where(e=>e.IsElement).Select(e=>e.assetId+"\t"+e.parentGlobalId+"\t"+e.name+"\t"+string.Join("; ",e.propertyLines)));
        File.WriteAllText(Temp+"placement.json", JsonUtility.ToJson(new Placement {world=origin,viewer=origin-reference}));
        EditorSceneManager.CloseScene(scratch,true);
        if (openedRoute) EditorSceneManager.CloseScene(route,true);
        if (initialActive.isLoaded) SceneManager.SetActiveScene(initialActive);
        File.WriteAllText(Temp+"status.txt","Generated. "+result);
    }

    [MenuItem("Tools/UTTVR/Bridge YTH/Validate sandbox and install")]
    public static void Install()
    {
        foreach (string path in new[] { Zone, Bim })
        {
            var open=SceneManager.GetSceneByPath(path);
            if(open.isLoaded&&open.isDirty) throw new InvalidOperationException("Scene đích có thay đổi chưa lưu: "+path);
        }
        var placement=JsonUtility.FromJson<Placement>(File.ReadAllText(Temp+"placement.json"));
        string result=File.ReadAllText(Temp+"geometry.txt");
        var catalog=AssetDatabase.LoadAssetAtPath<BridgeBimCatalog>(CatalogPath);
        ValidatePair(AssetDatabase.LoadAssetAtPath<GameObject>(Source),catalog.entries.Count(e=>e.IsElement));
        InstallSandbox(placement.viewer,result);
        InstallRoute(placement.world);
        RefreshViewer(placement.viewer);
        var bim=GetScene(Bim,out bool opened);
        var controller = bim.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RailwayBimController>(true)).Single();
        controller.zoneViewer.modularBridgeCatalog = catalog;
        foreach (var work in controller.zoneViewer.facilities.Where(f=>f.key=="Bridge_Main"))
            work.bindings = new[] { "Zone_018/RuntimeZoneContent/FixedInfrastructure_Bridge/Bridge_YTH_Visual" };
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        if (!EditorSceneManager.SaveScene(bim)) throw new IOException(Bim);
        if(opened)EditorSceneManager.CloseScene(bim,true);
        File.WriteAllText(Temp + "status.txt", "Installed. " + result);
    }

    static float VerifyDeckSeams(GameObject root)
    {
        var decks=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Bản mặt cầu").ToArray();
        float worst=0;
        for(int i=1;i<decks.Length;i++)
        {
            var previous=decks[i-1];
            var next=decks[i];
            var a=previous.sharedMesh.vertices; var b=next.sharedMesh.vertices;
            for (int j=0;j<4;j++)
                worst=Mathf.Max(worst,Vector3.Distance(previous.transform.TransformPoint(a[a.Length-1-j]),
                    next.transform.TransformPoint(b[b.Length-8+j])));
        }
        if(worst>.003f) throw new InvalidOperationException($"Đầu nhịp không kín: {worst:F6} m.");
        return worst;
    }

    static string StationKey(float position) => Mathf.RoundToInt(position*1000).ToString("D8",CultureInfo.InvariantCulture);

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = Dir + "Meshes/" + name + ".asset";
        mesh.name = name;
        var prior = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (prior == null) { AssetDatabase.CreateAsset(mesh,path); return mesh; }
        EditorUtility.CopySerialized(mesh,prior);
        EditorUtility.SetDirty(prior);
        Object.DestroyImmediate(mesh);
        return prior;
    }

    static Mesh UnitBox()
    {
        var rings = new[] { Ring(-.5f), Ring(.5f) };
        Vector3[] Ring(float z) => new[] {new Vector3(-.5f,-.5f,z),new Vector3(-.5f,.5f,z),new Vector3(.5f,.5f,z),new Vector3(.5f,-.5f,z)};
        return SaveMesh(BarMesh(rings,new Color(.40f,.42f,.42f)), "PierSolid");
    }

    static Mesh BarMesh(Vector3[][] rings, Color color)
    {
        var points = new List<Vector3>();
        var tris = new List<int>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int i=points.Count;
            points.AddRange(new[] {a,b,c,d});
            tris.AddRange(new[] {i,i+1,i+2,i,i+2,i+3});
        }
        for(int i=0;i<rings.Length-1;i++)
            for(int j=0;j<4;j++)
            {
                int k=(j+1)%4;
                Quad(rings[i][j],rings[i+1][j],rings[i+1][k],rings[i][k]);
            }
        var a=rings[0]; var b=rings[rings.Length-1];
        Quad(a[0],a[1],a[2],a[3]);
        Quad(b[3],b[2],b[1],b[0]);
        var mesh=new Mesh();
        mesh.SetVertices(points);
        mesh.SetTriangles(tris,0);
        mesh.SetColors(Enumerable.Repeat(color,points.Count).ToList());
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void SavePresentation(GameObject source, Scene scene, string path, BridgeBimCatalog catalog,
        Material highlight, Dictionary<string,string> paths)
    {
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);
        instance.name=Path.GetFileNameWithoutExtension(path);
        if (catalog!=null)
        {
            var adapter=instance.AddComponent<BridgeBimAdapter>();
            adapter.catalog=catalog;
            adapter.highlightMaterial=highlight;
            foreach (var binding in paths)
            {
                var go=instance.transform.Find(binding.Value).gameObject;
                var collider=go.AddComponent<MeshCollider>();
                collider.sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;
                go.AddComponent<BridgeBimIdentity>().Configure(binding.Key,new[]{go.GetComponent<Renderer>()});
            }
        }
        if(PrefabUtility.SaveAsPrefabAsset(instance,path)==null)throw new IOException(path);
        Object.DestroyImmediate(instance);
    }

    static void ValidatePair(GameObject source,int count)
    {
        var visual=AssetDatabase.LoadAssetAtPath<GameObject>(Visual);
        var detailed=AssetDatabase.LoadAssetAtPath<GameObject>(Detailed);
        var a=visual.GetComponentsInChildren<MeshFilter>();
        var b=detailed.GetComponentsInChildren<MeshFilter>();
        if(a.Length!=count||b.Length!=count||source.GetComponentsInChildren<MeshFilter>().Length!=count||
           visual.GetComponentsInChildren<MonoBehaviour>().Length!=0||visual.GetComponentsInChildren<Collider>().Length!=0||
           detailed.GetComponentsInChildren<BridgeBimIdentity>().Select(i=>i.GlobalId).Distinct().Count()!=count)
            throw new InvalidOperationException("Số lượng cấu kiện hoặc identity sai.");
        for(int i=0;i<count;i++)
            if(a[i].sharedMesh!=b[i].sharedMesh || a[i].transform.localToWorldMatrix!=b[i].transform.localToWorldMatrix ||
                !a[i].GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(b[i].GetComponent<MeshRenderer>().sharedMaterials))
                throw new InvalidOperationException("Hai prefab không dùng cùng hình học.");
    }

    static GameObject Place(string prefab,Vector3 position,Scene scene, Transform parent=null)
    {
        var result=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab),scene);
        if(parent!=null)result.transform.SetParent(parent,false);
        result.transform.SetPositionAndRotation(position,Quaternion.identity);
        return result;
    }

    static void InstallSandbox(Vector3 origin,string evidence)
    {
        string path=Sandbox;
        var open=SceneManager.GetSceneByPath(Sandbox);
        if(open.isLoaded&&open.isDirty)
        {
            // Use the saved baseline in a derived scene without touching the open editing session.
            path=Dir+"Bridge_Replacement_Validation.unity";
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(path)==null&&!AssetDatabase.CopyAsset(Sandbox,path))
                throw new IOException(path);
        }
        var scene=GetScene(path,out bool opened);
        var toggle=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RailwayBridgeReplacementSandbox>(true)).Single();
        if(toggle==null||toggle.oldBridge==null)throw new InvalidOperationException("Thiếu bản baseline trong sandbox.");
        // Detach only the three sandbox instances from the viewer-prefab files that will be updated.
        foreach(var root in scene.GetRootGameObjects().Where(PrefabUtility.IsPartOfPrefabInstance))
            if(root.name.StartsWith("Zone_")) PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
        if(toggle.newBridge!=null) Object.DestroyImmediate(toggle.newBridge);
        toggle.newBridge=Place(Visual,origin,scene);
        if(!toggle.TryShowReplacement(false)||!toggle.oldBridge.activeSelf||toggle.newBridge.activeSelf||
           !toggle.TryShowReplacement(true)||toggle.oldBridge.activeSelf||!toggle.newBridge.activeSelf)
            throw new InvalidOperationException("Sandbox đang render hai bản hoặc thiếu cầu mới.");
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException(path);
        if(opened)EditorSceneManager.CloseScene(scene,true);
        File.WriteAllText(Temp+"sandbox.txt",path+"; Old/New exclusive; shared geometry and clearance gates passed. "+evidence);
    }

    static Scene GetScene(string path,out bool opened)
    {
        var scene=SceneManager.GetSceneByPath(path);
        opened=!scene.isLoaded;
        return opened?EditorSceneManager.OpenScene(path,OpenSceneMode.Additive):scene;
    }

    static void InstallRoute(Vector3 origin)
    {
        var scene=GetScene(Zone,out bool opened);
        var parent=scene.GetRootGameObjects().Single(g=>g.name=="RuntimeZoneContent").transform.Find("FixedInfrastructure_Bridge");
        var old=parent.Find("ExtendedBridgeQuest");
        if(old==null||parent.Find("BridgeValleyWaterQuest")==null)throw new InvalidOperationException("Thiếu baseline/water.");
        var prior=parent.Find("Bridge_YTH_Visual");
        if(prior!=null) Object.DestroyImmediate(prior.gameObject);
        Place(Visual,origin,scene,parent);
        old.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException(Zone);
        if(opened)EditorSceneManager.CloseScene(scene,true);
    }

    static void RefreshViewer(Vector3 origin)
    {
        var root=PrefabUtility.LoadPrefabContents(Viewer);
        try
        {
            var parent=root.transform.Find("FixedInfrastructure_Bridge");
            var old=parent.Find("ExtendedBridgeQuest");
            var prior=parent.Find("Bridge_YTH_Detailed");
            if(prior!=null) Object.DestroyImmediate(prior.gameObject);
            if(old!=null)old.gameObject.SetActive(false);
            Place(Detailed,origin,root.scene,parent);
            if(PrefabUtility.SaveAsPrefabAsset(root,Viewer)==null)throw new IOException(Viewer);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    [MenuItem("Tools/UTTVR/Bridge YTH/Bake BIM Vietnamese font")]
    public static void ApplyBimFont()
    {
        const string fontPath=Dir+"BridgeBimVietnameseStatic.asset";
        string verification="Quản lý vòng đời tài sản Cầu chính Đoạn tuyến Nhịp Thông số Chưa có dữ liệu Vận hành ƠơƯư";
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if(font==null)
        {
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            if(source==null)throw new FileNotFoundException("LiberationSans.ttf");
            font=TMP_FontAsset.CreateFontAsset(source,48,7,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,false);
            string chars=new string(Enumerable.Range(32,95).Concat(Enumerable.Range(160,224))
                .Concat(Enumerable.Range(0x300,0x70)).Concat(Enumerable.Range(0x1EA0,90))
                .Select(c=>(char)c).Concat("ƠơƯư–—→−•✓·…").Distinct().ToArray());
            font.TryAddCharacters(chars,out _);
            font.atlasPopulationMode=AtlasPopulationMode.Static;
            font.name="Bridge BIM Vietnamese Static";
            AssetDatabase.CreateAsset(font,fontPath);
            font.material.name=font.name+" Material";
            AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var atlas in font.atlasTextures.Where(t=>t!=null))
            {
                atlas.name=font.name+" Atlas";
                AssetDatabase.AddObjectToAsset(atlas,font);
            }
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
        }
        if(!font.HasCharacters(verification,out uint[] missing,false,false))
        {
            font.atlasPopulationMode=AtlasPopulationMode.Dynamic;
            font.TryAddCharacters(new string(missing.Select(c=>(char)c).ToArray()),out _);
            font.atlasPopulationMode=AtlasPopulationMode.Static;
            EditorUtility.SetDirty(font);
            foreach(var atlas in font.atlasTextures.Where(t=>t!=null))EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
        }
        if(!font.HasCharacters(verification,out missing,false,false))
            throw new InvalidOperationException("Thiếu ký tự kiểm tra: "+string.Join(",",missing));
        var scene=GetScene(Bim,out bool opened);
        if(scene.isDirty)throw new InvalidOperationException("Scene BIM đang có thay đổi chưa lưu.");
        var dashboard=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RailwayBimDashboard>(true)).Single();

        void ApplyFont(GameObject root)
        {
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                label.font=font;
                label.fontSharedMaterial=font.material;
                if(PrefabUtility.IsPartOfPrefabInstance(label)) PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                EditorUtility.SetDirty(label);
            }
        }
        const string rowPath=Dir+"BridgeBimInfoRow.prefab";
        var row=AssetDatabase.LoadAssetAtPath<GameObject>(rowPath);
        if(row==null)
        {
            var item=(GameObject)PrefabUtility.InstantiatePrefab(dashboard.rowTemplate.gameObject,scene);
            item.name="BridgeBimInfoRow";
            ApplyFont(item);
            row=PrefabUtility.SaveAsPrefabAsset(item,rowPath);
            Object.DestroyImmediate(item);
        }
        if(row==null)throw new IOException(rowPath);
        dashboard.rowTemplate=row.GetComponent<ElementInfoRow>();
        foreach(var root in scene.GetRootGameObjects())ApplyFont(root);
        EditorUtility.SetDirty(dashboard);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException(Bim);
        if(opened)EditorSceneManager.CloseScene(scene,true);
        File.WriteAllText(Temp+"font.txt",$"Static atlas validated; {font.characterTable.Count} characters; BIM scene only.");
    }

    public static void Web()
    {
        var paths=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
        if(paths.Length==0||Path.GetFileNameWithoutExtension(paths[0])!="Login"||!paths.Contains(Zone)||!paths.Contains(Bim))
            throw new InvalidOperationException("Yêu cầu danh sách scene đang dùng, Login ở đầu.");
        File.WriteAllText(Temp+"web.txt","Building");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=paths,locationPathName="Builds/BridgeModular_Web",target=BuildTarget.WebGL,targetGroup=BuildTargetGroup.WebGL,
            options=BuildOptions.Development,extraScriptingDefines=new[]{"BRIDGE_MODULAR_AUDIT"}}).summary;
        File.WriteAllText(Temp+"web.txt",$"{report.result}; {report.totalErrors} errors; {report.totalWarnings} warnings; {report.totalSize} bytes; {report.totalTime}");
        if(report.result!=BuildResult.Succeeded)throw new InvalidOperationException("Web build failed: "+report.result);
    }

    public static void PrepareAndWeb(){Prepare();Install();Web();}
}
