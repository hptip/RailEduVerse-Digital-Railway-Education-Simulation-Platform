# G1 Visual Refinement

Mở scene mẫu `Assets/ExternalBIM/InfrastructureG1/Infrastructure_G1.unity`. Thư mục `Screenshots` giữ 17 ảnh G1 ban đầu. Thư mục `G1_Refined` chứa ảnh chụp từ Unity cho lượt duyệt này. Trạng thái duyệt của người dùng là **PENDING**.

| Hạng mục | Thay đổi so với G1 ban đầu | Ảnh Unity |
| --- | --- | --- |
| OCL | Cần tiết diện chữ nhật, giằng xiên, sứ nhiều vành, chốt liên kết, bản mã, tay định vị và kẹp dây có hình riêng. Kiểu cầu dùng bệ thấp, kiểu hầm dùng bản gá thành và kiểu ga dùng dàn cổng. | [Tổng thể đường thường](G1_Refined/OCL_OpenTrack.png), [cụm cần](G1_Refined/OCL_ArmAssembly.png), [móng](G1_Refined/OCL_Detail.png), [cầu](G1_Refined/OCL_Bridge.png), [hầm](G1_Refined/OCL_Tunnel.png), [ga](G1_Refined/OCL_Station.png) |
| Cửa và núi | Vòm bê tông dày, mặt cắt xiên, tường cánh, nền tiếp cận, rãnh và sườn chuyển tiếp. Hai mẫu có nhiều đỉnh, núi rừng có tán cây và núi đá có các mảng đá trải theo sườn. | [Cửa núi rừng](G1_Refined/Tunnel_Forest_Portal.png), [cao](G1_Refined/Tunnel_Forest_Elevated.png), [nghiêng](G1_Refined/Tunnel_Forest_Slope.png), [cửa núi đá](G1_Refined/Tunnel_Rock_Portal.png), [cao](G1_Refined/Tunnel_Rock_Elevated.png), [nghiêng](G1_Refined/Tunnel_Rock_Slope.png) |
| Nội thất | 15 vòng hầm mỗi mẫu, tổng chiều dài lắp ghép 36 m. Giữ khổ hầm đôi, bổ sung đầu bu lông tấm, tủ kỹ thuật, biển thoát hiểm, dây và giá đỡ. Ánh sáng điểm không đổ bóng, vật liệu bê tông giảm phản quang. | [Trong hầm](G1_Refined/Tunnel_Interior.png), [tấm vỏ](G1_Refined/Tunnel_LiningDetail.png), [mặt cắt](G1_Refined/Tunnel_Section.png) |
| Nền | Sáu lớp giữ nguyên. Dầm ô hình thoi có tiết diện theo pháp tuyến mái và chân dầm ngàm vào mái cỏ. Tường chắn có mũ tường và đầu thoát nước theo tấm. | [Tự nhiên](G1_Refined/Subgrade_Natural.png), [gia cố](G1_Refined/Subgrade_Reinforced.png), [tường chắn](G1_Refined/Subgrade_RetainingWall.png), [ô hình thoi](G1_Refined/Subgrade_Diamond.png), [chi tiết tường](G1_Refined/Subgrade_WallDetail.png), [mặt cắt sáu lớp](G1_Refined/BIM_Section.png) |
| BIM | Catalog và mã cấu kiện theo slot vật lý. Kiểm tra chọn từ catalog và từ collider, rồi thử tách, cắt, cô lập và khôi phục. | [Tách lớp](G1_Refined/BIM_Exploded.png), [mặt cắt](G1_Refined/BIM_Section.png), [cô lập](G1_Refined/BIM_Isolation.png) |

## Đo và xác minh nguồn

- **FBX.** Bảng `FBX_Accessory_Screening.csv` ghi từng mesh, Unity GUID/local ID, vị trí và kích thước. Hai mesh gần thân cột `Cylinder` và `Cylinder.001` có thể tham khảo cho sứ. Nhóm `Cylinder.002–004` và `Cylinder.006` có tỷ lệ phù hợp để xem xét cho các thanh đỡ và giằng. Nhóm phụ kiện nhỏ cần kiểm tra mặt gá và tâm chốt trước khi dùng. Chiều cao của thân nguồn không quyết định khả năng tái sử dụng từng phụ kiện.
- **BLEND và quyền sử dụng.** Header BLEND v292 và FBX v7400 đã ghi nhận. Chưa tìm thấy Blender trong PATH hoặc thư mục cài đặt chuẩn đã kiểm tra, nên chưa đối chiếu hình học trực tiếp giữa hai tệp. Chưa có bằng chứng quyền chỉnh sửa và phân phối. Cấu kiện OCL trong mẫu dùng hình học tham số.
- **LoD5.** Vòng nguồn cho một đường ray. Máng cáp, nắp máng, rãnh, lan can, đèn và tín hiệu có mesh riêng. Một số mesh chi tiết khá nặng cho mẫu lặp, như máng cáp 12.720 tam giác và lan can 12.728 tam giác. Mẫu dùng chúng làm tham chiếu bố trí. Các cấu kiện hầm quản lý BIM tiếp tục dùng hình học sinh từ generator.
- **Subgrade.** Kết luận của phép so sánh IFC ban đầu được giữ. Lượt này dùng sáu lớp sinh tham số và không đọc lại IFC nền. Hình chụp so sánh còn trong `Screenshots`.

## OCL-03

`OCL_03_Measurements.txt` ghi hình học thật của hai toa và ga đầu. Đỉnh bao tàu ở Y = 15,4186 m và tâm dây mẫu ở Y = 15,9454 m. Điểm neo mẫu trên SO 201 đã được lấy mẫu trên tam giác mặt bản cầu. Hình học mái ga được lấy mẫu theo hành lang hai ray, song đường dò không giao mặt mái đủ cao trong hành lang đo. Mặt tiếp xúc và hành trình pantograph chưa xác định được trong mesh tàu đã gộp. Hai giới hạn này giữ OCL-03 ở **BLOCKED**.

Các phép kiểm và ảnh thuộc Unity Edit Mode. Hai ảnh nội thất trước và sau được lấy mẫu ở cùng vùng ảnh giữa nền ray. Số điểm có cả ba kênh RGB trên 245 giảm từ 2.478 xuống 0 trên lưới lấy mẫu 2 px. Phép so sánh đo vùng chói trên ảnh, còn chiếu sáng thiết bị và hiệu năng thực tế vẫn cần đánh giá khi tích hợp. Mặt cắt dùng lưới cắt thật, phần núi trong ảnh là vỏ bề mặt và không có diện tô mặt cắt địa chất.

Kích thước trong ảnh thiết kế chỉ dùng để đối chiếu hình thức. Kích thước dựng mẫu nằm trong `Resolved_Geometry.json`.
