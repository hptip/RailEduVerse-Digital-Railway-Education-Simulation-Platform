using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using ProjectLopHoc.Features.Authentication.Services;

public class DataBridge : MonoBehaviour
{
    public static DataBridge Instance;

    [Header("Offline VR Demo")]
    [Tooltip("Editor/VR: bật = không gọi backend. Bản Web luôn online, trừ khi địa chỉ trang có ?offline=1.")]
    [SerializeField] private bool offlineDemo = true;

    /// <summary>Đang chạy offline thật sự (đã tính cả bản Web).</summary>
    public bool Offline => UttSession.ResolveOffline(offlineDemo);

    #if UNITY_EDITOR
    private string baseUrl = "http://127.0.0.1:5000/api";
    #else
    private string baseUrl = "/api";
    #endif

    // Đọc từ UttSession: bản Web mỗi tab một phiên riêng.
    public int CurrentUserId => UttSession.UserId;
    public string CurrentUsername => UttSession.Username;
    public string CurrentFullName => UttSession.FullName;
    public string CurrentRole => UttSession.Role;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [Serializable]
    private class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    public class UserData
    {
        public int user_id;
        public string username;
        public string full_name;
        public string role;
        public string token;
    }

    [Serializable]
    private class LoginResponse
    {
        public string status;
        public string message;
        public UserData data;
    }

    [Serializable]
    private class QuizResultPayload
    {
        public int user_id;
        public string quiz_title;
        public int score;
        public int total_questions;
    }

    // Đăng nhập trả về kết quả async
    public async Task<(bool success, string message)> LoginAsync(string username, string password)
    {
        if (Offline)
        {
            // This path never constructs or sends a UnityWebRequest.
            await Task.Yield();
            var user = OfflineAuthenticationService.FindUser(username);
            if (user == null) return (false, "Không tìm thấy tài khoản.");
            if (!OfflineAuthenticationService.VerifyPassword(user, password))
                return (false, "Mật khẩu không chính xác.");

            UttSession.Set(user.id, user.username, user.full_name, user.role, "", true);
            return (true, $"Xin chào {CurrentFullName}!");
        }

        LoginRequest reqData = new LoginRequest { username = username, password = password };
        string jsonBody = JsonUtility.ToJson(reqData);

        using (UnityWebRequest req = new UnityWebRequest($"{baseUrl}/auth/login", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    LoginResponse res = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
                    if (res == null || res.data == null || string.IsNullOrEmpty(res.data.token))
                        return (false, "Máy chủ trả về dữ liệu đăng nhập không hợp lệ.");
                    UttSession.Set(res.data.user_id, res.data.username, res.data.full_name, res.data.role, res.data.token, false);

                    return (true, $"Xin chào {CurrentFullName}!");
                }
                catch (Exception e)
                {
                    return (false, "Lỗi phân tích dữ liệu server: " + e.Message);
                }
            }
            else if (req.responseCode == 401)
            {
                return (false, "Tài khoản hoặc mật khẩu không chính xác!");
            }
            else
            {
                Debug.LogWarning($"[DataBridge] Đăng nhập lỗi {req.responseCode}: {req.error}");
                return (false, "Không kết nối được máy chủ. Kiểm tra backend (Backend/start_backend.bat) hoặc mở trang với ?offline=1.");
            }
        }
    }

    /// <summary>Gắn token của phiên hiện tại vào request.</summary>
    private static void AddAuth(UnityWebRequest req)
    {
        if (UttSession.HasToken) req.SetRequestHeader("Authorization", "Bearer " + UttSession.Token);
    }

    /// <summary>Máy chủ báo token sai hoặc hết hạn: xoá phiên và quay về màn hình đăng nhập.</summary>
    private static void HandleUnauthorized(UnityWebRequest req)
    {
        if (req.responseCode != 401) return;
        Debug.LogWarning("[DataBridge] Phiên đăng nhập hết hạn, quay về màn hình đăng nhập.");
        UttProfilePanel.ForceLogout();
    }

    /// <summary>
    /// Kiểm tra phiên đã lưu (sau khi bấm F5) còn dùng được không.
    /// Trả về false khi token sai/hết hạn; true khi hợp lệ, offline, hoặc máy chủ tạm thời không trả lời.
    /// </summary>
    public async Task<bool> ValidateSessionAsync()
    {
        if (!UttSession.IsLoggedIn) return false;
        if (UttSession.Offline || !UttSession.HasToken) return UttSession.Offline;

        using (UnityWebRequest req = UnityWebRequest.Get($"{baseUrl}/me"))
        {
            AddAuth(req);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            if (req.result == UnityWebRequest.Result.Success) return true;
            return req.responseCode != 401 && req.responseCode != 403;
        }
    }

    /// <summary>Đăng xuất: huỷ token trên máy chủ (không chờ) và xoá phiên ở tab này.</summary>
    public void Logout()
    {
        if (UttSession.HasToken && !UttSession.Offline)
        {
            var req = new UnityWebRequest($"{baseUrl}/auth/logout", "POST");
            req.downloadHandler = new DownloadHandlerBuffer();
            AddAuth(req);
            var op = req.SendWebRequest();
            op.completed += _ => req.Dispose();
        }
        UttSession.Clear();
    }

    public void SaveQuizResult(string quizTitle, int score, int totalQuestions)
    {
        if (Offline || UttSession.Offline) return;
        if (CurrentUserId <= 0)
        {
            Debug.LogError("[DataBridge] Chưa đăng nhập. Không thể lưu điểm!");
            return;
        }

        QuizResultPayload payload = new QuizResultPayload
        {
            user_id = CurrentUserId,
            quiz_title = quizTitle,
            score = score,
            total_questions = totalQuestions
        };

        StartCoroutine(PostSimpleRoutine($"{baseUrl}/quiz/submit", JsonUtility.ToJson(payload)));
    }

    private System.Collections.IEnumerator PostSimpleRoutine(string url, string jsonBody)
    {
        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            AddAuth(req);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[DataBridge] {url} lỗi {req.responseCode}: {req.error}");
                HandleUnauthorized(req);
            }
        }
    }

    [Serializable]
    public class StudentScoreRecord
    {
        public string full_name;
        public string student_id;
        public string score;
    }

    [Serializable]
    private class StudentListResponse
    {
        public string status;
        public StudentScoreRecord[] data;
    }

    /// <summary>Lỗi của lần gọi GetStudentRecordsAsync gần nhất (rỗng khi lấy được từ máy chủ hoặc đang offline).</summary>
    public string LastStudentRecordsError { get; private set; } = "";

    // Hàm lấy danh sách sinh viên từ máy chủ API
    public async Task<System.Collections.Generic.List<StudentScoreRecord>> GetStudentRecordsAsync()
    {
        var list = new System.Collections.Generic.List<StudentScoreRecord>();
        LastStudentRecordsError = "";

        if (Offline || UttSession.Offline)
        {
            AddFallbackStudentRecords(list);
            return list;
        }

        using (UnityWebRequest req = UnityWebRequest.Get($"{baseUrl}/students/scores"))
        {
            AddAuth(req);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    StudentListResponse res = JsonUtility.FromJson<StudentListResponse>(req.downloadHandler.text);
                    if (res != null && res.data != null)
                    {
                        list.AddRange(res.data);
                        return list;
                    }
                    LastStudentRecordsError = "Máy chủ trả về dữ liệu không đọc được.";
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[DataBridge] Lỗi đọc JSON: " + e.Message);
                }
            }
            else
            {
                // Không đăng xuất ở đây: xem bảng điểm lỗi không được làm mất phiên học/trình chiếu đang chạy.
                Debug.LogWarning($"[DataBridge] /students/scores lỗi {req.responseCode}.");
                LastStudentRecordsError = req.responseCode == 401
                    ? "Phiên đăng nhập của tab này không còn hợp lệ trên máy chủ (401). Hãy đăng nhập lại bằng tài khoản giảng viên khi rảnh."
                    : req.responseCode == 403
                        ? "Tài khoản này không có quyền xem điểm (403)."
                        : req.responseCode == 0
                            ? "Không kết nối được máy chủ. Kiểm tra Backend/server.py đang chạy."
                            : $"Máy chủ báo lỗi {req.responseCode}.";
                return list;
            }
        }

        // Dữ liệu dự phòng tự động khi máy chủ backend chưa bật endpoint
        AddFallbackStudentRecords(list);
        return list;
    }

    private static void AddFallbackStudentRecords(System.Collections.Generic.List<StudentScoreRecord> list)
    {
        list.Add(new StudentScoreRecord { full_name = "Nguyễn Văn An", student_id = "74DCTT21001", score = "8.5" });
        list.Add(new StudentScoreRecord { full_name = "Trần Thị Bình", student_id = "74DCTT21002", score = "9.0" });
        list.Add(new StudentScoreRecord { full_name = "Lê Hoàng Cường", student_id = "74DCTT21003", score = "7.0" });
        list.Add(new StudentScoreRecord { full_name = "Phạm Minh Đức", student_id = "74DCTT21004", score = "10.0" });
    }
}
