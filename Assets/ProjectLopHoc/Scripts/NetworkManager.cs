using Fusion;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour
{
    // Cấu trúc Singleton giúp gọi lệnh từ mọi nơi
    public static NetworkManager Instance { get; private set; }
    
    private NetworkRunner _runner;
    [Tooltip("Editor/VR: bật = Photon chạy một người. Bản Web luôn online (Shared), trừ khi địa chỉ trang có ?offline=1.")]
    [SerializeField] private bool offlineDemo = true;

    private void Awake()
    {
        // Giữ cho GameObject này không bị xóa khi chuyển từ Login sang Campus
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

    public async Task<bool> StartCampusSession(string username, bool isLecturer)
    {
        // Khởi tạo Runner nếu chưa có
        if (_runner == null)
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
        }

        // Bắt buộc phải có SceneManager để Fusion tự động load map
        var sceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null) sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        // Tên và vai trò đọc từ UttSession khi sinh nhân vật (bản Web: riêng từng tab).
        bool offline = UttSession.ResolveOffline(offlineDemo);

        // Đăng nhập xong vào thẳng Lớp học (giống bản VR, xem SceneQuickMenu.DefaultScene).
        int campusIndex = SceneUtility.GetBuildIndexByScenePath("Assets/ProjectLopHoc/Scenes/Classroom.unity");
        if (campusIndex < 0)
        {
            Debug.LogError("[NetworkManager] Classroom không có trong Build Settings.");
            return false;
        }

        // Thiết lập thông số phòng
        var args = new StartGameArgs()
        {
            GameMode = offline ? GameMode.Single : GameMode.Shared,
            SessionName = offline ? null : "UTTCampus",
            Scene = SceneRef.FromIndex(campusIndex),
            SceneManager = sceneManager
        };

        Debug.Log(offline ? "Đang khởi chạy Lớp học ở chế độ offline." : "Đang kết nối Photon Shared...");
        UttLoadingScreen.Begin("Classroom", true); // màn hình chờ sau khi đăng nhập
        var result = await _runner.StartGame(args);
        if (!result.Ok)
        {
            UttLoadingScreen.Cancel();
            Debug.LogError("[NetworkManager] Không thể khởi chạy Lớp học: " + result.ShutdownReason);
            return false;
        }
        return true;
    }
}
