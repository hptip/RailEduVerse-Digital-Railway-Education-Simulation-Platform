# Bảo toàn hồ sơ và quay lui

**PASS — Đối chiếu định danh.** Catalog đặt trên tuyến dùng 91 ID `BRIDGE-SO201-*` và các GlobalId đã có từ BridgePilot. Schema giữ `IFC4X3_ADD2`, nên dùng đúng `BridgeSO201_Infrastructure.db` ở nơi lưu của ứng dụng. Hồ sơ người dùng có trước đọc theo GlobalId trong cùng kho. Bản Web chạy ở origin mới sẽ thấy kho riêng của trình duyệt.

420 ID mô-đun và 1.486 ID của cầu nhân bản tiếp tục thuộc catalog, prefab và hai tệp SQLite legacy `BridgeYTH_Infrastructure.db`, `BridgeYTH_SO201Route.db` nếu đã sử dụng. Không có migration hay gán dữ liệu giữa các ID khác nhau. Excel giữ luồng đường ray đang có.

Muốn quay về cầu trước, mở các ZIP trong [Snapshots](Snapshots) và khôi phục có chọn lọc các scene tuyến, BIM, Zone_017–019, prefab trình xem của ba Zone. Bản cũ được đọc lại theo catalog và database tương ứng. Dữ liệu DB không cần khôi phục hoặc ghi đè.
