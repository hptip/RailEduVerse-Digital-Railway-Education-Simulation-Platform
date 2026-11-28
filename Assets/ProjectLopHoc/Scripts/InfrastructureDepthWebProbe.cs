#if INFRA_PRODUCTION_AUDIT
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Local development build only. Hold camera pose fixed while comparing projection planes.
[DefaultExecutionOrder(3000)]
public sealed class InfrastructureDepthWebProbe : MonoBehaviour
{
    static readonly float[] Distances = { 22, 105, 280 };
    readonly List<Bounds> context = new();
    readonly Plane[] frustum = new Plane[6];
    Camera cameraSource;
    Bounds focus;
    Vector3 center;
    int distanceIndex, testMode;
    bool isBim, initialized;
    UniversalAdditionalCameraData cameraData;
    bool originalShadows;
    bool compareFasteners;
    int sampleOverride;
    UniversalRenderPipelineAsset pipeline;
    int originalSamples;
    Renderer hiddenPart;
    int partIndex = -1;
    static readonly string[] Parts = { "C005", "C017", "C019", "C024", "C025", "C029", "C030", "C031", "C033" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (!Application.absoluteURL.Contains("depthAudit=1")) return;
        SceneManager.sceneLoaded += (scene, _) =>
        {
            if (scene.name is not ("YenTuHaLongXanh" or "QuanLyVongDoiTaiSan")) return;
            var owner = new GameObject("Local depth comparison");
            SceneManager.MoveGameObjectToScene(owner, scene);
            var probe = owner.AddComponent<InfrastructureDepthWebProbe>();
            probe.isBim = scene.name == "QuanLyVongDoiTaiSan";
            probe.compareFasteners = Application.absoluteURL.Contains("railFastenerAudit=1");
        };
    }

    void OnDestroy()
    {
        if (pipeline != null) pipeline.msaaSampleCount = originalSamples;
        if (hiddenPart != null) hiddenPart.enabled = true;
    }

    void LateUpdate()
    {
        var keys = Keyboard.current;
        if (keys == null) return;
        bool changed = keys.digit6Key.wasPressedThisFrame || keys.digit7Key.wasPressedThisFrame ||
            keys.digit8Key.wasPressedThisFrame || keys.digit9Key.wasPressedThisFrame || keys.digit0Key.wasPressedThisFrame;
        if (keys.digit6Key.wasPressedThisFrame) testMode = 1;
        if (keys.digit7Key.wasPressedThisFrame) testMode = 2;
        if (keys.digit8Key.wasPressedThisFrame) testMode = 3;
        if (keys.digit0Key.wasPressedThisFrame) testMode = 4;
        if (keys.digit9Key.wasPressedThisFrame) distanceIndex = (distanceIndex + 1) % Distances.Length;
        if (testMode == 0 || (!initialized && !CaptureFocus())) return;
        float distance = compareFasteners && distanceIndex == 0 ? (isBim ? 3f : 6f) :
            compareFasteners && distanceIndex == 1 ? 22f : compareFasteners ? 105f : Distances[distanceIndex];
        var offset = new Vector3(1f, .55f, -.74f).normalized * distance;
        cameraSource.transform.SetPositionAndRotation(center + offset, Quaternion.LookRotation(-offset));
        cameraSource.ResetWorldToCameraMatrix();
        cameraSource.ResetProjectionMatrix();
        cameraSource.nearClipPlane = isBim ? .01f : .03f;
        cameraSource.farClipPlane = isBim ? 3200 : 3500;
        if (testMode == 2) cameraSource.nearClipPlane = Mathf.Clamp(distance * .012f, .15f, 2.5f);
        if (testMode >= 3)
        {
            var clip = RailwayAdaptiveClipping.ForBounds(cameraSource, focus, context, distance, cameraSource.farClipPlane, frustum);
            cameraSource.nearClipPlane = clip.x;
            cameraSource.farClipPlane = clip.y;
        }
        if (cameraData != null) cameraData.renderShadows = testMode == 4 ? false : originalShadows;
        if (compareFasteners) CompareFasteners(keys, distance);
        if (changed)
            Debug.Log(FormattableString.Invariant($"[RailDepth] {(isBim ? "BIM" : "Route")},{testMode},{distance:F0},{cameraSource.nearClipPlane:F4},{cameraSource.farClipPlane:F2},{SystemInfo.graphicsDeviceType},{SystemInfo.usesReversedZBuffer}"));
    }

    void CompareFasteners(Keyboard keys, float distance)
    {
        if (keys.digit1Key.wasPressedThisFrame || keys.digit2Key.wasPressedThisFrame)
        {
            if (pipeline == null)
            {
                pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (pipeline != null) originalSamples = pipeline.msaaSampleCount;
            }
            sampleOverride = keys.digit2Key.wasPressedThisFrame ? 4 : 1;
            var descriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 24)
            {
                msaaSamples = sampleOverride
            };
            int supported = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
            if (pipeline != null) pipeline.msaaSampleCount = Mathf.Clamp(supported, 1, sampleOverride);
            Debug.Log($"[RailAA] requested={sampleOverride}, supported={supported}, set={pipeline?.msaaSampleCount}, camera={cameraSource.allowMSAA}");
        }
        if (!isBim || (!keys.digit3Key.wasPressedThisFrame && !keys.digit4Key.wasPressedThisFrame)) return;
        if (hiddenPart != null) hiddenPart.enabled = true;
        hiddenPart = null;
        partIndex = keys.digit4Key.wasPressedThisFrame ? -1 : (partIndex + 1) % Parts.Length;
        var controller = FindFirstObjectByType<RailwayBimController>();
        var proxy = partIndex >= 0 ? controller?.Visible("Z021-TR-S001-B001-" + Parts[partIndex]) : null;
        if (proxy != null && proxy.TryGetComponent<Renderer>(out var renderer) && renderer.enabled)
        {
            hiddenPart = renderer;
            renderer.enabled = false;
        }
        Debug.Log($"[RailPart] {distance},{(partIndex >= 0 ? Parts[partIndex] : "Full")},disabled={hiddenPart != null}");
    }

    bool CaptureFocus()
    {
        if (isBim)
        {
            var controller = FindFirstObjectByType<RailwayBimController>();
            var bim = controller != null ? controller.cameraController : null;
            if (bim == null || bim.FocusBounds.size.sqrMagnitude <= 0) return false;
            if (compareFasteners && controller.Visible("Z021-TR-S001-B001-C001") == null)
            {
                if (!controller.Ready) return false;
                controller.Locate("Z021-TR-S001-B001-C030");
                controller.ClearSelection();
            }
            cameraSource = bim.GetComponent<Camera>();
            var selected = controller.outline.Sources.Where(RailwayBimOutline.IsRenderable).ToArray();
            focus = selected.Length > 0 ? selected[0].bounds : bim.FocusBounds;
            foreach (var item in selected) focus.Encapsulate(item.bounds);
            center = selected.Length > 0 ? selected.OrderBy(item => (item.bounds.center - focus.center).sqrMagnitude).First().bounds.center : focus.center;
            if (compareFasteners)
            {
                var rail = controller.Visible("Z021-TR-S001-B001-C001");
                if (rail == null || !rail.TryGetComponent<Renderer>(out var railRenderer)) return false;
                focus = railRenderer.bounds;
                center = focus.center;
            }
            bim.DepthProbeControlsPose = true;
        }
        else
        {
            var stream = FindFirstObjectByType<RailwayZoneStreamingManager>();
            if (stream == null || stream.trainMover.totalRouteLength <= 0) return false;
            cameraSource = Camera.main;
            center = stream.trainMover.splineRoute.EvaluatePosition(540f / stream.trainMover.totalRouteLength);
            focus = new Bounds(center, new Vector3(12, 5, 15));
        }
        if (cameraSource == null) return false;
        if (cameraSource.TryGetComponent(out cameraData)) originalShadows = cameraData.renderShadows;
        foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (RailwayBimOutline.IsRenderable(renderer)) context.Add(renderer.bounds);
        initialized = true;
        return true;
    }
}
#endif
