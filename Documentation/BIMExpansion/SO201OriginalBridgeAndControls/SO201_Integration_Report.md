# Tích hợp cầu SO 201 nguyên bản

**PASS — Đối chiếu hình học trong Unity và lắp đặt.** YenTuHaLongXanh nạp `Bridge_Visual.prefab` tại Zone_018. QuanLyVongDoiTaiSan dùng `Bridge_Detailed.prefab` tại cùng tọa độ tuyến sau khi trừ origin của trình xem. Cả hai prefab dùng cùng 91 mesh con của `Bridge_SO201.dae`, tổng 36.099 tam giác. Mỗi mesh nguồn có một lần xuất hiện. Visual có 91 renderer, không mang identity hay MeshCollider theo cấu kiện. Detailed giữ GlobalId và AssetID gốc cùng quan hệ IFC, collider chọn cấu kiện và hồ sơ.

Cấu hình `Bridge_Main` vừa với Zone_018. Các instance cầu cũ và nước cũ đã được gỡ khỏi scene và prefab trình xem Zone_018. Prefab nguồn và snapshot quay lui được giữ. Các mesh môi trường nối cầu nằm tại Zone_017–019 để đi theo streaming hiện có. Scene sandbox chỉ dùng cho kiểm tra tác giả.

- [Đo tỷ lệ và khoảng hở](SO201_Scale_And_Clearance.md)
- [Mapping 91 cấu kiện](IFC_Asset_Mapping.csv)
- [Ảnh sandbox](Screenshots/SO201_Sandbox.png) · [Đầu vào](Screenshots/SO201_Entry.png) · [Đầu ra](Screenshots/SO201_Exit.png)
- [Kết quả trình duyệt](Web_Test_Report.md)
