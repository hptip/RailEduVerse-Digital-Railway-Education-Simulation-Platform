# Kiểm thử Web cầu tái sử dụng SO 201

**Kết luận.** Hai bản SO 201 qua hết cầu trên Web. Giữ cầu mô-đun hiện tại trong Zone_018 chính thức vì chi phí bộ nhớ và render của bản SO 201 vẫn cao sau khi gộp. Ứng viên có thể xem trong bản Web thử riêng.

## Build và chạy tàu

Build tại `Builds/SO201_Railway_Trial` dùng bản sao riêng của scene tuyến và Zone_018, giữ ray, Spline, tàu, OCL, đất, nước và logic streaming. Hai bản Development đều build thành công. Bản đầu có 0 lỗi, 1.050 cảnh báo. Bản gộp có 0 lỗi, 1.051 cảnh báo, kích thước 1.390.295.864 byte, build trong 2 phút 44,48 giây.

Thử trên Chrome 157, RTX 3060, canvas 1280 × 720. Qua Login và menu scene, probe ngồi vào ghế và chạy ga/phanh bằng điều khiển của tàu. Tàu đi qua toàn bộ cầu trên Track_Right ở **200 km/h** với **0 frame thiếu cầu**. Bản gộp ghi Zone_018 đã nạp tại **2.954,62 m**, đã unload tại **5.180,84 m** và tàu dừng ở **5.464,84 m**. Kiểm tra cabin vào/ra, góc trên nóc và bên phải. Không thấy khe hở mặt sàn, ray bị lấp hoặc mất trụ trong các ảnh so sánh. Bao toa trên Track_Left được kiểm bằng hình học. Chưa lái thực tế trên Track_Left hoặc Quest.

## So sánh chi phí render

| Camera | Cầu mô-đun đang chạy | SO 201 nguyên mesh | SO 201 gộp renderer |
| --- | ---: | ---: | ---: |
| Cabin vào cầu | 239 | 1.519 | 709 |
| Trên nóc | 96 | 306 | 209 |
| Ngoài bên phải | 228 | 880 | 470 |
| Cabin giữa cầu | 159 | 455 | 282 |
| Cabin ra cầu | 181 | 279 | 232 |

Các số trên là draw calls trung vị toàn frame. Bản gộp giảm draw calls ở cả năm góc so với SO 201 nguyên mesh, giữ nguyên 491.794 tam giác của cầu và giảm 1.486 xuống 129 renderer. Bản ngầm của móng cũng được hiệu chỉnh để hai lớp bê tông lót cách nhau 0,10 m, giữ nguyên mesh nguồn.

So với cầu đang chạy, ở đầu cầu bản gộp vẫn tăng **239 → 709 draw calls**, tam giác toàn frame **1,340 → 1,798 triệu** và Unity Used Memory **599,9 → 653,4 MB**. Frame-time p95 đồng mức **9 ms** trong phép thử PC. p99 tăng từ **9 lên 10 ms** tại ba trong năm mốc. Số đo này chưa cho biết GPU time hoặc hiệu năng Quest 3S. Một bước tối ưu thích hợp cho ứng viên là dùng instancing cho dầm và trụ lặp lại để tránh lưu lại từng bản sao mesh của nhóm, đồng thời tạo LOD giữ mặt cắt và xuất xứ SO 201 cho góc nhìn xa.

[CSV đầy đủ](Web_Performance_Comparison.csv) · [Log browser và Unity](Web_Optimized_Evidence.json) · [SO 201 nhìn từ ngoài tàu](Screenshots/Web_SO201_outside-middle.png) · [Cầu mô-đun ở cùng góc](Screenshots/Web_Modular_outside-middle.png) · [SO 201 từ nóc tàu](Screenshots/Web_SO201_overhead-middle.png)

**VR NOT TESTED.**
