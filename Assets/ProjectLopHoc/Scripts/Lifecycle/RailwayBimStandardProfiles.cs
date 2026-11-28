using System;

internal static class RailwayBimStandardProfiles
{
    internal readonly struct Template
    {
        public readonly string Material, Technical, Assembly, Inspection, Maintenance;
        public Template(string material, string technical, string assembly, string inspection, string maintenance)
        {
            Material = material;
            Technical = technical;
            Assembly = assembly;
            Inspection = inspection;
            Maintenance = maintenance;
        }
    }

    public static Template For(string type)
    {
        bool Has(string part) => type.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        if (Has("ray"))
            return new("Thép đường sắt", "Mặt lăn, tim ray và vị trí mối nối.", "Đo vị trí lắp và đối chiếu tim ray.",
                "Ghi mòn bề mặt ray và các dấu hiệu bất thường tại mối nối.", "Đánh giá xử lý bề mặt và kiểm tra lại biên dạng.");
        if (Has("liên kết") || Has("bu lông") || Has("neo") || Has("Fastener") || Has("Anchor"))
            return new("Thép liên kết hoặc vật liệu đàn hồi theo loại", "Kiểu liên kết, vị trí và khả năng thay thế.",
                "Đối chiếu vị trí, hướng và phương pháp siết theo thiết kế.", "Kiểm tra thiếu chi tiết, lỏng và dấu hiệu ăn mòn.",
                "Đánh giá thay cụm liên kết và ghi mã lô vật tư.");
        if (Has("tà vẹt") || Has("Sleeper") || Has("bản bê tông") || Has("Slab"))
            return new("Bê tông kết cấu", "Mặt đỡ ray, bản đế và tình trạng bề mặt chịu lực.",
                "Kiểm tra cao độ kê ray, vị trí gắn liên kết và mối nối bản.",
                "Ghi nứt trên bề mặt, hư hỏng tại liên kết và dấu hiệu lún lệch.",
                "Đối chiếu bề mặt và cao độ sau sửa hoặc thay tấm.");
        if (Has("hầm"))
            return new("Bê tông kết cấu hoặc thiết bị theo nhóm", "Khẩu độ, liên kết liên tục và thoát nước.",
                "Đo chuyển tiếp và kiểm tra mối nối theo lý trình.", "Ghi nứt, thấm nước và tình trạng các mối nối.",
                "Khoanh vùng thấm và đối chiếu tình trạng sau xử lý.");
        if (Has("dây") || Has("cáp"))
            return new("Kim loại dẫn và lớp bảo vệ tùy loại", "Đường đi dây, điểm đỡ và khoảng hở.",
                "Đối chiếu điểm treo và khoảng cách với bộ phận lân cận.", "Kiểm tra mối nối, độ võng và tình trạng kẹp.",
                "Ghi cấu kiện thay thế, đo lại vị trí tại điểm đỡ.");
        if (Has("cách điện"))
            return new("Gốm hoặc composite cách điện", "Bề mặt cách điện, đầu liên kết và hướng lắp.",
                "Đối chiếu kết nối và hướng bố trí.", "Quan sát nứt, nhiễm bẩn và bề mặt cách điện.",
                "Ghi vệ sinh hoặc thay thế và kiểm tra lại các đầu nối.");
        if (Has("cột") || Has("xà"))
            return new("Thép kết cấu", "Độ thẳng đứng, neo, bản đế và vị trí điểm treo.",
                "Đo tọa độ bản đế và kiểm liên kết neo.", "Kiểm tra nghiêng, ăn mòn ở chân cột và hư hỏng xà.",
                "Kiểm tra lớp bảo vệ và biên bản sửa liên kết.");
        if (Has("nhịp") || Has("gối") || Has("móng") || Has("trụ") || Has("cọc"))
            return new("Bê tông, thép hoặc vật liệu gối theo hồ sơ thiết kế", "Mặt đỡ, vị trí truyền tải và tiếp giáp kết cấu.",
                "Đối chiếu các vị trí lắp, cao độ và liên kết lân cận.", "Ghi dấu hiệu chuyển vị, nứt hoặc hỏng bề mặt.",
                "Đối chiếu biến đổi trước/sau xử lý tại vị trí đã ghi.");
        return new("Bê tông, cấp phối hoặc đất đắp theo lớp", "Cao độ, mặt cắt, liên tục và hướng thoát nước.",
            "Đo trước khi bị lớp trên che khuất.", "Kiểm tra đọng nước, lún hoặc xói bề mặt.", "Làm sạch thoát nước và theo dõi sau sửa.");
    }

    public static (string Work, string Document) Stage(Template profile, string key) => key switch
    {
        "Planning" => ("Gắn phạm vi và mã cấu kiện với kế hoạch quản lý tuyến.", "Danh mục, lý trình và công trình quản lý."),
        "Design" => (profile.Technical, "Mặt cắt hoặc bản vẽ chi tiết, nguồn thông số và phiên bản."),
        "Construction" => (profile.Assembly, "Nhật ký lắp đặt, toạ độ đo và ảnh trước khi che khuất."),
        "Operation" => (profile.Inspection, "Ngày kiểm tra, vị trí, ảnh và chỉ số theo dõi."),
        "Maintenance" => (profile.Maintenance, "Lệnh công việc, ảnh hiện trạng và kết quả đối chiếu."),
        "Upgrade" => ("Đánh giá phương án sửa hoặc thay, lưu liên kết sang mã tài sản kế nhiệm.", "Phương án và biên bản bàn giao khi có."),
        _ => ("", "")
    };
}
