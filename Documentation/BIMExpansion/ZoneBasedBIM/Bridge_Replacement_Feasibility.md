# Khả thi thay thế Bridge_Main

**BLOCKED — geometry.** SO 201 là cầu đường bộ khoảng 89,05 m, mặt đường khoảng 7,54 m. Nó thiếu ray đôi và thiếu định vị theo tuyến. Bridge_Main dài 1.120 m. Quét nguồn đã thấy DAE SO 201, ba IFC ray và một FBX dầm bê tông rời. Chưa có cầu BIM đường sắt hoàn chỉnh khớp hình học đã đo.

## Hình học tuyến hiện hành

Đo bằng API Spline của đúng project và đúng quy ước `chainage / CalculateLength()` đang dùng trong TrainSplineMover. Đoạn mẫu 57 mặt cắt cách nhau 20 m. Chiều dài spline trả **7.999,983 m**.

| Mốc | Lý trình cục bộ | Tim Track_Right theo Unity world (X, Y, Z) | Tim Track_Left suy từ khoảng cách tim | Hướng tiếp tuyến |
| --- | ---: | --- | --- | --- |
| Đầu cầu | 3.650,0015 m | (8793,605; 9,33166; −1144,249) | (8795,113; 9,33166; −1138,029) | (0,971843; 0; −0,235630) |
| Cuối cầu | 4.770,0015 m | (9881,828; 9,33166; −1409,121) | (9883,367; 9,33166; −1402,909) | (0,970636; 0; −0,240553) |

- Spline tham chiếu có Y 9,33166 m xuyên vùng đã lấy mẫu. Offset của pivot toa trước +0,35000 m. Toa sau cộng thêm −0,29859 m, cách dọc 24,092 m. TIM tham chiếu khác mặt đỉnh ray và khác mặt deck.
- Hai tim cách **6,400022 m**. Deck generator rộng **14 m**, dày **1,1 m**, đặt tâm ngang vào giữa hai tim. Generator dùng 56 đoạn deck 20 m, cộng 0,1 m giao ở mối nối, và 28 vị trí trụ đôi bắt đầu tại chainage khoảng 3.670 m rồi cách 40 m. Các số này mô tả hình học Unity, chưa xác nhận nhịp kết cấu kỹ thuật.
- Renderer của cầu gộp có bounds khoảng **1.091,666 × 28,427 × 278,497 m** theo các trục world, tính cả trụ và lan can. Mốc transform (9337,394; 9,33166; −1278,019), xoay identity, scale 1. Hình học nước là mesh riêng. Microchunk registry có các cặp ray 50 m cắt qua toàn bộ phạm vi. OCL gồm dây theo Zone và cột theo batch. Đoạn cổng 3.000–3.500 m nằm trước cầu.
- Chưa có khổ giới hạn động đã duyệt, profile ray chuẩn, độ võng/contact-wire profile và kích thước từ tài liệu kỹ thuật. Việc đo clearance của model thay thế cần so các tình huống dọc cả hai ray, mặt cắt đầu/cuối, tay vịn, bản mặt và OCL.

## Phương án cho model thay thế

1. Xuất các mốc và tiếp tuyến bằng CSV để dựng đường tim và mặt cắt trong Unity world hoặc hệ local có transform được duyệt. Gắn cùng một phép căn chỉnh cho hai prefab. Lấy mốc đặt từ cầu đang chạy thay vì suy từ tên Zone.
2. Nguồn BIM mới cần cấu kiện riêng cho nhịp, trụ, mố, dầm, bản mặt và các nhóm có mesh thật. Mỗi cấu kiện có GlobalId ổn định. Registry cầu giữ `Bridge_Main` làm khóa hạ tầng, chỉ cấp AssetID khi chủ dữ liệu duyệt mapping và namespace.
3. Dùng bản Detailed có collider/identity trong viewer. Bản Visual cùng nguồn mesh/material đặt đúng pose trong sandbox. Hiệu chỉnh cửa lên/xuống cầu bằng mặt cắt và tập đường ray hiện hành. Đo khe hở, bounds, chồng lấn, vật liệu, cảnh quan, OCL và trạng thái kích hoạt tại Zone_017–019.
4. Kiểm thử tàu di chuyển qua hai đầu cầu, tải/unload, Quest và Web trực tiếp trước khi đề xuất thay production. Giữ cầu cũ làm baseline và đường quay lại.

[Đối chiếu IFC SO 201](../BridgePilot/Railway_Compatibility_Report.md) · [Bản sandbox](Bridge_Replacement_Sandbox_Report.md)

[Mặt cắt tim ray và tiếp tuyến 20 m](Bridge_Spline_Sections.csv). Bề rộng deck và khoảng cách tim cho biên hình học danh nghĩa khoảng 3,80 m từ mỗi tim ray ngoài đến mép deck. Cần kiểm tra khổ động và OCL riêng trước tích hợp.

Ba mesh dây tiếp xúc `ContactWire_Zone_017`, `018` và `019` có root và transform dây identity. Bao cao độ world của dây trong ba Zone là khoảng **14,590–14,854 m**, tương ứng **+5,258–5,522 m** so với tim spline tham chiếu. Đây là envelope hình học của cả mesh dây, chưa thay cho khoảng hở động của toa hoặc độ cao tiếp xúc theo từng mặt cắt.
