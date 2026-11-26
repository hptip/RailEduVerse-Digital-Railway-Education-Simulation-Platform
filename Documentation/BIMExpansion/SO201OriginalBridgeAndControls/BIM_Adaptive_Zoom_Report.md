# Camera BIM zoom thích ứng

Zoom quỹ đạo dùng `d * exp(-steps * 0.18)` với bước scroll đã chuẩn hóa theo Input System. Hệ số, smoothing và khoảng cách tối thiểu cấu kiện có thể chỉnh trong Inspector. Với một bước chuẩn, khi chưa gặp giới hạn gần, giảm khoảng 16,47% khoảng cách đặt. Gần cấu kiện, camera có giới hạn theo bounds chiếu theo hướng nhìn và khoảng lùi gần. Zoom/pan giữ cùng tâm hiện hành. Scroll ở panel không gọi nhánh camera 3D. Không đổi FOV để thực hiện zoom.

Đo thực tế cùng bản Web trong [bảng khoảng cách](Zoom_Distance_Comparison.csv). Các giá trị cũ là mô hình công thức với cùng số bước chuẩn; chỉ bảng Web ghi số quan sát thực tế.

**PASS — Xác minh gần trên build cuối.** Cùng một bước: toàn cảnh Zone_021 giảm khoảng 94,40 m, dầm giảm 13,22 m, mô-đun giảm 6,54 m, bu lông C005 sau lệnh Định vị giảm 4,22 cm (0,2561 → 0,2139 m). Đã chụp [bu lông cận cảnh](Screenshots/SO201_Final_C005.png) và [mô-đun](Screenshots/Zoom_Module.png). Cây và chọn 3D trả về đúng AssetID của dầm gốc. Các phép đo ghi khoảng cách đặt, chưa đo thời gian ổn định camera độc lập.
