# UTTVR BIM Viewer V2.0

## Hình học và quyền sở hữu

- Geometry Overlap Audit tách trùng instance theo GUID, local file ID và ma trận, trùng hình học theo dấu vết tam giác, giao mặt đồng phẳng theo diện tích, tiếp xúc đối hướng và giao bề mặt cần xem xét. Phép quét giới hạn ứng viên bằng sweep AABB và cây bounds tam giác. Công cụ Editor ở menu **Tools → UTTVR → BIM Viewer → Audit Production Geometry**.
- Quét ban đầu trong YenTuHaLongXanh xác nhận 17 Zone, 162 MicroChunk, không trùng key Resources và không trùng khoảng lý trình logic. Phép đo tam giác lại cho thấy bề mặt thực chồng nhau ở các chunk giáp ranh. Do đó, khoảng chainage riêng và GUID riêng không đủ để kết luận không chồng hình.
- Sửa tại mesh hiển thị và công cụ ownership của generator. Polygon bị trừ phải có bề mặt cùng hướng che phủ thực tế. Vòng chính dùng sai số đồng phẳng 0,35 mm. Với các cặp đã xác nhận còn sát nhau, chỉ trừ lớp nằm sau mặt quan sát trong phạm vi 2 mm. Hình học ngoài vùng phủ, transform, spline và collider canonical được giữ theo nguồn. Không áp dụng offset hay depth bias để xử lý model.
- Phép sửa MicroChunk ghi nhận 2.253,3942 m² diện tích phủ dư ở lượt đầu và 200,6786 m² ở sáu cặp còn lớn. Vỏ hầm và nền đường xử lý 24 mesh Visual với 223,6989 m². Địa hình chuyển tiếp tại Zone_012–015 xử lý sáu mesh Visual với 220,8146 m². Mặt cầu xử lý 816,5195 m² bề mặt nằm sau mặt phủ. Đây là số diện tích phần đã trừ, không phải diện tích công trình hay thể tích giao nhau.
- Visual–Detailed chọn batch trên mọi Zone đã nạp bằng key của chunk. Khi dỡ Detailed, chỉ bật lại đúng renderer đã tắt. Kiểm trực tiếp Tunnel_056 có cấu kiện ở Zone_015 và batch Visual tương ứng lưu trong Zone_016. Highlight sử dụng nguồn renderer hiện hữu cho pass stencil.

| Cặp mặt đã xử lý trên tuyến | Trước (m²) | Sau lượt sửa chính (m²) |
| --- | ---: | ---: |
| Ranh giới MicroChunk giữa hai mesh ray | 2.466,982 | 13,026 |
| Các batch vòng vỏ hầm | 221,728 | 1,048 |
| Lớp nền trên và bê tông gia cố | 3,166 | 0,187 |
| Mặt cầu và lớp áo mặt | 1.349,604 | 560,941 |

Các số ở bảng dùng cùng phương pháp quét trước/sau cho những cặp mesh liên quan và sai số 2 mm. Đây là tổng diện tích giao nhau tính theo từng cặp tam giác. Các dòng `REVIEW` là ứng viên có chứng cứ giao mặt hoặc giao thể tích, gồm cả phần liên kết có chủ đích. Chỉ phần dư thừa có thể xác định owner và vùng che phủ mới được xử lý tự động.

Lượt quét mở rộng kiểm 1.148 nguồn mesh trên tuyến và 1.285 nguồn mesh trong BIM Viewer. Trong đó có 157 và 185 mesh import đọc qua API chỉ đọc của Unity Editor. Có 11 và 20 nguồn không có tam giác hiển thị. Quét toàn bộ không phát hiện renderer trùng chính xác theo GUID, local ID và transform, hoặc cặp mesh trùng toàn bộ tập tam giác.

Lượt cuối xử lý 4.111,269 m² mặt dư cùng lớp vật liệu bên trong 324 mesh ray gộp. Số đo cùng hướng trong từng mesh giảm từ khoảng 7.922,49 xuống 3.818,081 m² theo ngưỡng rộng 2 mm. Cả hai Scene dùng trực tiếp cùng mesh ray đã sửa. Ở mẫu L_0108_b, phép quét bổ sung phân nhóm 14,13 m² theo khoảng cách mặt phẳng và màu đỉnh. Khoảng 0,195 m² nằm trong ngưỡng 0,35 mm. Một phần có màu đỉnh khác. Chưa xác định các phần gần đồng phẳng còn lại đều gây nhấp nháy. Giữ nguyên chúng để tránh xóa bề mặt kỹ thuật và lớp vật liệu vẫn cần hiển thị.

Phép quét riêng các cặp mặt cầu và địa hình vừa sửa cho tổng còn 0,007 m² trên tuyến và 0,024 m² trong BIM Viewer. Kiểm tra bên trong các batch vỏ hầm và nền đường sau sửa lần cuối còn lần lượt khoảng 0,42 và 12,18 m² theo ngưỡng 2 mm. CSV giữ kết quả quét tổng thể và phân nhóm các lượt kiểm tập trung sau cùng. Các vùng tiếp xúc quanh đài, lớp phủ cầu và nền tự nhiên vẫn có mục cần đánh giá theo thiết kế kết cấu.

## Cây BIM, hồ sơ và toolbar

- Sáu đoạn quản lý logic được xác định từ các công trình và Zone hiện có. Forest_02 gồm Zone_012–013, Forest_01 gồm Zone_014–015. OCL khu cầu theo khóa tài sản Bridge; các occurrence OCL đường thường cùng Zone_017 được phân vào đoạn ngoài trời. Cây giữ 47.766 cấu kiện thống nhất, gồm 14.504 ID hạ tầng Production, cầu SO 201 và dữ liệu ray Z021. Registry chạy tàu vẫn xác nhận 17 Zone.
- Bộ hồ sơ chuẩn hóa chọn theo nhóm cấu kiện và AssetID ổn định, 150 mẫu tại mỗi đoạn, tổng 900 mẫu. Nhóm hồ sơ chọn được cả tuyến, đoạn, hệ thống, phân loại, cụm và cấu kiện. Các mẫu có vật liệu dự kiến, yêu cầu kỹ thuật theo loại, sáu giai đoạn và provenance. Phép đo diện tích lấy từ mesh có thể đọc. Thể tích chỉ hiện khi các cạnh của mesh tạo vỏ khép kín đúng hướng. Cách tách các đại lượng hình học tham khảo IFC 4.3. [buildingSMART IfcElementQuantity](https://ifc43-docs.standards.buildingsmart.org/IFC/RELEASE/IFC4x3/HTML/lexical/IfcElementQuantity.htm)
- Bộ lọc Tất cả, Có hồ sơ và Chưa có hồ sơ giữ nhánh cha phù hợp và dùng chung tìm kiếm. Hồ sơ đã có được xét riêng qua các trường kỹ thuật và sáu giai đoạn. Phạm vi hiệu ứng của nhóm vẫn gồm các cấu kiện đang bị bộ lọc ẩn.
- Toolbar có Tách, Mặt cắt, Định vị, Cô lập và X-ray. Khi kết hợp, khôi phục trạng thái gốc rồi cập nhật theo thứ tự định vị, cô lập, tách, cắt và X-ray. Bộ kiểm Editor xác nhận ba vòng bật/tắt phối hợp khôi phục transform, mesh, collider, visibility, material và camera. Định vị vẫn cho orbit, pan và zoom.
- Với phạm vi đủ nhỏ, Viewer nạp tối đa ba chunk Detailed với tổng cộng tối đa 450 cấu kiện. Các phạm vi rộng giữ đầy đủ AssetID và dùng các batch đang nạp. Một batch hỗn hợp có thể chứa thêm cấu kiện lân cận khi xem theo loại ở nhiều chunk. Phạm vi toàn tuyến giữ danh sách đầy đủ và áp dụng hiệu ứng hình ảnh trên các đại diện hiện tải.

## Phạm vi lý trình theo dữ liệu tài sản

| Đoạn logic | Zone dữ liệu | Lý trình nhỏ nhất–lớn nhất (m) | Cấu kiện | Hồ sơ mẫu |
| --- | --- | ---: | ---: | ---: |
| 01 · Nền đường | 011 | 310,000–790,000 | 856 | 150 |
| 02 · Forest_02 | 012, 013 | 1.067,000–1.733,000 | 5.756 | 150 |
| 03 · Forest_01 | 014, 015 | 2.167,000–2.800,000 | 5.698 | 150 |
| 04 · Điện tiếp xúc ngoài trời | 016, 017 | 2.815,000–3.500,000 | 1.048 | 150 |
| 05 · Khu cầu SO 201 | 017, 018 | 3.704,001–4.224,315 | 1.342 | 150 |
| 06 · Ray Z021 | 021 | 5.300,001–5.801,001 | 33.066 | 150 |

Các khoảng này lấy từ lý trình tài sản trong registry, catalog cầu và SQLite ray, gồm các công trình tiếp cận trong mỗi đoạn. Chúng không được dùng làm ranh giới tải Scene.

## Kiểm thử Editor

Đã đối chiếu 6 đoạn, 47.766 cấu kiện lá duy nhất, 900 mẫu, hai bộ lọc, nhánh cầu, Z021 C005 và phạm vi Forest_02 đi qua Zone_012–013. Đã thử ba vòng restore và hai lượt nạp/dỡ Detailed của Tunnel_056. Khi nạp hai chunk hầm 55–56, kiểm tra xác nhận 369 cấu kiện Detailed được nạp và đúng tám batch Visual được tắt rồi khôi phục.

## Kiểm thử Web

Unity 6000.3.0f1 tạo một build Web Development gồm 24 Scene, gồm Login, hai Scene trong phạm vi và các Zone. Build mất 14 phút 33 giây, 0 lỗi, 1.064 cảnh báo. Console có các cảnh báo script Decal Projector thiếu ở UTTCampus, importer phông chữ và tam giác collider lớn. Phạm vi kiểm thử lần này là tuyến tàu và BIM Viewer.

Chrome chạy thực tế từ mốc 25 m đến 4.900,27 m, tốc độ cao nhất 200 km/h. Tám vị trí lấy mẫu qua nền đường, hai hầm, OCL, cầu SO 201 và đầu ra cầu. Cả hai bản nạp 12 Zone, dỡ 9 Zone và ghi 0 mẫu tàu trong Zone thiếu. Các chuỗi ảnh runtime giữ được ray, nền và hình dạng cầu ở những góc quan sát đã lấy mẫu. Phép kiểm này chỉ bao phủ những vị trí và góc đã lấy mẫu.

| Chỉ số runtime tuyến | Bản trước | BIM Viewer V2 |
| --- | ---: | ---: |
| Frame time p95 / p99 | 9,00 / 9,00 ms | 9,00 / 9,00 ms |
| Renderer hoạt động cao nhất trong các mẫu | 607 | 607 |
| Draw calls trung vị tại 8 vị trí | 254,5 | 260 |
| Bộ nhớ Unity tại 8 vị trí | 533,3–613,1 MiB | 643,3–723,8 MiB |

Bộ nhớ đo trong bài chạy tăng khoảng 110 MiB. Chỉ số là phép đo trong Chrome headless ở 1.280 × 720. Chưa đo trên Quest.

Trên BIM Viewer, Chrome xác nhận 46 lần bật/tắt được ghi nhận qua cấp đoạn nền đường, cấu kiện, toàn cầu SO 201, Forest_02 và toàn tuyến. Đã thử đồng thời X-ray, mặt cắt và cô lập trên một cấu kiện, đổi node khi X-ray đang bật, orbit/pan/zoom khi định vị cầu, chọn Z021 C005 và chọn bằng cách click mô hình. Ba trạng thái lọc phản hồi đúng. Forest_02 nạp Zone_012 và Zone_013, dỡ khi đổi đoạn và nạp lại được. Hồ sơ thử được xuất Excel, nhập vào profile trình duyệt thử, lưu trong IndexedDB và mở lại thành công sau tải lại trang. Frame time trung vị của 44 mẫu log BIM là 9 ms. Không ghi nhận các exception runtime BIM trong bộ log kiểm tra.

Bản V2 dùng thư mục `Builds/UTTVR_BIM_Viewer_V2_Web` và máy chủ thử tại `http://127.0.0.1:8818/?offline=1`. Bản Production hiện hữu vẫn phục vụ ở cổng 8817 trong khi các điểm geometry còn phải xem xét và chênh lệch bộ nhớ được ghi nhận. Cổng 8818 dùng origin riêng, nên lần kiểm Excel không ghi vào IndexedDB của cổng 8817.

## Khôi phục

- Mesh ray được sửa tại `Assets/ProjectLopHoc/Generated/QuestRailway/Resources/YenTuQuestRails`. Các file này sạch trước tác vụ và có các blob trong Git. Chỉ khôi phục các mesh liệt kê ở Validation Results nếu cần quay lại.
- Các file nhẹ đã sửa tại chỗ ở Zone và prefab BIM, cùng pack Visual liên quan, có bản trước tác vụ trong `Temp/BimViewerV2Rollback`. Khôi phục cả cặp scene/prefab và pack tương ứng để tránh lệch GUID. Không đụng catalog, IFC hoặc SQLite.
- Mã thay đổi tập trung ở thư mục Lifecycle, InfrastructureDetailedLoader, RailwayMicroChunkStreamer và các công cụ Editor Ownership/Audit. Shader X-ray nằm trong Resources. Báo cáo chi tiết đổi file nằm ở Validation Results.

## Điều chỉnh theo kiểm tra của người dùng · Deck Girder

`EBS_Rail_2000_Prefab` có một cụm `Deck Girder` lồng bên dưới rộng 5,4 m. `Mesh_12` là tấm đế EBS riêng rộng khoảng 2,45 m. Chỉ cụm `Deck Girder` được loại khỏi mesh hiển thị trên tuyến. Công cụ nhận diện prefab lồng và loại đúng các material nguồn ở bước sinh MicroChunk. Lượt cập nhật mesh hiện có đối chiếu màu đỉnh nhúng theo nguồn và cao độ từng mặt trước khi bỏ chỉ số tam giác.

Editor xác nhận 248.507 tam giác Deck Girder đã được bỏ khỏi 324 mesh hiển thị, 78.277 tam giác của `Mesh_12` còn nguyên. Các mesh giữ nguyên asset ID để tuyến tàu và prefab vùng BIM tiếp tục chia sẻ tham chiếu. Prefab nguồn, hình học cầu độc lập, hình học chi tiết Z021 và hồ sơ BIM được giữ nguyên. Kiểm tra mẫu `L_0108_b` cho thấy vùng gần đồng phẳng ở ngưỡng 2 mm giảm từ 13,862 xuống 13,755 m². Deck Girder chỉ giải thích một phần nhỏ vùng gần đồng phẳng trong mẫu này.

Build Web cập nhật ở cổng 8818 hoàn tất với 0 lỗi. Ảnh trước/sau trong `BimViewerV2/09_DeckGirder_Before.png` và `10_DeckGirder_After.png` xác nhận dải trắng rộng quanh ray đã biến mất tại đoạn nền đường kiểm tra. `11_DeckGirder_BIM_After.png` cho thấy cùng thay đổi khi chọn lớp nền ở lý trình 411,111 m trong BIM Viewer. Chrome vẫn hiển thị cầu và chạy tàu đến 4.900,22 m, nạp 12 Zone, dỡ 9 và ghi 0 mẫu Zone thiếu.

## Nền đỡ toàn tuyến và bệ ray trên cầu

Registry thực tế trải dài 7.999,98 m qua 17 Zone. Trình tạo nền lấy mẫu mặt dưới `Mesh_12`, Terrain, nền gia cố, đáy hầm và mặt cầu theo hình học đang dùng. Nền đắp ngoài trời nối xuống mặt địa hình và hai cánh gặp nhau giữa tim ray. Trên cầu dùng bệ bê tông hẹp màu xám tựa trên mặt cầu, trong hầm dùng bê tông xanh xám tựa trên đáy hầm. Hình học cầu SO 201 và tuyến spline giữ nguyên.

Kiểm tra Editor đối chiếu từng mesh với số tam giác ray trước thay đổi. Giữ toàn bộ tam giác ray gốc của bản Deck Girder đã loại, thêm 32.640 tam giác nền trong 324 tài nguyên MicroChunk. Phép quét xác nhận không còn chỉ số tam giác tham chiếu màu đỉnh của Deck Girder trong các mesh ray đã xử lý. Z021 chi tiết có một mesh hỗ trợ riêng, đã bỏ 13.328 tam giác Deck Girder cũ, thay bằng 2.040 tam giác nền lấy từ 20 đoạn profile cùng tuyến. Các ID và khả năng chọn cấu kiện Z021 dùng dữ liệu gốc.

Trong bản Web dùng làm mốc trước thay đổi camera, tàu chạy đến 7.995,17 m ở tốc độ cao nhất 200 km/h. Nạp đủ 17 Zone, dỡ 15 Zone, 0 mẫu thiếu Zone và 0 khung hình thiếu MicroChunk cần thiết. Có ảnh nền ở Zone_026, bệ ray trên SO 201 và thao tác chọn cấu kiện Z021 C005.

## Nhấp nháy khi zoom xa

Phép thử Unity Editor giữ cùng hướng, điểm nhìn, ánh sáng và vật liệu ở ba khoảng cách 22, 105 và 280 m. Thiết bị dùng Direct3D 12 và reversed-Z. So sánh Near Clip cũ với 0,26 / 1,26 / 2,50 m chỉ làm thay đổi 1–6 pixel vượt ngưỡng trên khung 960 × 540 ở các mẫu tuyến và cầu. Kết quả Editor không đủ để loại trừ khác biệt độ sâu trên WebGL.

Camera BIM dùng khoảng chiếu của bounds trong khung nhìn để điều chỉnh near. Far dùng giới hạn khoảng cách bao quanh mọi renderer đang theo dõi, kể cả renderer vừa nằm ngoài khung hình. Lượt Web đầu phát hiện cầu bị cắt ở một góc nhìn thử khi Far Clip thu quá sớm. Giới hạn đã được sửa và camera đến thẳng vùng mới khi định vị qua các Zone xa nhau. Camera ngoài tàu desktop cũng chọn khoảng hiển thị theo vùng streaming và trả lại clipping ban đầu khi về cabin. Phép kiểm gần 4 cm tính từ mặt mẫu vẫn giữ được toàn bộ mặt trước, với near 1,8 cm. Ở mẫu Editor 280 m sau hiệu chỉnh, near thích ứng là 3,36 m và far 565,91 m.

Kiểm kê lại 1.148 vị trí renderer tuyến và 1.285 vị trí BIM không tìm thấy cặp trùng chính xác mesh và transform. Hai mẫu mesh ray vẫn có diện tích gần đồng phẳng khoảng 10,16 và 13,69 m² ở ngưỡng 2 mm. Không có đủ chứng cứ để bỏ thêm hình học trong các cặp gần này. Tọa độ X trong registry là 5.133,66–12.902,96 m. Bước biểu diễn float ở đầu lớn khoảng 0,98 mm. Tọa độ spline, tàu và cấu kiện được giữ nguyên.

Bộ ẩn renderer chỉ áp dụng cho cấu kiện bị một lưới kín, lồi, đục bao trọn theo kiểm tra các góc của bounding box. Collider, AssetID và dữ liệu canonical được giữ. Camera đi vào bounds của vỏ, chọn trực tiếp cấu kiện, hoặc bật X-ray, Tách, Mặt cắt hay Cô lập sẽ khôi phục renderer tương ứng. Kiểm thử dương tính với cấu kiện lồng kín, âm tính với tiếp xúc và đồng vị trí. Không xác nhận được khối kín lồng nhau ở 196 renderer của cầu IFC và mẫu Tunnel_056, nên không ẩn cưỡng bức các đối tượng đó.

Với nhóm có Detailed, zoom xa chuyển về Visual và giữ đầy đủ tập AssetID. Zoom lại gần hoặc bật chế độ kiểm tra khôi phục Detailed. Trình nạp tắt phiên bản đang hiện trước khi bật phiên bản còn lại. Mẫu Tunnel_056 đã qua ba chu kỳ Visual–Detailed với hai batch Visual tương ứng.

Shader ray có Queue Geometry, alpha đầu ra 1, ZWrite bật và không đọc shadow map ở pass màu. Shader X-ray hiện hữu chỉ dùng transparency khi bật chế độ. URP của project hiện đặt MSAA 1. Chưa thay đổi cấu hình đồ họa toàn project.

WebGL dùng OpenGLES3 và không dùng reversed-Z trong phép kiểm Chrome. Ở mẫu tuyến 280 m, tăng near từ 0,03 lên 2,50 m với far cố định 3.500 m làm thay đổi khoảng 9.002 pixel trong vùng so sánh 624.442 pixel, đồng thời giảm các vệt nhiễu thấy dọc nền ray. Tắt shadow ở cùng phép thử thích ứng không đổi pixel trong vùng đo. Điều này chỉ xác nhận ảnh hưởng của depth ở mẫu tuyến và góc quan sát này.

Mẫu cầu BIM ở 280 m giữ được cầu hiển thị với near 3,36 m và far 1.531,62 m sau sửa giới hạn xa. Dùng ảnh cầu đã xác nhận có mô hình cho so sánh khung cố định trên Web, bỏ ảnh thử phụ của nền đường vì khung cố định không chứa mục tiêu. Kiểm tra UI thông thường vẫn zoom gần được cấu kiện nền, định vị được cầu và chọn được cấu kiện Z021 C005. Chrome ghi nhận chuyển Visual khi zoom xa nhóm S0400-000 và chuyển về Detailed khi bật X-ray. Không thay đổi canonical geometry ở bước xử lý clipping/visibility.

Bản Web cuối hoàn tất với 0 lỗi build. Lượt chạy thực tế đến 7.995,18 m, đạt 200 km/h, nạp 17 Zone, dỡ 15 Zone, 0 mẫu Zone thiếu qua 15 vị trí kiểm tra và 0 khung hình thiếu MicroChunk cần thiết. Bản thử tiếp tục dùng cổng 8818. Hardware Quest chưa được kiểm thử.

## Đối chứng riêng cho cụm liên kết ray Z021

Đã kiểm tra tấm đệm, bản đế, gối đỡ và các chi tiết liên kết bằng cách tắt từng renderer tại cùng góc nhìn. Báo cáo kèm ảnh và số liệu theo khoảng cách. [Đối chứng liên kết ray Z021](BimViewerV2/Fasteners/Verification.md).
