# Camera ngoài tàu và font tiếng Việt

## Camera

Khi ngồi ghế lái trên Desktop/Web, phím B chuyển qua cabin → trên cao → bên phải → bên trái → kết cấu → cabin. Giữ chuột phải để xoay trong phạm vi giới hạn. Cuộn để zoom và nhấn Home để trả lại góc của chế độ hiện tại. Chế độ kết cấu sử dụng góc thấp quanh cầu. Kích thước khung nhìn dựa trên khoảng cách giữa hai toa của đoàn tàu hiện có.

Camera dùng chung với `FirstPersonController` và bộ quản lý con trỏ `UttDesktopCursor`. Điều khiển xoay/zoom tạm dừng khi UI giữ chuột. Kiểm tra chướng ngại dọc hướng nhìn và thoát về cabin trong vùng hầm đã đăng ký. Trên VR, nút B tiếp tục đi qua cabin → trên cao → bên phải → bên trái → cabin với giới hạn góc nhìn VR.

Trên Web, bấm vào khung game để bắt chuột sau khi đổi scene, nếu cần. Lượt kiểm tra ở ga đầu tuyến xác nhận cuộn thay đổi khoảng cách camera và giữ chuột phải thay đổi góc nhìn. Ảnh đối chiếu nằm trong `Before_After_Screenshots/Camera_Manual_*.png`. Lượt chạy qua cầu xác nhận chuyển sang chế độ kết cấu và trở về cabin. Góc nhìn mặt dưới cầu và va chạm ở mọi vị trí chưa có ảnh nghiệm thu riêng.

## Font

Atlas tĩnh UTT Vietnamese SDF được đăng ký lại khi khởi động, kể cả khi danh sách fallback trong TMP Settings bị mất sau merge. Các nhãn mất font tham chiếu và chữ tiếng Việt dùng font UGUI mặc định được sửa khi mở scene. Kiểm tra trước build xác minh asset font, atlas, material và bộ ký tự bắt buộc. Lệnh **Tools → UTTVR → Fonts → Verify Vietnamese before publishing** chạy lại phép kiểm tra ngay trong Editor.

Khi xử lý conflict liên quan đến font, giữ cặp asset và `.meta` của UTT Vietnamese SDF, TMP Settings và `Assets/Resources/UTT Project Fonts.asset`, rồi chạy lệnh kiểm tra trên. Kiểm tra này phát hiện lỗi tham chiếu và thiếu ký tự; bố cục và kiểu chữ tùy chọn của từng giao diện vẫn cần được xem trong bản chạy.
