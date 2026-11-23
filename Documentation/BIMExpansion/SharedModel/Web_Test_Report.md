# Kiểm thử WebGL với hai cách hiển thị cầu SO 201

## Bản thử

Unity 6000.3.0f1 xuất WebGL Development riêng ở `Builds/BridgeShared_Web`. Kết quả **0 lỗi build, 14 cảnh báo**, **811.868.305 byte**, thời gian build cuối **2 phút 57,573 giây**. Danh sách đưa vào bản thử gồm bootstrap cục bộ, scene quản lý, scene tuyến, scene Visual additive và 17 Zone lấy theo registry hiện hành. Build Profile sản phẩm giữ nguyên.

Chrome 157 headless chạy qua HTTP loopback với profile thử riêng, WebGL 2 qua ANGLE Direct3D 11 trên NVIDIA GeForce RTX 3060. Viewport trình duyệt 1.280 × 720. Vùng Canvas của BIM phủ viewport, còn tuyến dùng Canvas 960 × 600 của Web template. Số đo route tắt/bật dùng cùng Canvas và chainage khoảng 25 m. Thời gian nạp tính từ điều hướng đến log sẵn sàng, ở profile đã có seed ray và đã tắt HTTP cache. IndexedDB vẫn hoạt động.

## Kết quả chức năng

| Kiểm tra | Quan sát |
| --- | --- |
| Cây và hình học trong scene quản lý | Đăng ký **91 cấu kiện cầu** cùng 33.434 tài sản ray hiện hành. Chọn Pavement trong cây tự đưa camera tới cầu. Sau khi bỏ chọn rồi click Pavement trong vùng 3D, log khớp AssetID, một highlight và cây mở các tổ tiên đúng. [Ảnh cây và model](Screenshots/BIM_Detailed_Web.png) |
| Dữ liệu IFC | Đọc GlobalId, AssetID, IFC class `IfcPavement`, GlobalId cha và PredefinedType `FLEXIBLE`. Mục Property Sets hiển thị “Chưa có dữ liệu”. [Ảnh thông số](Screenshots/BIM_IFC_Properties_Web.png) |
| Lưu và mở lại | Đã nhập trạng thái riêng cho cả **6 giai đoạn**, một ghi chú kỹ thuật và một thông số tên/giá trị/đơn vị. Mở lại Web app cùng origin và mở lại hồ sơ Pavement. Đối chiếu đọc lại từng giai đoạn, ghi chú và thông số trong UI và SQLite lấy từ IndexedDB. `PRAGMA integrity_check` trả `ok`. Toàn bộ dữ liệu QA chỉ nằm trong profile trình duyệt thử. [Ảnh vòng đời](Screenshots/BIM_Stage_Reopened_Web.png) · [Ảnh thông số đã đọc lại](Screenshots/BIM_Technical_Reopened_Web.png) |
| Hồi quy ray | Tìm và chọn được `Z021-TR-S001-B001-C005` sau khi tương tác cầu. Hồ sơ và geometry ray vẫn đi qua controller ray hiện hữu. [Ảnh hồi quy](Screenshots/BIM_Rail_Regression_Web.png) |
| Tuyến ở trạng thái đứng yên | Zone_010 và Zone_011 được nạp. Bật F9 nạp Visual additive, log ghi **91 renderer, 0 collider, 0 adapter BIM trong scene thử**. Tắt F9 unload preview và trở lại camera tuyến. [Ảnh Visual](Screenshots/YenTu_Visual_Web.png) · [Ảnh sau unload](Screenshots/YenTu_Visual_Unloaded_Web.png) |
| Tuyến khi tàu chạy | Tàu đi từ **25,0 m đến 877,3 m** bằng ghế lái và đầu vào ga/phanh của tàu trong bootstrap QA. Visual vẫn mở, Zone_010 đến Zone_013 lần lượt được nạp, Zone_010 được thu hồi phía sau. Lượt này không ghi nhận lỗi tải Zone. [Ảnh Visual trong lượt chạy](Screenshots/YenTu_Visual_During_Streaming_Web.png) |

SQLite Web dùng hàng đợi IndexedDB của plugin. Lượt đầu điều hướng sớm sau ghi chưa đọc lại được dấu kiểm tra. Lượt cuối chờ dữ liệu xuất hiện trong IndexedDB rồi mở lại và xác nhận cả sáu trạng thái cùng thông số. Đóng tab ngay sau khi bấm lưu chưa được xác nhận.

## Đo trình duyệt và Unity

| Scenario | Nạp đến Ready | Renderer đang hoạt động | Draw calls Unity | P95 / P99 | Bộ nhớ Unity trong mẫu |
| --- | ---: | ---: | ---: | ---: | ---: |
| Quản lý, đang xem cấu kiện cầu | 9,71 s | 146 | 120 | 8,50 / 8,50 ms | 399,43 MB |
| Yên Tử, tắt preview | 8,93 s | 97 | 171 | 8,40 / 8,50 ms | 434,10–434,39 MB |
| Yên Tử, bật preview | Bật cùng phiên | 188 | 263 | 8,40 / 8,50 ms | 438,13–438,63 MB |

Mẫu `requestAnimationFrame` dài khoảng 5 giây khi đứng yên, gồm 599–600 khoảng khung hình mỗi kịch bản. Renderers là tổng renderer đang bật trong các scene đã nạp. Hai camera cùng tồn tại khi preview mở, nên chênh lệch draw calls phản ánh phép thử additive, không phải chi phí thay cầu hiện hành. Lượt đầu với profile mới mất **54,02 s** để quản lý sẵn sàng, có khởi tạo SQLite ray; chưa tách thời gian từng giai đoạn. Bộ nhớ là counter Unity Total Used Memory, chưa đo riêng RSS hoặc VRAM.

Console có cảnh báo shader WebGL nội bộ Unity từ pipeline dự án và cảnh báo cơ chế đồng bộ file cũ. Chưa kiểm thử toàn tuyến hoặc thiết bị thật. **VR NOT TESTED.**

## Mở lại bản thử

Chạy máy chủ loopback ở thư mục project bằng `node Documentation/PlatformCompatibility/WebTests/serve.mjs Builds/BridgeShared_Web 8788`.

- [Quản lý vòng đời tài sản](http://127.0.0.1:8788/?view=bim&bimAudit=1)
- [Yên Tử với chế độ Bridge_Visual](http://127.0.0.1:8788/?view=visual). F9 ẩn hoặc hiện scene thử.
- [Yên Tử với camera tuyến](http://127.0.0.1:8788/?view=route)

Entry point build cục bộ là `UttVr.BridgePilot.BridgeSharedModelEditor.BuildWeb` với Unity Batch Mode và `-buildTarget WebGL`. Chạy khi đã đóng instance Unity đang sử dụng project. Xem `Temp/BridgeShared/BuildStatus.txt` sau khi Unity thoát. Chỉ dùng bootstrap và scene additive này để kiểm thử cục bộ.
