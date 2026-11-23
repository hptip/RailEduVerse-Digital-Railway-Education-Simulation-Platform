# Kế hoạch chuyển đổi Web + Quest — chưa triển khai

Giữ **một project**, model/Scene/ID/schema và nghiệp vụ chung. Không làm lại scene/UI, không gỡ XR, không regenerate Showroom/railway, không thay TrainSplineMover, không chuyển dữ liệu thật trong audit.

## Kiến trúc thay đổi tối thiểu

```text
Build Profile / PlatformBootstrap
  ├─ WebControl: Desktop Camera, Mouse/Keyboard, UI pointer, Physics pick
  └─ VRControl: XR Origin, tracked input, locomotion, XR UI ray
           ↓ cùng commands / events
Existing scene menu + interaction + lecture + train + BIM controllers
           ↓
Platform File Adapter (bytes/Stream) + Database Bootstrap Adapter
           ↓
Existing XLSX codec / validators / SQLite store / fixed IDs
```

- WebGL build luôn chọn desktop 3D. Android Quest chọn VR. Editor/desktop có thể chọn XR simulator hoặc Quest Link theo phiên test; **không dùng sự có mặt của XROrigin để suy ra đang đeo kính**.
- Chỉ một control owner, một active camera/listener và một EventSystem cho scene/session. UI module hiện tại có thể dùng chung nếu bindings/capability phù hợp; không tự tạo EventSystem/Canvas thứ hai.
- Giữ Canvas, buttons, UnityEvents và style hiện tại. Adapter chọn camera, Screen/World presentation, raycaster, scale/anchors. World Space không mặc định hỏng trên Web, nhưng cần desktop event camera + GraphicRaycaster đúng hướng.
- Hai input adapters gọi chung Select/Interact, menu navigation, lecture/slide, train command và BIM controller; không nhân bản authentication, validation, lifecycle hoặc Photon authority.
- Không cần thay toàn bộ SQLite bằng backend khác: package hiện tại đã hỗ trợ Web/IndexedDB và ARM64. Ưu tiên adapter bootstrap/seed/persistence; chỉ dùng remote DataAdapter nếu yêu cầu dữ liệu dùng chung/server sau này.

## Giai đoạn 0 — Đóng băng phạm vi và baseline

1. Xác nhận 7 scene sản phẩm: Login, Campus, Classroom, Showroom, YenTu, Lifecycle, ChiTietSaBan. Xác nhận có phát hành các bản SaBan/preview/legacy không; recovery/sample không tự vào release.
2. Lập Android regression baseline riêng trên Quest, bảo toàn locomotion/comfort/seat/ray trước khi thay bootstrap. Không coi dữ liệu Editor là baseline headset.
3. Tạo Web và Android Build Profile riêng, copy scene list có chủ đích. Hiện registry YenTu dùng 17 Zone; chỉ đưa thêm 41 Zone khi tuyến thực sự đăng ký. Đưa ChiTietSaBan vào profile/menu nếu triển khai chức năng đó.
4. Chưa chạy full Web build khi Resources còn khoảng 1,22 GB raw. Đặt ngân sách transfer/startup/heap và xác định asset nào thực sự phải có trong Resources.

**File/cấu hình dự kiến:** Build Profiles mới; `SceneQuickMenu.cs`; cấu hình XR initialization theo target. Không thay profile Android hoặc global input một cách ngầm định.

## Giai đoạn 1 — Vertical slice Classroom trước

**Sửa tại biên:** `PlayerSpawner.cs`, `XR/YenTuStandaloneXRBootstrap.cs`, `XR/XROriginSessionPersistence.cs`, `XR/XRWorldSpaceCanvas.cs`, `SceneQuickMenu.cs`; thêm PlatformBootstrap/InputAdapter nhỏ khi thật sự cần.

- Web không khởi tạo/giữ rig VR từ Login; giữ adapter HMD/controller/comfort nguyên vẹn trong Android.
- Mouse raycasting phải đến được menu, panel và input: không chỉ bật mouse trên XRUIInputModule trong khi Canvas chỉ có tracked raycaster.
- Dùng lại `LectureBoardController.cs`, `StudentSlideReviewUI.cs`, `TeacherLecturePanelController.cs`. Không dựng LessonManager thứ hai.
- Kiểm thử 2 bài hiện có, previous/next lesson và slide, first slide khi đổi bài, dropdown đồng bộ, first/last disabled, empty/missing data, bảng trắng/quiz, thầy/trò và quyền Single/Shared.
- Sau smoke build thật: chạy Chrome/Edge, click/hover/scroll/drag, nhập tiếng Việt, blur/refocus, resize/fullscreen, thoát/đổi scene; mới ghi WEB PASS.

**Gate:** Classroom độc lập hoạt động trên browser và Quest, không mất ray/locomotion khi skip Login.

## Giai đoạn 2 — File XLSX và SQLite, giữ dữ liệu/ID

**File cần sửa ở biên:** `Lifecycle/RailwayBimDashboard.cs` (FilePath), `Lifecycle/RailwayBimWorkbook.cs` (thêm Read/Write bytes/Stream bên cạnh overload path), `ChiTietSaBan/ChiTiet/ExcelImportManager.cs`, `Lifecycle/RailwayBimController.cs` (bootstrap), `RailwayBimStore.cs` (seed/open boundary), `ChiTietSaBan/ChiTiet/RailwayDatabase.cs` (initialize boundary).

1. `IPlatformFileAdapter.PickXlsxAsync/SaveXlsxAsync` trả/nhận bytes hoặc Stream + tên file, không giả định browser/content URI là desktop path.
2. Web: HTML file input trong click gesture → ArrayBuffer/bytes → cùng workbook parser; export cùng writer → Blob + download. Có progress/cancel/error trong Unity UI. Jslib SFB hiện có chỉ là tài liệu tham khảo; cần ABI Unity 6.3 và callback thật, không gọi synchronous wrapper null.
3. Quest: Android SAF Intent/ContentResolver stream khi hỗ trợ; nếu picker không có, Unity UI liệt kê/chọn file trong `persistentDataPath/TaiLieu`, hiển thị rõ nơi lưu và hướng dẫn đưa file vào. Export có thông báo file/path và phương án lấy file/share phù hợp. Không bắt buộc native picker phải chạy được trên mọi bản Quest OS.
4. SQLite Web: dùng provider gilzoide hiện có, kiểm thử seed deserialize/backup đúng VFS và commit persistence; không dựa duy nhất vào File.Exists/File.Copy. Kiểm thử first-run, refresh tab, tab đóng/mở, storage denied/quota và version migration trên **DB sandbox**, không DB thật.
5. Giữ `AssetID` lifecycle và cặp `ModelID/MeshID` của ChiTietSaBan, giữ schema/transaction/validator. Header thêm/sửa phải hiện trong preview; không tự coi đổi tên cột là rename field cũ, không thay key.

**Gate:** Import → preview lỗi/hợp lệ → Apply vào DB thử → reload → export → import lại cùng ID; thao tác được hoàn toàn từ Unity UI trên browser và Quest. Giữ ExcelDataReader/writer nếu AOT test đạt; không viết lại codec chỉ vì file picker thiếu.

## Giai đoạn 3 — Campus, Login, ChiTietSaBan

- `FirstPersonController.cs`: chia input acquisition khỏi move/interact/seat commands; giữ kiểm tra Fusion authority và desktop branches đang có. Pointer lock phải nhường cho UI khi mở menu/popup.
- `LoginView`, `LoginController`, `QuestLoginKeyboard`: cùng auth service; desktop IME/mouse và Quest keyboard khác presentation. Có test route vào Login dù app mặc định bypass.
- `MeshClickHandler`, `RailwayElementUI`, `CameraFreeMove`: giữ selection/metadata. VR adapter tạo rig/presentation theo platform khi mở ChiTietSaBan độc lập; không sửa từng cấu kiện để nhân đôi click logic.
- Campus: xác định nguồn HDRP Decal Projector Missing Script và phương án material/decal URP; không xóa 1040 object theo số lượng thiếu. Test cửa/collider, transition và player spawn trên cả hai target.

## Giai đoạn 4 — Showroom và AI/audio

- Giữ `SaBanRoot`, `SaBanTrainRunner`, `SaBanLayer`, `SaBanControlPanel`, object identity, BIM colliders, train popup và 8 nhóm.
- `ShowroomInstancedVisualGroup.cs`/`GeometryV3/ShowroomGeometryController.cs`: Web geometry/capability profile thay vì mặc định Original; test WebGL2 instancing/shader variant/fallback, scale và selection. Không thay model thành một mesh hoặc regenerate.
- `XkiroBrain.cs`, `MetaConnectTTSClient.cs`: giữ UWR, test HTTPS/CORS/config trên browser và Android; không vô cớ chuyển sang HttpClient. Không hardcode đường dẫn Windows cho config.
- `GroqSpeechToText.cs`/`VoiceInputController.cs`: browser getUserMedia/permission/user gesture adapter, giữ native microphone trên Quest.
- `BIMAudioPlayer.cs:52`: Web decode PCM từng phần/main thread hoặc qua backend thay Task.Run; không thay mọi async/await.
- Kiểm thử robot/chat text trước, sau đó STT/TTS, close/clear/scroll/focus, autoplay sau user gesture và đổi scene cleanup.

## Giai đoạn 5 — YenTu + tải nội dung Web

- Giữ spline, tàu, ghế và 17 Zone đang dùng. `RailwayZoneStreamingManager`/microchunk/pool dùng chung, adapter input chỉ đưa lệnh vào train/seat/camera hiện có.
- HUD và UIInfo Web phải có mouse pointer; controller B/Y không phải lối thao tác duy nhất trên desktop.
- Kiểm thử bàn phím ngồi/thoát ghế, tiến/phanh không lùi, 5 nấc, camera, reset; ray và comfort Quest không đổi.
- Tách **runtime scene activation** khỏi **download asset**. Nếu cần tải Web theo vùng, lập asset dependency/bundle/Addressables manifest và chunk shared assets trước; không giả định Additive LoadSceneAsync tự giảm `.data` ban đầu.
- Test liên tiếp nhiều ranh giới, tốc độ cao, tải chậm, cầu/ga/hầm atomic, unload/pool không tăng, shader/material không mất. Chỉ mở rộng sau khi đoạn nhỏ đạt Web thật.

## Giai đoạn 6 — Release gates

|Hạng mục|Browser desktop|Quest 3S|
|---|---|---|
|Build/link/plugin|Web smoke có load runtime thật; không native call thiếu|ARM64/IL2CPP build và install mới|
|Input/UI|Chuột/IME/drag/hover/scroll/resize/focus|Ray/direct/locomotion/keyboard/comfort|
|Scenes|Từng scene độc lập + menu/cửa + quay lại|Không nhân đôi rig/EventSystem, không dashboard theo scene khác|
|Data|ID/schema/Excel roundtrip, IDB persistence|Picker/fallback trong UI, persistence và quyền file|
|Network/audio|HTTPS/CORS/gesture, Shared/Single test riêng|Permissions, headset audio và API thật|
|Performance|Transfer/startup/heap/frame p95/p99, WebGL2 fallback|GPU/CPU/memory/thermal trên kính; không suy từ Editor|

Không có thời gian/FPS cam kết trước benchmark. Việc tách platform phải không làm mất các setup Quest đang hoạt động; sau mỗi nhóm thay đổi compile và chạy regression nhỏ. Các bước trên là kế hoạch, **chưa sửa script/Scene/package/profile trong audit**.
