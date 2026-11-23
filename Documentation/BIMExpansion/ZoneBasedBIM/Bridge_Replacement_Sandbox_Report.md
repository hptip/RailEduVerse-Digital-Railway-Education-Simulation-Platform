# Sandbox thay cầu Zone_018

Scene `Assets/ExternalBIM/ZoneBased/Bridge_Replacement_Sandbox.unity` tham chiếu ba prefab viewer cho Zone_017, 018 và 019. Cầu baseline sử dụng đúng `ExtendedBridgeQuest` và vật liệu lưu trong Zone_018. Ray đôi, nước và địa hình vẫn là bối cảnh cùng hệ tọa độ. Mốc lệch của scene quản lý áp dụng đồng nhất cho cả ba Zone.

`RailwayBridgeReplacementSandbox` có hai lệnh context `Show OldBridge` và `Show NewBridge`. Với trạng thái bàn giao, trường `newBridge` rỗng. Khi chưa có candidate mà gọi NewBridge, adapter từ chối chuyển và giữ baseline hiện. Sau khi một candidate đã được đo và gán ở sandbox, lệnh tắt model trước rồi mới bật model còn lại. Slot rỗng chỉ là chỗ gắn candidate sau khi duyệt.

| Hạng mục | Trạng thái |
| --- | --- |
| Sandbox baseline | Đã tạo scene riêng, có cầu, môi trường và ray của ba Zone. Ảnh Zone Viewer trên Web dùng cùng các prefab baseline. |
| Candidate | **BLOCKED — geometry**, dữ liệu SO 201 không phù hợp với cầu đường sắt đang dùng. |
| Tàu qua candidate, độ hở, culling và preload | Chưa thể kiểm thử candidate. |
| Thay cầu production | Chưa triển khai. |

[Khả thi và số đo](Bridge_Replacement_Feasibility.md) · [Zone Viewer Web](Web_Test_Report.md)
