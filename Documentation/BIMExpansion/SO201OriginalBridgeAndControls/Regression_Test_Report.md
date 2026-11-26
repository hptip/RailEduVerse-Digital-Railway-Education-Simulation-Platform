# Kiểm thử hồi quy

| Hạng mục | Kết quả |
| --- | --- |
| Login, vào Campus và menu đúng năm scene | PASS trên Web |
| Mở Classroom, Showroom, YenTuHaLongXanh và BIM từ menu | PASS trên Web |
| Điều hướng, thả/khóa chuột, lồng hai popup, tab và fullscreen | BLOCKED do Chrome từ chối Pointer Lock trong phiên kiểm thử |
| Tàu vào–giữa–ra cầu bằng cabin, camera nóc và camera ngoài | PASS trên build Web cuối |
| Preload và unload cầu SO 201 | PASS, 0 frame thiếu cầu trong phép chạy |
| Cây và highlight cấu kiện cầu, hồ sơ và Thông số IFC | PASS trên Web |
| C005, scroll trên panel không gọi zoom, export và mở lại XLSX | PASS trên Web, chưa xác nhận thao tác áp dụng import |
| Kiểm tra tất cả 91 lá bằng click thủ công | NOT TESTED, mapping 91/91 đã đối chiếu |
| Lưu và đọc lại hồ sơ sau khi chọn lại tab trong phiên Web | PASS, chưa xác nhận sau tải lại trang trong phép chạy này |
| Giá trị checksum IFC, DAE, prefab tàu và Showroom | PASS |
| Trình chiếu bài giảng, chatbot và quay camera trong từng cảnh gameplay | BLOCKED cho luồng kiểm tra Pointer Lock; mở scene thành công |
| Android APK / Meta Quest 3S | VR NOT TESTED |
