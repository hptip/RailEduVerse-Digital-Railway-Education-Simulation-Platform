# Kiểm chứng Bridge_Visual

Unity đã đối chiếu cả 91 MeshFilter ở hai prefab theo tham chiếu subasset DAE, thứ tự shared materials và cả 16 phần tử của ma trận tại pose prefab. Hai bên có cùng bounds hợp từ MeshRenderer là **89,89264 × 27,76169 × 19,99279 m**. Đây là bounds bảo thủ của renderer Unity. Báo cáo pilot đo cực trị trực tiếp từ hình học và ghi khoảng 89,321 × 27,762 × 19,836 m.

Visual chỉ có Transform, MeshFilter và MeshRenderer. Số BridgeBimIdentity, BridgeBimAdapter, collider và component hồ sơ đều bằng **0**. Các node dùng tên trung lập thay vì để tên IFC GUID do DAE tạo. Không có thêm mesh dẫn xuất.

Material là cùng các tham chiếu presentation với bản Detailed ở trạng thái chưa highlight. Màu ảnh ở hai scene có thể khác theo camera, môi trường và ánh sáng.

[Kiểm chứng nguồn IFC](../BridgePilot/Import_Validation.md) · [Kết quả Web](Web_Test_Report.md)
