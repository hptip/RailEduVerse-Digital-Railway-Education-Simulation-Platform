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
/// Bảng lớp học bấm L (hoặc Tab) của bản Web, theo thiết kế "Quản lý bài giảng" (bộ UI Liquid Glass):
/// thanh điều hướng bên trái, bảng ở giữa, khung chi tiết bên phải.
/// Thay cho LecturePanel / StudentSlidePanel cũ của bản VR (DashboardManager chuyển sang đây khi chạy WebGL).
///  - Giảng viên: mục "Bài giảng" (chọn bài, xem trước, đổi trang, trình chiếu trong lớp) và "Kết quả học viên".
///  - Học viên: chỉ mục "Bài giảng", xem slide đang chiếu; không đổi được bài hay trang.
/// Quy trình giảng viên: chọn bài giảng trong bảng → chọn đề quiz ở khung bên phải → bấm "Trình chiếu".
/// Khi đang chiếu, nút "Mở Quiz Live" trên thanh trình chiếu mở đúng đề đã chọn.
/// L / Tab mở, Esc, L hoặc bấm ra ngoài để đóng.
/// </summary>
[DefaultExecutionOrder(-455)]
public sealed class UttClassPanel : MonoBehaviour
{
    static UttClassPanel _inst;
    public static bool IsOpen => _inst != null && _inst._open;

    /// <summary>Gọi từ DashboardManager khi bấm L/Tab trên bản Web.</summary>
    public static void Toggle()
    {
        if (_inst == null) return;
        if (_inst._open) _inst.Close(); else _inst.Open();
    }

    const float W = 1640f, H = 880f, RowH = 56f, RowGap = 6f;
    const float MainX = 320f, MainW = 1300f;

    static readonly Color CardFill = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color CardRing = new Color(1f, 1f, 1f, 0.18f);

    TMP_FontAsset _font;
    Canvas _canvas;
    CanvasGroup _group;
    RectTransform _panel, _lectureView, _resultView, _lectureContent, _resultContent;
    TextMeshProUGUI _title, _sub, _roleLine, _classInfo, _page, _presentText, _detailName, _detailMeta, _note, _resultStatus;
    readonly TextMeshProUGUI[] _tileValues = new TextMeshProUGUI[3];
    Button _navLecture, _navResult, _prev, _next, _present, _reload;
    Image _navLectureBg, _navResultBg, _navLectureRing, _navResultRing, _preview;
    bool _open, _onResults, _loading;
    float _anim, _nextFind, _nextCount;
    int _builtLectureCount = -1;
    LectureBoardController _board;

    class LectureRow
    {
        public Button btn; public Image bg, ring, chip; public TextMeshProUGUI num, name, state;
    }
    readonly List<LectureRow> _lectureRows = new List<LectureRow>();
    readonly List<RectTransform> _resultRows = new List<RectTransform>();

    class QuizRow { public Button btn; public Image bg, ring; public TextMeshProUGUI name, info; }
    readonly List<QuizRow> _quizRows = new List<QuizRow>();
    RectTransform _quizSection;
    TextMeshProUGUI _quizEmpty;
    bool _loggedError;

    // =========================================================
    void Awake()
    {
        _inst = this;
        _font = UttGlass.Font;
        Build();
        SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) { _board = null; _builtLectureCount = -1; if (_open) Close(); } };
    }

    void OnDisable() => _open = false;

    void Open()
    {
        FindBoard();
        if (_board == null) { UttChat.Notice("Khu vực này không có màn chiếu bài giảng."); return; }
        EnsureEventSystem();
        _open = true;
        _canvas.enabled = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        bool lecturer = UttSession.IsLecturer;
        _navResult.gameObject.SetActive(lecturer);
        _roleLine.text = lecturer ? "Bảng giảng viên" : "Học viên";
        _nextCount = 0f;
        ShowView(false);
    }

    void Close()
    {
        _open = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void FindBoard()
    {
        if (_board != null && _board) return;
        if (_board == null && Time.unscaledTime < _nextFind) return;
        _nextFind = Time.unscaledTime + 1f;
        _board = FindFirstObjectByType<LectureBoardController>();
        if (_board == null) _board = FindFirstObjectByType<LectureBoardController>(FindObjectsInactive.Include);
        _builtLectureCount = -1;
    }

    void ShowView(bool results)
    {
        _onResults = results && UttSession.IsLecturer;
        _lectureView.gameObject.SetActive(!_onResults);
        _resultView.gameObject.SetActive(_onResults);
        _reload.gameObject.SetActive(_onResults);
        StyleNav(_navLectureBg, _navLectureRing, _navLecture, !_onResults);
        StyleNav(_navResultBg, _navResultRing, _navResult, _onResults);
        _title.text = _onResults ? "Kết quả học viên" : "Bài giảng";
        if (_onResults) { _sub.text = "Điểm Quiz đã lưu trên máy chủ"; LoadResults(); }
    }

    static void StyleNav(Image bg, Image ring, Button b, bool on)
    {
        bg.color = on ? new Color(UttGlass.Accent.r, UttGlass.Accent.g, UttGlass.Accent.b, 0.22f) : new Color(1f, 1f, 1f, 0f);
        ring.color = on ? UttGlass.Accent : new Color(1f, 1f, 1f, 0f);
        var t = b.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null) t.color = on ? Color.white : UttGlass.Muted;
    }

    // =========================================================
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (_open && kb.escapeKey.wasPressedThisFrame) Close();
            else if ((kb.lKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame) && CanToggle())
            {
                if (_open) Close(); else Open();
            }
        }
#endif
        if (_open)
        {
            try
            {
                FindBoard();
                if (_board == null || !UttWebView.SceneAllowsFirstPerson) Close();
                else
                {
                    if (!_onResults) RefreshLecture();
                    if (Time.unscaledTime >= _nextCount) { _nextCount = Time.unscaledTime + 1f; RefreshClassInfo(); }
                }
            }
            catch (System.Exception e)
            {
                if (!_loggedError) { _loggedError = true; Debug.LogError("[UttClassPanel] " + e); }
            }
        }

        float target = _open ? 1f : 0f;
        if (!Mathf.Approximately(_anim, target))
        {
            _anim = Mathf.MoveTowards(_anim, target, Time.unscaledDeltaTime / 0.16f);
            float t = 1f - Mathf.Pow(1f - _anim, 3f);
            _group.alpha = t;
            _panel.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, t);
            if (_anim <= 0f) _canvas.enabled = false;
        }
    }

    static bool CanToggle()
    {
        if (UttChat.IsTyping || UttProfilePanel.IsOpen || UttMinimap.WorldMapOpen || SceneQuickMenu.IsOpen || UttQuizLive.IsOpen) return false;
        var es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject == null) return true;
        var tmp = es.currentSelectedGameObject.GetComponent<TMP_InputField>();
        if (tmp != null && tmp.isFocused) return false;
        var legacy = es.currentSelectedGameObject.GetComponent<InputField>();
        return legacy == null || !legacy.isFocused;
    }

    void RefreshClassInfo()
    {
        int students = 0, lecturers = 0;
        foreach (var p in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
        {
            if (p == null || p.Object == null || !p.Object.IsValid) continue;
            bool lec;
            try { lec = p.NetworkPlayerRole.ToString() == "Giảng viên"; } catch { lec = false; }
            if (lec) lecturers++; else students++;
        }
        _classInfo.text = $"Lớp đang mở: <b>Classroom</b>\n{students} học viên · {lecturers} giảng viên trực tuyến";
    }

    void RefreshLecture()
    {
        bool canNav = _board.CanNavigate && UttSession.IsLecturer;
        int count = _board.LectureCount, current = _board.DisplayLectureIndex;
        if (count != _builtLectureCount) BuildLectureRows(count);

        for (int i = 0; i < _lectureRows.Count; i++)
        {
            var r = _lectureRows[i];
            bool on = i < count;
            if (r.btn.gameObject.activeSelf != on) r.btn.gameObject.SetActive(on);
            if (!on) continue;
            bool here = i == current;
            bool live = here && _board.Presenting;
            r.ring.color = here ? UttGlass.Accent : new Color(1f, 1f, 1f, 0f);
            r.bg.color = here ? new Color(UttGlass.Accent.r, UttGlass.Accent.g, UttGlass.Accent.b, 0.16f) : new Color(1f, 1f, 1f, i % 2 == 0 ? 0.04f : 0f);
            if (r.chip.gameObject.activeSelf != here) r.chip.gameObject.SetActive(here);
            r.chip.color = live ? new Color(1f, 0.42f, 0.42f, 0.22f) : new Color(UttGlass.Green.r, UttGlass.Green.g, UttGlass.Green.b, 0.22f);
            r.state.color = live ? new Color(1f, 0.66f, 0.66f) : UttGlass.Green;
            r.state.text = live ? "Đang trình chiếu" : "Đang mở";
            r.btn.interactable = canNav;
        }

        var sprite = _board.DisplaySprite;
        if (_preview.sprite != sprite) _preview.sprite = sprite;
        _preview.enabled = sprite != null;
        int slides = _board.SlideCount;
        _page.text = slides == 0 ? "Chưa có slide" : $"Trang {Mathf.Clamp(_board.DisplaySlideIndex + 1, 1, slides)} / {slides}";
        string name = count == 0 ? "Chưa có bài giảng" : _board.DisplayLectureName;
        _detailName.text = NoParse(name);
        _detailMeta.text = count == 0 ? "Thêm bài vào ClassroomConfig" : $"Bài {current + 1} / {count} · {slides} trang";
        _sub.text = count == 0 ? "Chưa có bài giảng nào" : $"{count} bài giảng · đang mở: {NoParse(name)}";

        _prev.gameObject.SetActive(canNav);
        _next.gameObject.SetActive(canNav);
        _present.gameObject.SetActive(canNav);
        _quizSection.gameObject.SetActive(canNav);
        _note.gameObject.SetActive(!canNav);
        if (canNav) RefreshQuizzes();
        _presentText.text = _board.Presenting ? "Kết thúc trình chiếu" : "Trình chiếu  <color=#26265F80>(P)</color>";
        _note.text = "Đây là slide giảng viên đang chiếu. Chỉ giảng viên đổi được bài và trang.\n<color=#FFD77A><b>Z</b></color> xem gần · <color=#FFD77A><b>L</b></color> / <color=#FFD77A><b>Esc</b></color> đóng.";
    }

    void BuildLectureRows(int count)
    {
        _builtLectureCount = count;
        while (_lectureRows.Count < count)
        {
            int idx = _lectureRows.Count;
            var row = Row(_lectureContent, idx);
            var r = new LectureRow();
            r.bg = Img(row, UttGlass.Round14, Color.clear, true);
            r.bg.raycastTarget = true;
            r.ring = Img(Fill(NewRect("Ring", row)), UttGlass.Ring14, Color.clear, true);
            r.num = Label(Col(row, 0f, 0.08f), (idx + 1).ToString("00"), 15f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Bold);
            r.name = Label(Col(row, 0.08f, 0.72f), "", 16f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            r.name.overflowMode = TextOverflowModes.Ellipsis;
            var chipBox = Col(row, 0.72f, 1f);
            var chipRt = NewRect("Chip", chipBox);
            Place(chipRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(170f, 28f));
            r.chip = Img(chipRt, UttGlass.Pill, Color.clear, true);
            r.state = Label(chipRt, "", 13f, UttGlass.Green, TextAlignmentOptions.Center, FontStyles.Bold);
            r.btn = row.gameObject.AddComponent<Button>();
            r.btn.targetGraphic = r.bg;
            r.btn.transition = Selectable.Transition.None;
            r.btn.onClick.AddListener(() => { if (_board != null) _board.SelectLecture(idx); });
            _lectureRows.Add(r);
        }
        for (int i = 0; i < count && i < _lectureRows.Count; i++)
            _lectureRows[i].name.text = NoParse(_board.classroomConfig.availableLectures[i]);
        _lectureContent.sizeDelta = new Vector2(0f, Mathf.Max(0, count) * (RowH + RowGap));
    }

    void RefreshQuizzes()
    {
        var all = QuizSessionController.AllQuizzes;
        int n = Mathf.Min(all.Length, _quizRows.Count);
        if (all.Length > 0) UttPresenter.SelectedQuiz = Mathf.Clamp(UttPresenter.SelectedQuiz, 0, all.Length - 1);
        _quizEmpty.gameObject.SetActive(all.Length == 0);
        for (int i = 0; i < _quizRows.Count; i++)
        {
            var r = _quizRows[i];
            bool on = i < n;
            if (r.btn.gameObject.activeSelf != on) r.btn.gameObject.SetActive(on);
            if (!on) continue;
            var q = all[i];
            bool sel = i == UttPresenter.SelectedQuiz;
            r.name.text = NoParse(string.IsNullOrWhiteSpace(q.quizName) ? q.name : q.quizName);
            r.info.text = (q.questions != null ? q.questions.Count : 0) + " câu";
            r.bg.color = sel ? new Color(UttGlass.Accent.r, UttGlass.Accent.g, UttGlass.Accent.b, 0.2f) : new Color(1f, 1f, 1f, 0.06f);
            r.ring.color = sel ? UttGlass.Accent : new Color(1f, 1f, 1f, 0.16f);
        }
    }

    // =========================================================
    // Kết quả học viên
    async void LoadResults()
    {
        if (_loading) return;
        foreach (var r in _resultRows) Destroy(r.gameObject);
        _resultRows.Clear();
        SetTiles("—", "—", "—");
        if (DataBridge.Instance == null) { _resultStatus.text = "Không kết nối được máy chủ (DataBridge chưa có trong scene)."; return; }
        _loading = true;
        _resultStatus.text = "Đang tải…";
        List<DataBridge.StudentScoreRecord> list = null;
        try { list = await DataBridge.Instance.GetStudentRecordsAsync(); }
        catch (System.Exception e) { Debug.LogWarning("[UttClassPanel] " + e.Message); }
        if (_resultStatus == null) return; // panel đã bị huỷ (đổi scene) trong lúc chờ máy chủ
        _loading = false;
        if (this == null || !_onResults) return;
        if (list == null) { _resultStatus.text = "Không tải được danh sách. Kiểm tra backend (Backend/server.py)."; return; }

        int done = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i];
            bool none = string.IsNullOrWhiteSpace(s.score) || s.score.Contains("Chưa");
            if (!none) done++;
            var row = Row(_resultContent, i);
            Img(row, UttGlass.Round14, new Color(1f, 1f, 1f, i % 2 == 0 ? 0.05f : 0f), true);
            Label(Col(row, 0f, 0.5f), NoParse(s.full_name), 16f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold).overflowMode = TextOverflowModes.Ellipsis;
            Label(Col(row, 0.5f, 0.75f), NoParse(s.student_id), 15f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Normal);
            var chipBox = Col(row, 0.75f, 1f);
            var chip = NewRect("Chip", chipBox);
            Place(chip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(150f, 28f));
            Img(chip, UttGlass.Pill, none ? new Color(1f, 1f, 1f, 0.12f) : new Color(UttGlass.Green.r, UttGlass.Green.g, UttGlass.Green.b, 0.22f), true);
            Label(chip, none ? "Chưa làm bài" : NoParse(s.score), 13f, none ? Color.white : UttGlass.Green, TextAlignmentOptions.Center, FontStyles.Bold);
            _resultRows.Add(row);
        }
        _resultContent.sizeDelta = new Vector2(0f, list.Count * (RowH + RowGap));
        SetTiles(list.Count.ToString(), done.ToString(), (list.Count - done).ToString());
        string err = DataBridge.Instance != null ? DataBridge.Instance.LastStudentRecordsError : "";
        _resultStatus.text = !string.IsNullOrEmpty(err) ? "<color=#FFD77A>" + err + "</color>" : list.Count == 0 ? "Chưa có học viên nào." : "";
        _resultStatus.textWrappingMode = TextWrappingModes.Normal;
    }

    void SetTiles(string a, string b, string c) { _tileValues[0].text = a; _tileValues[1].text = b; _tileValues[2].text = c; }

    // =========================================================
    // Dựng giao diện
    void Build()
    {
        var go = new GameObject("UttClassPanelCanvas");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5045;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        _group = go.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _canvas.enabled = false;
        var root = (RectTransform)go.transform;

        var dim = Fill(NewRect("Dim", root));
        Img(dim, null, new Color(0.05f, 0.05f, 0.14f, 0.55f), false).raycastTarget = true;
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        _panel = NewRect("Panel", root);
        Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
        var shadow = NewRect("Shadow", _panel);
        shadow.anchorMin = Vector2.zero; shadow.anchorMax = Vector2.one;
        shadow.offsetMin = new Vector2(-24f, -24f); shadow.offsetMax = new Vector2(24f, 24f);
        Img(shadow, UttGlass.Shadow, new Color(0f, 0f, 0f, 0.45f), true);
        var panelImg = Img(_panel, UttGlass.Round28, UttGlass.Fill, true);
        panelImg.raycastTarget = true;
        UttGlass.Panel(panelImg);

        BuildSidebar();

        _title = Label(Box(_panel, MainX, -26f, 700f, 42f), "Bài giảng", 30f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        _sub = Label(Box(_panel, MainX, -70f, 800f, 24f), "", 15f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Normal);
        _sub.overflowMode = TextOverflowModes.Ellipsis;
        _reload = MakeButton(Box(_panel, W - 270f, -30f, 120f, 44f), "Tải lại", false, 15f, LoadResults);
        MakeButton(Box(_panel, W - 130f, -30f, 100f, 44f), "Đóng", false, 15f, Close);

        BuildLectureView();
        BuildResultView();
    }

    void BuildSidebar()
    {
        var side = Box(_panel, 20f, -20f, 280f, H - 40f);
        Img(side, UttGlass.Round18, CardFill, true);
        Img(Fill(NewRect("Ring", side)), UttGlass.Ring18, CardRing, true);

        var logoBox = Box(side, 20f, -24f, 72f, 36f);
        var logo = logoBox.gameObject.AddComponent<RawImage>();
        logo.texture = UttGlass.Logo;
        logo.raycastTarget = false;
        logo.enabled = logo.texture != null;
        Label(Box(side, 104f, -20f, 160f, 24f), "RailEduVerse", 18f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        _roleLine = Label(Box(side, 104f, -44f, 160f, 20f), "", 13f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Normal);

        _navLecture = NavButton(side, -96f, "Bài giảng", () => ShowView(false), out _navLectureBg, out _navLectureRing);
        _navResult = NavButton(side, -152f, "Kết quả học viên", () => ShowView(true), out _navResultBg, out _navResultRing);

        var info = Box(side, 16f, -(H - 40f - 16f - 92f), 248f, 92f);
        Img(info, UttGlass.Round14, new Color(UttGlass.Accent.r, UttGlass.Accent.g, UttGlass.Accent.b, 0.14f), true);
        Img(Fill(NewRect("Ring", info)), UttGlass.Ring14, new Color(UttGlass.Accent.r, UttGlass.Accent.g, UttGlass.Accent.b, 0.4f), true);
        var pad = Fill(NewRect("Pad", info)); pad.offsetMin = new Vector2(14f, 10f); pad.offsetMax = new Vector2(-14f, -10f);
        _classInfo = Label(pad, "", 14f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        _classInfo.textWrappingMode = TextWrappingModes.Normal;
        _classInfo.lineSpacing = 8f;
    }

    Button NavButton(RectTransform side, float y, string text, UnityEngine.Events.UnityAction onClick, out Image bg, out Image ring)
    {
        var r = Box(side, 16f, y, 248f, 48f);
        bg = Img(r, UttGlass.Round14, Color.clear, true);
        bg.raycastTarget = true;
        ring = Img(Fill(NewRect("Ring", r)), UttGlass.Ring14, Color.clear, true);
        var pad = Fill(NewRect("Pad", r)); pad.offsetMin = new Vector2(18f, 0f);
        Label(pad, text, 16f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        return b;
    }

    void BuildLectureView()
    {
        _lectureView = Fill(NewRect("LectureView", _panel));
        const float tableW = 880f, top = -116f, h = H - 116f - 20f;

        var table = Box(_lectureView, MainX, top, tableW, h);
        Img(table, UttGlass.Round18, CardFill, true);
        Img(Fill(NewRect("Ring", table)), UttGlass.Ring18, CardRing, true);
        var head = Box(table, 12f, -14f, tableW - 24f, 26f);
        Label(Col(head, 0f, 0.08f), "#", 13f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Bold);
        Label(Col(head, 0.08f, 0.72f), "BÀI GIẢNG", 13f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Bold);
        Label(Col(head, 0.72f, 1f), "TRẠNG THÁI", 13f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Bold);
        _lectureContent = Scroll(table, 12f, -48f, tableW - 24f, h - 60f);

        float ax = MainX + tableW + 20f, aw = MainW - tableW - 20f, iw = aw - 40f;
        var aside = Box(_lectureView, ax, top, aw, h);
        Img(aside, UttGlass.Round18, CardFill, true);
        Img(Fill(NewRect("Ring", aside)), UttGlass.Ring18, CardRing, true);

        var frame = Box(aside, 20f, -20f, iw, iw * 9f / 16f);
        Img(frame, UttGlass.Round14, new Color(0f, 0f, 0f, 0.35f), true);
        var pic = Fill(NewRect("Slide", frame)); pic.offsetMin = new Vector2(6f, 6f); pic.offsetMax = new Vector2(-6f, -6f);
        _preview = Img(pic, null, Color.white, false);
        _preview.preserveAspect = true;
        Img(Fill(NewRect("Ring", frame)), UttGlass.Ring14, CardRing, true);

        float y = -20f - iw * 9f / 16f - 18f;
        _detailName = Label(Box(aside, 20f, y, iw, 56f), "", 20f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        _detailName.textWrappingMode = TextWrappingModes.Normal;
        _detailName.overflowMode = TextOverflowModes.Ellipsis;
        _detailMeta = Label(Box(aside, 20f, y - 60f, iw, 22f), "", 14f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Normal);

        y -= 100f;
        _prev = MakeButton(Box(aside, 20f, y, 52f, 48f), "‹", false, 24f, () => { if (_board != null) _board.PreviousSlide(); });
        _page = Label(Box(aside, 80f, y, iw - 120f, 48f), "", 17f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        _next = MakeButton(Box(aside, 20f + iw - 52f, y, 52f, 48f), "›", false, 24f, () => { if (_board != null) _board.NextSlide(); });

        _note = Label(Box(aside, 20f, y - 70f, iw, 80f), "", 14f, UttGlass.Muted, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        _note.textWrappingMode = TextWrappingModes.Normal;

        // Chọn đề quiz
        _quizSection = Box(aside, 20f, y - 66f, iw, 230f);
        Label(Box(_quizSection, 0f, 0f, iw, 22f), "QUIZ ĐI KÈM", 13f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Bold);
        _quizEmpty = Label(Box(_quizSection, 0f, -30f, iw, 44f), "Chưa có đề. Thêm file QuizSO vào Assets/Resources/Quizzes.", 13f, UttGlass.Muted, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        _quizEmpty.textWrappingMode = TextWrappingModes.Normal;
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var rr = Box(_quizSection, 0f, -30f - i * 50f, iw, 44f);
            var q = new QuizRow();
            q.bg = Img(rr, UttGlass.Round14, Color.clear, true);
            q.bg.raycastTarget = true;
            q.ring = Img(Fill(NewRect("Ring", rr)), UttGlass.Ring14, Color.clear, true);
            var padL = Fill(NewRect("Pad", rr)); padL.offsetMin = new Vector2(16f, 0f); padL.offsetMax = new Vector2(-80f, 0f);
            q.name = Label(padL, "", 15f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            q.name.overflowMode = TextOverflowModes.Ellipsis;
            var padR = Fill(NewRect("Pad", rr)); padR.offsetMax = new Vector2(-16f, 0f);
            q.info = Label(padR, "", 13f, UttGlass.Muted, TextAlignmentOptions.Right, FontStyles.Normal);
            q.btn = rr.gameObject.AddComponent<Button>();
            q.btn.targetGraphic = q.bg;
            q.btn.transition = Selectable.Transition.None;
            q.btn.onClick.AddListener(() => UttPresenter.SelectedQuiz = idx);
            _quizRows.Add(q);
        }
        _present = MakeButton(Box(aside, 20f, -(h - 20f - 56f), iw, 56f), "", true, 17f, () =>
        {
            bool starting = _board != null && !_board.Presenting;
            UttPresenter.TogglePresenting();
            if (starting) Close();
        });
        _presentText = _present.GetComponentInChildren<TextMeshProUGUI>();
    }

    void BuildResultView()
    {
        _resultView = Fill(NewRect("ResultView", _panel));
        string[] labels = { "Tổng học viên", "Đã làm bài", "Chưa làm bài" };
        float tw = (MainW - 40f) / 3f;
        for (int i = 0; i < 3; i++)
        {
            var tile = Box(_resultView, MainX + i * (tw + 20f), -116f, tw, 96f);
            Img(tile, UttGlass.Round18, CardFill, true);
            Img(Fill(NewRect("Ring", tile)), UttGlass.Ring18, CardRing, true);
            Label(Box(tile, 22f, -16f, tw - 44f, 22f), labels[i], 14f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Normal);
            _tileValues[i] = Label(Box(tile, 22f, -40f, tw - 44f, 40f), "—", 30f, i == 1 ? UttGlass.Green : Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        }

        const float top = -232f, h = H - 232f - 20f;
        var table = Box(_resultView, MainX, top, MainW, h);
        Img(table, UttGlass.Round18, CardFill, true);
        Img(Fill(NewRect("Ring", table)), UttGlass.Ring18, CardRing, true);
        var head = Box(table, 12f, -14f, MainW - 24f, 26f);
        Label(Col(head, 0f, 0.5f), "HỌ TÊN", 13f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Bold);
        Label(Col(head, 0.5f, 0.75f), "MÃ SV", 13f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Bold);
        Label(Col(head, 0.75f, 1f), "ĐIỂM", 13f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Bold);
        _resultContent = Scroll(table, 12f, -48f, MainW - 24f, h - 60f);
        _resultStatus = Label(Box(table, 28f, -60f, MainW - 56f, 64f), "", 15f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Normal);
        _resultView.gameObject.SetActive(false);
    }

    // =========================================================
    // Tiện ích
    RectTransform Scroll(RectTransform parent, float x, float y, float w, float h)
    {
        var view = Box(parent, x, y, w, h);
        Img(view, null, new Color(0f, 0f, 0f, 0f), false).raycastTarget = true;
        view.gameObject.AddComponent<RectMask2D>();
        var content = NewRect("Content", view);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.content = content; sr.viewport = view;
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;
        return content;
    }

    static RectTransform Row(RectTransform content, int index)
    {
        var r = NewRect("Row" + index, content);
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(0f, RowH);
        r.anchoredPosition = new Vector2(0f, -index * (RowH + RowGap));
        return r;
    }

    static RectTransform Col(RectTransform parent, float from, float to)
    {
        var r = NewRect("Col", parent);
        r.anchorMin = new Vector2(from, 0f); r.anchorMax = new Vector2(to, 1f);
        r.offsetMin = new Vector2(16f, 0f); r.offsetMax = new Vector2(-8f, 0f);
        return r;
    }

    static string NoParse(string text)
    {
        text = (text ?? "").Replace("</noparse>", "</ noparse>").Replace("</NOPARSE>", "</ NOPARSE>");
        return "<noparse>" + text + "</noparse>";
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
