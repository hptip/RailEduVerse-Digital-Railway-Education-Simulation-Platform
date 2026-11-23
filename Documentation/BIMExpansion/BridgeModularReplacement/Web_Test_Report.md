# Kiểm thử Web cầu mô-đun

## Build và phạm vi đo

Unity 6000.3.0f1 xuất bản Development vào `Builds/BridgeModular_Web`. Kết quả **0 lỗi**, **1.062 cảnh báo**, **1.379.533.958 byte**, thời gian build **21 phút 30,88 giây**. Danh sách scene giữ nguyên và Login đứng đầu. Bài đi tàu dùng tài khoản demo trong Login, vào Campus rồi mở tuyến qua menu năm scene. Probe thử riêng kích hoạt bằng URL `bridgeDrive=1`, ngồi ghế bằng API hiện có và giữ ga/phanh qua điều khiển hiện có. Lý trình chỉ được đọc để ghi log.

Bản Web cuối, sau khi thay font TMP tĩnh riêng trong scene BIM, đã build lại vào cùng đường dẫn và thành công với **0 lỗi**, **1.049 cảnh báo**, **1.379.879.880 byte** trong **2 phút 59,49 giây**. Bài chạy qua toàn cầu và các số đo của tàu lấy từ bản đầu. Bản cuối được mở lại qua HTTP để thử Zone_018/021, hiển thị tiếng Việt và đọc lại dữ liệu đã lưu. Môi trường kiểm tra cuối là Chrome 157 trên Windows, RTX 3060 qua ANGLE/D3D11, canvas 1280 × 720.

## Tàu qua cầu thật trong WebGL

| Kiểm tra | Kết quả |
| --- | --- |
| Chạy đủ phạm vi | Tàu xuất phát tại **25,00 m**, qua đoạn cầu **3.650,00–4.770,00 m** ở **200 km/h**, chạy ga đến khoảng 5.169,72 m và dừng ở **5.464,83 m**. |
| Streaming | Zone_017, 018 và 019 nạp trước vùng cầu. Log ghi **0 frame thiếu cầu** trong đoạn đo. Cầu vẫn có đủ 420 renderer ở mốc 4.800 m. Zone_018 và hai Zone liên quan unload sau khi toa đã đi xa. Log kết thúc `bridgeLoaded=False`. |
| Camera | Cabin ở đoạn vào khoảng 3.700 m, camera trên nóc từ khoảng 3.910 m, camera ngoài phải từ 4.230 m, rồi về cabin trước đoạn ra. Ảnh kiểm tra cho thấy ray đôi, deck và OCL ở đúng ngữ cảnh tuyến. |
| Hai đường ray | Web chạy tàu trên Track_Right theo cấu hình hiện hành. Cả hai đường được đối chiếu bằng 23 cặp microchunk và bao toa lấy mẫu trong phép kiểm tra hình học. |
| Renderer và draw calls | Cầu giữ **420 renderer bật** qua các điểm đo có Zone_018. Draw calls toàn frame **131–538** trong các điểm lấy mẫu, tùy camera và culling. |
| Frame time và memory | 612 khoảng rAF trong khoảng 5,1 giây ở góc ngoài trên cầu, median **8,30 ms**, p95/p99 **8,50 ms**, max **8,90 ms**. Unity Total Used Memory ở các mốc chạy trên và quanh cầu khoảng **605–625 MB**. Đây là số đo PC, Quest và GPU memory chưa được đo. |

[Cabin vào cầu](Screenshots/Route_Cab_Entry.png) · [Camera ngoài trên nóc](Screenshots/Route_Exterior_Overhead.png) · [Góc ngoài bên phải](Screenshots/Route_Exterior_Right.png) · [Cabin ra cầu](Screenshots/Route_Cab_Exit.png)

## Cấu kiện BIM trên WebGL

- Qua Login và menu, Zone_018 nạp cùng Zone_017/019, với 532 đối tượng hiển thị được ghi bởi Zone Viewer. Prefab chính có 420 cấu kiện liên kết ID; đã thử chọn lan can `YTH-BRIDGE-MAIN-RAIL-A-04190002` từ cây và nhấp trên mô hình 3D. Log nhận đúng một cấu kiện từ thao tác nhấp.
- Đã nhập và lưu một thông số, một ghi chú kỹ thuật và trạng thái giai đoạn Vận hành. Sau khi mở lại ứng dụng ở bản Web cuối, cả ba giá trị hiện đúng trên UI và bản đọc từ SQLite trong profile Chrome thử nghiệm trả `integrity_check=ok`. Thao tác lưu trực tiếp được thử cho một trong sáu giai đoạn.
- Khi về Zone_021, Zone Viewer thu hồi ba Zone thử và chọn lại được cấu kiện ray `Z021-TR-S001-B001-C005`. Dữ liệu thử nằm trong profile Chrome riêng.
- Trên bản Web đầu, màn BIM sẵn sàng sau **40,15 giây** trong lần tải thử. 516 khoảng rAF trong khoảng 5,1 giây có p95 **8,4 ms**, p99 **8,5 ms**, max **291,6 ms**. Chrome ghi khoảng **247 MB JS heap** ở mốc kiểm tra. JS heap chỉ phản ánh phần bộ nhớ JavaScript.

[Cây cấu kiện cầu](Screenshots/Final_BIM_Tree.png) · [Thông số và highlight](Screenshots/Final_BIM_Properties.png) · [Giá trị kỹ thuật sau khi mở lại](Screenshots/Final_BIM_Technical_Reopened.png) · [Vận hành sau khi mở lại](Screenshots/Final_BIM_Operation_Reopened.png) · [Chọn lại cấu kiện ray Zone_021](Screenshots/Final_BIM_Z021_C005.png)

**VR NOT TESTED.**
