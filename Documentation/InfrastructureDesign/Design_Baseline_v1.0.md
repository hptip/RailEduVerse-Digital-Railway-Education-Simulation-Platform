# UTTVR Railway Infrastructure — Design Baseline v1.0
**Trạng thái:** 16 lựa chọn do người dùng duyệt, bổ sung núi bao quanh hầm. Ảnh concept là tham chiếu, mẫu Unity vẫn phải được người dùng duyệt. Đây là mô phỏng, không phải thiết kế kỹ thuật được chứng nhận.

## 1. Phạm vi và các lựa chọn khóa
| Mã | Quyết định |
|---|---|
| 01 | OCL kết hợp cột độc lập và cổng điện |
| 02 | Cột thép chữ H |
| 03 | Dây và phụ kiện chi tiết cao |
| 04 | BIM từng cấu kiện vật lý, kể cả phụ kiện nhỏ có hình học |
| 05 | OCL đường thường, cầu, hầm, ga |
| 06 | Mỗi hầm chứa cả hai đường ray |
| 07 | Cửa vòm bê tông cắt xiên |
| 08 | Vỏ hầm lắp ghép từng tấm |
| 09 | Nội thất kỹ thuật hoàn chỉnh |
| 10 | Hai hầm khác phong cách cảnh quan |
| 11 | Nền đất, mái gia cố và tường chắn kết hợp |
| 12 | Ô dầm bê tông hình thoi |
| 13 | Nền đầy đủ các lớp hình học |
| 14 | Exploded, Section, Isolation/Transparency |
| 15 | Module vật lý, ID duy nhất ổn định |
| 16 | Phong cách cao tốc Trung Quốc hiện đại |
| 17 | Núi bao phủ thân hầm, hòa địa hình hai bên |

Chỉ dùng tài nguyên/công cụ miễn phí; không mua thêm. Bảo vệ SO201 được người dùng chấp nhận (đo phiên bản hiện hành, không mặc định chiều dài cũ). Giữ tàu, spline, ray, ga hiện hành, UI/menu năm scene, login, XR, SQLite ray và dữ liệu người dùng. Không giới hạn cứng số tài sản; tối ưu hiển thị thay vì bỏ chi tiết BIM.

## 2. OCL — tham chiếu 01
Thép chữ H mạ kẽm bạc, móng bê tông xám sáng, cần đỡ kim loại, cách điện nâu đỏ hoặc màu trung tính phù hợp nguồn, dây màu kim loại/đồng không quá rực. Sử dụng FBX và BLEND đối chiếu vật liệu. Phân loại mesh bằng hình học/quan hệ, không suy diễn từ tên Cube. Giữ cụm nguồn hoàn chỉnh nếu phù hợp; bổ sung chi tiết còn thiếu có hình học thật. Móng, bản đế, bu lông, thân cột, cần chính, giằng, cách điện, tay định vị, dây chịu lực, dây tiếp xúc, dây treo, kẹp và phụ kiện thực tế. Không gán hồ sơ giả cho chi tiết không tồn tại.
- Đường thường: hai bên tuyến, mỗi ray cần đỡ riêng. Hai cụm liên tiếp và dây giữa chúng là mẫu tối thiểu.
- Cầu: cột hoặc cổng có chân đỡ hợp lý trên mặt cầu hiện hành; không sửa mesh, kết cấu, placement hoặc DB cầu. Nếu không đủ chỗ thì BLOCKED và báo vị trí; không giả PASS.
- Hầm: giá đỡ gắn thành/vòm, bố trí dây phù hợp mặt cắt.
- Ga: cổng ngang vượt hai ray, không xuyên mái/sân ga.
- Dây liên tục mỗi ray và qua chuyển tiếp; kiểm tra điểm treo, độ võng dây chịu lực, cao độ dây tiếp xúc, pantograph/bao tàu (nếu có), khoảng hở và chuyển Zone. Không đòi dây tiếp xúc tránh điểm tiếp xúc pantograph dự kiến. Mỗi span/dây/chi tiết có owner duy nhất, không trùng tại seam.

## 3. Tunnel — tham chiếu 02
Một lòng hầm đôi, cửa vòm cắt xiên, bê tông sáng, vỏ segment có đường nối rõ nhưng không hở bất thường. Hai biến thể: Hầm 01 núi rừng xanh, mái gia cố gần cửa; Hầm 02 núi đá xen cây, tường cánh rộng và rãnh thoát nước. Cả hai cùng kiểu cửa và module nội thất, khác cảnh quan. Gán tên theo vị trí thật sau kiểm kê; giữ vị trí/chiều dài hầm hiện có trừ điều chỉnh cục bộ cần thiết, báo rõ thay đổi; không sửa spline.
- Đo bao tàu và cả hai tim ray trước; xác định hình dạng tiết diện, chiều dày, số tấm và chiều dài vòng. Không scale toàn bộ IFC đơn tuyến để ép vừa. Tái dùng phần thích hợp từ LoD5; nếu không hợp thì tạo vỏ tham số hóa, giữ provenance.
- Nội thất: bản đáy, vỏ từng tấm, mối nối, lối đi kỹ thuật và lan can, máng cáp/nắp, rãnh thoát nước, đèn trắng trung tính, giá đỡ/dây OCL, biển thoát hiểm xanh, tủ kỹ thuật, thiết bị thông tin và an toàn mô phỏng phù hợp. Không thêm ray IFC chồng ray hiện có. Phần tử không gian không render như tài sản vật lý.
- Núi: bao kín nóc và hai bên thân hầm, sườn rộng và tự nhiên, chuyển tiếp cửa hầm vào địa hình; không tạo ống bê tông lộ thiên hay núi như dải đất hẹp. Lòng hầm thông suốt, không terrain/đá/cây xuyên vào; kiểm tra cả collider. Tạo bản terrain/mountain dẫn xuất riêng, không chỉnh shared mesh Showroom. Dùng cây/đá có sẵn; kiểm soát density và đèn runtime theo Zone, ưu tiên emissive/baked nếu phù hợp Web.

## 4. Subgrade — tham chiếu 03
So sánh UT-Earthworks-01 với nền hiện tại đúng một lượt, cùng góc và mặt cắt. Nếu không đẹp hơn hoặc không tương thích, tự sinh theo spline; không cố sửa IFC bằng nhiều vòng. Nền đất tự nhiên ở vùng thấp, nền đắp có mái gia cố ở vùng phù hợp, tường chắn nơi không gian hẹp. Dầm bê tông hình thoi, cỏ trong ô, dầm đỉnh/chân mái, rãnh thoát nước; không chồng ô hoặc rãnh treo.
Các lớp vật lý: lớp đỡ ray hiện có (tham chiếu, không nhân bản), lớp nền trên, cấp phối/gia cố, đất đắp trên, thân nền, xử lý nền và nền tự nhiên theo điều kiện địa hình. Máng cáp và thoát nước là hai hệ riêng. Tận dụng hình học IFC hầm nếu phù hợp. Nền phải đỡ ray và hòa terrain, không tạo gò dài hẹp như Approach cũ. Ẩn lớp bên trong trong Visual nếu không cần; Detailed giữ geometry và hồ sơ từng lớp. Không tạo cọc xử lý nền sâu ngoài phạm vi đã duyệt.

## 5. BIM Viewer và dữ liệu
Giữ tree hiện hành: Tuyến → Đoạn tuyến/Zone → domain → cụm/module → leaf vật lý. Group chỉ dùng điều hướng/focus; không thêm bản ghi trùng. Giữ ba tab Danh sách / Thông số / Vòng đời và 6 giai đoạn (Quy hoạch, Thiết kế, Thi công, Vận hành, Bảo trì, Nâng cấp). AssetID ổn định theo domain, vị trí/slot vật lý, loại và occurrence; không dùng GlobalId nguồn làm khóa duy nhất của bản lặp. Giữ GlobalId thật nếu có, FBX dùng SourceMeshId, geometry sinh dùng generator/config key. Dữ liệu IFC chỉ đọc, số đo Unity riêng, thông tin mô phỏng ghi nhãn rõ, dữ liệu người dùng ưu tiên, seed ổn định; lưu/reopen không reset. Giữ Excel và DB hiện hành; domain mới theo adapter/catalog đang dùng, không rewrite UI.
- Exploded: tách cục bộ theo cụm đang chọn, có mức độ và reset; không sửa transform gốc hoặc physics tuyến.
- Section: mặt cắt ngang/dọc có thể dịch chuyển, hiển thị phần bị che, clipping và picking phải nhất quán; không phá mesh/material nguồn.
- Isolation: ẩn/hiện hoặc transparency các phần không chọn; chọn từ tree và 3D đồng bộ; reset phục hồi renderer, collider/picking, camera phù hợp và trạng thái ban đầu. Không chọn vật đã ẩn/cắt. Chuyển Zone hoặc thoát viewer không để trạng thái rò rỉ. Dùng chung cả domain cũ và mới, không làm mất chức năng cầu/ray.

## 6. Chuẩn trực quan và thứ tự ưu tiên
Bê tông #D0D3D1, thép #9DAFB9, cách điện #98604A, dây #B37C44, núi #4A7353, cỏ #78945E, đất #AA957E, đèn #E5E7D5 là định hướng màu, không bắt buộc pixel exact. Các ảnh concept AI không phải bản vẽ kỹ thuật, không có số đo được phê duyệt; bỏ qua mọi số, tiêu chuẩn, tên dự án/lý trình, tốc độ, điện áp, chữ sai, tàu và cầu khác trong ảnh. Hình học/spline/tàu hiện hành và tài liệu này ưu tiên hơn ảnh. Kích thước chưa biết để null, đo thực tế và ghi nguồn/giả định/ngưỡng trong Resolved_Geometry.json. Mọi thay đổi kiến trúc phải được người dùng chấp thuận.
