using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UttVr.BridgePilot
{
    [InitializeOnLoad]
    public static class BridgePilotEditor
    {
        const string Area = "Assets/ExternalBIM/BridgeDemo";
        const string Model = Area + "/Models/Bridge_SO201.dae";
        const string Report = "Documentation/BIMExpansion/BridgePilot";
        const string Job = "Temp/BridgePilot/";
        const string ScenePath = Area + "/Testing/BridgeBIM_Test.unity";
        const string PrefabPath = Area + "/BridgeBIM_Demo.prefab";

        static BridgePilotEditor() => EditorApplication.update += ServiceRequest;

        static void ServiceRequest()
        {
            if (!File.Exists(Job + "request.txt") || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                BuildPipeline.isBuildingPlayer || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = File.ReadAllText(Job + "request.txt").Trim();
            File.Delete(Job + "request.txt");
            try
            {
                if (request == "import") CreatePilot();
                else if (request == "web") BuildWeb();
            }
            catch (Exception error)
            {
                File.WriteAllText(Job + "status.txt", error.ToString());
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/UTTVR/Bridge IFC Pilot/Create test scene")]
        public static void CreatePilot()
        {
            Directory.CreateDirectory(Job);
            File.WriteAllText(Job + "status.txt", "Validating model");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Bridge pilot scene already exists. This action does not overwrite it.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (model == null) throw new InvalidOperationException("Unity did not import the converted DAE as a model.");
            var metadata = JObject.Parse(File.ReadAllText(Report + "/IFC_Properties.json"));
            var transport = JObject.Parse(File.ReadAllText(Area + "/Models/BridgeGeometryTransport.json"));
            var expected = transport["elements"].ToDictionary(e => (string)e["globalId"], StringComparer.Ordinal);
            var entries = metadata["elements"].Select(ParseEntry).ToArray();
            var subset = entries.Where(row => row.IsElement).ToDictionary(row => row.globalId, StringComparer.Ordinal);
            if (!expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(subset.Keys))
                throw new InvalidOperationException("IFC and DAE GlobalId sets differ.");
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("Bridge BIM SO 201");
                var imported = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                PrefabUtility.UnpackPrefabInstance(imported, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                imported.transform.SetParent(root.transform, false);
                var meshes = imported.GetComponentsInChildren<MeshFilter>(true);
                var matched = new Dictionary<string, List<MeshFilter>>(StringComparer.Ordinal);
                foreach (var filter in meshes)
                {
                    var node = filter.transform;
                    while (node != null && !subset.ContainsKey(node.name)) node = node.parent;
                    if (node == null || filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null)
                        throw new InvalidOperationException("Cannot verify GUID/geometry on DAE leaf " + RelativePath(root.transform, filter.transform));
                    if (!matched.TryGetValue(node.name, out var group)) matched.Add(node.name, group = new List<MeshFilter>());
                    group.Add(filter);
                }
                if (!matched.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(subset.Keys))
                    throw new InvalidOperationException("Missing Unity GlobalIds: " + string.Join(",", subset.Keys.Except(matched.Keys)));

                var boundsById = matched.ToDictionary(p => p.Key, p => VertexBounds(p.Value));
                var alignment = ValidateGeometry(transport, expected, boundsById);
                foreach (var binding in matched)
                {
                    int expectedTriangles = (int)expected[binding.Key]["triangles"];
                    int actualTriangles = binding.Value.Sum(filter => Triangles(filter.sharedMesh));
                    if (actualTriangles != expectedTriangles)
                        throw new InvalidOperationException($"The imported triangles differ for {binding.Key}: {actualTriangles} != {expectedTriangles}.");
                }

                Directory.CreateDirectory(Area + "/Testing");
                Directory.CreateDirectory(Area + "/Materials");
                var catalog = ScriptableObject.CreateInstance<BridgeBimCatalog>();
                catalog.schema = (string)metadata["summary"]["schema"];
                catalog.sourceSha256 = (string)metadata["summary"]["sourceSha256"];
                catalog.entries = entries;
                AssetDatabase.CreateAsset(catalog, Area + "/BridgeBIM_Catalog.asset");

                var adapter = root.AddComponent<BridgeBimAdapter>();
                adapter.catalog = catalog;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader missing");
                adapter.highlightMaterial = CreateMaterial(shader, "BridgeHighlight", new Color(1, .43f, .06f));
                var materialCopies = new Dictionary<Material, Material>();
                var mapping = new List<string> { "GlobalId,AssetID,ParentGlobalId,IfcClass,Name,UnityTransform,RendererCount,MeshFilterCount,ColliderCount,Triangles,Status" };
                foreach (var entry in entries)
                {
                    if (!matched.TryGetValue(entry.globalId, out var group))
                    {
                        mapping.Add(Csv(entry.globalId, entry.assetId, entry.parentGlobalId, entry.ifcClass, entry.name, "", 0, 0, 0, 0, "IFC_GROUP_WITHOUT_GEOMETRY"));
                        continue;
                    }
                    var node = group[0].transform;
                    while (node.name != entry.globalId) node = node.parent;
                    var renderers = group.Select(filter => (Renderer)filter.GetComponent<MeshRenderer>()).ToArray();
                    foreach (var filter in group)
                    {
                        var renderer = filter.GetComponent<MeshRenderer>();
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(source => PilotMaterial(source, shader, materialCopies)).ToArray();
                        filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    }
                    node.gameObject.AddComponent<BridgeBimIdentity>().Configure(entry.globalId, renderers);
                    mapping.Add(Csv(entry.globalId, entry.assetId, entry.parentGlobalId, entry.ifcClass, entry.name,
                        RelativePath(root.transform, node), renderers.Length, group.Count, group.Count,
                        group.Sum(filter => Triangles(filter.sharedMesh)), "VERIFIED"));
                }
                var whole = boundsById.Values.Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
                if (prefab == null) throw new InvalidOperationException("Could not save the bridge prefab.");
                CreateView(adapter, whole);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save the bridge scene.");
                AssetDatabase.SaveAssets();
                File.WriteAllLines(Report + "/IFC_Unity_Mapping.csv", mapping);
                var verification = new JObject
                {
                    ["verifiedIds"] = matched.Count,
                    ["meshFilters"] = meshes.Length,
                    ["meshRenderers"] = meshes.Length,
                    ["colliders"] = meshes.Length,
                    ["triangles"] = meshes.Sum(filter => Triangles(filter.sharedMesh)),
                    ["materialVariants"] = materialCopies.Count,
                    ["dimensions"] = new JArray(whole.size.x, whole.size.y, whole.size.z),
                    ["axisMapping"] = alignment.axis,
                    ["maximumBoundsErrorMeters"] = alignment.error
                };
                File.WriteAllText(Job + "verification.json", verification.ToString());
                File.WriteAllText(Job + "status.txt", "Imported and saved isolated pilot");
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static BridgeBimEntry ParseEntry(JToken row)
        {
            var properties = row["propertySets"].Concat(row["typePropertySets"]).SelectMany(set =>
            {
                var definition = set["definition"];
                string title = (string)definition["Name"];
                var values = definition["HasProperties"] ?? definition["Quantities"];
                return values == null ? new[] { title } : values.Select(value => title + " / " + (string)value["Name"] + "  " +
                    (value["NominalValue"]?["value"] ?? value["LengthValue"] ?? value["AreaValue"] ?? value["VolumeValue"] ?? value["CountValue"])?.ToString());
            }).ToArray();
            return new BridgeBimEntry { globalId = (string)row["globalId"], assetId = (string)row["assetId"],
                parentGlobalId = (string)row["parentGlobalId"], ifcClass = (string)row["ifcClass"], name = (string)row["name"],
                description = (string)row["description"], predefinedType = (string)row["predefinedType"],
                tag = (string)row["tag"], propertyLines = properties };
        }

        static Bounds VertexBounds(IEnumerable<MeshFilter> meshes)
        {
            bool initialized = false;
            Bounds bounds = default;
            foreach (var filter in meshes)
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var world = filter.transform.TransformPoint(vertex);
                    if (!initialized) { bounds = new Bounds(world, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(world);
                }
            if (!initialized) throw new InvalidOperationException("Empty imported mesh.");
            return bounds;
        }

        static (string axis, float error) ValidateGeometry(JObject transport, Dictionary<string, JToken> expected, Dictionary<string, Bounds> actual)
        {
            var whole = actual.Values.Aggregate((a, b) => { a.Encapsulate(b); return a; });
            var originalCenter = (Vector(transport["minimum"]) + Vector(transport["maximum"])) * .5f;
            float best = float.PositiveInfinity;
            string axisLabel = "";
            foreach (int x in new[] { 0, 1 })
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
            {
                Vector3 Transform(Vector3 point) => new Vector3(sx * point[x], point.z, sz * point[1 - x]);
                Vector3 offset = whole.center - Transform(originalCenter);
                float error = 0;
                foreach (var pair in actual)
                {
                    var source = expected[pair.Key];
                    var a = Transform(Vector(source["minimum"])) + offset;
                    var b = Transform(Vector(source["maximum"])) + offset;
                    error = Mathf.Max(error, (pair.Value.min - Vector3.Min(a, b)).magnitude, (pair.Value.max - Vector3.Max(a, b)).magnitude);
                }
                if (error >= best) continue;
                best = error;
                axisLabel = $"Unity X={sx}*DAE[{x}], Y=DAE[2], Z={sz}*DAE[{1 - x}]";
            }
            if (best > .02f) throw new InvalidOperationException($"DAE to Unity coordinate validation failed, best bounds error {best:F4} m.");
            return (axisLabel, best);
        }

        static Vector3 Vector(JToken a) => new Vector3((float)a[0], (float)a[1], (float)a[2]);
        static int Triangles(Mesh mesh) => Enumerable.Range(0, mesh.subMeshCount).Sum(i => checked((int)mesh.GetIndexCount(i) / 3));
        static string RelativePath(Transform root, Transform node) => node == root ? root.name : RelativePath(root, node.parent) + "/" + node.name;
        static string Csv(params object[] fields) => string.Join(",", fields.Select(v => "\"" + Convert.ToString(v, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\""));

        static Material CreateMaterial(Shader shader, string name, Color color)
        {
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .18f);
            AssetDatabase.CreateAsset(material, Area + "/Materials/" + name + ".mat");
            return material;
        }

        static Material PilotMaterial(Material source, Shader shader, Dictionary<Material, Material> copies)
        {
            if (source == null) throw new InvalidOperationException("Unassigned DAE material.");
            if (copies.TryGetValue(source, out var material)) return material;
            Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.GetColor("_Color");
            material = CreateMaterial(shader, "BridgeStyle_" + copies.Count.ToString("00"), color);
            copies.Add(source, material);
            return material;
        }

        static void CreateView(BridgeBimAdapter adapter, Bounds bounds)
        {
            var gameCamera = new GameObject("Bridge Camera", typeof(Camera));
            var camera = gameCamera.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(84, 116, 140, 255);
            camera.fieldOfView = 48;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 450;
            var rotation = Quaternion.Euler(23, 27, 0);
            camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * (bounds.size.x * .93f), rotation);
            var light = new GameObject("Bridge Key", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.45f;
            light.transform.rotation = Quaternion.Euler(49, -36, 0);
            var fill = new GameObject("Bridge Fill", typeof(Light)).GetComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = .55f;
            fill.transform.rotation = Quaternion.Euler(16, 150, 0);
            var view = gameCamera.AddComponent<BridgePilotView>();
            view.adapter = adapter;
            view.view = camera;
            view.bridgeBounds = bounds;
            view.interfaceFont = PilotFont();
        }

        static Font PilotFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            if (font == null) throw new InvalidOperationException("Embedded Vietnamese-capable font not found.");
            return font;
        }

        static void EnsurePilotPresentation()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openHere = !scene.IsValid() || !scene.isLoaded;
            if (openHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BridgePilotView>()).Single();
                if (view.interfaceFont != null) return;
                view.interfaceFont = PilotFont();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the test scene font binding.");
            }
            finally { if (openHere) EditorSceneManager.CloseScene(scene, true); }
        }

        public static void BuildWeb()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Activate Web in Unity before building the isolated pilot.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("The open project has unsaved scenes.");
            Directory.CreateDirectory(Job);
            EnsurePilotPresentation();
            File.WriteAllText(Job + "status.txt", "Building isolated Web pilot");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/BridgeBIM_WebPilot",
                target = BuildTarget.WebGL, targetGroup = BuildTargetGroup.WebGL, options = BuildOptions.Development
            });
            var summary = report.summary;
            File.WriteAllLines(Job + "WebBuild.txt", new[] { "Result=" + summary.result, "Errors=" + summary.totalErrors,
                    "Warnings=" + summary.totalWarnings, "Time=" + summary.totalTime, "Bytes=" + summary.totalSize }
                .Concat(report.steps.SelectMany(step => step.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content)));
            File.WriteAllText(Job + "status.txt", summary.result.ToString());
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Bridge pilot Web build " + summary.result + ". See " + Job + "WebBuild.txt.");
        }
    }
}
