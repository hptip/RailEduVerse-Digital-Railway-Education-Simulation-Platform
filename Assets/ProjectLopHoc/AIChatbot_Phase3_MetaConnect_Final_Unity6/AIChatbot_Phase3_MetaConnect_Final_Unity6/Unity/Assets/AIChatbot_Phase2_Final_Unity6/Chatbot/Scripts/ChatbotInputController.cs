using UnityEngine;

/// <summary>
/// Đọc phím hoạt động với cả Input Manager cũ lẫn Input System mới.
/// Không đụng tới Input System của dự án.
/// </summary>
public static class ChatbotInputController
{
    public static bool TogglePressed()
    {
        if (UttChat.IsTyping) return false; // đang gõ chat (phím T): chữ C là chữ, không mở/đóng chatbot
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.C);
#elif ENABLE_INPUT_SYSTEM
        var k = UnityEngine.InputSystem.Keyboard.current;
        return k != null && k.cKey.wasPressedThisFrame;
#else
        return false;
#endif
    }

    /// <summary>Phím tương tác với robot: E hoặc click chuột trái.</summary>
    public static bool InteractPressed()
    {
        if (UttChat.IsTyping) return false;
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);
#elif ENABLE_INPUT_SYSTEM
        var k = UnityEngine.InputSystem.Keyboard.current;
        var m = UnityEngine.InputSystem.Mouse.current;
        return (k != null && k.eKey.wasPressedThisFrame) || (m != null && m.leftButton.wasPressedThisFrame);
#else
        return false;
#endif
    }

    public static bool EscapePressed()
    {
        if (UttChat.IsTyping) return false; // Esc lúc này chỉ để thoát khung chat
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
        var k = UnityEngine.InputSystem.Keyboard.current;
        return k != null && k.escapeKey.wasPressedThisFrame;
#else
        return false;
#endif
    }
}
