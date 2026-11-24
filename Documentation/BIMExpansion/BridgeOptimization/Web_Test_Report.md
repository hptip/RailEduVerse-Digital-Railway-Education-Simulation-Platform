# Kiểm thử Web Phase 2

## Build Web thật · PASS

| Build Development, Unity 6000.3.0f1 | Kết quả | Kích thước | Thời gian |
| --- | --- | ---: | ---: |
| `Builds/BridgePhase2_Baseline` | 0 lỗi, 14 cảnh báo | 1.380.000.472 byte | 7 phút 43,81 giây |
| `Builds/BridgePhase2_Trial` | 0 lỗi, 1.049 cảnh báo | 1.380.384.067 byte | 3 phút 15,26 giây |

Bản tối ưu dùng bản sao tạm của scene tuyến và Zone_018 để so sánh trước khi sửa Zone sản xuất. Scene tuyến thử chỉ đổi đường dẫn tải Zone_018 về bản sao có đúng tên scene, còn Build Settings của project giữ nguyên. Một lần build trung gian dùng lại cache scene gốc đã bị loại khỏi kết quả. Chỉ bản thử xác nhận runtime có 84 renderer được đưa vào CSV.

Đã chạy qua HTTP loopback trong Chrome 157, RTX 3060 qua ANGLE/D3D11, canvas 1280 × 720. Hai lượt đăng nhập qua Login, mở tuyến qua menu, ngồi ghế và chạy ga/phanh qua logic của tàu. Các cửa sổ Unity có 108–109 frame mỗi mốc. rAF trình duyệt ghi riêng khoảng 72 mẫu mỗi mốc, p95 8,4–8,5 ms và p99 8,5–8,6 ms trên baseline. Giá trị rAF và Unity cadence cho biết nhịp khung hình. Counter Main Thread của Unity cũng lưu riêng trong CSV. **GPU Profiler và Quest 3S chưa được đo.**

## Tàu, bridge streaming và hình ảnh · PASS

| Kiểm tra | Visual gốc | Visual tối ưu |
| --- | ---: | ---: |
| Lý trình khi nhận Zone_018 đã nạp | 2.955,73 m | 2.953,39 m |
| Lý trình nhận sự kiện Zone_018 đã unload | 5.177,10 m | 5.188,64 m |
| Frame thiếu cầu trong khoảng 3.650–4.770 m | 0 | 0 |
| Lý trình dừng sau khi qua cầu | 5.464,82 m | 5.464,82 m |

Đã kiểm tra ảnh cabin lúc vào/ra cầu, góc nóc, góc ngoài phải và vị trí quay về cabin. Ray, trụ và dây điện giữ vị trí trong ảnh đối chiếu. Mặt cầu không có khe hở mới nhìn thấy ở các điểm mẫu. Hai lượt chạy dùng Track_Right. Lái trên Track_Left **NOT TESTED**. Bước đối chiếu khởi động Web ghi nhận các cảnh báo vốn có về shader debug HDR, stencil và core copy. Các bài chạy hoàn tất.

| View | Baseline | Visual tối ưu |
| --- | --- | --- |
| Cabin vào | [Ảnh](Screenshots/Route_Baseline_cabin-entry.png) | [Ảnh](Screenshots/Route_Trial_cabin-entry.png) |
| Trên nóc | [Ảnh](Screenshots/Route_Baseline_overhead-middle.png) | [Ảnh](Screenshots/Route_Trial_overhead-middle.png) |
| Ngoài phải | [Ảnh](Screenshots/Route_Baseline_outside-middle.png) | [Ảnh](Screenshots/Route_Trial_outside-middle.png) |
| Cabin giữa | [Ảnh](Screenshots/Route_Baseline_cabin-middle.png) | [Ảnh](Screenshots/Route_Trial_cabin-middle.png) |
| Cabin ra | [Ảnh](Screenshots/Route_Baseline_cabin-exit.png) | [Ảnh](Screenshots/Route_Trial_cabin-exit.png) |

## Quản lý vòng đời · PASS

- 17 Zone được đối chiếu trong dữ liệu scene. Zone_018 tải ba Zone viewer 017/018/019. Danh sách renderer trong viewer khớp 532, bao gồm 420 cấu kiện thuộc đúng bản Detailed. Zone_021 thu hồi cả ba và chọn được bu lông `Z021-TR-S001-B001-C005`.
- Đã chọn theo cây, nhấp lấy đúng một cấu kiện trực tiếp trên 3D, thử kéo từ panel, orbit/pan/zoom, và mở cả ba tab. Đã quan sát đủ sáu nút vòng đời. Nhãn và tooltip tiếng Việt trong BIM hiển thị đủ dấu trong các ảnh kiểm tra. Menu đổi scene dùng font riêng và có một số chữ mất dấu sẵn từ trước Phase 2.
- Đã ghi trạng thái Vận hành của lan can `YTH-BRIDGE-MAIN-RAIL-A-04190002`, mở lại Web, xác nhận UI và SQLite đều đọc được `PHASE2-TEST`, `integrity_check=ok`. Dữ liệu thử chỉ tồn tại trong profile Chrome thử nghiệm. Bài kiểm tra lưu/mở lại trực tiếp dùng một trong sáu giai đoạn.

[Kết quả định vị](BIM_Camera_Fix_Report.md) · [Thông tin Vận hành sau khi mở lại](Screenshots/BIM_Operation_Reopened.png) · [Zone_021 và bu lông C005](Screenshots/BIM_Zone_021_C005.png)

**VR NOT TESTED.**
