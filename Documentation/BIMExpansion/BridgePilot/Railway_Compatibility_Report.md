# Đối chiếu cầu IFC với cầu trên tuyến Yên Tử – Hạ Long

**Kết luận.** SO 201 phù hợp để kiểm chứng BIM chi tiết. Chênh lệch chiều dài, bề rộng mặt đường và hệ tọa độ khiến nó chưa phù hợp để thay cầu trên tuyến. Bài kiểm tra thực hiện trong scene riêng.

| Hạng mục | Cầu IFC SO 201 | Cầu hiện hành Zone_018 |
| --- | --- | --- |
| Chiều dài | Toàn bộ hình học có AABB theo X 89,321 m. `IfcPavement` chiếu lên trục nối tâm P2–P3 dài khoảng **89,05 m**, có phần tiếp cận. | Registry `Bridge_Main` từ **3.650,0015 đến 4.770,0015 m**, dài **1.120 m**. Owner Zone_018, giữ đồng thời Zone_017–019. |
| Các khoảng đỡ | `IfcBridgePart` ghi Substructure, O1/P2/P3/O4. Chiếu tâm bounds của thân mố/trụ lên trục P2–P3 cho các vị trí khoảng -17,55 / 0 / 34,00 / 51,55 m. Suy ra ba khoảng đỡ 17,55 / 34 / 17,55 m, chưa phải khẩu độ thiết kế hoặc kích thước từ bản vẽ. | Generator bố trí deck theo đoạn 20 m (56 đoạn hình học), đặt trụ đôi mỗi 40 m ở 28 vị trí. Đây là nhịp lặp trong model gộp và không có IFC của cầu hiện hành để xác nhận số nhịp kết cấu. |
| Bề rộng | Mặt đường `IfcPavement` có bao chiếu ngang khoảng **7,54 m** theo pháp tuyến trục P2–P3. Không có `IfcRail`, `IfcTrackElement` hay `IfcAlignment` trong file. Tổng rộng bao gồm móng, đất, tường và phần phụ, không dùng làm khổ mặt cầu. | Generator dùng deck mô hình **14 m**, ray đôi lệch tim **6,400022 m**, còn phải giữ khổ giới hạn động, lan can và OCL. Chiều rộng IFC chưa phù hợp để xác nhận ray đôi an toàn. |
| Cao độ và tọa độ | IFC dùng metre, có `IfcProjectedCRS` `EPSG:5514`, map conversion offset 0, scale 1. Cao độ đỉnh/đáy toàn mô hình **504,712/476,950 m** theo Z nguồn. DAE đã center giữ offset có thể đối chiếu. | Dùng Unity local và Spline `Rail_Spline_Route`, tay ray và nền cầu đặt theo `RailwayMicroChunkData` và cao độ đáy mesh ray. Chưa có phép biến đổi geodetic IFC → Unity tuyến đã được chấp thuận. |
| Streaming và rendering | Prefab thử có 91 renderer, 91 MeshCollider, 36.099 tam giác. Cho phép xem hồ sơ/pick từng cấu kiện. | `ExtendedBridgeQuest` là một mesh gộp, nước là renderer thứ hai, không có collider cầu riêng trong nhánh đã lưu. `RailwayZoneStreamingManager` giữ nhóm theo chainage. Thay trực tiếp bằng prefab chi tiết làm đổi số renderer/collider và cần đo Web/Quest, đặc biệt khi nạp và unload. |

[Registry Yên Tử](../../../Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity) · [Generator cầu hiện hành](../../../Assets/ProjectLopHoc/Editor/RuntimeStreaming/RailwayEnvironmentDensity.cs) · [Kiểm kê hạ tầng đã có](../Infrastructure_Model_Audit.md)

## Cách tái sử dụng và phê duyệt

1. Dùng cùng **nguồn BIM và AssetID** trong scene BIM và các trình xem nếu chúng cùng thể hiện đúng cầu SO 201. Mỗi scene có geometry binding và occurrence riêng. Với cùng một tài sản, nhiều instance trình bày có thể dẫn tới cùng hồ sơ. Chưa có căn cứ để gán các ID SO 201 cho cầu `Bridge_Main` hiện hành.
2. Nếu cần cầu BIM đúng tuyến, tác giả IFC cần dựng model mới từ chainage, tim Spline, độ dốc, mặt cắt ray đôi, khổ giới hạn đoàn tàu, cao độ mép nước/trụ và vị trí OCL đã duyệt. Đặt GlobalId ở cấp cầu và mỗi cấu kiện, giữ các quan hệ tổng hợp. Chốt quy tắc bảo toàn ID qua các phiên bản. Không kéo giãn model đường bộ để bù hình học tuyến.
3. Khi có model đường sắt đúng kích thước, xác minh một pose chuẩn và cả hai đầu nối trong sandbox trước. Dùng bản BIM chi tiết cho xem hồ sơ; đánh giá một bản trình bày runtime riêng sau phê duyệt, với cùng bảng liên kết chỉ đọc và chính sách giữ các Zone cầu. Giữ nguồn BIM chi tiết có thể dùng lại trong Unity hoặc công cụ IFC.
4. Duyệt phạm vi thêm **catalog hạ tầng riêng**, trường dữ liệu có chứng cứ, cách lộ diện trong ba tab, phép đăng ký tọa độ, tiêu chí render/physics Web và Android, cũng như phương án quay lui. Việc cập nhật Zone, Showroom hoặc prefab chính cần duyệt riêng. Quest 3S chưa được đo trong pilot.
