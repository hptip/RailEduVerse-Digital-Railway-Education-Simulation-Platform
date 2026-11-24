# Tối ưu Visual cầu chính · PASS

`Zone_018` trên tuyến sử dụng `Bridge_YTH_Visual_Optimized`. Prefab dẫn xuất đặt ở `Assets/ExternalBIM/BridgeVisualOptimization/`. Bản gốc, Source, Detailed và catalog được giữ nguyên.

| Chỉ số cầu | Visual gốc | Visual tối ưu |
| --- | ---: | ---: |
| MeshRenderer đang hoạt động | 420 | 84 |
| Đỉnh / tam giác | 50.400 / 25.200 | 50.400 / 25.200 |
| Vật liệu riêng / tổng submesh | 1 / 420 | 1 / 84 |
| Bounds thế giới X × Y × Z | 1.091,5550 × 28,7771 × 278,4690 m | 1.091,5550 × 28,7771 × 278,4690 m |
| Thời gian từ khi nhận sự kiện scene đã nạp đến khi thấy root cầu active | 2,00 ms | 2,00 ms |
| Unity Total Used Memory tại 5 cửa sổ đo | 599,0–617,5 MB | 597,5–616,5 MB |
| Unity frame cadence p95/p99 tại 5 cửa sổ | 9/9 ms | 9/9 ms |

| Góc và mốc lý trình | Draw calls trung vị, toàn frame | Mức giảm |
| --- | ---: | ---: |
| Cabin vào cầu · 3.700 m | 537 → 239 | 55,5% |
| Trên nóc · 4.080 m | 134 → 96 | 28,4% |
| Ngoài bên phải · 4.300 m | 373 → 228 | 38,9% |
| Cabin đoạn giữa · 4.560 m | 217 → 159 | 26,7% |
| Cabin ra cầu · 4.710 m | 195 → 181 | 7,2% |

Gộp riêng 56 nhịp dài 20 m và 28 cụm trụ. Mỗi nhóm có AABB riêng và tiếp tục culling theo camera. Frustum của góc nóc nhận 2.580 → 2.880 tam giác cầu do bounds nhóm rộng hơn. Chênh lệch lớn nhất của counter tam giác toàn frame ở năm mốc là **+480, tương đương +0,126%**. Hai bản có cùng FOV, camera, tốc độ 200 km/h, khung 1280 × 720 và cấu hình Development WebGL.

Hai lượt chạy đều nạp cầu trước đoạn vào khoảng 694 m và ghi 0 frame thiếu cầu. Bản tối ưu hoàn tất unload sau khi tàu qua hết cầu. Các ảnh đối chiếu và kiểm tra vertex/winding/material không cho thấy hở nhịp, mặt bị mất hay thay đổi ray, trụ, OCL. Bản thử chứa thêm 383.595 byte dữ liệu build. Bài đo chưa tách riêng thời gian đọc/giải nén trước `sceneLoaded`, thời gian GPU hay bộ nhớ GPU.

**Quyết định triển khai.** Giảm draw calls ở cả năm mốc, bounds và hình học khớp, lượng tam giác culling tăng ít so với toàn frame và không ghi nhận tăng bộ nhớ Unity. Scene sản xuất được cập nhật bằng chính nội dung Zone_018 đã được Unity lưu và chạy trong bản thử. Có thể quay về Visual trước Phase 2 bằng [snapshot Zone_018](Snapshots/Before_Phase2.zip).

[CSV đo A/B](Visual_Performance_Comparison.csv) · [Kiểm tra hình học](Model_Integrity_Report.md) · [Web và ảnh](Web_Test_Report.md) · [Thống kê theo nhóm](Visual_Group_Inventory.csv)
