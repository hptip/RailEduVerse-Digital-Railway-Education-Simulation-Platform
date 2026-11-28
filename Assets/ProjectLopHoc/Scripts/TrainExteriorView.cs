using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>Local seated-only exterior viewpoint; reuses the tracked stereo camera.</summary>
[DefaultExecutionOrder(-100)]
public sealed class TrainExteriorView : MonoBehaviour
{
    [SerializeField] private TrainSplineMover train;
    [SerializeField] private Material restrictedViewMaterial;
    [SerializeField] private Vector3 offset = new Vector3(10, 5, -12);
    [SerializeField] private Vector3 overheadOffset = new Vector3(0, 12, -10);
    [SerializeField, Range(0, 30)] private float yawRange = 15;
    [SerializeField, Range(0, 20)] private float pitchRange = 8;
    private Transform anchor;
    private FirstPersonController driver;
    private Camera viewCamera;
    private GameObject mask;
    private Material maskMaterial;
    private Mesh maskMesh;
    private InputAction toggle, look;
    private float yaw, pitch, nextSearch;
    private bool active;
    private RailwayZoneStreamingManager route;
    private readonly RaycastHit[] obstructions = new RaycastHit[24];
    private float zoom = 1f, targetZoom = 1f;
    private int viewIndex = -1;
    private float originalNear, originalFar, desktopOrbitDistance;
    private bool desktopClipping, clippingContextDirty;
    private readonly List<Bounds> clippingContext = new();
    private readonly Plane[] clippingPlanes = new Plane[6];
    private Vector3 clipPose, clipDirection;
    private Vector2 plannedClipping;
    public int ViewIndex => viewIndex; // -1 cabin, 0 overhead, 1 right, 2 left, 3 structure (desktop)
    public bool IsExterior => active;
    public Transform ViewAnchor => anchor;
    public void Configure(TrainSplineMover mover, Material material) { train = mover; restrictedViewMaterial = material; }

    private void Awake()
    {
        var go = new GameObject("Train Exterior View Anchor");
        go.transform.SetParent(transform, false); anchor = go.transform;
        toggle = new InputAction("Train exterior B", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        toggle.AddBinding("<Keyboard>/b");
        look = new InputAction("Train exterior look", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
    }
    private void OnEnable()
    {
        toggle?.Enable(); look?.Enable();
        Application.onBeforeRender += BeforeRender;
        SceneManager.sceneLoaded += SceneLoaded;
        SceneManager.sceneUnloaded += SceneUnloaded;
    }
    private void OnDisable()
    {
        SetExterior(false); toggle?.Disable(); look?.Disable();
        Application.onBeforeRender -= BeforeRender;
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneUnloaded -= SceneUnloaded;
    }

    void SceneLoaded(Scene scene, LoadSceneMode mode) => clippingContextDirty = true;
    void SceneUnloaded(Scene scene) => clippingContextDirty = true;
    private bool LocalDriver => driver != null && driver.Object != null && driver.Object.IsValid && driver.HasStateAuthority && driver.IsDrivingTrain(train);
    private void Update()
    {
        if (train == null || train.frontCar == null) { SetExterior(false); return; }
        if (!LocalDriver && Time.unscaledTime >= nextSearch)
        {
            nextSearch = Time.unscaledTime + .5f;
            foreach (var player in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
                if (player.Object != null && player.Object.IsValid && player.HasStateAuthority && player.IsDrivingTrain(train)) { driver = player; break; }
        }
        if (active && !LocalDriver) SetExterior(false);
        if (toggle.WasPressedThisFrame() && !UttDesktopCursor.ModalOpen) ToggleView();
        if (!active) return;
        if (!driver.UsesXrView)
        {
            UpdateDesktopInput();
            return;
        }
        Vector2 input = look.ReadValue<Vector2>();
        if (input.magnitude < .2f) input = Vector2.zero;
        yaw = Mathf.Clamp(yaw + input.x * 25 * Time.unscaledDeltaTime, -yawRange, yawRange);
        pitch = Mathf.Clamp(pitch - input.y * 20 * Time.unscaledDeltaTime, -pitchRange, pitchRange);
        if (Application.isEditor && Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw = Mathf.Clamp(yaw + delta.x * .08f, -yawRange, yawRange);
            pitch = Mathf.Clamp(pitch - delta.y * .08f, -pitchRange, pitchRange);
        }
    }
    public bool ToggleView()
    {
        if (!active && !LocalDriver) return false;
        if (!active) { viewIndex = 0; SetExterior(true); }
        else if (viewIndex < (driver.UsesXrView ? 2 : 3)) { viewIndex++; ResetOrbit(); UpdateAnchor(); }
        else SetExterior(false);
        return active;
    }
    private void SetExterior(bool enabled)
    {
        if (enabled && (!LocalDriver || (driver.UsesXrView && restrictedViewMaterial == null))) return;
        if (active == enabled) return;
        active = enabled;
        if (!enabled) viewIndex = -1;
        if (enabled)
        {
            viewCamera = Camera.main;
            if (viewCamera == null) { active = false; return; }
            ResetOrbit();
            route = FindFirstObjectByType<RailwayZoneStreamingManager>();
            UpdateAnchor();
            if (!active) return;
            if (driver.UsesXrView) CreateMask();
            else
            {
                originalNear = viewCamera.nearClipPlane;
                originalFar = viewCamera.farClipPlane;
                desktopClipping = true;
                clippingContextDirty = true;
                plannedClipping = new Vector2(originalNear, originalFar);
            }
            driver.SetSeatedViewOverride(anchor);
        }
        else
        {
            if (desktopClipping && viewCamera != null)
            {
                viewCamera.nearClipPlane = originalNear;
                viewCamera.farClipPlane = originalFar;
            }
            desktopClipping = false;
            if (driver != null) driver.SetSeatedViewOverride(null);
        }
        if (mask != null) mask.SetActive(active && driver.UsesXrView);
    }
    private void ResetOrbit()
    {
        yaw = pitch = 0f;
        zoom = targetZoom = 1f;
    }

    private void UpdateDesktopInput()
    {
        var mouse = Mouse.current;
        if (mouse == null || !UttDesktopCursor.CanInteract || UttDesktopCursor.PointerOverCenteredUi)
            return;
        if (Keyboard.current?.homeKey.wasPressedThisFrame == true) ResetOrbit();
        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue() * .13f;
            yaw = Mathf.Clamp(yaw + delta.x, -42f, 42f);
            pitch = Mathf.Clamp(pitch - delta.y, -16f, 24f);
        }
        float scroll = RailwayBimCamera.ScrollSteps(mouse.scroll.ReadValue().y);
        if (Mathf.Abs(scroll) > .01f)
            targetZoom = RailwayBimCamera.ZoomDistance(targetZoom, scroll, .16f, .7f, 1.5f);
        zoom = Mathf.Lerp(zoom, targetZoom, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
    }

    private void UpdateAnchor()
    {
        var car = train.frontCar;
        Vector3 forward = Vector3.ProjectOnPlane(car.forward, Vector3.up).normalized;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        if (!driver.UsesXrView)
        {
            PlaceDesktopCamera(car, rotation);
            return;
        }
        Vector3 position = viewIndex == 0 ? overheadOffset : new Vector3(viewIndex == 2 ? -offset.x : offset.x, offset.y, offset.z);
        float baseYaw = viewIndex == 0 ? 0 : viewIndex == 2 ? 22 : -22;
        anchor.SetPositionAndRotation(car.position + rotation * position, rotation * Quaternion.Euler((viewIndex == 0 ? 40 : 12) + pitch, baseYaw + yaw, 0));
    }
    private void LateUpdate()
    {
        if (!active || viewCamera == null) return;
        if (!LocalDriver) { SetExterior(false); return; }
        UpdateAnchor();
        UpdateMask();
    }

    private void PlaceDesktopCamera(Transform car, Quaternion frame)
    {
        float carSpacing = train.rearCar != null ? Vector3.Distance(car.position, train.rearCar.position) : 24f;
        float span = Mathf.Clamp(carSpacing + 29f, 38f, 78f);
        Vector3 pivot = car.position + frame * new Vector3(0f, 2.5f, -carSpacing * .44f);
        float distance = train.currentDistanceMeters;
        bool nearBridge = false;
        if (route != null)
        {
            foreach (var facility in route.fixedInfrastructure)
            {
                if (facility == null || distance < facility.startChainage - 18f || distance > facility.endChainage + 18f)
                    continue;
                if (facility.kind == "TunnelMountain" || facility.kind == "Tunnel")
                {
                    SetExterior(false);
                    return;
                }
                nearBridge |= facility.id == "Bridge_Main";
            }
        }
        Vector3 offset = viewIndex switch
        {
            0 => new Vector3(0f, .88f, .59f),
            1 => new Vector3(.99f, .36f, .38f),
            2 => new Vector3(-.99f, .36f, .38f),
            _ => new Vector3(.85f, nearBridge ? -.14f : .34f, .29f)
        };
        Vector3 arm = frame * (Quaternion.Euler(pitch, yaw, 0f) * offset) * (span * zoom);
        Vector3 eye = pivot + arm;
        int count = Physics.SphereCastNonAlloc(pivot, .45f, arm.normalized, obstructions, arm.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float clearance = arm.magnitude;
        for (int i = 0; i < count; i++)
        {
            var hit = obstructions[i];
            if (hit.transform.IsChildOf(train.transform) || hit.transform.IsChildOf(car) ||
                (train.rearCar != null && hit.transform.IsChildOf(train.rearCar)) ||
                (driver != null && hit.transform.IsChildOf(driver.transform))) continue;
            clearance = Mathf.Min(clearance, hit.distance - .55f);
        }
        if (clearance < span * .42f)
            eye = pivot + frame * new Vector3(0f, span * .85f, span * .4f);
        else if (clearance < arm.magnitude)
            eye = pivot + arm.normalized * clearance;
        desktopOrbitDistance = Vector3.Distance(eye, pivot);
        anchor.SetPositionAndRotation(eye, Quaternion.LookRotation(pivot - eye, Vector3.up));
    }

    private void BeforeRender()
    {
        UpdateMask();
        if (!active || !desktopClipping || viewCamera == null) return;
        bool replan = clippingContextDirty || (viewCamera.transform.position - clipPose).sqrMagnitude > 16f ||
            Vector3.Dot(viewCamera.transform.forward, clipDirection) < .9986f;
        if (clippingContextDirty)
        {
            clippingContextDirty = false;
            clippingContext.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !scene.name.StartsWith("Zone_", System.StringComparison.Ordinal)) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                        if (renderer.enabled) clippingContext.Add(renderer.bounds);
            }
        }
        if (replan)
        {
            clipPose = viewCamera.transform.position;
            clipDirection = viewCamera.transform.forward;
            var focus = new Bounds(train.frontCar.position, Vector3.one * 8);
            if (train.rearCar != null) focus.Encapsulate(new Bounds(train.rearCar.position, Vector3.one * 8));
            plannedClipping = RailwayAdaptiveClipping.ForBounds(viewCamera, focus, clippingContext,
                desktopOrbitDistance, originalFar, clippingPlanes);
            // Keep incoming scenery and the forward rail pool within the far plane.
            plannedClipping.y = Mathf.Min(originalFar, Mathf.Max(plannedClipping.y, (route != null ? route.loadAheadMeters : 650) + 280));
        }
        var clip = plannedClipping;
        clip.x = Mathf.Max(originalNear, Mathf.Min(Mathf.Max(clip.x, desktopOrbitDistance * .005f), .40f));
        RailwayAdaptiveClipping.Apply(viewCamera, clip, Time.unscaledDeltaTime);
    }
    private void UpdateMask()
    {
        if (!active || maskMaterial == null || train == null || train.frontCar == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(train.frontCar.forward, Vector3.up).normalized;
        maskMaterial.SetVector("_TrainForward", forward);
    }
    private void CreateMask()
    {
        if (mask != null) return;
        mask = new GameObject("Exterior View Angular Guard"); mask.layer = viewCamera.gameObject.layer;
        mask.transform.SetParent(viewCamera.transform, false);
        float radius = Mathf.Max(.3f, viewCamera.nearClipPlane * 3);
        mask.transform.localScale = Vector3.one * radius;
        maskMesh = new Mesh { name = "Exterior view guard cube" };
        maskMesh.vertices = new[] {new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
        maskMesh.triangles = new[] {0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
        maskMesh.RecalculateBounds();
        mask.AddComponent<MeshFilter>().sharedMesh = maskMesh;
        maskMaterial = new Material(restrictedViewMaterial);
        var renderer = mask.AddComponent<MeshRenderer>(); renderer.sharedMaterial = maskMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
    }
    private void OnDestroy()
    {
        toggle?.Dispose(); look?.Dispose();
        if (mask != null) Destroy(mask);
        if (maskMaterial != null) Destroy(maskMaterial);
        if (maskMesh != null) Destroy(maskMesh);
    }
}
