# Nguồn hình học cầu SO 201

Zone_018 tuyến dùng `SO201_Full_Visual_Optimized.prefab`. Viewer dùng `SO201_Full_Detailed.prefab`. Hai bản xuất phát từ `SO201_Full_Source.prefab`. Bản gộp có một renderer mỗi nhịp hoặc gối, giữ material theo submesh. Detailed vẫn giữ mesh riêng và 1.486 identity; khâu tích hợp chỉ bổ sung metadata cho catalog.

| Chỉ số | Kết quả |
| --- | ---: |
| Chiều dài, bề rộng sàn | 1.120 m × 14 m |
| Nhịp | 64 × 17,5 m |
| Khoảng cách tim hai ray | 6,400022 m |
| Cấu kiện tham chiếu trực tiếp IFC qua DAE | 1.099 occurrence, 12 mesh nguồn |
| Cấu kiện bổ sung | 387 |
| Tam giác có nguồn SO 201 | 473.326 / 491.794 (96,24%) |
| Khoảng hở ngang nhỏ nhất theo bao toa trên hai tim | 1,8372 m |
| Khe mối nối sàn | 0,000000 m trong độ chính xác xuất báo cáo |

Trường `SourceGlobalId` giữ khóa IFC ban đầu cùng GlobalId cha, IFC class, tên và mô tả nếu có. Khóa lưu hồ sơ của occurrence là AssetID riêng trong registry. Lý trình và bao hình học X/Y/Z hiển thị dưới thông số mô phỏng. Lớp sàn ray, lan can, xà mũ và phần nối thân trụ ghi rõ là hình học bổ sung. IFC nguồn chưa có Property Sets.

[Registry 1.486 AssetID](Detailed_Asset_Registry.csv) · [Phép đo hình học đầy đủ](../SO201RailwayReuse/Full_Measurements.txt) · [Chuỗi provenance của mesh gộp](../SO201RailwayReuse/Visual_Group_Provenance.csv) · [Mẫu đối chiếu SO 201](../SO201RailwayReuse/SO201_Reuse_Report.md)
