# Phân tích tinh gọn dữ liệu đường ray

## 1. Dung lượng và quan hệ SQLite

Đọc bằng URI SQLite `mode=ro&immutable=1`, không dùng constructor Unity. Cả hai file không có sidecar WAL/SHM/journal lúc kiểm tra. `quick_check` trả `ok` và `foreign_key_check` trả 0 lỗi ở cả hai. `dbstat` đo page SQLite của bảng **và các index của bảng**. Cỡ page là 4.096 byte, freelist bằng 0. Còn 4.096 byte ngoài tổng các đối tượng được liệt kê trong mỗi file. Số trong hai cột là **bản ghi / byte**.

| Bảng | Seed `Initial.db` | Editor `QuanLyVongDoiTaiSan.db` | PK và FK | Mã đọc/ghi và giả định |
| --- | ---: | ---: | --- | --- |
| `Assets` | 33.434 / 9.052.160 | 33.434 / 9.052.160 | PK `AssetID`, self-FK `ParentAssetID → Assets.AssetID` | `RailwayBimSceneSetup.Seed`, `RailwayBimStore.ReloadRegistry/Children/Rename`, `RailwayBimTree`, `RailwayBimController`, `RailwayBimWorkbook`. CHECK chỉ nhận `Segment/Block/Component`. Bắt buộc `ZoneID`, `TrackID`. `SegmentID`, `BlockID` là khóa ngữ nghĩa và chưa được SQLite cưỡng chế bằng FK riêng. |
| `AssetProperties` | 167.170 / 22.704.128 | 167.171 / 25.698.304 | PK `(AssetID,Name)`, FK `AssetID → Assets` | `Store.Properties/Property/SaveProperties/SaveTechnicalProfile`, Dashboard, Workbook, seed/scenario. Tên trường động. Seed hiện dùng nhóm kích thước/nguồn của đường ray. |
| `AssetLifecycle` | 33.434 / 10.313.728 | 33.434 / 12.009.472 | PK/FK `AssetID → Assets` | Store, `RailwayBimStages` đồng bộ một số trường, Workbook, seed/scenario. `Lifecycle(id).Single()` giả định đúng một hồ sơ cơ sở cho mỗi ID. |
| `AssetLifecycleStages` | Chưa có | 200.604 / 29.716.480 | PK `(AssetID,StageKey)`, FK `AssetID → Assets` | `RailwayBimStages.MigrateStages/Stage/SaveStage`, Dashboard và StageWorkbook. Mặc định sinh đủ 6 hàng cho **mọi** AssetID, kể cả Segment/Block. |
| `AssetStageFields` | Chưa có | 1.504.530 / 259.059.712 | PK `(AssetID,StageKey,FieldKey)`, FK ghép `(AssetID,StageKey) → AssetLifecycleStages` | `RailwayBimStages.StageFields/SaveStage`, StageWorkbook, scenario. Một số SQL seed gắn với `ComponentType` ray, chi phí minh họa và tổng hợp theo `ParentAssetID`. |
| `AssetInspections` | 33.434 / 11.169.792 | 33.437 / 14.606.336 | PK `InspectionID`, FK `AssetID → Assets` | Store, Dashboard giai đoạn Vận hành, Workbook, seed/scenario. Đọc/ghi theo ID, nội dung seed mang ngữ cảnh ray. |
| `AssetMaintenance` | 11.144 / 3.235.840 | 11.147 / 4.870.144 | PK `MaintenanceID`, FK `AssetID → Assets` | Store, Dashboard giai đoạn Bảo trì, Workbook, seed/scenario. Tổng chi phí và ngày gần nhất được tính từ bản ghi. |
| `AssetLifecycleEvents` | 33.434 / 7.606.272 | 78.030 / 21.954.560 | PK `EventID`, FK `AssetID → Assets`; schema 2 thêm `StageKey` không có FK stage | Store `Event/History`, `RailwayBimStages.StageHistory`, Dashboard; các thao tác Excel tạo sự kiện. Giữ được cả sự kiện theo giai đoạn và sự kiện chung có khóa giai đoạn rỗng/NULL theo dữ liệu nguồn. |
| `SchemaMigrations` | 1 / 4.096 | 2 / 4.096 | PK `Version` | `Store.Migrate`, `MigrateStages`; lưu phiên bản ứng dụng 1/2. `PRAGMA user_version` bằng 0 ở cả hai. |
| `SeedVersions` | 1 / 8.192 | 6 / 8.192 | PK `Version` | `RailwayBimSceneSetup`, `MigrateStages`, `RailwayBimScenario`. Dấu seed quyết định có chạy logic bổ sung hay sửa dữ liệu minh họa. |

Mọi FK khai báo có `ON UPDATE` và `ON DELETE` mặc định **NO ACTION**. Xóa Asset không tự dọn bảng con. `ReplacementAssetID` trong trường Nâng cấp là tham chiếu văn bản, hiện rỗng ở cả 33.434 hàng Editor mang khóa này, vẫn cần kiểm trước mỗi lần trích dữ liệu. Tổng seed **64.098.304 byte (~61,13 MiB)**. Tổng Editor **376.983.552 byte (~359,52 MiB)**, riêng `AssetStageFields` chiếm khoảng **68,7%**, hai bảng giai đoạn cộng lại khoảng **76,6%**. [Store](../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimStore.cs) · [Giai đoạn](../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimStages.cs) · [Kịch bản](../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimScenario.cs) · [Workbook](../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimWorkbook.cs)

### Web khởi tạo và lưu

1. Seed là `Assets/StreamingAssets/RailwayLifecycle/Initial.db` schema 1, tách khỏi bản Editor trong `UserData`. Kiểm tra bản Web Development hiện hữu thấy bản seed tại `StreamingAssets` có cùng dung lượng 64.098.304 byte. Scene BIM dùng `UnityWebRequest` đọc seed khi chưa có bảng `Assets` trong DB ở `Application.persistentDataPath`, deserialize read-only trong RAM rồi backup thành DB local của ứng dụng.
2. `RailwayBimStore` bật FK, chạy `Migrate`, nạp registry và `MigrateStages`. Schema 2 thêm hai bảng và trường `StageKey`; seed có thể điền 6 stage và 45 giá trị trường mỗi tài sản, theo phân bố 8/8/8/7/5/9 cho Quy hoạch/Thiết kế/Thi công/Vận hành/Bảo trì/Nâng cấp, rồi áp dụng `RailwayBimScenario` qua các dấu phiên bản. Tệp seed chưa chứa các giá trị này, và dữ liệu Editor đã có các bản ghi phát sinh riêng.
3. SQLite Web dùng `idbvfs` của `com.gilzoide.sqlite-net` 1.3.2, gọi `FS.syncfs(false)` qua hàng đợi. `UttWebLinker` tắt auto-sync riêng của Unity để tránh xung đột journal. Bài thử cũ đọc lại hai trường sau khi đợi 8,5 giây rồi reload. Audit này không chạy browser và chưa xác minh đóng tab ngay sau khi ghi. Đổi nội dung seed mà không làm rõ file DB/version sẽ không cập nhật hồ sơ người dùng cũ vì `HasWebSeed()` thấy bảng `Assets`. [Controller](../../Assets/ProjectLopHoc/Scripts/Lifecycle/RailwayBimController.cs) · [Cấu hình Web](../../Assets/Editor/UttWebLinker.cs) · [Kết quả thử persistence trước đó](../PlatformCompatibility/WebTests/Web_Test_Report.md)

## 2. Ba tập mẫu có thể kiểm thử

Số đếm lấy trực tiếp bằng truy vấn `SELECT … WHERE AssetID IN (…)` trên hai nguồn. Dùng ID theo `SegmentID` và `BlockID` trên cả TR/TL. Kèm đầy đủ cha; nhóm tổng hợp chỉ ở UI. Các tập chỉ là đề xuất, chưa có tập nào được áp dụng. Cả ba tập có đủ 12 `ComponentType` và đều giữ bu lông mẫu `Z021-TR-S001-B001-C005`.

| Kịch bản | Tập mô-đun | Assets | GROUP (Asset / kể cả 3 nút tổng hợp) | COMPONENT | Stage Editor | StageField Editor |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| **4 mô-đun** | Cả TR/TL, S001, B001–B002 mỗi đường | 402 | 6 / 9 | 396 | 2.412 | 18.090 |
| **8 mô-đun** | Cả TR/TL, S001, B001–B004 mỗi đường | 802 | 10 / 13 | 792 | 4.812 | 36.090 |
| **4 phân đoạn** | TR/TL × S001 và S017, gồm 10 + 7 mô-đun mỗi đường | 3.404 | 38 / 41 | 3.366 | 20.424 | 153.180 |

S001 nằm ở 5.300,0015–5.330,0015 m, S017 ở 5.780,0015–5.801,0015 m theo DB. S017 hữu ích để thử đầu cuối không đủ 10 mô-đun. Số GROUP trên bảng tính gốc `Z021` và hai đường làm ba node bổ sung, chưa tính nút tuyến hay bốn chuyên ngành mới. Bảng stage của seed chưa có, nên số giữ vật lý trên seed là **0 / 0**. Với tập mẫu và logic seed hiện tại, 6/45 giá trị được sinh trên mỗi tài sản khi khởi tạo mới, nhưng hồ sơ phát sinh của Editor chỉ có nếu trích từ Editor với dữ liệu tương ứng.

| Kịch bản | Seed kiểm tra / bảo trì / sự kiện | Editor kiểm tra / bảo trì / sự kiện | Thuộc tính seed / Editor | Lifecycle mỗi DB | Ước lượng DB seed / Editor |
| --- | ---: | ---: | ---: | ---: | ---: |
| 4 mô-đun | 402 / 135 / 402 | 405 / 138 / 957 | 2.010 / 2.011 | 402 | ~0,75 / 4,34 MiB |
| 8 mô-đun | 802 / 267 / 802 | 805 / 270 / 1.889 | 4.010 / 4.011 | 802 | ~1,48 / 8,64 MiB |
| 4 phân đoạn | 3.404 / 1.136 / 3.404 | 3.407 / 1.139 / 7.962 | 17.020 / 17.021 | 3.404 | ~6,23 / 36,62 MiB |

Ước tính theo tỷ lệ bản ghi giữ lại trong từng bảng nhân số byte `dbstat` của bảng và index hiện hành, cộng metadata bảng phiên bản. Chưa tạo bản sao để đo. Sai số có thể đến từ độ dài trường theo tập và độ đầy B-tree. Các số không bao gồm tài sản mới, dữ liệu phát sinh trên từng trình duyệt hoặc journal tạm.

## 3. So sánh cách giảm và tác động

| Phương án | Tác động và quyết định |
| --- | --- |
| **A · Giữ DB, lọc danh sách** | Khởi đầu ít rủi ro. Tree dùng danh sách ID mẫu và có thể cung cấp chế độ xem toàn bộ. Bốn mẫu dễ giới thiệu, tám mẫu kiểm nhiều chuyển mô-đun hơn, bốn phân đoạn kiểm được mép khu đoạn và số con khác nhau. Chọn/highlight theo controller hiện có. Thành phần ngoài mẫu có thể báo ngoài phạm vi demo hoặc bật nhánh khi chọn 3D. Giữ Import/Export toàn bộ theo nghiệp vụ hiện hành, thêm lựa chọn xuất mẫu tường minh nếu cần. Dung lượng seed, migration và memory registry hiện hành vẫn như cũ. |
| **B · DB demo mới** | Phù hợp khi mục tiêu là giảm download seed lúc vào BIM và giảm load/memory SQLite sau khi đã duyệt. Ưu tiên tám mô-đun. Giữ nguyên ID, hai bên ray và hình học, filter catalog lookup và cấp detail khỏi việc trỏ vào ID không có hồ sơ. Cần xử lý overview tĩnh có 34 proxy phân đoạn, pool mô-đun lấy từ catalog đầy đủ và visual chưa có trong DB demo. `Pick` bỏ qua ID ngoài registry, song `CurrentItems`/`Locate` và nút cấp chi tiết cần test khi danh mục mô hình lớn hơn danh mục hồ sơ. XLSX ngoài tập như mẫu cũ `Z021-TL-S002` sẽ bị báo orphan trong DB demo. Dùng file DB/version demo riêng, giữ DB browser cũ và chuẩn bị workbook mẫu khớp tập. |
| **C · Xóa DB đầy đủ** | Chưa làm. FK NO ACTION, nhật ký và dữ liệu đã lưu của người dùng có thể bị mất. Trình phát đã có DB ở persistentDataPath cũng không tự đồng bộ khi thay seed. Xóa row không đồng nghĩa file giảm dung lượng vật lý. |

### Khi được duyệt phương án B

1. Ghi kiểm kê hash, phiên bản schema, danh sách AssetID và backup có thể phục hồi. Với DB đang hoạt động, sao lưu nhất quán bằng SQLite Backup API hoặc dừng writer rồi sao lưu. Trên Web, xuất dữ liệu người dùng trước khi chuyển phiên bản.
2. Tạo file demo mới ngoài nguồn. Trích ID đã duyệt và đóng tập theo cha, dùng schema/index đúng phiên bản. Chép hàng giữ lại bằng thứ tự cha → con, giữ sự kiện, kiểm tra, bảo trì và trạng thái. Xác minh mọi tham chiếu logic như `ReplacementAssetID`; giữ trạng thái tham chiếu ngoài tập hoặc trình bày rõ, tránh tự sửa ID. Sao chép đúng dấu seed để tránh ghi đè hồ sơ giai đoạn.
3. Trên **file demo** chạy `integrity_check`, `foreign_key_check`, so sánh PK/FK/đếm từng bảng với các truy vấn gốc, so hash nguồn, thử import/export bằng workbook khớp ID, thử lưu và reload bằng browser profile riêng. Đo kích thước file, page count và dbstat sau khi đóng kết nối. Nếu cần đo kích thước đã đóng gói, compact bằng `VACUUM INTO` một file **khác** trong thư mục scratch rồi đo, chỉ sau khi được duyệt. Ghi thời gian tải seed, mở DB, p95 khi cuộn và bộ nhớ trước/sau.
4. Rollback bằng cấu hình chọn lại DB đầy đủ, dùng nguyên file persistent cũ và workbook backup. Thất bại ở rename, báo orphan hoặc mất dữ liệu sau reload là điều kiện dừng trước khi phát hành demo.

**Đề xuất.** Giảm phạm vi hiển thị xuống **8 mô-đun** trước. Duy trì DB đầy đủ. Chỉ tạo bản demo riêng nếu cần giảm chi phí khởi tạo Web sau khi đo và có phê duyệt. Không cần thay hình học ray để thực hiện cả A hoặc B.
