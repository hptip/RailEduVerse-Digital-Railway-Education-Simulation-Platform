using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UttVr.BridgePilot;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SO201ProductionIntegration
{
    const string Job = "Temp/SO201Production/";
    const string Docs = "Documentation/BIMExpansion/SO201ProductionIntegration/";
    const string Evidence = "Documentation/BIMExpansion/SO201RailwayReuse/";
    const string Candidate = "Assets/ExternalBIM/SO201RouteCandidate/";
    const string Zone = "Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_018.unity";
    const string Viewer = "Assets/ExternalBIM/ZoneBased/Resources/RouteBimZones/Zone_018.prefab";
    const string Bim = "Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity";
    const string PilotCatalog = "Assets/ExternalBIM/BridgeDemo/BridgeBIM_Catalog.asset";
    const string Visual = Candidate + "SO201_Full_Visual_Optimized.prefab";
    const string Detailed = Candidate + "SO201_Full_Detailed.prefab";
    const string Catalog = Candidate + "SO201_Full_Catalog.asset";
    const string Branch = "FixedInfrastructure_Bridge";
    const string PrimaryVisual = "Bridge_YTH_Visual";
    const string LegacyVisual = "Legacy_Modular_Android_Fallback";

    [Serializable] sealed class RouteOrigin { public Vector3 sourceWorldOrigin; }

    static SO201ProductionIntegration() => EditorApplication.update += Watch;

    static void Watch()
    {
        string request = Job + "request.txt";
        if (!File.Exists(request) || EditorApplication.isUpdating || EditorApplication.isCompiling ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        string action = File.ReadAllText(request).Trim();
        File.Delete(request);
        try
        {
            if (action == "install") Install();
            if (action == "web") BuildWeb();
        }
        catch (Exception error)
        {
            File.WriteAllText(Job + action + "-error.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/UTTVR/Bridge SO201/Install verified production configuration")]
    public static void Install()
    {
        if (!File.Exists(Docs + "Snapshots/Before_Integration.zip"))
            throw new IOException("Production rollback snapshot missing.");
        Directory.CreateDirectory(Job);
        foreach (string path in new[] { Zone, Bim })
            if (SceneManager.GetSceneByPath(path).isDirty) throw new InvalidOperationException("Unsaved scene: " + path);

        var numbers = File.ReadAllText(Evidence + "Full_Measurements.txt").Split(new[] { "origin=" }, StringSplitOptions.None)
            .Last().Trim().Split('|').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        var origin = new Vector3(numbers[0], numbers[1], numbers[2]);
        var localOrigin = JsonUtility.FromJson<RouteOrigin>(File.ReadAllText(
            "Assets/ProjectLopHoc/Generated/QuanLyVongDoiTaiSan/SceneProvenance.json")).sourceWorldOrigin;
        var detailed = AssetDatabase.LoadAssetAtPath<GameObject>(Detailed);
        var visual = AssetDatabase.LoadAssetAtPath<GameObject>(Visual);
        var catalog = AssetDatabase.LoadAssetAtPath<BridgeBimCatalog>(Catalog);
        if (visual.GetComponentsInChildren<MeshRenderer>(true).Length != 129 ||
            visual.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
            detailed.GetComponent<BridgeBimAdapter>().catalog != catalog)
            throw new InvalidOperationException("Verified candidate configuration not found.");
        UpdateProvenance(catalog, detailed);

        ChangeScene(Zone, scene =>
        {
            var branch = scene.GetRootGameObjects().Single(g => g.name == "RuntimeZoneContent").transform.Find(Branch);
            var options = branch.GetComponent<RailwayBridgeVisualPlatform>();
            if (options == null)
            {
                var legacy = branch.Find(PrimaryVisual).gameObject;
                legacy.name = LegacyVisual;
                legacy.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(legacy);
                var instance = Place(visual, scene, branch, PrimaryVisual, origin);
                options = branch.gameObject.AddComponent<RailwayBridgeVisualPlatform>();
                options.legacyVisual = legacy;
                options.so201Visual = instance;
            }
            else
            {
                options.so201Visual.transform.position = origin;
                options.so201Visual.SetActive(true);
                options.legacyVisual.SetActive(false);
            }
            if (branch.Find("ExtendedBridgeQuest").gameObject.activeSelf || branch.Find("BridgeValleyWaterQuest") == null)
                throw new InvalidOperationException("Canonical bridge context changed.");
        });

        var viewRoot = PrefabUtility.LoadPrefabContents(Viewer);
        try
        {
            var branch = viewRoot.transform.Find(Branch);
            var legacy = branch.Find("Bridge_YTH_Detailed");
            if (legacy != null) Object.DestroyImmediate(legacy.gameObject);
            var previous = branch.Find("SO201_Full_Detailed");
            if (previous == null) Place(detailed, viewRoot.scene, branch, "SO201_Full_Detailed", origin - localOrigin);
            else previous.position = origin - localOrigin;
            if (PrefabUtility.SaveAsPrefabAsset(viewRoot, Viewer) == null) throw new IOException(Viewer);
        }
        finally { PrefabUtility.UnloadPrefabContents(viewRoot); }

        ChangeScene(Bim, scene =>
        {
            var viewer = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RailwayBimZoneViewer>(true)).Single();
            viewer.modularBridgeCatalog = catalog;
            viewer.facilities.Single(f => f.key == "Bridge_Main").label = "Cầu chính SO 201";
        });
        AssetDatabase.SaveAssets();
        File.WriteAllText(Job + "installed.txt", "Canonical Zone_018 and BIM scene saved; 129 Visual renderers; 1,486 Detailed identities.");
    }

    static void UpdateProvenance(BridgeBimCatalog catalog, GameObject detailed)
    {
        var original = AssetDatabase.LoadAssetAtPath<BridgeBimCatalog>(PilotCatalog).entries.ToDictionary(e => e.globalId);
        var mapped = File.ReadLines(Evidence + "Full_Occurrences.csv").Skip(1)
            .Select(line => Regex.Matches(line, "\"([^\"]*)\"").Cast<Match>().Select(match => match.Groups[1].Value).ToArray())
            .ToDictionary(parts => parts[0], StringComparer.Ordinal);
        var identities = detailed.GetComponentsInChildren<BridgeBimIdentity>(true).ToDictionary(i => i.GlobalId);
        var elements = catalog.entries.Where(e => e.IsElement).ToArray();
        if (elements.Length != 1486 || identities.Count != 1486 || mapped.Count != 1486 ||
            !elements.Select(e => e.assetId).ToHashSet().SetEquals(identities.Keys))
            throw new InvalidOperationException("Detailed identity count/keys differ from catalog.");
        int direct = 0;
        foreach (var entry in elements)
        {
            var row = mapped[entry.assetId];
            entry.modeledStation = float.Parse(row[3], CultureInfo.InvariantCulture);
            entry.modeledBounds = identities[entry.globalId].Renderers.Single().bounds.size;
            if (string.IsNullOrEmpty(row[4])) continue;
            var source = original[row[4]];
            entry.sourceGlobalId = source.globalId;
            entry.sourceParentGlobalId = source.parentGlobalId;
            entry.sourceIfcClass = source.ifcClass;
            entry.sourceName = source.Label;
            entry.description = source.description;
            entry.tag = source.tag;
            entry.predefinedType = source.predefinedType;
            direct++;
        }
        if (direct != 1099) throw new InvalidOperationException("Source GlobalId join incomplete.");
        EditorUtility.SetDirty(catalog);
        File.Copy(Evidence + "Full_Occurrences.csv", Docs + "Detailed_Asset_Registry.csv", true);
    }

    static GameObject Place(GameObject prefab, Scene scene, Transform parent, string name, Vector3 pose)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(pose, Quaternion.identity);
        instance.name = name;
        instance.SetActive(true);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        return instance;
    }

    static void ChangeScene(string path, Action<Scene> change)
    {
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            change(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException(path);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static void BuildWeb()
    {
        File.WriteAllText(Job + "web.txt", "Building");
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0 || Path.GetFileNameWithoutExtension(scenes[0]) != "Login" ||
            !scenes.Contains(Zone) || !scenes.Contains(Bim)) throw new InvalidOperationException("Unexpected Web scene list.");
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes, locationPathName = "Builds/SO201_Production_Web", target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL, options = BuildOptions.Development,
            extraScriptingDefines = new[] { "BRIDGE_MODULAR_AUDIT" }
        }).summary;
        File.WriteAllText(Job + "web.txt",
            $"{result.result}; {result.totalErrors} errors; {result.totalWarnings} warnings; {result.totalSize} bytes; {result.totalTime}");
        if (result.result != BuildResult.Succeeded) throw new InvalidOperationException("Production Web build failed.");
    }
}
