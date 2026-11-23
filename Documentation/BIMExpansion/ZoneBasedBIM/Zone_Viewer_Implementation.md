# Zone Viewer trong Quản lý vòng đời tài sản

Khởi đầu ở Zone_021 như trước đây. 17 đoạn dùng registry đã lưu; 16 đoạn ngoài Z021 nạp prefab xem chứa tham chiếu mesh, terrain và material gốc. Tool Editor kích hoạt nhóm theo danh sách activation đã lưu, bỏ MonoBehaviour và collider từ bản sao, rồi thêm mesh ray đôi theo `RailwayMicroChunkData`. Tất cả dùng cùng phép tịnh tiến từ `SceneProvenance.json` của mô hình Z021. Bản xem giữ nguyên pose tương đối ở hai đầu ranh giới. Runtime source Scene và streaming manager của tuyến chỉ chạy ở YenTuHaLongXanh.

| Hạng mục | Cách triển khai |
| --- | --- |
| Tải theo nhu cầu | `Resources.LoadAsync` cho Zone được chọn và tập phụ thuộc của công trình. Các instance mới ở một nhánh inactive trong lúc chuẩn bị. |
| Đổi Zone | Xử lý lựa chọn mới nhất. Khi nạp đủ phần cần thiết mới đổi vùng hiển thị. Đối tượng cũ được tắt và hủy, tài nguyên không dùng được trả lại. Chọn lại Zone đang mở định vị camera. |
| Zone_018 | Nạp đúng ba bản xem Zone_017–019. Camera fit theo chainage và dùng khoảng nhìn tăng riêng trong chế độ xem để nhìn trọn cầu. |
| Zone_021 | Khôi phục môi trường và pool Z021 đang dùng. Chọn cấu kiện ray từ tìm kiếm về Z021 trước khi kích hoạt lớp chi tiết. |
| Nghiệp vụ | Giữ ba tab, lựa chọn hai chiều đối với ray có ID, tìm kiếm và các lệnh Excel. Các đoạn xem ngoài Z021 dùng hình học chỉ đọc và các nhóm điều hướng. |
| SO 201 | Instance đã bỏ khỏi scene quản lý chính. Prefab, catalog IFC và cơ sở dữ liệu thử SO 201 vẫn lưu riêng. Scene thử trước đây giữ lại làm hồ sơ pilot. |

Scene quản lý giữ một panel trái 38% trên Web. Viewer dùng camera hiện có. Trình bày VR cũ giữ hệ thống input, bổ sung các nút chọn Zone; Quest chưa kiểm thử.

[Web test](Web_Test_Report.md) · [Tính toàn vẹn](Model_Integrity_Report.md)

Tọa độ viewer thu được bằng phép tịnh tiến chung (−10612,55957; −9,33166; +1598,86401) m. Cách này giữ phép quay và scale của từng renderer trong 16 bản xem. Dữ liệu gốc `Zone_021_Editable` vẫn là vùng hoạt động cho hồ sơ chi tiết.

Quyền sở hữu cầu và hai hầm lấy trực tiếp từ registry. OCL và subgrade dùng các binding geometry đã lưu và khoảng chainage trong Manifest. Zone_016 là đầu mối điều hướng cho hai mesh cổng OCL. Zone_011 là đầu mối điều hướng cho hai mesh subgrade, vì phần giao ghi ở Zone_010 chỉ khoảng 1,5 mm. Các đầu mối này là quy ước điều hướng của viewer.
