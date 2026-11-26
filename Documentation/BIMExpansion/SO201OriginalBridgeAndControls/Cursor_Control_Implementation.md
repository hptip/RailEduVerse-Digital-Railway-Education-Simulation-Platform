# Triển khai quản lý con trỏ

`UttDesktopCursor` nhận quyền từ controller desktop cục bộ và tập hợp yêu cầu UI theo GameObject. Yêu cầu tự hết khi panel bị hủy hoặc thôi active. Mouse look, ray gameplay, nhảy và di chuyển desktop chỉ hoạt động khi bắt chuột và qua khung ổn định đầu. Máy lấy tia hướng camera ở tâm. Quản lý reticle theo Canvas Crosshair của scene hiện hành và tắt raycast của Canvas đó.

Web đăng ký gesture thật lên canvas. Bấm vùng gameplay để capture và mở lại sau Esc, mất focus hoặc đóng UI. Xuất trạng thái khóa thực của trình duyệt về Unity; không liên tục gọi yêu cầu Pointer Lock. Mở cửa sổ tương tác giải phóng chuột. Trình xem BIM và Login vẫn là thao tác con trỏ tự do. Kính và tay cầm đi theo nhánh XR hiện có.

Menu vẫn có đúng năm đích. Startup dùng Login đã cấu hình và đã bỏ lệnh ép nhảy thẳng vào Classroom của nhánh native. PlayerSettings và pipeline Android không thuộc phạm vi.
