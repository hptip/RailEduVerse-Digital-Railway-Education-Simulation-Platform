using System.Collections.Generic;
using System.Text;
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
/// Chat chữ trong game của bản Web (bộ UI Liquid Glass), góc dưới bên trái.
///  - T: mở ô nhập. Enter: gửi. Esc: thoát.
///  - Trong lúc gõ nhân vật đứng yên, không xoay góc nhìn, các phím tắt (E, V, M, H...) không có tác dụng.
///  - Tin nhắn gửi qua Photon RPC trên nhân vật của mình (FirstPersonController.RpcChat) tới mọi người trong phòng.
///  - Tối đa 150 ký tự mỗi tin, 1 tin mỗi giây. Lưu 50 tin gần nhất, hiện 8 tin, tự mờ đi sau 10 giây.
/// Chỉ chạy trên WebGL (được thêm trong UttWebView.Init).
/// </summary>
[DefaultExecutionOrder(-470)]
public sealed class UttChat : MonoBehaviour
{
    public const int MaxLength = 150;
    const int Keep = 50, Show = 8;
    const float FadeAfter = 10f, MinInterval = 1f;

    static UttChat _inst;

    /// <summary>Đang gõ chat.</summary>
    public static bool IsTyping => _inst != null && _inst._open;

    struct Line { public string html; }

    readonly List<Line> _lines = new List<Line>();
    TMP_FontAsset _font;
    Canvas _canvas;
    CanvasGroup _logGroup;
    TextMeshProUGUI _log;
    RectTransform _inputRoot;
    TMP_InputField _field;
    TextMeshProUGUI _counter;
    bool _open, _dirty;
    int _closedFrame = -10;
    float _lastMessageAt = -100f, _lastSentAt = -100f;
    FirstPersonController _local;
    float _nextFind;

    void Awake()
    {
        _inst = this;
        _font = UttGlass.Font;
        BuildUi();
        SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) { _local = null; if (_open) Close(); } };
    }

    void OnDisable() { _open = false; }

    // =========================================================
    /// <summary>Gọi từ RPC khi có tin nhắn tới (kể cả tin của chính mình).</summary>
    public static void Receive(FirstPersonController sender, string message)
    {
        if (_inst == null || sender == null) return;
        message = Clean(message);
        if (message.Length == 0) return;

        string name = "Người chơi";
        bool lecturer = false;
        try
        {
            string n = sender.NetworkPlayerName.ToString();
            if (!string.IsNullOrWhiteSpace(n)) name = n;
            lecturer = sender.NetworkPlayerRole.ToString() == "Giảng viên";
        }
        catch { }

        string color = sender.HasStateAuthority ? "#F7931E" : lecturer ? "#FFD77A" : "#BFD9FF";
        var sb = new StringBuilder();
        sb.Append("<color=").Append(color).Append("><b>").Append(NoParse(name)).Append("</b>");
        if (lecturer) sb.Append(" <size=80%>(GV)</size>");
        sb.Append(":</color> ").Append(NoParse(message));
        _inst.Add(sb.ToString());
    }

    /// <summary>Dòng thông báo của hệ thống (chữ nghiêng, màu nhạt).</summary>
    public static void Notice(string text)
    {
        if (_inst == null) return;
        _inst.Add("<color=#FFFFFFB0><i>" + NoParse(text) + "</i></color>");
    }

    void Add(string html)
    {
        _lines.Add(new Line { html = html });
        if (_lines.Count > Keep) _lines.RemoveRange(0, _lines.Count - Keep);
        _lastMessageAt = Time.unscaledTime;
        _dirty = true;
    }

    // =========================================================
    void Update()
    {
        bool allowed = UttWebView.SceneAllowsFirstPerson && FindLocal() != null;
        if (!allowed && _open) Close();
        if (_canvas.enabled != allowed) _canvas.enabled = allowed;
        if (!allowed) return;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (!_open && kb.tKey.wasPressedThisFrame && Time.frameCount > _closedFrame + 1 && !OtherFieldFocused() &&
                !UttProfilePanel.IsOpen && !UttMinimap.WorldMapOpen && !SceneQuickMenu.IsOpen)
                Open();
        }
#endif
        // Phím T vừa bấm để mở có thể lọt vào ô nhập ở khung hình đầu: xoá đi
        if (_open && Time.frameCount <= _openedFrame + 3 && (_field.text == "t" || _field.text == "T")) _field.text = "";

        // Esc, click ra ngoài, hoặc ô nhập mất focus vì lý do khác: đóng
        if (_open && Time.frameCount > _openedFrame + 1 && !_field.isFocused) Close();

        if (_dirty) { _dirty = false; Render(); }

        float age = Time.unscaledTime - _lastMessageAt;
        float alpha = _open ? 1f : age < FadeAfter ? 1f : Mathf.Clamp01(1f - (age - FadeAfter) / 1.5f);
        if (_lines.Count == 0) alpha = 0f;
        if (!Mathf.Approximately(_logGroup.alpha, alpha)) _logGroup.alpha = alpha;

        if (_open) _counter.text = _field.text.Length + "/" + MaxLength;
    }

    int _openedFrame = -10;

    void Open()
    {
        EnsureEventSystem();
        _open = true;
        _openedFrame = Time.frameCount;
        _inputRoot.gameObject.SetActive(true);
        _field.text = "";
        EventSystem.current?.SetSelectedGameObject(_field.gameObject);
        _field.ActivateInputField();
        _dirty = true;
    }

    void Close()
    {
        _open = false;
        _closedFrame = Time.frameCount;
        if (_field.isFocused) _field.DeactivateInputField();
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _field.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        _inputRoot.gameObject.SetActive(false);
        _lastMessageAt = Mathf.Max(_lastMessageAt, Time.unscaledTime - FadeAfter + 3f); // giữ log thêm 3 giây
    }

    void Submit(string text)
    {
        if (!_open) return;
        text = Clean(text);
        if (text.Length > 0)
        {
            if (Time.unscaledTime - _lastSentAt < MinInterval)
            {
                Notice("Gửi chậm lại một chút nhé.");
            }
            else
            {
                var me = FindLocal();
                if (me != null && me.Object != null && me.Object.IsValid)
                {
                    _lastSentAt = Time.unscaledTime;
                    me.SendChat(text);
                }
            }
        }
        Close();
    }

    FirstPersonController FindLocal()
    {
        if (_local != null && _local.isActiveAndEnabled && _local.Object != null && _local.Object.IsValid) return _local;
        _local = null;
        if (Time.unscaledTime < _nextFind) return null;
        _nextFind = Time.unscaledTime + 1f;
        foreach (var p in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            if (p != null && p.Object != null && p.Object.IsValid && p.HasStateAuthority) { _local = p; break; }
        return _local;
    }

    void Render()
    {
        var sb = new StringBuilder();
        int start = Mathf.Max(0, _lines.Count - Show);
        for (int i = start; i < _lines.Count; i++)
        {
            if (i > start) sb.Append('\n');
            sb.Append(_lines[i].html);
        }
        _log.text = sb.ToString();
    }

    public static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
            if (!char.IsControl(c)) sb.Append(c);
        string s = sb.ToString().Trim();
        return s.Length > MaxLength ? s.Substring(0, MaxLength) : s;
    }

    /// <summary>Không cho người chơi chèn thẻ định dạng (màu, cỡ chữ...) vào tin nhắn.</summary>
    static string NoParse(string text)
    {
        text = text.Replace("</noparse>", "</ noparse>").Replace("</NOPARSE>", "</ NOPARSE>");
        return "<noparse>" + text + "</noparse>";
    }

    bool OtherFieldFocused()
    {
        var es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject == null) return false;
        var tmp = es.currentSelectedGameObject.GetComponent<TMP_InputField>();
        if (tmp != null && tmp != _field && tmp.isFocused) return true;
        var legacy = es.currentSelectedGameObject.GetComponent<InputField>();
        return legacy != null && legacy.isFocused;
    }

    // =========================================================
    void BuildUi()
    {
        var go = new GameObject("UttChatCanvas");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 4995;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        _canvas.enabled = false;

        const float W = 560f;

        // Khung tin nhắn
        var log = NewRect("Log", go.transform);
        Place(log, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 84f), new Vector2(W, 230f));
        _logGroup = log.gameObject.AddComponent<CanvasGroup>();
        _logGroup.blocksRaycasts = false;
        _logGroup.interactable = false;
        _logGroup.alpha = 0f;
        var bg = log.gameObject.AddComponent<Image>();
        bg.sprite = UttGlass.Round18;
        bg.type = Image.Type.Sliced;
        bg.color = UttGlass.FillLight;
        bg.raycastTarget = false;
        UttGlass.Layer(log, "UttRing", UttGlass.Ring18, UttGlass.Border);
        var textRect = NewRect("Text", log);
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 12f); textRect.offsetMax = new Vector2(-16f, -12f);
        _log = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) _log.font = _font;
        _log.fontSize = 18f;
        _log.color = Color.white;
        _log.richText = true;
        _log.alignment = TextAlignmentOptions.BottomLeft;
        _log.textWrappingMode = TextWrappingModes.Normal;
        _log.overflowMode = TextOverflowModes.Truncate;
        _log.raycastTarget = false;

        // Ô nhập
        _inputRoot = NewRect("Input", go.transform);
        Place(_inputRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 28f), new Vector2(W, 48f));
        var fieldBg = _inputRoot.gameObject.AddComponent<Image>();
        fieldBg.sprite = UttGlass.Pill;
        fieldBg.type = Image.Type.Sliced;
        fieldBg.color = UttGlass.Fill;

        var area = NewRect("Text Area", _inputRoot);
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(22f, 6f); area.offsetMax = new Vector2(-86f, -6f);
        area.gameObject.AddComponent<RectMask2D>();

        var ph = MakeText(area, "Nhập tin nhắn, Enter để gửi, Esc để thoát", 17f, new Color(1f, 1f, 1f, 0.5f));
        ph.fontStyle = FontStyles.Italic;
        var txt = MakeText(area, "", 18f, Color.white);

        _field = _inputRoot.gameObject.AddComponent<TMP_InputField>();
        _field.targetGraphic = fieldBg;
        _field.textViewport = area;
        _field.textComponent = txt;
        _field.placeholder = ph;
        _field.lineType = TMP_InputField.LineType.SingleLine;
        _field.characterLimit = MaxLength;
        _field.richText = false;
        _field.restoreOriginalTextOnEscape = false;
        if (_font != null) _field.fontAsset = _font;
        UttGlass.Input(_field, 18f);
        fieldBg.sprite = UttGlass.Pill; // UttGlass.Input dùng góc 14, ô chat dùng dạng viên thuốc
        fieldBg.color = UttGlass.Fill;
        var ring = _inputRoot.Find("UttRing");
        if (ring != null && ring.TryGetComponent<Image>(out var ringImg)) ringImg.sprite = UttGlass.PillRing;
        _field.onSubmit.AddListener(Submit);

        var counterRect = NewRect("Counter", _inputRoot);
        counterRect.anchorMin = new Vector2(1f, 0f); counterRect.anchorMax = new Vector2(1f, 1f);
        counterRect.pivot = new Vector2(1f, 0.5f);
        counterRect.sizeDelta = new Vector2(76f, 0f);
        counterRect.anchoredPosition = new Vector2(-16f, 0f);
        _counter = counterRect.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) _counter.font = _font;
        _counter.fontSize = 14f;
        _counter.color = UttGlass.Muted;
        _counter.alignment = TextAlignmentOptions.Right;
        _counter.raycastTarget = false;

        _inputRoot.gameObject.SetActive(false);
    }

    TextMeshProUGUI MakeText(RectTransform parent, string text, float size, Color color)
    {
        var r = NewRect("Text", parent);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.richText = false;
        return t;
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
