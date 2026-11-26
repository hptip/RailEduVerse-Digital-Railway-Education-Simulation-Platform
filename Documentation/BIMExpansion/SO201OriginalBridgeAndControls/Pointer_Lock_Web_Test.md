# Browser Pointer Lock

**BLOCKED — Xác nhận khóa ở bốn scene gameplay.** Chrome báo `WrongDocumentError: The root document of this element is not valid for pointer lock` trong môi trường điều khiển. Chẩn đoán trực tiếp thấy `pointerdown` có `isTrusted=true`, user activation có hiệu lực, tài liệu đang focus, canvas có quyền yêu cầu từ Unity và trực tiếp nhận click. Trình duyệt từ chối yêu cầu, nên không thể xác nhận camera quay, nhả khi mở UI, trạng thái hai UI lồng nhau, Esc, đổi tab hoặc fullscreen trong phép đo này. Công cụ điều khiển cửa sổ Windows cũng dừng vì không xác định được URL Chrome hiện hành.

**PASS — Không bắt chuột ở Login và BIM.** Đã mở cả sáu scene chính qua đúng năm đích. Picker nhận lại XLSX đã xuất từ C005 và không bật Pointer Lock. Các bằng chứng Web nằm trong `Web_BIM_Evidence.json`. Ray và điều khiển tay cầm Quest chưa thử trên thiết bị.

## Crosshair như con trỏ

**PASS — Play Mode.** Một Button thật ở tâm nhận hover vào/ra, nhấn, nhả, click và scroll từ Input System khi vị trí chuột vật lý ở góc màn hình. Module trả con trỏ về bộ xử lý mặc định khi mở giao diện.
**PASS — Chrome Web.** Chrome trong môi trường thử vẫn từ chối Pointer Lock. Chế độ tâm ngắm dự phòng hoạt động tại Campus và Classroom. Mở menu hoặc bài giảng trả chuột về UI. Nút chuyển bài giảng nhận click. [Kết quả](Crosshair_Web_Evidence.json)