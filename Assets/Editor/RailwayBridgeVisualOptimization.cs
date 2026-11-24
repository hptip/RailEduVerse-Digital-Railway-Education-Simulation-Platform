using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class RailwayBridgeVisualOptimization
{
    const string Report = "Documentation/BIMExpansion/BridgeOptimization/";
    const string Inbox = "Temp/BridgePhase2/";
    const string Bridge = "Assets/ExternalBIM/BridgeModular/";
    const string ExistingVisual = Bridge + "Bridge_YTH_Visual.prefab";
    const string Output = "Assets/ExternalBIM/BridgeVisualOptimization/";
    const string Candidate = Output + "Bridge_YTH_Visual_Optimized.prefab";
    const string BaselineName = "Bridge_YTH_Visual";
    const string ZonePath = "Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_018.unity";
    const string RoutePath = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";

    [InitializeOnLoadMethod]
    static void Listen() => EditorApplication.update += ConsumeRequest;

    static void ConsumeRequest()
    {
        string request = Inbox + "request.txt";
        if (!File.Exists(request) || EditorApplication.isUpdating || EditorApplication.isCompiling ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        string action = File.ReadAllText(request).Trim();
        File.Delete(request);
        try
        {
            if (action == "generate") Generate();
            else if (action == "baseline") BuildWeb(false);
            else if (action == "trial") BuildWeb(true);
            else if (action == "verify") VerifyInstalled();
        }
        catch (Exception error)
        {
            File.WriteAllText(Inbox + action + "-error.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/UTTVR/Bridge YTH/Generate optimized Visual trial")]
    public static void Generate()
    {
        Directory.CreateDirectory(Inbox);
        if (!AssetDatabase.IsValidFolder(Output.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets/ExternalBIM", "BridgeVisualOptimization");
        if (!AssetDatabase.IsValidFolder(Output + "Meshes"))
            AssetDatabase.CreateFolder(Output.TrimEnd('/'), "Meshes");

        var source = PrefabUtility.LoadPrefabContents(ExistingVisual);
        var candidate = new GameObject("Bridge_YTH_Visual_Optimized");
        try
        {
            SceneManager.MoveGameObjectToScene(candidate, source.scene);
            candidate.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
            candidate.transform.localScale = source.transform.localScale;
            int originalRenderers = 0, mergedRenderers = 0, vertices = 0, triangles = 0;
            float pointError = 0, normalError = 0, boundsError = 0;
            using var groups = new StringWriter(CultureInfo.InvariantCulture);
            groups.WriteLine("Group,SourceRenderers,OptimizedRenderers,Vertices,Triangles,BoundsX,BoundsY,BoundsZ");
            for (int index = 0; index < source.transform.childCount; index++)
            {
                var group = source.transform.GetChild(index);
                var parts = group.GetComponentsInChildren<MeshRenderer>();
                if (parts.Length == 0) throw new InvalidOperationException("Empty group " + group.name);
                var first = parts[0];
                var material = first.sharedMaterial;
                foreach (var part in parts) ValidateRenderer(part, first);

                var item = new GameObject(group.name);
                item.transform.SetParent(candidate.transform, false);
                item.transform.SetLocalPositionAndRotation(group.localPosition, group.localRotation);
                item.transform.localScale = group.localScale;
                var center = new Bounds(parts[0].bounds.center, Vector3.zero);
                foreach (var part in parts) center.Encapsulate(part.bounds);
                item.transform.position = center.center;
                GameObjectUtility.SetStaticEditorFlags(item, GameObjectUtility.GetStaticEditorFlags(first.gameObject));
                var renderer = item.AddComponent<MeshRenderer>();
                EditorUtility.CopySerialized(first, renderer);
                var filter = item.AddComponent<MeshFilter>();
                var instances = parts.Select(part => new CombineInstance
                {
                    mesh = part.GetComponent<MeshFilter>().sharedMesh,
                    transform = item.transform.worldToLocalMatrix * part.localToWorldMatrix
                }).ToArray();
                var assembled = new Mesh { name = $"BridgeVisualGroup_{index + 1:00}" };
                if (instances.Sum(i => i.mesh.vertexCount) > ushort.MaxValue) assembled.indexFormat = IndexFormat.UInt32;
                assembled.CombineMeshes(instances, true, true, false);
                assembled.RecalculateBounds();
                int count = VerifyMesh(instances, assembled, ref pointError, ref normalError);
                string path = Output + "Meshes/" + assembled.name + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null)
                {
                    AssetDatabase.CreateAsset(assembled, path);
                    saved = assembled;
                }
                else
                {
                    EditorUtility.CopySerialized(assembled, saved);
                    UnityEngine.Object.DestroyImmediate(assembled);
                }
                filter.sharedMesh = saved;
                if (renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial != material)
                    throw new InvalidOperationException("Material changed for " + group.name);

                // Compare the actual transformed geometry bounds, not the looser oriented renderer AABB.
                var baseBounds = ExactBounds(instances);
                boundsError = Mathf.Max(boundsError, (baseBounds.min - saved.bounds.min).magnitude,
                    (baseBounds.max - saved.bounds.max).magnitude);
                originalRenderers += parts.Length;
                mergedRenderers++;
                vertices += saved.vertexCount;
                triangles += count;
                groups.WriteLine(FormattableString.Invariant(
                    $"\"{group.name}\",{parts.Length},1,{saved.vertexCount},{count},{saved.bounds.size.x:F5},{saved.bounds.size.y:F5},{saved.bounds.size.z:F5}"));
            }
            if (originalRenderers != 420 || mergedRenderers != 84 || pointError > .0001f ||
                normalError > .0001f || boundsError > .0001f)
                throw new InvalidOperationException("The optimized Visual failed its geometry limits.");
            if (PrefabUtility.SaveAsPrefabAsset(candidate, Candidate) == null) throw new IOException(Candidate);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Report + "Visual_Group_Inventory.csv", groups.ToString());
            File.WriteAllText(Inbox + "geometry.txt", FormattableString.Invariant(
                $"SourceRenderers={originalRenderers}; TrialRenderers={mergedRenderers}; Vertices={vertices}; Triangles={triangles}; MaxPositionErrorMeters={pointError:E6}; MaxNormalVectorError={normalError:E6}; MaxBoundsErrorMeters={boundsError:E6}"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(candidate);
            PrefabUtility.UnloadPrefabContents(source);
        }
    }

    static void ValidateRenderer(MeshRenderer part, MeshRenderer first)
    {
        var mesh = part.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null || !mesh.isReadable || mesh.subMeshCount != 1 || mesh.GetTopology(0) != MeshTopology.Triangles ||
            part.localToWorldMatrix.determinant <= 0 || !part.enabled ||
            part.sharedMaterials.Length != 1 || part.sharedMaterial != first.sharedMaterial ||
            part.shadowCastingMode != first.shadowCastingMode || part.receiveShadows != first.receiveShadows ||
            part.lightProbeUsage != first.lightProbeUsage || part.reflectionProbeUsage != first.reflectionProbeUsage ||
            part.renderingLayerMask != first.renderingLayerMask || part.gameObject.layer != first.gameObject.layer ||
            part.lightmapIndex >= 0 || part.realtimeLightmapIndex >= 0 ||
            part.probeAnchor != first.probeAnchor || part.motionVectorGenerationMode != first.motionVectorGenerationMode)
            throw new InvalidOperationException("Incompatible mesh or rendering state: " + part.name);
    }

    static Bounds ExactBounds(CombineInstance[] parts)
    {
        var result = new Bounds(parts[0].transform.MultiplyPoint3x4(parts[0].mesh.vertices[0]), Vector3.zero);
        foreach (var part in parts)
            foreach (var point in part.mesh.vertices)
                result.Encapsulate(part.transform.MultiplyPoint3x4(point));
        return result;
    }

    static int VerifyMesh(CombineInstance[] parts, Mesh result, ref float pointError, ref float normalError)
    {
        if (result.subMeshCount != 1) throw new InvalidOperationException("Material slot count changed.");
        var points = result.vertices;
        var normals = result.normals;
        var colors = result.colors;
        var winding = result.GetIndices(0);
        int vertexOffset = 0, indexOffset = 0;
        foreach (var part in parts)
        {
            var mesh = part.mesh;
            var sourcePoints = mesh.vertices;
            var sourceNormals = mesh.normals;
            var sourceColors = mesh.colors;
            var normalTransform = part.transform.inverse.transpose;
            if (sourceNormals.Length != sourcePoints.Length || normals.Length != points.Length ||
                (sourceColors.Length != 0 && colors.Length != points.Length) ||
                mesh.HasVertexAttribute(VertexAttribute.Tangent) || result.HasVertexAttribute(VertexAttribute.Tangent))
                throw new InvalidOperationException("Unexpected vertex streams in " + mesh.name);
            for (int i = 0; i < sourcePoints.Length; i++)
            {
                int at = vertexOffset + i;
                pointError = Mathf.Max(pointError, (points[at] - part.transform.MultiplyPoint3x4(sourcePoints[i])).magnitude);
                var normal = normalTransform.MultiplyVector(sourceNormals[i]).normalized;
                normalError = Mathf.Max(normalError, (normals[at].normalized - normal).magnitude);
                if (sourceColors.Length > 0 && colors[at] != sourceColors[i])
                    throw new InvalidOperationException("Vertex color changed: " + mesh.name);
            }
            var sourceUv = new System.Collections.Generic.List<Vector4>();
            var mergedUv = new System.Collections.Generic.List<Vector4>();
            for (int channel = 0; channel < 8; channel++)
            {
                mesh.GetUVs(channel, sourceUv);
                result.GetUVs(channel, mergedUv);
                if (sourceUv.Count == 0 && mergedUv.Count == 0) continue;
                if (sourceUv.Count != sourcePoints.Length || mergedUv.Count != points.Length)
                    throw new InvalidOperationException("UV stream changed: " + mesh.name);
                for (int i = 0; i < sourceUv.Count; i++)
                    if ((sourceUv[i] - mergedUv[vertexOffset + i]).sqrMagnitude > 1e-10f)
                        throw new InvalidOperationException("UV changed: " + mesh.name);
            }
            foreach (int sourceIndex in mesh.GetIndices(0))
                if (winding[indexOffset++] != sourceIndex + vertexOffset)
                    throw new InvalidOperationException("Triangle winding or topology changed.");
            vertexOffset += sourcePoints.Length;
        }
        if (points.Length != vertexOffset || winding.Length != indexOffset)
            throw new InvalidOperationException("Vertex or index count changed.");
        return indexOffset / 3;
    }

    static void ReplaceVisual(Scene scene, string assetPath)
    {
        var branch = scene.GetRootGameObjects().Single(root => root.name == "RuntimeZoneContent")
            .transform.Find("FixedInfrastructure_Bridge");
        var original = branch.Find(BaselineName);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (original == null || prefab == null) throw new InvalidOperationException("Trial Visual unavailable.");
        var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        replacement.name = BaselineName;
        replacement.transform.SetParent(branch, false);
        replacement.transform.SetLocalPositionAndRotation(original.localPosition, original.localRotation);
        replacement.transform.localScale = original.localScale;
        replacement.SetActive(original.gameObject.activeSelf);
        UnityEngine.Object.DestroyImmediate(original.gameObject);
    }

    static void VerifyInstalled()
    {
        var scene = SceneManager.GetSceneByPath(ZonePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ZonePath, OpenSceneMode.Additive);
        try
        {
            var content = scene.GetRootGameObjects().Single(root => root.name == "RuntimeZoneContent").transform;
            var branch = content.Find("FixedInfrastructure_Bridge");
            var activeVisual = branch.Find(BaselineName).gameObject;
            var historicalBridge = branch.Find("ExtendedBridgeQuest").gameObject;
            int count = activeVisual.GetComponentsInChildren<MeshRenderer>(true).Count(renderer => renderer.enabled);
            bool visibleOnLoad = branch.gameObject.activeSelf ||
                content.GetComponent<RailwayZoneActivation>().chunks.Contains(branch.gameObject);
            if (!activeVisual.activeSelf || historicalBridge.activeSelf || branch.Find("BridgeValleyWaterQuest") == null ||
                count != 84 || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(activeVisual) != Candidate ||
                !visibleOnLoad)
                throw new InvalidOperationException("Installed Zone_018 differs from the validated configuration.");
            File.WriteAllText(Inbox + "installed.txt", "Canonical Zone_018 opened; 84 enabled renderers, trial prefab bound, historical Visual inactive, water and activation retained.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static void StageVariant(string folder, string[] scenes, string visual)
    {
        string zoneCopy = folder + "/Zone_018.unity";
        string routeCopy = folder + "/YenTuHaLongXanh.unity";
        if (!AssetDatabase.CopyAsset(ZonePath, zoneCopy) || !AssetDatabase.CopyAsset(RoutePath, routeCopy))
            throw new IOException("Could not copy the two trial scenes.");

        SaveCopy(zoneCopy, scene => ReplaceVisual(scene, visual));
        SaveCopy(routeCopy, scene =>
        {
            var manager = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var zone = manager.zones.Single(record => record.zoneId == "Zone_018");
            if (zone.scenePath != ZonePath) throw new InvalidOperationException("Unexpected baseline Zone_018 path.");
            zone.scenePath = zoneCopy;
        });
        scenes[Array.IndexOf(scenes, ZonePath)] = zoneCopy;
        scenes[Array.IndexOf(scenes, RoutePath)] = routeCopy;
    }

    static void SaveCopy(string path, Action<Scene> change)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            change(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    static void BuildWeb(bool optimize)
    {
        Directory.CreateDirectory(Inbox);
        string label = optimize ? "trial" : "baseline";
        string state = Inbox + label + "-build.txt";
        File.WriteAllText(state, "Building");
        string stage = null;
        try
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0 || Path.GetFileNameWithoutExtension(scenes[0]) != "Login" ||
                !scenes.Contains(ZonePath) || !scenes.Contains(RoutePath))
                throw new InvalidOperationException("Expected Login and Zone_018 in the current Web scene list.");
            if (optimize || AssetDatabase.GetDependencies(ZonePath, false).Contains(Candidate))
            {
                // Distinct temporary scene assets force Unity to serialize the actual candidate.
                // Build callbacks can be skipped when the serialized scene is reused from cache.
                string folder = "Trial_" + Guid.NewGuid().ToString("N");
                stage = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(Output.TrimEnd('/'), folder));
                if (string.IsNullOrEmpty(stage)) throw new IOException("Trial stage could not be created.");
                StageVariant(stage, scenes, optimize ? Candidate : ExistingVisual);
            }
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = optimize ? "Builds/BridgePhase2_Trial" : "Builds/BridgePhase2_Baseline",
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.Development,
                extraScriptingDefines = new[] { "BRIDGE_MODULAR_AUDIT" }
            }).summary;
            File.WriteAllText(state, $"{result.result}; {result.totalErrors} errors; {result.totalWarnings} warnings; {result.totalSize} bytes; {result.totalTime}");
            if (result.result != BuildResult.Succeeded) throw new InvalidOperationException("Web " + label + " build failed.");
        }
        finally { if (!string.IsNullOrEmpty(stage)) AssetDatabase.DeleteAsset(stage); }
    }
}
