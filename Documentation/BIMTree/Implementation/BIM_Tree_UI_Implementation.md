# BIM Tree Explorer cho QuanLyVongDoiTaiSan

Phiên bản Web dùng Canvas và EventSystem hiện có. Một panel trái rộng 38% chứa Danh sách, Thông số và Vòng đời. Viewport camera chiếm 62% bên phải. Bố cục Web được áp dụng khi mở scene; khi rời scene trình duyệt khôi phục kích thước Canvas trước đó. Nhánh Android tiếp tục dùng giao diện world-space hiện có và cùng controller nghiệp vụ.

## Dữ liệu, thao tác và mã nguồn

| Hạng mục | Cách triển khai |
| --- | --- |
| Cây và tìm kiếm | `RailwayBimTree` lập chỉ mục AssetID và nối `ParentAssetID`. Khu đoạn và đường được nhóm bằng `ZoneID`/`TrackID` với node tổng hợp riêng. Giữ cấp Phân đoạn. Các nút nhóm mở/thu cây. Tìm theo tên, mã hoặc loại, kèm đường dẫn cha và giới hạn 150 kết quả. |
| Hiệu năng | uGUI ScrollRect, hàng cao cố định, tái sử dụng hàng trong viewport, tìm kiếm đợi 160 ms sau lần nhập cuối. Đọc hồ sơ chỉ khi mở tab đang cần và có cấu kiện lá được chọn. |
| Đồng bộ lựa chọn | Hàng lá chuyển `AssetID` đến `Locate`. Controller giữ lookup và pool của catalog. Chọn từ raycast dùng `BimAssetProxy` theo cấp hình học đang có; cây mở tổ tiên để hiện đúng hàng. `F`, nút Định vị và nhấp đúp dùng cơ chế focus có sẵn. |
| Camera | Click trái chọn, kéo trái xoay, kéo phải dịch, bánh xe zoom. Nhấn Esc bỏ chọn khi menu và trường nhập không giữ phím. Kéo vượt ngưỡng 6 pixel được nhận là thao tác camera. Menu và panel giữ chuột khỏi vùng raycast. |
| Thông số | Nhóm định danh, vị trí, thuộc tính và nguồn lấy từ Assets/AssetProperties. AssetID và khóa quan hệ chỉ đọc. Sửa tên và thông số trong cùng transaction với validation và nhật ký hiện có. |
| Vòng đời | Các nút 1–6 lấy thứ tự và tên từ `RailwayBimStages.All`. Tái sử dụng hồ sơ, trường trạng thái, hồ sơ kiểm tra/bảo trì và nhật ký theo trang. Hồ sơ nhóm vẫn nằm trong SQLite. |
| Excel và tác vụ | Header giữ Nhập/Xuất Excel. Footer hiển thị kết quả preview và xác nhận nhập. Web tiếp tục xuất tài sản được chọn, dùng file picker/download trình duyệt. Thanh lệnh giữ Chi tiết, Quay lại, Định vị, Cô lập và Toàn cảnh. Nội dung lỗi và trạng thái vẫn đi qua dashboard. |

## Cách chạy

Build bằng profile **UTTVR Web** trong Unity 6000.3.0f1. Đăng nhập bằng tài khoản demo, vào Campus, mở menu bằng `M` hoặc `F1` và chọn Quản lý vòng đời tài sản. Để chạy bản Development đã xuất cho kiểm thử, dùng `node Documentation/PlatformCompatibility/WebTests/serve.mjs Builds/UTTVR_BIMTreeWeb_Measured 8785` rồi mở `http://127.0.0.1:8785/`. Thêm `?bimAudit=1` để hiện số liệu kiểm tra cây trong Console trình duyệt.

## Tệp triển khai

- `Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimExplorer.cs` và `RailwayBimTree.cs`, cùng `.meta`.
- `Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimDashboard.cs`, `RailwayBimController.cs`, `RailwayBimCamera.cs`, `RailwayBimStore.cs`.
- `Assets/Plugins/WebGL/UttBimExplorer.jslib` cùng `.meta`.

Kết quả kiểm thử và số liệu nằm trong báo cáo Web và CSV cùng thư mục. Quest 3S cần được nghiệm thu bằng thiết bị.
