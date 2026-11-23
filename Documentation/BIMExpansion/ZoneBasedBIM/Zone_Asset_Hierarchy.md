# Cây BIM theo đoạn tuyến

`Tuyến Yên Tử – Hạ Long Xanh → Đoạn tuyến → Hạng mục → Công trình hoặc nhóm → Cấu kiện`.

- Đoạn tuyến 01–17 khớp lần lượt với Zone_010–026, sắp theo chainage registry. Nhãn có thể đổi, khóa `zone:Zone_XXX` giữ ổn định.
- Đoạn tuyến 12 là Zone_021. `Đường ray → Đường bên phải (TR) / Đường bên trái (TL) → Phân đoạn → Mô-đun → Component` giữ nguyên AssetID và ParentAssetID đã có. Bộ cây vẫn dùng hàng UI tái sử dụng.
- Chọn dòng đoạn tuyến nạp hoặc định vị hình học ngay. Cầu, hầm, cổng OCL và nền gia cố hiện có khóa điều hướng, để trống AssetID. Khi chưa có hồ sơ, thông số và vòng đời không mở. Tìm kiếm tên công trình hoặc Zone vẫn dùng được.
- `Bridge_Main` có một bản ghi tại Zone_018. Zone_017 và Zone_019 hiển thị đường dẫn xem tại chủ sở hữu. Các hầm và phần công trình đi qua nhiều đoạn dùng cùng quy tắc.
- 41 Zone ngoài registry không được đưa vào cây hoặc bộ prefab xem. Bảng mapping chỉ ghi bản ghi thực tế và mã vùng đã xác minh.

[Zone_Asset_Mapping.csv](Zone_Asset_Mapping.csv) · [Zone Viewer](Zone_Viewer_Implementation.md)
