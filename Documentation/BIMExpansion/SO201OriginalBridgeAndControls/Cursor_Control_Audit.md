# Kiểm kê xử lý con trỏ

Kiểm kê code và thành phần crosshair trong scene cho thấy FirstPersonController làm chủ look, ray và locomotion ở bốn scene gameplay. Trước thay đổi Web dùng giữ chuột phải để look và dùng tọa độ con trỏ cho ray. Build native dùng lock trực tiếp. SceneQuickMenu, DashboardManager, QuizPopupUI, bản đồ, thông tin tàu và chatbot từng riêng lẻ sửa con trỏ hoặc crosshair. CrosshairInteractor cũng có nhánh ray độc lập.

Login dùng con trỏ cho giao diện. QuanLyVongDoiTaiSan dùng pan, orbit và chọn 3D trong viewport cạnh panel. DemoPlayer trong demo chatbot có mouse look riêng. Mục tiêu tương thích ghi tại [ma trận](Scene_Input_Compatibility_Matrix.csv).
