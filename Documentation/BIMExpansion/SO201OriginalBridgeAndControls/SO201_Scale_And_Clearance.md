# Tỷ lệ và khoảng hở SO 201

## Lần cập nhật 2,4×

**PASS — Kiểm tra hình học trong sandbox.** Mặt cầu dài 213,72 m, rộng khoảng 18,08 m, lý trình 3.850,001–4.063,722 m. Độ lệch ngang của dây cung là 0,021 m. Không có mẫu thiếu mặt đỡ ở bước lấy mẫu 2 m và không phát hiện chi tiết nguồn đi vào bao kiểm tra đoàn tàu. Cầu có một instance Visual trong Zone_018 và một instance Detailed trong trình xem BIM.

## Số đo lịch sử ở 1,8×

**PASS — Kiểm tra hình học trong Unity.** Mặt `IfcPavement` nguồn đo theo trục hai trụ được 89,050 × 7,535 m. Tỷ lệ đồng nhất 1,8× cho mặt cầu dài khoảng 160,29 m, rộng 13,563 m. Hai tim ray giữ khoảng cách 6,400022 m. Vai tàu rộng nhất trong bộ bounds renderer đo khoảng 1,715 m từ tim. Kể cả lệch tim tuyến theo dây cung 0,011 m, bao ngang hai làn có bán rộng 4,926 m. Chênh từ bao này đến biên chiếu ngang mặt cầu khoảng 1,86 m mỗi bên. Đây là kiểm tra mô hình mô phỏng, không phải chứng nhận khổ giới hạn kỹ thuật.

Lý trình được chọn 3.875,001–4.035,292 m, tâm 3.955,147 m, nằm hoàn toàn trong Zone_018. Đáy ray thấp nhất của các mesh cắt qua khoảng lắp cầu là Y = 8,932 m. Đỉnh mặt đường đặt ở Y = 8,907 m. Dưới các dải ray, mặt đường có dốc và chênh cao đến 1,175 m. Bốn mẫu mép gối ray tại mỗi lát cắt cách nhau 2 m đều có mặt phủ. Nền ray riêng bù chênh cao; mặt cầu không được kéo phẳng. Kiểm tra biên tam giác phần phụ không phát hiện giao với hộp bao mô hình có thêm 0,1 m ngang. Bounds và kiểm tra lấy mẫu không thay thế quan sát khi chạy tàu.

| Scale | Độ lệch ngang tốt nhất | Chênh đáy ray/mặt cầu lớn nhất |
| --- | ---: | ---: |
| 1,8 | 0,011 m | 1,175 m |
| 2,0 | 0,015 m | 1,293 m |
| 2,2 | 0,018 m | 1,401 m |

Dùng 1,8× vì đủ chiều rộng mà chiều dài, độ cong dây cung và bổ sung nền ray đều nhỏ hơn. Số liệu sau scale có nhãn riêng trong UI và không ghi vào Property Sets IFC.
