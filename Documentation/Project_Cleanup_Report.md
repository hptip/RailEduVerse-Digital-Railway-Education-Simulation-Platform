# Dọn dung lượng UTTVR · 27/11/2026

Project đo theo tổng dung lượng file. Dung lượng trống đo riêng trên ổ F. Kiểm kê bỏ qua junction và symbolic link. Lượt đầu ghi nhận 131.957 file và 0 lỗi đọc. Lượt cuối bỏ qua 0 mục.

| Chỉ số | Kết quả |
| --- | ---: |
| Project trước | 128,30 GiB |
| Project sau | 53,79 GiB |
| Dung lượng file đã xóa | 74,51 GiB |
| Dung lượng trống ổ F tăng | 74,51 GiB |
| File đã xóa | 3.019 |

## Đã xóa

| Nhóm | File | GiB |
| --- | ---: | ---: |
| Cache HTTP của profile Chrome kiểm thử riêng | 2.268 | 51,65 |
| Build Web thí nghiệm đã được thay thế | 389 | 21,76 |
| Web build root cũ cùng ngày 26/11 | 20 | 0,92 |
| Ảnh Web thử nghiệm trung gian đã lưu trong Git | 341 | 0,15 |
| ZIP evidence trùng SHA-256 73/73 với Evidence/Production | 1 | 0,03 |

Build và cache đã xóa là đầu ra ignored có thể tạo lại. Ảnh thử nghiệm trung gian đã được đối chiếu với Git trước khi bỏ khỏi working tree. ZIP đã xóa có 73/73 mục trùng SHA-256 với nội dung nằm ngoài ZIP. Danh sách từng file nằm trong CSV.

## Thư mục cấp cao

| Thư mục | Trước GiB | Sau GiB |
| --- | ---: | ---: |
| `Temp` | 63,53 | 11,88 |
| `Builds` | 29,71 | 7,02 |
| `Library` | 15,38 | 15,38 |
| `Assets` | 13,60 | 13,60 |
| `.git` | 4,96 | 4,96 |
| `Documentation` | 0,49 | 0,31 |
| `UserData` | 0,35 | 0,35 |
| `Backups` | 0,20 | 0,20 |
| `_SaBanImportBackup_d9791aeb` | 0,08 | 0,08 |
| `Logs` | 0,00 | 0,00 |
| `.utmp` | 0,00 | 0,00 |
| `outputs` | 0,00 | 0,00 |
| `Backend` | 0,00 | 0,00 |
| `UTT Offline VR Demo_BurstDebugInformation_DoNotShip` | 0,00 | 0,00 |
| `UserSettings` | 0,00 | 0,00 |
| `ProjectSettings` | 0,00 | 0,00 |
| `Packages` | 0,00 | 0,00 |
| `.vscode` | 0,00 | 0,00 |
| `.codex` | 0,00 | 0,00 |
| `UTTVR` | 0,00 | 0,00 |

Trước dọn, Library/Artifacts chứa 4,77 GiB, Library/Bee 4,70 GiB, Library/PlayerDataCache 3,37 GiB và Library/PackageCache 1,83 GiB. Tổng backup, snapshot và ZIP nằm ngoài .git/Library trước dọn khoảng 1,32 GiB. Không tìm thấy APK trong project.

## Tài nguyên giữ theo bằng chứng

- Kiểm tra 4.692 root Unity và tên/GUID serialize của 868 mesh StaticRailwayMap. Unity xác nhận 693 mesh có dependency. Cả 175 mesh còn lại xuất hiện theo GUID trong dữ liệu đã kiểm, bao gồm scene sao lưu. Không có mesh nào đạt tập điều kiện xóa. Thư mục này chiếm khoảng 7,41 GiB.
- G2 Production preflight còn đọc scene G2 Pilot. Detailed Loader của sản phẩm nằm cùng khu vực mã G2 Pilot. Giữ công cụ bảo trì, output nguồn, BIM và rollback đang có đường dẫn phụ thuộc.
- Bốn nhóm tài nguyên nguồn trùng SHA-256 có GUID riêng. Giữ các bản sao vì chưa chứng minh có thể loại một GUID mà bảo toàn toàn bộ tham chiếu. Giữ các snapshot khác nội dung và các scene thử có tham chiếu chéo.
- Giữ bản Windows OneClick và ba build SO 201 đang được máy chủ local phục vụ. Giữ dữ liệu profile/IndexedDB, SQLite, hồ sơ Excel và cache Library của Editor đang hoạt động.

## Kiểm tra cuối

- Unity mở lại 24 scene trong danh sách build. Registry tuyến có 17 Zone và liên kết tàu/Spline hợp lệ. Zone_018 có SO 201. Có root Infrastructure_Production_v1 tại cả chín Zone_010 đến Zone_018. Đối chiếu 99 file bảo vệ gồm SO 201, scene tuyến và script tàu với snapshot Production khớp SHA-256.
- AssetDatabase kiểm 2.905 dependency, có 0 đường dẫn Assets đã resolve bị mất. Catalog tại Scene mở được và chứa 14.504 AssetID duy nhất. Có 86 asset Detailed. Quét 135 scene và prefab cho thấy 4 trong 1.335 GUID chưa resolve được. Chưa có phép đếm GUID tương ứng trước dọn.
- Editor dừng Play Mode và không báo đang compile. Đếm 1.040 vị trí Missing Script trong scene, bằng với số đã đo trước dọn. Kiểm 111 prefab và thấy 712 vị trí Missing Script trong prefab. Chưa có baseline riêng cho prefab. Các batch chỉ xóa build, cache, ZIP thừa và ảnh, nên hiện trạng Missing Script cần xử lý riêng.
- Bốn file lõi của Web Production khớp SHA-256 trước/sau. HTTP local trả 200. Build Report hiện hành ghi 0 lỗi và 17 cảnh báo từ lần build đã nghiệm thu.
- File UserData/QuanLyVongDoiTaiSan.db tồn tại, có header SQLite 3 và dung lượng 376.983.552 byte. PRAGMA quick_check ở chế độ chỉ đọc trả ok cho cả UserData và SQLite seed. Chưa chạy Play Mode, build mới hoặc kiểm thử VR. Không lấy HTTP 200 làm kết luận source sạch lỗi.
- Git còn các thay đổi source từ trước và danh sách ảnh lịch sử đã xóa. Mục build và cache là ignored. Hai báo cáo là untracked. Không commit hoặc thay đổi lịch sử Git.

## 30 nhóm lớn trước khi dọn

| Thư mục hoặc nhóm | GiB |
| --- | ---: |
| `Temp/SO201Original` | 20,44 |
| `Temp/InfrastructureProduction` | 13,67 |
| `Temp/BridgePhase2` | 11,21 |
| `Assets/ProjectLopHoc` | 10,61 |
| `Assets/ProjectLopHoc/Generated` | 9,69 |
| `Temp/UTTVietnameseFonts` | 6,96 |
| `Library/Artifacts` | 4,77 |
| `Library/Bee` | 4,70 |
| `Library/Bee/artifacts` | 4,43 |
| `Library/PlayerDataCache` | 3,37 |
| `Temp/BimTreeChromeTest` | 2,96 |
| `Temp/BimTreeChromeTest/Default` | 2,84 |
| `Library/PlayerDataCache/Android` | 2,28 |
| `Temp/SO201Original/Chrome-ExtendedCameraDrive` | 1,93 |
| `Library/PackageCache` | 1,83 |
| `Temp/ZoneBasedBimChromeTest` | 1,67 |
| `Temp/BridgeModularChromeTest` | 1,63 |
| `Temp/UTTVietnameseFonts/Chrome-VerifiedFontScenes` | 1,56 |
| `Temp/ZoneBasedBimChromeTest/Default` | 1,51 |
| `Temp/InfrastructureProduction/Chrome-InfrastructureF1` | 1,43 |
| `Temp/BridgeModularChromeTest/Default` | 1,42 |
| `Temp/BridgePhase2/Chrome-prod-bim` | 1,40 |
| `Temp/SO201Original/Chrome-Headed1` | 1,36 |
| `Temp/InfrastructureProduction/Chrome-InfrastructureBimSettled` | 1,34 |
| `Temp/InfrastructureProduction/Chrome-workbook-diagnose` | 1,34 |
| `Temp/UTTVietnameseFonts/Chrome-SixScenesFont` | 1,34 |
| `Temp/SO201Original/Chrome-Controls_1` | 1,33 |
| `Temp/InfrastructureProduction/Chrome-InfrastructureBimFinal` | 1,33 |
| `Temp/InfrastructureProduction/Chrome-workbook-fix-final` | 1,33 |
| `Temp/SO201Original/Chrome-FinalBim` | 1,32 |

## 30 file lớn trước khi dọn

| File | GiB |
| --- | ---: |
| `.git/objects/pack/pack-072626ab794784aaf7cbce2ec4bdbea163843b22.pack` | 3,23 |
| `Library/PlayerDataCache/Android/Data/data.unity3d` | 2,28 |
| `Temp/InfrastructureProduction/Chrome-InfrastructureF1/Default/Service Worker/CacheStorage/7491ad518bf54e38f67b99570cb4d63fb7bb1e9b/f97f4ab5-329d-402e-bf3e-45cd7e697b29/370311acc7f0054b_0` | 1,10 |
| `Temp/InfrastructureProduction/Chrome-InfrastructureOffline/Default/Service Worker/CacheStorage/7491ad518bf54e38f67b99570cb4d63fb7bb1e9b/c919e3da-554b-4ca2-8f9f-dd94dddd9206/370311acc7f0054b_0` | 1,10 |
| `Temp/InfrastructureProduction/Chrome-InfrastructureProduction/Default/Service Worker/CacheStorage/7491ad518bf54e38f67b99570cb4d63fb7bb1e9b/5e86a007-40dd-40c4-b1c7-19bad68a8a12/370311acc7f0054b_0` | 1,10 |
| `Temp/InfrastructureProduction/Chrome-InfrastructureRegression/Default/Service Worker/CacheStorage/7491ad518bf54e38f67b99570cb4d63fb7bb1e9b/bdda17ea-9b24-4a2d-9535-8e36374c53e9/370311acc7f0054b_0` | 1,10 |
| `Temp/InfrastructureProduction/Chrome-workbook-fix-final/Default/Service Worker/CacheStorage/7491ad518bf54e38f67b99570cb4d63fb7bb1e9b/fa0b034e-ac5b-4409-8b68-04aef8e14829/370311acc7f0054b_0` | 1,10 |
| `Temp/InfrastructureProduction/Chrome-InfrastructureBimFinal/Default/Service Worker/CacheStorage/7491ad518bf54e38f67b99570cb4d63fb7bb1e9b/466eff0c-23b8-4f38-a37c-b641692d1e78/370311acc7f0054b_0` | 1,10 |
| `Temp/InfrastructureProduction/Chrome-InfrastructureBimSettled/Default/Service Worker/CacheStorage/7491ad518bf54e38f67b99570cb4d63fb7bb1e9b/36b8b5bd-76be-4e24-85ea-b7c96eac6ed1/370311acc7f0054b_0` | 1,10 |
| `Temp/InfrastructureProduction/Chrome-InfrastructureReloadFinal/Default/Service Worker/CacheStorage/7491ad518bf54e38f67b99570cb4d63fb7bb1e9b/f7a79ee4-113d-461e-8b3a-19ac44dcfca4/370311acc7f0054b_0` | 1,10 |
| `Temp/InfrastructureProduction/Chrome-model-pick-verified/Default/Service Worker/CacheStorage/7491ad518bf54e38f67b99570cb4d63fb7bb1e9b/2c419d25-394d-4d95-98dc-e05b3893dee4/370311acc7f0054b_0` | 1,10 |
| `Temp/InfrastructureProduction/Chrome-workbook-diagnose/Default/Service Worker/CacheStorage/7491ad518bf54e38f67b99570cb4d63fb7bb1e9b/b680effc-3962-4479-974a-659f0876e9d7/370311acc7f0054b_0` | 1,10 |
| `Builds/UTTVR_Infrastructure_Production_Web/Build/UTTVR_Infrastructure_Production_Web.data` | 1,10 |
| `Library/Bee/artifacts/WebGL/webgl.data` | 1,10 |
| `Library/PlayerDataCache/WebGL/Data/data.unity3d` | 1,08 |
| `Temp/SO201Original/Chrome-Controls_1/Default/Service Worker/CacheStorage/4c7944dd0bd772e50a9abe7ac115643bdac7baaf/754996a8-fda1-4c85-9c35-a852cd14c0e7/59f6301ed7e401c2_0` | 1,08 |
| `Temp/SO201Original/Chrome-Headed1/Default/Service Worker/CacheStorage/4c7944dd0bd772e50a9abe7ac115643bdac7baaf/b1fc932a-ca86-4895-8689-e4070327c420/59f6301ed7e401c2_0` | 1,08 |
| `Temp/UTTBimOutline/Chrome-WebTest/Default/Service Worker/CacheStorage/533697bda66f61394055ce9ec4eea082fc1a1def/095caadb-66c0-47f9-a81f-7ebb7a93a7b4/f5b4e5cc2d5b74bd_0` | 1,08 |
| `Builds/UTTVR_Vietnamese_Web/Build/UTTVR_Vietnamese_Web.data` | 1,08 |
| `Temp/UTTVietnameseFonts/Chrome-screenshots/Default/Service Worker/CacheStorage/533697bda66f61394055ce9ec4eea082fc1a1def/ab72ffa2-f3ea-4695-87ac-c4b03552b846/f5b4e5cc2d5b74bd_0` | 1,08 |
| `Temp/BridgePhase2/Chrome-prod-bim/Default/Service Worker/CacheStorage/614f5f070c6c476730cc4b9febe48737c1545d10/4a7c8b92-2ca6-4043-bffc-44c0896a5d51/a10089a6c899dabf_0` | 1,08 |
| `Temp/BridgePhase2/Chrome-prod-train/Default/Service Worker/CacheStorage/614f5f070c6c476730cc4b9febe48737c1545d10/98ce53d2-2191-4d04-9427-23744c91eff7/a10089a6c899dabf_0` | 1,08 |
| `Builds/SO201_Production_Web/Build/SO201_Production_Web.data` | 1,08 |
| `Temp/BridgePhase2/Chrome-so201-optimized/Default/Service Worker/CacheStorage/c6c2fb058457c94aa4b166cea14dc8c829fd6f48/81679d94-d108-413c-ad4e-8831e071f978/fc065cb15cfaec61_0` | 1,08 |
| `Builds/SO201_Railway_Trial/Build/SO201_Railway_Trial.data` | 1,08 |
| `Temp/SO201Original/Chrome-SevenSpanReentry/Default/Service Worker/CacheStorage/a3eec87265f461753e4973b2810bb1f034d98134/a4bfcc52-c206-48a7-9967-b84b22f0bd33/460845a4ac14613d_0` | 1,07 |
| `Temp/SO201Original/Chrome-SevenSpanReload/Default/Service Worker/CacheStorage/a3eec87265f461753e4973b2810bb1f034d98134/9115b073-67f0-435a-83c9-324b0eaa36e0/460845a4ac14613d_0` | 1,07 |
| `Temp/SO201Original/Chrome-SevenSpanTrain/Default/Service Worker/CacheStorage/a3eec87265f461753e4973b2810bb1f034d98134/aff802b1-2d6c-4e82-b716-8c32a421b9e4/460845a4ac14613d_0` | 1,07 |
| `Builds/UTTVR_SO201_SevenSpan_Web/Build/UTTVR_SO201_SevenSpan_Web.data` | 1,07 |
| `Temp/SO201Original/Chrome-CameraCapturedInput/Default/Service Worker/CacheStorage/b80f18b71d7362f19b78d6dbe4dbee9bb38080e6/57179198-d84e-4e43-883c-5b50d03b8db3/f39723f681b8c6e9_0` | 1,07 |
