# Kiểm thử hiệu năng Bridge BIM trên WebGL

## Môi trường

Unity 6000.3.0f1, WebGL Development Build riêng của `BridgeBIM_Test`. Chạy thật qua HTTP loopback trong Chrome 157 headless trên Windows, 1.280 × 720, DPR 1, ANGLE Direct3D 11 và NVIDIA GeForce RTX 3060. Không có phép đo Quest hoặc Internet.

## Lượt Web đầu

| Hạng mục | Kết quả đo |
| --- | --- |
| Unity Build | Thành công, 0 lỗi, 14 cảnh báo. 7 phút 06,533 giây, tổng output Unity báo 631.738.312 byte. |
| Thời gian mở ứng dụng | 3,764 giây từ lúc điều hướng đến log Unity Ready, profile trình duyệt mới, HTTP localhost. |
| Nhịp khung hình quan sát được | 600 khoảng `requestAnimationFrame` trong 5.003,3 ms khi đứng yên sau khi chọn cấu kiện. Median **8,30 ms**, p95 **8,40 ms**, p99 **8,50 ms**, tối đa 8,50 ms. Tương đương khoảng **120 FPS** trong lần lấy mẫu này. |
| Bộ nhớ quan sát được | Chrome `JSHeapUsedSize` **32.326.676 byte**. Unity `Total Used Memory` recorder trả khoảng **111,45–112,94 MB** trong 15 mẫu đứng yên ghi cách nhau khoảng 2 giây. Đây là hai scope đo khác nhau. Chưa đo riêng GPU memory hoặc process RSS. |
| Tương tác thực tế | Chọn từ cây, focus, duy trì một highlight, đọc Thông số và Vòng đời, chọn từ model bằng ray. [Bằng chứng](BIM_Selection_Test.md) |

Kết quả thời gian khung hình là cadence quan sát qua trình duyệt ở một góc nhìn đứng yên, không bao gồm thời gian GPU riêng. Build development vẫn chứa phần runtime và dữ liệu dùng chung của project, vì vậy kích thước build phản ánh nhiều hơn model cầu. Thử nghiệm phục vụ trên loopback.

## Lượt Web cuối

Bản sửa nhúng font hỗ trợ tiếng Việt, căn toàn cảnh để thấy đủ hai đầu cầu và tự cuộn cây tới cấu kiện được click trong mô hình. Các ảnh bàn giao chụp từ lượt này.

| Hạng mục | Kết quả đo |
| --- | --- |
| Unity Build | Thành công, 0 lỗi, 3 cảnh báo. 7 phút 07,156 giây, output Unity báo 631.430.345 byte. |
| Thời gian mở ứng dụng | **4,074 giây** từ điều hướng đến log Ready, cùng profile thử, vô hiệu hóa HTTP cache qua Chrome DevTools. |
| Nhịp khung hình | 600 khoảng `requestAnimationFrame` trong 5.008,6 ms, góc toàn cảnh đứng yên sau khi đã thao tác với cây. Median **8,30 ms**, p95 **8,50 ms**, p99 **8,50 ms**, tối đa 8,50 ms. Khoảng **120 FPS** trong lượt đo. |
| Bộ nhớ | Chrome JS heap **50.632.304 byte**. 15 mẫu Unity `Total Used Memory` khoảng **112,46–114,42 MB**, bộ nhớ managed qua `GC.GetTotalMemory(false)` khoảng **4,65–6,11 MB**. Chưa đo GPU memory riêng, RSS hay peak khi tải. |
| Tương tác | Chọn từ cây, focus, mở Thông số và Vòng đời, chọn Pavement từ mô hình và cuộn về node tương ứng. [Kết quả và ảnh](BIM_Selection_Test.md) |

Console trình duyệt báo ba shader nội bộ không hỗ trợ gồm `CoreCopy`, `StencilDitherMaskSeed` và `HDRDebugView`. Hình học và highlight hiển thị trong lượt kiểm tra. Không ghi nhận lỗi tải, exception hay lỗi chọn. Lượt đầu có lỗi hiển thị dấu tiếng Việt và cạnh trái cầu bị cắt, hai điểm đều đã kiểm tra lại trên bản cuối.

## Chạy lại

Từ thư mục gốc project, đóng Unity Editor đang dùng project rồi chạy build thử riêng. Log tóm tắt nằm tại `Temp/BridgePilot/WebBuild.txt`.

~~~powershell
New-Item -ItemType Directory -Force Temp/BridgePilot | Out-Null
$unity = 'F:/6000.3.0f1/Editor/Unity.exe'
$unityArgs = @(
    '-batchmode', '-nographics', '-quit', '-projectPath', 'F:/GITHUB/UTTVR',
    '-buildTarget', 'WebGL', '-executeMethod', 'UttVr.BridgePilot.BridgePilotEditor.BuildWeb',
    '-logFile', 'F:/GITHUB/UTTVR/Temp/BridgePilot/BridgePilot_WebBuild.log'
)
$playerSettings = (Resolve-Path 'ProjectSettings/ProjectSettings.asset').Path
$originalSettings = [System.IO.File]::ReadAllBytes($playerSettings)
$originalHash = (Get-FileHash -LiteralPath $playerSettings).Hash
try {
    $build = Start-Process -FilePath $unity -ArgumentList $unityArgs -WindowStyle Hidden -Wait -PassThru
    if ($build.ExitCode -ne 0) { throw 'Build WebGL thất bại. Xem log trong Temp/BridgePilot.' }
} finally {
    if ((Get-FileHash -LiteralPath $playerSettings).Hash -ne $originalHash) {
        [System.IO.File]::WriteAllBytes($playerSettings, $originalSettings)
    }
}
node Documentation/PlatformCompatibility/WebTests/serve.mjs Builds/BridgeBIM_WebPilot 8786
~~~

Mở [Bridge Web Pilot](http://127.0.0.1:8786/?bridgeAudit=1). Tham số `bridgeAudit=1` bật log đo. Chọn phần tử trong cây, chuyển qua hai tab chi tiết, quay về Toàn cảnh rồi click cấu kiện trong vùng 3D. Xem các log `TreePick`, `Focus`, `ModelPick` và `Sample` của Unity trong console trình duyệt.
