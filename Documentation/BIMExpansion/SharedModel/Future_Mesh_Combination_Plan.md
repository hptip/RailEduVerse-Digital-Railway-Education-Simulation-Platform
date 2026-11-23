# Kế hoạch cho Bridge_Combined

1. Lấy `Bridge_Visual` làm nguồn placement và baseline render. Xuất mesh dẫn xuất trong thư mục riêng với manifest gốc, material, nhóm nhịp và tổng số tam giác. Prefab Detailed vẫn dùng subasset mesh gốc và giữ nhận diện.
2. Đánh giá trước theo nhịp hoặc Zone. Gộp theo material/submesh tương thích và đưa vertex từ local nguồn sang local chunk bằng `chunk.worldToLocalMatrix * source.localToWorldMatrix`. Xử lý normal bằng inverse transpose, đối chiếu tangent/dấu và winding ở transform mirror.
3. So sánh UV, normals, material theo submesh, world-space bounds, số tam giác và ảnh từ cùng camera. Xác minh không mất phạm vi culling hoặc thiết lập LOD, shadow và light/probe. Chỉ tạo collider proxy nếu vật lý có yêu cầu.
4. Đo trên chính Web và Quest ở cùng tuyến/camera, theo dõi draw calls, renderers, memory, thời gian nạp và p95/p99. Kiểm tra khi nhóm preload, unload, đi qua ranh giới và khi culling chỉ thấy một nhịp.
5. Giữ tham chiếu có thể chuyển về Bridge_Visual. Chỉ triển khai sau phê duyệt vị trí, budget và rollback. Static batching thuộc các đối tượng cố định phù hợp, còn nhóm di chuyển hoặc pooling dùng đường render tương ứng.

Đợt này dừng ở hai prefab dùng chung mesh nguồn.
