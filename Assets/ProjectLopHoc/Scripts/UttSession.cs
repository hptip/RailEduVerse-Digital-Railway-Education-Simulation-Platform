using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Phiên đăng nhập của người chơi hiện tại.
///
/// Bản Web: mỗi tab trình duyệt là một phiên riêng. Thông tin giữ trong bộ nhớ của tab đó
/// và sao lưu vào sessionStorage (mỗi tab một bản) để bấm F5 không bị đăng xuất.
/// Không dùng PlayerPrefs trên Web vì mọi tab cùng trang dùng chung một PlayerPrefs (IndexedDB),
/// tab đăng nhập sau sẽ ghi đè tên và vai trò của tab trước.
///
/// Bản VR / Editor: vẫn ghi thêm các khóa PlayerPrefs cũ (IsLecturer, PlayerName, Username, UserId, UserRole)
/// để mở thẳng một scene trong Editor mà không qua Login vẫn chạy như trước.
/// </summary>
public static class UttSession
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern string UttSessionGet(string key);
    [DllImport("__Internal")] static extern void UttSessionSet(string key, string value);
    [DllImport("__Internal")] static extern void UttSessionRemove(string key);
    static readonly bool UseBrowserStorage = true;
#else
    static string UttSessionGet(string key) => "";
    static void UttSessionSet(string key, string value) { }
    static void UttSessionRemove(string key) { }
    static readonly bool UseBrowserStorage = false;
#endif

    const string StoreKey = "utt.session";

    [Serializable]
    class Data
    {
        public int userId = -1;
        public string username = "";
        public string fullName = "";
        public string role = "";
        public string token = "";
        public bool offline;
    }

    static Data _data;
    static bool _loaded;

    /// <summary>Gọi khi phiên bị xoá (đăng xuất hoặc hết hạn).</summary>
    public static event Action Cleared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _data = null;
        _loaded = false;
        Cleared = null;
    }

    static Data Current
    {
        get
        {
            if (!_loaded)
            {
                _loaded = true;
                _data = UseBrowserStorage ? LoadFromBrowser() : LoadFromPlayerPrefs();
            }
            return _data;
        }
    }

    public static bool IsLoggedIn => Current != null && (Current.userId > 0 || !string.IsNullOrEmpty(Current.fullName));
    public static int UserId => Current?.userId ?? -1;
    public static string Username => Current?.username ?? "";
    public static string FullName => Current?.fullName ?? "";
    public static string Role => Current?.role ?? "";
    public static string Token => Current?.token ?? "";
    public static bool HasToken => !string.IsNullOrEmpty(Token);
    public static bool IsLecturer => Role == "teacher";

    /// <summary>Tên hiển thị trên đầu nhân vật, trong chat, menu.</summary>
    public static string DisplayName => string.IsNullOrWhiteSpace(FullName) ? "Sinh viên" : FullName;

    /// <summary>
    /// Chế độ offline (không gọi backend, Photon chạy một người).
    /// Bản Web: mặc định online; thêm ?offline=1 vào địa chỉ trang để chạy offline.
    /// Editor / VR: theo ô offlineDemo trong Inspector của DataBridge / NetworkManager.
    /// </summary>
    public static bool ResolveOffline(bool inspectorValue)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string url = Application.absoluteURL ?? "";
        int q = url.IndexOf('?');
        if (q < 0) return false;
        string query = url.Substring(q + 1).ToLowerInvariant();
        return query.Contains("offline=1") || query.Contains("offline=true");
#else
        return inspectorValue;
#endif
    }

    /// <summary>Chế độ offline của phiên hiện tại (ghi lúc đăng nhập).</summary>
    public static bool Offline => Current != null && Current.offline;

    public static void Set(int userId, string username, string fullName, string role, string token, bool offline)
    {
        _loaded = true;
        _data = new Data
        {
            userId = userId,
            username = username ?? "",
            fullName = fullName ?? "",
            role = role ?? "",
            token = token ?? "",
            offline = offline
        };
        Save();
    }

    public static void Clear()
    {
        _loaded = true;
        _data = null;
        if (UseBrowserStorage) UttSessionRemove(StoreKey);
        try { Cleared?.Invoke(); }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    static void Save()
    {
        if (_data == null) return;
        if (UseBrowserStorage)
        {
            UttSessionSet(StoreKey, JsonUtility.ToJson(_data));
            return;
        }

        // VR / Editor: giữ các khóa cũ cho code và scene chạy thẳng không qua Login.
        PlayerPrefs.SetInt("IsLecturer", IsLecturer ? 1 : 0);
        PlayerPrefs.SetString("PlayerName", DisplayName);
        PlayerPrefs.SetString("Username", _data.username);
        PlayerPrefs.SetInt("UserId", _data.userId);
        PlayerPrefs.SetString("UserRole", _data.role);
        PlayerPrefs.SetInt("OfflineDemo", _data.offline ? 1 : 0);
        PlayerPrefs.Save();
    }

    static Data LoadFromBrowser()
    {
        try
        {
            string json = UttSessionGet(StoreKey);
            if (string.IsNullOrEmpty(json)) return null;
            var d = JsonUtility.FromJson<Data>(json);
            return d != null && (d.userId > 0 || !string.IsNullOrEmpty(d.fullName)) ? d : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[UttSession] Không đọc được phiên đã lưu: " + ex.Message);
            return null;
        }
    }

    static Data LoadFromPlayerPrefs()
    {
        if (!PlayerPrefs.HasKey("PlayerName") && !PlayerPrefs.HasKey("IsLecturer")) return null;
        string role = PlayerPrefs.GetString("UserRole", "");
        if (string.IsNullOrEmpty(role)) role = PlayerPrefs.GetInt("IsLecturer", 0) == 1 ? "teacher" : "student";
        return new Data
        {
            userId = PlayerPrefs.GetInt("UserId", -1),
            username = PlayerPrefs.GetString("Username", ""),
            fullName = PlayerPrefs.GetString("PlayerName", ""),
            role = role,
            token = "",
            offline = PlayerPrefs.GetInt("OfflineDemo", 1) == 1
        };
    }
}
