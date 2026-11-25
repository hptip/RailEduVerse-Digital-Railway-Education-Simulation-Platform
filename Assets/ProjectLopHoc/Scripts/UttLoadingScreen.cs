using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Màn hình chờ khi chuyển scene trên bản Web (HTML/CSS, xem Plugins/WebGL/UttLoading.jslib).
/// Giai đoạn 1: % tải scene thật (0–70%). Giai đoạn 2: scene tự khởi tạo, thanh bò chậm tới 95%.
/// Chỉ khi scene sẵn sàng (nhân vật đã xuất hiện; Quản lý vòng đời: dữ liệu BIM đã nạp) thanh mới đầy 100% và ẩn đi.
/// Ảnh nền, tiêu đề, mẹo: StreamingAssets/Loading/loading.json.
/// </summary>
public sealed class UttLoadingScreen : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void UttLoadingShow(string key, string title, string baseUrl);
    [DllImport("__Internal")] static extern void UttLoadingProgress(float progress);
    [DllImport("__Internal")] static extern void UttLoadingPhase2();
    [DllImport("__Internal")] static extern void UttLoadingReady();
    [DllImport("__Internal")] static extern void UttWebFullscreenInit();
#else
    static void UttWebFullscreenInit() { }
    static void UttLoadingShow(string key, string title, string baseUrl) { }
    static void UttLoadingProgress(float progress) { }
    static void UttLoadingPhase2() { }
    static void UttLoadingReady() { }
#endif

    /// <summary>
    /// Các scene có màn hình chờ. Hiện chỉ bật cho Quản lý vòng đời tài sản.
    /// Muốn thêm: ghi tên scene vào đây, ví dụ "Classroom", "UTTCampus", "Showroom", "YenTuHaLongXanh", "Login".
    /// </summary>
    static readonly string[] EnabledScenes = { "QuanLyVongDoiTaiSan" };

    const float MaxWait = 60f;      // dù thế nào cũng tự ẩn sau 60 giây
    const float ReadyWait = 45f;    // chờ scene sẵn sàng tối đa 45 giây

    static UttLoadingScreen _instance;
    static bool _active, _phase2;
    static string _target;
    static AsyncOperation _op;
    static float _startedAt, _lastProgress;

    /// <summary>
    /// Gọi ngay trước khi bắt đầu tải scene (tên scene hoặc đường dẫn .unity).
    /// force = true: luôn hiện, kể cả scene không nằm trong EnabledScenes (dùng cho lượt vào sau khi đăng nhập).
    /// </summary>
    public static void Begin(string scene, bool force = false)
    {
        if (!UttWebPresentation.Active || string.IsNullOrEmpty(scene)) return;
        string name = Path.GetFileNameWithoutExtension(scene);
        if (!force && System.Array.IndexOf(EnabledScenes, name) < 0) return;
        _target = name;
        _active = true;
        _phase2 = false;
        _op = null;
        _lastProgress = 0f;
        _startedAt = Time.realtimeSinceStartup;
        if (_instance != null) _instance.StopAllCoroutines();
        Debug.Log($"[UttLoading] Bắt đầu màn hình chờ: {_target}");
        UttLoadingShow(_target, Title(_target), Application.streamingAssetsPath);
    }

    /// <summary>Theo dõi % tải thật khi scene được tải bằng SceneManager.LoadSceneAsync.</summary>
    public static AsyncOperation Track(AsyncOperation op)
    {
        if (_active && !_phase2) _op = op;
        return op;
    }

    /// <summary>Ẩn ngay (ví dụ tải thất bại).</summary>
    public static void Cancel()
    {
        if (!_active) return;
        Finish();
    }

    static void Finish()
    {
        _active = false;
        _phase2 = false;
        _op = null;
        UttLoadingReady();
    }

    void Awake()
    {
        _instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Nút toàn màn hình của Unity phóng to vùng chứa game để màn hình chờ vẫn hiện khi toàn màn hình
        UttWebFullscreenInit();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        if (!_active) return;
        if (Time.realtimeSinceStartup - _startedAt > MaxWait) { Finish(); return; }
        if (!_phase2 && _op != null)
        {
            float p = Mathf.Clamp01(_op.progress / 0.9f);
            if (p - _lastProgress >= 0.02f) { _lastProgress = p; UttLoadingProgress(p); }
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_active || _phase2) return;
        if (!string.Equals(scene.name, _target, System.StringComparison.OrdinalIgnoreCase)) return; // bỏ qua Zone tải thêm
        _phase2 = true;
        _op = null;
        Debug.Log($"[UttLoading] Đã tải scene {scene.name}, chờ nhân vật và camera");
        UttLoadingProgress(1f);
        UttLoadingPhase2();
        StartCoroutine(WaitUntilReady(scene.name));
    }

    IEnumerator WaitUntilReady(string scene)
    {
        float started = Time.realtimeSinceStartup;
        float deadline = started + ReadyWait;
        var wait = new WaitForSecondsRealtime(0.2f);
        while (Time.realtimeSinceStartup < deadline && !SceneReady(scene))
            yield return wait;
        if (Time.realtimeSinceStartup >= deadline)
            Debug.LogWarning($"[UttLoading] Hết {ReadyWait:0}s chờ {scene}, tự ẩn màn hình chờ");
        // Hiện tối thiểu 0,6 giây để không chớp tắt, rồi cho scene vẽ thêm vài khung hình
        while (Time.realtimeSinceStartup - started < 0.6f) yield return null;
        for (int i = 0; i < 3; i++) yield return null;
        Debug.Log($"[UttLoading] {scene} sẵn sàng sau {Time.realtimeSinceStartup - started:0.0}s");
        if (_active) Finish();
    }

    static bool SceneReady(string scene)
    {
        if (scene == "QuanLyVongDoiTaiSan")
        {
            var bim = FindFirstObjectByType<RailwayBimController>();
            return bim == null || bim.Ready;
        }
        // Scene có sinh nhân vật: chờ nhân vật của mình xuất hiện VÀ camera đã về cạnh nhân vật
        // (tránh lộ cảnh camera mặc định của scene, ví dụ nhìn lên trời, trước khi camera nhân vật vào chỗ)
        if (FindFirstObjectByType<PlayerSpawner>() == null) return true;
        foreach (var p in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
        {
            if (p == null || p.Object == null || !p.Object.IsValid || !p.HasStateAuthority) continue;
            var cam = Camera.main;
            return cam != null && cam.isActiveAndEnabled &&
                   Vector3.Distance(cam.transform.position, p.transform.position) < 8f;
        }
        return false;
    }

    static string Title(string scene) => scene switch
    {
        "UTTCampus" => "Khuôn viên UTT",
        "Classroom" => "Lớp học",
        "Showroom" => "Phòng trưng bày",
        "YenTuHaLongXanh" => "Yên Tử – Hạ Long",
        "QuanLyVongDoiTaiSan" => "Quản lý vòng đời tài sản",
        "Login" => "Đăng nhập",
        _ => scene
    };
}
