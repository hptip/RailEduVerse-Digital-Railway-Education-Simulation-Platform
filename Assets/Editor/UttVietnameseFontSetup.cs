using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

[InitializeOnLoad]
static class UttVietnameseFontSetup
{
    const string Work = "Temp/UTTVietnameseFonts/";
    const string Source = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
    const string Settings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    const string Atlas = "Assets/TextMesh Pro/Resources/Fonts & Materials/UTT Vietnamese SDF.asset";
    const string Configuration = "Assets/Resources/" + UttProjectFonts.ResourceName + ".asset";
    const string Extra = "ĐđĂăÂâÊêÔôƠơƯư–—→←−•·…×₫";

    static UttVietnameseFontSetup() => EditorApplication.update += Watch;

    static void Watch()
    {
        string request = Work + "request.txt";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        string action = File.ReadAllText(request).Trim();
        File.Delete(request);
        try
        {
            if (action == "setup") Setup();
            else if (action == "web") BuildWeb();
        }
        catch (Exception error)
        {
            File.WriteAllText(Work + action + "-error.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/UTTVR/Fonts/Set up shared Vietnamese fonts")]
    static void Setup()
    {
        Directory.CreateDirectory(Work);
        var source = AssetDatabase.LoadAssetAtPath<Font>(Source);
        if (source == null) throw new FileNotFoundException(Source);
        // Latin-1, Vietnamese base vowels and pairwise diacritics, plus the existing UI punctuation.
        string required = new string(Enumerable.Range(32, 95).Concat(Enumerable.Range(0x1EA0, 90))
            .Select(code => (char)code).Concat("ÀÁÃÈÉÌÍÒÓÕÙÚÝàáãèéìíòóõùúý".Concat(Extra)).Distinct().ToArray());
        var atlas = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Atlas);
        if (atlas == null)
        {
            atlas = TMP_FontAsset.CreateFontAsset(source, 56, 8, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, false);
            string characters = new string(required.Concat(Enumerable.Range(160, 224).Select(code => (char)code))
                .Concat("̛̣̀́̃̉̂̆").Distinct().ToArray());
            atlas.TryAddCharacters(characters, out _);
            Validate(atlas, required);
            atlas.atlasPopulationMode = AtlasPopulationMode.Static;
            atlas.name = "UTT Vietnamese SDF";
            AssetDatabase.CreateAsset(atlas, Atlas);
            atlas.material.name = "UTT Vietnamese SDF Material";
            AssetDatabase.AddObjectToAsset(atlas.material, atlas);
            foreach (var texture in atlas.atlasTextures) AssetDatabase.AddObjectToAsset(texture, atlas);
            EditorUtility.SetDirty(atlas);
        }
        Validate(atlas, required);
        if (atlas.atlasPopulationMode != AtlasPopulationMode.Static)
            throw new InvalidOperationException("The shared TMP atlas must be static for Web and Android.");
        string legacyProbe = "Đăng Nhập Chuyển không gian Đường sắt ệỡựữ ơ ư đ";
        var missingLegacy = legacyProbe.Where(character => !source.HasCharacter(character)).Distinct().ToArray();
        if (missingLegacy.Length != 0) throw new InvalidOperationException("Legacy font missing: " + new string(missingLegacy));

        var projectFonts = AssetDatabase.LoadAssetAtPath<UttProjectFonts>(Configuration);
        if (projectFonts == null)
        {
            projectFonts = ScriptableObject.CreateInstance<UttProjectFonts>();
            AssetDatabase.CreateAsset(projectFonts, Configuration);
        }
        projectFonts.legacyVietnamese = source;
        projectFonts.vietnamese = atlas;
        EditorUtility.SetDirty(projectFonts);

        var tmpSettings = new SerializedObject(AssetDatabase.LoadAssetAtPath<TMP_Settings>(Settings));
        var fallback = tmpSettings.FindProperty("m_fallbackFontAssets");
        for (int i = fallback.arraySize - 1; i >= 0; i--)
        {
            var item = fallback.GetArrayElementAtIndex(i);
            if (item.objectReferenceValue != atlas) continue;
            item.objectReferenceValue = null;
            fallback.DeleteArrayElementAtIndex(i);
        }
        fallback.InsertArrayElementAtIndex(0);
        fallback.GetArrayElementAtIndex(0).objectReferenceValue = atlas;
        tmpSettings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, atlas);
        File.WriteAllText(Work + "font.txt",
            $"PASS: {required.Length} required characters, {atlas.characterTable.Count} baked characters, legacy probe complete.");
    }

    static void Validate(TMP_FontAsset atlas, string required)
    {
        if (!atlas.HasCharacters(required, out uint[] missing, false, false))
            throw new InvalidOperationException("Missing required glyphs: " +
                string.Join(", ", missing.Select(value => $"U+{value:X4}")));
    }

    static void BuildWeb()
    {
        File.WriteAllText(Work + "web.txt", "Building");
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length == 0 || Path.GetFileNameWithoutExtension(scenes[0]) != "Login")
            throw new InvalidOperationException("Expected the existing Web scene list beginning with Login.");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/UTTVR_Vietnamese_Web",
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.Development
        }).summary;
        File.WriteAllText(Work + "web.txt",
            $"{report.result}; {report.totalErrors} errors; {report.totalWarnings} warnings; {report.totalSize} bytes; {report.totalTime}");
        if (report.result != BuildResult.Succeeded) throw new InvalidOperationException("Web build failed.");
    }
}
