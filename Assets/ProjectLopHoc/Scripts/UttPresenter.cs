using System.Collections.Generic;
using ProjectLopHoc.Features.Lecture;
using ProjectLopHoc.Features.Quiz;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Chế độ trình chiếu trong lớp của bản Web (bộ UI Liquid Glass). Chỉ hiện ở scene có LectureBoardController (Classroom).
///
/// Giảng viên:
///  - P hoặc nút "Bắt đầu trình chiếu" ở giữa phía trên. Bật/tắt được đồng bộ cho cả phòng (LectureBoardController.IsPresenting).
///  - Thanh dưới: trang trước/sau, ảnh các trang, gọi cả lớp nhìn bảng, mở/đóng Quiz Live (QuizSessionController), kết thúc.
///  - PgUp / PgDn đổi trang (bút trình chiếu gửi đúng 2 phím này), B ẩn/hiện thanh, Z xem gần.
///  - Góc phải dưới minimap: danh sách học viên giơ tay, có nút "Hạ tay".
/// Học viên:
///  - Không đổi được trang. Khi giảng viên trình chiếu: chip trạng thái phía trên, Z xem gần slide, G giơ/hạ tay.
///  - Giảng viên bấm "Gọi cả lớp nhìn bảng" thì góc nhìn tự quay về màn chiếu.
/// Được thêm trong UttWebView.Init, nên chỉ chạy trên WebGL; bản VR không đổi.
/// </summary>
[DefaultExecutionOrder(-460)]
public sealed class UttPresenter : MonoBehaviour
{
    static UttPresenter _inst;

    /// <summary>Đang mở chế độ xem gần slide (chặn di chuyển nhân vật).</summary>
    public static bool ZoomOpen => _inst != null && _inst._zoomOpen;

    /// <summary>Đề quiz giảng viên đã chọn trong bảng L (chỉ số trong QuizSessionController.AllQuizzes).</summary>
    public static int SelectedQuiz;

    /// <summary>Bắt đầu / kết thúc trình chiếu (nút trong bảng L của giảng viên).</summary>
    public static void TogglePresenting()
    {
        if (_inst == null) return;
        _inst.FindRefs();
        if (_inst._board == null) return;
        if (_inst._board.Presenting) _inst.Stop(); else _inst.StartPresenting();
    }

    const int ThumbCount = 7;
    const float BarShiftX = 130f; // lệch sang phải để không đè lên khung chat góc dưới trái

    LectureBoardController _board;
    QuizSessionController _quiz;
    float _nextFind;
    bool _wasPresenting, _iStarted, _barHidden, _thumbsOpen, _zoomOpen, _handUp;
    float _presentStart;
    int _lastSlide = -1, _lastCount = -1;
    readonly List<string> _hands = new List<string>();

    TMP_FontAsset _font;
    Canvas _hud, _zoomCanvas;
    RectTransform _startPill, _chip, _studentRow, _bar, _hint, _thumbs, _handsPanel;
    TextMeshProUGUI _chipText, _pageText, _quizText, _handText, _handsTitle, _zoomInfo;
    Button _quizBtn;
    Image _zoomImage;
    readonly List<(Image img, Image ring, TextMeshProUGUI num, Button btn)> _thumbPool = new List<(Image, Image, TextMeshProUGUI, Button)>();
    readonly List<int> _thumbIndex = new List<int>();
    readonly List<(RectTransform root, TextMeshProUGUI name)> _handRows = new List<(RectTransform, TextMeshProUGUI)>();
    readonly List<string> _handRowNames = new List<string>();

    // =========================================================
    void Awake()
    {
        _inst = this;
        _font = UttGlass.Font;
        BuildHud();
        BuildZoom();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unhook();
        if (_inst == this) _inst = null;
    }

    void OnApplicationQuit() => StopIfMine();

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        _screenGo = null; _screenFor = null; _screenTex = null; // đã bị huỷ cùng scene cũ
        Unhook();
        _board = null; _quiz = null; _nextFind = Time.unscaledTime + 0.5f;
        ResetSession();
    }

    void ResetSession()
    {
        _wasPresenting = false; _iStarted = false; _thumbsOpen = false; _handUp = false;
        _hands.Clear();
        _lastSlide = -1; _lastCount = -1;
        CloseZoom();
    }

    void Hook(LectureBoardController b)
    {
        _board = b;
        _board.HandChanged += OnHand;
        _board.LookRequested += OnLookRequested;
    }

    void Unhook()
    {
        if (_board == null) return;
        _board.HandChanged -= OnHand;
        _board.LookRequested -= OnLookRequested;
    }

    void StopIfMine()
    {
        try { if (_iStarted && _board != null && _board.Presenting) _board.SetPresenting(false, ""); } catch { }
    }

    // =========================================================
    void Update()
    {
        FindRefs();
        bool active = _board != null && UttWebView.SceneAllowsFirstPerson;
        bool lecturer = UttSession.IsLecturer && active && _board.CanNavigate;
        bool presenting = active && _board.Presenting;

        if (presenting != _wasPresenting)
        {
            _wasPresenting = presenting;
            if (presenting)
            {
                _presentStart = Time.unscaledTime;
                if (!lecturer) UttChat.Notice("Giảng viên " + _board.PresenterDisplayName + " bắt đầu trình chiếu. Z: xem gần, G: giơ tay.");
            }
            else
            {
                _iStarted = false; _thumbsOpen = false; _handUp = false;
                _hands.Clear();
                CloseZoom();
            }
        }

        HandleKeys(active, lecturer, presenting);
        if (active) UpdateScreen(presenting);

        if (_hud.enabled != active) _hud.enabled = active;
        if (!active) { if (_zoomOpen) CloseZoom(); return; }

        SetActive(_startPill, lecturer && !presenting);
        SetActive(_chip, presenting);
        SetActive(_studentRow, presenting && !lecturer);
        SetActive(_bar, presenting && lecturer && !_barHidden);
        SetActive(_hint, presenting && lecturer && !_barHidden);
        SetActive(_thumbs, presenting && lecturer && !_barHidden && _thumbsOpen);
        SetActive(_handsPanel, presenting && lecturer && _hands.Count > 0);

        if (!presenting) return;

        int count = _board.SlideCount, slide = Mathf.Clamp(_board.DisplaySlideIndex, 0, Mathf.Max(0, count - 1));
        string page = count == 0 ? "Chưa có slide" : $"Trang {slide + 1}/{count}";

        if (lecturer)
        {
            int secs = Mathf.FloorToInt(Time.unscaledTime - _presentStart);
            string lecture = string.IsNullOrEmpty(_board.DisplayLectureName) ? "Bài giảng" : _board.DisplayLectureName;
            _chipText.text = $"<color=#FF6B6B><size=140%>•</size></color>  Đang trình chiếu · {NoParse(lecture)} · {secs / 60:00}:{secs % 60:00}" +
                             (_barHidden ? "  <color=#FFD77A><b>B</b></color> hiện thanh" : "");
            _pageText.text = page;
            UpdateQuizButton();
            if (_thumbsOpen && (slide != _lastSlide || count != _lastCount)) RefreshThumbs(slide, count);
            RefreshHands();
        }
        else
        {
            string who = string.IsNullOrWhiteSpace(_board.PresenterDisplayName) ? "Giảng viên" : _board.PresenterDisplayName;
            _chipText.text = $"<color=#3DDC97><size=140%>•</size></color>  GV {NoParse(who)} đang trình chiếu · {page}";
            _handText.text = _handUp ? "<color=#FFD77A><b>G</b></color>  Hạ tay" : "<color=#FFD77A><b>G</b></color>  Giơ tay";
        }

        if (_zoomOpen)
        {
            var sprite = _board.DisplaySprite;
            if (_zoomImage.sprite != sprite) _zoomImage.sprite = sprite;
            _zoomImage.enabled = sprite != null;
            _zoomInfo.text = page + "  ·  <color=#FFD77A><b>Z</b></color> / <color=#FFD77A><b>Esc</b></color> để đóng" +
                             (lecturer ? "  ·  <color=#FFD77A><b>PgUp</b></color> <color=#FFD77A><b>PgDn</b></color> đổi trang" : "");
        }
        _lastSlide = slide; _lastCount = count;
    }

    void FindRefs()
    {
        bool hasBoard = _board != null && _board;
        if (_board != null && !hasBoard) { Unhook(); _board = null; }
        bool hasQuiz = _quiz != null && _quiz;
        if ((hasBoard && hasQuiz) || Time.unscaledTime < _nextFind) return;
        if (!hasBoard) { var b = FindFirstObjectByType<LectureBoardController>(); if (b != null) Hook(b); }
        if (!hasQuiz) _quiz = FindFirstObjectByType<QuizSessionController>();
        // Scene không có màn chiếu (Campus, Showroom, tàu): dò thưa hơn
        _nextFind = Time.unscaledTime + (_board != null ? 1f : 5f);
    }

    void HandleKeys(bool active, bool lecturer, bool presenting)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null || !active) return;
        if (_zoomOpen && (kb.escapeKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)) { CloseZoom(); return; }
        if (UttChat.IsTyping || UttProfilePanel.IsOpen || UttMinimap.WorldMapOpen || UttClassPanel.IsOpen || UttQuizLive.IsOpen || SceneQuickMenu.IsOpen || OtherFieldFocused()) return;

        if (lecturer && kb.pKey.wasPressedThisFrame) { if (presenting) Stop(); else StartPresenting(); return; }
        if (!presenting) return;

        if (kb.zKey.wasPressedThisFrame) OpenZoom();
        if (lecturer)
        {
            if (kb.pageDownKey.wasPressedThisFrame) _board.NextSlide();
            if (kb.pageUpKey.wasPressedThisFrame) _board.PreviousSlide();
            if (kb.bKey.wasPressedThisFrame) _barHidden = !_barHidden;
        }
        else if (kb.gKey.wasPressedThisFrame) ToggleHand();
#endif
    }

    // =========================================================
    // Màn chiếu 3D: chỉ hiện slide khi đang trình chiếu.
    // Dựng một Canvas trong không gian 3D phủ đúng vị trí màn chiếu (shader UI luôn có trong bản Web, hiện cả 2 mặt),
    // thay cho quad cũ của LectureBoardController (quad cũ được tắt trên bản Web).
    GameObject _screenGo;
    RawImage _screenImg;
    Vector2 _screenSize;
    LectureBoardController _screenFor;
    Texture _screenTex;

    void UpdateScreen(bool presenting)
    {
        if (_board == null) return;
        if (_screenGo == null || _screenFor != _board) BuildScreen();
        if (_screenGo == null) return;
        var quad = _board.boardRenderer;
        if (quad != null && quad.enabled) quad.enabled = false;

        var sprite = presenting ? _board.DisplaySprite : null;
        bool show = sprite != null && sprite.texture != null;
        if (show && !_screenGo.activeSelf) OrientScreen();
        if (_screenGo.activeSelf != show) _screenGo.SetActive(show);
        if (!show || _screenTex == sprite.texture) return;

        _screenTex = sprite.texture;
        _screenImg.texture = sprite.texture;
        var tr = sprite.textureRect;
        var tex = sprite.texture;
        _screenImg.uvRect = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        // Phủ kín toàn bộ màn chiếu 3D (giống bảng chiếu cũ)
        _screenImg.rectTransform.sizeDelta = _screenSize * 100f;
    }

    /// <summary>Đặt màn slide nhô 4 cm về phía người xem và quay mặt chữ về phía họ (không bị ngược chữ).</summary>
    void OrientScreen()
    {
        if (_screenGo == null || _board == null) return;
        Transform t = _board.boardRenderer != null ? _board.boardRenderer.transform : _board.transform;
        Vector3 pos = t.position, n = t.forward;
        var cam = Camera.main;
        bool viewerInFront = cam == null || Vector3.Dot(cam.transform.position - pos, n) < 0f; // quad nhìn được từ phía -forward
        Quaternion rot = viewerInFront ? t.rotation : t.rotation * Quaternion.Euler(0f, 180f, 0f);
        _screenGo.transform.SetPositionAndRotation(pos + n * (viewerInFront ? -0.04f : 0.04f), rot);
    }

    void BuildScreen()
    {
        if (_screenGo != null) Destroy(_screenGo);
        _screenGo = null; _screenTex = null;
        _screenFor = _board;
        if (_board == null) return;
        Transform t = _board.boardRenderer != null ? _board.boardRenderer.transform : _board.transform;

        // Quad của Unity rộng 1 x 1 theo trục X/Y của chính nó
        Vector3 s = t.lossyScale;
        _screenSize = new Vector2(Mathf.Max(0.5f, Mathf.Abs(s.x)), Mathf.Max(0.3f, Mathf.Abs(s.y)));
        if (_board.boardRenderer == null)
        {
            var b = _board.GetComponentInChildren<Renderer>();
            if (b != null) _screenSize = new Vector2(Mathf.Max(b.bounds.size.x, b.bounds.size.z), b.bounds.size.y);
        }

        _screenGo = new GameObject("UttSlideScreen");
        _screenGo.transform.localScale = Vector3.one * 0.01f;
        var canvas = _screenGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)_screenGo.transform;
        rt.sizeDelta = _screenSize * 100f;

        var bg = Fill(NewRect("Black", rt));
        Img(bg, null, Color.black, false);
        var slide = NewRect("Slide", rt);
        Place(slide, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, _screenSize * 100f);
        _screenImg = slide.gameObject.AddComponent<RawImage>();
        _screenImg.raycastTarget = false;
        _screenGo.SetActive(false);
    }

    // =========================================================
    // Hành động
    void StartPresenting()
    {
        if (_board == null || !_board.CanNavigate) return;
        _iStarted = true; _barHidden = false;
        _board.SetPresenting(true, UttSession.DisplayName);
    }

    void Stop()
    {
        if (_board == null) return;
        if (_quiz != null && QuizActive()) _quiz.CloseQuiz();
        _board.SetPresenting(false, "");
    }

    void ToggleHand()
    {
        if (_board == null) return;
        _handUp = !_handUp;
        _board.RaiseHand(UttSession.DisplayName, _handUp);
    }

    void OnHand(string who, bool up)
    {
        if (string.IsNullOrEmpty(who)) return;
        if (who == UttSession.DisplayName && !UttSession.IsLecturer) _handUp = up;
        int i = _hands.IndexOf(who);
        if (up && i < 0)
        {
            _hands.Add(who);
            if (UttSession.IsLecturer) UttChat.Notice(who + " giơ tay.");
        }
        else if (!up && i >= 0) _hands.RemoveAt(i);
    }

    void OnLookRequested()
    {
        if (_board == null) return;
        if (!UttSession.IsLecturer)
        {
            var me = FindLocal();
            if (me != null) me.FaceTowards(_board.BoardCenter);
        }
        UttChat.Notice("Giảng viên gọi cả lớp nhìn lên màn chiếu.");
    }

    bool QuizActive()
    {
        try { return _quiz != null && _quiz.Object != null && _quiz.Object.IsValid && _quiz.IsQuizActive; }
        catch { return false; }
    }

    void ToggleQuiz()
    {
        if (_quiz == null || _quiz.Object == null || !_quiz.Object.IsValid) return;
        if (QuizActive()) _quiz.CloseQuiz(); else _quiz.OpenQuiz(SelectedQuiz);
    }

    void UpdateQuizButton()
    {
        bool ok = _quiz != null && _quiz.Object != null && _quiz.Object.IsValid &&
                  (QuizSessionController.AllQuizzes.Length > 0 || _quiz.quizConfig != null);
        _quizBtn.interactable = ok;
        if (!ok) { _quizText.text = "Chưa có Quiz"; return; }
        _quizText.text = QuizActive() ? $"Đóng Quiz ({_quiz.SubmittedCount} đã nộp)" : "Mở Quiz Live";
    }

    void OpenZoom()
    {
        if (_board == null || _board.DisplaySprite == null) { UttChat.Notice("Chưa có slide để xem gần."); return; }
        EnsureEventSystem();
        _zoomOpen = true;
        _zoomCanvas.enabled = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void CloseZoom()
    {
        _zoomOpen = false;
        if (_zoomCanvas != null) _zoomCanvas.enabled = false;
    }

    // =========================================================
    // Ảnh các trang
    void ToggleThumbs()
    {
        _thumbsOpen = !_thumbsOpen;
        _lastSlide = -1;
    }

    void RefreshThumbs(int slide, int count)
    {
        int n = Mathf.Min(ThumbCount, count);
        int start = Mathf.Clamp(slide - ThumbCount / 2, 0, Mathf.Max(0, count - ThumbCount));
        float w = n * 120f + Mathf.Max(0, n - 1) * 10f + 20f;
        _thumbs.sizeDelta = new Vector2(Mathf.Max(w, 200f), 92f);
        _thumbIndex.Clear();
        for (int i = 0; i < _thumbPool.Count; i++)
        {
            var t = _thumbPool[i];
            bool on = i < n;
            t.btn.gameObject.SetActive(on);
            _thumbIndex.Add(on ? start + i : -1);
            if (!on) continue;
            int idx = start + i;
            ((RectTransform)t.btn.transform).anchoredPosition = new Vector2(10f + i * 130f, 0f);
            t.img.sprite = _board.GetSlide(idx);
            t.img.enabled = t.img.sprite != null;
            t.ring.color = idx == slide ? UttGlass.Accent : UttGlass.SoftRing;
            t.num.text = (idx + 1).ToString();
        }
    }

    void PickThumb(int poolIndex)
    {
        if (_board == null || poolIndex < 0 || poolIndex >= _thumbIndex.Count) return;
        int idx = _thumbIndex[poolIndex];
        if (idx >= 0) _board.GoToSlide(idx);
    }

    // =========================================================
    // Học viên giơ tay
    void RefreshHands()
    {
        int n = Mathf.Min(_hands.Count, 6);
        _handsTitle.text = $"Học viên giơ tay ({_hands.Count})";
        _handsPanel.sizeDelta = new Vector2(300f, 60f + n * 50f);
        while (_handRows.Count < n) MakeHandRow(_handRows.Count);
        _handRowNames.Clear();
        for (int i = 0; i < _handRows.Count; i++)
        {
            bool on = i < n;
            SetActive(_handRows[i].root, on);
            _handRowNames.Add(on ? _hands[i] : null);
            if (on) _handRows[i].name.text = NoParse(_hands[i]);
        }
    }

    void MakeHandRow(int i)
    {
        var row = Box(_handsPanel, 16f, -52f - i * 50f, 268f, 42f);
        Img(row, UttGlass.Round14, new Color(1f, 1f, 1f, 0.09f), true);
        var name = Label(Box(row, 14f, 0f, 160f, 42f), "", 16f, Color.white, TextAlignmentOptions.Left, FontStyles.Normal);
        name.overflowMode = TextOverflowModes.Ellipsis;
        int idx = i;
        MakeButton(Box(row, 180f, -5f, 82f, 32f), "Hạ tay", false, 14f, () =>
        {
            if (_board != null && idx < _handRowNames.Count && _handRowNames[idx] != null) _board.RaiseHand(_handRowNames[idx], false);
        });
        _handRows.Add((row, name));
    }

    // =========================================================
    // Dựng giao diện
    void BuildHud()
    {
        var go = new GameObject("UttPresenterCanvas");
        go.transform.SetParent(transform, false);
        _hud = go.AddComponent<Canvas>();
        _hud.renderMode = RenderMode.ScreenSpaceOverlay;
        _hud.sortingOrder = 4992;
        AddScaler(go);
        go.AddComponent<GraphicRaycaster>();
        _hud.enabled = false;
        var root = (RectTransform)go.transform;

        // Giảng viên, chưa trình chiếu
        _startPill = NewRect("StartPill", root);
        Place(_startPill, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(330f, 48f));
        Shadow(_startPill, 0.3f);
        MakeButton(Fill(NewRect("Btn", _startPill)), "Bắt đầu trình chiếu  <color=#26265F80>(P)</color>", true, 17f, StartPresenting);

        // Chip trạng thái
        _chip = NewRect("Chip", root);
        Place(_chip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(720f, 48f));
        Shadow(_chip, 0.3f);
        Img(_chip, UttGlass.Pill, UttGlass.Fill, true);
        Img(Fill(NewRect("Ring", _chip)), UttGlass.PillRing, UttGlass.Border, true);
        _chipText = Label(Box(_chip, 24f, 0f, 672f, 48f), "", 17f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
        _chipText.overflowMode = TextOverflowModes.Ellipsis;

        // Học viên: Z / G
        _studentRow = NewRect("StudentRow", root);
        Place(_studentRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(380f, 44f));
        MakeButton(Box(_studentRow, 0f, 0f, 185f, 44f), "<color=#FFD77A><b>Z</b></color>  Xem gần slide", false, 15f, OpenZoom);
        var hand = MakeButton(Box(_studentRow, 195f, 0f, 185f, 44f), "", false, 15f, ToggleHand);
        _handText = hand.GetComponentInChildren<TextMeshProUGUI>();

        // Giảng viên: thanh điều khiển
        const float barW = 1064f;
        _bar = NewRect("Bar", root);
        Place(_bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(BarShiftX, 28f), new Vector2(barW, 72f));
        Shadow(_bar, 0.4f);
        var barImg = Img(_bar, UttGlass.Pill, UttGlass.Fill, true);
        barImg.raycastTarget = true;
        Img(Fill(NewRect("Ring", _bar)), UttGlass.PillRing, UttGlass.Border, true);

        BarButton(12f, 48f, "‹", false, () => { if (_board != null) _board.PreviousSlide(); }, 24f);
        _pageText = Label(BarBox(66f, 134f), "", 17f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        BarButton(206f, 48f, "›", false, () => { if (_board != null) _board.NextSlide(); }, 24f);
        var sep = BarBox(266f, 1.5f); sep.sizeDelta = new Vector2(1.5f, 32f);
        Img(sep, null, new Color(1f, 1f, 1f, 0.2f), false);
        BarButton(282f, 132f, "Ảnh trang", false, ToggleThumbs, 15f);
        BarButton(424f, 236f, "Gọi cả lớp nhìn bảng", false, () => { if (_board != null) _board.CallClassToBoard(); }, 15f);
        _quizBtn = BarButton(670f, 236f, "Mở Quiz Live", true, ToggleQuiz, 15f);
        _quizText = _quizBtn.GetComponentInChildren<TextMeshProUGUI>();
        BarButton(916f, 136f, "Kết thúc", false, Stop, 15f);

        _hint = NewRect("Hint", root);
        Place(_hint, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(BarShiftX, 106f), new Vector2(barW, 24f));
        Label(_hint, "<color=#FFD77A><b>PgUp</b></color> <color=#FFD77A><b>PgDn</b></color> đổi trang  ·  <color=#FFD77A><b>B</b></color> ẩn thanh  ·  " +
                     "<color=#FFD77A><b>Z</b></color> xem gần  ·  giữ <color=#FFD77A><b>Alt</b></color> để bấm nút", 14f, UttGlass.Muted, TextAlignmentOptions.Center, FontStyles.Normal);

        // Ảnh các trang
        _thumbs = NewRect("Thumbs", root);
        Place(_thumbs, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(BarShiftX, 140f), new Vector2(940f, 92f));
        var thumbBg = Img(_thumbs, UttGlass.Round18, UttGlass.FillLight, true);
        thumbBg.raycastTarget = true;
        Img(Fill(NewRect("Ring", _thumbs)), UttGlass.Ring18, UttGlass.Border, true);
        var thumbRing = UttGlass.Rounded(14, 3f, 1f);
        for (int i = 0; i < ThumbCount; i++)
        {
            var r = NewRect("Thumb" + i, _thumbs);
            Place(r, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f + i * 130f, 0f), new Vector2(120f, 68f));
            var bg = Img(r, UttGlass.Round14, new Color(1f, 1f, 1f, 0.92f), true);
            bg.raycastTarget = true;
            var pic = Fill(NewRect("Pic", r)); pic.offsetMin = new Vector2(3f, 3f); pic.offsetMax = new Vector2(-3f, -3f);
            var img = Img(pic, null, Color.white, false);
            img.preserveAspect = true;
            var ring = Img(Fill(NewRect("Ring", r)), thumbRing, UttGlass.SoftRing, true);
            var numBox = NewRect("Num", r);
            Place(numBox, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(4f, 4f), new Vector2(28f, 20f));
            Img(numBox, UttGlass.Pill, UttGlass.Fill, true);
            var num = Label(numBox, "", 12f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var btn = r.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            int pool = i;
            btn.onClick.AddListener(() => PickThumb(pool));
            _thumbPool.Add((img, ring, num, btn));
        }

        // Học viên giơ tay (dưới minimap)
        _handsPanel = NewRect("Hands", root);
        Place(_handsPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -300f), new Vector2(300f, 60f));
        Shadow(_handsPanel, 0.3f);
        var hp = Img(_handsPanel, UttGlass.Round18, UttGlass.Fill, true);
        hp.raycastTarget = true;
        Img(Fill(NewRect("Ring", _handsPanel)), UttGlass.Ring18, UttGlass.Border, true);
        _handsTitle = Label(Box(_handsPanel, 18f, -10f, 264f, 32f), "", 16f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        SetActive(_startPill, false); SetActive(_chip, false); SetActive(_studentRow, false); SetActive(_bar, false);
        SetActive(_hint, false); SetActive(_thumbs, false); SetActive(_handsPanel, false);
    }

    RectTransform BarBox(float x, float w)
    {
        var r = NewRect("Item", _bar);
        Place(r, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(w, 48f));
        return r;
    }

    Button BarButton(float x, float w, string text, bool primary, UnityEngine.Events.UnityAction onClick, float size) =>
        MakeButton(BarBox(x, w), text, primary, size, onClick);

    void BuildZoom()
    {
        var go = new GameObject("UttPresenterZoomCanvas");
        go.transform.SetParent(transform, false);
        _zoomCanvas = go.AddComponent<Canvas>();
        _zoomCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _zoomCanvas.sortingOrder = 5040;
        AddScaler(go);
        go.AddComponent<GraphicRaycaster>();
        _zoomCanvas.enabled = false;
        var root = (RectTransform)go.transform;

        var dim = Fill(NewRect("Dim", root));
        var dimImg = Img(dim, null, new Color(0.04f, 0.04f, 0.12f, 0.86f), false);
        dimImg.raycastTarget = true;
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(CloseZoom);

        var frame = NewRect("Slide", root);
        Place(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(1640f, 922f));
        _zoomImage = Img(frame, null, Color.white, false);
        _zoomImage.preserveAspect = true;

        var info = NewRect("Info", root);
        Place(info, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(760f, 44f));
        Img(info, UttGlass.Pill, UttGlass.Fill, true);
        Img(Fill(NewRect("Ring", info)), UttGlass.PillRing, UttGlass.Border, true);
        _zoomInfo = Label(info, "", 16f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
    }

    // =========================================================
    // Tiện ích
    FirstPersonController _local;
    FirstPersonController FindLocal()
    {
        if (_local != null && _local.isActiveAndEnabled && _local.Object != null && _local.Object.IsValid) return _local;
        _local = null;
        foreach (var p in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            if (p != null && p.Object != null && p.Object.IsValid && p.HasStateAuthority) { _local = p; break; }
        return _local;
    }

    static void SetActive(RectTransform r, bool on) { if (r != null && r.gameObject.activeSelf != on) r.gameObject.SetActive(on); }

    static string NoParse(string text)
    {
        text = (text ?? "").Replace("</noparse>", "</ noparse>").Replace("</NOPARSE>", "</ NOPARSE>");
        return "<noparse>" + text + "</noparse>";
    }

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

    static RectTransform Fill(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }

    static RectTransform Box(RectTransform parent, float x, float y, float w, float h)
    {
        var r = NewRect("Box", parent);
        Place(r, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, h));
        return r;
    }

    static void Shadow(RectTransform parent, float alpha)
    {
        var s = NewRect("Shadow", parent);
        s.anchorMin = Vector2.zero; s.anchorMax = Vector2.one;
        s.offsetMin = new Vector2(-14f, -18f); s.offsetMax = new Vector2(14f, 10f);
        Img(s, UttGlass.Shadow, new Color(0f, 0f, 0f, alpha), true);
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

    Button MakeButton(RectTransform r, string text, bool primary, float size, UnityEngine.Events.UnityAction onClick)
    {
        var img = Img(r, UttGlass.Pill, Color.white, true);
        img.raycastTarget = true;
        var btn = r.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        Label(r, text, size, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        if (primary) UttGlass.PrimaryButton(btn); else UttGlass.GlassButton(btn);
        btn.onClick.AddListener(onClick);
        return btn;
    }

    static bool OtherFieldFocused()
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
