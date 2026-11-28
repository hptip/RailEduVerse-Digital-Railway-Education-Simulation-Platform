# G2 Production và G3 Web · YenTuHaLongXanh

## G2 Production

Đã tích hợp và mở lại chín Zone từ Zone_010 đến Zone_018. Registry vẫn giữ 17 Zone. Cầu SO 201, spline, tàu, build scene list và các scene ngoài phạm vi giữ nguyên. Trong bộ 129 file baseline, 19 file thay đổi theo phạm vi được duyệt gồm 9 Zone Scene, 9 prefab xem Zone BIM và Scene quản lý vòng đời. 110 file còn lại khớp SHA-256.

| Trạng thái | Phạm vi |
| --- | --- |
| PASS | Forest_02, Forest_01, hai vùng núi, bốn cửa hầm, ba kiểu Subgrade, OCL đường thường, cầu và hai hầm đã lưu vào Zone thật. Đã mở lại từng Zone và kiểm tham chiếu mesh cùng các root cũ. |
| PASS | Ba mối nối đáy ray của Subgrade tại 400, 600 và 700 m. Bề mặt Layer00 đo ở năm vị trí ngang ở mỗi mối nối và đạt sai lệch cao độ dưới 2 mm. |
| PASS | Catalog chính thức có 14.504 ID duy nhất, gồm 5.176 OCL, 8.472 hầm và 856 nền. Có 86 asset Detailed nạp riêng. Catalog và lookup trong Scene giữ metadata. |
| PASS | Thông số và sáu sheet vòng đời Excel được xuất, sửa, nhập và mở lại từ SQLite thử riêng. Kho dữ liệu hạ tầng tách khỏi SQLite ray và hồ sơ SO 201. |
| DEFERRED | OCL ga. Loại 160 ID và toàn bộ Visual batch phụ thuộc của ứng viên ga. Giữ OCL ga đang hoạt động. |
| EXCEPTION | SRC-02 chỉ áp dụng cho tài sản tham số độc lập đã duyệt. Quyền nguồn nghiên cứu vẫn chưa xác minh. Kiểm tra dependency đệ quy của các gói mới đạt. |
| EXCEPTION | OCL chỉ dùng để minh họa. Tiếp xúc và chuyển động pantograph **NOT VERIFIED**. |

Bộ kết quả Editor ghi 24 phép kiểm PASS. Số liệu Visual của công trình mới là 477 renderer, 968.140 tam giác và khoảng 50,1 MB mesh theo Unity Editor khi cộng cả chín Zone. Ứng viên Finalization có 486 renderer và 979.444 tam giác trước khi loại phần OCL ga và chốt tích hợp. Các số trên cộng cả chín Zone. Bộ nhớ mesh của một chunk Detailed lớn nhất đo được khoảng 1,22 MB. Draw call, thời gian khung hình và bộ nhớ Web nằm trong báo cáo chạy riêng.

Để xem BIM, mở nhóm hạ tầng trong Tree View ở Zone phù hợp hoặc tìm theo AssetID. Khi chọn một cấu kiện, loader lấy asset của chunk tương ứng. Các tab hiện hành dùng chung chọn từ cây và mesh collider. Hạ tầng có thao tác tách cấu kiện, cắt, cô lập và đặt lại. Hồ sơ mô phỏng được trình bày riêng với số liệu hình học và dữ liệu người dùng.

## G3 Web

Đã chạy qua Login offline và menu sản phẩm trong Chrome. Tàu chạy bằng input từ 25,00 m đến 4.900,27 m và đạt 200 km/h. Có 12 lượt tải Zone và 9 lượt dỡ, với 0 Zone thiếu ở các điểm lấy mẫu. Đã kiểm tải lại Zone_010 khi mở lại tuyến trên bản Web cuối. Lượt đo ghi p95/p99 9,0 ms, tối đa 366 draw call và 1,63 triệu tam giác tại các điểm lấy mẫu. Phiên kiểm dùng Chrome headless trên máy phát triển.

Bản Web mới nhất build với 0 lỗi, 17 cảnh báo và dung lượng 1.415.580.109 byte. Đã kiểm các chế độ xem BIM, chọn cấu kiện từ mesh và hồi quy Z021. Ghi chú vòng đời nhập từ Excel và sửa tay đều đã được xác minh còn sau khi tải lại trang. VR và backend online chưa được xác nhận. OCL ga tiếp tục dùng cấu hình hiện hành. Tiếp xúc pantograph chưa được kiểm chứng.

## Evidence

- [Geometry Test Results](Production/Geometry_Test_Results.csv)
- [Production Asset Registry](Production/Production_Asset_Registry.csv)
- [Performance Comparison](Production/Performance_Comparison.csv)
- [Ảnh Scene tuyến](Production/Screenshots/Forest_01.png), [Subgrade](Production/Screenshots/Subgrade_Reinforced.png), [OCL](Production/Screenshots/OCL_Open.png) và [SO 201](Production/Screenshots/SO201_OCL.png)
- [Web Test Report](Production/Web_Test_Report.md)
- [Tệp thay đổi và rollback](Changed_Files_And_Rollback.md)
