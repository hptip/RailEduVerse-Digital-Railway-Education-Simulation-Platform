# Test bản Web ở máy (đăng nhập nhiều tài khoản, bản đồ, chat)

## 1. Bật backend
Nhấp đúp `Backend/start_backend.bat` (hoặc `cd Backend` rồi `python server.py`). Để cửa sổ đó mở.
Lần đầu chạy bản mới, mật khẩu cũ trong `classroom.db` tự chuyển sang dạng băm khi đăng nhập, không cần làm gì.

## 2. Build
**File → Build Profiles → UTTVR Web → Build** vào `Builds/UTTVR_Web_Release`.
Bản Web giờ **mặc định online** (gọi backend, Photon chung phòng), không cần sửa ô `offlineDemo` trong Inspector.
Muốn chạy offline như trước: thêm `?offline=1` vào cuối địa chỉ trang.

## 3. Chạy
```
node .\Documentation\PlatformCompatibility\WebTests\serve.mjs .\Builds\UTTVR_Web_Release 8778
```
`serve.mjs` tự chuyển `/api/...` sang backend ở cổng 5000 (giống nginx trên máy chủ).

## 4. Kiểm tra
| Việc | Cách làm | Kết quả đúng |
|---|---|---|
| 2 tài khoản cùng lúc | Tab 1 mở `http://127.0.0.1:8778`, đăng nhập `sv01 / 123456`. Tab 2 (gõ lại địa chỉ, **không** dùng "Nhân đôi tab") đăng nhập `gv01 / 123456`, chọn vai trò Giảng viên | Hai bên thấy nhau, đúng tên trên đầu, menu E → Cá nhân đúng tài khoản |
| F5 | Bấm F5 ở một tab | Vào lại luôn, không phải đăng nhập lại; tab kia không bị ảnh hưởng |
| Đăng xuất | Menu E → Đăng xuất ở tab 1 | Tab 1 về màn đăng nhập, tab 2 vẫn chơi bình thường |
| Minimap | Đi lại trong Lớp học / Khuôn viên | Minimap góc trên phải xoay theo hướng nhìn, có chấm của người kia. Lăn chuột trên minimap hoặc `+` / `-` để phóng to |
| Bản đồ lớn | Bấm `M` | Hiện bản đồ, danh sách người trong khu vực, nút "Đến đây", "Về điểm xuất phát". `M` hoặc `Esc` để đóng |
| Menu khu vực | Bấm `F1` | Mở menu chuyển khu vực (trước đây là `M`) |
| Chat | Bấm `T`, gõ, `Enter` | Tin hiện ở cả 2 tab. Lúc gõ nhân vật đứng yên, phím E/V/M/H/L không tác dụng. `Esc` thoát |

## Lỗi hay gặp
- "Không kết nối được máy chủ": backend chưa chạy, hoặc mở trang không qua `serve.mjs`.
- Bảng điểm sinh viên hiện dữ liệu mẫu: đăng nhập bằng tài khoản sinh viên thì đúng như vậy (chỉ giảng viên xem được điểm).
