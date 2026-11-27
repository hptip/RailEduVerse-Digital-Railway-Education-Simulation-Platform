using ProjectLopHoc.Features.Quiz;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Thẻ Quiz Live của bản Web (bộ UI Liquid Glass), theo thiết kế "Trình chiếu – học viên".
/// QuizPopupUI chuyển sang đây khi chạy WebGL; bản VR vẫn dùng popup cũ.
///  - Chỉ học viên làm bài. Giảng viên theo dõi số bài đã nộp trên thanh trình chiếu.
///  - Trong lúc làm bài nhân vật đứng yên, chuột được thả tự do.
///  - Nộp bài: chấm điểm, báo số bài đã nộp qua Photon (QuizSessionController) và lưu điểm lên máy chủ (DataBridge),
///    giống hệt QuizPopupUI.SubmitQuiz.
/// </summary>
[DefaultExecutionOrder(-450)]
public sealed class UttQuizLive : MonoBehaviour
{
    static UttQuizLive _inst;
    public static bool IsOpen => _inst != null && _inst._open;

    public static void Show(QuizSO quiz, QuizSessionController session)
    {
        if (_inst == null || quiz == null || quiz.questions == null || quiz.questions.Count == 0) return;
        if (UttSession.IsLecturer) return;
        _inst.Begin(quiz, session);
    }

    public static void Hide() { if (_inst != null) _inst.Close(); }

    const float CardW = 900f;

    TMP_FontAsset _font;
    Canvas _canvas;
    RectTransform _card, _body, _result;
    TextMeshProUGUI _progress, _submitted, _question, _picked, _resultText, _resultSub;
    readonly Button[] _answers = new Button[4];
    readonly Image[] _answerBg = new Image[4], _answerRing = new Image[4], _keyBg = new Image[4];
    readonly TextMeshProUGUI[] _answerText = new TextMeshProUGUI[4], _keyText = new TextMeshProUGUI[4];
    Button _prev, _next, _submit;

    QuizSO _quiz;
    QuizSessionController _session;
    int _index;
    int[] _picks;
    bool _open, _done;

    // =========================================================
    void Awake()
    {
        _inst = this;
        _font = UttGlass.Font;
        Build();
        SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) Close(); };
    }

    void OnDisable() => _open = false;

    void Begin(QuizSO quiz, QuizSessionController session)
    {
        _quiz = quiz; _session = session;
        _index = 0; _done = false;
        _picks = new int[quiz.questions.Count];
        for (int i = 0; i < _picks.Length; i++) _picks[i] = -1;
        EnsureEventSystem();
        _open = true;
        _canvas.enabled = true;
        _body.gameObject.SetActive(true);
        _result.gameObject.SetActive(false);
        Refresh();
    }

    void Close()
    {
        _open = false;
        if (_canvas != null) _canvas.enabled = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void LateUpdate()
    {
        if (!_open) return;
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;
        int sent = 0;
        try { if (_session != null && _session.Object != null && _session.Object.IsValid) sent = _session.SubmittedCount; } catch { }
        _submitted.text = sent + " bài đã nộp";
    }

    void Refresh()
    {
        int n = _quiz.questions.Count;
        var q = _quiz.questions[_index];
        string title = string.IsNullOrWhiteSpace(_quiz.quizName) ? "" : " · " + NoParse(_quiz.quizName);
        _progress.text = $"Câu {_index + 1} / {n}{title}";
        _question.text = NoParse(q.questionText);
        for (int i = 0; i < 4; i++)
        {
            string opt = q.options != null && i < q.options.Length ? q.options[i] : "";
            bool has = !string.IsNullOrWhiteSpace(opt);
            _answers[i].gameObject.SetActive(has);
            if (!has) continue;
            bool on = _picks[_index] == i;
            _answerText[i].text = NoParse(opt);
            _answerBg[i].color = on ? new Color(UttGlass.Accent.r, UttGlass.Accent.g, UttGlass.Accent.b, 0.22f) : new Color(1f, 1f, 1f, 0.09f);
            _answerRing[i].color = on ? UttGlass.Accent : new Color(1f, 1f, 1f, 0.22f);
            _keyBg[i].color = on ? UttGlass.Accent : new Color(1f, 1f, 1f, 0.14f);
            _keyText[i].color = on ? UttGlass.Navy : Color.white;
        }
        int picked = 0;
        foreach (var p in _picks) if (p >= 0) picked++;
        _picked.text = $"Đã chọn {picked}/{n} câu · chọn bằng chuột";
        _prev.interactable = _index > 0;
        _next.interactable = _index < n - 1;
    }

    void Pick(int i)
    {
        if (_done || _quiz == null) return;
        _picks[_index] = i;
        Refresh();
    }

    void Move(int dir)
    {
        if (_quiz == null) return;
        _index = Mathf.Clamp(_index + dir, 0, _quiz.questions.Count - 1);
        Refresh();
    }

    void Submit()
    {
        if (_done || _quiz == null) return;
        _done = true;
        int n = _quiz.questions.Count, score = 0;
        for (int i = 0; i < n; i++) if (_picks[i] == _quiz.questions[i].correctIndex) score++;

        try { _session?.RPC_SubmitResult(UttSession.DisplayName, score, n); }
        catch (System.Exception e) { Debug.LogWarning("[UttQuizLive] " + e.Message); }
        string quizName = string.IsNullOrWhiteSpace(_quiz.quizName) ? "Bài kiểm tra" : _quiz.quizName;
        if (DataBridge.Instance != null) DataBridge.Instance.SaveQuizResult(quizName, score, n);

        _body.gameObject.SetActive(false);
        _result.gameObject.SetActive(true);
        _resultText.text = $"Bạn trả lời đúng <color=#3DDC97>{score}/{n}</color> câu";
        _resultSub.text = "Điểm đã được gửi cho giảng viên.";
    }

    // =========================================================
    void Build()
    {
        var go = new GameObject("UttQuizLiveCanvas");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5030;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        _canvas.enabled = false;
        var root = (RectTransform)go.transform;

        // Làm tối nhẹ phía sau, chặn click lọt vào khung cảnh
        Img(Fill(NewRect("Dim", root)), null, new Color(0.05f, 0.05f, 0.14f, 0.35f), false).raycastTarget = true;

        _card = NewRect("Card", root);
        Place(_card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(CardW, 430f));
        var shadow = NewRect("Shadow", _card);
        shadow.anchorMin = Vector2.zero; shadow.anchorMax = Vector2.one;
        shadow.offsetMin = new Vector2(-24f, -24f); shadow.offsetMax = new Vector2(24f, 24f);
        Img(shadow, UttGlass.Shadow, new Color(0f, 0f, 0f, 0.45f), true);
        var cardImg = Img(_card, UttGlass.Round28, UttGlass.Fill, true);
        cardImg.raycastTarget = true;
        UttGlass.Panel(cardImg);

        // Đầu thẻ
        var chip = Box(_card, 28f, -26f, 124f, 30f);
        Img(chip, UttGlass.Pill, UttGlass.Accent, true);
        Label(chip, "QUIZ LIVE", 14f, UttGlass.Navy, TextAlignmentOptions.Center, FontStyles.Bold);
        _progress = Label(Box(_card, 166f, -26f, 480f, 30f), "", 15f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Normal);
        _progress.overflowMode = TextOverflowModes.Ellipsis;
        _submitted = Label(Box(_card, CardW - 228f, -26f, 200f, 30f), "", 15f, UttGlass.Muted, TextAlignmentOptions.Right, FontStyles.Normal);

        // Phần câu hỏi
        _body = Fill(NewRect("Body", _card));
        _question = Label(Box(_body, 28f, -72f, CardW - 56f, 84f), "", 22f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        _question.textWrappingMode = TextWrappingModes.Normal;
        _question.overflowMode = TextOverflowModes.Ellipsis;
        _question.lineSpacing = 6f;

        float aw = (CardW - 56f - 12f) / 2f, ah = 72f;
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var r = Box(_body, 28f + (i % 2) * (aw + 12f), -168f - (i / 2) * (ah + 12f), aw, ah);
            _answerBg[i] = Img(r, UttGlass.Round18, Color.white, true);
            _answerBg[i].raycastTarget = true;
            _answerRing[i] = Img(Fill(NewRect("Ring", r)), UttGlass.Ring18, Color.white, true);
            var key = NewRect("Key", r);
            Place(key, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(34f, 34f));
            _keyBg[i] = Img(key, UttGlass.Pill, Color.white, true);
            _keyText[i] = Label(key, "ABCD"[i].ToString(), 15f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var txt = Fill(NewRect("Pad", r)); txt.offsetMin = new Vector2(64f, 6f); txt.offsetMax = new Vector2(-16f, -6f);
            _answerText[i] = Label(txt, "", 16f, Color.white, TextAlignmentOptions.Left, FontStyles.Normal);
            _answerText[i].textWrappingMode = TextWrappingModes.Normal;
            _answerText[i].overflowMode = TextOverflowModes.Ellipsis;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = _answerBg[i];
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => Pick(idx));
            _answers[i] = b;
        }

        float fy = -168f - 2f * (ah + 12f) - 14f;
        _picked = Label(Box(_body, 28f, fy, 360f, 48f), "", 14f, UttGlass.Muted, TextAlignmentOptions.Left, FontStyles.Normal);
        _prev = MakeButton(Box(_body, CardW - 28f - 160f - 12f - 140f - 12f - 140f, fy, 140f, 48f), "‹  Câu trước", false, 15f, () => Move(-1));
        _next = MakeButton(Box(_body, CardW - 28f - 160f - 12f - 140f, fy, 140f, 48f), "Câu sau  ›", false, 15f, () => Move(1));
        _submit = MakeButton(Box(_body, CardW - 28f - 160f, fy, 160f, 48f), "Nộp bài", true, 16f, Submit);

        // Màn kết quả
        _result = Fill(NewRect("Result", _card));
        _resultText = Label(Box(_result, 28f, -150f, CardW - 56f, 56f), "", 32f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        _resultSub = Label(Box(_result, 28f, -214f, CardW - 56f, 28f), "", 16f, UttGlass.Muted, TextAlignmentOptions.Center, FontStyles.Normal);
        MakeButton(Box(_result, CardW * 0.5f - 90f, -290f, 180f, 50f), "Đóng", true, 16f, Close);
        _result.gameObject.SetActive(false);
    }

    // =========================================================
    // Tiện ích
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
