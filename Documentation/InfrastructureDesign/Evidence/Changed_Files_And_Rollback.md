# Thay đổi G0/G1 và điểm khôi phục

## G2 Production và G3 Web

- Các scene Zone_010–018 và prefab BIM tương ứng nhận mesh, bản sao terrain và phần OCL giữ lại từ tác vụ tích hợp. Scene QuanLyVongDoiTaiSan tham chiếu catalog sản phẩm và loader. Gói theo Zone và chunk nằm trong `Assets/ProjectLopHoc/Generated/InfrastructureProduction`.
- Các script `InfrastructureProduction*.cs` chuẩn bị tích hợp, kiểm định và build. Script BIM thêm nhánh tìm kiếm, nạp theo chunk, thao tác hiển thị và Excel vào các tab hiện hành. File SQLite riêng là `RailwayInfrastructure_G2_Production.db`. Trên Web, `UttWorkbook.jslib` xác nhận khi các trang của riêng database này đã được ghi vào IndexedDB. Hoàn tác nhánh lưu Web cùng script dashboard và store nếu quay về baseline.
- [Bản rollback trước Production](../Rollback/G2_Production_PreChange_20261127.zip) có SHA-256 `FAEBC3465E3F143E8417855CA7D169F3AA9F9614A5091EEFE2105AE3BAE7C362`. Archive có 147 file. Đối chiếu cuối có 24 file thay đổi, gồm 19 scene/prefab và 5 script BIM. 123 file còn lại khớp byte. Trong 129 file thuộc nhóm scene và hạ tầng, 110 file khớp byte. Khi cần rollback một công trình, dùng bản sao Zone và prefab BIM cùng phạm vi để tránh trùng Visual. Scene BIM và catalog phải cùng phiên bản. Dữ liệu SQLite người dùng được giữ riêng.
- Danh sách 17 Zone, Build Settings và Scene Pilot không đổi. Cầu SO 201, tàu, spline, Login, Classroom, Showroom, UTTCampus và SQLite ray được kiểm hash theo phạm vi được bảo vệ.

## G2 Finalization

- Cập nhật `InfrastructureG2Pilot*.cs`, `Design_Configuration.json` và các asset sinh trong `Assets/ExternalBIM/InfrastructureG2Pilot`. Cùng một Scene Pilot và generator được sử dụng. Menu Unity **Tools → UTTVR → Infrastructure → Generate isolated G2 Pilot** dựng lại ứng viên. **Verify saved G2 Pilot** mở lại bản đã lưu và kiểm binding.
- `InfrastructureDetailedLoader.cs` dựng GameObject theo chunk từ catalog. `BridgeBimIdentity` cho phép gắn identity lúc nạp. `BridgeBimAdapter` dùng cùng phép khởi tạo cho mẫu cũ và loader. `BridgeInfrastructureStore` thêm đường dẫn lưu riêng cho schema Pilot, các schema cầu giữ đường dẫn đã có.
- [Snapshot trước Finalization](../Rollback/G2_Finalization_PreChange_20261127.zip) chứa Scene, geometry và bằng chứng Pilot trước lượt chỉnh sửa. SHA-256 `49B00E6A0B04A9C99A89ED0120B64A2DB9465ECE647C664661B5863FEE5A4808`. Đóng Scene Pilot trước khi khôi phục cặp scene/asset và meta trong snapshot. [Snapshot 129 file bảo vệ](../Rollback/G2_Pilot_PreIntegration_20261127.zip) phục vụ đối chiếu sản phẩm.
- G2 Production vẫn chờ duyệt. Kế hoạch áp dụng là đưa Visual theo owner/Zone/chunk đã kiểm chứng vào các Zone hiện có, thay thế đúng instance cũ của từng công trình đã duyệt, để nguyên OCL ga hiện hành, và nối Detailed với controller/Tree View. Loader và store cần duy trì catalog đầy đủ khi đổi chunk để giữ truy vấn và dữ liệu người dùng. Kiểm tra load/unload qua ranh giới trước G3.

- File `AGENTS.md` ở project root chứa quy tắc bổ sung. Kiểm kê lúc bắt đầu không thấy AGENTS.md ở root.
- `InfrastructureG1.cs`, `InfrastructureG1Modules.cs`, `InfrastructureG1Land.cs` và `InfrastructureG1Review.cs` dưới `Assets/Editor` chứa đo đạc, generator, kiểm thử và xuất ảnh. Bộ chạy SO 201 được bổ sung lệnh `infrastructure-g1`. Menu Unity tương ứng là **Tools → UTTVR → Infrastructure → Generate and validate G1**.
- Scene và asset đầu ra riêng tại `Assets/ExternalBIM/InfrastructureG1`. Các script kiểm kê nguồn miễn phí nằm trong `Documentation/InfrastructureDesign/Tools`.
- `Temp/InfrastructureG1/Before_G1.zip` là snapshot Git của các đường dẫn đã kiểm kê. `Protected_Baseline.sha256` ghi SHA-256 bản trước khi chạy. Đợt import IFC tự động của Unity đã làm thay đổi prefab tàu. Nội dung trước tác vụ được khôi phục từ baseline trước phép kiểm cuối. Bản IFC/FBX/BLEND vẫn theo hash Source Manifest.
- Để hoàn tác G1, đóng scene mẫu nếu đang mở, sau đó hoàn tác riêng các file G1 và dòng lệnh đã thêm vào watcher. Giữ lại các nguồn người dùng đã đặt. Các scene sản phẩm và cầu SO 201 không cần rollback.

Lượt Visual Refinement cập nhật riêng generator G1, scene mẫu, catalog và Evidence. Hàm cắt mesh dùng chung trong `SO201ExtendedBridgeSandbox.cs` chuyển sang chỉ số 32 bit khi vượt giới hạn 65.535 đỉnh. Nhánh xử lý cũ vẫn dùng chỉ số 16 bit cho lưới nhỏ. Cầu SO 201 đã duyệt được kiểm tra theo checksum, không dựng lại.
