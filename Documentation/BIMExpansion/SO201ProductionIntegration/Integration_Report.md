# Tích hợp cầu SO 201 vào hai scene

## Tuyến Yên Tử – Hạ Long Xanh

Zone_018 dùng bản Visual gộp 129 renderer tại vị trí đã thử. Nhánh `FixedInfrastructure_Bridge` giữ OCL, nước, cảnh quan, ray đôi và ranh giới của công trình trong registry. Trên Web chỉ Visual SO 201 được bật. Nhánh Visual không có identity, UI cấu kiện hay truy cập SQLite. 

Visual mô-đun được giữ dưới nhánh `Legacy_Modular_Android_Fallback`. Tùy chọn `RailwayBridgeVisualPlatform.useLegacyOnAndroid` bật sẵn cho Android player, tắt tùy chọn để dùng Visual SO 201 trên Android khi cần thử. Cơ chế này quyết định bản render; các prefab tham chiếu vẫn có thể cùng được đóng gói. Quest 3S chưa được kiểm thử.

## Scene quản lý vòng đời

Chọn **Đoạn tuyến 09 → Cầu → Cầu chính SO 201** để mở bản Detailed ở đúng hệ tọa độ viewer. Bấm nhịp hoặc nhóm gối trụ để định vị nhóm; bấm lá để định vị đúng cấu kiện. Các node bridge thành viên được tạo khi mở nhánh, xem kết quả tìm kiếm hoặc chọn từ mô hình. Danh sách dùng pool hàng theo viewport; tìm kiếm trả tối đa 150 kết quả. Mô hình Detailed vẫn chứa toàn bộ 1.486 cấu kiện riêng.

Tab Thông số tách dữ liệu IFC, thông số mô hình và dữ liệu do người dùng nhập. Tab Vòng đời dùng sáu giai đoạn hiện có và kho SO 201 độc lập. [Bảo toàn 420 hồ sơ cũ](Legacy_Data_Preservation.md) · [Nguồn hình học](Geometry_Provenance_Report.md)

Đã xác minh các scene chính thức trên bản Web Development. [Kết quả Web](Web_Test_Report.md) · [Đối chiếu tính toàn vẹn](Model_Integrity_Report.md) · [Số đo trước và sau](Performance_Comparison.csv)
