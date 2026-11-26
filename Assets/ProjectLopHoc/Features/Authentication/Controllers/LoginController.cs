using UnityEngine;
using System;
using System.Threading.Tasks;
using ProjectLopHoc.Core.ScriptableObjects;
using ProjectLopHoc.Features.Authentication.Models;
using ProjectLopHoc.Features.Authentication.Views;

namespace ProjectLopHoc.Features.Authentication.Controllers
{
    public class LoginController : MonoBehaviour
    {
        [Header("--- Tham chiếu cấu hình ---")]
        public ClassroomConfigSO classroomConfig;

        [Header("--- Tham chiếu View ---")]
        public LoginView loginView;

        private LoginModel _model;
        private string _selectedRole = "student";
        private bool _loginInProgress = false;

        public static event Action<string, bool> OnLoginSuccess;

        private float _acceptLoginAfter;
        private void Start()
        {
            if (UttWebPresentation.Active) _acceptLoginAfter = Time.unscaledTime + 0.5f;
            if (loginView == null)
            {
                Debug.LogError("[LoginController] LoginView chưa được gán!");
                return;
            }

            _model = new LoginModel();
            loginView.SetInitialCredentials("sv01", "123456");
            
            // Luôn hiện trường mật khẩu để cả Sinh viên lẫn Giảng viên đều phải nhập
            loginView.SetPasswordFieldVisible(true);

            loginView.OnRoleChanged += HandleRoleChanged;
            loginView.OnLoginClicked += HandleLogin;

            // Bản Web: tab này đã đăng nhập trước khi bấm F5 thì vào lại luôn, không bắt nhập lại.
            if (UttWebPresentation.Active && UttSession.IsLoggedIn)
                ResumeSavedSession();
        }

        private async void ResumeSavedSession()
        {
            if (_loginInProgress || DataBridge.Instance == null) return;
            _loginInProgress = true;
            loginView.SetError("Đang khôi phục phiên đăng nhập...");

            bool valid = await DataBridge.Instance.ValidateSessionAsync();
            if (this == null || loginView == null) return;
            if (!valid)
            {
                UttSession.Clear();
                loginView.SetError("Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.");
                _loginInProgress = false;
                return;
            }

            await EnterCampusAsync(UttSession.DisplayName, UttSession.IsLecturer);
        }

        private void OnDestroy()
        {
            if (loginView != null)
            {
                loginView.OnRoleChanged -= HandleRoleChanged;
                loginView.OnLoginClicked -= HandleLogin;
            }
        }

        private void HandleRoleChanged(string role)
        {
            if (_loginInProgress) return;
            _selectedRole = role;
            loginView.SetError("");
        }

        private async void HandleLogin()
        {
            if (UttWebPresentation.Active && Time.unscaledTime < _acceptLoginAfter) return;
            if (_loginInProgress) return;

            if (loginView == null) return;

            string inputUsername = loginView.GetInputName();
            string inputPassword = loginView.GetInputPassword();

            if (string.IsNullOrWhiteSpace(inputUsername) || string.IsNullOrWhiteSpace(inputPassword))
            {
                loginView.SetError("Vui lòng nhập đầy đủ Mã tài khoản và Mật khẩu!");
                return;
            }

            if (DataBridge.Instance == null)
            {
                loginView.SetError("Lỗi hệ thống: Không tìm thấy DataBridge trong Scene!");
                return;
            }

            _loginInProgress = true;
            loginView.SetError(DataBridge.Instance.Offline ? "Đang xác thực tài khoản offline..." : "Đang xác thực tài khoản...");

            // =========================================================
            // 1. XÁC THỰC TÀI KHOẢN VỚI SQLITE SERVER
            // =========================================================
            var (isSuccess, message) = await DataBridge.Instance.LoginAsync(inputUsername, inputPassword);

            if (!isSuccess)
            {
                loginView.SetError(message);
                _loginInProgress = false; // Mở khóa để người dùng nhập lại
                return;
            }

            // =========================================================
            // 2. KIỂM TRA KHỚP VAI TRÒ (ROLE) GIỮA GIAO DIỆN & CSDL
            // =========================================================
            string userRole = DataBridge.Instance.CurrentRole; // "student", "teacher", "admin"
            bool roleMatches =
                (_selectedRole == "student" && userRole == "student") ||
                (_selectedRole == "teacher" && userRole == "teacher") ||
                (_selectedRole == "admin" && userRole == "admin");

            if (!roleMatches)
            {
                DataBridge.Instance.Logout();
                loginView.SetError("Vai trò đã chọn không khớp với tài khoản.");
                _loginInProgress = false;
                return;
            }

            bool isActuallyTeacher = userRole == "teacher";

            // =========================================================
            // 3. ĐỒNG BỘ DỮ LIỆU TÊN THẬT VÀO HỆ THỐNG
            // =========================================================
            string realFullName = DataBridge.Instance.CurrentFullName;
            _model.SetUserData(realFullName, isActuallyTeacher);

            // Tên, vai trò, token đã nằm trong UttSession (DataBridge.LoginAsync ghi).
            // Bản Web không ghi PlayerPrefs để 2 tab đăng nhập 2 tài khoản không đè lên nhau.
            await EnterCampusAsync(realFullName, isActuallyTeacher);
        }

        private async Task EnterCampusAsync(string realFullName, bool isActuallyTeacher)
        {
            loginView.SetError("Đang kết nối vào khuôn viên UTT...");

            try
            {
                OnLoginSuccess?.Invoke(realFullName, isActuallyTeacher);
            }
            catch (Exception ex)
            {
                Debug.LogError("[LoginController] Lỗi event: " + ex);
            }

            // =========================================================
            // 4. KẾT NỐI PHOTON FUSION
            // =========================================================
            if (NetworkManager.Instance != null)
            {
                bool started = await NetworkManager.Instance.StartCampusSession(realFullName, isActuallyTeacher);
                // Campus may already have unloaded the Login scene by the time Fusion returns.
                if (loginView == null) return;
                if (started) loginView.gameObject.SetActive(false);
                else
                {
                    loginView.SetError("Không thể tải UTTCampus. Vui lòng thử lại.");
                    _loginInProgress = false;
                }
            }
            else
            {
                loginView.SetError("Lỗi: Không tìm thấy NetworkManager trong Scene!");
                _loginInProgress = false;
            }
        }
    }
}
