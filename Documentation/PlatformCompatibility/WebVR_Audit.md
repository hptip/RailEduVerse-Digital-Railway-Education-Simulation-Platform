# Audit Web + Quest VR — UTT

Ngày kiểm tra: 20/11/2026. Project thực tế: **F:/GITHUB/UTTVR**, xác minh bằng `Application.dataPath` của Editor và Assets/Packages/ProjectSettings. Unity **6000.3.0f1**, URP 17.3.0, Input System 1.20.1, XRI 3.7.0, OpenXR 1.19.0, Meta OpenXR 2.7.0. Web ở đây là desktop 3D chuột/bàn phím, **không phải WebXR**.

## Kết luận

Một project dùng chung Web + Quest là khả thi, nhưng **chưa sẵn sàng triển khai đầy đủ hai nền tảng**. Chưa có Web Build Profile, chưa chạy browser, và chưa kiểm thử Quest trong đợt audit này. Không thể coi mã chạy Editor/Android là bằng chứng Web tương thích.

Các blocker quan trọng:

1. **P0 — File Excel trong UI:** Lifecycle Web và ChiTietSaBan Web/Android vẫn gọi SFB desktop wrapper. `StandaloneFileBrowser.cs:15–25` không khởi tạo wrapper Web/Android; `OpenFilePanel`/`SaveFilePanel` sẽ đi vào wrapper null trên các nhánh đó. Lifecycle Android đã tránh SFB nhưng chỉ dùng file cố định `persistentDataPath/TaiLieu/NhapTaiSan.xlsx`; chưa đáp ứng tự chọn file ngay trong Unity UI. Cần browser upload/download và Android picker/fallback UI file list.
2. **P0 — UI pointer/presentation:** LoginCanvas, TrainSpeedHUD và UIInfo có canvas chỉ dùng `TrackedDeviceGraphicRaycaster`. Component này chỉ xử lý `TrackedDeviceEventData`, không thay thế raycaster chuột. XRUIInputModule bật mouse không khắc phục điều này. `XRWorldSpaceCanvas.cs:38–78` còn đổi Canvas sang world space và tắt GraphicRaycaster khi thấy XR Origin. Menu scene được tạo runtime cũng dùng tracked raycaster. Giữ UI/OnClick nhưng chọn raycaster và event camera đúng platform.
3. **P0 — Bootstrap và nhập liệu:** `XROriginSessionPersistence` giữ rig Login qua scene; `RailwayBimVrPresentation.cs:51` coi sự tồn tại của XROrigin là điều kiện chọn VR. Vì vậy Web đi qua Login có thể vô tình dùng XR presentation. `FirstPersonController` đã kiểm tra device/simulator, không được kết luận mọi controller chỉ hỗ trợ XR. Cần một điểm chọn control mode rõ ràng và test từng scene độc lập, text/IME, keyboard VR, focus/scroll/drag.
4. **P0 — Voice Web:** `GroqSpeechToText` dùng Unity Microphone; `BIMAudioPlayer.cs:52` dùng `Task.Run` để decode PCM. Hai đường này cần adapter Web. Không đánh đồng toàn bộ `async/await` với background thread không hỗ trợ.
5. **P0 — Dung lượng/streaming:** Các thư mục Resources ngoài Editor có khoảng **1.222.897.551 byte raw**, riêng `Generated/QuestRailway/Resources` khoảng **1.206.196.638 byte**; StreamingAssets khoảng **64.098.304 byte**. Đây là kích thước file nguồn, không phải dung lượng Web nén hoặc heap đã đo. Additive SceneManager hiện chỉ trì hoãn instantiate/unload, không tự tải mỗi Zone qua mạng. Cần đánh giá tải ban đầu trước full Web build.

## Phạm vi và mức chứng cứ

- Quét **83 Scene Assets**, gồm **58 Zone**, **587 C# trong Assets**, cấu hình package/assembly và **295 PluginImporter** của Assets/UPM. Cấu trúc tham chiếu MonoScript lấy từ AssetDatabase, không đoán đường dẫn.
- Mở **13 Scene bằng preview scene API** để resolve prefab và đọc hierarchy/component, rồi đóng mà không lưu; không chạy Awake/Start bằng Play Mode. Gồm 7 scene chức năng chính, 3 scene/preview sa bàn và 3 Zone đại diện ga/cầu/ga.
- Đọc **172 Selectable/ScrollRect** và **22 Canvas** trong snapshot; tìm **0 persistent event target null có method**. Runtime listeners chưa có trong serialized event không bị đánh dấu là hỏng.
- 7 scene chính có **208 TMP_Text**, không thiếu font/material reference trong snapshot. Chưa chứng minh mọi glyph tiếng Việt, IME, anchor/layout hoặc hiển thị đúng ở mọi độ phân giải.
- Ma trận có **299 dòng UI/control/interaction/zone**, gồm template ẩn và các nhóm sinh runtime. Số dòng không đồng nghĩa 296 thao tác đã được click thành công.
- **EDITOR PASS:** codec XLSX đọc/ghi lại tiếng Việt, chỉ trong `Temp/PlatformAudit/CodecProbe.xlsx`; mở seed SQLite với `SQLiteOpenFlags.ReadOnly`, đọc 8 tên bảng. Không gọi `RailwayBimStore` constructor/migration hoặc ghi DB thật.
- **STATIC PASS:** chỉ đạt kiểm tra code/serialized references tương ứng. **EDITOR PASS:** đã thực thi probe cụ thể trong Editor. **WEB PASS/QUEST PASS:** chỉ dùng khi thực sự chạy/thao tác trên nền tảng đó — **không có trong đợt này**. **BLOCKED:** xác định đường chức năng không đáp ứng. **NOT TESTED:** chưa đủ chứng cứ.
- Không chuyển target, không build Web/APK, không Play Mode các scene có thể tự migrate DB. Web smoke build chưa thực hiện: project có Resources toàn cục rất lớn; scene rỗng cũng không đảm bảo build nhỏ. Cần giải quyết biên platform và chọn phạm vi build trước khi đổi target/reimport project đang dùng Android.

## Kết quả theo scene

|Scene|Hierarchy prefab-resolved|Canvas / controls|Kết quả và việc còn thiếu|
|---|---:|---:|---|
|Login|59 nodes|1 / 9|World space, tracked-only. Có rig, XRUIInputModule bật mouse/XR/touch; cần GraphicRaycaster/desktop camera cho Web. Offline service đọc Resources/offline_users. Default menu bypass Login → Classroom, nên phải có test route riêng cho auth, password, role/ngành, lỗi và keyboard.|
|UTTCampus|6.561|3 / 0|PlayerSpawner và XRUIInputModule; 3D door/movement cần Play/target tests, không suy từ số UI button bằng 0. **1.040 Missing Script** nằm ở `Assets/(HDRP) NYC-Like City Buildings Set (PBR)/Prefabs/Decal Projector.prefab`; là vấn đề prefab/decal, chưa chứng minh gây mất nghiệp vụ player. Không tự xóa/swap.|
|Classroom|230|3 / 28|Student/Teacher panel, board và lesson/slide dùng controller chung. Views có re-enable mouse raycaster; vẫn cần kiểm tra thứ tự với XRWorldSpaceCanvas, authority, lecture/slide boundary, bảng trắng, quiz, nhập liệu và scene exit. Nên dùng làm vertical slice Web đầu tiên vì ít phụ thuộc DB/file.|
|Showroom|94.827|6 / 33|Sa bàn/popup/chat/8 nhóm/tàu dùng nghiệp vụ chung. World-space control panel có cả hai raycaster; popup/hud/menu còn rủi ro chuyển presentation. Không regenerate sa bàn. Chưa đo WebGL2, memory hoặc FPS trên target.|
|QuanLyVongDoiTaiSan|191|1 / 32|Dashboard serialized là overlay; VR presentation split panel và nhiều row/input được tạo runtime. Core search/select/metadata/lifecycle/6 giai đoạn dùng chung được; cần xác nhận keyboard cho search/edit ngoài Login. Excel picker và bootstrap SQLite Web là ưu tiên cao.|
|YenTuHaLongXanh|1.205|4 / 19|Seat/train/FirstPersonController có desktop + XR branches. HUD tracked-only; cần mouse adapter. Chưa chạy tàu, phanh/5 nấc, reset, thoát ghế, camera B và ba góc nhìn trong audit. Không thay spline/tàu/seat.|
|ChiTietSaBan|153|1 / 35|CameraFreeMove/Physics selection và InputSystemUIInputModule phù hợp hướng desktop; Canvas ScreenSpaceCamera. Không có VR setup độc lập được resolve trong Scene. Import SFB block Web/Android. ID nghiệp vụ là ModelID/MeshID, không tự đổi thành AssetID của lifecycle.|
|YenTuHaLongXanh_SaBan|1.145|3 / 16|Bản sa bàn riêng, desktop input module; cần kiểm thử adapter nếu phát hành. Đang có trong scene list.|
|SaBan_Display_Preview|1.051|0 / 0|Preview/model, không có UI/camera dùng được tự thân trong snapshot; không coi là ứng dụng hoàn chỉnh.|
|ChiTietSaBan/SaBan|2|0 / 0|Scene gần rỗng trong snapshot; cần xác nhận mục đích thay vì tự dựng lại.|

`YenTuHaLongXanh 1`, 9 Scene `_Recovery`, các sample HDRP/cliff/XRI/SFB đều được scan serialized và liệt kê cuối báo cáo; chưa coi là scene sản phẩm hoặc đã kiểm thử prefab đầy đủ. Không tự đưa vào build, hợp nhất hoặc xóa.

## Railway streaming / scene list

- **58 Zone tồn tại trên đĩa**. Main hiện có **17 scenePath Zone_010–Zone_026**, và **cả 17 đều enabled** trong EditorBuildSettings. **24 entries hiện tại = 6 scene chính + 17 Zone + YenTuHaLongXanh_SaBan**. Không tự kết luận 41 Zone còn lại bị thiếu đối với tuyến hiện đăng ký.
- Zone_010, Zone_018 và Zone_026 đã mở preview để resolve cấu trúc: lần lượt 23/25/16 nodes, không có UI/camera hoặc Missing Script. Cầu/ga là ngoại lệ có chủ đích; 55 Zone khác scan cấu hình/code chung, không chạy thủ công từng Zone.
- Chưa test load/unload, đảo chiều, ranh giới liên tiếp, pool growth, bridge/tunnel atomic group hay network download. Các kết quả kiểm thử từ đợt trước không thay thế kiểm thử Web/Quest lần này.
- Không có Addressables package trong manifest; chưa tìm được đường triển khai bundle-download thực tế thay cho SceneManager hiện tại. Muốn Web tải theo vùng, cần adapter nội dung/bundle manifest hoặc Addressables được duyệt sau, không đổi streamer trong audit.
- SceneQuickMenu có F1 và Y/secondary trái, 5 đích Campus/Classroom/Showroom/YenTu/Lifecycle; ChiTietSaBan chưa có trong menu và scene list hiện tại. Login vẫn có trong list nhưng bị bypass mặc định.

## Platform dependencies

|Hệ thống|Phát hiện cụ thể|Đánh giá|
|---|---|---|
|SQLite|gilzoide sqlite-net **1.3.2** có Web `__Internal`, C/C++ SQLite và IDBVFS/IndexedDB; có Android arm64 `.so`; package đổi scheduler async trên Web.|**Không cần mặc định thay SQLite.** Web persistence/seed vẫn NOT TESTED. `RailwayBimController` đã dùng UWR cho seed URL; `RailwayBimStore` còn File.Exists/File.Copy trước SQLiteConnection. Phải xác minh filesystem seed khớp custom VFS, first-run, reload và migration. Giữ schema/AssetID.|
|Excel|ExcelDataReader 3.9.0 netstandard2.1 + writer ZIP/XML của RailwayBimWorkbook.|Codec EDITOR PASS; parser/AOT Web + Android và file bridge NOT TESTED. Thêm trường/tên header phải qua validator, giữ ID. Không biến chức năng XLSX thành CSV thay thế.|
|SFB|Có jslib upload/download và sample Web, nhưng call sites nghiệp vụ không dùng nhánh sample; synchronous desktop wrapper không có Web/Android.|BLOCKED tại file-picker nghiệp vụ. Jslib dùng ABI cũ Pointer_stringify; phải kiểm tra/cập nhật UTF8ToString khi tích hợp Unity 6.3, không coi sample có mặt là đã hoạt động.|
|Networking|GameMode.Single/Shared trong NetworkManager/PlayerSpawner. Fusion Web NanoSockets DLL, Photon WebSocket.jslib có import Web; AllowClientServerModesInWebGL=false.|Shared có hướng hỗ trợ Web. Offline Single/host/authority phải chạy thử với cấu hình hiện tại; không tự bật client-server hoặc thay Photon.|
|Chat/STT/TTS|XkiroBrain và MetaConnectTTSClient dùng UnityWebRequest; microphone native và Task.Run ở đường WAV là vấn đề cụ thể.|Text/network không bị gán nhầm là HttpClient. Cần HTTPS/CORS, autoplay/user gesture, config/API credential adapter; voice Web cần getUserMedia + PCM bridge. Không đọc/in key thật hoặc gửi chat thật.|
|Shaders/instancing|URP Lit/Unlit, TMP, UTT/Railway Vertex Colour Matte, UTT/Environment Cluster Fade; custom instancing dùng RenderMeshInstanced, không compute dispatch trong đường này.|WebGL2/capability, shader variants và custom fades chưa chạy browser. Không suy mọi Shader URP đều đạt. DeviceDefault mesh profile chọn optimized chỉ Android, **Web vẫn Original**; cần budget/profile Web và fallback khi supportsInstancing=false.|
|Native/Editor|295 PluginImporter đã kiểm kê. Pixyz runtime native plugin không được chọn Web trong snapshot; runtime asmdef vẫn include all và PIXYZ defines còn ở Web.|Chưa chứng minh Pixyz gây build Web lỗi. Không gỡ/sửa Pixyz. Các using UnityEditor ở AutoEnableReadWrite/MeshCombiner/MuseumLabelStand và các file probe được guard UNITY_EDITOR; search hit không phải lỗi compile. Full Web link/stripping vẫn NOT TESTED.|
|Build settings|Web Support=true, Android Support=true; active target Android; **0 custom Build Profile**, dùng global scene list. Android IL2CPP/ARM64. Web initial heap 32 MB, max 2048 MB, Brotli.|Tạo hai profile riêng sau khi duyệt; server cần đúng MIME/encoding và HTTPS, memory phải đo runtime. Kích thước raw Resources không đồng nghĩa heap hoặc compressed transfer size.|

## Điều chưa được xác nhận

Không có WEB PASS hoặc QUEST PASS. Chưa thử click/hover/drag/scroll/focus trên browser/HMD, IME/bàn phím ảo ngoài Login, shader trên Web, APK hiện tại, backend/CORS/API thật, multiplayer thật, browser DB reload/tab conflict, nhiệt độ/72 FPS, kích thước Web nén, hoặc metadata import ghi DB. Không có lý do coi tất cả các mục này đã đạt từ compile Windows/Editor.

## Nguồn đối chiếu chính thức

- [Unity 6.3 Web technical limitations](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/intro/technical-overview): managed threading, AOT và browser limitations.
- [Unity 6.3 Web networking](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/develop/networking): UWR dùng browser transport/CORS.
- [Unity 6.3 Web audio](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/develop/audio): audio restrictions và Microphone.
- [Photon Fusion topology/Web](https://doc.photonengine.com/fusion/v2/fusion-choose): Shared được khuyến nghị cho Web; host/server cần đánh giá riêng.
- SQLite kiểm tra trực tiếp `Library/PackageCache/com.gilzoide.sqlite-net@08248bd5884d/README.md:16–25,115–123` và `Runtime/SQLiteExtensions.cs:77–83`, không suy từ một bản SQLite Windows khác.

## Toàn bộ Scene Assets được kiểm kê

Số GameObject ở bảng sau là record serialized trong file, có thể thấp hơn hierarchy đã resolve prefab ở bảng phía trên. UI/control records gồm cả script điều khiển, không chỉ button. PackageCache SDK template không phải scene sản phẩm trong Assets.
|Scene asset|Phân loại|UI/control records serialized|Objects serialized|Kích thước file MiB|
|---|---|---:|---:|---:|
|Assets/(HDRP) NYC-Like City Buildings Set (PBR)/Sample.unity|Vendor/demo|1|8|0.02|
|Assets/ChiTietSaBan/ChiTiet/ChiTietSaBan.unity|Main/legacy|12|29|0.10|
|Assets/ChiTietSaBan/ChiTiet/SaBan.unity|Main/legacy|2|2|0.01|
|Assets/Cliff/LowPolyCliffPack/Example.unity|Vendor/demo|1|3|0.03|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_000.unity|Zone|0|171|0.44|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_001.unity|Zone|0|33|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_002.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_003.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_004.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_005.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_006.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_007.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_008.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_009.unity|Zone|0|33|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_010.unity|Zone|0|23|0.05|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_011.unity|Zone|0|24|0.06|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_012.unity|Zone|0|24|0.06|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_013.unity|Zone|0|29|0.07|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_014.unity|Zone|0|23|0.06|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_015.unity|Zone|0|29|0.07|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_016.unity|Zone|0|24|0.06|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_017.unity|Zone|0|24|0.06|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_018.unity|Zone|0|25|0.06|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_019.unity|Zone|0|22|0.05|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_020.unity|Zone|0|25|0.06|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_021.unity|Zone|0|25|0.06|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_022.unity|Zone|0|26|0.06|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_023.unity|Zone|0|24|0.06|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_024.unity|Zone|0|23|0.06|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_025.unity|Zone|0|24|0.06|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_026.unity|Zone|0|16|0.03|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_027.unity|Zone|0|33|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_028.unity|Zone|0|39|0.10|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_029.unity|Zone|0|33|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_030.unity|Zone|0|39|0.10|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_031.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_032.unity|Zone|0|33|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_033.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_034.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_035.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_036.unity|Zone|0|39|0.10|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_037.unity|Zone|0|33|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_038.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_039.unity|Zone|0|33|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_040.unity|Zone|0|39|0.10|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_041.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_042.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_043.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_044.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_045.unity|Zone|0|33|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_046.unity|Zone|0|39|0.10|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_047.unity|Zone|0|33|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_048.unity|Zone|0|39|0.10|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_049.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_050.unity|Zone|0|33|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_051.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_052.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_053.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_054.unity|Zone|0|39|0.10|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_055.unity|Zone|0|33|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_056.unity|Zone|0|36|0.09|
|Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_057.unity|Zone|0|160|0.41|
|Assets/ProjectLopHoc/Scenes/Classroom.unity|Main/legacy|53|203|0.64|
|Assets/ProjectLopHoc/Scenes/Login.unity|Main/legacy|26|57|0.20|
|Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity|Main/legacy|78|191|0.61|
|Assets/ProjectLopHoc/Scenes/Showroom.unity|Main/legacy|76|1625|25.65|
|Assets/ProjectLopHoc/Scenes/UTTCampus.unity|Main/legacy|18|72|14.26|
|Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh 1.unity|Main/legacy|21|26|0.61|
|Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh_SaBan.unity|Main/legacy|43|67|0.83|
|Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity|Main/legacy|33|53|0.75|
|Assets/SaBanUTTNew/ProjectLopHoc/Scenes/SaBan_Display_Preview.unity|Main/legacy|1|3|3.82|
|Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets/DemoScene.unity|Vendor/demo|31|57|0.26|
|Assets/StandaloneFileBrowser/Sample/BasicSampleScene.unity|Vendor/demo|1|2|0.01|
|Assets/StandaloneFileBrowser/Sample/CanvasSampleScene.unity|Vendor/demo|2|15|0.04|
|Assets/_Recovery/0 (1).unity|Recovery|13|219|0.51|
|Assets/_Recovery/0 (2).unity|Recovery|13|132|0.23|
|Assets/_Recovery/0 (3).unity|Recovery|27|36|0.64|
|Assets/_Recovery/0.unity|Recovery|19|87|14.91|
|Assets/_Recovery/1 (1).unity|Recovery|41|174|0.55|
|Assets/_Recovery/1.unity|Recovery|2|2|0.01|
|Assets/_Recovery/2.unity|Recovery|21|26|0.61|
|Assets/_Recovery/3.unity|Recovery|21|26|0.61|
|Assets/_Recovery/4.unity|Recovery|41|174|0.55|

## Bổ sung control và bảo toàn

Login chứa 2 XRRayInteractor, 2 XRInteractorLineVisual và TunnelingVignetteController; không thấy TeleportationProvider chuẩn trong các preview chính. Locomotion chủ yếu là FirstPersonController; Campus có MinimapTeleport riêng. `ProjectSettings/ProjectSettings.asset:957` là activeInputHandler=1 (new Input System), nhưng `MinimapTeleport (1).cs:25-40` dùng Input.GetKeyDown không guard. Cần adapter toggle/raycast cho cả M và VR, không tự chuyển global input sang Both.

SceneTeleporter của Showroom đã có core OnInteract dùng chung và resolve scene index/name; transition thực chưa thử. DesignDocument trong lifecycle hiện là mã metadata, không phải bằng chứng có viewer/upload/download tài liệu.

Đối chiếu SHA-256: **710 file Scene/script/database/settings/package được bảo vệ, 0 thay đổi**. Editor bàn giao vẫn mở QuanLyVongDoiTaiSan, sạch, không Play Mode; không đổi target Android, không tạo profile/branch/commit, không sửa package/Scene/SQLite. Các thay đổi có sẵn trước audit được giữ nguyên. Compile status của project không báo lỗi; lỗi sửa trong các probe tạm thời không được coi là lỗi runtime của game.
