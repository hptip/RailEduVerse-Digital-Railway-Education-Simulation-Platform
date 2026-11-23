# Bridge_Main mô-đun trên tuyến Yên Tử – Hạ Long Xanh

Cầu mới được sinh từ spline, registry và mesh ray hiện có. Scene tuyến dùng `Bridge_YTH_Visual` tại đúng nhánh công trình Zone_018. Zone Viewer dùng `Bridge_YTH_Detailed` tại cùng pose sau phép chuyển hệ tọa độ viewer. Hai prefab là variant của một nguồn `Bridge_YTH_Source`. SO 201 vẫn là pilot riêng.

| Hạng mục | Kết quả kiểm tra hình học trong Unity |
| --- | --- |
| Phạm vi | 3.650,00–4.770,00 m, chiều dài danh nghĩa 1.120 m |
| Mặt cắt | Deck rộng 14 m, dày 1,1 m, tim ray cách nhau 6,400022 m. Mép deck đặt đối xứng theo trung điểm hai tim. |
| Mô-đun | 56 nhịp. Mỗi nhịp có một bản mặt, hai dầm đỡ và hai lan can dạng vách mép. 28 cụm trụ, mỗi cụm có hai thân trụ, hai bệ và một xà mũ. Tổng **420 cấu kiện**. |
| Đỡ ray | Đã kiểm tra 23 cặp microchunk gối lên đoạn cầu. Bao đáy mesh ray Y = **8,9291–8,9320 m**, deck Y = **8,9271 m**, còn khoảng **2,0–4,9 mm** ở bao đáy theo mô hình. |
| Mối nối | Các tiết diện ở ranh giới dùng chung lý trình. Đo sai khác lớn nhất giữa bốn góc hai đầu mesh deck liền kề **0,008 mm** trong hệ nguồn. |
| Khổ mô phỏng | Bounds toa trên frame pivot **3,429 × 5,532 × 27,880 m**. Lấy mẫu góc bounds qua cả hai tim mỗi 2 m cho khoảng hở ngang nhỏ nhất tới mép trong lan can **1,848 m**. Xe thực thi trong scene hiện hành đi Track_Right. |
| Prefab | 420 renderer mỗi bản, cùng từng tham chiếu mesh, ma trận và thứ tự material. Visual có 0 MonoBehaviour và 0 collider. Detailed có 420 identity và collider lựa chọn. |

Tiết diện swept bước tối đa 2 m nối theo mẫu spline. Trụ và xà mũ dùng cùng một mesh khối chuẩn với kích thước instance. Hai prefab cầu cũ có cụm mesh lồng và scale nhập lớn, nên đợt này dùng tiết diện đơn giản và tái sử dụng vật liệu đường sắt hiện có. Cao độ đáy trụ −18 m theo cấu hình cầu cũ. Báo cáo này mô tả mô hình chạy tàu trong Unity, chưa xác nhận thiết kế kết cấu, địa chất, khổ giới hạn động chính thức hoặc quy chuẩn an toàn.

## Dữ liệu BIM

`Cầu chính → Nhịp / Cụm trụ → cấu kiện lá`. Mã nội bộ dạng `YTH-BRIDGE-MAIN-<LOẠI>-<MỐC_MM>`. Đổi kích thước mà giữ loại và mốc không làm đổi AssetID. Khi chia lại nhịp cần lập mapping cho các mốc đổi. Bộ thông số mô phỏng chỉ đọc, hồ sơ chưa xác thực hiển thị “Chưa có dữ liệu”. Ghi chú, thông số người dùng và sáu giai đoạn dùng SQLite `BridgeYTH_Infrastructure.db`, riêng với ray Z021 và SO 201.

## Sandbox, cài đặt và khôi phục

Đã chạy cổng kiểm tra trên `Bridge_Replacement_Validation.unity`, bản sao của sandbox đã lưu vì sandbox gốc đang mở với thay đổi chưa lưu. Kiểm tra OldBridge/NewBridge bật luân phiên, cùng bối cảnh ray, nước và địa hình Zone_017–019, cùng hình học và các cổng khớp ở bảng trên. Sau khi đạt cổng cơ bản, cài Visual vào Zone_018; old renderer chuyển inactive và mesh nguồn cũ giữ nguyên. Registry tiếp tục quản lý cả nhóm theo logic hiện có.

Đối chiếu SHA-256 sau build Web cuối khớp **73/73 tệp** trong danh sách bảo toàn, gồm scene tuyến chính, 57 Zone còn lại, tài nguyên ray, prefab tàu, mã spline mover/streaming, dữ liệu SQLite đường ray, Showroom, Login và hồ sơ build.

Bản rollback ở `Temp/BridgeModular/Before_Zone_018.unity`, `Before_Zone_018.prefab`, `Before_QuanLyVongDoiTaiSan.unity` và `Before_Bridge_Replacement_Sandbox.unity`. Khôi phục tệp tương ứng khi scene đã đóng hoặc đã lưu phần làm việc của người dùng. Giữ nguyên các GUID và cơ sở dữ liệu người dùng.

Thông số có thể chỉnh trong `Assets/ExternalBIM/BridgeModular/Bridge_YTH_Config.json`. Menu **Tools → UTTVR → Bridge YTH** có hai bước sinh prefab rồi kiểm tra sandbox và cài đặt. Bước sinh từ chối chiều dài hoặc khoảng cách tim xung đột với registry/ray đang vận hành. Cấu hình dài hơn cần kiểm tra lại cả phạm vi giữ Zone và đầu nối, các tài nguyên tuyến giữ nguyên trong đợt này.

[Kiểm thử Web](Web_Test_Report.md) · [Danh sách các cấu kiện](Bridge_Asset_Registry.csv)
