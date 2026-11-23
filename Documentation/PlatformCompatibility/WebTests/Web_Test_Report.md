# UTTVR Web · Kiểm thử bản Login

Bản `Builds/UTTVR_WebLogin_Final` đã build bằng Unity 6000.3.0f1. BuildReport ghi `Succeeded`, 0 lỗi, 20 cảnh báo và 986.736.517 byte. Web Build Profile bắt đầu tại Login, có 24 scene, gồm 17 Zone đăng ký và ChiTietSaBan phụ thuộc.

## Chạy lại

Mở [demo cục bộ](http://127.0.0.1:8774/) bằng Chrome hoặc Edge. Đăng nhập bằng tài khoản demo có sẵn. `M` hoặc `F1` mở menu, `Esc` đóng. Giữ chuột phải để xoay góc nhìn, WASD để di chuyển, `L` mở bảng trong Classroom.

```powershell
node Documentation/PlatformCompatibility/WebTests/serve.mjs Builds/UTTVR_WebLogin_Final 8774
```

Để build lại, kích hoạt **UTTVR Web** trong Build Profiles, kiểm tra Login ở đầu Scene List, chọn **Build** và đưa output vào thư mục ngoài Assets. Giữ target Android tích hợp cho Quest.

## Kết quả Chrome

- Login ở khởi động. Đổi Toggle và gửi vai trò sai báo lỗi tại Login. Vai trò đúng vào Campus. Đăng nhập giảng viên bằng nhập bàn phím mở được bảng danh sách trong Classroom.
- Menu dùng chung bộ tạo giao diện với VR. Đã click bốn đích từ Campus rồi click Campus để quay lại, chụp ở 1280×720 và 1920×1080, và thử chuyển tab rồi quay lại. Một số glyph tiếng Việt biến mất với font mặc định giữ nguyên. Unity Web không dùng trực tiếp font cài trên máy theo [tài liệu đồ họa Web của Unity](https://docs.unity3d.com/cn/2023.2/Manual/webgl-graphics.html).
- Classroom đã đổi bài và slide hai chiều. Hình trên bảng khớp nội dung slide. Campus nhận WASD và mouse look. Hai lần thử click cửa Campus chưa chuyển cảnh.
- Showroom chạy/dừng, chọn x4, có thao tác reset và thử tắt/bật tám lớp. Ảnh các nút và so sánh trạng thái màu nằm trong Evidence. Popup BIM, chuyển động ở góc nhìn cận và chatbot chưa nghiệm thu.
- Yên Tử mở đúng nội thất toa. Đã thử đi đến ghế lái và bấm F ở hai vai trò. Chưa quan sát được HUD ghế lái hoặc chứng minh chạy qua ranh giới Zone, cầu và hầm.
- Quản lý vòng đời tài sản đã tìm `s002`, chọn, xem vòng đời, tải XLSX, từ chối AssetID giả và áp dụng XLSX hợp lệ trong hồ sơ Chrome riêng. Lượt với Unity auto-sync từng mất lượt cập nhật sau reload. Bản bàn giao dùng hàng đợi `idbvfs` của SQLite. Cả lượt thử riêng và lượt ở URL bàn giao đọc lại đúng hai trường sau 8,5 giây chờ rồi reload và đăng nhập. Chưa đo đóng tab ngay sau xác nhận. Hai SQLite nguồn giữ nguyên. ChiTietSaBan vẫn nằm trong build và không xuất hiện ở menu. Đường mở gián tiếp chưa được kiểm thử lại.

## Phép đo và lỗi còn lại

`Web_Performance_Before_After.csv` ghi FPS, frame time, draw calls WebGL, số tam giác gửi và JS heap trước/sau. Các mẫu 5 giây trên RTX 3060 cho thấy Showroom 44,18 → 50,72 FPS, với khoảng 6.177 draw calls và 13,8 triệu tam giác gửi mỗi khung ở cả hai bản. Chênh lệch FPS chưa đủ để quy thành hiệu quả tối ưu. Gói data tải lạnh khoảng 903 MB.

Console còn shader URP không hỗ trợ, các cảnh báo decal Campus và một `NullReferenceException` ở lượt Lifecycle. Bài làm tiếp tục sau lỗi. Mức độ hoàn thành từng thao tác xem trong `Web_Interaction_Results.csv`. Chưa đánh dấu toàn bộ ứng dụng WEB PASS.

Đối chiếu 4.362 tệp khớp SHA-256, số đếm 24 scene khớp. Unity đã về Android và compile Editor thành công. Đợt này chưa build hoặc chạy APK trên Quest.

## Chạy kiểm thử tự động

```powershell
$env:UTTVR_HEADLESS='1'
$env:UTTVR_SHARED_MENU='1'
$env:UTTVR_LOGIN_ROLE_PROBE='1'
$env:UTTVR_UI_PROBES='1'
$env:UTTVR_CLASSROOM_PROBE='1'
$env:UTTVR_SHOWROOM_PROBE='1'
$env:UTTVR_PERFORMANCE='1'
$env:UTTVR_TEST_OUTPUT='Temp/PlatformAudit/WebLoginRetest'
$env:UTTVR_TEST_WORKBOOK='Documentation/PlatformCompatibility/WebTests/Evidence/InvalidAssetID_ImportProbe.xlsx'
node Documentation/PlatformCompatibility/WebTests/browser-test.mjs http://127.0.0.1:8774/
```

Bằng chứng chính ở `Evidence/After/Release/Evidence`, baseline ở `Evidence/Before/Evidence`, kết quả lưu sau reload ở `Evidence/After/PersistenceQueued/Evidence`. Script dùng hồ sơ Chrome riêng. Để thử lại lưu, đặt `UTTVR_SCENES=4`, `UTTVR_APPLY_PROBE=1`, `UTTVR_RELOAD_PROBE=1` và chọn `Valid_Sandbox_Update.xlsx` trong thư mục Evidence. Cần xem ảnh để xác nhận đích và trạng thái. JSON có `UNVERIFIED` cho thao tác đã gửi khi script chưa tự đánh giá ảnh.
