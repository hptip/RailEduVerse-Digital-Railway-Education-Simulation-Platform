using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Owns the desktop cursor and combines the active UI requests in the current scene.</summary>
public sealed class UttDesktopCursor : MonoBehaviour
{
    static UttDesktopCursor instance;
    readonly HashSet<GameObject> modals = new();
    readonly List<Canvas> reticles = new();
    readonly List<BaseInputModule> suspendedModules = new();
    UttCenteredPointer centeredPointer;
    MonoBehaviour player;
    bool configured, wantsCapture, captured, screenCenterFallback;
    int captureFrame;

    static bool Desktop => !Application.isMobilePlatform && !XRSettings.isDeviceActive;
    public static bool HasPlayer => Desktop && instance != null && instance.player is FirstPersonController && instance.player.isActiveAndEnabled;
    public static bool ModalOpen => Desktop && instance != null && instance.HasModal();
    public static bool Captured => Desktop && instance != null && instance.captured && !instance.HasModal();
    public static bool CanInteract => Desktop && instance != null && !instance.HasModal() &&
        (instance.captured || instance.screenCenterFallback) && Time.frameCount > instance.captureFrame + 1;
    public static bool CanLook => CanInteract && (Captured || Mouse.current?.rightButton.isPressed == true);
    public static bool PointerOverCenteredUi => CanInteract &&
        instance.centeredPointer != null && instance.centeredPointer.enabled &&
        instance.centeredPointer.HasGraphicAtCenter();

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void UttCursorSetCaptureAvailable(int available);
    [DllImport("__Internal")] static extern int UttCursorCaptured();
#endif

    /// <summary>
    /// TẮT: dự án giữ hệ thống chuột cũ (UttWebView: chuột tự do / góc nhìn thứ nhất / thứ ba).
    /// Bật lại = đặt true, nhưng phải chuyển các script đang tự khoá/mở chuột sang SetModal/UsePlayer trước,
    /// nếu không 2 hệ thống sẽ giành nhau khoá chuột.
    /// </summary>
    const bool Enabled = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        if (!Enabled) return;
        var root = new GameObject(nameof(UttDesktopCursor));
        DontDestroyOnLoad(root);
        instance = root.AddComponent<UttDesktopCursor>();
    }

    public static void UsePlayer(MonoBehaviour controller, bool desktop)
    {
        if (instance == null || !Desktop) return;
        if (desktop) instance.player = controller;
        else if (instance.player == controller) instance.player = null;
    }

    public static void SetModal(GameObject panel, bool open)
    {
        if (instance == null || panel == null || !Desktop) return;
        if (open) instance.modals.Add(panel);
        else instance.modals.Remove(panel);
        if (open) instance.Apply(false);
    }

    bool HasModal()
    {
        modals.RemoveWhere(panel => panel == null || !panel.activeInHierarchy);
        var selection = EventSystem.current?.currentSelectedGameObject;
        return modals.Count > 0 ||
            selection != null && (selection.TryGetComponent<TMPro.TMP_InputField>(out var tmp) && tmp.isFocused ||
                                  selection.TryGetComponent<InputField>(out var input) && input.isFocused);
    }

    void OnEnable() => SceneManager.activeSceneChanged += OnSceneChanged;
    void OnDisable() => SceneManager.activeSceneChanged -= OnSceneChanged;

    void OnSceneChanged(Scene oldScene, Scene scene)
    {
        if (!Desktop) return;
        SetCenteredPointer(false);
        player = null;
        reticles.Clear();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.name == "Crosshair")
                {
                    reticles.Add(canvas);
                    if (canvas.TryGetComponent<GraphicRaycaster>(out var raycaster)) raycaster.enabled = false;
                }
        Apply(false);
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) Apply(false);
    }

    void LateUpdate()
    {
        if (!Desktop)
        {
            if (wantsCapture) Apply(false);
            SetCenteredPointer(false);
            return;
        }
        bool available = Application.isFocused && player != null && player.isActiveAndEnabled && !HasModal();
        Apply(available);
        SetCenteredPointer(captured || screenCenterFallback);
    }

    void SetCenteredPointer(bool active)
    {
        var events = EventSystem.current;
        if (centeredPointer != null && active && centeredPointer.Owner == events && centeredPointer.enabled) return;
        if (centeredPointer != null) centeredPointer.enabled = false;
        foreach (var module in suspendedModules)
            if (module != null) module.enabled = true;
        suspendedModules.Clear();
        centeredPointer = null;
        if (!active || events == null) return;

        centeredPointer = events.GetComponent<UttCenteredPointer>() ?? events.gameObject.AddComponent<UttCenteredPointer>();
        foreach (var module in events.GetComponents<BaseInputModule>())
        {
            if (module == centeredPointer || !module.enabled) continue;
            module.enabled = false;
            suspendedModules.Add(module);
        }
        centeredPointer.enabled = true;
    }

    void Apply(bool available)
    {
        if (!Desktop && !wantsCapture) return;
        if (!configured || wantsCapture != available)
        {
            configured = true;
            wantsCapture = available;
#if UNITY_WEBGL && !UNITY_EDITOR
            UttCursorSetCaptureAvailable(available ? 1 : 0);
#else
            Cursor.lockState = available ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !available;
#endif
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        int browserState = available ? UttCursorCaptured() : 0;
        bool now = browserState == 1;
        bool fallback = browserState == 2;
#else
        bool now = available && Cursor.lockState == CursorLockMode.Locked;
        const bool fallback = false;
#if ENABLE_INPUT_SYSTEM
        if (available && !now && !UttWebPresentation.PointerOverUi && Mouse.current?.leftButton.wasPressedThisFrame == true)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            now = true;
        }
#endif
#endif
        if ((now || fallback) && !captured && !screenCenterFallback) captureFrame = Time.frameCount;
        captured = now;
        screenCenterFallback = fallback;
        foreach (var canvas in reticles)
            if (canvas != null && canvas.enabled != (now || fallback)) canvas.enabled = now || fallback;
    }
}
