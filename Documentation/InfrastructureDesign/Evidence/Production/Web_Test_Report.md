# G3 Web · kiểm thử trong Chrome

## Lượt build đầu

Unity build Web Development thành công, 0 lỗi, 1.065 cảnh báo, 1.415.685.001 byte. Thời gian build 12 phút 06 giây. Các lượt chạy dùng HTTP cục bộ, Chrome headless, khung hình 1.280 × 720. Không đo VR.

Login online nhận HTTP 404 khi máy chủ ứng dụng chưa chạy trên phiên kiểm thử. Với tham số offline có sẵn, Login hoạt động và đưa vào Classroom. Luồng tiếp tục qua menu F1 sang tuyến và BIM. Thời gian từ mở trang đến menu khoảng 10,1 giây trong lượt đo tuyến.

## Tàu và streaming

Tàu chạy bằng input throttle của hệ hiện hành từ **25,00 m tới 4.900,27 m**, đi qua toàn bộ nền, hai hầm, OCL đường thường và cầu SO 201. Tốc độ thấp đo ở nền là **40 km/h**. Tốc độ tối đa ghi được **200 km/h**. Có 12 Zone tải, 9 Zone dỡ và 0 Zone thiếu tại điểm lấy mẫu. Camera nóc, cabin và kết cấu đã được ghi ảnh. Ở bản Web cuối, đã xác nhận Zone_010 tải lại khi rời tuyến về Classroom rồi mở tuyến lần nữa.

| Cảnh | Lý trình m | km/h | Draw call | Tam giác | Bộ nhớ Unity MB |
| --- | ---: | ---: | ---: | ---: | ---: |
| Nền đường | 350,08 | 40,00 | 282 | 1.334.184 | 559,6 |
| Forest_02 | 1.140,19 | 155,61 | 266 | 1.477.849 | 573,5 |
| Trong Forest_02 | 1.480,35 | 187,71 | 250 | 1.462.461 | 583,3 |
| Forest_01 | 2.230,35 | 200,00 | 307 | 1.627.443 | 606,4 |
| Trong Forest_01 | 2.650,17 | 200,00 | 251 | 1.452.739 | 619,6 |
| OCL đường thường | 3.140,37 | 200,00 | 366 | 1.488.044 | 631,6 |
| SO 201 · kết cấu | 3.920,31 | 200,00 | 217 | 591.919 | 634,9 |
| Ra khỏi SO 201 | 4.255,48 | 200,00 | 210 | 713.052 | 643,4 |

Unity ghi frame-time p95 và p99 cùng **9,0 ms** trong lượt này. Đây là số Unity báo tại môi trường kiểm thử, có thể bỏ qua các gián đoạn ngắn giữa mẫu. Số MeshRenderer tối đa lấy mẫu là 607. Bộ nhớ trong bảng là Total Used Memory của Unity; không cộng thêm GPU hoặc toàn bộ bộ nhớ Chrome.

## Bản Web kiểm tra BIM

Build Development thành công với **0 lỗi**, 1.064 cảnh báo, **1.415.575.950 byte** và thời gian build 6 phút 44 giây. Bản build dùng cùng nội dung tuyến đã đo ở lượt đầu, với thao tác BIM được sửa và kiểm lại trong Chrome. Chỉ số chạy tàu ở bảng trên thuộc lượt đầu. Build nằm trong `Builds/UTTVR_Infrastructure_Production_Web`. Mở qua HTTP với `?offline=1` và dùng F1 để chọn scene. Bản cuối tải lại Zone_010 ở lý trình 355,10 m sau khi quay về Classroom. Thời gian từ chọn BIM đến log Ready trong lượt kiểm thanh nút là khoảng 0,68 giây.

| Trạng thái | Phạm vi đã kiểm tra |
| --- | --- |
| PASS | Chọn cấu kiện Detailed từ cây và bấm trực tiếp mô hình để xác nhận một renderer được làm nổi bật. Đã dùng Tách cấu kiện, Mặt cắt, Cô lập và Đặt lại BIM. Các lệnh được xác nhận qua log và ảnh. |
| PASS | Z021 tải được và chọn lại được cấu kiện Z021-TR-S001-B001-C005. |
| PASS | Excel Web xuất tệp, mở trình chọn tệp, xem trước và nhập thành công. Ghi chú mới hiển thị đúng ngay trong phiên. |
| FAIL | Ghi chú mới bị mất sau khi tải lại. Đối chiếu các trang SQLite trong IndexedDB xác nhận dữ liệu mới chưa được ghi. Đã sửa và kiểm lại trên bản build mới nhất. |
| PASS | Nút Lưu hồ sơ ghi nhận thao tác lưu giai đoạn Vận hành. Lượt thử đầu vào nhầm trường ngày và bị chặn hợp lệ. |
| PASS | Đã mở Classroom, UTTCampus và Showroom trong lượt hồi quy. Không thấy exception trong các mẫu kiểm khi mở Scene. |
| NOT TESTED | VR trên kính, các thao tác chuyên sâu trong Classroom/Campus/Showroom và kiểm thử backend online. Lần thử online trên HTTP cục bộ nhận 404 khi backend không chạy. |

- [Ảnh menu sau Login](Screenshots/Web_SceneMenu.png), [nền đường](Screenshots/Web_Subgrade.png), [Forest_02](Screenshots/Web_Forest_02.png), [Forest_01](Screenshots/Web_Forest_01.png), [OCL](Screenshots/Web_OCL_Open.png), [cầu SO 201](Screenshots/Web_SO201.png).
- [Ảnh BIM](Screenshots/Web_Final_Infrastructure_Tree.png), [Tách cấu kiện](Screenshots/Web_Final_Exploded.png), [Mặt cắt](Screenshots/Web_Final_Section.png), [Excel đã áp dụng](Screenshots/Web_Final_Excel_Applied.png), [Z021](Screenshots/Web_Final_Z021_C005.png), [chọn từ mô hình](Screenshots/Web_Final_Model_Pick.png).
- [Telemetry chạy tàu](Web_Test_Telemetry.json), [lượt tải lại Zone](Web_Final_Reload_Telemetry.json), [lượt BIM](Web_Final_BIM_Telemetry.json), [lượt Lưu hồ sơ](Web_Final_Persistence_Telemetry.json), [ảnh ghi chú sau nhập](Screenshots/Web_Final_Lifecycle_After_Import.png).

## Kiểm tra và sửa lưu Web

Bản chẩn đoán build thành công với 0 lỗi. Giao dịch IndexedDB lưu được nội dung ghi chú, nhưng SQLite không mở lại được dữ liệu vì thiếu bản ghi kích thước của VFS. Đã bật lại SQLite xSync để ghi bản ghi này. Bộ lưu áp dụng riêng cho hồ sơ hạ tầng. Thông báo lưu thành công chỉ hiện sau khi giao dịch IndexedDB hoàn tất.

Bản sửa cuối build thành công trong 2 phút 30 giây với **0 lỗi**, 17 cảnh báo và 1.415.580.109 byte. **PASS** với ghi chú sửa từ Excel. Chrome ghi nội dung và bản ghi kích thước vào IndexedDB. Sau khi tải lại trang và mở lại AssetID `G2-Subgrade-S0400-000-Layer00`, tab Vận hành hiển thị lại ghi chú đã nhập.

**PASS** với sửa tay. Lượt đầu chọn nhầm trường ngày và bị chặn hợp lệ. Lượt kiểm lại nhập `g3 manual` vào đúng ô ghi chú Vận hành, bấm Lưu hồ sơ, xác nhận giao dịch IndexedDB và tải lại. Chrome hiển thị đúng giá trị đã sửa.

- [Bằng chứng Excel sau tải lại](Screenshots/Web_Verified_Workbook_Reopened.png), [telemetry của bài kiểm Excel](Web_Verified_Persistence_Telemetry.json).
- [Bằng chứng sửa tay sau tải lại](Screenshots/Web_Verified_Manual_Reopened.png), [telemetry của bài kiểm sửa tay](Web_Verified_Manual_Telemetry.json).
