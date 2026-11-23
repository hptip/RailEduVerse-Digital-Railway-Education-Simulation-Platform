# Kiểm thử WebGL Zone Viewer

## Bản thực thi

- Unity 6000.3.0f1, Development Build tại `Builds/ZoneBasedBim_Web`. **0 lỗi, 1.061 cảnh báo**, 1.378.785.781 byte, 12 phút 29,33 giây. Giữ nguyên danh sách 24 scene của profile và khởi động từ Login.
- Chrome headless 1.280 × 720 qua HTTP localhost. Người kiểm thử nhấn Đăng Nhập, vào Campus rồi chọn Quản lý vòng đời tài sản bằng menu hiện hành. Lượt đầu mất **49,46 s** từ click menu tới log BIM Ready, gồm khởi tạo dữ liệu ray ở profile mới.
- Khi tìm `zone_`, Tree View trả đúng **17 kết quả**. Đã click lần lượt cả 17 đoạn trên trình duyệt. Mỗi click có đúng một sự kiện `ZoneReady` với đúng Zone được chọn.

## Kết quả tương tác

| Hạng mục | Quan sát |
| --- | --- |
| Đoạn tuyến 09 | Nạp Zone_017, 018 và 019, **113 renderer** bật trong nhóm xem. Thấy đường ray, dải mặt nước và toàn đoạn cầu trong ngữ cảnh nguồn. Nhánh Cầu chính báo chưa có hồ sơ. [Ảnh Zone_018](Screenshots/Web_Zone018_Bridge.png) |
| Đoạn tuyến 12 | Quay về Zone_021 thu hồi toàn bộ instance viewer ngoài Z021. Tìm `Z021-TR-S001-B001-C005` và chọn được bu lông. Thông số và Vòng đời mở với dữ liệu hiện có, sáu nút giai đoạn còn hoạt động. [C005](Screenshots/Web_Z021_C005.png) · [Vòng đời](Screenshots/Web_Z021_Lifecycle.png) |
| Tập Zone hoạt động | Tối đa **4 Zone viewer** trong lượt đi hết danh sách, khi gặp giao của các nhóm phụ thuộc. 018 giữ 3 Zone, 021 giữ 0 prefab bên ngoài, các đoạn 020 và 022–026 giữ một Zone. |
| Cầu và hầm nhiều Zone | Owner dùng một khóa công trình; các Zone còn lại dùng tham chiếu. Các Zone 012–013 mở đúng nhóm Forest_02. Các Zone 014–015 mở đúng nhóm Forest_01. 016 giữ thêm Zone_017 vì phạm vi cổng; 017 giữ Zone_016 ngoài nhóm cầu. |
| Kiểm tra cảnh quan | Quan sát vùng 018 sau khi chuyển từ 021; đoạn ray ở đúng vị trí tương đối so với môi trường và cầu trong góc đã kiểm tra. Các trường hợp qua hai đầu candidate chưa có dữ liệu để thử. |

## Số đo

- Click → nhận sự kiện `ZoneReady` trong 17 lượt là khoảng **193–358 ms**, gồm độ trễ của automation click và kiểm tra log. Đo đầu tiên riêng cho Zone_018 là **266 ms**.
- Mẫu frame trình duyệt khoảng 5 giây ngay sau khi vào 018 có **539 khoảng rAF**. Median **8,30 ms**, p95 **8,40 ms**, p99 **8,50 ms**. Có một khoảng lớn nhất **474,90 ms** trong mẫu. Kết quả mô tả bản Web ở máy kiểm thử.
- Mẫu đó ghi JS heap đang dùng **87,93 MB** và backing storage **1.509,81 MB** qua Chrome DevTools. Các chỉ số này thuộc phiên trình duyệt, chưa tách tổng memory Unity, native process RSS hoặc VRAM.

**VR NOT TESTED.** Cầu thay thế chưa qua bước kiểm chứng vì source hiện có chưa phù hợp.

Chạy lại tại project bằng `node Documentation/PlatformCompatibility/WebTests/serve.mjs Builds/ZoneBasedBim_Web 8790` rồi mở [bản Web thử](http://127.0.0.1:8790/?bimAudit=1).

## Chuyển lặp và đo sau khi ổn định

Chrome 157, WebGL 2 qua ANGLE Direct3D 11 trên RTX 3060. Đổi lặp **4 vòng Zone_018 ↔ Zone_021**. Mỗi lần đứng tại 018 đều có **3 Zone và 113 renderer**. Sau V8 GC, JS heap ở 018 lần lượt là **44,65 → 44,87 → 44,87 → 44,87 MB**. Backing storage giữ gần **1.503,93 MB**. Không thấy số instance tích lũy hoặc xu hướng tăng liên tục trong bốn vòng này. Chưa có đo leak native dài hạn hoặc Quest.

Lượt đứng yên sau khi chờ ổn định ở 018 cho **600 khoảng khung hình**, p95 **8,40 ms**, p99 **8,50 ms**, tối đa **8,60 ms**. Console trong chuỗi đổi Zone không có exception hoặc thiếu hình học. [Ảnh quy mô công trình ở góc orbit](Screenshots/Web_Zone018_Orbit.png) · [Ảnh vùng hầm](Screenshots/Web_Zone015_Tunnel.png)

## Chọn hai chiều và Excel

Trong Zone_021, tìm kiếm chọn đúng `Z021-TR-S001-B001-C005`. Từ màn hình 3D, Alt-click qua các collider giao nhau ghi lần lượt C019, C009, rồi **C005**. Log C005 ghi đúng một renderer highlight và cây mở lại đường dẫn cấu kiện. [Ảnh chọn từ 3D](Screenshots/Web_Z021_Tree_From_Model.png)

Xuất Excel tạo tệp `TaiSanDuongSat.xlsx` 9.384 byte từ UI trong profile thử. Nút Nhập Excel đã phát sự kiện mở trình chọn tệp. Kiểm tra quyền tự động đã chặn thử nghiệm tự chọn workbook chưa xác định, vì vậy lượt này dừng ở hộp thoại và không nạp workbook. [Ảnh xuất Excel](Screenshots/Web_Excel_Export.png)
