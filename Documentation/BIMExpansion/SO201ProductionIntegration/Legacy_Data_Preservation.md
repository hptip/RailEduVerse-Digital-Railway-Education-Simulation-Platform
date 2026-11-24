# Bảo toàn hồ sơ cầu mô-đun

Cây Zone_018 dùng catalog SO 201 làm mặc định. 420 ID `YTH-BRIDGE-MAIN-*` vẫn nằm trong `Bridge_YTH_Catalog.asset` và Detailed cũ. Dữ liệu ứng dụng giữ đường dẫn `BridgeYTH_Infrastructure.db` trong `Application.persistentDataPath`. Trình duyệt giữ dữ liệu theo origin và đường dẫn Web. Các lần kiểm thử dùng profile Chrome riêng.

Catalog SO 201 dùng 1.486 ID `YTH-SO201-*` và SQLite riêng `BridgeYTH_SO201Route.db`. Giữ cơ chế hồ sơ sáu giai đoạn, ghi chú và thông số người dùng. Dữ liệu khởi đầu trống. Pilot IFC ban đầu dùng `BridgeSO201_Infrastructure.db`. SQLite Z021 giữ nguyên.

Rà soát 420 ID cũ cho thấy mỗi cấu kiện có nhiều ứng viên gần lý trình theo bộ lọc vai trò. Chưa xác nhận quan hệ tương đương 1:1. Đây là danh sách tham khảo, chưa dùng để chuyển hồ sơ. [Bảng rà hồ sơ](../SO201RailwayReuse/Legacy_420_Review.csv)

Khôi phục cây cũ bằng scene BIM và prefab viewer Zone_018 từ [snapshot](Snapshots/Before_Integration.zip). Catalog cũ sử dụng đúng tên database cũ. Khôi phục riêng scene runtime Zone_018 để quay về Visual trước tích hợp. Không cần ghi đè bất kỳ database nào.
