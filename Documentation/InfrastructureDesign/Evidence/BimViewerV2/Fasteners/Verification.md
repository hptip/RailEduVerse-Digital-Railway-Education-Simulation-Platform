# Kiểm tra hiển thị liên kết ray Z021

Module kiểm tra là `Z021-TR-S001-B001`. Phép thử Editor dựng một Preview Scene bằng 99 mesh canonical của catalog, với đúng vị trí và hướng từ catalog. Các mô-đun lân cận cung cấp bối cảnh cho hai đường ray. Mỗi ảnh dùng cùng điểm ngắm và hướng camera ở khoảng cách 3, 22 hoặc 105 m. Các phép thử tắt từng renderer khôi phục trạng thái ngay sau lúc chụp.

## Hình học và vật liệu

| Cặp cấu kiện | Diện tích chiếu gần đồng phẳng trong ngưỡng 2 mm |
| --- | ---: |
| Bản đế C030 và C031 | 0,273608 m² |
| Bản đế C062 và C063 | 0,273608 m² |
| Bản đế C094 và C095 | 0,273608 m² |
| Tấm đệm C033 và gối đỡ C024 | 0,107823 m² |
| Tấm đệm C032 và gối đỡ C023 | 0,107471 m² |

Audit ghi nhận 84 cặp bề mặt gần đồng phẳng cùng chiều pháp tuyến và 144 hàng tiếp xúc ngược chiều pháp tuyến cần đọc theo ngữ cảnh lắp ráp. Không phát hiện mesh hoặc instance trùng hoàn toàn trong mẫu này. Diện tích trong bảng là diện tích giao theo phép chiếu, vì vậy riêng chỉ số này chưa chứng minh có Z-fighting ở vùng người dùng nhìn thấy. Vật liệu cấu kiện mẫu là Opaque, queue 2000, ghi depth và loại mặt sau. Shader ray tuyến ghi depth và pass màu không lấy mẫu shadow map.

## Đối chứng trong Unity Editor

| Thay đổi riêng trong khung 960 × 540 | Pixel đổi ở 3 m | Pixel đổi ở 22 m |
| --- | ---: | ---: |
| Tắt C030 hoặc C031 | 0 | 0 |
| Tắt C033 | 0 | 0 |
| Tắt C017 | 11 | 0 |
| Tắt C019 | 1 | 0 |
| Tắt C024 | 5.237 | 9 |
| Tắt C025 | 86 | 1 |
| Tắt bóng đổ của camera | 23.981 | 566 |

Một pixel được tính nếu tổng chênh lệch ba kênh RGB vượt 18/255 đơn vị kênh. Đây là số pixel đổi giữa hai trạng thái, gồm cả các vùng bị khuất hiện ra hoặc mất bóng hợp lệ. Viền bóng răng cưa cạnh ray ở 3 m thay đổi rõ khi tắt bóng. Hiện tượng nhiễu mảnh ở mép ray và các đường nối vẫn còn trong ảnh không bóng. Kết quả chưa chứng minh gối đỡ C024 có thể ẩn ở mọi góc hoặc bóng gây ra toàn bộ nhiễu.

Ảnh chứng cứ Editor ở cùng hướng camera gồm [3 m](3_Full.png), [22 m](22_Full.png), [105 m](105_Full.png), [tắt C024](3_Without_C024.png) và [tắt bóng](3_NoShadows.png). [Kết quả kiểm tra giao bề mặt](Z021_Surface_Overlap.csv).

## MSAA trong Unity Editor

Đối chứng thứ hai dùng camera Game và cùng đường resolve màu cho cả hai cấu hình. URP chỉ thực hiện MSAA khi pipeline chạy với MSAA trên 1 và camera cho phép. Trong phiên kiểm tra, URP được chuyển tạm sang 4x trong bộ nhớ, đích render lần lượt dùng 1 và 4 mẫu. Thiết lập được trả lại sau phép thử.

| Khoảng cách | Ảnh 1 mẫu | Ảnh 4 mẫu | Pixel đổi vượt ngưỡng |
| --- | --- | --- | ---: |
| 3 m | [1×](3_TestMSAA1.png) | [4×](3_TestMSAA4.png) | 6.848 |
| 22 m | [1×](22_TestMSAA1.png) | [4×](22_TestMSAA4.png) | 3.479 |
| 105 m | [1×](105_TestMSAA1.png) | [4×](105_TestMSAA4.png) | 560 |

Mép ray và đường bao chi tiết ở 22 m mượt hơn với 4 mẫu. Răng cưa của mép bóng và một số đường mảnh giữa các cấu kiện vẫn nhìn thấy. Số pixel chỉ xác nhận ảnh thay đổi ở các vùng này, chưa đo được mức giảm nhấp nháy theo thời gian hay chi phí GPU thực tế trên thiết bị người dùng.

Chuyển Visual/Detailed hiện có dựa trên khoảng cách và kích thước cụm, có ngưỡng vào/ra riêng để tránh đổi liên tục. Ray trên tuyến hiện dùng mesh MicroChunk ghép sẵn; phép tắt từng bản đế được thực hiện trong bản Detailed ở Z021. Các collider và chế độ kiểm tra vẫn dùng dữ liệu cấu kiện gốc.

## Đối chứng trên WebGL

Chrome chạy bản Development WebGL. Bộ kiểm thử định vị trực tiếp mô-đun Z021 qua controller của Scene BIM rồi khóa điểm ngắm. Logic này chỉ hoạt động khi có cờ chẩn đoán. Mỗi phép A/B giữ nguyên tư thế camera, dùng cùng độ phân giải 1.280 × 720. Số điểm ảnh dưới đây đo trong vùng mô hình bên phải.

| Khoảng cách | MSAA 1× so với 4× | Near/Far cũ so với thích ứng | Tắt bóng khi dùng clipping thích ứng |
| --- | ---: | ---: | ---: |
| 3 m | 3.354 | 2.700 | 1.417 |
| 22 m | 8.167 | 2.971 | 28 |
| 105 m | 11.341 | 3.730 | 0 |

MSAA 4× làm mềm các cạnh hình học và đường bao ray trong các ảnh đối chứng. Số pixel gồm cả địa hình trong khung; số liệu này không phải thước đo giảm nhấp nháy. Tắt C024 thay đổi trực tiếp 2.131 pixel ở 3 m và 27 pixel ở 22 m; C030, C031, C033 đều không làm đổi ảnh ở hai khoảng cách đã thử. Kết quả củng cố việc giữ nguyên C024 và các chi tiết lắp ráp.

Trong khung Z021, clipping thích ứng giữ near ở 0,01 m để bảo toàn bounds bối cảnh và hạ far từ 3.200 m xuống 1.553,86 / 1.536,10 / 1.464,29 m. Phép thử nâng riêng near dùng lần lượt 0,15 / 0,264 / 1,26 m. Tăng near toàn cục sẽ bỏ qua điều kiện giữ vật thể tiền cảnh này.

Ảnh WebGL ở 3 m gồm [1×](BIM_3_MSAA1.png) và [4×](BIM_3_MSAA4.png), ở 22 m gồm [1×](BIM_22_MSAA1.png) và [4×](BIM_22_MSAA4.png), ở 105 m gồm [1×](BIM_105_MSAA1.png) và [4×](BIM_105_MSAA4.png).

Trên YenTuHaLongXanh, camera đo ở chainage 540 m với ảnh 6, 22 và 105 m. Chuyển 1× sang 4× làm đổi tương ứng 37.276, 38.076 và 24.311 pixel trên toàn ảnh. Tắt bóng khi dùng clipping thích ứng cho ảnh giống hệt ở cả ba mẫu. Bộ đo dùng toàn bộ bounds tải sẵn nên có thể báo far khoảng 9,5 km; camera ngoài tàu trong sản phẩm vẫn áp trần từ thiết lập camera ban đầu. [Tuyến ở 22 m, 1×](Route_22_MSAA1.png), [tuyến ở 22 m, 4×](Route_22_MSAA4.png).

Bản Web kiểm thử báo 0 lỗi build và 17 cảnh báo. Chrome báo OpenGLES3, không dùng reversed-Z và hỗ trợ 4 mẫu trước khi chạy phép đối chứng. MSAA 4× chỉ bật bằng công cụ chẩn đoán, chưa đưa thành mặc định vì chưa đo chi phí GPU ngoài máy kiểm thử. Bài đối chứng dùng camera cố định theo từng khoảng cách; kết quả chạy tàu liên tục nằm trong báo cáo BIM Viewer V2.

## Tính toàn vẹn BIM

Đối chiếu SHA-256 trước và sau khớp đủ 101 đầu vào, gồm 99 mesh cấu kiện, catalog và Scene quản lý BIM. Catalog lưu identity, vị trí/hướng đặt và quan hệ module. Không ẩn thêm các bản đế hoặc tấm đệm trong chế độ thường vì phép thử chưa đủ để chứng minh chúng luôn bị che kín. Các chế độ kiểm tra tiếp tục sử dụng logic khôi phục Detailed hiện có. Không thay đổi asset đồ họa mặc định trong phép thử này.
