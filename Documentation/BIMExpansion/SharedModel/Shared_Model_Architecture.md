# Cầu SO 201 dùng chung hình học

`Bridge_SO201.dae` là nguồn duy nhất của 91 mesh. Bản Detailed là prefab variant của `BridgeBIM_Demo` và kế thừa liên kết IFC, catalog, collider và adapter. Bản Visual dùng trực tiếp cùng các subasset mesh và các material trình bày đã được kiểm chứng trong pilot.

| Lớp | QuanLyVongDoiTaiSan | YenTuHaLongXanh |
| --- | --- | --- |
| Hình học | Bridge_Detailed, instance có pose riêng gần khu đoạn 021 | Bridge_Visual, instance trong scene additive thử nghiệm |
| Lựa chọn | Tree View và ray 3D gọi chung RailwayBimController. BridgeBimAdapter giữ ánh xạ, highlight và bounds. | Thành phần hiển thị thuần. |
| Sự thật IFC | Catalog bất biến, giữ GlobalId cha/con, tên, lớp, thuộc tính đã trích xuất. | Không có tham chiếu catalog hoặc đối tượng nhận diện IFC. |
| Hồ sơ người dùng | SQLite `BridgeSO201_Infrastructure.db` trong persistent data, mở khi xem hồ sơ cầu. Khóa là GlobalId và mã giai đoạn khi cần. | Không mở hồ sơ cầu. |

Trong Web, hai domain cùng dùng ba tab hiện có. Chọn ray dùng controller, outline và SQLite đường ray hiện hành. Chọn cầu đưa AssetID sang adapter cộng với catalog IFC riêng. IFC được hiển thị chỉ đọc, phần nhập lưu vào các bảng thông số và sáu giai đoạn riêng. Excel hiện hành vẫn dành cho ray.

Có thể áp dụng tổ chức này cho hầm, OCL và subgrade khi có nguồn chi tiết cùng mapping IFC đáng tin cậy. Gán namespace AssetID và catalog riêng cho tài sản trước, sau đó tạo bản visualization chỉ tham chiếu mesh và material. Phạm vi sở hữu streaming do tuyến hoặc Zone quyết định.

[Kiểm chứng prefab chi tiết](Detailed_Prefab_Validation.md) · [Kiểm chứng prefab hiển thị](Visual_Prefab_Validation.md) · [Tích hợp scene](Scene_Integration_Report.md)
