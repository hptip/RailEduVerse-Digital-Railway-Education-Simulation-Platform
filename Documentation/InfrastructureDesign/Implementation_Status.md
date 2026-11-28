# Trạng thái Infrastructure Design Kit · 27/11/2026

Design Baseline v1.0 đã được người dùng duyệt. Duyệt trực quan bộ mẫu G1 vẫn **PENDING**.

| Cổng | Trạng thái | Kết quả |
| --- | --- | --- |
| G0 | BLOCKED một phần | Preflight khớp 4/4 SHA-256. Đã đo nguồn và baseline tuyến trong Unity. SRC-02 còn thiếu đối chiếu BLEND bằng Blender và bằng chứng quyền phát hành. |
| G1 | Chờ duyệt hình thức | Đã lưu mẫu tinh chỉnh cho bốn biến thể OCL, hai hầm và cảnh quan, ba kiểu nền sáu lớp và ba chế độ BIM. 288/288 phép kiểm Unity đạt. Còn hai tiêu chí BLOCKED gồm SRC-02 và OCL-03. |
| G2 Finalization | Đã duyệt theo phạm vi | Ứng viên được ghi dấu bằng manifest và Approval. Phê duyệt chỉ áp dụng cho công trình đạt kiểm tra. |
| G2 Production | Đã lưu và mở lại trong Unity | Chín Zone và BIM, 14.504 ID. Nhà ga giữ OCL hiện hành. Các ngoại lệ giữ riêng trong kết quả. |
| G3 Web | Đã build và kiểm thử thực tế trong Chrome | Tàu chạy 4.900,27 m. Đã xác minh tải lại Zone, các chế độ BIM, lưu Excel và sửa tay qua tải lại và hồi quy Z021. VR NOT TESTED. |

## Các điểm đã chốt bằng phép đo

- Khoảng cách tim hai ray tại mẫu đo là 6,39992 m. Cao độ đáy module ray là 8,93095 m theo tọa độ world. Nửa bề rộng bao quét bảo thủ là 1,76510 m, đỉnh tàu cao 5,57327 m so với đầu ray.
- Mẫu hầm đôi có khẩu độ trong 13,50 m. Chiều cao, khẩu độ và các giả định dựng mẫu được ghi riêng trong `Evidence/Resolved_Geometry.json`.
- SO 201 hiện hành vẫn dài 540,121 m, có 196 mesh ở Zone_018. Đối chiếu cuối xác nhận các file cầu SO 201 được bảo vệ khớp SHA-256. Registry giữ nguyên 17 Zone.
- OCL-03 có bốn mẫu và đã kiểm tra tam giác bản mặt SO 201 tại vị trí gắn chân. Đã đo mesh mái ga đầu trong hành lang ray và khảo sát phần nóc đoàn tàu. Thiếu giao điểm mái có thể đo trong hành lang và thiếu mặt tiếp xúc pantograph xác định được.
- Hai mẫu hầm dài 36 m theo 15 bước vòng 2,4 m. Giữ mặt cắt và sáu lớp nền đã xác minh. Mẫu ray dài dùng chỉ số mesh 32 bit để giữ đủ tam giác của lưới tham chiếu sau cắt.
- Nguồn sáng đặt cạnh từng cụm đèn được chọn trên thành hầm. Vị trí nguồn sáng được đo riêng vì gốc Transform của mesh đèn nằm ở gốc mô-đun.
- Catalog có 1.382 cấu kiện, giữ đủ 840 AssetID từ G1 ban đầu. Hình học quản lý BIM sinh tham số. 17 ảnh ban đầu được đối chiếu SHA-256 với gói Evidence trước đó.

## Dấu vân tay và cách chạy lại

- Unity scene `Assets/ExternalBIM/InfrastructureG1/Infrastructure_G1.unity`.
- Generator và kiểm thử `Assets/Editor/InfrastructureG1*.cs`. Dùng menu **Tools → UTTVR → Infrastructure → Generate and validate G1** trong Edit Mode. Công cụ dùng lại hàm lấy mẫu tam giác, cắt mesh, đo bao tàu và BIM adapter của SO 201.
- Hai lượt dựng cùng input có cùng SHA-256 hình học. Giá trị nằm tại `Evidence/Unity_Geometry_Fingerprint.txt`.
- Manifest ứng viên `Evidence/Candidate_Manifest.csv` ghi SHA-256 theo file. `G1_Refined_Evidence.zip` chứa ảnh Unity mới, 17 ảnh ban đầu và các bằng chứng kiểm thử.
- `py -3 Documentation/InfrastructureDesign/Tools/validate_kit.py preflight --project .`
- `py -3 Documentation/InfrastructureDesign/Tools/validate_kit.py gate --stage G1`

Xem `Evidence/G2_Pilot/Pilot_Report.md` cho ứng viên đã duyệt, `Evidence/Final_Implementation_Report.md` cho tích hợp đã lưu và `Evidence/Production/Web_Test_Report.md` cho nghiệm thu trình duyệt.

## Hoàn thiện điện tiếp xúc và cảnh quan hai ga

- Đã mở lại và kiểm tra 17 Zone sau khi thay cột và dây cũ. Giữ 225 batch OCL đã duyệt ở cầu, hầm và đường thường, bổ sung 293 batch dùng cùng mẫu. Phần mới có 130 cụm đỡ và 133 nhịp dây đôi, độ lệch dây thẳng lớn nhất 0,047 m so với đường tham chiếu.
- Dây kết thúc tại cổng phía tiếp tuyến của ga đầu và ga cuối, lần lượt ở lý trình 40,681 m và 7.919,085 m. Khoảng hở chiếu từ OCL tới mép kết cấu ga đo được là 2,58 m ở mỗi ga. Hai đồi nền được nối bằng phần địa hình nằm dưới đế ga. Cầu SO 201, ray, spline và tàu dùng dữ liệu cũ.
- Dựng lại trong Edit Mode bằng **Tools → UTTVR → Infrastructure → Complete route electrification**. [Ảnh ga đầu](Evidence/Production/Screenshots/RouteElectrification/Start_Station.png), [ảnh ga cuối](Evidence/Production/Screenshots/RouteElectrification/End_Station.png), [ảnh OCL đoạn thường](Evidence/Production/Screenshots/RouteElectrification/Zone_020_OCL.png).
- WebGL build thành công với 0 lỗi. Lượt Chrome từ lý trình 25 đến 7.995,26 m đạt 200 km/h. Đã nạp 17 Zone, dỡ 15 và không ghi nhận Zone bị thiếu hoặc khung hình thiếu ray thiết yếu. Đã thử Tách rồi khôi phục cấu kiện Z021 C024. [Ảnh khi chạy ở Zone_020](Evidence/Production/Screenshots/RouteElectrification/Route_Zone_020.png).
- Trên WebGL với nhóm Đường ray Z021 được chọn, Tách bị vô hiệu hóa. Định vị đưa camera về nhóm, lần bấm sau giữ góc đã định vị. [Ảnh đối chứng nút Định vị](Evidence/Production/Screenshots/RouteElectrification/BIM_Focus_Repeat.png).

## Nối sườn núi quanh hai hầm

- Đã lưu 10 mesh địa hình phủ cỏ trong Zone_012–Zone_016, nối sườn núi Forest_01 và Forest_02 với các dãy núi nền. Mặt cắt được lấy từ mesh hiện có. Phần mới bắt đầu cách tim hầm 62 m, hạ mép xuống dưới bề mặt có sẵn và giảm cao độ quanh đường giao thông.
- Mở lại cả năm Zone trong Unity. Sáu cặp mép mesh qua Zone khớp tọa độ và pháp tuyến. 3.380 mẫu trong hành lang ray và cửa hầm không chạm địa hình bổ sung. Mesh bổ sung dùng cho hiển thị, không gắn collider. Tổng 31.464 tam giác.
- Dựng lại bằng **Tools → UTTVR → Infrastructure → Blend tunnel foothills**. Ảnh cùng góc nhìn [Forest_01 trước](Evidence/Production/Screenshots/TunnelFoothills/Forest_01_Before.png), [Forest_01 sau](Evidence/Production/Screenshots/TunnelFoothills/Forest_01_After.png), [Forest_02 trước](Evidence/Production/Screenshots/TunnelFoothills/Forest_02_Before.png), [Forest_02 sau](Evidence/Production/Screenshots/TunnelFoothills/Forest_02_After.png).
