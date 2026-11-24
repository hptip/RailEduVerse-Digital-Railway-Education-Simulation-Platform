# Tái sử dụng hình học SO 201 cho cầu đường sắt Yên Tử

## Nguồn và kết quả phân loại

Đối chiếu 91 sản phẩm IFC có hình học với 91 mesh riêng đã import trong `BridgeBIM_Demo.prefab`. Unity xác nhận mỗi phần tử tham chiếu một subasset riêng của `Bridge_SO201.dae`. Tổng dữ liệu kiểm tra là 36.099 tam giác. [Bảng 91 cấu kiện, kích thước và mã mesh](SO201_91_Components.csv)

| Phân loại | Số mesh nguồn | Chi tiết |
| --- | ---: | --- |
| Đã sử dụng trực tiếp | 12 | 5 dầm nhịp 1, tấm mặt số 5, dầm ngang số 6, thân trụ P2, 2 bệ gối P2, đài móng P2 và bê tông lót P2. |
| Cần chỉnh hoặc cần thêm dữ liệu | 65 | Cọc, mố, tường cánh và các biến thể gối, trụ, dầm hoặc tấm chưa dùng. Cần kiểm tra mặt cắt, địa hình và địa chất trước khi đưa thêm vào ứng viên. |
| Không phù hợp với cách lắp hiện tại | 14 | Mặt đường bộ, các gờ dọc, bản chuyển tiếp O1, các lớp phủ, đất đắp và ống thoát nước đường bộ. |

Kích thước sử dụng tính trong hệ trục dọc/ngang theo dầm SO 201. Hai nhịp đầu/cuối của mẫu IFC có cụm dầm dài khoảng 16,86 m. Tấm mặt được dùng dài khoảng 15,60 m, rộng 10,14 m. Dầm ngang nguồn dài khoảng 1,90 m và lắp vào vùng gối ở ranh giới nhịp. Chiều rộng toàn phần của cầu tuyến là 14 m, tim hai đường ray cách nhau 6,400022 m. Chỉ thay đổi vị trí và hướng đặt của nhóm mesh IFC. Các mesh dùng lại giữ nguyên kích thước, subasset Unity và material đã import.

## Mẫu đã kiểm tra trong Unity

Hai nhịp 17,5 m và ba cụm gối đôi trong scene độc lập `Assets/ExternalBIM/SO201RouteCandidate/SO201_Railway_Sandbox.unity`. Đo trên lý trình **4.192,501–4.227,501 m**. Ray lấy từ đúng hai cặp microchunk của tuyến. Ray tham chiếu kéo dài ngoài phạm vi mẫu để thấy điểm tiếp cận.

| Hạng mục | Số đo |
| --- | --- |
| Nguồn hình học | 45/60 occurrence dùng trực tiếp 12 mesh nguồn. **15.332/15.944 tam giác (96,16%)** có nguồn SO 201. |
| Cao độ sàn | Y = 8,92707 m. Bao đáy mesh của hai ray Y = 8,92907–8,93112 m. Phần IFC nguyên bản cao nhất Y = 8,86707 m. |
| Mối nối sàn | 0,000000 m giữa hai nhịp ở độ chính xác in ra. |
| Độ lệch chord khỏi tim spline | Tối đa 0,00205 m trong phạm vi mẫu. |
| Khoảng hở ngang nhỏ nhất tới lan can, bao toa trên cả hai tim | 1,8427 m theo hình học mô phỏng. |
| Khoảng hở giữa hai đài móng và hai lớp bê tông lót | 0,6000 m và 0,1000 m sau khi dịch tâm trụ đôi sang ±4,10 m. |
| Kiểm tra biến đổi rigid từ mesh gốc | Sai lệch đỉnh lớn nhất 0,000006 m trong hệ gốc cục bộ của mẫu. |

Ảnh đối chiếu dùng cùng camera, đèn và ray. Mẫu mới có dầm, vai trụ, bệ gối và đài móng phân biệt rõ. Bản mô-đun đang chạy dùng tiết diện tự sinh đơn giản với nhịp 20 m. Đây là phép đo hình học trong Unity.

[Góc bên của SO 201](Screenshots/SO201_Side.png) · [Dầm nguồn](Screenshots/SO201_Girders.png) · [Cụm trụ và móng](Screenshots/SO201_Support.png) · [Từ trên](Screenshots/SO201_Top.png) · [Cầu mô-đun cùng góc](Screenshots/Modular_Same_View.png) · [Mapping mẫu](Sample_Occurrences.csv)

## Hình học bổ sung và lặp nhịp

- **Sàn ray đôi liên tục**, rộng 14 m, dày 0,28 m, lấy cao độ từ đáy mesh ray. Sàn này phẳng và nối trực tiếp theo spline, giữ tấm bê tông SO 201 bên dưới với kích thước nguyên bản.
- **Hai lan can đường sắt** ở mép ngoài và **xà mũ đôi** rộng 13,4 m để nối hai nhánh gối. Gờ đường bộ IFC đã được phân loại riêng.
- **Đoạn nối thân trụ xuống đài móng** giữ thân trụ P2 nguyên kích thước, tiếp tục sử dụng cao độ đáy đài theo mô hình tuyến. Kết cấu mố, nhóm cọc, hồ sơ địa chất và thoát nước chi tiết cần dữ liệu riêng nếu đưa vào phạm vi.

Phương án lặp **64 × 17,5 m = 1.120 m**. Sàn và lan can theo tiết diện tuyến; các cấu kiện IFC giữ hình học và đặt rigid theo chord hoặc hướng gối. Các nhà ga, ray, tàu, OCL và logic giữ Zone_017–019 dùng hệ thống hiện có.

Prefab Visual và Detailed cùng tạo từ một Source. Bản Detailed gắn identity và catalog; bản Visual giữ mesh và material để hiển thị 3D. AssetID mới dùng tiền tố `YTH-SO201-` cùng chỉ số nhịp/gối và vai trò. Cột `SourceGlobalId` ghi nguồn IFC, có thể lặp lại vì nhiều occurrence cùng dùng một mesh. Các occurrence có AssetID riêng.

## Ứng viên toàn cầu

Unity đã sinh và kiểm tra 64 nhịp và 65 vị trí gối tại đúng lý trình **3.650,001–4.770,001 m**. Biên ngang nhỏ nhất theo bao hai toa trên hai tim là **1,8372 m**. Độ lệch chord tối đa **0,00366 m**, mối nối sàn khép kín ở độ chính xác phép đo. Đối chiếu cao độ với 23 cặp microchunk ray cho khoảng cách từ mặt sàn đến bao đáy ray khoảng **2,00–4,96 mm**. [Kết quả đo toàn cầu](Full_Measurements.txt)

| Hạng mục | Cầu mô-đun đang hoạt động | Ứng viên SO 201 |
| --- | ---: | ---: |
| Chiều dài × bề rộng sàn | 1.120 × 14 m | 1.120 × 14 m |
| Nhịp và cụm gối | 56 nhịp / 28 cụm trụ đôi | 64 nhịp / 65 gối đôi kể cả hai đầu |
| Cấu kiện Detailed | 420 | 1.486 |
| Cấu kiện thật tham chiếu SO 201 | 0 | 1.099, từ 12 mesh riêng |
| Tổng tam giác về mặt hình học | 25.200 | 491.794 |
| Tam giác từ SO 201 | 0 | 473.326 (**96,24%**) |

Source, Visual và Detailed lưu riêng trong `Assets/ExternalBIM/SO201RouteCandidate`. [Mapping của 1.486 occurrence](Full_Occurrences.csv)

Đã đối chiếu prefab Detailed có 1.486 identity và collider. Prefab Visual nguyên hình học có 1.486 renderer và dùng lại trực tiếp các subasset DAE. Bản Visual gộp dẫn xuất giữ các vật liệu theo submesh, gộp riêng 64 nhịp và 65 vị trí gối thành **129 renderer**, giữ nguyên **491.794 tam giác**. Sai lệch lớn nhất khi tính lại bounds cục bộ là 0,0000013 m. [Provenance từng nhóm Visual](Visual_Group_Provenance.csv)

## Hồ sơ đang sử dụng và rollback

Đã đối chiếu đủ 420 AssetID cũ với vị trí và loại cấu kiện của phương án mới. Bước nhịp thay đổi từ 20 m sang 17,5 m, dầm và trụ thay đổi số lượng/phạm vi. Cả 420 dòng có nhiều ứng viên gần lý trình theo bộ lọc vai trò. Chưa xác nhận quan hệ tương đương 1:1. Giữ nguyên 420 AssetID, catalog và SQLite đang dùng. Nếu cần chuyển hồ sơ, cần duyệt từng quan hệ theo loại, vị trí và lịch sử tài sản, sau đó thử trên cơ sở dữ liệu riêng trước. [Bảng rà 420 AssetID](Legacy_420_Review.csv)

[Snapshot Zone_018 trước ứng viên SO 201](Zone_018_Optimized_Rollback.zip) chứa scene và file meta. Bản Visual tối ưu trước đó vẫn có đầy đủ asset để mở lại. Bộ xác minh SHA-256 dùng [danh sách nguồn bảo vệ](Protected_Baseline.sha256).

**Trạng thái triển khai.** Mẫu và ứng viên dài 1.120 m đạt kiểm tra hình học; đã chạy hết cầu trên Web. Giữ cầu mô-đun trên tuyến chính thức sau khi đo chi phí bản SO 201. Đối chiếu **24/24 SHA-256** giữ nguyên, bao gồm Zone_018 chính thức, catalog và dữ liệu ray hiện tại, model SO 201 gốc, Scene BIM, Showroom, tàu, Spline/registry của tuyến và Player Settings. Detailed vẫn là prefab ứng viên có catalog riêng. SQLite và giao diện quản lý hiện tại tiếp tục sử dụng hồ sơ cũ. [Kết quả Web và lý do giữ bản đang chạy](Web_Trial_Report.md)
