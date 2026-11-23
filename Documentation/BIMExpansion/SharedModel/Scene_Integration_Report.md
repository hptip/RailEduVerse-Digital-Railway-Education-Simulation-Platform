# Tích hợp Bridge SO 201 vào hai scene

## Quản lý vòng đời tài sản

Scene có instance riêng `Bridge_Detailed` trên một khoảng quan sát cách mép khu đoạn 021. Pose lưu tại instance. RailwayBimController phục vụ hai miền lựa chọn và để dữ liệu cầu mở riêng khi cần. Trên Web, `Cầu → Cầu mẫu SO 201` dùng cùng Tree View và ba tab. Trình bày Android/Editor cũ dùng thư mục cầu và tìm cấu kiện trong danh sách đang có. VR NOT TESTED.

Hồ sơ cầu dùng `Application.persistentDataPath/BridgeSO201_Infrastructure.db`. Model ray và SQLite đường ray giữ quy trình hiện có. Trường nguồn IFC chỉ đọc. Hồ sơ người dùng hiển thị dưới tiêu đề riêng. Các giai đoạn dùng tên và bộ trường hiện có, với dữ liệu khởi tạo rỗng. Không có lịch sử hay các biểu mẫu kiểm tra/bảo trì độc lập cho cầu trong đợt thử này.

## Tuyến Yên Tử – Hạ Long

Scene `Bridge_Visual_Trial` dành cho xem additive, tách pose khỏi đường tàu. Camera thử nhìn một khoảng tại Y = 9.000 m, ngoài không gian vận hành. Khi thử cục bộ, log ghi tải/unload scene preview và kết quả số renderer, collider, adapter. Lựa chọn của menu sản phẩm và danh sách scene build profile giữ nguyên.

SO 201 vẫn là cầu đường bộ khoảng 89 m; việc thay cầu Bridge_Main cần model đường sắt có kích thước và phép đăng ký tọa độ đã duyệt. Tác vụ chỉ lưu scene quản lý và hai scene thử dưới khu vực BridgeDemo. Registry, scene tuyến, Zone và hệ thống tàu sử dụng tài nguyên hiện hành.

[Đối chiếu với Bridge_Main](../BridgePilot/Railway_Compatibility_Report.md) · [Kiểm thử trình duyệt](Web_Test_Report.md)

## Đối chiếu trước và sau

**51/51 tệp được bảo vệ khớp SHA-256**, gồm DAE và import settings, IFC gốc, catalog, material của pilot, scene tuyến, 17 Zone trong registry cùng Zone_018, dữ liệu SQLite ray, các script tệp được kiểm tra của tàu/streaming và profile. Player Settings cũng khớp SHA-256 baseline sau khi hoàn tác một mục preloaded asset mà Unity Batch Mode tự bỏ trong quá trình thoát. Thay đổi serialized trong scene Quản lý chỉ gồm prefab instance, Transform instance và liên kết adapter.

Trên Web, preview đã bật/tắt bằng F9 trong một phiên tuyến. Bài kiểm thử bổ sung giữ preview hoạt động khi tàu chạy 25,0 → 877,3 m, nạp Zone_010–013 và unload Zone_010. Phạm vi thử chưa bao gồm hành trình đến cầu hiện hành. Kết quả bảo toàn cầu hiện hành dựa trên đối chiếu scene, registry và Zone đã lưu. [Bằng chứng Web](Web_Test_Report.md)
