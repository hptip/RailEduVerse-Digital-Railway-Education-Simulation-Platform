# G2 Finalization · YenTuHaLongXanh

Mở scene `Assets/ExternalBIM/InfrastructureG2Pilot/Infrastructure_G2_Pilot.unity` để duyệt. Generator cập nhật chính scene Pilot và giữ model ray, spline, tàu, nhà ga, cầu SO 201 và registry 17 Zone của sản phẩm. Cả hai hầm giữ chiều dài 600 m, mỗi hầm có 250 vòng bê tông × 11 tấm. Các bản sao terrain trong Pilot được chỉnh để khớp nền và cửa hầm.

Unity mở lại scene đã lưu, xác minh 1.366 tham chiếu mesh và đủ 14.664 placement của Detailed Loader. Có 158 phép kiểm PASS, một FAIL về độ phủ OCL ga và 25 ảnh Unity. Cả 129 file baseline khớp SHA-256 sau lượt dựng.

## Núi, cửa hầm và nền đường

- Núi dùng một trường cao độ world-space theo hai cụm đỉnh bất đối xứng và ba dải nhiễu có seed cố định. Tạo một lưới toàn vùng rồi chia theo ranh giới 50 m, dùng lại cùng đỉnh và pháp tuyến tại mối nối. Cao độ biên lấy mẫu từ terrain có sẵn. Forest_01 dùng thảm màu rừng và cụm cây giới hạn. Forest_02 có địa hình đá bất đối xứng, các mảng đá lộ và cây thưa. Chỗ bề mặt đi qua tiết diện hầm được kiểm bằng tam giác.
- Cả bốn cửa dùng vòm cắt xiên và kết cấu từ mẫu trước. Đã kéo vùng nền tiếp cận tới 14 m, kéo tường cánh, bổ sung tường cánh và rãnh ở cả đầu ra. Các ảnh dùng camera Unity tại vị trí tuyến. Các số đo liên quan góc nhìn và chi tiết hình thức vẫn thuộc phạm vi duyệt trực quan.
- Nền đường dùng bốn dải quy hoạch trong `Design_Configuration.json` gồm 300–400 m tự nhiên, 400–600 m gia cố hình thoi, 600–700 m tường chắn và 700–800 m tự nhiên. Mesh nền giữ sáu lớp. Visual ẩn các lớp trong. Việc chỉnh địa hình giới hạn trong hai vai nền của bản sao terrain. Các chỗ nối kiểu nền cần duyệt trực quan.

## OCL và quyền nguồn

- Ga đầu có hai cổng tại **37,500 m** và **51,897 m**. Phép đo mặt đỡ và giao cắt đạt tại bố trí này. Khoảng hở bề mặt của hình học đã lắp vượt ngưỡng kiểm Pilot **20 mm** theo phép đo khoảng cách tam giác và cạnh. Đây là ngưỡng kiểm giao hình học. **37,500 m đầu còn thiếu dây** nên phép kiểm độ phủ ga vẫn FAIL. Chưa đưa OCL ga vào gói có thể thay thế.
- Cầu SO 201 giữ nguyên. Các mặt đỡ được kiểm theo tam giác mặt bản. Đã so giao cắt với 14 mesh mép cầu. Bao quét hai toa trên cả hai đường ray đạt ngưỡng hình học hiện hành. Hành trình và tiếp xúc pantograph vẫn **NOT VERIFIED**. Ngoại lệ OCL trực quan cần duyệt riêng.
- Mesh và material ứng viên đều lưu trong `PilotGeometry.asset`. Kiểm tra đệ quy và ô texture không tìm thấy mesh, material hoặc texture từ nguồn nghiên cứu chưa xác minh trong ứng viên. Những tài nguyên hiện trường của bản sao scene nằm ngoài đánh giá ngoại lệ. **SRC-02 vẫn BLOCKED**, ngoại lệ hình học tự sinh cần người dùng duyệt.

## Detailed và hiệu năng

- Chọn **04 · Detailed Loader** trong Hierarchy. Chọn domain và chunk 50 m, sau đó dùng các nút Inspector để nạp, tách, cắt, cô lập và khôi phục. Loader chỉ khởi tạo GameObject và collider của chunk được chọn và tạm ẩn Visual tương ứng. Catalog có đủ ID, nguồn tham số, mesh, material, placement, owner, Zone và liên kết Visual. Không dùng IFC GlobalId nguồn cho các cấu kiện tự sinh.
- Unity thử nạp đủ ba domain và kiểm tra chọn qua BridgeBimAdapter, Section khớp collider, Isolation và Reset. Catalog có 14.664 ID duy nhất. Đợt thử nạp tối đa 374 GameObject cấu kiện cùng lúc. Loader dùng định nghĩa sáu giai đoạn hiện hành và hỗ trợ mở SQLite riêng cho Pilot. Nạp mesh vào bộ nhớ theo asset, liên kết ba tab/Tree View trong scene sản phẩm, lưu–mở lại và Excel vẫn dành cho đợt tích hợp đã duyệt. Pilot tạo GameObject theo chunk, còn scene giữ tham chiếu tới tập mesh của catalog.
- [Bảng so sánh hiệu năng](Performance_Comparison.csv) ghi Visual của ứng viên tăng từ 459 lên 486 renderer và từ 943.872 lên 979.444 tam giác. Số liệu lấy từ mesh đã sinh và không bao gồm bối cảnh. Chưa đo draw call, frame time, bộ nhớ thực tế hoặc kích thước Web. VR **NOT TESTED**.

## Bàn giao duyệt

- [Forest_01 trên cao](Screenshots/Forest_01_Mountain.png), [cửa vào](Screenshots/Forest_01_Portal.png), [cửa ra](Screenshots/Forest_01_Exit.png).
- [Forest_02 trên cao](Screenshots/Forest_02_Mountain.png), [cửa vào](Screenshots/Forest_02_Portal.png), [cửa ra](Screenshots/Forest_02_Exit.png).
- [Nền tự nhiên](Screenshots/Subgrade_Natural.png), [mái hình thoi](Screenshots/Subgrade_Reinforced.png), [tường chắn](Screenshots/Subgrade_RetainingWall.png), [chuyển tiếp](Screenshots/Subgrade_Transition.png).
- [OCL ga](Screenshots/OCL_Station.png), [OCL cầu](Screenshots/OCL_SO201.png), [Detailed Pilot](Screenshots/Detailed_Pilot_Exploded.png).
- [Kiểm hình học](Geometry_Test_Results.csv), [catalog CSV](Asset_Registry.csv), [thay đổi và rollback](../Changed_Files_And_Rollback.md).

G2 Production chờ duyệt hình thức và hai ngoại lệ. Sau duyệt, chỉ triển khai công trình đạt kiểm tra. OCL ga tiếp tục dùng cấu hình đang hoạt động trong khi tìm giải pháp cho 37,500 m đầu. G3 Web chưa chạy.
