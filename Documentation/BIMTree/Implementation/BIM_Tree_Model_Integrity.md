# Kiểm tra tính toàn vẹn model BIM

Đối chiếu SHA-256 trước và sau triển khai cho 960 file. 959 file trùng khớp byte-for-byte, bao gồm Scene `QuanLyVongDoiTaiSan.unity`, toàn bộ asset và `.meta` trong `Generated/RailwayLifecycleBim`, catalog, prefab XR chứa trong thư mục đó, FBX tham chiếu, prefab `EBS_Rail_2000_Prefab`, SQLite seed và SQLite Editor. File khác hash là `RailwayBimStore.cs` do bổ sung lưu tên và thuộc tính trong cùng transaction.

| Kiểm tra | Kết quả |
| --- | --- |
| Scene, mesh, pose, renderer và collider đã lưu | PASS theo SHA-256 của Scene và asset đã kiểm tra. |
| Catalog và liên kết dữ liệu | PASS ở cấp file. Catalog và hai SQLite trùng baseline. Audit trước triển khai đã đối chiếu 334 mô-đun, 34 phân đoạn, 99 định nghĩa chi tiết và 33.066 khóa cấu kiện. |
| Schema, ID và khóa quan hệ | Không có thay đổi. Lệnh sửa hồ sơ kỹ thuật chỉ cập nhật `DisplayName` và `AssetProperties` cho AssetID hiện có. |
| Prefab nguồn và lớp trình bày VR | PASS ở cấp file. Không sửa `RailwayBimVrPresentation.cs`. Android tiếp tục dùng nhánh bố cục cũ. |

Kết quả chọn, highlight và trình bày trên bản Web được theo dõi trong `BIM_Tree_Web_Test.md`. Quest 3S cần kiểm tra riêng trên thiết bị.
