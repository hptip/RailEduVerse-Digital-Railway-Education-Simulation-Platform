using TMPro;
using UnityEngine;

public sealed class UttProjectFonts : ScriptableObject
{
    public const string ResourceName = "UTT Project Fonts";
    public Font legacyVietnamese;
    public TMP_FontAsset vietnamese;

    static UttProjectFonts projectFonts;
    static UttProjectFonts ProjectFonts => projectFonts != null
        ? projectFonts : projectFonts = Resources.Load<UttProjectFonts>(ResourceName);

    public static Font LegacyVietnamese => ProjectFonts != null && ProjectFonts.legacyVietnamese != null
        ? ProjectFonts.legacyVietnamese : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static TMP_FontAsset Vietnamese => ProjectFonts != null && ProjectFonts.vietnamese != null
        ? ProjectFonts.vietnamese : TMP_Settings.defaultFontAsset;
}
