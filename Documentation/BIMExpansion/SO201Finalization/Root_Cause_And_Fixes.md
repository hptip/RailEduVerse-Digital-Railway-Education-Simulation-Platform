# Cầu SO 201 · Nguyên nhân và cách sửa

Kiểm kê dùng 91 mesh DAE, IFC GlobalId và phép biến đổi về một hệ tọa độ cầu. Cùng bộ đo được áp dụng cho nguồn, cầu 214 m và mẫu năm nhịp trước sửa. Báo cáo lấy mẫu mặt tam giác tại vị trí giao chiếu đứng. Bounds chỉ giới hạn miền lấy mẫu.

| Phát hiện | Nguyên nhân và xử lý |
| --- | --- |
| Chênh 0,93–1,93 m ở kiểm tra dầm dọc–bệ gối cũ | Các cặp đó được ghép theo AABB. Chuỗi đỡ đi qua dầm ngang. Đáy dầm ngang đúng có chênh khoảng 0,56–0,82 m với đỉnh bệ trong hình học nguồn đã scale. Dời bệ ở trụ theo mặt dầm ngang, hiệu chỉnh đoạn giữa thân trụ để đầu trụ theo bệ. Ở mố, hiệu chỉnh lưới bệ có sẵn để mặt đáy giữ nguyên chỗ tựa. |
| Kiểm tra tia báo chân trụ cách đài nhiều mét | Mesh nhập không có mặt đóng đáy. Kiểm tra vành chân trụ đối với mặt tam giác trên đài móng cho sai số tối đa 0,03 mm. Giữ nguyên chân trụ và đài. |
| Đầu cọc trụ giữa thấp hơn đáy đài khoảng 0,36 m | Lớp bê tông lót ở giữa. Đo tách liên kết cọc–lót và lót–đài, cả hai đạt. |
| Bậc đứng khi lặp nhịp giữa | Hai đầu nhịp nguồn lệch độ cao. Hiệu chỉnh độ dốc trong cùng phép biến đổi cho kết cấu nhịp. Năm điểm đo trên mỗi trong sáu mối nối của ứng viên cuối lệch tối đa 0,07 mm. |
| Approach slab O1 có hình học ở cả hai đầu cầu | Cắt ở khoảng trống giữa hai phần, chuyển mỗi phần theo mố tương ứng. Chỉ hai đầu cầu có mố và bản quá độ. |

Generator dùng cùng cách ghép cho mẫu 2, 5 và 7 nhịp. Tại bản 7 nhịp, 16 bệ gối có chênh tuyệt đối lớn nhất 4,13 mm với dầm ngang trong miền lấy mẫu. Không tạo cấu kiện bê tông mới để lấp khoảng hở. Kết quả có số mẫu, vị trí, ngưỡng và trạng thái trong CSV.
