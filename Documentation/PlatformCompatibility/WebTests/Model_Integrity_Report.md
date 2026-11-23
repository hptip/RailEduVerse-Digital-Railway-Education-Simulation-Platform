# Đối chiếu tính toàn vẹn mô hình UTTVR

Baseline lấy từ `F:/GITHUB/UTTVR`, Unity 6000.3.0f1, tại target Android trước khi đổi target WebGL. Unity mở từng scene dưới dạng preview để đếm đối tượng mà không lưu. Danh sách scene lấy trực tiếp từ Web Build Profile gồm Login, Campus, Classroom, Showroom, Yên Tử, Quản lý vòng đời tài sản, ChiTietSaBan và 17 Zone đã đăng ký.

## Bằng chứng tĩnh

- `Evidence/Before/Model_Asset_Paths.txt` là danh sách dependency từ `AssetDatabase`.
- `Evidence/Before/Model_Asset_SHA256.csv` gồm SHA-256 của scene, dependency dưới `Assets`, các file `.meta`, Resources động của tuyến, cấu hình XR và hai SQLite. Scene, prefab và `.meta` giống byte là cơ sở đối chiếu GUID, cấu trúc, Transform và tham chiếu được serialize.
- `Evidence/Before/Scene_Model_Counts.csv` ghi GameObject, Renderer, MeshFilter, SkinnedMeshRenderer, material slots và null slots theo scene, gồm cả prefab đã resolve. Slot trống tồn tại từ baseline cần được tách khỏi lỗi phát sinh ở bản mới.

## Phạm vi được bảo vệ

| Scene | Đối tượng ưu tiên |
|---|---|
| Login | Scene, background, UI gốc và XR rig |
| UTTCampus | Trường, công trình, đường đi, cây, collider và prefab dùng chung |
| Classroom | Nội thất, bảng, màn chiếu, bàn ghế |
| Showroom | Sa bàn, thân/toa/bánh, ray, cầu, ga, địa hình, các lớp hiển thị |
| YenTuHaLongXanh và Zone | Spline, tàu, ghế, ga, cầu, hầm, ray, terrain và pool |
| QuanLyVongDoiTaiSan và ChiTietSaBan | Cấu kiện BIM, mapping, model IDs, mesh, collider và material |

## Giới hạn nền tảng

Kiểm tra tĩnh không thay thế kiểm thử rendering hoặc điều khiển trên Quest. Không có thiết bị Quest trong đợt này. Các phần động và khu vực đường sắt chỉ được xác nhận ở những ảnh hoặc thao tác trình duyệt đã ghi lại.

## Kết quả sau build

- **4.362 / 4.362 file khớp SHA-256.** Xem `Evidence/After/Model_Asset_SHA256_Comparison.csv`. Các thay đổi do Unity tự ghi khi đổi target được khôi phục theo byte baseline. Hai SQLite nguồn, `Packages/manifest.json`, cấu hình XR và Scene List Android vẫn khớp.
- **24 / 24 scene khớp toàn bộ số đếm.** Hai file `Scene_Model_Counts.csv` trước và sau được xuất từ Unity Preview Scene. Showroom có 94.827 GameObject, 53.763 Renderer, 53.763 MeshFilter và 55.222 material slot ở cả hai lượt. Scene, prefab, mesh, material, importer `.meta`, tham chiếu và Transform được đối chiếu trong phạm vi danh sách dependency đã chụp.
- Chrome đã hiển thị Campus, nội thất Classroom, thân tàu và sa bàn Showroom, nội thất toa Yên Tử và cấu kiện BIM ở góc thử. Các ảnh gốc trước/sau nằm tại `Evidence/Before/Evidence` và `Evidence/After/Release/Evidence`. Showroom dùng đường render/instancing có sẵn. Các ảnh tổng quan không cho thấy thay đổi cấu trúc thân/toa. Màu trạng thái Play, Pause, x4 và tắt tám lớp khớp trước/sau. Chưa có góc cận cảnh tàu chạy qua từng đoạn ray, cầu, ga và núi.
- `Evidence/After/Model_Visual_Comparison.csv` ghi mẫu RGB ở các vùng cố định của cặp ảnh. Với ngưỡng chênh 8 trên một kênh, vùng thân tàu Showroom có 4/1.634 mẫu chênh, vùng mô hình BIM 8/9.900 và các vùng kiểm tra Login, Campus, Classroom, toa Yên Tử ghi 0. Phép đo chỉ áp dụng cho vùng ảnh và góc thử đã ghi.
- Tại Yên Tử, đã đi trong toa bằng WASD và thử phím F bằng hai vai trò, chưa ghi nhận HUD ghế lái hoặc chứng minh đoàn tàu vượt ranh giới Zone trong đợt này. Chưa nghiệm thu hình ảnh động ở ga, cầu và hầm.
- Unity đã về target Android, profile tùy chỉnh đang chọn là `null` như ban đầu, và kiểm tra biên dịch Editor trở về thành công. Quest standalone chưa kiểm thử.
