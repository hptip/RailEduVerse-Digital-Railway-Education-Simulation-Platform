using System.Collections;
using System.Collections.Generic;
using ProjectLopHoc.Features.Authentication.Views;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Khoác bộ UI Liquid Glass lên giao diện có sẵn của bản Web, KHÔNG đổi logic:
///  - Màn hình Đăng nhập: giữ nguyên ô nhập, nút, lựa chọn vai trò/chuyên ngành của LoginView, chỉ sắp xếp lại vào thẻ kính.
///  - AI chatbot (Showroom, Yên Tử – Hạ Long): khung chat, nút, ô nhập và bong bóng tin nhắn kiểu kính.
/// Không sửa file scene/prefab; bản VR giữ nguyên.
/// </summary>
public sealed class UttWebSkins : MonoBehaviour
{
    sealed class ChatWatch { public AIChatbotUIController ui; public int count = -1; }
    readonly List<ChatWatch> _chats = new List<ChatWatch>();
    float _nextChatCheck;

    void Awake() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;
    void Start() => StartCoroutine(SkinSoon());
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(SkinSoon());

    IEnumerator SkinSoon()
    {
        yield return null; // để Awake/Start của scene chạy xong (LoginView tạo thêm nút Quản trị viên)
        _chats.RemoveAll(c => c.ui == null);
        try { SkinLogin(); } catch (System.Exception e) { Debug.LogWarning("[UttWebSkins] Login: " + e.Message); }
        try { SkinChatbots(); } catch (System.Exception e) { Debug.LogWarning("[UttWebSkins] Chatbot: " + e.Message); }
    }

    void Update()
    {
        if (_chats.Count == 0 || Time.unscaledTime < _nextChatCheck) return;
        _nextChatCheck = Time.unscaledTime + 0.25f;
        foreach (var c in _chats)
        {
            if (c.ui == null || c.ui.content == null) continue;
            int n = c.ui.content.childCount;
            if (n != c.count) { c.count = n; SkinBubbles(c.ui); }
        }
    }

    // =====================================================================
    // Đăng nhập
    void SkinLogin()
    {
        var view = FindFirstObjectByType<LoginView>();
        if (view == null || view.transform.Find("UttLoginCard") != null) return;
        Transform canvas = view.transform;

        // Nền: ảnh nằm sau cùng + lớp tối nhẹ để chữ dễ đọc
        var bg = canvas.Find("Background");
        if (bg != null) bg.SetAsFirstSibling();
        var dim = UttGlass.Layer(canvas, "UttDim", null, new Color(0.03f, 0.07f, 0.16f, 0.32f));
        dim.transform.SetSiblingIndex(bg != null ? 1 : 0);

        var panel = canvas.Find("LoginPanel");
        if (panel != null && panel.TryGetComponent<Image>(out var panelImage)) panelImage.enabled = false;
        var oldTitle = FindDeep(canvas, "LoginText");
        if (oldTitle != null) oldTitle.gameObject.SetActive(false);

        BuildHero(canvas);

        // Thẻ đăng nhập bên phải
        var card = NewRect("UttLoginCard", canvas, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(540f, 0f));
        card.anchoredPosition = new Vector2(-140f, 0f);
        var cardImage = card.gameObject.AddComponent<Image>();
        UttGlass.Panel(cardImage);
        cardImage.raycastTarget = true;
        var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 36, 36);
        layout.spacing = 16f;
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Đầu thẻ
        var header = Row(card, 60f);
        var logo = Box(header, 0f, 0f, 60f, 60f);
        var logoImg = logo.gameObject.AddComponent<Image>();
        UttGlass.Soften(logoImg, UttGlass.Round18, UttGlass.Ring18, UttGlass.Navy);
        logoImg.raycastTarget = false;
        LogoImage(logo, 48f);
        Label(Box(header, 76f, -2f, 380f, 36f), "Đăng nhập", 28f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Label(Box(header, 76f, -38f, 380f, 22f), "Dùng tài khoản trường cấp", 15f, UttGlass.Muted, TextAlignmentOptions.TopLeft, FontStyles.Normal);

        // Vai trò: Sinh viên / Giảng viên / Quản trị viên (dạng nút gạt)
        if (view.studentToggle != null)
        {
            var roleParent = view.studentToggle.transform.parent;
            var roles = new List<Toggle>();
            foreach (Transform child in roleParent)
                if (child.TryGetComponent<Toggle>(out var tg)) roles.Add(tg);
            var seg = Row(card, 52f);
            UttGlass.Layer(seg, "UttSegBg", UttGlass.Pill, UttGlass.Soft);
            UttGlass.Layer(seg, "UttSegRing", UttGlass.PillRing, UttGlass.SoftRing);
            var h = seg.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(4, 4, 4, 4); h.spacing = 4f;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            foreach (var tg in roles) { tg.transform.SetParent(seg, false); Normalize(tg.transform, true); SkinChoice(tg, true); }
        }

        // Chuyên ngành (lưới 2×2)
        var majors = new List<Toggle>();
        foreach (var tg in new[] { view.toggleDuongSatTDC, view.toggleQuanLyDieuHanh, view.toggleCongNghePhuongTien, view.toggleXayDungQuanLy })
            if (tg != null) majors.Add(tg);
        if (majors.Count > 0)
        {
            Label(Row(card, 20f), "Chuyên ngành", 15f, UttGlass.Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            int rows = (majors.Count + 1) / 2;
            var grid = Row(card, rows * 44f + (rows - 1) * 8f);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2((460f - 8f) / 2f, 44f);
            g.spacing = new Vector2(8f, 8f);
            foreach (var tg in majors) { tg.transform.SetParent(grid, false); Normalize(tg.transform, true); SkinChoice(tg, false); }
            ShortLabel(view.toggleDuongSatTDC, "Đường sắt tốc độ cao");
            ShortLabel(view.toggleQuanLyDieuHanh, "Quản lý & điều hành vận tải");
            ShortLabel(view.toggleCongNghePhuongTien, "Công nghệ phương tiện");
            ShortLabel(view.toggleXayDungQuanLy, "Xây dựng & khai thác đô thị");
        }

        // Tài khoản, mật khẩu
        if (view.nameInputField != null) Field(card, view.nameInputField.transform.parent, view.nameInputField, "Tài khoản");
        if (view.passwordInputField != null)
        {
            var container = view.passwordFieldContainer != null ? view.passwordFieldContainer.transform : view.passwordInputField.transform.parent;
            Field(card, container, view.passwordInputField, "Mật khẩu");
        }

        // Nút đăng nhập
        if (view.loginButton != null)
        {
            view.loginButton.transform.SetParent(card, false);
            Normalize(view.loginButton.transform, true);
            Element(view.loginButton.gameObject, 54f);
            UttGlass.PrimaryButton(view.loginButton);
            foreach (var t in view.loginButton.GetComponentsInChildren<TMP_Text>(true)) { t.fontSize = 18f; SetFont(t); }
        }

        // Dòng trạng thái
        if (view.statusText != null)
        {
            view.statusText.transform.SetParent(card, false);
            Normalize(view.statusText.transform, true);
            Element(view.statusText.gameObject, 22f);
            view.statusText.alignment = TextAlignmentOptions.Center;
            view.statusText.fontSize = 14f;
            view.statusText.color = UttGlass.Muted;
            SetFont(view.statusText);
        }
    }

    void BuildHero(Transform canvas)
    {
        var hero = NewRect("UttHero", canvas, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(640f, 336f));
        hero.anchoredPosition = new Vector2(140f, 0f);
        var bg = hero.gameObject.AddComponent<Image>();
        UttGlass.Panel(bg);
        bg.color = UttGlass.FillLight;
        bg.raycastTarget = false;

        // Logo + tên trường
        var mark = Box(hero, 36f, -36f, 128f, 64f);
        LogoImage(mark, 128f);
        Label(Box(hero, 182f, -44f, 420f, 22f), "TRƯỜNG ĐẠI HỌC", 15f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Label(Box(hero, 182f, -68f, 420f, 22f), "CÔNG NGHỆ GIAO THÔNG VẬN TẢI", 15f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        var line = Box(hero, 36f, -118f, 568f, 1f);
        var lineImg = line.gameObject.AddComponent<Image>();
        lineImg.color = new Color(1f, 1f, 1f, 0.18f); lineImg.raycastTarget = false;

        var title = Label(Box(hero, 36f, -136f, 580f, 72f), "Rail<color=#F7931E>Edu</color>Verse", 56f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        title.richText = true;
        var sub = Label(Box(hero, 36f, -220f, 568f, 84f),
            "Học về đường sắt trong không gian 3D: khuôn viên trường, lớp học, phòng trưng bày và tuyến Yên Tử – Hạ Long.",
            19f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.TopLeft, FontStyles.Normal);
        sub.textWrappingMode = TextWrappingModes.Normal;
        sub.lineSpacing = 8f;
    }

    /// <summary>Đặt logo UTT (ảnh tỉ lệ 2:1) giữa một khung; không có ảnh thì ghi chữ "UTT".</summary>
    void LogoImage(RectTransform parent, float width)
    {
        var tex = UttGlass.Logo;
        var r = NewRect("UttLogo", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(width, width * 0.5f));
        if (tex != null)
        {
            var raw = r.gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
        }
        else
        {
            Label(r, "UTT", Mathf.Max(14f, width * 0.32f), UttGlass.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
        }
    }

    static void ShortLabel(Toggle toggle, string text)
    {
        if (toggle == null) return;
        var label = toggle.transform.Find("Label");
        if (label == null) return;
        if (label.TryGetComponent<Text>(out var legacy)) legacy.text = text;
        if (label.TryGetComponent<TMP_Text>(out var tmp)) tmp.text = text;
    }

    /// <summary>
    /// Đưa tỉ lệ/góc xoay về chuẩn. Các object này vốn dựng cho bản VR với tỉ lệ khác 1,
    /// nên khi chuyển vào thẻ đăng nhập bị phóng to và lệch.
    /// </summary>
    static void Normalize(Transform root, bool deep)
    {
        if (root == null) return;
        void Fix(Transform t)
        {
            t.localScale = Vector3.one;
            t.localRotation = Quaternion.identity;
            if (t is RectTransform rt) { var p = rt.anchoredPosition3D; p.z = 0f; rt.anchoredPosition3D = p; }
        }
        Fix(root);
        if (!deep) return;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) Fix(t);
    }

    void Field(RectTransform card, Transform container, TMP_InputField field, string title)
    {
        if (container == null) container = field.transform.parent;
        container.SetParent(card, false);
        Normalize(container, false);
        Normalize(field.transform, true);
        Element(container.gameObject, 82f);
        if (!container.TryGetComponent<VerticalLayoutGroup>(out var v)) v = container.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 6f;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var label = Label(NewRect("UttLabel", container, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero),
            title, 15f, UttGlass.Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        label.transform.SetAsFirstSibling();
        Element(label.gameObject, 22f);
        Element(field.gameObject, 52f);
        UttGlass.Input(field, 19f);
        if (field.textViewport != null)
        {
            field.textViewport.anchorMin = Vector2.zero; field.textViewport.anchorMax = Vector2.one;
            field.textViewport.offsetMin = new Vector2(16f, 6f); field.textViewport.offsetMax = new Vector2(-16f, -6f);
        }
    }

    void SkinChoice(Toggle toggle, bool segmented)
    {
        var oldBg = toggle.transform.Find("Background");
        if (oldBg != null) oldBg.gameObject.SetActive(false);
        var sel = UttGlass.Layer(toggle.transform, "UttChoiceBg", UttGlass.Pill, Color.clear);
        sel.transform.SetAsFirstSibling();
        sel.raycastTarget = true;
        if (!segmented) UttGlass.Layer(toggle.transform, "UttChoiceRing", UttGlass.PillRing, UttGlass.SoftRing).transform.SetSiblingIndex(1);
        toggle.targetGraphic = sel;
        toggle.graphic = null;
        toggle.transition = Selectable.Transition.None;
        if (!toggle.TryGetComponent<LayoutElement>(out var le)) le = toggle.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 44f; le.flexibleWidth = 1f;

        Text legacy = null; TMP_Text tmp = null;
        var labelT = toggle.transform.Find("Label");
        if (labelT != null)
        {
            var r = (RectTransform)labelT;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(8f, 0f); r.offsetMax = new Vector2(-8f, 0f);
            legacy = labelT.GetComponent<Text>();
            tmp = labelT.GetComponent<TMP_Text>();
            if (legacy != null)
            {
                legacy.font = UttProjectFonts.LegacyVietnamese;
                legacy.alignment = TextAnchor.MiddleCenter;
                legacy.fontSize = segmented ? 17 : 14;
                legacy.horizontalOverflow = HorizontalWrapMode.Wrap;
                legacy.verticalOverflow = VerticalWrapMode.Truncate;
                legacy.resizeTextForBestFit = false;
            }
            if (tmp != null) { tmp.alignment = TextAlignmentOptions.Center; tmp.fontSize = segmented ? 17f : 14f; SetFont(tmp); }
        }

        void Refresh(bool on)
        {
            sel.color = segmented ? (on ? Color.white : Color.clear)
                                  : (on ? new Color(UttGlass.Blue.r, UttGlass.Blue.g, UttGlass.Blue.b, 0.32f) : new Color(1f, 1f, 1f, 0.06f));
            Color text = segmented && on ? UttGlass.Navy : Color.white;
            if (legacy != null) { legacy.color = text; legacy.fontStyle = on ? FontStyle.Bold : FontStyle.Normal; }
            if (tmp != null) { tmp.color = text; tmp.fontStyle = on ? FontStyles.Bold : FontStyles.Normal; }
        }
        toggle.onValueChanged.AddListener(Refresh);
        Refresh(toggle.isOn);
    }

    // =====================================================================
    // AI chatbot
    void SkinChatbots()
    {
        foreach (var ui in FindObjectsByType<AIChatbotUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ui == null || ui.panel == null) continue;
            bool done = false;
            foreach (var c in _chats) if (c.ui == ui) done = true;
            if (done) continue;
            _chats.Add(new ChatWatch { ui = ui });

            var panel = ui.panel.transform;
            if (panel.TryGetComponent<Image>(out var panelImage)) UttGlass.Panel(panelImage);
            var header = panel.Find("Header");
            if (header != null && header.TryGetComponent<Image>(out var headerImage)) headerImage.color = new Color(1f, 1f, 1f, 0f);

            foreach (var b in panel.GetComponentsInChildren<Button>(true))
            {
                if (b == ui.sendButton) UttGlass.PrimaryButton(b);
                else UttGlass.GlassButton(b);
            }
            UttGlass.Input(ui.inputField);
            foreach (var s in panel.GetComponentsInChildren<Slider>(true))
            {
                foreach (var img in s.GetComponentsInChildren<Image>(true))
                {
                    img.sprite = UttGlass.Pill; img.type = Image.Type.Sliced;
                    if (s.fillRect != null && img.transform == s.fillRect) img.color = UttGlass.Blue;
                }
            }
            // Chữ tối trên nền kính tối → chuyển sang trắng
            foreach (var t in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.transform.IsChildOf(ui.inputField != null ? ui.inputField.transform : panel) && ui.inputField != null) continue;
                Color.RGBToHSV(t.color, out _, out _, out float v);
                if (v < 0.6f) t.color = Color.white;
            }
            if (ui.statusText != null) ui.statusText.color = UttGlass.Muted;
            SkinBubbles(ui);
        }
    }

    void SkinBubbles(AIChatbotUIController ui)
    {
        if (ui.content == null) return;
        foreach (Transform row in ui.content)
        {
            if (row.Find("UttSkinned") != null) continue;
            var marker = new GameObject("UttSkinned", typeof(RectTransform));
            marker.transform.SetParent(row, false);
            marker.AddComponent<LayoutElement>().ignoreLayout = true;

            bool user = row.name.StartsWith("UserMessage");
            var bubble = row.GetComponentInChildren<ChatBubble>(true);
            Image img = null;
            if (bubble != null && bubble.layout != null) img = bubble.layout.GetComponent<Image>();
            if (img == null) img = row.GetComponentInChildren<Image>(true);
            if (img != null)
            {
                img.sprite = UttGlass.Round18; img.type = Image.Type.Sliced;
                img.color = user ? UttGlass.Blue : new Color(1f, 1f, 1f, 0.12f);
                if (!user && img.transform.Find("UttRing") == null) UttGlass.Layer(img.transform, "UttRing", UttGlass.Ring18, UttGlass.SoftRing);
            }
            foreach (var t in row.GetComponentsInChildren<TMP_Text>(true)) t.color = user ? UttGlass.Navy : Color.white;
        }
    }

    // =====================================================================
    // Tiện ích
    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot; r.sizeDelta = size;
        return r;
    }

    /// <summary>Một hàng trong thẻ (VerticalLayout), cao cố định; con bên trong đặt tự do.</summary>
    static RectTransform Row(RectTransform card, float height)
    {
        var r = NewRect("UttRow", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, height));
        Element(r.gameObject, height);
        return r;
    }

    static RectTransform Box(Transform parent, float x, float y, float w, float h)
    {
        var r = NewRect("UttBox", parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(w, h));
        r.anchoredPosition = new Vector2(x, y);
        return r;
    }

    /// <summary>Object con phủ kín cha (để đặt chữ lên trên một Image).</summary>
    static RectTransform Fill(Transform parent) =>
        NewRect("UttText", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);

    static void Element(GameObject go, float height)
    {
        if (!go.TryGetComponent<LayoutElement>(out var le)) le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height; le.minHeight = height;
        le.ignoreLayout = false;
    }

    static TextMeshProUGUI Label(RectTransform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        var t = parent.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(t);
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    static void SetFont(TMP_Text t)
    {
        var f = UttGlass.Font;
        if (f != null && t != null) t.font = f;
    }
}
