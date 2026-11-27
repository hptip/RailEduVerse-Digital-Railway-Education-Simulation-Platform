# Kiểm thử Web

## Build

Unity 6000.3.0f1 đã tạo Development Build tại `Builds/UTTVR_SO201_Extended_Web` với 0 lỗi build, 1.062 cảnh báo và tổng dung lượng 1.385.501.772 byte. Bản này dùng cầu SO 201 214 m hiện hành và chứa cập nhật font cùng camera. Cầu năm nhịp dùng cho kiểm tra hình học trong Unity.

## Font trong Unity

- Atlas tĩnh có đủ 243 ký tự kiểm tra. Unity xác nhận khôi phục được fallback khi danh sách TMP bị xóa trong bộ nhớ.
- Một nhãn thử dùng font ASCII được dựng thành mesh trong Unity. Cả 23 ký tự ngoài ASCII trong chuỗi thử lấy đúng glyph từ atlas vừa được khôi phục.

## Phạm vi

Chrome đã tải Login, Classroom, UTTCampus, Showroom, YenTuHaLongXanh và QuanLyVongDoiTaiSan qua HTTP localhost. Ảnh chữ tiếng Việt nằm trong `Before_After_Screenshots/Web_Font_*.png`. Log thu được từ lượt này không có cảnh báo thiếu glyph Unicode của TMP. Một lượt kiểm tra BIM riêng đã đợi giao diện sẵn sàng rồi tìm được dầm `BRIDGE-SO201-08X3nXPrQ8kjF15zlU0Bh1` và mở tab Thông số.

Tàu đã chạy từ chainage 25,00 tới 5.169,63 m. Zone_018 được tải tại 3.102,43 m và gỡ tại 4.720,65 m. Probe không ghi nhận frame thiếu bản Visual lúc qua cầu. Camera vào được chế độ trên cao, bên phải và kết cấu rồi trở về cabin khi người lái vẫn ngồi. Ảnh tại các điểm đo và JSON ngắn nằm trong báo cáo.

`Performance_Comparison.csv` đối chiếu từng cửa sổ 26 frame của cùng tuyến 214 m. Camera mới thay đổi khung nhìn nên số triangles và draw calls nhìn thấy có thể tăng hoặc giảm. Mẫu dùng Chrome headless, các số liệu này không đại diện cho GPU hoặc FPS của Quest. Một số cảnh có log shader WebGL và decal thiếu script; các hạng mục đó nằm ngoài phép kiểm tra font.

VR NOT TESTED. Cầu mở rộng chưa đạt cổng hình học; số liệu Web thuộc cầu hiện hành.
