# Kiểm tra dữ liệu cho cây BIM của QuanLyVongDoiTaiSan

**Project `F:/GITHUB/UTTVR` · Unity `6000.3.0f1` · 23/11/2026**

**Kết luận.** Có thể dựng cây từ dữ liệu sẵn có. `ParentAssetID` đã mô tả đầy đủ **Phân đoạn → Mô-đun → Cấu kiện**. `ZoneID` và `TrackID` cung cấp khóa nhóm cho cấp khu đoạn và đường ray. Hai SQLite đều có **33.434 tài sản**, trong đó **33.066 cấu kiện lá** đối chiếu được với catalog hình học. Có **12 cấu kiện của mẫu cũ** trong một nhánh tắt, dùng hệ ID riêng và chưa có ánh xạ sang danh mục hiện hành.

Phạm vi xác minh gồm hai SQLite mở chỉ đọc, ba workbook mẫu đọc trực tiếp từ XLSX, Scene và catalog đã lưu, GUID mesh và mã nguồn liên quan. Kiểm tra tĩnh xác nhận chuỗi liên kết đã lưu. Raycast, bố cục mới và hiệu năng trình duyệt chưa được chạy trong đợt này.

## 1. Nguồn dữ liệu và khóa nối

| Nguồn | Dữ liệu sử dụng |
| --- | --- |
| `Assets/StreamingAssets/RailwayLifecycle/Initial.db` | Bản seed có 33.434 tài sản, schema 1, kèm thông số và hồ sơ vòng đời cơ sở. Chưa có hai bảng sáu giai đoạn trong file seed. |
| `UserData/QuanLyVongDoiTaiSan.db` | Bản Editor đang có trong repo làm việc, schema 2. Cùng bộ ID và phân cấp với seed, có dữ liệu sáu giai đoạn và các chỉnh sửa bổ sung. Build dùng `Application.persistentDataPath`; Web khởi tạo từ seed khi chưa có bảng `Assets`. `RailwayBimStore` chạy migration hiện có lúc mở dữ liệu. Nội dung phiên Web đã lưu trên máy người dùng nằm ngoài phạm vi kiểm tra. |
| `Assets` | `AssetID` là khóa chính. `DisplayName` là tên lưu, `AssetType` gồm `Segment`, `Block`, `Component`, `ComponentType` là loại chi tiết. `ParentAssetID` tự tham chiếu. `ZoneID`, `TrackID`, `SegmentID`, `BlockID`, `ChainageStart`, `ChainageEnd` định vị tài sản. `ModelReference` là chuỗi mô tả nguồn hình học, có lặp giữa các mô-đun. |
| `AssetProperties` | Khóa `(AssetID, Name)`, các trường `Value`, `Unit`, `DataType`. Seed có `DataProvenance`, `ModelHeight`, `ModelLength`, `ModelSource`, `ModelWidth`; bản Editor có thêm `VerificationParameter`. Thông số và tên hiển thị có thể chỉnh độc lập với ID. |
| `AssetLifecycle`, `AssetInspections`, `AssetMaintenance`, `AssetLifecycleEvents` | Hồ sơ cơ sở theo `AssetID` gồm ngày lắp đặt, đưa vào sử dụng, tuổi thọ và trạng thái. Kiểm tra, bảo trì và lịch sử dùng `InspectionID`, `MaintenanceID`, `EventID` riêng, kèm `AssetID`. Bản schema 2 có thêm `StageKey` trong nhật ký. |
| `AssetLifecycleStages`, `AssetStageFields` | Khóa lần lượt là `(AssetID, StageKey)` và `(AssetID, StageKey, FieldKey)`. Lưu trạng thái, phiên bản, thời điểm cập nhật và giá trị trường, kiểu, đơn vị. Bản Editor có 200.604 hồ sơ giai đoạn và 1.504.530 giá trị trường. |
| `Generated/RailwayLifecycleBim/Catalog.asset` | `segments` ánh xạ `id → overview Mesh`; `blocks` chứa `id`, `segmentID`, `track`, pose, bounds và overview Mesh; `parts` chứa `key`, `name`, `type`, `sourcePath`, Mesh, Material và bounds. Mỗi mô-đun tái sử dụng cùng 99 định nghĩa hình học. |

`DataProvenance` mô tả dữ liệu nghiệp vụ là kịch bản giả định và kích thước lấy từ hình học. Cây phản ánh danh mục ứng dụng của `Z021`, khoảng lý trình 5.300–5.801 m. Trong liên kết Scene–catalog đã kiểm tra, model chi tiết xuất phát từ FBX `MB-EBS-DRW-003_00EBSLR49E1-MS140SklTrack-3DView_Renamed.fbx`. Ba file `RailMedium.ifc`, `RailLow.ifc`, `RailExtraLow.ifc` trong `Art/BIM` không xuất hiện dưới dạng tham chiếu trực tiếp của Scene hoặc catalog; schema hiện có không chứa IFC GUID.

**Excel.** Đã đọc `Exported_Sandbox_Asset.xlsx`, `Valid_Sandbox_Update.xlsx` và `InvalidAssetID_ImportProbe.xlsx` dưới `Documentation/PlatformCompatibility/WebTests/Evidence`. File xuất có `Assets`, `Technical`, `Inspections`, `Maintenance` và sáu sheet vòng đời. Sheet `Assets` xuất cả `ParentAssetID`, `TrackID` và các khóa phân cấp; khi nhập, nhánh `Assets` của `RailwayBimWorkbook.Apply` chỉ cập nhật `DisplayName`. `Technical` dùng header `Name|DataType|Unit`, có thể thêm thông số cho ID hiện có. ID lạ bị chặn ở Preview; `AUDIT_ONLY` trong file mẫu là ca thử lỗi. Workbook dùng để trao đổi hồ sơ theo `AssetID`. `RailwayDatabase.cs` của ChiTietSaBan dùng `RailwayModels.db` riêng; cây này dùng `RailwayBimStore`.

## 2. Phân cấp và kiểm tra liên kết

| Hạng mục | Kết quả |
| --- | --- |
| Danh mục `Z021` | `TR` (đường bên phải) và `TL` (đường bên trái), mỗi đường có 17 phân đoạn, 167 mô-đun và 16.533 cấu kiện. Mỗi mô-đun có 99 cấu kiện. |
| ID, cha và loại | Trong mỗi SQLite có 0 ID rỗng, 0 ID trùng kể cả khi gộp hoa/thường, 0 cấu kiện thiếu `ComponentType`, 0 quan hệ sai cấp, thiếu cha hoặc lệch Zone/Track. 34 `Segment` là gốc trong SQLite. `Zone` và `Track` có trường định danh riêng, chưa có bản ghi `Assets` làm node cha. |
| Liên kết hình học | 0 ID trong SQLite thiếu bộ khóa tương ứng ở catalog, 0 khóa dư và 0 bất nhất ở loại, Track, Segment, Block hoặc `ModelReference`. 467 mục mesh catalog có GUID và file đích. 34 proxy overview khớp đủ ID, MeshFilter, MeshRenderer và BoxCollider, cùng nằm dưới `BimSegmentOverview`. |
| Unity Hierarchy | `BimSegmentOverview` giữ các proxy phân đoạn. `PooledBlockLayer` cấp slot mô-đun theo phân đoạn, `PooledPhysicalLayer` tái sử dụng slot cấu kiện khi mở chi tiết. Hai root `Track_Right` và `Track_Left` cũ đang tắt. Quan hệ `Transform.parent` phục vụ trình bày và bộ pool. Cây lấy quan hệ từ các khóa SQLite. |
| Ngoài danh mục hiện hành | 12 `RailwayLifecycleElement` với 12 `ElementIdentity` riêng trong `DetailedModule_Right_Z021_M001` đang tắt, ví dụ `QLVDTS-Z021-R-M001/Mesh_52`. 0/12 ID xuất hiện trong `Assets`; chưa có khóa nối hoặc cha BIM được xác minh cho cả 12. Scene còn có 55 MeshFilter trên GameObject không gắn proxy BIM overview, thống kê này bao gồm hình học ngoài danh mục và có thể bao gồm mẫu cũ. Chưa có căn cứ quy chúng thành 55 cấu kiện BIM. |

## 3. Cây đề xuất

Dùng một gốc hiển thị **Khu đoạn 021**, dưới đó là **Đường bên phải** và **Đường bên trái**. Mỗi đường mở ra 17 **Phân đoạn**, mỗi phân đoạn mở các **Mô-đun**, mỗi mô-đun mở các **Cấu kiện** đã có cha xác nhận. Gốc, đường, phân đoạn và mô-đun là `GROUP`; `Component` là `ELEMENT`. Tổng cộng **33.437 node** nếu gộp tuyến và `Z021` thành một gốc hiển thị, gồm **371 nhóm** và **33.066 lá**.

Ví dụ một đường dẫn đầy đủ theo khóa thật. Dòng cuối tóm tắt các lá còn lại trong cùng mô-đun, không tạo nhóm phụ.

```text
Khu đoạn 021 [Z021, GROUP]
└── Đường bên phải [TR, GROUP]
    └── Phân đoạn S001 [Z021-TR-S001, GROUP]
        └── Mô-đun B001 [Z021-TR-S001-B001, GROUP]
            ├── Thanh ray phải [Z021-TR-S001-B001-C001, ELEMENT]
            ├── Thanh ray trái [Z021-TR-S001-B001-C002, ELEMENT]
            ├── Tấm nền bê tông [Z021-TR-S001-B001-C003, ELEMENT]
            ├── Tà vẹt [Z021-TR-S001-B001-C004, ELEMENT]
            ├── Bu lông [Z021-TR-S001-B001-C005, ELEMENT]
            └── 94 lá còn lại cùng cha, mã C006–C099
```

Hai nhãn ray trái/phải trong ví dụ dựa trên `DisplayName` và catalog part `Ray_Left`/`Ray_Right`. Từ C001, có thể truy `TamNenBetongRay49E1_001/connected_0` và thông số `ModelLength = 3 m`. Cùng nhánh có tổng 2 ray, 1 tấm nền, 3 tà vẹt, 12 bu lông và 81 cấu kiện thuộc 8 loại còn lại.

Không cần bảng ánh xạ mới cho cây này. Nếu sau này cần thêm cấp tuyến, số thứ tự đường theo hồ sơ chính thức, IFC GUID hoặc liên kết 12 ID mẫu cũ đã được chủ dữ liệu duyệt, lưu một bảng bên ngoài SQLite với `NodeKey`, `ParentNodeKey`, `NodeKind`, `AssetID` tùy chọn, `DisplayLabel`, `SortOrder`, `Source` và nếu cần `ExternalElementID`. Tách namespace khóa nhóm tổng hợp khỏi `AssetID`, xác thực tham chiếu và chu trình. Chỉ đưa dữ liệu có chứng cứ vào bảng.

## 4. Chọn hai chiều và phạm vi sửa UI

- **Cây → 3D.** Khi chọn lá, gọi `RailwayBimController.Locate(AssetID)`. Controller mở phân đoạn rồi mô-đun, đặt pool theo pose catalog, gán proxy bằng `BlockID + "-" + part.key`, sau đó `Select`, outline và focus theo renderer bounds. Cây giữ `AssetID` vì một slot GameObject có thể được gán lại. Mở/đóng nhánh chỉ cập nhật cây; có thể dùng thao tác riêng để định vị nhóm có bounds.
- **3D → cây.** Chuột trái raycast từ camera, lọc collider có `BimAssetProxy` đúng cấp đang hiển thị, `Alt + click` xoay qua các hit hợp lệ. VR đi qua `RailwayBimVrPresentation → UsePointer → Pick`. Cây đồng bộ khi lựa chọn ổn định qua `Changed`/`SelectedID`, truy cha bằng khóa dữ liệu, mở tổ tiên và cuộn hàng vào viewport. `Locate` phát nhiều sự kiện khi đổi cấp, nên gom thông báo UI sau lần chọn cuối và tránh gọi `Locate` lại từ callback. Ở overview, click chỉ chọn phân đoạn; để chọn chi tiết vật lý cần mở mô-đun hoặc chọn lá từ cây/tìm kiếm.
- **Tìm kiếm.** Dashboard hiện tìm theo `AssetID`, `DisplayName` và tên tiếng Việt trên toàn registry, tối đa 150 kết quả. Giữ đường tìm toàn khu đoạn, hiển thị đường dẫn cha cho kết quả và mở đúng nhánh khi chọn.
- **Script cần sửa khi triển khai.** `RailwayBimDashboard.cs` thay `RefreshList`, tổ chức ba tab và chỉ mở thông số/vòng đời với `Component`; thêm phần quản lý hàng cây. `RailwayBimController.cs` chỉ cần điều chỉnh nếu cần phát một sự kiện lựa chọn cuối hoặc tách chọn khỏi tự focus, tái dùng `Locate`, `Pick`, `Visible`, `FocusSelected`. `RailwayBimCamera.cs` cần kiểm tra mapping con trỏ, orbit, pan, zoom và focus nếu camera thật sự chỉ render 62% bên phải. Dùng vùng camera thống nhất giữa `ScreenPointToRay`, điều khiển chuột và UI hit test. `RailwayBimVrPresentation.cs` cần rà lại các tham chiếu panel nếu dùng chung layout với VR, vì hiện script bố trí các Canvas world-space riêng. `RailwayBimVietnamese.cs` cung cấp nhãn hiện tại; bổ sung nhãn cây khi cần. Nguồn mesh, prefab, lớp lưu dữ liệu và luồng Excel có thể được tái sử dụng với API hiện có.

## 5. Vòng đời và dữ liệu hồ sơ

| Nút | Tên giai đoạn | `StageKey` | Sheet Excel | Ví dụ trường |
| --- | --- | --- | --- | --- |
| 1 | Quy hoạch | `Planning` | `QuyHoach` | `PlanCode` |
| 2 | Thiết kế | `Design` | `ThietKe` | `DesignDocument` |
| 3 | Thi công | `Construction` | `ThiCong` | `Contractor` |
| 4 | Vận hành | `Operation` | `VanHanh` | `CommissioningDate`, bản ghi kiểm tra |
| 5 | Bảo trì | `Maintenance` | `BaoTri` | `MaintenancePlan`, bản ghi và tổng chi phí bảo trì |
| 6 | Nâng cấp | `Upgrade` | `NangCap` | `UpgradePlan` |

Giữ thứ tự `RailwayBimStages.All`, `RailwayBimDashboard.ShowStage(key)`, `RenderStage` và `Store.Stage/StageFields/SaveStage`. Nút chỉ cần hiện số; tiêu đề hoặc nhãn truy cập giữ tên đầy đủ. Giữ Hồ sơ, Kiểm tra/Bảo trì theo cấp giai đoạn, Nhật ký 50 bản ghi/trang và các kiểm tra hợp lệ của Store. Khi chọn `GROUP`, khóa hai tab hồ sơ và thao tác ghi trong UI. 368 nhóm `Segment`/`Block` vẫn có thông số và đủ sáu giai đoạn trong SQLite; cách trình bày mới có thể ẩn chúng và giữ tương thích với dữ liệu xuất cũ. Tên sửa được hiện nằm ở tab Tổng quan, nên cần chọn vị trí mới cho thao tác này nếu tiếp tục hỗ trợ sửa tên.

## 6. Hiệu năng Web

- Giữ uGUI `ScrollRect` và cơ chế event hiện có. Ảo hóa danh sách phẳng của các nhánh đang mở bằng chiều cao hàng cố định. Tái sử dụng số hàng vừa đủ cho viewport và một ít hàng đệm. Tham khảo cùng nguyên tắc chiều cao cố định của Unity UI Toolkit nếu quyết định dùng UI Toolkit cho Web. [Unity 6.3 · Collection virtualization](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/UIElements.BaseVerticalCollectionView-virtualizationMethod.html)
- Lập chỉ mục `ParentAssetID → children` một lần sau `Ready`, dùng `ZoneID`/`TrackID` cho hai cấp trên cùng. Tạo phần dữ liệu hàng khi nhánh mở. Chỉ bind hàng trong vùng nhìn, truy thông số/giai đoạn khi mở tab cho lá đã chọn. `Children()` hiện quét toàn registry và `RefreshList()` bind toàn danh sách của cấp hiện tại rồi ép cập nhật Canvas. Tránh lặp các thao tác đó khi cuộn hoặc đổi highlight.
- Chuẩn hóa trước khóa tìm kiếm, trì hoãn lọc khi đang gõ và giữ giới hạn kết quả. Giữ pool hình học hiện tại, tải chi tiết 3D khi đổi mô-đun, độc lập với việc mở nhánh. Khi nghiệm thu Web, đo thời gian mở nhánh/tìm kiếm, UI rebuild, GC và frame-time lúc cuộn qua nhiều nhánh; đo riêng thời gian mở DB từ seed lần đầu.

## 7. Quyết định trước khi triển khai

1. Dùng gốc **Khu đoạn 021** và nhãn **Đường bên phải/bên trái** theo dữ liệu, hay cung cấp tên tuyến và thứ tự đường chính thức để ánh xạ `TR`/`TL`?
2. Giữ cấp **Phân đoạn** hiển thị như sơ đồ, hay gộp cấp đó ở phần trình bày và giữ quan hệ thật ở dữ liệu?
3. Giữ giao diện VR hiện hành hay bố trí lại VR cùng đợt với panel 38% cho PC/Web? Chọn hành vi focus tự động khi chọn lá hoặc chỉ khi yêu cầu định vị.
4. Đặt sửa tên, nhập/xuất Excel và các lệnh hồ sơ hiện có ở đâu trong ba tab? Mặc định dữ liệu cũ của nhóm được giữ trong DB; nếu cần truy cập hồ sơ nhóm, cần chọn một luồng riêng phù hợp quy tắc chỉ lá có hai tab hồ sơ.
5. Chỉ xem xét đưa 12 cấu kiện mẫu cũ vào cây khi có bảng đối chiếu được duyệt. Phần cảnh quan, đường dây và kết cấu ngoài catalog cần danh mục/khóa riêng nếu sau này đưa vào quản lý.

**Tệp đối chiếu** · [Scene](../../Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity) · [Catalog](../../Assets/ProjectLopHoc/Generated/RailwayLifecycleBim/Catalog.asset) · [Controller](../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimController.cs) · [Dashboard](../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimDashboard.cs) · [Store](../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimStore.cs) · [Workbook](../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimWorkbook.cs) · [Sáu giai đoạn](../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimStages.cs) · [Nguồn hình học](../../Assets/Editor/RailwayBimBuild.cs).
