# UTTVR Infrastructure Design Kit v1.0 — bắt đầu tại đây
1. Giải nén ZIP vào `F:\GITHUB\UTTVR` để có `Documentation\InfrastructureDesign\...`. Không copy vào Assets. Nếu đã có thư mục cùng tên, sao lưu/merge, không ghi đè bằng tay khi có thay đổi.
2. Giữ bốn file nguồn ở các đường dẫn trong Source_Manifest.json (kit không chứa model nguồn vì bạn đã tải; quyền phân phối cần kiểm tra). Không cần chuyển IFC thủ công. Giữ tên file và texture nếu có.
3. Mở Codex đúng project root. Gửi nguyên nội dung Prompt_A.md. Codex đọc hồ sơ tại máy, không cần đính kèm từng ảnh hay paste JSON. Yêu cầu merge AGENTS_Addendum vào AGENTS hiện có.
4. Sau G1, xem ảnh Unity trong Evidence/Screenshots và report, đối chiếu 4 concept. Nếu chưa ưng, nêu chính xác phần cần chỉnh, chạy lại validator riêng phần đó. Nếu ưng, xác nhận rõ cả OCL/Tunnel/Subgrade/BIMViewer (hoặc phạm vi riêng) trong Codex. Chỉ sau đó gửi Prompt_B.md.
5. G2/G3 tạo full output và tích hợp; chỉ nghiệm thu khi có Web thực chạy và test rõ. VR NOT_TESTED nếu không Quest. G1 và G3 evidence-check script không kiểm tra Unity trực tiếp.

Bốn sheet là concept AI; bỏ qua mọi thông số/kích thước/tiêu chuẩn in trên ảnh. Không thay model cầu/tàu theo ảnh. Không cần tải thêm nguồn trả phí. Mọi dữ liệu thiết kế đã duyệt nằm trong Markdown/JSON; kích thước chưa xác định do Codex đo và ghi riêng.
