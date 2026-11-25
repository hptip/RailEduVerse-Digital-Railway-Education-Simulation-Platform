using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;

public static class UttWebPresentation
{
    public static bool Active => Application.platform == RuntimePlatform.WebGLPlayer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (!Active) return;
        SceneManager.sceneLoaded += Configure;
        SceneManager.sceneUnloaded += LogZoneUnload;
    }

    private static bool IsRailwayZone(Scene scene) =>
        scene.path?.StartsWith("Assets/ProjectLopHoc/Generated/RuntimeZones/", System.StringComparison.Ordinal) == true;

    private static void LogZoneUnload(Scene scene)
    {
        if (IsRailwayZone(scene)) Debug.Log("[RailwayStreaming] Unloaded " + scene.name);
    }

    private static void Configure(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive && IsRailwayZone(scene))
            Debug.Log("[RailwayStreaming] Loaded " + scene.name);

        var roots = scene.GetRootGameObjects();
        foreach (var origin in roots.SelectMany(root => root.GetComponentsInChildren<XROrigin>(true)))
        {
            if (scene.name == "Login" && origin.Camera != null)
            {
                var camera = origin.Camera;
                camera.transform.SetParent(null, true);
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                foreach (var behaviour in camera.GetComponents<Behaviour>())
                    if (behaviour.GetType().Name.Contains("PoseDriver"))
                        behaviour.enabled = false;
                camera.gameObject.SetActive(true);
            }
            origin.gameObject.SetActive(false);
        }

        // Login's desktop camera and its Canvas became a scene root above.
        roots = scene.GetRootGameObjects();
        foreach (var canvas in roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)))
        {
            if (canvas.name == "Crosshair" && canvas.TryGetComponent<GraphicRaycaster>(out var crosshairRaycaster))
                crosshairRaycaster.enabled = false;
            var tracked = canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
            if (tracked == null) continue;
            tracked.enabled = false;
            var pointer = canvas.GetComponent<GraphicRaycaster>();
            if (pointer == null) pointer = canvas.gameObject.AddComponent<GraphicRaycaster>();
            pointer.enabled = true;
            if (canvas.name == "LoginCanvas")
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                // Co giãn theo cả chiều ngang lẫn chiều dọc: cửa sổ rộng không làm ô nhập/nút to quá khổ
                scaler.matchWidthOrHeight = 0.5f;
                // Ảnh nền phủ kín nhưng GIỮ TỈ LỆ (cắt bớt mép thừa), không bị kéo ngang khi cửa sổ rộng
                foreach (var image in canvas.GetComponentsInChildren<Image>(true))
                {
                    var rect = image.rectTransform;
                    if (image.sprite == null || image.sprite.rect.height <= 0f || image.sprite.rect.width < 512f) continue;
                    if (rect.anchorMin != Vector2.zero || rect.anchorMax != Vector2.one) continue;
                    var fitter = image.GetComponent<AspectRatioFitter>();
                    if (fitter == null) fitter = image.gameObject.AddComponent<AspectRatioFitter>();
                    fitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
                    fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                }
            }
        }

        foreach (var module in roots.SelectMany(root => root.GetComponentsInChildren<XRUIInputModule>(true)))
        {
            module.enableXRInput = false;
            module.enableMouseInput = true;
        }
    }

    public static bool PointerOverUi => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
