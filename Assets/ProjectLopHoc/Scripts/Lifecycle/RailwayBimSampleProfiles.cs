using System;
using System.Collections.Generic;
using UttVr.BridgePilot;

internal static class RailwayBimSampleProfiles
{
    internal enum Kind { Formation, TunnelPortal, OpenMast, BridgeMast, Girder }

    internal readonly struct Profile
    {
        public readonly string Name;
        public readonly Kind Type;

        public Profile(string name, Kind type)
        {
            Name = name;
            Type = type;
        }
    }

    static readonly Dictionary<string, Profile> Profiles = new(StringComparer.Ordinal)
    {
        ["G2-Subgrade-S0400-000-Layer00"] = new("Lớp nền trên cùng · S0400-000", Kind.Formation),
        ["G2-Tunnel-Forest_02-Portal-In-PortalArch"] = new("Vòm cửa vào hầm Forest 02", Kind.TunnelPortal),
        ["G2-Tunnel-Forest_02-Portal-Out-PortalRearArch"] = new("Vòm cửa ra hầm Forest 02", Kind.TunnelPortal),
        ["G2-Tunnel-Forest_01-Portal-In-PortalArch"] = new("Vòm cửa vào hầm Forest 01", Kind.TunnelPortal),
        ["G2-Tunnel-Forest_01-Portal-Out-PortalRearArch"] = new("Vòm cửa ra hầm Forest 01", Kind.TunnelPortal),
        ["G2-OCL-Open-P000-P0L-Mast"] = new("Thân cột điện tiếp xúc · Đường thường P000", Kind.OpenMast),
        ["G2-OCL-Bridge-P000-P0L-Mast"] = new("Thân cột điện tiếp xúc · Khu vực cầu P000", Kind.BridgeMast),
        ["G2-OCL-Bridge-P003-P0L-Mast"] = new("Thân cột điện tiếp xúc · Khu vực cầu P003", Kind.BridgeMast),
        ["BRIDGE-SO201-1HfwXIbME2yQxwO10KnFqY"] = new("Dầm chủ số 3", Kind.Girder)
    };

    public static bool Contains(string id) => TryGet(id, out _);

    public static bool TryGet(string id, out Profile profile)
    {
        profile = default;
        return !string.IsNullOrEmpty(id) && Profiles.TryGetValue(id, out profile);
    }

    public static bool TryGetName(string id, out string name)
    {
        bool found = TryGet(id, out var profile);
        name = profile.Name;
        return found;
    }

    public static string SearchText(string id) => Contains(id) ? "hồ sơ mẫu ho so mau" : "";

    public static string Function(Kind type) => type switch
    {
        Kind.Formation => "Tạo bề mặt liên tục dưới kết cấu đường ray trong đoạn nền có gia cố.",
        Kind.TunnelPortal => "Tạo hình lối vào hoặc ra và chuyển tiếp giữa thân hầm với sườn núi.",
        Kind.OpenMast => "Đỡ cụm xà và phụ kiện treo dây điện tiếp xúc dọc đoạn đường ngoài trời.",
        Kind.BridgeMast => "Đỡ cụm xà điện tiếp xúc trong phạm vi cầu và chuyển tải tại chân cột.",
        Kind.Girder => "Đỡ mặt cầu và truyền tải của nhịp về hệ gối và trụ.",
        _ => ""
    };

    public static string Interface(Kind type) => type switch
    {
        Kind.Formation => "Đáy kết cấu ray, lớp nền kế cận, mái dốc và thoát nước dọc.",
        Kind.TunnelPortal => "Vỏ hầm, sườn núi, rãnh thoát nước và bao hình đoàn tàu.",
        Kind.OpenMast => "Bản đế, móng cột, bu lông neo, xà và cụm treo dây.",
        Kind.BridgeMast => "Bản đế, neo vào bản mặt cầu, xà và khu vực khe co giãn.",
        Kind.Girder => "Bản mặt cầu, dầm ngang, gối và mố hoặc trụ đỡ.",
        _ => ""
    };

    public static (string Action, string Record) Stage(Kind type, string stage)
    {
        return stage switch
        {
            "Planning" => ("Đăng ký cấu kiện theo lý trình và xác định phạm vi cần quản lý.",
                "Phiếu danh mục, vị trí và công trình tiếp giáp."),
            "Design" => (type switch
            {
                Kind.Formation => "Đối chiếu mặt cắt nền với đáy kết cấu ray và tuyến thoát nước.",
                Kind.TunnelPortal => "Kiểm tra khẩu độ tại cửa hầm và vùng chuyển tiếp với sườn núi.",
                Kind.OpenMast => "Đối chiếu tim cột, xà và vị trí neo theo bản vẽ điện tiếp xúc.",
                Kind.BridgeMast => "Đối chiếu vị trí đế cột với bản mặt cầu và khe co giãn.",
                _ => "Đối chiếu chiều dài dầm, tim gối và cao độ bản mặt trong mô hình."
            }, "Bản vẽ thiết kế, mặt cắt, sai lệch cần xử lý."),
            "Construction" => (type switch
            {
                Kind.Formation => "Đo cao độ bề mặt và kiểm tra mối nối trước khi phủ lớp tiếp theo.",
                Kind.TunnelPortal => "Đo vị trí vòm và kiểm tra khe tiếp giáp với vỏ hầm.",
                Kind.Girder => "Kiểm tra vị trí kê gối và hướng dầm sau lắp đặt.",
                _ => "Kiểm tra vị trí, độ thẳng đứng của cột và liên kết neo sau lắp."
            }, "Biên bản đo hoàn công, ảnh hiện trường và kết quả nghiệm thu."),
            "Operation" => (type switch
            {
                Kind.Formation => "Theo dõi lún cục bộ, đọng nước và biến dạng ở mối chuyển tiếp.",
                Kind.TunnelPortal => "Quan sát nứt, thấm nước và vật cản trong vùng cửa hầm.",
                Kind.Girder => "Quan sát vết nứt, thoát nước trên mặt cầu và dấu hiệu dịch chuyển ở gối.",
                _ => "Quan sát độ nghiêng cột, tình trạng neo và dấu hiệu biến dạng của xà."
            }, "Vị trí phát hiện, ảnh, ngày kiểm tra và mức độ ưu tiên."),
            "Maintenance" => (type switch
            {
                Kind.Formation => "Làm sạch đường thoát nước và đánh giá bù lún khi có số đo kiểm tra.",
                Kind.TunnelPortal => "Vệ sinh rãnh, khoanh vùng thấm và đánh giá xử lý vết nứt.",
                Kind.Girder => "Kiểm tra lại gối và bề mặt dầm theo vị trí bất thường đã ghi nhận.",
                _ => "Kiểm tra lớp bảo vệ, bản đế và bu lông trong đợt bảo trì điện tiếp xúc."
            }, "Phiếu công việc, kết quả trước và sau, ảnh đối chiếu."),
            "Upgrade" => ("Đánh giá phương án sửa hoặc thay thế theo hồ sơ tình trạng và ảnh hưởng khai thác.",
                "Phương án được duyệt, hồ sơ bàn giao và mã cấu kiện thay thế nếu có."),
            _ => ("", "")
        };
    }
}
