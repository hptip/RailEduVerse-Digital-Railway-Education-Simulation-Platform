# Build và kiểm thử UTTVR Web trên Windows

## 1. Tải project

Cài Git, Git LFS và Unity Hub. Trong Unity Hub, cài **Unity 6000.3.0f1** cùng module **Web Build Support**. [Cài Git LFS](https://docs.github.com/en/repositories/working-with-files/managing-large-files/installing-git-large-file-storage) · [Unity Web](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/intro/gettingstarted)

Mở PowerShell tại thư mục bạn muốn lưu project và chạy:

```powershell
git lfs install
git clone https://github.com/hptip/RailEduVerse-Digital-Railway-Education-Simulation-Platform.git
cd RailEduVerse-Digital-Railway-Education-Simulation-Platform
git lfs pull
```

Nếu Git LFS báo lỗi tải, hãy xử lý lỗi trước khi mở Unity. Có thể kiểm tra nhanh bằng `(Get-Item .\Assets\StreamingAssets\RailwayLifecycle\Initial.db).Length`; bản hiện tại khoảng 64 MB.

## 2. Build và mở trên trình duyệt

1. Trong Unity Hub, chọn **Add** và trỏ tới thư mục vừa clone, nơi có `Assets`, `Packages` và `ProjectSettings`. Mở project và đợi Unity import, compile xong.
2. Vào **File → Build Profiles**, chọn **UTTVR Web** và bấm **Switch Profile** nếu profile chưa hoạt động. Giữ danh sách scene có sẵn và kiểm tra `Login` đứng đầu.
3. Bấm **Build And Run**, chọn thư mục `Builds\UTTVR_Web_Local` trong project. Unity sẽ mở bản build qua máy chủ cục bộ. Có thể mở lại URL đó bằng Chrome hoặc Edge. [Tài liệu build Web của Unity](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/building-distribution/building)

Để mở lại bản đã build mà không build lần nữa, chạy lệnh sau từ thư mục project nếu đã cài Node.js. Mở [bản chạy cục bộ](http://127.0.0.1:8778/) và giữ cửa sổ PowerShell mở trong lúc thử.

```powershell
node .\Documentation\PlatformCompatibility\WebTests\serve.mjs .\Builds\UTTVR_Web_Local 8778
```

## 3. Tự kiểm thử

| Khu vực | Thao tác và kết quả mong đợi |
| --- | --- |
| Login và Campus | Chọn **Sinh viên** và đăng nhập bằng tài khoản demo điền sẵn. Khi vào Campus, dùng `WASD` để di chuyển và giữ chuột phải để xoay góc nhìn. |
| Menu đổi scene | Nhấn `M` hoặc `F1`. Mở lần lượt Classroom, Showroom, Yên Tử Hạ Long và Quản lý vòng đời tài sản. Dùng menu để quay lại Campus. |
| Classroom và Showroom | Nhấn `L` trong Classroom và thử đổi slide nếu tài khoản có quyền. Trong Showroom, thử chạy và dừng tàu trên sa bàn. |
| Yên Tử Hạ Long | Kiểm tra cảnh tải được. Nếu điều khiển được tàu, chạy qua một ranh giới Zone và quan sát ray, địa hình. Ghi rõ nếu chưa chạy được bài thử này. |
| Quản lý vòng đời tài sản | Tìm `s002`, chọn cấu kiện và kiểm tra bảng thông tin mở được. |

Khi gặp lỗi, ghi lại tên scene, thao tác vừa thực hiện và lỗi đỏ trong **F12 → Console** của trình duyệt.
