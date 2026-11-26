using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Menu E của bản Web theo bộ UI Liquid Glass đã chốt:
/// thanh bên (Tiếp tục, Chuyển khu vực, Cá nhân, Cài đặt, Phím tắt, Đăng xuất) + nội dung bên phải.
/// E mở/đóng, Esc đóng, phím 1–5 chọn khu vực khi đang ở mục Chuyển khu vực.
/// Kính "giả" (ảnh bo góc 9-slice tạo bằng code + viền sáng), không làm nhòe màn hình nên nhẹ trên WebGL.
/// </summary>
[DefaultExecutionOrder(-490)]
public sealed class UttProfilePanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    static UttProfilePanel _inst;
    bool _forAreas; // mở bằng M/F1: cho phép cả ở scene không có nhân vật (Quản lý vòng đời)

    /// <summary>M / F1: mở/đóng menu ở mục Chuyển khu vực.</summary>
    public static void ToggleAreas()
    {
        if (_inst == null) return;
        if (IsOpen && _inst._tab == Tab.Areas) { _inst.Close(); return; }
        _inst._tab = Tab.Areas;
        _inst._forAreas = true;
        _inst.Open();
    }

    enum Tab { Areas, Profile, Settings, Keys }

    // ----- Bảng màu đã chốt -----
    // Phương án B: màu thương hiệu UTT (chàm + cam)
    static readonly Color Navy = new Color32(0x26, 0x26, 0x5F, 0xFF);
    static readonly Color GlassFill = new Color(0.149f, 0.149f, 0.376f, 0.88f);
    static readonly Color GlassTint = new Color(1f, 1f, 1f, 0.05f);
    static readonly Color GlassBorder = new Color(1f, 1f, 1f, 0.22f);
    static readonly Color GlassShine = new Color(1f, 1f, 1f, 0.36f);
    static readonly Color CardFill = new Color(1f, 1f, 1f, 0.09f);
    static readonly Color CardRing = new Color(1f, 1f, 1f, 0.22f);
    static readonly Color Blue = new Color32(0xF7, 0x93, 0x1E, 0xFF); // màu nhấn cam UTT
    static readonly Color Amber = new Color32(0xFF, 0xC9, 0x4D, 0xFF);
    static readonly Color AmberText = new Color32(0xFF, 0xD7, 0x7A, 0xFF);
    static readonly Color Green = new Color32(0x3D, 0xDC, 0x97, 0xFF);
    static readonly Color Orange = new Color32(0xFF, 0xB3, 0x94, 0xFF);
    static readonly Color Muted = new Color(1f, 1f, 1f, 0.72f);

    const string MousePref = "utt.web.mouse";
    const string QualityPref = "utt.web.quality";   // 0 = Thấp, 1 = Vừa, 2 = Cao (mặc định, giữ nguyên cấu hình gốc)
    const string FpsPref = "utt.web.fps";
    const float PW = 1280f, PH = 760f, NavX = 36f, NavW = 260f, CX = 340f, CW = 904f;

    static Sprite _r28, _r24, _r16, _r14, _pill, _ring28, _ring24, _ring16, _ring2px, _pillRing, _shadow;

    TMP_FontAsset _font;
    Canvas _canvas;
    CanvasGroup _group;
    RectTransform _panel;
    Tab _tab = Tab.Profile;
    readonly RectTransform[] _pages = new RectTransform[4];
    readonly Image[] _navBg = new Image[4];
    readonly TextMeshProUGUI[] _navText = new TextMeshProUGUI[4];
    TextMeshProUGUI _title, _subtitle;

    // nav header
    TextMeshProUGUI _navInitials, _navName, _navSub;
    // profile
    TextMeshProUGUI _pInitials, _pName, _pRole, _pClass, _rAccount, _rRole, _rClass, _rArea, _rPlayers, _rOnline;
    Image _pRoleBg;
    // areas
    readonly GameObject[] _here = new GameObject[5];
    readonly Image[] _areaRing = new Image[5];
    // settings
    readonly Image[] _modeBg = new Image[3];
    readonly TextMeshProUGUI[] _modeText = new TextMeshProUGUI[3];
    Slider _mouse;
    TextMeshProUGUI _mouseValue;
    readonly Image[] _qBg = new Image[3];
    readonly TextMeshProUGUI[] _qText = new TextMeshProUGUI[3];
    TextMeshProUGUI _qNote, _fpsBtnText;
    Image _fpsBtnBg;

    // Bộ đếm FPS
    Canvas _fpsCanvas;
    TextMeshProUGUI _fpsText;
    float _fpsTime;
    int _fpsFrames;

    // Cấu hình gốc của project (đọc một lần trước khi đổi)
    static bool _defaultsCaptured;
    static float _defScale, _defShadow, _defLod;

    float _anim, _nextRefresh, _sessionStart = -1f;
    bool _loggingOut;
    FirstPersonController _player, _sensApplied;
    float _nextFind;

    static readonly (string name, string sub, Color top, Color bottom)[] Areas =
    {
        ("Khuôn viên UTT", "Sân trường, các tòa nhà", new Color32(0x86, 0xB3, 0xE6, 0xFF), new Color32(0x6F, 0x8F, 0x72, 0xFF)),
        ("Lớp học", "Bài giảng, slide, trắc nghiệm", new Color32(0xD7, 0xCB, 0xB4, 0xFF), new Color32(0x8C, 0x7A, 0x62, 0xFF)),
        ("Phòng trưng bày", "Tàu, sa bàn tuyến đường", new Color32(0x5A, 0x6E, 0x8C, 0xFF), new Color32(0x25, 0x2F, 0x45, 0xFF)),
        ("Yên Tử – Hạ Long", "Lái tàu trên tuyến", new Color32(0x9C, 0xC3, 0xE8, 0xFF), new Color32(0x3F, 0x6B, 0x4E, 0xFF)),
        ("Quản lý vòng đời tài sản", "", Color.clear, Color.clear),
    };
    static readonly string[] AreaScenes = { "UTTCampus", "Classroom", "Showroom", "YenTuHaLongXanh", "QuanLyVongDoiTaiSan" };

    // =========================================================
    void Awake()
    {
        _inst = this;
        _font = UttProjectFonts.Vietnamese;
        if (_font == null) _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto Vietnamese SDF");
        BuildSprites();
        BuildUi();
        BuildFpsCounter();
        SetVisible(false);
        ApplyQuality(PlayerPrefs.GetInt(QualityPref, 2));
        _fpsCanvas.enabled = PlayerPrefs.GetInt(FpsPref, 0) == 1;
    }

    void OnDisable() => IsOpen = false;

    void Update()
    {
        if (_sessionStart < 0f && UttWebView.SceneAllowsFirstPerson) _sessionStart = Time.realtimeSinceStartup;

        // Áp độ nhạy chuột đã lưu, chỉ một lần cho mỗi nhân vật mới (tìm nhân vật tối đa 1 lần/giây)
        var p = FindLocalPlayerCached();
        if (p != null && p != _sensApplied)
        {
            _sensApplied = p;
            if (PlayerPrefs.HasKey(MousePref)) p.mouseSensitivity = PlayerPrefs.GetFloat(MousePref);
        }

        bool allowed = UttWebView.SceneAllowsFirstPerson;
        if (!allowed && IsOpen && !_forAreas) Close();

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (allowed || IsOpen) && !IsTyping() && !SceneQuickMenu.IsOpen)
        {
            if (kb.eKey.wasPressedThisFrame) { if (IsOpen) Close(); else Open(); }
            else if (IsOpen && kb.escapeKey.wasPressedThisFrame) Close();
            else if (IsOpen && _tab == Tab.Areas)
            {
                if (kb.digit1Key.wasPressedThisFrame) GoTo(0);
                else if (kb.digit2Key.wasPressedThisFrame) GoTo(1);
                else if (kb.digit3Key.wasPressedThisFrame) GoTo(2);
                else if (kb.digit4Key.wasPressedThisFrame) GoTo(3);
                else if (kb.digit5Key.wasPressedThisFrame) GoTo(4);
            }
        }
#endif
        if (_fpsCanvas.enabled) UpdateFps();

        float target = IsOpen ? 1f : 0f;
        if (!Mathf.Approximately(_anim, target))
        {
            _anim = Mathf.MoveTowards(_anim, target, Time.unscaledDeltaTime / 0.18f);
            ApplyAnim();
            if (!IsOpen && _anim <= 0f) SetVisible(false);
        }

        if (IsOpen && Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + 0.5f;
            Refresh();
        }
    }

    void Open()
    {
        EnsureEventSystem();
        IsOpen = true;
        SetVisible(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ShowTab(_tab);
    }

    void Close() { IsOpen = false; _forAreas = false; }

    void SetVisible(bool visible)
    {
        _canvas.enabled = visible;
        _group.blocksRaycasts = visible;
        if (!visible) { _anim = 0f; ApplyAnim(); }
    }

    void ApplyAnim()
    {
        float t = 1f - Mathf.Pow(1f - _anim, 3f);
        _group.alpha = t;
        _panel.anchoredPosition = new Vector2(0f, -28f * (1f - t));
        _panel.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, t);
    }

    void ShowTab(Tab tab)
    {
        _tab = tab;
        for (int i = 0; i < 4; i++)
        {
            bool on = i == (int)tab;
            _pages[i].gameObject.SetActive(on);
            _navBg[i].color = on ? new Color(1f, 1f, 1f, 0.14f) : new Color(1f, 1f, 1f, 0f);
            _navText[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0.85f);
            _navText[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
        }
        switch (tab)
        {
            case Tab.Areas: _title.text = "Chuyển khu vực"; _subtitle.text = "Bấm vào thẻ hoặc phím số để đi tới"; break;
            case Tab.Profile: _title.text = "Cá nhân"; _subtitle.text = "Thông tin tài khoản và phiên học hiện tại"; break;
            case Tab.Settings: _title.text = "Cài đặt"; _subtitle.text = "Áp dụng ngay, được lưu cho lần sau"; break;
            case Tab.Keys: _title.text = "Phím tắt"; _subtitle.text = "Điều khiển trên bàn phím và chuột"; break;
        }
        Refresh();
    }

    void GoTo(int index)
    {
        if (string.Equals(SceneManager.GetActiveScene().name, AreaScenes[index], System.StringComparison.OrdinalIgnoreCase)) { Close(); return; }
        Close();
        SceneQuickMenu.GoTo(index);
    }

    async void Logout()
    {
        if (_loggingOut) return;
        _loggingOut = true;
        Close();
        var runner = FindFirstObjectByType<NetworkRunner>();
        try
        {
            if (runner != null && runner.IsRunning) await runner.Shutdown(true, ShutdownReason.Ok);
        }
        catch (System.Exception ex) { Debug.LogWarning("[UttMenu] Lỗi khi đăng xuất: " + ex.Message); }
        _loggingOut = false;
        UttLoadingScreen.Begin("Login");
        SceneManager.LoadScene("Login");
    }

    // =========================================================
    // Dữ liệu
    FirstPersonController FindLocalPlayerCached()
    {
        if (_player != null && _player.isActiveAndEnabled) return _player;
        _player = null;
        if (Time.unscaledTime < _nextFind) return null;
        _nextFind = Time.unscaledTime + 1f;
        foreach (var p in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            if (p != null && p.Object != null && p.Object.IsValid && p.HasStateAuthority) { _player = p; break; }
        return _player;
    }

    void Refresh()
    {
        var player = FindLocalPlayerCached();
        string name = player != null ? player.NetworkPlayerName.ToString() : "";
        if (string.IsNullOrWhiteSpace(name)) name = PlayerPrefs.GetString("PlayerName", "Sinh viên");
        bool lecturer = PlayerPrefs.GetInt("IsLecturer", 0) == 1;
        string roleLine = player != null ? player.NetworkPlayerRole.ToString() : "";
        string cls = roleLine.StartsWith("Lớp:") ? roleLine.Substring(4).Trim() : "";
        string role = lecturer ? "Giảng viên" : "Sinh viên";
        string initials = Initials(name);
        string scene = SceneManager.GetActiveScene().name;

        _navInitials.text = initials; _navName.text = name;
        _navSub.text = cls.Length > 0 ? role + " · " + cls : role;

        switch (_tab)
        {
            case Tab.Profile:
            {
                _pInitials.text = initials; _pName.text = name; _pRole.text = role;
                _pRoleBg.color = lecturer ? new Color(Amber.r, Amber.g, Amber.b, 0.24f) : new Color(Blue.r, Blue.g, Blue.b, 0.26f);
                _pRole.color = lecturer ? new Color32(0xFF, 0xE3, 0xA3, 0xFF) : new Color32(0xD4, 0xEB, 0xFF, 0xFF);
                _pClass.transform.parent.gameObject.SetActive(cls.Length > 0);
                _pClass.text = cls;
                string account = PlayerPrefs.GetString("Username", "");
                _rAccount.text = string.IsNullOrEmpty(account) ? "Khách" : account;
                _rRole.text = role;
                _rClass.text = cls.Length > 0 ? cls : "—";
                _rArea.text = AreaName(scene);
                int count = 1;
                if (player != null && player.Runner != null && player.Runner.IsRunning)
                {
                    count = 0;
                    foreach (var _ in player.Runner.ActivePlayers) count++;
                    if (count == 0) count = 1;
                }
                _rPlayers.text = count + " người";
                float secs = _sessionStart < 0f ? 0f : Time.realtimeSinceStartup - _sessionStart;
                int m = Mathf.FloorToInt(secs / 60f);
                _rOnline.text = m >= 60 ? $"{m / 60} giờ {m % 60} phút" : $"{m} phút";
                break;
            }
            case Tab.Areas:
                for (int i = 0; i < 5; i++)
                {
                    bool here = string.Equals(scene, AreaScenes[i], System.StringComparison.OrdinalIgnoreCase);
                    if (_here[i] != null) _here[i].SetActive(here);
                    _areaRing[i].color = here ? Blue : CardRing;
                }
                break;
            case Tab.Settings:
            {
                int mode = (int)UttWebView.Current;
                for (int i = 0; i < 3; i++)
                {
                    bool on = i == mode;
                    _modeBg[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0f);
                    _modeText[i].color = on ? Navy : new Color(1f, 1f, 1f, 0.86f);
                    _modeText[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                }
                float sens = player != null ? player.mouseSensitivity : PlayerPrefs.GetFloat(MousePref, 2f);
                _mouse.SetValueWithoutNotify(sens);
                _mouseValue.text = sens.ToString("0.0");
                int q = PlayerPrefs.GetInt(QualityPref, 2);
                for (int i = 0; i < 3; i++)
                {
                    bool on = i == q;
                    _qBg[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0f);
                    _qText[i].color = on ? Navy : new Color(1f, 1f, 1f, 0.86f);
                    _qText[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                }
                _qNote.text = q switch
                {
                    0 => "Tắt bóng đổ, độ phân giải 70%, giảm chi tiết ở xa. Dành cho máy yếu.",
                    1 => "Bóng đổ ngắn hơn, độ phân giải 85%. Cân bằng giữa đẹp và mượt.",
                    _ => "Giữ nguyên hình ảnh gốc của dự án."
                };
                bool fps = _fpsCanvas.enabled;
                _fpsBtnText.text = fps ? "Đang bật" : "Đang tắt";
                _fpsBtnBg.color = fps ? new Color(Green.r, Green.g, Green.b, 0.28f) : new Color(1f, 1f, 1f, 0.10f);
                break;
            }
        }
    }

    static string Initials(string name)
    {
        var parts = name.Trim().Split(' ');
        if (parts.Length == 0 || parts[0].Length == 0) return "?";
        string first = parts[0], last = parts[parts.Length - 1];
        return parts.Length == 1 ? first.Substring(0, 1).ToUpper() : (first.Substring(0, 1) + last.Substring(0, 1)).ToUpper();
    }

    static string AreaName(string scene)
    {
        for (int i = 0; i < AreaScenes.Length; i++)
            if (string.Equals(scene, AreaScenes[i], System.StringComparison.OrdinalIgnoreCase)) return Areas[i].name;
        return scene;
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

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
#if ENABLE_INPUT_SYSTEM
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
    }

    // =========================================================
    // Dựng giao diện
    void BuildUi()
    {
        var canvasGo = new GameObject("UttMenuCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        _group = canvasGo.AddComponent<CanvasGroup>();

        // Lớp tối nhẹ phía sau, bấm ra ngoài để đóng
        var dim = Fill(canvasGo.transform, "Dim", null, new Color(0.02f, 0.05f, 0.12f, 0.38f));
        dim.raycastTarget = true;
        var dimBtn = dim.gameObject.AddComponent<UnityEngine.UI.Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        _panel = NewRect("Menu", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(PW, PH));
        Glass(_panel, _r28, _ring28, PH);
        var block = _panel.GetChild(1).GetComponent<Image>(); block.raycastTarget = true; // chặn click xuyên bảng

        BuildNav();

        // Đường chia
        var line = Rect(_panel, CX - 26f, -36f, 1f, PH - 72f);
        Solid(line, new Color(1f, 1f, 1f, 0.12f));

        // Tiêu đề nội dung
        _title = Text(_panel, "", 30, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold, CX, -34f, 600f, 42f);
        _subtitle = Text(_panel, "", 15, Muted, TextAlignmentOptions.TopLeft, FontStyles.Normal, CX, -80f, 600f, 24f);
        var close = Rect(_panel, CX + CW - 132f, -36f, 132f, 44f);
        PillButton(close, "Đóng", Close, out _);
        KeyChip(close, "E", 92f, -7f);

        for (int i = 0; i < 4; i++) _pages[i] = Rect(_panel, CX, -128f, CW, PH - 128f - 36f);
        BuildAreas(_pages[0]);
        BuildProfile(_pages[1]);
        BuildSettings(_pages[2]);
        BuildKeys(_pages[3]);
    }

    void BuildNav()
    {
        var avatar = Rect(_panel, NavX + 8f, -36f, 52f, 52f);
        Img(avatar, _r16, Blue);
        _navInitials = Text(avatar, "", 19, Navy, TextAlignmentOptions.Center, FontStyles.Bold);
        _navName = Text(_panel, "", 16, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold, NavX + 72f, -38f, NavW - 72f, 24f);
        _navName.textWrappingMode = TextWrappingModes.NoWrap; _navName.overflowMode = TextOverflowModes.Ellipsis;
        _navSub = Text(_panel, "", 13, Muted, TextAlignmentOptions.TopLeft, FontStyles.Normal, NavX + 72f, -64f, NavW - 72f, 20f);
        _navSub.textWrappingMode = TextWrappingModes.NoWrap; _navSub.overflowMode = TextOverflowModes.Ellipsis;

        float y = -124f;
        NavItem("Tiếp tục", y, Close, out _, out _); y -= 58f;
        string[] labels = { "Chuyển khu vực", "Cá nhân", "Cài đặt", "Phím tắt" };
        for (int i = 0; i < 4; i++)
        {
            Tab t = (Tab)i;
            NavItem(labels[i], y, () => ShowTab(t), out _navBg[i], out _navText[i]);
            y -= 58f;
        }
        NavItem("Đăng xuất", -(PH - 36f - 52f), Logout, out _, out var logoutText);
        logoutText.color = Orange;
    }

    void NavItem(string label, float y, UnityEngine.Events.UnityAction onClick, out Image bg, out TextMeshProUGUI text)
    {
        var r = Rect(_panel, NavX, y, NavW, 52f);
        bg = Img(r, _r14, new Color(1f, 1f, 1f, 0f));
        bg.raycastTarget = true;
        var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
        b.targetGraphic = bg;
        var c = b.colors; c.highlightedColor = new Color(1f, 1f, 1f, 1f); b.colors = c;
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        text = Text(r, label, 16, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.MidlineLeft, FontStyles.Normal, 18f, 0f, NavW - 36f, 52f);
    }

    void BuildAreas(RectTransform page)
    {
        float cw = (CW - 20f) / 2f, ch = 226f;
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            float x = (i % 2) * (cw + 20f), y = -(i / 2) * (ch + 18f);
            var card = Rect(page, x, y, cw, ch);
            var bg = Img(card, _r24, CardFill); bg.raycastTarget = true;
            _areaRing[i] = Img(card, _ring24, CardRing);
            var b = card.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = bg; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => GoTo(idx));

            // Ảnh khu vực (khối màu thay cho ảnh chụp; thay bằng ảnh chụp scene sau)
            var pic = Rect(card, 14f, -14f, cw - 28f, 138f);
            Img(pic, _r16, Areas[i].bottom);
            var picTop = NewRect("Sky", pic, new Vector2(0, 0.45f), new Vector2(1, 1), new Vector2(0.5f, 1f), Vector2.zero);
            Img(picTop, _r16, Areas[i].top);

            var here = Rect(pic, 10f, -10f, 128f, 28f);
            Img(here, _pill, Blue);
            Text(here, "Bạn đang ở đây", 12, Navy, TextAlignmentOptions.Center, FontStyles.Bold);
            _here[i] = here.gameObject;

            Text(card, Areas[i].name, 19, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold, 16f, -164f, cw - 90f, 28f);
            Text(card, Areas[i].sub, 13, Muted, TextAlignmentOptions.TopLeft, FontStyles.Normal, 16f, -192f, cw - 90f, 20f);
            KeyChip(card, (i + 1).ToString(), cw - 50f, -176f);
        }
        // Quản lý vòng đời: hàng ngang
        var row = Rect(page, 0f, -2f * (ch + 18f), CW, 60f);
        var rbg = Img(row, _r16, CardFill); rbg.raycastTarget = true;
        _areaRing[4] = Img(row, _ring16, CardRing);
        var rb = row.gameObject.AddComponent<UnityEngine.UI.Button>();
        rb.targetGraphic = rbg; rb.transition = Selectable.Transition.None;
        rb.onClick.AddListener(() => GoTo(4));
        Text(row, Areas[4].name, 16, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, 18f, 0f, 500f, 60f);
        KeyChip(row, "5", CW - 50f, -15f);
    }

    void BuildProfile(RectTransform page)
    {
        var avatar = Rect(page, 0f, 0f, 96f, 96f);
        Img(avatar, _r24, Blue);
        _pInitials = Text(avatar, "", 34, Navy, TextAlignmentOptions.Center, FontStyles.Bold);
        var dot = Rect(avatar, 76f, -76f, 22f, 22f);
        Img(dot, _pill, Navy);
        var inner = Rect(dot, 4f, -4f, 14f, 14f);
        Img(inner, _pill, Green);

        _pName = Text(page, "", 28, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold, 124f, -6f, 600f, 38f);
        _pName.textWrappingMode = TextWrappingModes.NoWrap; _pName.overflowMode = TextOverflowModes.Ellipsis;
        var roleChip = Rect(page, 124f, -54f, 116f, 32f);
        _pRoleBg = Img(roleChip, _pill, Color.white);
        Img(roleChip, _pillRing, new Color(1f, 1f, 1f, 0.3f));
        _pRole = Text(roleChip, "", 14, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        var classChip = Rect(page, 250f, -54f, 128f, 32f);
        Img(classChip, _pill, new Color(1f, 1f, 1f, 0.14f));
        Img(classChip, _pillRing, new Color(1f, 1f, 1f, 0.3f));
        _pClass = Text(classChip, "", 14, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        var box = Rect(page, 0f, -128f, 640f, 6f * 56f + 16f);
        Img(box, _r24, CardFill);
        Img(box, _ring24, CardRing);
        float y = -8f;
        _rAccount = Row(box, "Tài khoản", ref y, true);
        _rRole = Row(box, "Vai trò", ref y, true);
        _rClass = Row(box, "Lớp", ref y, true);
        _rArea = Row(box, "Khu vực hiện tại", ref y, true);
        _rPlayers = Row(box, "Người trong phòng", ref y, true);
        _rOnline = Row(box, "Thời gian online", ref y, false);

        var note = Text(page, "Điểm trắc nghiệm và tiến độ học sẽ hiện ở đây khi có tài khoản trực tuyến.", 14, Muted,
            TextAlignmentOptions.TopLeft, FontStyles.Normal, 0f, -128f - 6f * 56f - 36f, 640f, 40f);
    }

    TextMeshProUGUI Row(RectTransform box, string label, ref float y, bool divider)
    {
        Text(box, label, 16, Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, 24f, y, 260f, 56f);
        var v = Text(box, "", 17, Color.white, TextAlignmentOptions.MidlineRight, FontStyles.Bold, 280f, y, 640f - 280f - 24f, 56f);
        v.textWrappingMode = TextWrappingModes.NoWrap; v.overflowMode = TextOverflowModes.Ellipsis;
        y -= 56f;
        if (divider) Solid(Rect(box, 24f, y, 640f - 48f, 1f), new Color(1f, 1f, 1f, 0.10f));
        return v;
    }

    void BuildSettings(RectTransform page)
    {
        Text(page, "Góc nhìn", 16, Muted, TextAlignmentOptions.TopLeft, FontStyles.Normal, 0f, 0f, 400f, 24f);
        var seg = Rect(page, 0f, -34f, 600f, 56f);
        Img(seg, _pill, new Color(1f, 1f, 1f, 0.10f));
        Img(seg, _pillRing, new Color(1f, 1f, 1f, 0.22f));
        string[] names = { "Chuột tự do", "Thứ nhất", "Thứ ba" };
        float sw = (600f - 8f) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int mode = i;
            var b = Rect(seg, 4f + i * sw, -4f, sw, 48f);
            _modeBg[i] = Img(b, _pill, new Color(1, 1, 1, 0));
            _modeBg[i].raycastTarget = true;
            var btn = b.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = _modeBg[i]; btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { UttWebView.SetMode((UttWebView.Mode)mode); Refresh(); });
            _modeText[i] = Text(b, names[i], 16, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
        }
        Text(page, "Phím tắt: V", 13, Muted, TextAlignmentOptions.TopLeft, FontStyles.Normal, 0f, -98f, 400f, 20f);

        Text(page, "Độ nhạy chuột", 16, Muted, TextAlignmentOptions.TopLeft, FontStyles.Normal, 0f, -150f, 400f, 24f);
        _mouseValue = Text(page, "", 16, Color.white, TextAlignmentOptions.TopRight, FontStyles.Bold, 500f, -150f, 100f, 24f);
        _mouse = BuildSlider(Rect(page, 0f, -186f, 600f, 32f), 0.5f, 5f);
        _mouse.onValueChanged.AddListener(v =>
        {
            v = Mathf.Round(v * 10f) / 10f;
            PlayerPrefs.SetFloat(MousePref, v);
            PlayerPrefs.Save();
            var p = FindLocalPlayerCached();
            if (p != null) p.mouseSensitivity = v;
            _mouseValue.text = v.ToString("0.0");
        });

        Text(page, "Chất lượng đồ họa", 16, Muted, TextAlignmentOptions.TopLeft, FontStyles.Normal, 0f, -250f, 400f, 24f);
        Segmented(page, -284f, new[] { "Thấp", "Vừa", "Cao" }, _qBg, _qText, q =>
        {
            PlayerPrefs.SetInt(QualityPref, q);
            PlayerPrefs.Save();
            ApplyQuality(q);
            Refresh();
        });
        _qNote = Text(page, "", 13, Muted, TextAlignmentOptions.TopLeft, FontStyles.Normal, 0f, -348f, 700f, 20f);

        Text(page, "Hiện FPS ở góc màn hình", 16, Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, 0f, -390f, 400f, 44f);
        var fpsBtn = Rect(page, 460f, -390f, 140f, 44f);
        _fpsBtnBg = Img(fpsBtn, _pill, new Color(1f, 1f, 1f, 0.10f));
        _fpsBtnBg.raycastTarget = true;
        Img(fpsBtn, _pillRing, new Color(1f, 1f, 1f, 0.28f));
        var fb = fpsBtn.gameObject.AddComponent<UnityEngine.UI.Button>();
        fb.targetGraphic = _fpsBtnBg; fb.transition = Selectable.Transition.None;
        fb.onClick.AddListener(() =>
        {
            _fpsCanvas.enabled = !_fpsCanvas.enabled;
            _fpsTime = 0f; _fpsFrames = 0;
            PlayerPrefs.SetInt(FpsPref, _fpsCanvas.enabled ? 1 : 0);
            PlayerPrefs.Save();
            Refresh();
        });
        _fpsBtnText = Text(fpsBtn, "", 15, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        Text(page, "Bảng phím ở viền phải màn hình: bấm <color=#FFD77A><b>H</b></color> để ẩn hoặc hiện.", 15, Muted,
            TextAlignmentOptions.TopLeft, FontStyles.Normal, 0f, -470f, 700f, 24f);
    }

    void Segmented(RectTransform page, float y, string[] names, Image[] bgs, TextMeshProUGUI[] texts, System.Action<int> onPick)
    {
        var seg = Rect(page, 0f, y, 600f, 56f);
        Img(seg, _pill, new Color(1f, 1f, 1f, 0.10f));
        Img(seg, _pillRing, new Color(1f, 1f, 1f, 0.22f));
        float sw = (600f - 8f) / names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var b = Rect(seg, 4f + i * sw, -4f, sw, 48f);
            bgs[i] = Img(b, _pill, new Color(1, 1, 1, 0));
            bgs[i].raycastTarget = true;
            var btn = b.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bgs[i]; btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onPick(idx));
            texts[i] = Text(b, names[i], 16, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
        }
    }

    /// <summary>
    /// Đổi cấu hình đồ họa lúc chạy (chỉ bản Web). Mức Cao = đúng cấu hình gốc của dự án.
    /// Chỉ đổi độ phân giải render, tầm bóng đổ và độ chi tiết ở xa; không đụng model, vật liệu hay shader.
    /// </summary>
    static void ApplyQuality(int level)
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;
        if (!_defaultsCaptured)
        {
            _defScale = urp.renderScale;
            _defShadow = urp.shadowDistance;
            _defLod = QualitySettings.lodBias;
            _defaultsCaptured = true;
        }
        switch (level)
        {
            case 0:
                urp.renderScale = Mathf.Min(_defScale, 0.7f);
                urp.shadowDistance = 0f;
                QualitySettings.lodBias = _defLod * 0.6f;
                break;
            case 1:
                urp.renderScale = Mathf.Min(_defScale, 0.85f);
                urp.shadowDistance = _defShadow * 0.5f;
                QualitySettings.lodBias = _defLod * 0.8f;
                break;
            default:
                urp.renderScale = _defScale;
                urp.shadowDistance = _defShadow;
                QualitySettings.lodBias = _defLod;
                break;
        }
    }

    void BuildFpsCounter()
    {
        var go = new GameObject("UttFpsCanvas");
        go.transform.SetParent(transform, false);
        _fpsCanvas = go.AddComponent<Canvas>();
        _fpsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _fpsCanvas.sortingOrder = 5200;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var pill = Rect(go.transform, 24f, -24f, 190f, 40f);
        Img(pill, _pill, new Color(0.149f, 0.149f, 0.376f, 0.8f));
        Img(pill, _pillRing, new Color(1f, 1f, 1f, 0.28f));
        _fpsText = Text(pill, "— FPS", 16, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
    }

    void UpdateFps()
    {
        _fpsTime += Time.unscaledDeltaTime;
        _fpsFrames++;
        if (_fpsTime < 0.5f) return;
        float fps = _fpsFrames / _fpsTime;
        _fpsText.text = $"{fps:0} FPS · {1000f / Mathf.Max(fps, 0.01f):0.0} ms";
        _fpsText.color = fps >= 50f ? Color.white : fps >= 30f ? AmberText : Orange;
        _fpsTime = 0f; _fpsFrames = 0;
    }

    Slider BuildSlider(RectTransform r, float min, float max)
    {
        var track = Rect(r, 0f, -11f, r.sizeDelta.x, 10f);
        Img(track, _pill, new Color(1f, 1f, 1f, 0.16f));
        var fillArea = Rect(r, 0f, -11f, r.sizeDelta.x, 10f);
        var fill = NewRect("Fill", fillArea, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero);
        Img(fill, _pill, Blue);
        var handleArea = Rect(r, 12f, 0f, r.sizeDelta.x - 24f, 32f);
        var handle = NewRect("Handle", handleArea, Vector2.zero, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(28f, 0f));
        var hImg = Img(handle, _pill, Color.white);
        hImg.raycastTarget = true;
        var bgHit = Img(r, null, new Color(0, 0, 0, 0)); bgHit.raycastTarget = true;
        bgHit.transform.SetAsFirstSibling();
        var s = r.gameObject.AddComponent<Slider>();
        s.fillRect = fill; s.handleRect = handle; s.targetGraphic = hImg;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min; s.maxValue = max;
        s.transition = Selectable.Transition.None;
        return s;
    }

    void BuildKeys(RectTransform page)
    {
        (string k, string a)[] left =
        {
            ("W A S D", "Di chuyển"), ("Space", "Nhảy"), ("Chuột trái", "Tương tác"),
            ("Giữ chuột phải", "Xoay góc nhìn (chuột tự do)"), ("F", "Ngồi / rời ghế lái"),
        };
        (string k, string a)[] right =
        {
            ("E", "Mở / đóng menu này"), ("V", "Đổi góc nhìn"), ("Alt / Esc", "Hiện chuột"),
            ("M / F1", "Menu chuyển khu vực nhanh"), ("L / Tab", "Bảng bài giảng (Lớp học)"), ("H", "Ẩn / hiện bảng phím"),
        };
        float y = 0f;
        foreach (var it in left) { KeyRow(page, it.k, it.a, 0f, y); y -= 52f; }
        y -= 16f;
        Text(page, "Khi lái tàu", 15, Muted, TextAlignmentOptions.TopLeft, FontStyles.Bold, 0f, y, 300f, 22f); y -= 36f;
        KeyRow(page, "W", "Tăng tốc", 0f, y); y -= 52f;
        KeyRow(page, "S", "Giảm tốc", 0f, y); y -= 52f;
        KeyRow(page, "Space", "Dừng tàu", 0f, y);
        y = 0f;
        foreach (var it in right) { KeyRow(page, it.k, it.a, 470f, y); y -= 52f; }
    }

    void KeyRow(RectTransform page, string key, string action, float x, float y)
    {
        float w = KeyChip(page, key, x, y - 6f);
        Text(page, action, 16, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, x + Mathf.Max(w, 140f) + 14f, y, 300f, 44f);
    }

    float KeyChip(RectTransform parent, string key, float x, float y)
    {
        float w = Mathf.Max(32f, 18f + key.Length * 9.5f);
        var r = Rect(parent, x, y, w, 32f);
        Img(r, _r14, new Color(Amber.r, Amber.g, Amber.b, 0.2f));
        Img(r, _ring2px, new Color(Amber.r, Amber.g, Amber.b, 0.65f));
        Text(r, key, 14, AmberText, TextAlignmentOptions.Center, FontStyles.Bold);
        return w;
    }

    void PillButton(RectTransform r, string label, UnityEngine.Events.UnityAction onClick, out Image bg)
    {
        bg = Img(r, _pill, new Color(1f, 1f, 1f, 0.12f));
        bg.raycastTarget = true;
        Img(r, _pillRing, new Color(1f, 1f, 1f, 0.32f));
        var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
        b.targetGraphic = bg;
        var c = b.colors; c.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f); c.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); b.colors = c;
        b.onClick.AddListener(onClick);
        Text(r, label, 15, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, 18f, 0f, 80f, r.sizeDelta.y);
    }

    void Glass(RectTransform r, Sprite fill, Sprite ring, float height)
    {
        var shadow = NewRect("Shadow", r, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        shadow.offsetMin = new Vector2(-28f, -40f); shadow.offsetMax = new Vector2(28f, 16f);
        Img(shadow, _shadow, new Color(0.01f, 0.03f, 0.09f, 0.55f));
        Img(r, fill, GlassFill);
        Img(r, ring, GlassBorder);
        var shine = NewRect("Shine", r, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(-64f, 1f));
        shine.anchoredPosition = new Vector2(0f, -1f);
        Solid(shine, GlassShine);
    }

    // ----- tiện ích -----
    static RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot; r.sizeDelta = size;
        return r;
    }

    /// <summary>Hình chữ nhật neo góc trên-trái của cha, x/y tính từ góc đó (y âm là xuống dưới).</summary>
    static RectTransform Rect(Transform parent, float x, float y, float w, float h)
    {
        var r = NewRect("Box", parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(w, h));
        r.anchoredPosition = new Vector2(x, y);
        return r;
    }

    static Image Img(Transform parent, Sprite sprite, Color color)
    {
        var r = NewRect(sprite != null ? "Img" : "Rect", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Image Fill(Transform parent, string name, Sprite sprite, Color color)
    {
        var img = Img(parent, sprite, color);
        img.gameObject.name = name;
        return img;
    }

    static void Solid(RectTransform r, Color color)
    {
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    TextMeshProUGUI Text(Transform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        var r = NewRect("Text", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        return SetupText(r, text, size, color, align, style);
    }

    TextMeshProUGUI Text(Transform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles style, float x, float y, float w, float h)
        => SetupText(Rect(parent, x, y, w, h), text, size, color, align, style);

    TextMeshProUGUI SetupText(RectTransform r, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        r.gameObject.name = "Text";
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.raycastTarget = false;
        return t;
    }

    // Ảnh bo góc 9-slice tạo bằng code (một lần cho cả game)
    static void BuildSprites()
    {
        if (_r28 != null) return;
        _r28 = Rounded(28, 0f, 1f); _r24 = Rounded(24, 0f, 1f); _r16 = Rounded(16, 0f, 1f); _r14 = Rounded(10, 0f, 1f);
        _pill = Rounded(24, 0f, 1f);
        _ring28 = Rounded(28, 1.5f, 1f); _ring24 = Rounded(24, 1.5f, 1f); _ring16 = Rounded(16, 1.5f, 1f); _ring2px = Rounded(10, 1.5f, 1f);
        _pillRing = Rounded(24, 1.5f, 1f);
        _shadow = Rounded(56, 0f, 28f);
    }

    static Sprite Rounded(int radius, float ring, float feather)
    {
        int pad = Mathf.CeilToInt(feather);
        int size = radius * 2 + 4 + pad * 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        float half = size * 0.5f, inner = half - pad - radius;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float qx = Mathf.Abs(x + 0.5f - half) - inner, qy = Mathf.Abs(y + 0.5f - half) - inner;
            float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            float a = Mathf.Clamp01((0.5f - d) / feather);
            if (ring > 0f) a *= Mathf.Clamp01(d + ring + 0.5f);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        float border = radius + 2 + pad;
        return Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
    }
}
