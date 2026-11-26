# Phạm vi tệp đã thay đổi

- `Assets/Editor/SO201OriginalBridgeAudit.cs` và `SO201OriginalBridgePlacement.cs` kiểm kê nguồn, đo mesh/pivot, tạo sandbox, đặt cầu và build Web.
- `Assets/ExternalBIM/SO201Original` chứa sandbox, catalog placement và mesh môi trường mới. DAE, catalog gốc và prefab nguồn giữ trên đường dẫn BridgeDemo.
- `YenTuHaLongXanh.unity` cập nhật registry. Zone_017–019 và ba prefab viewer tương ứng giữ phần tiếp giáp. Zone_018 gỡ instance cầu và nước cũ, dùng SO 201 nguyên bản.
- `QuanLyVongDoiTaiSan.unity`, BridgeBimCatalog, BridgeBimAdapter và RailwayBimDashboard giữ cây IFC và bổ sung thông số placement. Probe Web chuyển sang các mốc lấy từ cầu đang dùng.
- `UttDesktopCursor.cs` và `UttDesktopCursor.jslib` quản lý con trỏ. Các controller người chơi, menu, bài giảng, trắc nghiệm, minimap, chatbot và thông tin tàu sử dụng cơ chế chung. Nhánh bàn phím của tàu dừng nhận input khi UI chiếm chuột.
- `RailwayBimCamera.cs` và `RailwayBimController.cs` chuẩn hóa cuộn và giới hạn zoom theo khoảng cách.
- PlayerSettings sau build được đối chiếu về đúng nội dung có trước tác vụ. Không commit hoặc push.
