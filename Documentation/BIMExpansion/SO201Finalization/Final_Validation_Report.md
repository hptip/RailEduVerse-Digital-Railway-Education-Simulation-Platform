# Cầu SO 201 bảy nhịp

## Kết quả trong Unity

| Nhóm | Trạng thái | Bằng chứng |
| --- | --- | --- |
| Lắp ráp và hình học | PASS | Mẫu 2 nhịp đạt 44 phép kiểm, mẫu 5 nhịp đạt 86 và ứng viên 7 nhịp đạt 118. Các hàng OBSERVED ghi số đo của những cặp chồng chiếu để truy vết. |
| Đặt trên tuyến | PASS | 540,121 m từ 3.694,001 tới 4.234,122 m. Có 7 nhịp, 6 trụ, 2 mố và 62 cọc ở tỷ lệ 2,4×. |
| Ray, nền và nước | PASS trong miền lấy mẫu | 96 phép kiểm đặt tuyến đạt, gồm độ phủ bản mặt, độ nhô kết cấu lên ray, khe nền đỡ trên phạm vi 1.120 m cũ, tiếp xúc đài và đất, độ phủ nước và cọc chôn trong nền. Khe nền lớn nhất 35 mm. |
| Hai scene và dọn cầu cũ | PASS | Tuyến dùng Visual trong Zone_018 và giữ các Zone_017–018 quanh cầu. Scene BIM dùng Detailed. Đối chiếu dependency không thấy cầu 214 m hoặc scene thử nghiệm trong bộ asset sản phẩm. |
| BIM | PASS trong Unity | Bản Detailed và Visual khớp 196 mesh và transform. MeshCollider dùng đúng mesh đã hiển thị. Có 196 ID duy nhất, gồm 91 ID gốc và 105 occurrence mới. Dữ liệu mô phỏng chỉ hiện khi hồ sơ người dùng chưa có dữ liệu. |

Đo hình học dành cho mô hình đào tạo UTTVR. Số đo là phép thử mesh trong miền lấy mẫu, không phải kiểm định kết cấu công trình thật.

## Kiểm thử Web

- **Build PASS**. Unity 6000.3.0f1 tạo bản tại `Builds/UTTVR_SO201_SevenSpan_Web` với 0 lỗi và 1.062 cảnh báo. Chrome chạy qua HTTP tại `http://127.0.0.1:8803/`.
- **Chạy tàu và streaming PASS**. Qua Login và menu, tàu chạy từ lý trình 25,00 tới 5.169,72 m trước khi giảm tốc. Zone_017 và Zone_018 được tải, bộ đo nhận 196 renderer và 74.197 tam giác của cầu mới. Số frame thiếu cầu trong phạm vi kiểm tra là 0. Zone_018 được tải tại 2.995,50 m và gỡ tại 4.717,01 m.
- **Camera PASS trong lượt Web**. Xác nhận cabin, trên cao, bên phải, camera kết cấu và quay về cabin. Tại 4.143,04 m, camera kết cấu ghi nhận 42 renderer cầu trong khung. Kéo xoay và cuộn zoom có ảnh đối chiếu. Khi trình duyệt không cấp khóa chuột, thông tin từ lượt chạy ghi nhận cơ chế bắt chuột dự phòng.
- **BIM Web PASS**. Viewer tải Zone_017 và Zone_018, tìm và chọn được `BRIDGE-SO201-EXTENDED-S04-00TgNFCH9HGoplDgAwRwDO`. Tab Thông số và Vòng đời hiển thị mô hình mới, nhãn mô phỏng và ID nguồn. Lượt Web này không ghi dữ liệu người dùng và không chạy lại thử nhập/xuất Excel.
- **Mẫu hiệu năng**. Cửa sổ 26 frame ở camera kết cấu có thời gian khung hình trung vị 8 ms, p95 9 ms và p99 9 ms, 201 draw calls trung vị. Đây là số đo cục bộ Chrome headless bằng probe Unity. Không suy ra tốc độ Quest từ các số này.
- **VR NOT TESTED**.
