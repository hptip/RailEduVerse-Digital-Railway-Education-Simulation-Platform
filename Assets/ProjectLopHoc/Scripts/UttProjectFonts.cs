using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class UttProjectFonts : ScriptableObject
{
    public const string ResourceName = "UTT Project Fonts";
    public const string AtlasResource = "Fonts & Materials/UTT Vietnamese SDF";
    public Font legacyVietnamese;
    public TMP_FontAsset vietnamese;

    static UttProjectFonts projectFonts;
    static UttProjectFonts ProjectFonts => projectFonts != null
        ? projectFonts : projectFonts = Resources.Load<UttProjectFonts>(ResourceName);

    public static Font LegacyVietnamese => ProjectFonts != null && ProjectFonts.legacyVietnamese != null
        ? ProjectFonts.legacyVietnamese : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    static TMP_FontAsset packagedAtlas;
    public static TMP_FontAsset Vietnamese => ProjectFonts != null && ProjectFonts.vietnamese != null
        ? ProjectFonts.vietnamese : packagedAtlas != null
            ? packagedAtlas : packagedAtlas = Resources.Load<TMP_FontAsset>(AtlasResource);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        RegisterFallback();
        SceneManager.sceneLoaded -= RepairSceneFonts;
        SceneManager.sceneLoaded += RepairSceneFonts;
    }

    // The packaged atlas is independent of the scene and of the serialized TMP Settings fallback order.
    public static void RegisterFallback()
    {
        var atlas = Vietnamese;
        if (atlas == null || TMP_Settings.instance == null) return;
        var fallbacks = TMP_Settings.fallbackFontAssets ??= new List<TMP_FontAsset>();
        for (int i = fallbacks.Count - 1; i >= 0; i--)
            if (fallbacks[i] == null || fallbacks[i] == atlas) fallbacks.RemoveAt(i);
        fallbacks.Insert(0, atlas);
    }

    static void RepairSceneFonts(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive && scene.path.Contains("/Generated/RuntimeZones/", StringComparison.Ordinal))
            return;
        var atlas = Vietnamese;
        var legacy = LegacyVietnamese;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                if (label.font == null && atlas != null) label.font = atlas;
            foreach (var label in root.GetComponentsInChildren<Text>(true))
                if (RequiresLegacyRepair(label) && legacy != null) label.font = legacy;
        }
    }

    static bool RequiresLegacyRepair(Text label)
    {
        var font = label.font;
        if (font == null || font.name is "Arial" or "LegacyRuntime") return true;
        foreach (char character in label.text)
            if ((character is 'Đ' or 'đ' or >= '\u1EA0' and <= '\u1EF9' or 'ơ' or 'Ơ' or 'ư' or 'Ư') &&
                !font.HasCharacter(character)) return true;
        return false;
    }
}
