# Thay đổi và rollback

- Cầu bảy nhịp dùng bộ asset Visual, Detailed, catalog và địa hình trong `Assets/ExternalBIM/SO201Extended/Finalization/Placed`.
- Hai scene sản phẩm, ba scene Zone_017–019 và ba prefab tương ứng trong RouteBimZones đã cập nhật. Bản Visual sở hữu bởi Zone_018. Streaming giữ cả Zone_017 và Zone_018 khi tàu đi qua phạm vi cầu.
- Công cụ đặt cầu và kiểm tra mặt mesh nằm trong `SO201ExtendedBridgeIntegration.cs`, `SO201BridgeContactValidation.cs`, `SO201ExtendedBridgeSandbox.cs` và bộ chạy `SO201OriginalBridgeAudit.cs` dưới `Assets/Editor`.
- Cập nhật tab vòng đời chỉ hiển thị dữ liệu mô phỏng cho occurrence mới khi hồ sơ người dùng còn trống. 91 ID gốc vẫn trỏ tới nguồn dữ liệu hiện hành. SQLite đường ray và tính năng Excel tiếp tục sử dụng code cũ.
- `Before_Finalization.zip` lưu scene, prefab Zone và mẫu trước đợt thay thế. Để rollback bản cầu, khi Unity đã đóng các scene cần khôi phục, trích đúng các đường dẫn scene và prefab Zone từ ZIP về workspace, rồi refresh và xác nhận `Bridge_SO201_Original` trong Zone_018. Bản IFC/DAE và asset cầu 214 m vẫn còn để rollback. Khi cần rollback code, hoàn tác riêng các thay đổi trong danh sách trên để giữ thay đổi font và camera có trước.
