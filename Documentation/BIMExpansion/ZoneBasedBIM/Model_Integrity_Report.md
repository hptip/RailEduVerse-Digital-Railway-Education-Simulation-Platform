# Bảo toàn model và dữ liệu

## Kiểm tra ở lớp tài sản

- Danh mục bảo vệ gồm **76 tệp**. Có toàn bộ 58 Zone trên đĩa, scene YenTuHaLongXanh, Login, Showroom, dữ liệu bản đồ ray, mesh Bridge_Main, mã tàu và streamer, SQLite ray nguồn và SQLite Editor, DAE/import metadata, Build Settings và gói dự án.
- Đã đối chiếu **288 instance mesh nguồn** ở 16 prefab viewer. So từng cặp tham chiếu mesh, mảng material có thứ tự, vị trí world sau phép tịnh tiến chung, rotation và scale. Các cặp ray lấy trực tiếp từ MicroChunkData. Quét YAML prefab không thấy MonoBehaviour, camera, light hoặc collider.
- Scene quản lý giữ mô hình và pool Z021. Thay đổi serialized gồm bỏ instance demo SO 201, thêm Zone Viewer và dữ liệu registry, giữ liên kết mô hình ray hiện hữu. Asset nguồn SO 201 và lịch sử pilot còn nguyên.
- Bản Web thử dùng danh sách 24 scene từ profile hiện hành và Login đứng đầu. Không đăng ký thêm Zone hoặc menu.

## Đối chiếu hình ảnh

[Baseline của trình bày Z021 trước đổi hướng](Screenshots/Baseline_Previous_Z021_Presentation.png). Ảnh sau thay đổi sẽ dùng màn hình Web thực tế sau khi vào từ Login.

**SHA-256 sau build khớp 76/76 tệp.** Việc nạp các Zone thử không thay đổi asset gốc. [Z021 sau cập nhật](Screenshots/Web_Z021_Overview.png) · [C005 cùng mô-đun cũ](Screenshots/Web_Z021_C005.png) · [Cầu, ray và địa hình Zone_018](Screenshots/Web_Zone018_Bridge.png)
