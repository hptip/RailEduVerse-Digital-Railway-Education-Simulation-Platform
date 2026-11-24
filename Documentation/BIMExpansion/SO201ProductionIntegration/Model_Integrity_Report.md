# Đối chiếu model và scene

- Zone_018 tuyến tham chiếu Visual SO 201 129 renderer tại world origin **(9338,154; 9,331662; −1274,91089)** theo bản kiểm chứng. Viewer áp dụng phép tịnh tiến chung từ `SceneProvenance.json` của vùng Z021.
- Scene chính `YenTuHaLongXanh` và registry không đổi. Phạm vi giữ Zone_017–019 và khoảng nối 3.650–4.770 m được kế thừa. Nhánh Zone_018 giữ cảnh quan, nước, hình học ray/OCL và fallback cũ. Chỉ một Visual cầu được bật trên Web.
- Catalog có 1.486 AssetID phân biệt, 1.099 bản ghi có SourceGlobalId và 387 bản ghi hình học bổ sung. Detailed giữ 1.486 identity, mesh và collider từ ứng viên. Scene quản lý Z021 và SQLite nguồn tiếp tục liên kết như trước.
- Kiểm tra cả hai tim bằng bao hình học hai toa trên tuyến. Mối nối sàn khép kín theo phép đo và cao độ mặt sàn giữ khoảng 2,00–4,96 mm dưới bao đáy mesh ray. [Hình học và provenance](Geometry_Provenance_Report.md)

Snapshot trước tích hợp lưu scene Zone_018, prefab Zone_018 viewer, scene BIM, catalog ứng viên và Player Settings cùng các file meta tương ứng. [Snapshot](Snapshots/Before_Integration.zip) · [Danh sách SHA-256](Snapshots/Protected.sha256)

Đối chiếu sau build Web khớp **19/19 SHA-256** trong danh sách bảo vệ, gồm Source, Visual gộp, Detailed đã kiểm chứng, IFC/DAE gốc, catalog 420 tài sản, Login, Showroom, scene tuyến chính, hai Zone lân cận, các dữ liệu ray và tàu, SQLite Z021, Build Settings và Player Settings.
