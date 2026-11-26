using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bộ UI Liquid Glass dùng chung, màu thương hiệu UTT (phương án B: kính chàm + điểm nhấn cam như logo trường).
/// Gồm màu, ảnh bo góc 9-slice tạo bằng code, logo, và hàm "khoác kính" cho UI có sẵn.
/// Kính "giả": nền mờ đục + viền sáng + vệt sáng mép trên, không làm nhòe màn hình nên rất nhẹ trên WebGL.
/// </summary>
public static class UttGlass
{
    /// <summary>Màu chàm của logo UTT (dùng cho chữ trên nền cam/trắng).</summary>
    public static readonly Color Navy = new Color32(0x26, 0x26, 0x5F, 0xFF);
    public static readonly Color Fill = new Color(0.149f, 0.149f, 0.376f, 0.86f);
    public static readonly Color FillLight = new Color(0.149f, 0.149f, 0.376f, 0.62f);
    public static readonly Color Border = new Color(1f, 1f, 1f, 0.24f);
    public static readonly Color Shine = new Color(1f, 1f, 1f, 0.36f);
    public static readonly Color Soft = new Color(1f, 1f, 1f, 0.10f);
    public static readonly Color SoftRing = new Color(1f, 1f, 1f, 0.28f);
    /// <summary>Màu nhấn = cam logo UTT (#F7931E). Giữ tên Blue để code cũ không phải đổi.</summary>
    public static readonly Color Blue = new Color32(0xF7, 0x93, 0x1E, 0xFF);
    public static Color Accent => Blue;

    static Texture2D _logo;
    /// <summary>Biểu tượng logo UTT màu cam, nền trong suốt (Assets/Resources/UttBrand/utt_mark.png, tỉ lệ 2:1).</summary>
    public static Texture2D Logo => _logo != null ? _logo : _logo = Resources.Load<Texture2D>("UttBrand/utt_mark");
    public static readonly Color Amber = new Color32(0xFF, 0xC9, 0x4D, 0xFF);
    public static readonly Color AmberText = new Color32(0xFF, 0xD7, 0x7A, 0xFF);
    public static readonly Color Green = new Color32(0x3D, 0xDC, 0x97, 0xFF);
    public static readonly Color Muted = new Color(1f, 1f, 1f, 0.72f);

    static Sprite _r28, _r18, _r14, _pill, _ring28, _ring18, _ring14, _pillRing, _shadow;
    public static Sprite Round28 => _r28 != null ? _r28 : _r28 = Rounded(28, 0f, 1f);
    public static Sprite Round18 => _r18 != null ? _r18 : _r18 = Rounded(18, 0f, 1f);
    public static Sprite Round14 => _r14 != null ? _r14 : _r14 = Rounded(14, 0f, 1f);
    public static Sprite Pill => _pill != null ? _pill : _pill = Rounded(24, 0f, 1f);
    public static Sprite Ring28 => _ring28 != null ? _ring28 : _ring28 = Rounded(28, 1.5f, 1f);
    public static Sprite Ring18 => _ring18 != null ? _ring18 : _ring18 = Rounded(18, 1.5f, 1f);
    public static Sprite Ring14 => _ring14 != null ? _ring14 : _ring14 = Rounded(14, 1.5f, 1f);
    public static Sprite PillRing => _pillRing != null ? _pillRing : _pillRing = Rounded(24, 1.5f, 1f);
    public static Sprite Shadow => _shadow != null ? _shadow : _shadow = Rounded(56, 0f, 28f);

    public static TMP_FontAsset Font
    {
        get
        {
            var f = UttProjectFonts.Vietnamese;
            return f != null ? f : Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto Vietnamese SDF");
        }
    }

    /// <summary>Biến một Image có sẵn thành tấm kính lớn (bảng, khung chat, thẻ đăng nhập).</summary>
    public static void Panel(Image image)
    {
        if (image == null) return;
        image.sprite = Round28;
        image.type = Image.Type.Sliced;
        image.color = Fill;
        var t = image.transform;
        if (t.Find("UttRing") == null) Layer(t, "UttRing", Ring28, Border);
        if (t.Find("UttShine") == null)
        {
            var shine = Layer(t, "UttShine", null, Shine);
            var r = shine.rectTransform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(30f, -2f); r.offsetMax = new Vector2(-30f, -1f);
        }
    }

    /// <summary>Nút/ô kiểu kính nhỏ: nền trắng mờ + viền.</summary>
    public static void Soften(Image image, Sprite fill, Sprite ring, Color color)
    {
        if (image == null) return;
        image.sprite = fill;
        image.type = Image.Type.Sliced;
        image.color = color;
        if (ring != null && image.transform.Find("UttRing") == null) Layer(image.transform, "UttRing", ring, SoftRing);
    }

    /// <summary>Nút chính màu xanh UTT, chữ navy.</summary>
    public static void PrimaryButton(Button button)
    {
        if (button == null) return;
        var img = button.targetGraphic as Image;
        if (img == null) button.TryGetComponent(out img);
        if (img != null) { img.sprite = Pill; img.type = Image.Type.Sliced; img.color = Blue; }
        FlatColors(button);
        foreach (var t in button.GetComponentsInChildren<TMP_Text>(true)) { t.color = Navy; t.fontStyle = FontStyles.Bold; }
        foreach (var t in button.GetComponentsInChildren<Text>(true)) { t.color = Navy; t.fontStyle = FontStyle.Bold; }
    }

    /// <summary>Nút phụ kiểu kính.</summary>
    public static void GlassButton(Button button)
    {
        if (button == null) return;
        var img = button.targetGraphic as Image;
        if (img == null) button.TryGetComponent(out img);
        Soften(img, Pill, PillRing, new Color(1f, 1f, 1f, 0.12f));
        FlatColors(button);
        foreach (var t in button.GetComponentsInChildren<TMP_Text>(true)) t.color = Color.white;
        foreach (var t in button.GetComponentsInChildren<Text>(true)) t.color = Color.white;
    }

    /// <summary>Ô nhập kiểu kính.</summary>
    public static void Input(TMP_InputField field, float fontSize = 0f)
    {
        if (field == null) return;
        var bg = field.targetGraphic as Image;
        if (bg == null) field.TryGetComponent(out bg);
        Soften(bg, Round14, Ring14, new Color(1f, 1f, 1f, 0.08f));
        var font = Font;
        if (field.textComponent != null)
        {
            field.textComponent.color = Color.white;
            if (font != null) field.textComponent.font = font;
            if (fontSize > 0f) field.textComponent.fontSize = fontSize;
        }
        if (field.placeholder is TMP_Text ph)
        {
            ph.color = new Color(1f, 1f, 1f, 0.5f);
            if (font != null) ph.font = font;
            if (fontSize > 0f) ph.fontSize = fontSize;
        }
        field.caretColor = Color.white;
        field.customCaretColor = true;
        field.selectionColor = new Color(Blue.r, Blue.g, Blue.b, 0.45f);
        var colors = field.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.selectedColor = new Color(1.4f, 1.4f, 1.4f, 1f); colors.pressedColor = Color.white;
        field.colors = colors;
    }

    static void FlatColors(Selectable s)
    {
        var c = s.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        c.selectedColor = Color.white;
        c.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        c.colorMultiplier = 1f;
        s.colors = c;
    }

    /// <summary>Thêm một lớp ảnh phủ kín cha (không chặn chuột, không tham gia layout).</summary>
    public static Image Layer(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        return img;
    }

    /// <summary>Ảnh bo góc 9-slice (chỉ tạo một lần cho cả game).</summary>
    public static Sprite Rounded(int radius, float ring, float feather)
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
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
    }
}
