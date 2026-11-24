# Camera trình xem BIM cầu chính

Panel trái và camera tương tác hiện có được tái sử dụng. Kiểu focus thay đổi theo nút cây đã chọn:

| Nút cây | Điểm ngắm và khung nhìn |
| --- | --- |
| Đoạn tuyến 09 / Zone_018 | Fit toàn bộ cầu với lề rộng hơn để thấy mặt nước và hai đầu tuyến. |
| Cầu chính | Góc xiên thấp từ bên ngoài, fit mặt cầu và 28 cụm trụ theo hướng trục cầu. |
| Nhịp / Cụm trụ | Fit bounds của các renderer được liên kết tới nhóm trong catalog, không nạp dữ liệu vòng đời cho nhóm. |
| Bản mặt / Dầm / Trụ / Lan can | Fit bounds của renderer gắn đúng AssetID. Khi chọn từ mô hình, cây và highlight đồng bộ. |

Camera chiếu các góc bounds cục bộ của renderer sang hệ tọa độ khung nhìn. Khoảng cách fit dùng FOV và phần viewport Web còn lại sau panel trái. Khi zoom hoặc orbit sát, camera giữ mặt phẳng near cách điểm gần nhất của tập bounds. `Định vị` dùng lại preset của lựa chọn, `Toàn cảnh` trở về overview Zone hiện hành. Hỗ trợ orbit, pan và zoom bằng chuột bắt đầu trong viewport, còn thao tác trên panel ưu tiên UI. Góc lái tàu và pose XR giữ nguyên.

Danh mục trong Scene có **17 Zone**, từ Zone_010 đến Zone_026. Đối chiếu đường dẫn tìm thấy đủ 16 prefab viewer bên ngoài Zone_021. Dữ liệu Zone_021 tiếp tục dùng môi trường chi tiết có sẵn.

## Kiểm thử WebGL · PASS

| Lựa chọn đã thử trên cây | Renderer dùng để fit | Khoảng cách camera |
| --- | ---: | ---: |
| Zone_018 | 420 | 1.499,20 m |
| Cầu chính | 420 | 1.288,37 m |
| Nhịp 01 | 5 | 47,08 m |
| Cụm trụ 01 | 5 | 42,27 m |
| Dầm A / thân trụ A / bản mặt / lan can | 1 mỗi cấu kiện | 28,03–46,94 m |

Kéo từ panel trái sang viewport giữ nguyên ảnh vùng 3D. Kéo chuột trong viewport thay đổi góc hoặc vị trí, cuộn chuột thay đổi mức zoom. Đã dùng nút `Định vị` để đưa lan can về khung mặc định. Sau khi bỏ chọn, nhấp vào lan can trong ảnh 3D ghi đúng một highlight và chọn lại cùng AssetID trong cây. Tại trụ sát mố, địa hình có thể che phần chôn của trụ; ảnh trụ giữa thung lũng cho thấy toàn bộ phần thân nằm trên mặt nước.

[Góc xiên bên cầu](Screenshots/BIM_Bridge_Side.png) · [Góc trên](Screenshots/BIM_Bridge_Top.png) · [Nhịp 01](Screenshots/BIM_Span.png) · [Thân trụ giữa cầu](Screenshots/BIM_Pier.png) · [Lan can](Screenshots/BIM_Railing.png)
