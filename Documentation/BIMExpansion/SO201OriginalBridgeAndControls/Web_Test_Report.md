# Kiểm tra Web

**PASS — Web Development được build và chạy thực tế qua Chrome.** Bản cuối tại `Builds/UTTVR_SO201_Original_Controls`, 1.381.247.293 byte, 0 lỗi và 1.049 cảnh báo build Unity. [Mở tại máy phát triển](http://127.0.0.1:8801/). PlayerSettings được đưa về đúng hash có trước tác vụ sau khi Unity tự ghi một mục preload trong lúc build.

**PASS — Chạy tàu thật qua cầu.** Tàu chạy bằng input ga của phép kiểm thử, bắt đầu 25 m, đi qua 3.875,001–4.035,292 m, tới 5.169,65 m rồi dừng tại 5.464,82 m. Khi đi qua cầu tốc độ ghi nhận 200 km/h, tương đương khoảng 2,9 giây để qua mặt cầu. Zone_018 nạp tại 3.107,47 m, không có khung thiếu cầu khi vượt, dỡ tại 4.717,89 m. Đã chụp cabin lúc vào/ra, camera nóc và bên phải ở giữa. Log xác nhận Visual có đúng 91 renderer và 36.099 tam giác khi đang nạp.

**PASS — BIM và zoom trong build kiểm tra.** Cây dùng 91 hồ sơ với GlobalId nguồn. Đã chọn dầm, trụ, đài cọc, bu lông C005; đọc dữ liệu IFC, lưu hồ sơ vận hành, quay lại đọc trong phiên kiểm thử và thử picker mở XLSX đã xuất của ray. Các thao tác ghi chỉ nằm trong profile Chrome dành riêng cho test. Không phát hiện ngoại lệ theo bộ lọc log của phép đo. Chrome còn cảnh báo tham chiếu script cũ trên một Decal Projector ở Campus.

**BLOCKED — Pointer Lock.** Xem [lỗi trình duyệt](Pointer_Lock_Web_Test.md). **VR NOT TESTED** trên Quest 3S.

## Số đo quan sát

| Hạng mục | Kết quả |
| --- | ---: |
| Khởi tạo Web lần kiểm tra đầu | 2,36 giây |
| Login → Campus, đến mốc PlayerSpawner | 1,77 giây |
| Chọn BIM → log Ready, kiểm tra đầu | 37,04 giây |
| Tìm và chọn dầm → Zone nạp và focus, kiểm tra đầu | 3,85 giây |
| Draw calls median, bốn mốc chạy qua cầu | 137–250 |
| Tam giác Unity median | 492.166–1.402.707 |
| Memory used Unity median | 601.535.554–608.989.200 byte |
| Unity frame time p95 và p99 trong bốn cửa sổ 26 mẫu | 9 ms |
| Main thread median | khoảng 8,3 ms |

So với build 1.486 cấu kiện trước, phép đo tài sản Visual ghi 129 → 91 renderer, 491.794 → 36.099 tam giác hình học. Các mốc chainage và nội dung camera khác nhau do vị trí và chiều dài cầu thay đổi, nên số đo frame không dùng để suy diễn hệ số tăng FPS hoặc GPU time. Chưa đo GPU timing riêng. Các thống kê runtime lấy từ Profiler Recorder và frame delta Unity.

[Log tàu](Web_Train_Evidence.json) · [Log BIM](Web_BIM_Evidence.json) · [Ảnh giữa cầu](Screenshots/Route_outside-middle.png) · [Bảng hiệu năng](Performance_Comparison.csv)

**PASS — Xác minh lại BIM trên build cuối.** Đã chọn đúng dầm từ cây và click lại trực tiếp trên model, quay về Zone_021, chọn và định vị C005, trở về mô-đun ray và đo zoom. [Log BIM build cuối](Web_BIM_Final_Evidence.json) · [Ảnh chọn trực tiếp](Screenshots/SO201_Model_Pick.png).
