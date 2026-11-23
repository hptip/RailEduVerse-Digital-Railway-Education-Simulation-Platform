# Kiểm chứng import IFC Bridge SO 201

## Nguồn và công cụ

- IFC gốc `F:/BIM_Workspace/BridgeDemo/original/demo-bridge-so201.ifc`, 1.071.991 byte, schema **IFC4X3_ADD2**, Reference View. SHA-256 `F967E72EA1E7648A5D2BF82DB806E4AB2F24397177ED2D6E9B795288F0AA261D`.
- `F:/BIM_Workspace/Tools/IfcConvert` có thư viện Python **IfcOpenShell 0.9.0post1** dành cho Python 3.13. Tìm trong thư mục Tools không thấy `IfcConvert.exe`. Đã tải bản portable từ phát hành chính thức vào `Temp/BridgePilot/OfficialTool`, chạy `--version` xác nhận executable tự báo **IfcConvert 0.8.0, OCC 7.8.1** dù archive phát hành mang tên 0.8.4. Không cài tool toàn hệ thống. [IfcConvert · cài đặt](https://docs.ifcopenshell.org/ifcconvert/installation.html) · [Cách dùng](https://docs.ifcopenshell.org/ifcconvert/usage.html)
- Lệnh đã chạy là `IfcConvert.exe --center-model --use-element-guids -j 4 <IFC gốc> <DAE thử nghiệm>`. Bản DAE xuất được chép vào `Assets/ExternalBIM/BridgeDemo/Models/Bridge_SO201.dae`.

## Dữ liệu và liên kết

| Kiểm tra | Kết quả |
| --- | --- |
| IFC gốc | 105 `IfcProduct` + 1 `IfcProject`. 91 sản phẩm có biểu diễn hình học tạo được tam giác. 15 node điều hướng gồm Project, Site, Bridge và 12 BridgePart. |
| Cây nguồn | 5 quan hệ `IfcRelAggregates`, 10 `IfcRelContainedInSpatialStructure`, 0 `IfcRelNests`. Không có cha nhập nhằng. Project là gốc duy nhất. CSV ghi GlobalId cha và loại quan hệ; JSON giữ cả STEP ID và GlobalId của quan hệ. |
| Dữ liệu nghiệp vụ | Giữ `GlobalId`, IFC class, Name, Description/Tag/PredefinedType khi có, đơn vị, biểu diễn, quan hệ, bounds và số hình học đo được. File này không chứa `IfcPropertySet`, `IfcElementQuantity`, `IfcRelDefinesByProperties`, `IfcRelDefinesByType` hay `IfcRelAssociatesMaterial`. Có 15 `IfcSurfaceStyle`, đây là dữ liệu màu/hiển thị. Vòng đời và Pset hiển thị “Chưa có dữ liệu”. |
| DAE | Có 91 node có geometry tham chiếu, 91 GlobalId riêng, khớp chính xác tập GlobalId thực sự có hình học. Không cần phương án dự phòng. DAE giữ đơn vị metre và `Z_UP`. |
| Unity 6000.3.0f1 | **91/91 GlobalId** được gắn `BridgeBimIdentity`, 91 MeshFilter, 91 MeshRenderer và 91 MeshCollider trong prefab. Binding dùng chuỗi GlobalId từ DAE làm khóa vận chuyển để tạo trường serialized. Tên hiển thị và thứ tự cây chỉ là dữ liệu trình bày. |
| Tam giác | IfcOpenShell Python 0.9 tạo 36.175 tam giác, CLI 0.8.0 tạo 36.099. Unity giữ đúng **36.099** và khớp số tam giác DAE **theo từng GlobalId**. Giữ 91 mesh riêng. Chênh lệch giữa hai lượt tessellation không được diễn giải là có thao tác giảm mesh. |
| Đơn vị và vị trí | Bounds Unity **89,32117 × 27,76169 × 19,83568 m** theo X/Y/Z. Quan hệ trục đo được `Unity X = -DAE X, Unity Y = DAE Z, Unity Z = -DAE Y`. Cực trị từng cấu kiện lệch tối đa **0,00001162 m** so với DAE sau quy đổi hệ trục và offset chung. Unity dùng scale 1 và mesh compression 0 trên bản DAE thử. |
| Offset trung tâm | `IFC world − DAE` đo được gần **(-743001,981692; -1043001,387521; 494,338131) m**. Cực trị IFC/DAE sau trừ offset khớp trong sai số số học của mẫu. Centering tạo hệ trục để quan sát, cần phép đăng ký tọa độ riêng khi đưa vào tuyến. |
| Material | 15 biến thể URP Lit được tạo trong sandbox, lấy màu từ material import DAE. DAE không khai báo ảnh texture hoặc transparency. Material của prefab thử và material highlight có đường dẫn riêng. |

Các lớp có hình học gồm IfcPile 30, IfcBeam 17, IfcSlab 13, IfcFooting 12, IfcWall 6, IfcCovering 4, IfcEarthworksFill 4, IfcColumn 2, IfcPipeSegment 2 và IfcPavement 1. Một bằng chứng mapping cụ thể là `Pier stem P2`, `IfcColumn`, `0Fx3Ykz9Zoo5T0r0sZtWPp`, trỏ về AssetID `BRIDGE-SO201-0Fx3Ykz9Zoo5T0r0sZtWPp` và renderer/collider riêng.

[IFC inventory](IFC_Element_Inventory.csv) · [Dữ liệu JSON](IFC_Properties.json) · [Mapping Unity](IFC_Unity_Mapping.csv) · [Transport gồm kích thước từng GlobalId](../../../Assets/ExternalBIM/BridgeDemo/Models/BridgeGeometryTransport.json)

## Tệp thử nghiệm và phạm vi

- [Scene thử](../../../Assets/ExternalBIM/BridgeDemo/Testing/BridgeBIM_Test.unity) · [Prefab](../../../Assets/ExternalBIM/BridgeDemo/BridgeBIM_Demo.prefab) · [Catalog](../../../Assets/ExternalBIM/BridgeDemo/BridgeBIM_Catalog.asset).
- Prefab là instance thử đã tách liên kết với model import để giữ binding và material trình bày riêng. Mesh vẫn tham chiếu subasset DAE. Có thể tạo instance của prefab ở scene khác sau khi duyệt quyền định vị và tích hợp.
- File IFC gốc, Scene chính, Zone, Showroom, SQLite và Build Profile thông thường giữ nguyên. Tác vụ import chỉ lưu scene thử nghiệm và assets dưới `Assets/ExternalBIM/BridgeDemo`. [Báo cáo tương thích tuyến](Railway_Compatibility_Report.md)

[Ảnh WebGL với toàn bộ cầu trong khung nhìn](Screenshots/Web_01_Overview.png) · [Ảnh chọn và đối chiếu IFC](Screenshots/Web_03_Properties.png) · [Kết quả WebGL](Web_Performance_Test.md)

Đã so SHA-256 của 15 tệp được bảo vệ trước và sau thử nghiệm. IFC gốc, các Scene chính kiểm tra, cầu hiện hành, Zone_018, hai SQLite, Build Profile và thiết lập project trong danh sách đều khớp. Unity Batch Mode tự bỏ một mục preloaded asset trong Player Settings khi thoát. Mục này đã được khôi phục theo SHA-256 gốc.
