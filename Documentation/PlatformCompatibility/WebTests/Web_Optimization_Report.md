# Tối ưu UTTVR Web

## Phạm vi

Bản đối chiếu là `Builds/UTTVR_WebDemo_Release`. Bản ứng viên khởi động ở Login và tái sử dụng menu Canvas năm đích của VR. Thay đổi hiệu năng nằm trong điều khiển Web và server demo. Các số đo render phải được đọc cùng góc camera và trạng thái sa bàn trong ảnh trước/sau.

## Thay đổi

- Web bỏ dò XR Origin và thiết bị XR trong vòng cập nhật desktop. Nhánh XR gốc tiếp tục dò thiết bị và nhận nút Y.
- Menu Web dùng chung bộ tạo bảng, kích thước thiết kế, cỡ nút, font và bảng màu với VR. Canvas chung chạy `ScreenSpaceOverlay` trên Web nên chuột đến bảng mà không cần tìm camera mỗi frame. Quest vẫn dùng World Space với tracked raycaster và `XRRigFollowPanel`.
- HTTP demo trả `ETag` và `Last-Modified`. Khi tệp gzip hiện có còn trong cache và khớp ETag, lần xác thực lại dùng HTTP 304.
- Web dùng hàng đợi sync của SQLite `idbvfs` cho dữ liệu hồ sơ. Bản với Unity auto-sync từng mất giá trị sau reload. Khi tắt tiến trình ghi tự động bổ sung, hai lượt độc lập đọc lại đúng trường đã nhập sau 8,5 giây. Thiết lập nằm ở `index.html` và được đặt lại bởi `UttWebLinker` khi build. Không thay schema hoặc mã plugin.
- Build Web vẫn chứa dữ liệu scene và Zone đã đăng ký. Không đổi mô hình đóng gói, kết cấu tuyến hoặc cấu hình Android để giảm dung lượng.

## Phép đo

Chrome 157 ở 1280×720, WebGL2 qua ANGLE D3D11 và RTX 3060. Harness đo mẫu 5 giây mỗi cảnh, đếm callback khung hình có lệnh vẽ WebGL, và đếm lệnh/vật thể hình học được **gửi** qua các hàm WebGL draw chuẩn. Số tam giác bao gồm số lần instanced và render pass. Đây là số tam giác gửi, không phải số tam giác hiện trên màn hình sau culling/raster. CPU lấy `TaskDuration` của renderer chính trong CDP. Bộ nhớ lấy `usedJSHeapSize` của Chrome, không bao gồm toàn bộ bộ nhớ tiến trình hoặc GPU.

## Giới hạn an toàn

Baseline Showroom đo khoảng 44 FPS, 6.177 lệnh vẽ và 13,8 triệu tam giác gửi mỗi frame ở góc spawn. Cơ chế instancing hiện có vẫn được giữ. Trước khi can thiệp vào bước submit, cần xác định batch tĩnh nào được gửi khi hoàn toàn ngoài frustum, kể cả khi có camera phụ, shadow và reflection. Điều kiện thử tiếp là cùng camera, cùng nhóm đang hiển thị và draw count giảm, với ảnh tàu đứng/chạy và APK thử riêng trước khi áp dụng cho Quest.

## Kết quả

- `Web_Performance_Before_After.csv` ghi đủ 6 bối cảnh Login và năm scene sản phẩm. Showroom đo 44,18 → 50,72 FPS, còn số lệnh vẽ và số tam giác gửi gần như giữ nguyên. CPU nền và trình tự scene giữa hai lượt có khác, nên chênh lệch FPS này chưa chứng minh mức tăng do tối ưu. Một lượt trung gian ở cùng góc Showroom đo 43,06 FPS.
- Gói `data.gz` của bản mới là 903.387.748 byte. Đã xác nhận GET revalidation cùng ETag nhận 304 trên server local, sau phản hồi 200 có đúng `Content-Encoding: gzip`. Lần tải lạnh vẫn cần tải gói dữ liệu lớn.
- Chưa đo trực tiếp GPU time, Unity GC và thời gian tải riêng từng scene. Điểm nghẽn mất khung khi chạy tàu qua Zone chưa có mẫu vì bài thử chưa vào ghế lái. Không áp dụng cải tiến rendering, texture, shader hoặc hình học để ép FPS.
- Bản trung gian World Space thất bại khi bấm menu bằng chuột. Đã thay bằng trình bày overlay chỉ trên Web. Ảnh kiểu World Space và log thử nằm ở `Evidence/After/LoginMenu` và `Evidence/After/WorldSpaceKeyboard`.
