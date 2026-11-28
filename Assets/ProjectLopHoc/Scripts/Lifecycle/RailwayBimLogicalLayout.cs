using System;
using System.Collections.Generic;
using System.Linq;
using UttVr.BridgePilot;

internal sealed class RailwayBimSelectionScope
{
    public readonly string Key, Label;
    public readonly HashSet<string> AssetIds;
    public readonly string[] Zones;
    public readonly int ProfileCount;
    public readonly float Start, End;
    public readonly bool WholeRoute;

    public RailwayBimSelectionScope(string key, string label, IEnumerable<string> assets, IEnumerable<string> zones,
        int profileCount, float start, float end, bool wholeRoute)
    {
        Key = key;
        Label = label;
        AssetIds = new HashSet<string>(assets, StringComparer.Ordinal);
        Zones = zones.Distinct(StringComparer.Ordinal).OrderBy(zone => zone, StringComparer.Ordinal).ToArray();
        ProfileCount = profileCount;
        Start = start;
        End = end;
        WholeRoute = wholeRoute;
    }
}

public static class RailwayBimLogicalLayout
{
    public sealed class Section
    {
        public readonly string Key, Label;
        public readonly string[] Zones;
        public Section(string key, string label, params string[] zones)
        {
            Key = key;
            Label = label;
            Zones = zones;
        }
    }

    // Logical management ownership. Zone_017 is shared by the open-line OCL and
    // bridge OCL. Each occurrence is assigned once by its canonical ID.
    public static readonly Section[] Sections =
    {
        new("formation", "01 · Nền đường", "Zone_011"),
        new("forest02", "02 · Hầm Forest_02", "Zone_012", "Zone_013"),
        new("forest01", "03 · Hầm Forest_01", "Zone_014", "Zone_015"),
        new("open", "04 · Điện tiếp xúc ngoài trời", "Zone_016", "Zone_017"),
        new("bridge", "05 · Khu vực cầu SO 201", "Zone_017", "Zone_018"),
        new("rail", "06 · Đường ray Z021", "Zone_021")
    };

    public static Section ForInfrastructure(string id, string zone)
    {
        if (id.StartsWith("G2-OCL-Bridge-", StringComparison.Ordinal)) return Sections[4];
        return Sections.FirstOrDefault(section => section.Zones.Contains(zone));
    }

    public static string Domain(string type) => type switch
    {
        "Subgrade" => "Nền đường",
        "Tunnel" => "Kết cấu hầm",
        "OCL" => "Điện tiếp xúc",
        _ => type
    };

    public static string InfrastructureType(BridgeBimEntry entry, string assembly)
    {
        string slot = entry.assetId.Substring(entry.assetId.LastIndexOf('-') + 1);
        if (entry.ifcClass == "Subgrade")
        {
            if (slot.StartsWith("Layer", StringComparison.Ordinal)) return "Các lớp nền";
            if (slot.Contains("Drain", StringComparison.Ordinal)) return "Thoát nước";
            if (slot.Contains("Wall", StringComparison.Ordinal)) return "Tường chắn";
            if (slot.Contains("Slope", StringComparison.Ordinal) || slot.Contains("Diamond", StringComparison.Ordinal)) return "Mái dốc gia cố";
            if (slot.Contains("Cable", StringComparison.Ordinal)) return "Ống và rãnh cáp";
            return "Cấu kiện nền khác";
        }
        if (entry.ifcClass == "Tunnel")
        {
            if (assembly.Contains("Portal", StringComparison.Ordinal)) return "Cửa hầm";
            if (slot.Contains("Slab", StringComparison.Ordinal) || slot.Contains("Invert", StringComparison.Ordinal)) return "Bản đáy";
            if (slot.Length > 1 && slot[0] == 'A' && char.IsDigit(slot[1])) return "Vỏ hầm";
            if (slot.Contains("Bolt", StringComparison.Ordinal)) return "Liên kết vỏ hầm";
            return "Thiết bị và cấu kiện trong hầm";
        }
        if (slot.Contains("Wire", StringComparison.Ordinal) || slot.Contains("Cable", StringComparison.Ordinal)) return "Dây và cáp";
        if (slot.Contains("Insulator", StringComparison.Ordinal) || slot.Contains("Ceramic", StringComparison.Ordinal)) return "Cách điện";
        if (slot.Contains("Mast", StringComparison.Ordinal) || slot.Contains("Column", StringComparison.Ordinal)) return "Cột";
        if (slot.Contains("Anchor", StringComparison.Ordinal) || slot.Contains("Foundation", StringComparison.Ordinal) ||
            slot.Contains("Base", StringComparison.Ordinal)) return "Móng, đế và neo";
        return "Xà và phụ kiện";
    }

    public static string BridgeType(BridgeBimEntry entry)
    {
        var name = entry.sourceName ?? entry.Label;
        bool Has(string text) => name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        if (Has("pile cap")) return "Đài móng";
        if (Has("bearing")) return "Gối và bệ gối";
        if (entry.ifcClass == "IfcPile" || Has("pile")) return "Cọc";
        if (entry.ifcClass == "IfcColumn" || Has("pier") || Has("abutment")) return "Trụ và mố";
        if (entry.ifcClass == "IfcBeam" || entry.ifcClass == "IfcSlab" || Has("deck")) return "Kết cấu nhịp";
        return "Cấu kiện cầu khác";
    }
}
