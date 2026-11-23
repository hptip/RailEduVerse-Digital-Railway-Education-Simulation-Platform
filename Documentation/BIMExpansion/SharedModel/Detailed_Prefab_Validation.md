# Kiểm chứng Bridge_Detailed

- Prefab variant tái sử dụng `BridgeBIM_Demo.prefab`. Giữ 91 MeshFilter, 91 MeshRenderer, 91 MeshCollider và 91 BridgeBimIdentity. Tập GlobalId khớp đầy đủ 91 phần tử có hình học trong catalog IFC.
- Mỗi mesh tham chiếu trực tiếp subasset trong Bridge_SO201.dae. Thứ tự material trên renderer và phép biến đổi ở pose gốc được đối chiếu với Visual.
- Dùng AssetID `BRIDGE-SO201-<GlobalId>` để chọn từ cây và tra hồ sơ. Node nhóm không mở hồ sơ. Ray 3D xác nhận identity thuộc đúng adapter trước khi chọn.
- IFC SO 201 hiện không có Property Sets khai báo. Dữ liệu người dùng nhập gồm ghi chú kỹ thuật, thông số tên/giá trị/đơn vị và hồ sơ từng giai đoạn. Giá trị ngày/số được kiểm tra theo kiểu trường khi người dùng điền. Cơ sở dữ liệu hạ tầng tạo rỗng.
- 15 node IFC quản lý nhóm giữ đúng quan hệ đã kiểm chứng. Nhánh dưới `Cầu → Cầu mẫu SO 201` giữ Project, Site, Bridge và các BridgePart.

[Catalog IFC và đối chiếu pilot](../BridgePilot/Import_Validation.md) · [Kết quả Web](Web_Test_Report.md)
