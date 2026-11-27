using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Chế độ góc nhìn cho bản Web (chỉ chạy trên WebGL, không ảnh hưởng bản VR).
///  - Chuột tự do (mặc định): con trỏ hiện, giữ chuột phải để xoay góc nhìn.
///  - Góc nhìn thứ nhất: khóa chuột, di chuyển chuột là xoay góc nhìn, tâm ngắm ở giữa màn hình.
///  - Góc nhìn thứ ba: như góc nhìn thứ nhất nhưng camera ở sau lưng, hiện model người.
/// Phím V để đổi lần lượt: chuột tự do → thứ nhất → thứ ba. Scene Quản lý vòng đời tài sản và Login luôn dùng chuột tự do.
/// Khi đang khóa chuột: giữ Alt hoặc bấm Esc để hiện chuột; mở menu (F1), bản đồ (M), bảng (L, Tab) cũng tự hiện chuột.
/// Click vào khung cảnh để khóa chuột lại.
/// Bảng phím điều khiển ở viền phải màn hình (phím H để ẩn/hiện), tự đổi nội dung khi đang lái tàu.
/// </summary>
public static class UttWebView
{
    public enum Mode { FreeCursor = 0, FirstPerson = 1, ThirdPerson = 2 }

    const string PrefKey = "utt.web.viewmode";
    static readonly string[] FreeCursorOnlyScenes = { "QuanLyVongDoiTaiSan", "Login" };

    public static Mode Current { get; private set; }

    /// <summary>Frame mà cú click được dùng để khóa chuột (không tính là click tương tác).</summary>
    public static int LockClickFrame = -1;

    static int _allowsSceneHandle = int.MinValue;
    static bool _allows;

    /// <summary>Được gọi nhiều lần mỗi khung hình: nhớ kết quả theo scene, tránh tạo chuỗi tên scene liên tục.</summary>
    public static bool SceneAllowsFirstPerson
    {
        get
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.handle != _allowsSceneHandle)
            {
                _allowsSceneHandle = scene.handle;
                string name = scene.name;
                _allows = true;
                foreach (var s in FreeCursorOnlyScenes) if (name == s) { _allows = false; break; }
            }
            return _allows;
        }
    }

    public static bool FirstPersonActive =>
        UttWebPresentation.Active && Current == Mode.FirstPerson && SceneAllowsFirstPerson;

    public static bool ThirdPersonActive =>
        UttWebPresentation.Active && Current == Mode.ThirdPerson && SceneAllowsFirstPerson;

    /// <summary>Đang ở chế độ khóa chuột (góc nhìn thứ nhất hoặc thứ ba).</summary>
    public static bool MouseLookActive => FirstPersonActive || ThirdPersonActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!UttWebPresentation.Active) return;
        Current = (Mode)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2);
        var go = new GameObject("UttWebView");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<UttWebViewRunner>();
        go.AddComponent<UttProfilePanel>();
        go.AddComponent<UttLoadingScreen>();
        go.AddComponent<UttWebSkins>();
        go.AddComponent<UttMinimap>();
        go.AddComponent<UttChat>();
        go.AddComponent<UttPresenter>();
        go.AddComponent<UttClassPanel>();
        go.AddComponent<UttQuizLive>();
    }

    public static void SetMode(Mode mode)
    {
        Current = mode;
        PlayerPrefs.SetInt(PrefKey, (int)mode);
        PlayerPrefs.Save();
    }
}

[DefaultExecutionOrder(-500)]
public sealed class UttWebViewRunner : MonoBehaviour
{
    Canvas _canvas;
    Image _crosshair;
    TextMeshProUGUI _hint;
    TextMeshProUGUI _toast;
    float _toastUntil;
    bool _weLocked;

    // Bảng phím điều khiển ở viền phải màn hình
    const string KeyHintsPref = "utt.web.keyhints";
    GameObject _keysPanel;
    TextMeshProUGUI _keysText;
    GameObject _keysTab;
    bool _keysVisible = true;
    string _lastKeys;
    float _nextScan;
    FirstPersonController _player;
    bool _hasDashboard;
    bool _hasSeat;
    int _scannedScene = -1;
    int _lastKeyState = -1;

    void Awake() { BuildUi(); }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool typing = IsTyping();

        // Phím H: ẩn/hiện bảng phím điều khiển
        if (kb != null && !typing && kb.hKey.wasPressedThisFrame && UttWebView.SceneAllowsFirstPerson)
        {
            _keysVisible = !_keysVisible;
            PlayerPrefs.SetInt(KeyHintsPref, _keysVisible ? 1 : 0);
            PlayerPrefs.Save();
        }
        UpdateKeyHints();

        // Phím V: đổi góc nhìn
        if (kb != null && !typing && !SceneQuickMenu.IsOpen && kb.vKey.wasPressedThisFrame)
        {
            if (!UttWebView.SceneAllowsFirstPerson)
                ShowToast("Khu vực này chỉ dùng chế độ chuột tự do");
            else
            {
                var next = UttWebView.Current switch
                {
                    UttWebView.Mode.FreeCursor => UttWebView.Mode.FirstPerson,
                    UttWebView.Mode.FirstPerson => UttWebView.Mode.ThirdPerson,
                    _ => UttWebView.Mode.FreeCursor
                };
                UttWebView.SetMode(next);
                ShowToast(next switch
                {
                    UttWebView.Mode.FirstPerson => "Góc nhìn thứ nhất – click vào khung cảnh để khóa chuột",
                    UttWebView.Mode.ThirdPerson => "Góc nhìn thứ ba – click vào khung cảnh để khóa chuột",
                    _ => "Chế độ chuột tự do – giữ chuột phải để xoay góc nhìn"
                });
                if (next == UttWebView.Mode.FreeCursor) Unlock();
            }
        }

        if (!UttWebView.MouseLookActive)
        {
            // Chuột tự do: luôn hiện con trỏ (một số bảng cũ có thể đã khóa chuột).
            if (Cursor.lockState == CursorLockMode.Locked) Unlock();
            UpdateUi(false, false);
            return;
        }

        bool alt = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
        bool openedPanel = kb != null && !typing &&
                           (kb.lKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);

        // Gõ chat không mở khoá chuột (vẫn ở góc nhìn thứ nhất/thứ ba, chỉ khoá di chuyển và xoay).
        if (SceneQuickMenu.IsOpen || UttProfilePanel.IsOpen || UttMinimap.WorldMapOpen || UttClassPanel.IsOpen || UttPresenter.ZoomOpen || UttQuizLive.IsOpen || (typing && !UttChat.IsTyping) || alt || openedPanel)
        {
            if (Cursor.lockState == CursorLockMode.Locked) Unlock();
        }
        else if (Cursor.lockState != CursorLockMode.Locked)
        {
            bool clickWorld = mouse != null && mouse.leftButton.wasPressedThisFrame && !UttWebPresentation.PointerOverUi;
            if (clickWorld)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _weLocked = true;
                UttWebView.LockClickFrame = Time.frameCount;
            }
        }

        bool locked = Cursor.lockState == CursorLockMode.Locked;
        UpdateUi(locked, !locked && !SceneQuickMenu.IsOpen && !UttProfilePanel.IsOpen && !UttMinimap.WorldMapOpen);
#endif
    }

    void Unlock()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _weLocked = false;
    }

    void UpdateKeyHints()
    {
        if (_keysPanel == null) return;

        // Không hiện ở Login và Quản lý vòng đời tài sản.
        if (!UttWebView.SceneAllowsFirstPerson)
        {
            if (_keysPanel.activeSelf) _keysPanel.SetActive(false);
            if (_keysTab.activeSelf) _keysTab.SetActive(false);
            _lastKeyState = -1;
            return;
        }

        // Quét scene chỉ khi vừa vào scene mới; tìm nhân vật tối đa 1 lần/giây khi chưa có.
        int scene = SceneManager.GetActiveScene().handle;
        if (scene != _scannedScene)
        {
            _scannedScene = scene;
            _player = null;
            _nextScan = 0f;
            _hasDashboard = FindFirstObjectByType<ProjectLopHoc.UI.DashboardManager>() != null;
            _hasSeat = FindFirstObjectByType<TrainSeatInteractable>() != null;
        }
        if ((_player == null || !_player.isActiveAndEnabled) && Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + 1f;
            _player = FindLocalPlayer();
        }

        if (_keysPanel.activeSelf != _keysVisible) _keysPanel.SetActive(_keysVisible);
        if (_keysTab.activeSelf == _keysVisible) _keysTab.SetActive(!_keysVisible);
        if (!_keysVisible) return;

        // Chỉ dựng lại chữ khi trạng thái đổi (tránh tạo chuỗi mới mỗi khung hình → giật do dọn rác).
        bool driving = _player != null && _player.isDriving;
        int state = (driving ? 1 : 0) | (UttWebView.MouseLookActive ? 2 : 0) | ((int)UttWebView.Current << 2)
                    | (_hasDashboard ? 16 : 0) | (_hasSeat ? 32 : 0);
        if (state == _lastKeyState) return;
        _lastKeyState = state;
        string text = BuildKeyHints();
        if (text != _lastKeys)
        {
            _keysText.text = text;
            _lastKeys = text;
        }
    }

    static FirstPersonController FindLocalPlayer()
    {
        foreach (var p in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
        {
            if (p != null && p.Object != null && p.Object.IsValid && p.HasStateAuthority)
                return p;
        }
        return null;
    }

    string BuildKeyHints()
    {
        bool driving = _player != null && _player.isDriving;
        bool locked = UttWebView.MouseLookActive;
        var sb = new StringBuilder();
        sb.Append("<size=115%><b>Phím điều khiển</b></size>\n");

        if (driving)
        {
            Key(sb, "W", "Tăng tốc tàu");
            Key(sb, "S", "Giảm tốc");
            Key(sb, "Space", "Dừng tàu");
            Key(sb, "F", "Rời ghế lái");
            Key(sb, locked ? "Di chuột" : "Giữ chuột phải", "Nhìn quanh cabin");
        }
        else
        {
            Key(sb, "W A S D", "Di chuyển");
            Key(sb, "Space", "Nhảy");
            Key(sb, locked ? "Di chuột" : "Giữ chuột phải", "Xoay góc nhìn");
            Key(sb, "Chuột trái", "Tương tác");
            if (_hasSeat) Key(sb, "F", "Ngồi ghế lái (khi đứng gần)");
        }

        string mode = UttWebView.Current switch
        {
            UttWebView.Mode.FirstPerson => "thứ nhất",
            UttWebView.Mode.ThirdPerson => "thứ ba",
            _ => "chuột tự do"
        };
        Key(sb, "V", "Đổi góc nhìn (đang: " + mode + ")");
        if (locked) Key(sb, "Alt / Esc", "Hiện chuột");
        Key(sb, "E", "Menu: cá nhân, khu vực, cài đặt");
        Key(sb, "M", "Bản đồ");
        Key(sb, "F1", "Menu chuyển khu vực (1–5)");
        Key(sb, "T", "Chat");
        if (_hasDashboard) Key(sb, "L / Tab", "Bảng bài giảng");
        Key(sb, "H", "Ẩn bảng này");
        return sb.ToString().TrimEnd('\n');
    }

    static void Key(StringBuilder sb, string key, string action)
    {
        sb.Append("<color=#FFD54F><b>").Append(key).Append("</b></color><pos=175>").Append(action).Append('\n');
    }

    static bool IsTyping()
    {
        var es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject == null) return false;
        var tmp = es.currentSelectedGameObject.GetComponent<TMP_InputField>();
        if (tmp != null && tmp.isFocused) return true;
        var legacy = es.currentSelectedGameObject.GetComponent<InputField>();
        return legacy != null && legacy.isFocused;
    }

    void ShowToast(string text)
    {
        if (_toast == null) return;
        _toast.text = text;
        _toastUntil = Time.unscaledTime + 2.5f;
    }

    void UpdateUi(bool showCrosshair, bool showHint)
    {
        if (_canvas == null) return;
        _crosshair.enabled = showCrosshair;
        _hint.transform.parent.gameObject.SetActive(showHint);
        _toast.transform.parent.gameObject.SetActive(Time.unscaledTime < _toastUntil);
    }

    void BuildUi()
    {
        TMP_FontAsset font = UttProjectFonts.Vietnamese;
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto Vietnamese SDF");

        var canvasGo = new GameObject("UttWebViewCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        // Không thêm GraphicRaycaster: lớp này không chặn click.

        var ch = new GameObject("Crosshair", typeof(RectTransform));
        ch.transform.SetParent(canvasGo.transform, false);
        _crosshair = ch.AddComponent<Image>();
        _crosshair.color = new Color(1f, 1f, 1f, 0.9f);
        _crosshair.raycastTarget = false;
        var chRt = (RectTransform)ch.transform;
        chRt.anchorMin = chRt.anchorMax = new Vector2(0.5f, 0.5f);
        chRt.sizeDelta = new Vector2(8f, 8f);
        var outline = ch.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        _hint = MakeLabel(canvasGo.transform, font, "Hint",
            "Click vào khung cảnh để điều khiển  •  Alt: hiện chuột  •  V: đổi góc nhìn",
            new Vector2(0.5f, 0f), new Vector2(0f, 90f), 24);
        _toast = MakeLabel(canvasGo.transform, font, "Toast", "", new Vector2(0.5f, 1f), new Vector2(0f, -120f), 26);
        BuildKeyHintsUi(canvasGo.transform, font);
        UpdateUi(false, false);
    }

    void BuildKeyHintsUi(Transform parent, TMP_FontAsset font)
    {
        _keysVisible = PlayerPrefs.GetInt(KeyHintsPref, 1) == 1;

        // Bảng đầy đủ: sát viền phải, giữa chiều cao màn hình.
        var panel = new GameObject("KeyHints", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var bg = panel.AddComponent<Image>();
        bg.sprite = UttGlass.Round18;
        bg.type = Image.Type.Sliced;
        bg.color = UttGlass.FillLight;
        bg.raycastTarget = false;
        UttGlass.Layer(panel.transform, "UttRing", UttGlass.Ring18, UttGlass.Border);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-16f, 0f);
        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        var fit = panel.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        _keysText = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) _keysText.font = font;
        _keysText.fontSize = 19;
        _keysText.lineSpacing = 12f;
        _keysText.color = Color.white;
        _keysText.alignment = TextAlignmentOptions.TopLeft;
        _keysText.textWrappingMode = TextWrappingModes.Normal;
        _keysText.raycastTarget = false;
        var element = textGo.AddComponent<LayoutElement>();
        element.preferredWidth = 440f;
        _keysPanel = panel;

        // Khi ẩn: chỉ còn một nhãn nhỏ ở viền phải nhắc phím H.
        var tabLabel = MakeLabel(parent, font, "KeyHintsTab", "<color=#FFD54F><b>H</b></color>  Phím điều khiển",
            new Vector2(1f, 0.5f), Vector2.zero, 18);
        _keysTab = tabLabel.transform.parent.gameObject;
        var tabRt = (RectTransform)_keysTab.transform;
        tabRt.pivot = new Vector2(1f, 0.5f);
        tabRt.anchoredPosition = new Vector2(-16f, 0f);

        _keysPanel.SetActive(false);
        _keysTab.SetActive(false);
    }

    static TextMeshProUGUI MakeLabel(Transform parent, TMP_FontAsset font, string name, string text, Vector2 anchor, Vector2 pos, float size)
    {
        var bg = new GameObject(name, typeof(RectTransform));
        bg.transform.SetParent(parent, false);
        var img = bg.AddComponent<Image>();
        img.sprite = UttGlass.Pill;
        img.type = Image.Type.Sliced;
        img.color = UttGlass.Fill;
        img.raycastTarget = false;
        UttGlass.Layer(bg.transform, "UttRing", UttGlass.PillRing, UttGlass.SoftRing);
        var rt = (RectTransform)bg.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        var fitter = bg.AddComponent<HorizontalLayoutGroup>();
        fitter.padding = new RectOffset(22, 22, 10, 10);
        fitter.childControlWidth = true; fitter.childControlHeight = true;
        var csf = bg.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var tGo = new GameObject("Text", typeof(RectTransform));
        tGo.transform.SetParent(bg.transform, false);
        var t = tGo.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }
}
