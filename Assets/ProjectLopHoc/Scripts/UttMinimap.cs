using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Minimap và bản đồ lớn của bản Web (bộ UI Liquid Glass).
///  - Minimap tròn góc trên phải, xoay theo hướng nhìn. Mũi tên cam = mình, chấm xanh = sinh viên, chấm vàng = giảng viên.
///    Lăn chuột trên minimap (hoặc phím + / -) để phóng to, thu nhỏ.
///  - Phím M: bản đồ lớn (hướng Bắc lên trên), danh sách người trong khu vực, nút "Đến đây" và "Về điểm xuất phát".
///
/// Ảnh bản đồ không vẽ lại mỗi khung hình: một camera nhìn từ trên xuống chỉ chụp MỘT khung hình vào RenderTexture
/// khi vào scene, và chụp lại khi mình đi ra gần mép vùng đã chụp. Camera cắt mọi thứ cao hơn đầu người chơi
/// (clipAbove) nên trong nhà thấy được mặt bằng phòng thay vì mái nhà.
/// Chỉ chạy trên WebGL (được thêm trong UttWebView.Init), không ảnh hưởng bản VR.
/// </summary>
[DefaultExecutionOrder(-480)]
public sealed class UttMinimap : MonoBehaviour
{
    public static bool WorldMapOpen { get; private set; }

    // ----- Cấu hình theo scene -----
    struct SceneCfg
    {
        public bool enabled;
        public float area;       // cạnh vùng chụp (m)
        public float clipAbove;  // cắt mọi thứ cao hơn đầu người chơi bao nhiêu mét
        public float zoom;       // đường kính minimap mặc định (m)
        public bool teleport;    // cho phép "Đến đây"
    }

    static SceneCfg CfgFor(string scene)
    {
        switch (scene)
        {
            case "Login":
            case "QuanLyVongDoiTaiSan":
                return new SceneCfg { enabled = false };
            case "Classroom":
                return new SceneCfg { enabled = true, area = 70f, clipAbove = 2.3f, zoom = 30f, teleport = true };
            case "Showroom":
                return new SceneCfg { enabled = true, area = 160f, clipAbove = 6f, zoom = 60f, teleport = true };
            case "UTTCampus":
                return new SceneCfg { enabled = true, area = 360f, clipAbove = 35f, zoom = 110f, teleport = true };
            case "YenTuHaLongXanh":
                // Người chơi ngồi trên tàu đang chạy: không dịch chuyển, vùng chụp rộng để đỡ phải chụp lại.
                return new SceneCfg { enabled = true, area = 600f, clipAbove = 60f, zoom = 220f, teleport = false };
            default:
                return new SceneCfg { enabled = true, area = 220f, clipAbove = 20f, zoom = 80f, teleport = true };
        }
    }

    const float MiniSize = 230f;
    const int TexSize = 1024;
    const float MapSize = 720f;

    static readonly string[] AreaNames = { "UTTCampus", "Classroom", "Showroom", "YenTuHaLongXanh" };
    static readonly string[] AreaLabels = { "Khuôn viên UTT", "Lớp học", "Phòng trưng bày", "Yên Tử – Hạ Long" };

    SceneCfg _cfg;
    float _zoom;

    // Chụp bản đồ
    Camera _cam;
    RenderTexture _rt;
    int _captureFrame = -1;
    bool _hasCapture;
    Vector2 _capCenter;
    float _nextCaptureAllowed;

    // Minimap
    Canvas _miniCanvas;
    RectTransform _miniRoot, _rotor, _miniDots, _northLabel;
    RawImage _miniMap;
    TextMeshProUGUI _miniHint;

    // Bản đồ lớn
    Canvas _bigCanvas;
    CanvasGroup _bigGroup;
    RectTransform _bigPanel, _bigMapRect, _bigDots, _bigSelf, _listRoot;
    RawImage _bigMap;
    TextMeshProUGUI _bigTitle, _bigSub, _emptyNote;
    float _anim;

    // Người chơi
    FirstPersonController _local;
    float _nextPlayerScan;
    readonly List<FirstPersonController> _players = new List<FirstPersonController>();
    readonly List<Image> _miniDotPool = new List<Image>();
    readonly List<(Image dot, TextMeshProUGUI label)> _bigDotPool = new List<(Image, TextMeshProUGUI)>();
    readonly List<Row> _rows = new List<Row>();
    float _nextListRefresh;

    class Row
    {
        public RectTransform root;
        public Image dot;
        public TextMeshProUGUI name, info;
        public Button go;
        public FirstPersonController target;
    }

    static Sprite _circle, _circleRing, _dot, _arrow;
    TMP_FontAsset _font;

    // =========================================================
    void Awake()
    {
        _font = UttGlass.Font;
        _circle = UttGlass.Rounded(64, 0f, 1f);
        _circleRing = UttGlass.Rounded(64, 2f, 1f);
        _dot = UttGlass.Rounded(10, 0f, 1f);
        _arrow = MakeArrowSprite();

        _rt = new RenderTexture(TexSize, TexSize, 16, RenderTextureFormat.ARGB32) { name = "UttMinimapRT" };
        _rt.Create();
        BuildCamera();
        BuildMinimap();
        BuildWorldMap();
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        WorldMapOpen = false;
        if (_rt != null) _rt.Release();
    }

    void OnDisable() => WorldMapOpen = false;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) { _floorDirty = true; return; } // vùng đường ray nạp thêm ở Yên Tử
        _cfg = CfgFor(scene.name);
        _zoom = _cfg.zoom;
        _hasCapture = false;
        _local = null;
        _players.Clear();
        _nextPlayerScan = 0f;
        if (WorldMapOpen) CloseWorldMap();
        DisableLegacyMinimaps();
        _floorY = float.NaN; _floorDirty = true; _nextFloorScan = Time.unscaledTime + 2f; _fallRescues = 0; _ncc = null; _nccOwner = null;
        _hasHome = false; _hasSafe = false; _groundedSince = -1f;
    }

    // =========================================================
    // Điểm xuất phát thật: chỗ người chơi ĐÃ đứng vững sau khi vào scene (không tính toán từ điểm spawn trong scene,
    // vì ở Showroom điểm spawn gốc không có sàn). Kèm "chỗ an toàn gần nhất" để cứu khi rơi.
    bool _hasHome, _hasSafe;
    NetworkCharacterController _ncc;
    FirstPersonController _nccOwner;
    Vector3 _homePos, _safePos;
    Quaternion _homeRot;
    float _groundedSince = -1f, _nextSafeSample;

    void TrackSafeGround(FirstPersonController me)
    {
        if (me.isDriving) { _groundedSince = -1f; return; }
        bool grounded;
        if (_nccOwner != me) { _nccOwner = me; _ncc = me.GetComponent<NetworkCharacterController>(); }
        var ncc = _ncc;
        if (ncc != null) grounded = ncc.Grounded;
        else { var cc = me.GetComponent<CharacterController>(); grounded = cc != null && cc.isGrounded; }
        Vector3 p = me.transform.position;
        if (!grounded || (!float.IsNaN(_floorY) && p.y < _floorY - 1f)) { _groundedSince = -1f; return; }
        if (_groundedSince < 0f) _groundedSince = Time.unscaledTime;
        if (Time.unscaledTime - _groundedSince < 1f) return; // đứng trên sàn liên tục 1 giây mới tính là chỗ an toàn

        if (!_hasHome)
        {
            _hasHome = true;
            _homePos = p;
            Vector3 f = me.transform.forward; f.y = 0f;
            _homeRot = f.sqrMagnitude > 0.01f ? Quaternion.LookRotation(f) : me.transform.rotation;
            Debug.Log($"[UttMinimap] Ghi nhớ điểm xuất phát {_homePos} (scene {SceneManager.GetActiveScene().name})");
        }
        if (Time.unscaledTime >= _nextSafeSample)
        {
            _nextSafeSample = Time.unscaledTime + 0.5f;
            _safePos = p; _hasSafe = true;
        }
    }

    // =========================================================
    // Lưới an toàn: rơi xuyên sàn / rơi khỏi map thì tự đưa về điểm xuất phát (mọi scene có minimap).
    float _floorY = float.NaN, _nextFloorScan, _nextFallCheck;
    bool _floorDirty = true;
    int _fallRescues;

    void GuardFall(FirstPersonController me)
    {
        if (Time.unscaledTime < _nextFallCheck) return;
        _nextFallCheck = Time.unscaledTime + 0.25f;
        if (me.isDriving || _cfg.teleport == false) return; // trên tàu (Yên Tử) vị trí do tàu quyết định

        // Đáy của map = mặt dưới thấp nhất của các va chạm trong scene. Chỉ đo khi vào scene (sau 2 giây cho scene ổn định)
        // và khi có vùng nạp thêm, không quét liên tục (Campus có rất nhiều collider).
        if (_floorDirty && Time.unscaledTime >= _nextFloorScan)
        {
            _floorDirty = false;
            _nextFloorScan = Time.unscaledTime + 5f;
            float min = float.PositiveInfinity;
            foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c == null || !c.enabled || c.isTrigger || c is CharacterController) continue;
                float y = c.bounds.min.y;
                if (y < min && y > -10000f) min = y;
            }
            if (!float.IsInfinity(min)) _floorY = min;
        }
        if (float.IsNaN(_floorY)) return;
        if (me.transform.position.y > _floorY - 15f) return;

        if (_fallRescues >= 5) return; // tránh vòng lặp vô hạn nếu chính điểm xuất phát cũng hỏng
        _fallRescues++;
        Debug.LogWarning($"[UttMinimap] Người chơi rơi khỏi map tại {me.transform.position} (đáy map {_floorY:0.0}), đưa về điểm xuất phát.");
        if (_hasSafe || _hasHome)
        {
            Vector3 back = _hasSafe ? _safePos : _homePos;
            UttChat.Notice("Bạn bị rơi khỏi khu vực, đã đưa về chỗ đứng gần nhất.");
            PlaceAt(me, back + Vector3.up * 0.05f);
            return;
        }
        UttChat.Notice("Bạn bị rơi khỏi khu vực, đã đưa về điểm xuất phát.");
        GoToSpawn(force: true);
    }

    /// <summary>Minimap cũ (MinimapTeleport + camera vẽ vào MinimapRT mỗi khung hình) chỉ dành cho bản VR.</summary>
    static void DisableLegacyMinimaps()
    {
        foreach (var old in FindObjectsByType<MinimapTeleport>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (old.mapPanel != null) old.mapPanel.SetActive(false);
            if (old.minimapCamera != null) old.minimapCamera.enabled = false;
            old.enabled = false;
        }
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (cam.targetTexture != null && cam.targetTexture.name == "MinimapRT") cam.enabled = false;
    }

    // =========================================================
    void Update()
    {
        if (_captureFrame >= 0 && Time.frameCount > _captureFrame)
        {
            // Camera đã vẽ xong một khung hình vào _rt ở cuối khung trước: tắt lại ngay.
            _cam.enabled = false;
            _captureFrame = -1;
            _hasCapture = true;
        }

        bool sceneOk = _cfg.enabled && UttWebView.SceneAllowsFirstPerson;
        var player = sceneOk ? FindLocal() : null;
        bool show = player != null;

        if (!show && WorldMapOpen) CloseWorldMap();

        if (show)
        {
            Vector3 p = player.transform.position;
            Vector2 p2 = new Vector2(p.x, p.z);
            bool nearEdge = _hasCapture && Vector2.Distance(p2, _capCenter) > _cfg.area * 0.3f;
            if ((!_hasCapture || nearEdge) && _captureFrame < 0 && Time.unscaledTime >= _nextCaptureAllowed)
                Capture(p);

            ScanPlayers();
            TrackSafeGround(player);
            GuardFall(player);
            HandleInput();
            UpdateMinimap(player);
            if (WorldMapOpen) UpdateWorldMap(player);
        }

        bool miniVisible = show && !WorldMapOpen && _hasCapture;
        if (_miniCanvas.enabled != miniVisible) _miniCanvas.enabled = miniVisible;

        float target = WorldMapOpen ? 1f : 0f;
        if (!Mathf.Approximately(_anim, target))
        {
            _anim = Mathf.MoveTowards(_anim, target, Time.unscaledDeltaTime / 0.16f);
            float t = 1f - Mathf.Pow(1f - _anim, 3f);
            _bigGroup.alpha = t;
            _bigPanel.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, t);
            if (_anim <= 0f) _bigCanvas.enabled = false;
        }
    }

    void HandleInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null || IsTyping() || SceneQuickMenu.IsOpen) return;

        if (kb.mKey.wasPressedThisFrame && !UttProfilePanel.IsOpen)
        {
            if (WorldMapOpen) CloseWorldMap(); else OpenWorldMap();
        }
        else if (WorldMapOpen && kb.escapeKey.wasPressedThisFrame) CloseWorldMap();

        if (UttProfilePanel.IsOpen || WorldMapOpen) return;

        if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) Zoom(-1f);
        if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) Zoom(1f);

        var mouse = Mouse.current;
        if (mouse != null && Cursor.lockState != CursorLockMode.Locked)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f &&
                RectTransformUtility.RectangleContainsScreenPoint(_miniRoot, mouse.position.ReadValue(), null))
                Zoom(scroll > 0f ? -1f : 1f);
        }
#endif
    }

    void Zoom(float dir)
    {
        float min = 15f, max = Mathf.Max(min + 1f, _cfg.area * 0.9f);
        _zoom = Mathf.Clamp(_zoom * (dir < 0f ? 0.8f : 1.25f), min, max);
    }

    // =========================================================
    // Chụp ảnh bản đồ
    void BuildCamera()
    {
        var go = new GameObject("UttMinimapCamera");
        go.transform.SetParent(transform, false);
        _cam = go.AddComponent<Camera>();
        _cam.enabled = false;
        _cam.orthographic = true;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color32(0x1E, 0x1E, 0x4E, 0xFF);
        _cam.targetTexture = _rt;
        _cam.nearClipPlane = 0.05f;
        _cam.allowHDR = false;
        _cam.allowMSAA = false;
        _cam.useOcclusionCulling = false;
        _cam.cullingMask = ~(1 << 5); // bỏ lớp UI
        _cam.depth = -100;
        var data = _cam.GetUniversalAdditionalCameraData();
        data.renderShadows = false;
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.Off;
    }

    void Capture(Vector3 playerPos)
    {
        _capCenter = new Vector2(playerPos.x, playerPos.z);
        float height = _cfg.clipAbove;
        _cam.transform.SetPositionAndRotation(new Vector3(playerPos.x, playerPos.y + height, playerPos.z), Quaternion.Euler(90f, 0f, 0f));
        _cam.orthographicSize = _cfg.area * 0.5f;
        _cam.farClipPlane = height + 500f;
        _cam.enabled = true;              // vẽ đúng một khung hình rồi tắt ở Update sau
        _captureFrame = Time.frameCount;
        _nextCaptureAllowed = Time.unscaledTime + 1.5f;
    }

    // =========================================================
    // Người chơi
    FirstPersonController FindLocal()
    {
        if (_local != null && _local.isActiveAndEnabled && _local.Object != null && _local.Object.IsValid) return _local;
        _local = null;
        if (Time.unscaledTime < _nextPlayerScan) return null;
        _nextPlayerScan = Time.unscaledTime + 1f;
        foreach (var p in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            if (p != null && p.Object != null && p.Object.IsValid && p.HasStateAuthority) { _local = p; break; }
        return _local;
    }

    float _nextOthersScan;
    void ScanPlayers()
    {
        if (Time.unscaledTime < _nextOthersScan) return;
        _nextOthersScan = Time.unscaledTime + 0.5f;
        _players.Clear();
        foreach (var p in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            if (p != null && p != _local && p.Object != null && p.Object.IsValid && p.isActiveAndEnabled) _players.Add(p);
    }

    static bool IsLecturer(FirstPersonController p)
    {
        try { return p.NetworkPlayerRole.ToString() == "Giảng viên"; }
        catch { return false; }
    }

    static string NameOf(FirstPersonController p)
    {
        try
        {
            string n = p.NetworkPlayerName.ToString();
            return string.IsNullOrWhiteSpace(n) ? "Người chơi" : n;
        }
        catch { return "Người chơi"; }
    }

    float ViewYaw(FirstPersonController player)
    {
        var cam = Camera.main;
        Vector3 f = cam != null && cam != _cam ? cam.transform.forward : player.transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) f = player.transform.forward;
        return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    // =========================================================
    // Minimap
    void BuildMinimap()
    {
        var go = new GameObject("UttMinimapCanvas");
        go.transform.SetParent(transform, false);
        _miniCanvas = go.AddComponent<Canvas>();
        _miniCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _miniCanvas.sortingOrder = 4990;
        AddScaler(go);
        _miniCanvas.enabled = false;

        _miniRoot = NewRect("Minimap", go.transform);
        Place(_miniRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(MiniSize, MiniSize));

        var shadow = NewRect("Shadow", _miniRoot);
        Stretch(shadow, -10f);
        Img(shadow, UttGlass.Shadow, new Color(0f, 0f, 0f, 0.35f), true);

        Img(Fill(NewRect("Bg", _miniRoot)), _circle, UttGlass.Fill, false);

        var maskRect = Fill(NewRect("Mask", _miniRoot));
        Img(maskRect, _circle, Color.white, false);
        maskRect.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        _rotor = NewRect("Rotor", maskRect);
        Place(_rotor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        _miniMap = NewRect("Map", _rotor).gameObject.AddComponent<RawImage>();
        _miniMap.texture = _rt;
        _miniMap.raycastTarget = false;
        _miniMap.color = new Color(1f, 1f, 1f, 0.92f);

        _miniDots = NewRect("Dots", _rotor);
        Place(_miniDots, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        _northLabel = NewRect("North", _rotor);
        Place(_northLabel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, MiniSize * 0.5f - 16f), new Vector2(24f, 24f));
        Img(_northLabel, _dot, UttGlass.Navy, false);
        Label(_northLabel, "B", 14f, UttGlass.AmberText, TextAlignmentOptions.Center, FontStyles.Bold);

        Img(Fill(NewRect("Ring", _miniRoot)), _circleRing, UttGlass.SoftRing, false);

        var arrow = NewRect("Me", _miniRoot);
        Place(arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
        Img(arrow, _arrow, UttGlass.Accent, false);

        var hint = NewRect("Hint", _miniRoot);
        Place(hint, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(150f, 30f));
        Img(hint, UttGlass.Pill, UttGlass.FillLight, true);
        _miniHint = Label(hint, "<color=#FFD54F><b>M</b></color>  Bản đồ", 15f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
    }

    void UpdateMinimap(FirstPersonController player)
    {
        if (!_hasCapture) return;
        Vector3 p = player.transform.position;
        float ppm = MiniSize / Mathf.Max(1f, _zoom);
        float yaw = ViewYaw(player);
        _rotor.localEulerAngles = new Vector3(0f, 0f, yaw);
        _northLabel.localEulerAngles = new Vector3(0f, 0f, -yaw);

        var mapRt = _miniMap.rectTransform;
        mapRt.anchorMin = mapRt.anchorMax = new Vector2(0.5f, 0.5f);
        mapRt.pivot = new Vector2(0.5f, 0.5f);
        mapRt.sizeDelta = Vector2.one * (_cfg.area * ppm);
        mapRt.anchoredPosition = new Vector2(_capCenter.x - p.x, _capCenter.y - p.z) * ppm;

        float radius = MiniSize * 0.5f - 10f;
        EnsurePool(_miniDotPool, _players.Count, _miniDots, 12f);
        for (int i = 0; i < _miniDotPool.Count; i++)
        {
            var dot = _miniDotPool[i];
            bool on = i < _players.Count;
            if (dot.gameObject.activeSelf != on) dot.gameObject.SetActive(on);
            if (!on) continue;
            var other = _players[i];
            Vector3 o = other.transform.position;
            Vector2 d = new Vector2(o.x - p.x, o.z - p.z) * ppm;
            bool clamped = d.magnitude > radius;
            if (clamped) d = d.normalized * radius;
            dot.rectTransform.anchoredPosition = d;
            Color c = IsLecturer(other) ? UttGlass.Amber : UttGlass.Green;
            c.a = clamped ? 0.55f : 1f;
            dot.color = c;
        }
    }

    void EnsurePool(List<Image> pool, int count, RectTransform parent, float size)
    {
        while (pool.Count < count)
        {
            var r = NewRect("Dot", parent);
            Place(r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var img = Img(r, _dot, Color.white, false);
            var ring = Fill(NewRect("Ring", r));
            Img(ring, UttGlass.Rounded(10, 1.5f, 1f), new Color(0f, 0f, 0f, 0.55f), false);
            pool.Add(img);
        }
    }

    // =========================================================
    // Bản đồ lớn
    void BuildWorldMap()
    {
        var go = new GameObject("UttWorldMapCanvas");
        go.transform.SetParent(transform, false);
        _bigCanvas = go.AddComponent<Canvas>();
        _bigCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _bigCanvas.sortingOrder = 5050;
        AddScaler(go);
        go.AddComponent<GraphicRaycaster>();
        _bigGroup = go.AddComponent<CanvasGroup>();
        _bigGroup.alpha = 0f;
        _bigCanvas.enabled = false;

        // Nền tối phía sau, bấm ra ngoài để đóng
        var dim = Fill(NewRect("Dim", go.transform));
        var dimImg = Img(dim, null, new Color(0.05f, 0.05f, 0.14f, 0.55f), false);
        dimImg.raycastTarget = true;
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(CloseWorldMap);

        var panel = NewRect("Panel", go.transform);
        _bigPanel = panel;
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1280f, 800f));
        var shadow = NewRect("Shadow", panel);
        Stretch(shadow, -24f);
        Img(shadow, UttGlass.Shadow, new Color(0f, 0f, 0f, 0.45f), true);
        var panelImg = Img(panel, UttGlass.Round28, UttGlass.Fill, true);
        panelImg.raycastTarget = true; // chặn click lọt xuống nền tối
        UttGlass.Panel(panelImg);

        // Ảnh bản đồ bên trái
        var frame = NewRect("MapFrame", panel);
        Place(frame, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(MapSize, MapSize));
        Img(frame, UttGlass.Round18, new Color(1f, 1f, 1f, 0.06f), true);
        var frameMask = Fill(NewRect("Mask", frame));
        Img(frameMask, UttGlass.Round18, Color.white, true);
        frameMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        _bigMapRect = Fill(NewRect("Map", frameMask));
        _bigMap = _bigMapRect.gameObject.AddComponent<RawImage>();
        _bigMap.texture = _rt;
        _bigMap.raycastTarget = false;
        _bigDots = Fill(NewRect("Dots", frameMask));
        _bigSelf = NewRect("Me", frameMask);
        Place(_bigSelf, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
        Img(_bigSelf, _arrow, UttGlass.Accent, false);
        Img(Fill(NewRect("Ring", frame)), UttGlass.Ring18, UttGlass.Border, true);

        // Hướng Bắc
        var north = NewRect("North", frame);
        Place(north, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(76f, 32f));
        Img(north, UttGlass.Pill, UttGlass.Fill, true);
        var northArrow = NewRect("Arrow", north);
        Place(northArrow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(16f, 16f));
        Img(northArrow, _arrow, UttGlass.AmberText, false);
        var northText = Box(north, 30f, 0f, 44f, 32f);
        Label(northText, "Bắc", 15f, UttGlass.AmberText, TextAlignmentOptions.Left, FontStyles.Bold);

        // Cột phải
        float colX = 40f + MapSize + 36f, colW = 1280f - colX - 40f;
        _bigTitle = Label(Box(panel, colX, -40f, colW, 40f), "Bản đồ", 30f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        _bigSub = Label(Box(panel, colX, -82f, colW, 26f), "", 16f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Normal);

        var spawnBtn = MakeButton(Box(panel, colX, -126f, colW, 46f), "Về điểm xuất phát", true, GoToSpawn);
        spawnBtn.name = "SpawnButton";
        var recap = MakeButton(Box(panel, colX, -182f, colW, 40f), "Chụp lại bản đồ tại chỗ đang đứng", false, () =>
        {
            var p = FindLocal();
            if (p != null) { _nextCaptureAllowed = 0f; Capture(p.transform.position); }
        });
        recap.name = "RecaptureButton";

        Label(Box(panel, colX, -244f, colW, 24f), "NGƯỜI TRONG KHU VỰC", 14f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Bold);
        _listRoot = Box(panel, colX, -276f, colW, 440f);
        _emptyNote = Label(Box(panel, colX, -280f, colW, 60f), "Chưa có ai khác trong khu vực này.", 16f, UttGlass.Muted, TextAlignmentOptions.TopLeft, FontStyles.Italic);

        // Chú thích + phím
        LegendItem(panel, colX, -730f, _arrow, UttGlass.Accent, "Bạn");
        LegendItem(panel, colX + 90f, -730f, _dot, UttGlass.Green, "Sinh viên");
        LegendItem(panel, colX + 210f, -730f, _dot, UttGlass.Amber, "Giảng viên");
        Label(Box(panel, colX, -760f, colW, 24f), "<color=#FFD54F><b>M</b></color> / <color=#FFD54F><b>Esc</b></color>  Đóng bản đồ", 14f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Normal);
    }

    void LegendItem(RectTransform panel, float x, float y, Sprite icon, Color color, string text)
    {
        var item = Box(panel, x, y, 120f, 26f);
        var ic = NewRect("Icon", item);
        Place(ic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(14f, 14f));
        Img(ic, icon, color, false);
        Label(Box(item, 20f, 0f, 100f, 26f), text, 15f, Color.white, TextAlignmentOptions.Left, FontStyles.Normal);
    }

    void OpenWorldMap()
    {
        EnsureEventSystem();
        WorldMapOpen = true;
        _bigCanvas.enabled = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _nextListRefresh = 0f;
        string scene = SceneManager.GetActiveScene().name;
        int idx = System.Array.IndexOf(AreaNames, scene);
        _bigTitle.text = "Bản đồ · " + (idx >= 0 ? AreaLabels[idx] : scene);
        _bigSub.text = $"Vùng hiển thị {Mathf.RoundToInt(_cfg.area)} x {Mathf.RoundToInt(_cfg.area)} m quanh chỗ bạn đứng";
        var spawn = _bigCanvas.transform.Find("Panel/SpawnButton");
        if (spawn != null) spawn.gameObject.SetActive(_cfg.teleport);
    }

    void CloseWorldMap()
    {
        WorldMapOpen = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void UpdateWorldMap(FirstPersonController player)
    {
        float ppm = MapSize / _cfg.area;
        Vector3 p = player.transform.position;

        _bigSelf.anchoredPosition = new Vector2(p.x - _capCenter.x, p.z - _capCenter.y) * ppm;
        _bigSelf.localEulerAngles = new Vector3(0f, 0f, -ViewYaw(player));

        while (_bigDotPool.Count < _players.Count)
        {
            var r = NewRect("Dot", _bigDots);
            Place(r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            var img = Img(r, _dot, Color.white, false);
            Img(Fill(NewRect("Ring", r)), UttGlass.Rounded(10, 1.5f, 1f), new Color(0f, 0f, 0f, 0.6f), false);
            var lr = NewRect("Name", r);
            Place(lr, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(220f, 22f));
            var label = Label(lr, "", 14f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(0, 0, 0, 200);
            _bigDotPool.Add((img, label));
        }
        float half = MapSize * 0.5f;
        for (int i = 0; i < _bigDotPool.Count; i++)
        {
            var (dot, label) = _bigDotPool[i];
            bool on = i < _players.Count;
            if (on)
            {
                Vector3 o = _players[i].transform.position;
                Vector2 d = new Vector2(o.x - _capCenter.x, o.z - _capCenter.y) * ppm;
                on = Mathf.Abs(d.x) < half && Mathf.Abs(d.y) < half;
                if (on)
                {
                    dot.rectTransform.anchoredPosition = d;
                    dot.color = IsLecturer(_players[i]) ? UttGlass.Amber : UttGlass.Green;
                    label.text = NameOf(_players[i]);
                }
            }
            if (dot.gameObject.activeSelf != on) dot.gameObject.SetActive(on);
        }

        if (Time.unscaledTime >= _nextListRefresh)
        {
            _nextListRefresh = Time.unscaledTime + 0.5f;
            RefreshList(player);
        }
    }

    void RefreshList(FirstPersonController player)
    {
        // Giảng viên lên đầu, sau đó theo khoảng cách
        Vector3 p = player.transform.position;
        _players.Sort((a, b) =>
        {
            int la = IsLecturer(a) ? 0 : 1, lb = IsLecturer(b) ? 0 : 1;
            if (la != lb) return la.CompareTo(lb);
            return (a.transform.position - p).sqrMagnitude.CompareTo((b.transform.position - p).sqrMagnitude);
        });

        int shown = Mathf.Min(_players.Count, 8);
        while (_rows.Count < shown) _rows.Add(MakeRow(_rows.Count));
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            bool on = i < shown;
            if (row.root.gameObject.activeSelf != on) row.root.gameObject.SetActive(on);
            if (!on) { row.target = null; continue; }
            var other = _players[i];
            bool lec = IsLecturer(other);
            row.target = other;
            row.dot.color = lec ? UttGlass.Amber : UttGlass.Green;
            row.name.text = NameOf(other);
            float dist = Vector3.Distance(other.transform.position, p);
            row.info.text = (lec ? "Giảng viên" : "Sinh viên") + " · " + (dist < 1000f ? Mathf.RoundToInt(dist) + " m" : (dist / 1000f).ToString("0.0") + " km");
            row.go.gameObject.SetActive(_cfg.teleport && dist > 4f);
        }
        _emptyNote.gameObject.SetActive(_players.Count == 0);
    }

    Row MakeRow(int index)
    {
        var row = new Row();
        float w = _listRoot.sizeDelta.x;
        row.root = NewRect("Row" + index, _listRoot);
        Place(row.root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -index * 54f), new Vector2(w, 48f));
        Img(row.root, UttGlass.Round14, new Color(1f, 1f, 1f, 0.07f), true);
        var dot = NewRect("Dot", row.root);
        Place(dot, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), new Vector2(12f, 12f));
        row.dot = Img(dot, _dot, Color.white, false);
        row.name = Label(Box(row.root, 40f, -4f, w - 170f, 24f), "", 16f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        row.name.overflowMode = TextOverflowModes.Ellipsis;
        row.info = Label(Box(row.root, 40f, -26f, w - 170f, 20f), "", 13f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Normal);
        var go = Box(row.root, w - 120f, -8f, 108f, 32f);
        row.go = MakeButton(go, "Đến đây", false, () => GoToPlayer(row.target));
        return row;
    }

    // =========================================================
    // Dịch chuyển (chỉ tới điểm có sẵn: chỗ người khác đang đứng, điểm xuất phát)
    void GoToPlayer(FirstPersonController other)
    {
        var me = FindLocal();
        if (me == null || other == null || !_cfg.teleport || me.isDriving) return;
        Vector3 back = other.transform.forward; back.y = 0f;
        if (back.sqrMagnitude < 0.01f) back = Vector3.forward;
        Vector3 target = other.transform.position - back.normalized * 1.5f;
        // Phía sau có tường thì đứng ngay cạnh
        Vector3 from = other.transform.position + Vector3.up * 1f;
        if (Physics.Linecast(from, target + Vector3.up * 1f, ~0, QueryTriggerInteraction.Ignore))
            target = other.transform.position + other.transform.right * 1.2f;
        TeleportLocal(me, target);
    }

    void GoToSpawn() => GoToSpawn(false);

    void GoToSpawn(bool force)
    {
        var me = FindLocal();
        if (me == null || !_cfg.teleport || me.isDriving) return;
        if (!force) _fallRescues = 0;
        if (_hasHome)
        {
            Debug.Log($"[UttMinimap] Về điểm xuất phát đã ghi nhớ {_homePos}");
            PlaceAt(me, _homePos + Vector3.up * 0.05f);
            Vector3 f = _homeRot * Vector3.forward;
            me.FaceTowards(_homePos + Vector3.up * 1.6f + f * 10f);
            CloseWorldMap();
            return;
        }
        bool lec = UttSession.IsLecturer;
        // Dùng đúng cách tính vị trí như lúc vào scene (PlayerSpawner), không lấy thẳng điểm spawn gốc:
        // ở Showroom điểm gốc nằm ngoài/ trên mái, lúc vào scene game đã dời người chơi tới trước sa bàn.
        var spawner = FindFirstObjectByType<PlayerSpawner>();
        if (spawner != null && spawner.gameObject.scene == SceneManager.GetActiveScene() &&
            spawner.TryGetSpawnPose(lec, out var pos, out var rot))
        {
            Debug.Log($"[UttMinimap] Về điểm xuất phát: từ {me.transform.position} tới {pos} (scene {SceneManager.GetActiveScene().name})");
            TeleportLocal(me, pos, rot);
            return;
        }
        var go = GameObject.Find(lec ? "Spawn_Lecturer" : "Spawn_Student");
        Debug.LogWarning($"[UttMinimap] Không dùng được PlayerSpawner (có={spawner != null}), lấy thẳng điểm '{(go != null ? go.name : "không có")}'.");
        if (go != null) TeleportLocal(me, go.transform.position, go.transform.rotation);
    }

    void TeleportLocal(FirstPersonController me, Vector3 target, Quaternion? facing = null)
    {
        // Mặt sàn gần nhất với điểm đích (giống lúc spawn), không lấy vật đầu tiên chạm phải từ trên xuống (mái, trần...)
        target = PlayerSpawner.SnapToGround(target, out bool ground);
        Debug.Log($"[UttMinimap] Dịch chuyển tới {target}, có sàn={ground}");
        if (!ground)
        {
            UttChat.Notice("Khu vực này chưa tải xong, thử lại sau vài giây.");
            return;
        }
        PlaceAt(me, target);
        if (facing.HasValue)
        {
            Vector3 f = facing.Value * Vector3.forward; f.y = 0f;
            if (f.sqrMagnitude > 0.01f) me.FaceTowards(target + Vector3.up * 1.6f + f.normalized * 10f);
        }
        CloseWorldMap();
        if (_landing != null) StopCoroutine(_landing);
        _landing = StartCoroutine(SafeLanding(me, target));
    }

    static void PlaceAt(FirstPersonController me, Vector3 target)
    {
        var ncc = me.GetComponent<NetworkCharacterController>();
        if (ncc != null)
        {
            // Teleport của Fusion giữ nguyên vận tốc: đang rơi nhanh mà không xoá vận tốc thì xuyên thủng sàn ngay khung sau.
            ncc.Velocity = Vector3.zero;
            ncc.Teleport(target);
            ncc.Velocity = Vector3.zero;
        }
        else me.transform.position = target;
    }

    Coroutine _landing;

    /// <summary>
    /// Chống rơi khỏi map sau khi dịch chuyển: trong 4 giây, nếu nhân vật tụt xuống quá 3 m dưới điểm đích
    /// (sàn chưa kịp có va chạm, bị đẩy ra khỏi tường...) thì đưa lại về điểm đích.
    /// </summary>
    System.Collections.IEnumerator SafeLanding(FirstPersonController me, Vector3 target)
    {
        float end = Time.unscaledTime + 4f;
        int resets = 0;
        while (Time.unscaledTime < end && me != null && resets < 3)
        {
            yield return new WaitForSecondsRealtime(0.2f);
            if (me == null) break;
            if (me.transform.position.y < target.y - 3f)
            {
                resets++;
                PlaceAt(me, PlayerSpawner.SnapToGround(target));
            }
        }
        _landing = null;
    }

    // =========================================================
    // UI helpers
    static void AddScaler(GameObject go)
    {
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    static RectTransform Fill(RectTransform r) { Stretch(r, 0f); return r; }

    static void Stretch(RectTransform r, float inset)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>Hộp đặt theo toạ độ góc trên trái của cha (x sang phải, y âm xuống dưới).</summary>
    static RectTransform Box(RectTransform parent, float x, float y, float w, float h)
    {
        var r = NewRect("Box", parent);
        Place(r, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, h));
        return r;
    }

    static Image Img(RectTransform r, Sprite sprite, Color color, bool sliced)
    {
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI Label(RectTransform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        var r = Fill(NewRect("Text", parent));
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.richText = true;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    Button MakeButton(RectTransform r, string text, bool primary, UnityEngine.Events.UnityAction onClick)
    {
        var img = Img(r, UttGlass.Pill, Color.white, true);
        img.raycastTarget = true;
        var btn = r.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        Label(r, text, primary ? 17f : 15f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        if (primary) UttGlass.PrimaryButton(btn); else UttGlass.GlassButton(btn);
        btn.onClick.AddListener(onClick);
        return btn;
    }

    static Sprite MakeArrowSprite()
    {
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[s * s];
        // Mũi tên hướng lên: đỉnh (32, 60), hai cánh (8, 6) và (56, 6), khía lõm ở (32, 20)
        Vector2 a = new Vector2(32f, 60f), b = new Vector2(56f, 6f), c = new Vector2(32f, 20f), d = new Vector2(8f, 6f);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            var q = new Vector2(x + 0.5f, y + 0.5f);
            bool inside = InTri(q, a, b, c) || InTri(q, a, c, d);
            bool edge = !inside && (InTri(q, a + new Vector2(0, 3), b + new Vector2(3, -2), c + new Vector2(0, -3)) ||
                                    InTri(q, a + new Vector2(0, 3), c + new Vector2(0, -3), d + new Vector2(-3, -2)));
            px[y * s + x] = inside ? new Color32(255, 255, 255, 255) : edge ? new Color32(30, 30, 70, 230) : new Color32(0, 0, 0, 0);
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
    }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

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
}

/// <summary>
/// Các bảng của bản Web đang chiếm bàn phím/chuột: khi true thì nhân vật không di chuyển, không xoay góc nhìn.
/// </summary>
public static class UttOverlay
{
    public static bool BlocksGameplay => UttProfilePanel.IsOpen || UttMinimap.WorldMapOpen || UttChat.IsTyping || UttPresenter.ZoomOpen || UttClassPanel.IsOpen || UttQuizLive.IsOpen;
}
