# Kiểm chứng ứng viên cầu SO 201

## Phạm vi

Unity đã dựng một mẫu năm nhịp trong scene riêng `SO201_Extended_Prototype.unity`. Mẫu dùng lại prefab nhập từ DAE, giữ tỷ lệ 2,4× và lặp cụm nhịp giữa. Hai mố chỉ xuất hiện ở hai đầu. Mỗi vị trí gối giữa có một cụm trụ và móng. Các đoạn mặt cầu và gờ biên vượt qua ranh giới nhịp được cắt từ mesh nguồn. Mã occurrence trong bảng CSV tách riêng với IFC GlobalId nguồn.

Mẫu đo được dài 376,921 m theo mặt cầu, có 4 trụ, 2 mố và 46 cọc. Trong 148 occurrence, 105 dùng mesh DAE trực tiếp và 43 dùng mesh dẫn xuất từ DAE. Kiểm tra bảng mapping không phát hiện trùng mã occurrence. Khoảng cách hai tim trụ của nhịp giữa là 81,600 m. Nếu bổ sung thêm hai nhịp giữa vào mẫu năm nhịp thì mặt cầu dự kiến dài 540,121 m, với 7 nhịp, 6 trụ, 2 mố và 62 cọc. Đây là kích thước dự kiến của ứng viên.

## Cổng kiểm chứng trước khi triển khai

- Bản lặp trực tiếp có bậc đứng tới 0,562 m ở mối nối mặt cầu. Hiệu chỉnh độ dốc dọc tại các nhịp lặp hạ sai số xuống tối đa 0,00007 m ở năm điểm đo ngang. Hai đỉnh thân trụ gốc P2/P3 chênh 0,600 m sau scale.
- Ứng viên dây cung thẳng dài 540,121 m lệch tim tuyến tối đa 0,147 m trong khu vực cầu cũ. Khoảng trống bên hông theo bao mặt cầu 18,085 m là 3,981 m. Phép đo này chưa bao gồm gờ biên và đáy ray.
- Mesh IFC `Approach slab O1` có hình học ở cả hai bờ. Hai phần đã được tách bằng mặt phẳng cắt ở vùng giữa cầu và di chuyển theo mố tương ứng. Hai occurrence có chung GlobalId nguồn, mỗi occurrence có một mã riêng.
- Kiểm tra 24 cặp bounds bệ gối–dầm chồng phủ theo phương ngang cho thấy đáy bounds dầm cao hơn đỉnh bệ gối từ 0,9312 đến 1,9277 m. Chưa có bằng chứng kết nối bằng hình học trung gian tại các cặp này. Cần giải quyết tiếp điểm chịu đỡ, độ chôn móng tại đáy sông, khoảng hở thực tế với hai đường ray và chạy tàu qua ứng viên đã căn chỉnh.
- Xem `Deck_Joint_Samples.csv`, `Bearing_Bounds_Audit.csv`, `SO201_Component_Envelope.csv` và `Railway_Compatibility_Report.md` để đối chiếu số liệu.
- Cầu 214 m hiện hành tiếp tục phục vụ các scene sản phẩm. Ứng viên chỉ xuất hiện trong sandbox. Các chỉnh sửa địa hình, nước, cấu kiện BIM và việc thay cầu sẽ phụ thuộc vào nghiệm thu ứng viên.
