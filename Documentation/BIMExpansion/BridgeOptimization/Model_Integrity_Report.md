# Kiểm tra tính toàn vẹn cầu Yên Tử – Hạ Long Xanh

## Hình học bản gộp trong Unity · PASS

Tool tạo 56 mesh cho nhịp 20 m và 28 mesh cho các cụm trụ đôi. Mỗi nhóm giữ phạm vi culling riêng. Tổng 50.400 đỉnh và 25.200 tam giác được giữ nguyên. Mỗi slot vẫn dùng đúng vật liệu gốc, và từng bộ chỉ số tam giác giữ winding gốc. Công cụ đối chiếu từng đỉnh, pháp tuyến đã chuẩn hóa, màu đỉnh và các kênh UV hiện có. Các mesh nguồn không có tangent.

| Phép đo theo không gian nhóm | Sai lệch lớn nhất | Giới hạn kiểm tra |
| --- | ---: | ---: |
| Vị trí đỉnh | 0,000000481 m | 0,0001 m |
| Vector pháp tuyến đơn vị | 0,000000123 | 0,0001 |
| Biên bounds đo từ hình học | 0,000003932 m | 0,0001 m |

Mối nối dùng cùng các đỉnh ở đầu nhịp. Cầu vẫn dài 1.120 m, tâm và pose cầu lấy đúng từ instance đang có ở Zone_018. UV, material, winding và bounds được kiểm ở Editor. Hiện tượng nhìn thấy như khe hở, mặt mất và culling được kiểm riêng qua bản Web.

## Bảo toàn tài sản · PASS

601/609 tệp trong snapshot giữ nguyên SHA-256. Tám tệp khác gồm bảy script của camera, cây, điều hướng và probe thử Web, cùng scene Zone_018. Diff scene chỉ thay prefab con trong cùng nhánh hạ tầng cầu, giữ nguyên transform gốc và các đối tượng xung quanh. Đã mở lại Zone_018 chính thức trong Unity và xác nhận prefab mới có 84 renderer được bật, nhánh cầu vẫn được tải, nước còn đúng vị trí và cầu lịch sử ExtendedBridge tắt. Bản Visual trước Phase 2 vẫn ở thư mục prefab để khôi phục. Bản Detailed, Source, toàn bộ mesh nguồn, catalog 420 AssetID, prefab viewer Zone_018 và scene quản lý đều khớp snapshot.

Đối chiếu thêm 73/73 tệp bảo vệ từ Phase 1. Tập này gồm scene tuyến chính, 57 Zone khác, ray, tàu, hai cơ sở dữ liệu ray, Showroom, Login, Build Profile và cấu hình Player.

## Khôi phục

[`Before_Phase2.zip`](Snapshots/Before_Phase2.zip) lưu 609 tệp bên ngoài Temp và đã được đọc lại đối chiếu SHA-256 với [manifest](Snapshots/Before_Phase2.sha256). Giải nén các tệp cần khôi phục vào đúng đường dẫn khi các scene đó đã đóng. Giữ nguyên GUID từ các file `.meta` đi kèm. Đối với thay đổi Visual, khôi phục riêng `Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_018.unity` là đủ để dùng lại prefab Visual trước Phase 2.
