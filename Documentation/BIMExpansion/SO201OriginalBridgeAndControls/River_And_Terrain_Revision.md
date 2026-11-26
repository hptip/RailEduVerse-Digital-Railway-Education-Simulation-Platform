# Điều chỉnh sông và địa hình

## Lần cập nhật 2,4×

**PASS — Kiểm tra trên hình học đã đặt.** Mặt nước Y = −10,569 m. Đài cao hơn trong hai đài giữa sông P2/P3 ở Y = −15,172 m, cách mặt nước khoảng 4,60 m. Hai đài giữa và 16 cọc của chúng được phủ nước, xác minh theo từng đỉnh mesh và tia nhìn từ hai bên cầu. Đài mố O1/O4 ở trên cạn, ngoài hình chiếu mặt sông. Đất/cát chôn kín toàn bộ 30 cọc và chạm đáy bốn đài trong sai số dưới 3,5 cm.

Thân trụ P2/P3 ngập lần lượt khoảng 36,6% và 29,4%. Dải sông dài 230 m theo dòng và rộng khoảng 122,3 m tại cầu.

Lòng sông được tạo từ một bản dẫn xuất của mesh địa hình Zone_018. Bề mặt quanh móng có màu cát, chiều dài dải sông 230 m. Hai đoạn tiếp cận thu mặt bằng cao còn 10 m, bố trí vai dốc xuống sớm và chuyển màu về phía địa hình. Hai điểm tiếp giáp mố tại 3.876,228 m và 4.047,188 m.

## Số đo lịch sử ở 1,8×

**PASS — Quan sát trong sandbox.** Gỡ mặt nước của cầu cũ. Dải sông mới dài 230 m theo hướng dòng, thắt dần ở hai đầu và hẹp tối đa khoảng 96 m tại mặt cắt ngay dưới cầu. Mặt nước Y = −16 m, mặt địa hình ngay tâm cầu Y = −18 m, điểm thấp nhất của cọc hoặc móng Y = −40,922 m. Hai dải bờ nối mặt nước về địa hình có sẵn. Bốn đoạn nền đắp qua Zone_017, Zone_018 và Zone_019 đỡ ray trước và sau hai đầu cầu. Bề mặt nền đất kéo tới mặt ngoài của hai mố IFC và thu chân taluy khi sát mố. Trong phần chồng với mặt đường IFC, nền đặt thấp hơn mặt đường để tránh che kết cấu. Nền ray trên cầu lấy cao độ từ mesh mặt đường IFC và dùng lớp đắp riêng dưới hai đường ray. Nguồn mesh địa hình và ray được tái sử dụng; OCL giữ bố trí và cao độ.

Các mesh bổ sung được lưu dưới `Assets/ExternalBIM/SO201Original/Placement`. Phạm vi streaming của cầu dùng một owner Zone_018. Nền đắp tiếp tục dùng vòng đời của từng Zone. Không thay chiều dài tuyến, vị trí spline hoặc thiết lập tốc độ tàu.

Nền đất tiếp giáp các mố tại 3.894,722 m và 4.022,842 m. Ảnh Web giữa nhịp cho thấy nền đắp nối liền khu vực tiếp cận bên phải khung hình. [Ảnh Web lúc tàu qua cầu](Screenshots/Route_outside-middle.png)
