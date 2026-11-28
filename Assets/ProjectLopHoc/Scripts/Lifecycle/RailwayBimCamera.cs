using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class RailwayBimCamera:MonoBehaviour
{
    public event System.Action ViewRequested;
    [Header("Orbit zoom")]
    [SerializeField, Min(.01f)] float zoomSensitivity = .18f;
    [SerializeField, Min(0f)] float zoomSmoothing = 16f;
    [SerializeField, Min(.01f)] float detailMinimumDistance = .04f;
    [SerializeField, Min(1f)] float focusDistanceMultiplier = 1f;

    public bool xrPresentation;public bool FreeMode{get;private set;}public float freeSpeed=12;
    [System.Serializable] public struct View{public Vector3 center;public float yaw,pitch,distance,min,max;public Bounds bounds;}
    public View current;public int mode;public Bounds zoneBounds;public Bounds FocusBounds{get;private set;}
    readonly View[] previous=new View[3];readonly bool[] hasPrevious=new bool[3];
    Vector3 smoothCenter;float smoothDistance;bool initialized;
    bool bridgeFrame;
    bool orbitStarted, panStarted;
    readonly List<Vector3> bridgeCorners = new List<Vector3>();
    Vector3 bridgeProjectionOrigin, bridgeProjectionForward;
    float bridgeNearestDepth;
    readonly List<Bounds> clippingContext = new();
    readonly Plane[] clippingPlanes = new Plane[6];
    Camera outputCamera;
    float baseNear, baseFar;
#if INFRA_PRODUCTION_AUDIT
    internal bool DepthProbeControlsPose;
#endif

    internal void SetClippingContext(IEnumerable<Renderer> renderers)
    {
        clippingContext.Clear();
        foreach (var renderer in renderers)
            if (RailwayBimOutline.IsRenderable(renderer)) clippingContext.Add(renderer.bounds);
    }

    void OnEnable()
    {
        outputCamera = GetComponent<Camera>();
        if (outputCamera == null) return;
        baseNear = outputCamera.nearClipPlane;
        baseFar = outputCamera.farClipPlane;
    }

    void OnDisable()
    {
        if (outputCamera == null) return;
        outputCamera.nearClipPlane = baseNear;
        outputCamera.farClipPlane = baseFar;
    }

    public enum BridgeFrame { Zone, Structure, Group, Component }

    public bool FocusBridge(IReadOnlyList<Renderer> renderers, Vector3 axis, BridgeFrame frame)
    {
        if (renderers.Count == 0) return false;
        if (xrPresentation)
        {
            Bounds target = renderers[0].bounds;
            foreach (var renderer in renderers) target.Encapsulate(renderer.bounds);
            Focus(target, 0, false);
            return true;
        }

        var horizontal = Vector3.ProjectOnPlane(axis, Vector3.up);
        if (horizontal.sqrMagnitude < .001f) horizontal = Vector3.forward;
        float sideYaw = Quaternion.LookRotation(Vector3.Cross(Vector3.up, horizontal)).eulerAngles.y;
        float yaw = sideYaw + (frame == BridgeFrame.Zone ? 30 : frame == BridgeFrame.Structure ? 18 : 30);
        float pitch = frame == BridgeFrame.Zone ? 25 : frame == BridgeFrame.Structure ? 18 : 19;
        var rotation = Quaternion.Euler(pitch, yaw, 0);
        var toView = Quaternion.Inverse(rotation);

        // Start near the bridge so viewer world offsets don't reduce projection precision.
        Vector3 origin = renderers[0].bounds.center;
        Bounds world = renderers[0].bounds, projected = default;
        bridgeCorners.Clear();
        bridgeProjectionOrigin = origin;
        bridgeProjectionForward = Vector3.zero;
        bool first = true;
        foreach (var renderer in renderers)
        {
            world.Encapsulate(renderer.bounds);
            var local = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = local.center + Vector3.Scale(local.extents, new Vector3(
                    (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var worldCorner = renderer.localToWorldMatrix.MultiplyPoint3x4(corner);
                bridgeCorners.Add(worldCorner);
                var point = toView * (worldCorner - origin);
                if (first) projected = new Bounds(point, Vector3.zero);
                else projected.Encapsulate(point);
                first = false;
            }
        }
        var camera = GetComponent<Camera>();
        float aspect = UttWebPresentation.Active ? camera.pixelWidth / (float)Mathf.Max(1, camera.pixelHeight) : camera.aspect;
        float width = frame == BridgeFrame.Zone ? .74f : frame == BridgeFrame.Structure ? .9f : .82f;
        if (!UttWebPresentation.Active) width *= .62f;
        float distance = FitDistance(projected, Quaternion.identity, camera.fieldOfView, aspect, width, .83f);
        mode = 0;
        FocusBounds = world;
        current = new View
        {
            bounds = world, center = origin + rotation * projected.center, yaw = yaw, pitch = pitch,
            distance = Mathf.Max(distance * focusDistanceMultiplier, .05f), min = .05f,
            max = Mathf.Max(60, distance * 2.35f)
        };
        bridgeFrame = true;
        FreeMode = false;
        // Land on the new target immediately; orbit, pan and zoom still interpolate.
        smoothCenter = current.center;
        smoothDistance = current.distance;
        initialized = true;
        ViewRequested?.Invoke();
        return true;
    }
    public static float FitDistance(Bounds bounds,Quaternion rotation,float verticalFov,float aspect,float widthFraction=1,float heightFraction=1)
    {
        var local=Quaternion.Inverse(rotation);Vector3 max=Vector3.zero;
        for(int i=0;i<8;i++){var e=bounds.extents;var v=local*new Vector3((i&1)==0?-e.x:e.x,(i&2)==0?-e.y:e.y,(i&4)==0?-e.z:e.z);max=Vector3.Max(max,new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)));}
        float tan=Mathf.Tan(Mathf.Clamp(verticalFov,20,120)*Mathf.Deg2Rad*.5f);
        return Mathf.Max(max.y/(tan*heightFraction),max.x/(tan*Mathf.Max(aspect,.5f)*widthFraction))*1.07f+max.z;
    }
    public void Focus(Bounds bounds,int newMode,bool preserve=true)
    {
        bridgeFrame = false;
        if(preserve&&initialized&&mode!=newMode){previous[mode]=current;hasPrevious[mode]=true;}
        mode=newMode;FocusBounds=bounds;current.bounds=bounds;current.center=bounds.center;FreeMode=false;
        if(!initialized){current.yaw=130;current.pitch=42;}
        current.min=newMode==0?.6f:newMode==1?.4f:detailMinimumDistance;current.max=newMode==0?1600:newMode==1?180:30;
        var camera=GetComponent<Camera>();
        if (xrPresentation) camera.nearClipPlane = .01f;
        bool fullView = xrPresentation || UttWebPresentation.Active;
        current.distance=Mathf.Clamp(FitDistance(bounds,Quaternion.Euler(current.pitch,current.yaw,0),camera.fieldOfView,camera.aspect,fullView?1:.40f,fullView?1:.72f)*focusDistanceMultiplier,current.min,current.max);
        if (!initialized || Vector3.Distance(smoothCenter, bounds.center) > Mathf.Max(60, bounds.size.magnitude * 1.5f))
        {
            smoothCenter = current.center;
            smoothDistance = current.distance;
            initialized = true;
        }
        ViewRequested?.Invoke();
    }
    public void Restore(int toMode,Bounds fallback){FreeMode=false;bridgeFrame=false;mode=toMode;if(hasPrevious[toMode]){current=previous[toMode];FocusBounds=current.bounds;ViewRequested?.Invoke();}else Focus(fallback,toMode,false);}
    public void ResetView(){Focus(zoneBounds,0,false);}
    public void SetFreeMode(bool value)
    {
        if(value&&!FreeMode){current.center=transform.position+Quaternion.Euler(current.pitch,current.yaw,0)*Vector3.forward*current.distance;smoothCenter=current.center;smoothDistance=current.distance;}
        FreeMode=value;
    }
    public void ReturnToFocus(){FreeMode=false;ViewRequested?.Invoke();}
    public void Orbit(Vector2 delta)
    {
        var previousForward=Quaternion.Euler(current.pitch,current.yaw,0)*Vector3.forward;
        current.yaw+=delta.x*.17f;current.pitch=Mathf.Clamp(current.pitch-delta.y*.17f,FreeMode?-89:5,FreeMode?89:85);
        if(FreeMode)current.center+=(Quaternion.Euler(current.pitch,current.yaw,0)*Vector3.forward-previousForward)*current.distance;
    }
    public void Zoom(float scroll)
    {
        if(FreeMode){current.center+=Quaternion.Euler(current.pitch,current.yaw,0)*Vector3.forward*(scroll*freeSpeed*.5f);return;}
        float minimum = MinimumDistance(current.center);
        current.distance = ZoomDistance(current.distance, scroll, zoomSensitivity, minimum, Mathf.Max(minimum, current.max));
    }

    public static float ZoomDistance(float distance, float scroll, float sensitivity, float minimum, float maximum) =>
        Mathf.Clamp(distance * Mathf.Exp(-Mathf.Clamp(scroll, -5f, 5f) * sensitivity), minimum, maximum);

    public static float ScrollSteps(float delta)
    {
        bool windows = Application.platform is RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer;
        bool platformUnits = InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange;
        return windows && platformUnits ? delta / 120f : delta;
    }

    float MinimumDistance(Vector3 center)
    {
        if (bridgeFrame) return BridgeMinimum(center);
        if (mode != 2) return current.min;
        var forward = Quaternion.Euler(current.pitch, current.yaw, 0) * Vector3.forward;
        var extent = current.bounds.extents;
        float reach = Vector3.Dot(extent, new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z)));
        return Mathf.Max(current.min, Vector3.Dot(center - current.bounds.center, forward) + reach + .025f);
    }

    float BridgeMinimum(Vector3 center)
    {
        var forward = Quaternion.Euler(current.pitch, current.yaw, 0) * Vector3.forward;
        if (bridgeProjectionForward != forward)
        {
            bridgeProjectionForward = forward;
            bridgeNearestDepth = float.PositiveInfinity;
            foreach (var corner in bridgeCorners)
                bridgeNearestDepth = Mathf.Min(bridgeNearestDepth, Vector3.Dot(corner - bridgeProjectionOrigin, forward));
        }
        return Mathf.Max(.05f, Vector3.Dot(center - bridgeProjectionOrigin, forward) - bridgeNearestDepth + .04f);
    }
    static bool Typing()=>EventSystem.current?.currentSelectedGameObject?.GetComponent<TMPro.TMP_InputField>()?.isFocused==true;
    public void Pan(Vector2 delta) => current.center += (-transform.right * delta.x - transform.up * delta.y) * current.distance * .001f;
    void Update()
    {
        if(xrPresentation||Typing()||SceneQuickMenu.IsOpen)return;var mouse=Mouse.current;
        if(!UttWebPresentation.Active&&mouse!=null)
        {
            bool overView = GetComponent<Camera>().pixelRect.Contains(mouse.position.ReadValue()) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            if (mouse.rightButton.wasPressedThisFrame) orbitStarted = overView;
            if (mouse.middleButton.wasPressedThisFrame) panStarted = overView;
            if (!mouse.rightButton.isPressed) orbitStarted = false;
            if (!mouse.middleButton.isPressed) panStarted = false;
            if (overView)
            {
                if (orbitStarted) Orbit(mouse.delta.ReadValue());
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0) Zoom(ScrollSteps(scroll));
                if (panStarted) Pan(mouse.delta.ReadValue());
            }
        }
        if(FreeMode&&Keyboard.current!=null&&!UttProfilePanel.IsOpen) // menu E đang mở: WASD/Q/E không bay camera
        {
            var k=Keyboard.current;Vector3 move=transform.right*((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0))+transform.forward*((k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0))+Vector3.up*((k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0));
            Vector3 delta=Vector3.ClampMagnitude(move,1)*freeSpeed*(k.leftShiftKey.isPressed||k.rightShiftKey.isPressed?3:1)*Time.unscaledDeltaTime;
            current.center+=delta;
        }
    }
    void LateUpdate()
    {
#if INFRA_PRODUCTION_AUDIT
        if (DepthProbeControlsPose) return;
#endif
        if(xrPresentation)return;float t=1-Mathf.Exp(-Time.unscaledDeltaTime*14);
        smoothCenter=FreeMode?current.center:Vector3.Lerp(smoothCenter,current.center,t);smoothDistance=FreeMode||zoomSmoothing<=0?current.distance:Mathf.Lerp(smoothDistance,current.distance,1-Mathf.Exp(-Time.unscaledDeltaTime*zoomSmoothing));
        if (!FreeMode) smoothDistance = Mathf.Max(smoothDistance, MinimumDistance(smoothCenter));
        var r=Quaternion.Euler(current.pitch,current.yaw,0);var desired=smoothCenter-r*Vector3.forward*smoothDistance;
        transform.SetPositionAndRotation(desired,r);
        if (outputCamera == null || !initialized) return;
        var clip = FreeMode ? new Vector2(RailwayAdaptiveClipping.MinimumNear, baseFar)
            : RailwayAdaptiveClipping.ForBounds(outputCamera, current.bounds, clippingContext,
                smoothDistance, baseFar, clippingPlanes);
        RailwayAdaptiveClipping.Apply(outputCamera, clip, Time.unscaledDeltaTime);
    }
}
