# Kiểm thử Web chính thức

Unity 6000.3.0f1 đã build các scene chính thức ở `Builds/SO201_Production_Web` với **0 lỗi**, 1.062 cảnh báo, 1.391.112.051 byte và thời gian 8 phút 33,77 giây. Chạy HTTP cục bộ trên Chrome 157 / RTX 3060, canvas 1280 × 720. Bản kiểm thử dùng Development.

## Chạy tàu · PASS

Đăng nhập trong Login, vào Campus rồi chuyển qua menu tuyến. Probe ngồi ghế và giữ ga/phanh qua API hiện có. Tàu đi qua hết cầu ở 200 km/h trên Track_Right. Zone_018 nạp tại **2.954,39 m**, cầu hiện đủ **129 renderer, 491.794 tam giác** và có **0 frame thiếu cầu** trong phạm vi 3.650–4.770 m. Lượt chạy đổi cabin → nóc → ngoài phải → cabin. Zone_018 unload tại **5.175,21 m**, tàu dừng ở **5.464,76 m**. Cả hai tim được kiểm bằng hình học. Lượt lái thực tế trên Track_Left chưa thực hiện, điều khiển hiện hành đi Track_Right.

[Cabin vào cầu](Screenshots/Train_SO201_cabin-entry.png) · [Từ nóc](Screenshots/Train_SO201_overhead-middle.png) · [Ngoài bên phải](Screenshots/Train_SO201_outside-middle.png) · [Cabin ra cầu](Screenshots/Train_SO201_cabin-exit.png) · [Cầu cũ từ ngoài bên phải](Screenshots/Train_Legacy_outside-middle.png)

## Chi phí theo mốc

| Vị trí | Draw calls mô-đun → SO 201 | Unity memory mô-đun → SO 201 |
| --- | ---: | ---: |
| Cabin vào cầu | 239 → 713 | 599,9 → 659,7 MB |
| Trên nóc | 96 → 213 | 597,5 → 654,8 MB |
| Ngoài bên phải | 228 → 474 | 606,0 → 664,8 MB |
| Cabin giữa | 159 → 285 | 606,5 → 663,8 MB |
| Cabin ra | 181 → 230 | 616,5 → 673,4 MB |

Các số là trung vị theo cửa sổ quanh từng lý trình, bản mô-đun dùng kết quả trước tích hợp cùng camera và tốc độ. Bản chính thức đo Unity frame cadence p95/p99 **9/9 ms** ở năm mốc. Chi phí render tăng, lượt chạy PC hoàn tất và giữ nhịp khung hình đã đo. Thời gian từ sự kiện scene đã nạp đến lúc thấy cầu active là 2 ms; chưa tách riêng thời gian đọc/giải nén hay GPU time. [CSV hiệu năng](Performance_Comparison.csv) · [Log lượt chạy](Train_Web_Evidence.json)

## BIM Viewer · PASS

- Mở Zone_018 nạp đúng Zone_017/018/019. Log ghi 1.598 renderer cảnh quan và cầu, với 1.486 renderer của đúng bản Detailed. Cây chỉ giữ **19 hàng UI** trong phép đo mở nhánh cầu, khi có 155 mục logic mở.
- Đã chọn bằng cây và xác nhận focus/highlight cho dầm `YTH-SO201-B32-GIRDER-1`, tấm mặt IFC `YTH-SO201-B32-SLAB`, sàn mô phỏng `YTH-SO201-B32-CAP`, đài móng `YTH-SO201-P32-L-FOUND` và thân trụ `YTH-SO201-P32-L-STEM`. Nhấp trực tiếp thân trụ trên mô hình chọn lại cùng ID và đúng một highlight. Tab Thông số hiển thị SourceGlobalId, lớp và tên IFC.
- Đã mở cả sáu giai đoạn. Đã lưu thử trạng thái và ghi chú Vận hành của thân trụ trong `BridgeYTH_SO201Route.db` ở profile Chrome riêng. SQLite sau khi mở lại trả đúng AssetID, giai đoạn và hai giá trị, cùng `integrity_check=ok`. Lượt đầu với khoảng chờ 1,9 giây chưa đọc lại được; hai lượt chờ lần lượt 5 và 8,5 giây đã đọc lại. Chưa xác nhận khả năng đóng trang ngay sau khi lưu.
- Sau khi đổi Zone_021, log ghi ba Zone của cầu được thu hồi. Đã chọn và highlight bu lông `Z021-TR-S001-B001-C005`.

[Cây cầu](Screenshots/BIM_Tree.png) · [Dầm và SourceGlobalId](Screenshots/BIM_Girder_IFC.png) · [Đài móng](Screenshots/BIM_Foundation.png) · [Nhấp thân trụ trên 3D](Screenshots/BIM_Model_Click.png) · [Vận hành sau khi mở lại](Screenshots/BIM_Operation_Reopened.png) · [Bu lông C005](Screenshots/BIM_Zone_021_C005.png) · [Log BIM](BIM_Web_Evidence.json)

**VR NOT TESTED.**
