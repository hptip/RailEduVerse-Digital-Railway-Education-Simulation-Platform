# Hiện trạng cầu và phạm vi đối chiếu

Baseline Git `6840780` có cầu SO 201 214 m tại Zone_018, đăng ký bằng `Bridge_Main` từ chainage 3.850,001 đến 4.063,722 m. Scene quản lý tài sản dùng bản Detailed cho cùng 91 mesh gốc. Bản Visual của tuyến không có identity BIM hoặc collider chi tiết của bản Detailed.

Đối chiếu asset và GUID của bản hiện hành đã xác nhận một instance Visual trong tuyến, một instance Detailed trong scene BIM và không có tham chiếu bắc cầu tới mesh của các phiên bản cầu cũ. Các đối tượng nằm gần và giao bounds với cầu gồm terrain, nền ray, mặt sông, hai bờ, approach và nền móng hiện hành. Chồng bounds với những bề mặt này chưa đủ để kết luận có hai phiên bản cầu trùng nhau.

Ứng viên mở rộng được dựng trong một scene sandbox riêng. Dữ liệu nguồn IFC/DAE, các prefab import, prefab tàu, hai scene sản phẩm, các Zone, database và hồ sơ BIM đã có được bảo toàn trong lần thực hiện này. Bản build sử dụng danh sách scene sản phẩm sẵn có, không đăng ký sandbox.

Các phép đo hỗ trợ quyết định giữ cầu 214 m nằm ở `Geometry_Validation.md`. Các bản thử cũ vẫn tồn tại để truy xuất và đối chiếu; việc gỡ các instance cầu khi thay phiên bản phụ thuộc vào nghiệm thu ứng viên mở rộng.
