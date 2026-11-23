# Thiết kế mở rộng BIM theo chuyên ngành

## 1. Một cây và ba tab

Panel trái tiếp tục có **Danh sách | Thông số | Vòng đời**. `GROUP` chỉ mở/thu, lọc và có thể định vị phạm vi nếu có bounds. `ELEMENT` có hồ sơ kỹ thuật và vòng đời. Một công trình gộp mesh có thể là một `ELEMENT` dùng chung nhiều renderer. Các ID dưới đây là đề xuất đăng ký, chưa phải AssetID đã có.

```text
Tuyến Yên Tử – Hạ Long [GROUP]
├── Đường ray [GROUP]
│   └── Khu đoạn 021 [GROUP]
│       ├── Đường bên phải TR [GROUP]
│       │   └── Phân đoạn S001 [GROUP] → Mô-đun B001 [GROUP] → C005 [ELEMENT]
│       └── Đường bên trái TL [GROUP] → Phân đoạn → Mô-đun → Cấu kiện [ELEMENT]
├── Cầu [GROUP]
│   └── Cầu chính [ELEMENT, BRIDGE-YTH-0001]
├── Hầm [GROUP]
│   ├── Hầm Forest 01 [ELEMENT, TUNNEL-YTH-0001]
│   └── Hầm Forest 02 [ELEMENT, TUNNEL-YTH-0002]
├── Hệ thống điện trên cao [GROUP]
│   └── Đoạn cổng 3.000–3.500 m [ELEMENT, OCL-YTH-0001]
└── Nền đường [GROUP]
    └── Đoạn gia cố hai bên 300–800 m [ELEMENT, SUBGRADE-YTH-0001]
```

Khóa UI cho nhóm dùng namespace riêng như `group:route:YTH:OCL`. Duy trì mọi `Z021-…` hiện có. ID mới cấp một lần theo `DOMAIN-ROUTE-SEQUENCE`, giữ nguyên khi đổi tên, thay mesh hoặc chỉnh phạm vi. Lưu lý trình, `ZoneID` và ID registry kỹ thuật ở trường riêng. Cân nhắc thêm một `ELEMENT` OCL tại Z021 nếu ưu tiên thao tác ngay trên hình học BIM đang có, sau khi chốt phạm vi và quy tắc tránh ghi trùng tài sản.

## 2. Mô hình dữ liệu đề xuất

Giữ SQLite đường ray làm nguồn hiện hành. Thêm catalog hạ tầng **riêng** ở bản thiết kế đầu tiên vì `Assets.AssetType` hiện bị giới hạn ba loại đường ray, trường `TrackID` bắt buộc và migration stage mang dữ liệu giả lập riêng ngành ray. Tái sử dụng API đọc và render hồ sơ qua adapter, cho hai nguồn cùng một UI. Hoãn hợp nhất DB đến khi có migration đa chuyên ngành được duyệt và kiểm thử đầy đủ.

| Bảng hoặc dữ liệu phụ | Trường cốt lõi |
| --- | --- |
| `InfrastructureAssets` | `AssetID` PK, `Domain`, `DisplayName`, `RouteID`, `ChainageStart/End`, `ChainageReference`, `SourceRecordID`, `Provenance`, `VerificationStatus`. Phân biệt số đo model, tham số generator và dữ liệu được phê duyệt. |
| `AssetZoneMembership` | `(AssetID, ZoneID, Role)` với vai trò `Owner`, `Coverage`, `ActivationDependency`. Ví dụ Bridge_Main có owner 018, vùng giữ 017–019. |
| `AssetGeometryBinding` | `BindingID` PK, `AssetID` FK trong catalog hạ tầng, `SceneGUID`, `ZoneID`, `SourceAssetGUID + localFileID`, `OccurrenceKey`, `TargetRole`, `PickMode`, `ReferenceFrame`, `LocalBounds`, `Revision`. Một AssetID có nhiều binding. Mesh shared chỉ là định danh hình học; `OccurrenceKey` ánh xạ instance hợp lệ trong từng scene. |
| Hồ sơ và vòng đời | Bảng thuộc tính theo `(AssetID, FieldKey)` với `TypedValue`, `Unit`, `Source`, `VerifiedAt`, `ValueStatus`. Hồ sơ giai đoạn theo `(AssetID, StageKey)` và bảng trường phát sinh, kiểm tra, bảo trì, sự kiện. Tái dùng sáu StageKey và nút 1–6. Thêm định nghĩa trường theo chuyên ngành; hồ sơ chưa có hiện “Chưa có dữ liệu”. Không điền giả ngày, vật liệu, tình trạng hoặc chi phí. |

Không tạo FK SQLite xuyên hai file. Nếu adapter ghép nhiều catalog, dùng validation đối chiếu ID namespace, quyền ghi, khóa ghép và nguồn Excel trước transaction. Excel đường ray giữ schema hiện tại. Chỉ mở rộng Excel chuyên ngành bằng bộ sheet được version hóa và tài liệu xác thực field/domain, sau khi duyệt. Header và preview tiếp tục ở panel hiện tại.

## 3. Liên kết geometry và điều khiển hai chiều

| Trạng thái | Mở model, chọn và highlight |
| --- | --- |
| Mesh riêng cho cấu kiện hoặc công trình | Resolver trả renderer tương ứng. Collider có thể map bằng `(scene, instance, collider)` với validation layer. Nếu chưa có collider, cấu hình proxy pick độc lập, chỉ trong scene/preview BIM. Bounds proxy xấp xỉ dùng để chọn cả tài sản và không dùng để kết luận vị trí hit chính xác trên mesh. |
| Nhiều renderer cùng AssetID | Gộp bounds theo tọa độ BIM, highlight mảng renderer. Gắn role `Primary`, `Context` hoặc `Marker`. Ví dụ cầu highlight `ExtendedBridgeQuest` và hiển thị nước làm ngữ cảnh. Hầm highlight thân hầm và lấy cửa làm điểm focus. |
| Mesh gộp nhiều công trình con | Highlight theo toàn mesh/Zone. Nếu chỉ có thông tin vị trí một phần, cung cấp **định vị gần đúng** bằng bounds đã được xác nhận. Không tạo hit giả theo submesh, material slot hoặc tên node. |
| Zone đang nạp/chưa nạp | Ghi binding theo SceneGUID/owner Zone và occurrence đã author. Resolver đăng ký khi scene đã nạp và chunk đã active, hủy khi unload. Chọn cây giữ `SelectedAssetID` trong lúc chờ; chỉ highlight khi binding sẵn sàng. Nạp nhiều Zone của cùng một công trình nếu dùng chế độ mở tuyến, giữ session chọn tới khi hoàn tất. Không dùng InstanceID làm khóa bền vững. |
| Chưa có hình học | Chỉ xem hồ sơ. Nếu có bounds đại diện đã xác nhận, chỉ cho phép focus gần đúng. Khi thiếu bounds cũng hiện trạng thái chưa liên kết, giữ hồ sơ và không nhận pick từ 3D. |

Tree → chọn `AssetID` → gọi adapter của từng domain → renderer/pick/focus phù hợp. 3D → resolver đổi hit thành `AssetID` → cập nhật trạng thái chọn chung → mở tổ tiên và cuộn dòng vào viewport. Dùng một event lựa chọn cuối, chống callback lặp. Duy trì `RailwayBimController.Locate` và pool hiện tại cho ray. Adapter mới cung cấp selection cho hạ tầng, vì `Pick()` hiện chỉ nhận `BimAssetProxy` và đúng cấp ray. Kiểm tra outline khi một tài sản có nhiều MeshFilter/Renderer; outline hiện tại dựa vào `MeshFilter.sharedMesh`, chưa hỗ trợ renderer skinned trực tiếp. [Controller ray](../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimController.cs) · [Outline hiện tại](../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimOutline.cs)

## 4. Bố trí model và chi phí Web

Không cần chuyển object khỏi Yên Tử hoặc Showroom sang scene BIM. Với OCL Z021 có thể dùng visual đang có. Với cầu, hầm và nền đường, khi cần trình diễn 3D trong BIM, ưu tiên nạp preview nhỏ tham chiếu mesh/material có sẵn với pose đã đối chiếu. Mỗi preview có hệ tọa độ nguồn và phép đổi sang BIM riêng, giữ nguyên file mẫu dùng chung. Chỉ tải package asset/scene tương ứng khi người dùng chọn hạng mục; giữ một preview hoạt động, giải phóng phần tải khi rời sau khoảng chờ hợp lý. Mở cả tuyến Yên Tử có thể là chế độ riêng nếu cần trải nghiệm streaming thật. Tránh kéo toàn bộ 17 Zone, đoàn tàu, XR và scenery vào scene BIM chỉ để xem một hồ sơ.

| Chi phí | Cách xử lý và đo |
| --- | --- |
| DB và seed | Danh sách theo domain/cha, chỉ lấy trường menu, tìm kiếm có index và phân trang. Nạp thuộc tính, stage đã mở, kiểm tra/bảo trì và nhật ký theo trang. Dùng trạng thái chưa có dữ liệu và tạo hồ sơ khi cần. Đo byte seed HTTP, thời gian mở/khởi tạo, dung lượng lưu và heap registry. |
| UI Tree View | Giữ virtualization hàng đã có, lazy load con theo mở nhánh, debounce tìm và giữ lựa chọn khi đổi domain. Đo thời gian bind hàng, số UI row, frame-time p95/p99 và GC. |
| Renderer/mesh/texture | Đo số renderer hoạt động, draw calls, triangles, GPU time và bộ nhớ asset riêng trên Web/Quest. Một hồ sơ ít hơn trong SQLite không thay đổi mesh/texture hay lượt vẽ trong lúc camera render cùng hình học. |
| Pool và streaming | Giữ pool ray. Adapter chỉ nắm binding đang load. Không tạo GameObject cho mọi AssetID. Đo số scene/chunk hoạt động, độ trễ tích hợp, thời gian unload, đỉnh RAM và chi phí giữ đồng thời nhiều Zone. |
| Tổng Web startup | Tách download player, data/wasm, seed tải khi vào BIM và asset preview theo domain. DB nhỏ chủ yếu giảm seed, khởi tạo và tài nguyên SQLite. Nếu mesh được đưa thêm vào gói chính, dung lượng build có thể tăng dù DB nhỏ. Benchmark bản cache lạnh và cache nóng theo từng giai đoạn. |

## 5. Quyết định trước khi sửa project

1. **Duyệt tám mô-đun** TR/TL ở S001, B001–B004 mỗi đường để lọc giao diện. Chọn có cần khóa chế độ toàn bộ hay vẫn cung cấp tìm/hiện tất cả. Chỉ tạo DB demo riêng nếu tốc độ vào scene là mục tiêu đo được.
2. **Duyệt mức hồ sơ tổng thể.** Một cầu, hai hầm, một đoạn OCL cổng, một đoạn nền gia cố hai bên. Chốt có thêm OCL Z021 làm mẫu thao tác tại chỗ. Giữ nước, núi, terrain và dây vượt phạm vi là ngữ cảnh khi thích hợp.
3. Chốt danh mục trường có căn cứ và đầu mối cung cấp bản vẽ/mã IFC, thông số vận hành hoặc biên bản. Xác nhận lý trình dùng hệ 8 km hiện tại hay cần lưu song song lý trình tuyến gốc.
4. Chọn rollout **hồ sơ trước** hay **hồ sơ + preview chọn được**. Nếu chọn preview, duyệt quyền thêm lớp binding/proxy trong scene BIM, giữ nguyên mesh, prefab, transform nguồn và hệ thống streaming.
5. Xác nhận cách trao đổi Excel đa chuyên ngành và chính sách DB browser đã có dữ liệu. Tách catalog hạ tầng và dữ liệu mới khỏi DB ray cho tới khi phạm vi và rollback được duyệt.

[Kiểm kê model](Infrastructure_Model_Audit.md) · [Phân tích giảm dữ liệu](Railway_Data_Reduction_Analysis.md) · [Danh mục CSV](Infrastructure_Asset_Inventory.csv)
