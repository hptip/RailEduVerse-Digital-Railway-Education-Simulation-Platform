# Snapshot trước thay đổi

- `Snapshots/Before_Extension.zip` chứa phiên bản Git `6840780` của scene tuyến, scene BIM, các Zone liên quan và những asset đã chọn để đối chiếu.
- `Snapshots/Protected.sha256` ghi mã kiểm SHA-256 của nguồn DAE/prefab, tàu và các scene được bảo vệ. IFC nguồn được ghi bằng đường dẫn tuyệt đối.
- Cầu mẫu nằm ở `Assets/ExternalBIM/SO201Extended` với công cụ dựng trong `Assets/Editor/SO201ExtendedBridgeSandbox.cs`. Mẫu nằm ngoài danh sách Build và hiện chưa được đăng ký vào streaming.
- Khi cần hoàn tác phần camera hoặc font, so sánh các file C# tương ứng với phiên bản trước thay đổi; phục hồi riêng từng phần thay vì thay toàn bộ project.
