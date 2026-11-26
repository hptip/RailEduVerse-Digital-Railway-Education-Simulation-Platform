using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace ProjectLopHoc.UI
{
    public class DashboardManager : MonoBehaviour
    {
        [Header("Tham chiếu 2 Bảng UI")]
        public GameObject lecturerPanel;
        public GameObject studentSlidePanel;

        [Header("Phím tắt đóng/mở")]
        public KeyCode toggleKey = KeyCode.Tab;

        private bool _isLecturer;
        private bool _xrMode;
        private bool _xrToggleWasPressed;

        private void Start()
        {
            // Kiểm tra quyền từ PlayerPrefs đã lưu từ màn hình Login
            _isLecturer = PlayerPrefs.GetInt("IsLecturer", 0) == 1;
            _xrMode = !UttWebPresentation.Active && (FindFirstObjectByType<XROrigin>() != null || XRSettings.isDeviceActive);

            // Đóng cả 2 bảng khi mới vào Scene
            if (lecturerPanel != null) lecturerPanel.SetActive(false);
            if (studentSlidePanel != null) studentSlidePanel.SetActive(false);
        }

        private void Update()
        {
            if (UttWebPresentation.Active && SceneQuickMenu.IsOpen) return;
            bool togglePressed = _xrMode ? WasXRControllerTogglePressed() : WasToggleKeyPressed();
            if (togglePressed)
            {
                ToggleDashboard();
            }
        }

        private bool WasXRControllerTogglePressed()
        {
            UnityEngine.XR.InputDevice rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool primaryPressed = false;
            bool pressed = rightController.isValid &&
                           rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out primaryPressed) &&
                           primaryPressed;
            bool pressedThisFrame = pressed && !_xrToggleWasPressed;
            _xrToggleWasPressed = pressed;
            return pressedThisFrame;
        }

        private bool WasToggleKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return false;
            if (UttWebPresentation.Active && Keyboard.current.lKey.wasPressedThisFrame)
                return !UttWebPresentation.PointerOverUi ||
                    UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.GetComponent<TMPro.TMP_InputField>()?.isFocused != true;

            string controlName = toggleKey.ToString().ToLowerInvariant();
            KeyControl control = Keyboard.current.TryGetChildControl<KeyControl>(controlName);
            return control != null && control.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(toggleKey);
#else
            return false;
#endif
        }

        public void ToggleDashboard()
        {
            var panel = _isLecturer ? lecturerPanel : studentSlidePanel;
            if (panel == null) return;
            bool open = !panel.activeSelf;
            panel.SetActive(open);
            UttDesktopCursor.SetModal(panel, open);
        }
    }
}
