# Kiểm thử lựa chọn cấu kiện IFC

## Adapter và cơ chế

`BridgeBimCatalog` giữ catalog thử dạng ScriptableObject. `BridgeBimIdentity` giữ GlobalId và danh sách renderer serialized. `BridgeBimAdapter` nối hai phía bằng `GlobalId` và `AssetID = BRIDGE-SO201-<GlobalId>`. `SelectAsset` hoạt động từ cây, `Pick` dùng raycast tới MeshCollider thật và truy ngược binding, `TryGetSelectedBounds` hợp bounds các renderer để định vị. Khi đổi hoặc bỏ chọn, adapter khôi phục đúng mảng material cũ của renderer. Node không có geometry chỉ mở/thu nhánh IFC. Bản chọn thử giữ tên hiển thị riêng khỏi định danh.

**Kiểm tra tĩnh Unity** có 91 identity hợp lệ và 91 collider ở trên 10 lớp có hình học. 15 node IFC còn lại là nhóm điều hướng. Từng phần tử trong bảng mapping có renderer và collider riêng. Một phần tử bị che vẫn có thể chọn bằng danh sách hoặc đổi góc nhìn. [Mapping Unity](IFC_Unity_Mapping.csv)

## Dùng chung với Quản lý vòng đời tài sản

| Tab | Adapter cầu trong sandbox | Tích hợp sau khi duyệt |
| --- | --- | --- |
| Danh sách | Cây khởi tạo từ GlobalId cha theo Aggregate/Nest ưu tiên, sau đó Containment. Tìm theo tên, IFC class hoặc AssetID. Cây và raycast dùng chung `Selected`. | Ghép node domain cầu vào cây, chuẩn hóa `NodeKey` khác AssetID. Chuyển event chọn tới adapter đúng domain thay vì gọi `RailwayBimController.Locate` cho mọi ID. Dùng state lựa chọn chung và mở tổ tiên. |
| Thông số | Hiển thị dữ liệu IFC đã trích xuất. Với file SO 201, Property Sets báo “Chưa có dữ liệu”. | Adapter read-only trả hồ sơ cấu kiện hạ tầng theo namespace. `RailwayBimDashboard` hiện gọi `RailwayBimStore` trực tiếp, nên cần interface đọc chuyển theo domain và quy tắc quyền ghi trước khi dùng chung UI. |
| Vòng đời | Hiển thị “Chưa có dữ liệu vòng đời”. | Tái sử dụng sáu nhãn/nút đã có ở `RailwayBimStages` sau khi có hồ sơ hạ tầng được duyệt. Không chạy migration ray trên ID cầu và không tạo dữ liệu giả. |

Prefab chứa adapter và geometry. Camera/UI thử đặt trong `BridgeBIM_Test`. API `SelectionChanged` và `TryGetSelectedBounds` là điểm nối dự kiến với UI và camera scene chính. Phiên thử không dùng SQLite gốc và không ghi hồ sơ. [Adapter](../../../Assets/ExternalBIM/BridgeDemo/BridgeBimAdapter.cs) · [View thử](../../../Assets/ExternalBIM/BridgeDemo/BridgePilotView.cs) · [Dashboard hiện hành](../../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimDashboard.cs)

## Kiểm thử tương tác trên WebGL

Bản development được chạy qua HTTP localhost trong Chrome, viewport 1.280 × 720.

| Thao tác | Kết quả quan sát |
| --- | --- |
| Mở scene thử | 106 node IFC. 91 cấu kiện có thể chọn. Cầu hiển thị với vật liệu đã import. [Ảnh toàn cảnh](Screenshots/Web_01_Overview.png) |
| Chọn từ cây | Chọn **Pile cap O1** (`IfcFooting`, `2gImNpnGyqpWl$gbNtpzMj`). Camera focus tại (35,158 / 5,262 / -4,037) m và adapter ghi nhận đúng một cấu kiện được highlight. [Ảnh chọn từ cây](Screenshots/Web_02_Tree_Selection.png) |
| Đọc thuộc tính | Tab **Thông số** hiển thị GlobalId, AssetID, lớp, tên và liên kết cha của phần tử đã chọn. Không có Pset trong IFC này. [Ảnh tab Thông số](Screenshots/Web_03_Properties.png) |
| Xem vòng đời | Tab **Vòng đời** báo chưa có hồ sơ cho cấu kiện. [Ảnh tab Vòng đời](Screenshots/Web_04_Lifecycle.png) |
| Chọn từ mô hình | Click **Pavement** (`IfcPavement`, `0r$xTC36ULMGQJvAkStNpQ`). Raycast trả đúng AssetID, làm nổi một renderer và cuộn cây tới node Pavement. [Ảnh chọn từ mô hình](Screenshots/Web_05_Model_Selection.png) |

Bản cuối xác minh hai GlobalId bằng click. Bản đầu cũng chọn được **Cornices 1** (`IfcBeam`). 91 collider đã được đối chiếu theo ID, còn thao tác click trực tiếp được kiểm tra theo mẫu. Vùng tiếp giáp bệ cọc với kết cấu liền kề có một số vệt răng cưa khi zoom sát và highlight. Hình học IFC/DAE được giữ nguyên. [Đo hiệu năng Web](Web_Performance_Test.md)
