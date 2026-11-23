# Kiểm thử BIM Tree trên Web

Unity 6000.3.0f1, profile **UTTVR Web**, 24 Scene, máy chủ HTTP loopback và Chrome điều khiển qua DevTools Protocol. Lượt đầu dùng WebGL 2 qua ANGLE D3D11 trên NVIDIA RTX 3060. Các thao tác ghi chạy trong profile trình duyệt riêng. Cổng thử nghiệm và bộ nhớ trình duyệt tách khỏi phiên sử dụng thường ngày.

## Bản bàn giao và nghiệm thu

**`Builds/UTTVR_BIMTreeWeb_Measured`** là bản Development đã kiểm thử, 1.378.297.018 byte, 0 lỗi build, 14 cảnh báo, 24 Scene. Build cuối ghi thời gian 1 phút 55,9 giây khi cache Unity đã có sẵn. Đã kiểm tra bằng HTTP ở cổng 8785, từ Login qua Campus và menu đổi scene.

| Hạng mục | Kết quả | Bằng chứng |
| --- | --- | --- |
| Bố cục, resize, không có panel cũ | PASS | Canvas tự phủ viewport từ 1280×720 lên 1920×1080. Panel rộng 38%, camera bên phải, đúng ba tab. [Ảnh 1920×1080](04_Viewport_1920x1080_Web.png) |
| Mở/thu cây, tìm theo ID và tên | PASS | Sau sửa raycast, đếm 1 → 3 → 20 → 30 → 129 hàng logic khi lần lượt mở Khu đoạn, TR, S001, B001. B001 thêm đúng 99 cấu kiện. `Z021-TR-S001-B001-C005` ra 1 kết quả. `Concrete` trả kết quả theo tên với giới hạn 150. |
| Cây → model → cây | PASS | Chọn C005 từ cây và dùng `F`. Sau `Esc` và thu gốc, click trực tiếp lại bu lông. Console ghi `ModelPick,Z021-TR-S001-B001-C005,1`, cây mở lại tổ tiên và chọn đúng hàng. [Ảnh Danh sách](01_Danh_sach_Web.png) |
| Giữ lựa chọn, thông số thật, GROUP | PASS | C005 và outline giữ nguyên qua ba tab. Thông số hiện AssetID, cha B001 và tham chiếu `ThanhNgangBetongDucSan_001_BuLongVit_001`. Chọn phân đoạn ở overview rồi vào Thông số chỉ thấy hướng dẫn chọn lá. [Ảnh Thông số](02_Thong_so_Web.png) |
| Sáu giai đoạn và tooltip | PASS | Click lần lượt 1–6 với C005, xem được các nội dung khác nhau và tooltip tên giai đoạn. [Ảnh Vòng đời – Vận hành](03_Vong_doi_Web.png) |
| Camera và giao dịch cơ bản | PASS ở lượt kiểm thử ban đầu | Xoay, dịch, zoom, Esc, lưu tên/trạng thái và import/export được thử ở build đầu. Bản bàn giao chạy lại focus, Esc và cả hai chiều chọn. Các mẫu ghi được giữ trong sandbox của Chrome. |
| Sửa toàn bộ loại nhật ký/kiểm tra/bảo trì | NOT TESTED | Đợt này đã mở hồ sơ và kiểm tra nút nghiệp vụ. Chưa ghi mới từng loại bản ghi. |
| Console | Có cảnh báo hiện hữu | Chrome ghi ba lỗi shader không hỗ trợ `CoreCopy`, `StencilDitherMaskSeed` và `HDRDebugView`. Không thấy exception của BIM Explorer trong chuỗi thao tác kiểm thử. |

## Hiệu năng quan sát được

- B001 mở thêm 99 lá trong **0,9 ms**. Cây có **129 hàng logic** lúc đó và chỉ dùng **19 hàng UI**. Chọn C005 từ cây mất **5,1 ms** trong mẫu. Truy vấn ID mất **17,6 ms** và `Concrete` mất **8,9 ms**, tính phần lọc/render sau debounce.
- Một lượt cuộn 5 giây ở 1280×720, 505 khoảng `requestAnimationFrame`. Median **8,3 ms**, p95 **8,4 ms**, p99 **16,6 ms**, lớn nhất **250 ms**. Ba mẫu Unity lấy rời rạc trong lượt dùng 19 hàng UI, ghi GC 0 byte ở ba mẫu và `Canvas.BuildBatch` cao nhất **0,300001 ms**. Đây là mẫu điểm, không xác nhận GC của mọi khung hình.
- Lúc mở scene trên một lượt thử, Unity ghi khoảng **36,556 giây giữa hai khung hình** và mẫu GC **110.274.670 byte**. Chưa cô lập phần đóng góp của tải SQLite, khởi tạo dữ liệu và trình duyệt. Kết quả cuộn sau khi tải xong không dùng để suy ra thời gian vào cảnh hoặc hiệu năng bản Release.

Chi tiết trong [CSV hiệu năng](BIM_Tree_Performance.csv).

## Bản Web kiểm thử đầu tiên

Build `Builds/UTTVR_BIMTreeWeb` hoàn tất sau 11 phút 43 giây, 986.752.932 byte, 0 lỗi build và 1.068 cảnh báo do BuildReport đếm. Phiên Chrome đi qua Login, Campus và mở Quản lý vòng đời tài sản từ menu. Canvas của lượt này được đặt toàn màn hình từ harness để thử bố cục 1280×720.

| Hạng mục | Kết quả | Quan sát |
| --- | --- | --- |
| Bố cục 1280×720 | PASS | Panel duy nhất rộng khoảng 486 px, ba tab đúng thứ tự và camera bên phải. |
| Tìm mã `Z021-TR-S001-B001-C005` | PASS | Một kết quả đúng Bu lông C005, có đường dẫn cha. |
| Click hàng cây | FAIL ở bản đầu | Image trên template có Raycast Target tắt. Đã sửa mã để bật raycast cho nút và cần xác nhận trên bản có sửa. |
| Chọn 3D và highlight | PASS ở bản đầu | Chọn lần lượt phân đoạn, mô-đun, rồi tấm nền `Z021-TL-S008-B005-C003`. Outline bám đúng tấm nền. |
| Chuột và bỏ chọn | PASS ở bản đầu | Kéo trái, kéo phải và cuộn bánh xe thay đổi ảnh vùng model khi Thông số/Vòng đời vẫn mở. `Esc` xóa outline. |
| Hồ sơ GROUP và hồ sơ lá | PASS ở bản đầu | Khi chưa chọn lá, hiển thị hướng dẫn. Sau khi chọn tấm nền, hai tab đọc cùng AssetID; thấy tham chiếu `TamNenBetongRay49E1_001/connected_2`. |
| Vòng đời | PASS một phần ở bản đầu | Nút 1–6 trên một hàng, nút 4 hiện Vận hành và tooltip. Hồ sơ hiện dữ liệu và hành động Kiểm tra. |
| Sửa tên và trạng thái | PASS trong phiên thử nghiệm | Đã lưu `BIM Sandbox Name` và trạng thái giai đoạn 4 `BIM Sandbox Stage` cho tấm nền. Dashboard hiển thị thông báo lưu và đọc lại giá trị trong phiên. |
| Excel Web | PASS ở bản đầu | Tải `TaiSanDuongSat.xlsx`; đọc lại được 10 sheet, sheet `Assets` chứa đúng ID được chọn. Từ chối file chứa `AUDIT_ONLY`. File sandbox hợp lệ qua preview và Xác nhận nhập trả thông báo đã lưu. |

## Thiết bị

**Quest 3S / Android VR — NOT TESTED.** Không có thiết bị. Đối chiếu tệp và phạm vi thay đổi trong [báo cáo tính toàn vẹn model](BIM_Tree_Model_Integrity.md).
