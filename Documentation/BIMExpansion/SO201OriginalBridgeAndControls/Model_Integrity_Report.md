# Tính toàn vẹn model và dữ liệu

**PASS — SHA-256 trước/sau.** File IFC bên ngoài, DAE, meta của DAE, BridgeBIM_Catalog gốc, hai prefab nguồn, Train_THSR_700T và Showroom.unity khớp hash baseline. Cả hai representation nguồn SO 201 cùng tham chiếu 91 mesh DAE và giữ material nguồn. 91 GlobalId, AssetID và liên kết cha IFC đối chiếu từ prefab Detailed được ghi vào mapping.

Mức thay đổi được phép gồm registry cầu trong scene tuyến, catalog đặt trong scene BIM, prefab viewer Zone_017–019, scene runtime Zone_017–019 và mesh nền/sông mới. Dữ liệu SQLite nền không ghi bởi công cụ tích hợp. Baseline và snapshot trong [Snapshots](Snapshots).
