using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.Networking;
using SQLite;
using System.Runtime.InteropServices;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UttVr.BridgePilot;

public sealed partial class RailwayBimController:MonoBehaviour
{
    public RailwayBimCatalog catalog;public RailwayBimCamera cameraController;public RailwayBimOutline outline;public Transform overviewRoot;
    public RailwayBimStore Store{get;private set;}public int Level{get;private set;}public string SegmentID{get;private set;}public string BlockID{get;private set;}public string SelectedID{get;private set;}
    public event Action Changed;public event Action<string> Status;
    public string LastStatus{get;private set;}public double LastTransitionMilliseconds{get;private set;}
    public bool Measuring{get;private set;}public float LastMeasurement{get;private set;}
    public string DatabasePath=>Application.isEditor?Path.Combine(Directory.GetParent(Application.dataPath).FullName,"UserData","QuanLyVongDoiTaiSan.db"):Path.Combine(Application.persistentDataPath,"QuanLyVongDoiTaiSan.db");
    Dictionary<string,RailwayBimCatalog.Block> blocks;Dictionary<string,RailwayBimCatalog.Segment> segments;
    Dictionary<string,BimAssetProxy> overview=new Dictionary<string,BimAssetProxy>(),visible=new Dictionary<string,BimAssetProxy>();
    List<BimAssetProxy> blockPool=new List<BimAssetProxy>(),partPool=new List<BimAssetProxy>();Transform blockRoot,detailRoot;Vector3? measureStart;LineRenderer measureLine;
    readonly Dictionary<Renderer,bool> isolated=new Dictionary<Renderer,bool>();bool suppressFocus;
    Vector2 webPress;
    bool webLeftDown, webDragged, webRightDown, webMenuWasOpen;
    float webLastClick = float.NegativeInfinity;
    string webLastID;
    public BridgeBimAdapter bridge;
    public RailwayBimZoneViewer zoneViewer;
    public bool ViewingDetailedZone => zoneViewer == null || zoneViewer.ActiveZone == RailwayBimZoneViewer.DetailedZone;
    public void ReportZone(string status) => Message(status);
    public void FocusZone(Bounds bounds) => cameraController.Focus(bounds, 0, false);
    public void FocusDetailedZone() => cameraController.Focus(cameraController.zoneBounds, 0, false);
    public bool FocusBridge(string target, string group, RailwayBimCamera.BridgeFrame frame)
    {
        if (bridge == null) return false;
        var renderers = (frame == RailwayBimCamera.BridgeFrame.Component
            ? bridge.SelectedRenderers : bridge.FocusRenderers(group)).ToArray();
        if (!cameraController.FocusBridge(renderers, bridge.RailwayDirection(), frame)) return false;
        RailwayBimExplorer.Audit(FormattableString.Invariant(
            $"BridgeFocus,{target},{frame},{renderers.Length},{cameraController.current.distance:F2}"));
        return true;
    }

    public void SetZonePresentation(bool detailed)
    {
        ReleaseViewEffects();
        bridge?.Clear();
        RestoreIsolation();
        outline.Clear();
        SelectedID = null;
        Level = 0;
        SegmentID = BlockID = null;
        visible.Clear();
        detailRoot.gameObject.SetActive(false);
        foreach (var proxy in blockPool) proxy.gameObject.SetActive(false);
        foreach (var proxy in overview.Values) proxy.gameObject.SetActive(true);
        overviewRoot.gameObject.SetActive(detailed);
        zoneViewer.detailedEnvironment.gameObject.SetActive(detailed);
        Changed?.Invoke();
    }
    public BridgeBimEntry SelectedBridge => bridge == null ? null : bridge.Selected;
    public InfrastructureDetailedLoader ActiveInfrastructure =>
        bridge!=null&&bridge==zoneViewer?.productionLoader?.ActiveAdapter?zoneViewer.productionLoader:null;
    BridgeInfrastructureStore bridgeStore;
    BridgeInfrastructureStore infrastructureStore;
    public BridgeInfrastructureStore BridgeStore => bridge!=null&&bridge==zoneViewer?.productionLoader?.ActiveAdapter
        ? infrastructureStore ??= zoneViewer.productionLoader.OpenUserStore()
        : bridgeStore ??= new BridgeInfrastructureStore(bridge.catalog);

    public bool SelectBridge(string id, bool focus)
    {
        if (bridge == null || !bridge.SelectAsset(id, useMaterialHighlight: false)) return false;
        GroupSelection = null;
        RestoreIsolation();
        SelectedID = null;
        RefreshOutline();
        if (focus) FocusSelected();
        Changed?.Invoke();
        return true;
    }

    public bool IsIsolated=>isolated.Count>0||ActiveInfrastructure?.IsolatedAssetId!=null;
    public bool Ready=>Store!=null;public int ActiveDetailedParts=>detailRoot!=null&&detailRoot.gameObject.activeSelf?partPool.Count:0;
    void Awake()
    {
        outline.Configure(cameraController.GetComponent<Camera>(), GetComponent<RailwayBimVrPresentation>());
        StartCoroutine(PrepareDatabase());
    }
    IEnumerator PrepareDatabase()
    {
        string seed=Path.Combine(Application.streamingAssetsPath,"RailwayLifecycle","Initial.db");
        if (UttWebPresentation.Active)
        {
            if (!HasWebSeed())
            {
                using var request = UnityWebRequest.Get(seed);
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Message("Không đọc được dữ liệu ban đầu: " + request.error);
                    yield break;
                }
                bool seeded = false;
                try { SaveWebSeed(request.downloadHandler.data); seeded = true; }
                catch (Exception error) { Message(error.Message); Debug.LogException(error, this); }
                if (!seeded) yield break;
            }
            Initialize(null);
            yield break;
        }
        if(!File.Exists(DatabasePath)&&seed.Contains("://"))
        {
            Message("Đang chuẩn bị dữ liệu lần đầu…");
            using(var request=UnityWebRequest.Get(seed))
            {
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){Message("Không đọc được dữ liệu ban đầu: "+request.error);yield break;}
                seed=Path.Combine(Application.temporaryCachePath,"RailwayLifecycleSeed.db");File.WriteAllBytes(seed,request.downloadHandler.data);
            }
        }
        Initialize(seed);
    }
    bool HasWebSeed()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
        using var database = new SQLiteConnection(DatabasePath);
        return database.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Assets'") == 1;
    }

    void SaveWebSeed(byte[] bytes)
    {
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            using var source = new SQLiteConnection(":memory:");
            source.Deserialize(bytes, "main", SQLite3.DeserializeFlags.ReadOnly);
            source.Backup(DatabasePath);
        }
        finally { pinned.Free(); }
    }

    void Initialize(string seed)
    {
        try
        {
            if(!UttWebPresentation.Active&&!File.Exists(DatabasePath)&&!File.Exists(seed))throw new FileNotFoundException("Thiếu dữ liệu ban đầu",seed);
            Store=new RailwayBimStore(DatabasePath,seed);blocks=catalog.BlockIndex();segments=catalog.segments.ToDictionary(s=>s.id);
            foreach(var proxy in overviewRoot.GetComponentsInChildren<BimAssetProxy>(true)){overview.Add(proxy.assetID,proxy);EnsureRayTarget(proxy);}
            blockRoot=new GameObject("PooledBlockLayer").transform;blockRoot.SetParent(transform,false);detailRoot=new GameObject("PooledPhysicalLayer").transform;detailRoot.SetParent(transform,false);
            int capacity=catalog.blocks.GroupBy(b=>b.segmentID).Max(g=>g.Count());for(int i=0;i<capacity;i++)blockPool.Add(NewProxy(blockRoot,"BlockSlot_"+i));
            detailRoot.gameObject.SetActive(false);
            var bounds=catalog.segments[0].bounds;foreach(var s in catalog.segments)bounds.Encapsulate(s.bounds);cameraController.zoneBounds=bounds;cameraController.Focus(bounds,0,false);SetOverview();Message("Sẵn sàng · Chọn tài sản để xem hồ sơ");Debug.Log($"[RailwayBim] Ready {Time.timeSinceLevelLoad:F2}s after scene load");
        }
        catch(Exception e){Message(e.Message);Debug.LogException(e,this);enabled=false;}
    }
    // Detailed parts are only needed once a block is opened, so they are built then instead of at scene load.
    void EnsureParts()
    {
        if(partPool.Count>0)return;
        foreach(var part in catalog.parts){var proxy=NewProxy(detailRoot,part.name);Assign(proxy,part.mesh,part.materials);proxy.gameObject.SetActive(true);partPool.Add(proxy);}
    }
    // Box colliders are enough for ray selection; mesh colliders cooked millions of vertices on the main thread at load.
    static BimAssetProxy NewProxy(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>();g.AddComponent<MeshRenderer>();g.AddComponent<BoxCollider>();var p=g.AddComponent<BimAssetProxy>();g.SetActive(false);return p;}
    static void Assign(BimAssetProxy p,Mesh mesh,Material[] materials){p.GetComponent<MeshFilter>().sharedMesh=mesh;p.GetComponent<MeshRenderer>().sharedMaterials=materials;var box=p.GetComponent<BoxCollider>();var b=mesh!=null?mesh.bounds:new Bounds();box.center=b.center;box.size=Vector3.Max(b.size,Vector3.one*.05f);EnsureRayTarget(p);}
    static void EnsureRayTarget(BimAssetProxy proxy){if(proxy.GetComponent<XRSimpleInteractable>()==null)proxy.gameObject.AddComponent<XRSimpleInteractable>();}
    public List<BimAsset> CurrentItems()=>!Ready?new List<BimAsset>():Level==0?Store.Assets.Values.Where(a=>a.AssetType=="Segment").OrderBy(a=>a.AssetID).ToList():Store.Children(Level==1?SegmentID:BlockID);
    public void Select(string id)
    {
        Store.Require(id);string expected=Level==0?"Segment":Level==1?"Block":"Component";if(Store.Assets[id].AssetType!=expected)throw new ArgumentException("Sai cấp lựa chọn");
        GroupSelection = null;
        bridge?.Clear();
        if(SelectedID!=id)RestoreIsolation();SelectedID=id;RefreshOutline();Changed?.Invoke();if(!suppressFocus)FocusSelected();
    }
    public void Locate(string id) => Locate(id, true);
    public void Locate(string id, bool focus)
    {
        GroupSelection = null;
        ReleaseViewEffects();
        if(zoneViewer?.OpenInfrastructureAsset(id,focus)==true)return;
        bool bridgeContext = zoneViewer == null || zoneViewer.ActiveZone == RailwayBimZoneViewer.BridgeZone;
        if ((bridgeContext && SelectBridge(id, focus || SelectedBridge == null)) || zoneViewer?.OpenBridgeAsset(id) == true) return;
        Store.Require(id);
        zoneViewer?.Open(RailwayBimZoneViewer.DetailedZone, false);
        var asset=Store.Assets[id];suppressFocus=true;
        try
        {
            if (UttWebPresentation.Active && asset.AssetType == "Component" && Level == 2 && BlockID == asset.BlockID) Select(id);
            else if(asset.AssetType=="Segment"){SetOverview();Select(id);}else{SetOverview();Select(asset.SegmentID);EnterDetails();if(asset.AssetType=="Block")Select(id);else{Select(asset.BlockID);EnterDetails();Select(id);}}
        }
        finally{suppressFocus=false;}if(focus)FocusSelected();
    }

    public void ClearSelection()
    {
        if (SelectedID == null && SelectedBridge == null && GroupSelection == null) return;
        GroupSelection = null;
        ViewModes.Reset();
        bridge?.Clear();
        RestoreIsolation();
        SelectedID = null;
        RefreshOutline();
        Changed?.Invoke();
    }
    public void EnterDetails()
    {
        if(!ViewingDetailedZone||string.IsNullOrEmpty(SelectedID)||Level==2)return;ReleaseViewEffects();RestoreIsolation();double started=Time.realtimeSinceStartupAsDouble;outline.Clear();
        if(Level==0)
        {
            SegmentID=SelectedID;Level=1;foreach(var proxy in overview.Values)proxy.gameObject.SetActive(proxy.assetID!=SegmentID);visible.Clear();
            var bs=catalog.blocks.Where(b=>b.segmentID==SegmentID).ToArray();for(int i=0;i<blockPool.Count;i++){var p=blockPool[i];p.gameObject.SetActive(i<bs.Length);if(i>=bs.Length)continue;var b=bs[i];p.assetID=b.id;p.transform.SetPositionAndRotation(b.position,b.rotation);Assign(p,b.overview,b.materials);visible.Add(b.id,p);}
            cameraController.Focus(segments[SegmentID].bounds,1);
        }
        else
        {
            BlockID=SelectedID;Level=2;var b=blocks[BlockID];visible[BlockID].gameObject.SetActive(false);detailRoot.SetPositionAndRotation(b.position+Vector3.up*catalog.detailedVerticalOffset,b.rotation);EnsureParts();detailRoot.gameObject.SetActive(true);
            for(int i=0;i<partPool.Count;i++)partPool[i].assetID=BlockID+"-"+catalog.parts[i].key;
            cameraController.Focus(b.bounds,2);
        }
        SelectedID=null;LastTransitionMilliseconds=(Time.realtimeSinceStartupAsDouble-started)*1000;Changed?.Invoke();
    }
    public void Back()
    {
        ReleaseViewEffects();
        RestoreIsolation();outline.Clear();if(Level==2){detailRoot.gameObject.SetActive(false);visible[BlockID].gameObject.SetActive(true);Level=1;SelectedID=BlockID;cameraController.Restore(1,segments[SegmentID].bounds);BlockID=null;RefreshOutline();Changed?.Invoke();}
        else if(Level==1){string id=SegmentID;SetOverview();SelectedID=id;cameraController.Restore(0,cameraController.zoneBounds);RefreshOutline();Changed?.Invoke();}
    }
    public void SetOverview()
    {ReleaseViewEffects();bridge?.Clear();RestoreIsolation();outline.Clear();detailRoot.gameObject.SetActive(false);foreach(var p in blockPool)p.gameObject.SetActive(false);foreach(var p in overview.Values)p.gameObject.SetActive(true);visible.Clear();Level=0;SelectedID=null;SegmentID=null;BlockID=null;cameraController.Restore(0,cameraController.zoneBounds);Changed?.Invoke();}
    public BimAssetProxy Visible(string id)
    {if(overview.TryGetValue(id,out var proxy)&&Level==0)return proxy;if(visible.TryGetValue(id,out proxy)&&Level==1)return proxy;if(Level==2)return partPool.FirstOrDefault(p=>p.assetID==id);return null;}
    void RefreshOutline()
    {
        if (UttWebPresentation.Active) { RefreshScopeView(); return; }
        cameraController.SetClippingContext(CandidateRenderers());
        if (SelectedBridge != null)
        {
            outline.Show(bridge.SelectedRenderers);
            return;
        }
        var proxy = SelectedID != null ? Visible(SelectedID) : null;
        outline.Show(proxy != null ? proxy.GetComponentsInChildren<Renderer>(true) : null);
    }
    public void FocusSelected()
    {
        if (GroupSelection != null)
        {
            if (TryScopeBounds(ScopeRenderers(), out var scopeBounds)) cameraController.Focus(scopeBounds, 0, false);
            return;
        }
        if (bridge != null && bridge.TryGetSelectedBounds(out var bridgeBounds))
        {
            if (zoneViewer?.ActiveZone != RailwayBimZoneViewer.BridgeZone ||
                !FocusBridge(SelectedBridge.assetId, null, RailwayBimCamera.BridgeFrame.Component))
                cameraController.Focus(bridgeBounds, 0, false);
            return;
        }
        if (!ViewingDetailedZone) { zoneViewer.Refocus(); return; }
        if(SelectedID==null){cameraController.Focus(Level==0?cameraController.zoneBounds:Level==1?segments[SegmentID].bounds:blocks[BlockID].bounds,Level,false);return;}var proxy=Visible(SelectedID);if(proxy!=null)cameraController.Focus(proxy.GetComponent<Renderer>().bounds,Level,false);}
    public void ToggleVisibility(){if(SelectedID==null)return;var proxy=Visible(SelectedID);if(proxy==null)return;var renderer=proxy.GetComponent<Renderer>();renderer.enabled=!renderer.enabled;RefreshOutline();Message(renderer.enabled?"Đã hiện đối tượng":"Đã ẩn đối tượng; chọn lại từ danh sách để hiện");}
    public void ToggleIsolation()
    {
        if(ActiveInfrastructure is { } loader&&SelectedBridge!=null)
        {
            bool showAll=loader.IsolatedAssetId!=null;
            if(showAll)loader.ResetView();
            else loader.Isolate(SelectedBridge.assetId);
            RailwayBimExplorer.Audit($"InfraView,{(showAll?"Reset":"Isolation")},{SelectedBridge.assetId}");
            RefreshOutline();Changed?.Invoke();return;
        }
        if(IsIsolated){RestoreIsolation();RefreshOutline();Changed?.Invoke();return;}if(SelectedID==null)return;
        var selected=Visible(SelectedID);if(selected==null)return;var keep=new HashSet<Renderer>(selected.GetComponentsInChildren<Renderer>(true));
        foreach(var proxy in overview.Values.Concat(blockPool).Concat(partPool))foreach(var renderer in proxy.GetComponentsInChildren<Renderer>(true))
        {if(isolated.ContainsKey(renderer))continue;isolated.Add(renderer,renderer.enabled);renderer.enabled=keep.Contains(renderer);}
        RefreshOutline();Changed?.Invoke();
    }
    public void RestoreIsolation(){foreach(var item in isolated)if(item.Key!=null)item.Key.enabled=item.Value;isolated.Clear();}
    public bool Pick(Ray ray,bool cycle=false)
    {
        if (!ViewingDetailedZone)
            return bridge != null && bridge.TryPick(ray, out var pickedBridge) && SelectBridge(pickedBridge.assetId, !suppressFocus);
        var hits=Physics.RaycastAll(ray,2000,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).ToArray();var valid=new List<BimAssetProxy>();
        foreach(var hit in hits){
            if (valid.Count == 0 && bridge != null)
            {
                var identity = hit.collider.GetComponentInParent<BridgeBimIdentity>();
                if (bridge.Owns(identity) && bridge.TryGetGlobalId(identity.GlobalId, out var entry))
                    return SelectBridge(entry.assetId, !suppressFocus);
            }
            var proxy=hit.collider.GetComponent<BimAssetProxy>();if(proxy==null){if(valid.Count==0)return false;break;}if(!proxy.GetComponent<Renderer>().enabled||!Store.Assets.TryGetValue(proxy.assetID,out var a))continue;string expected=Level==0?"Segment":Level==1?"Block":"Component";if(a.AssetType==expected&&!valid.Contains(proxy))valid.Add(proxy);if(!cycle&&valid.Count>0)break;}
        if(valid.Count==0)return false;int index=cycle?valid.FindIndex(p=>p.assetID==SelectedID):-1;Select(valid[(index+1)%valid.Count].assetID);return true;
    }
    void Update()
    {
        if(!Ready)return;if(Keyboard.current!=null&&Keyboard.current.fKey.wasPressedThisFrame&&(!UttWebPresentation.Active||!SceneQuickMenu.IsOpen&&!UttProfilePanel.IsOpen)&&!(EventSystem.current?.currentSelectedGameObject?.GetComponent<TMPro.TMP_InputField>()!=null))FocusSelected();
        if(cameraController.xrPresentation)return;
        if(UttWebPresentation.Active){UpdateWebPointer();return;}
        if(Mouse.current==null||EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())return;
        if(Mouse.current.leftButton.wasPressedThisFrame)
        {
            var ray=cameraController.GetComponent<Camera>().ScreenPointToRay(Mouse.current.position.ReadValue());
            if(Measuring){if(Physics.Raycast(ray,out var hit,2000))MeasurePoint(hit.point);return;}
            bool alt=Keyboard.current!=null&&(Keyboard.current.leftAltKey.isPressed||Keyboard.current.rightAltKey.isPressed);Pick(ray,alt);
        }
    }
    void UpdateWebPointer()
    {
        var keyboard = Keyboard.current;
        var typing = EventSystem.current?.currentSelectedGameObject?.GetComponent<TMPro.TMP_InputField>()?.isFocused == true;
        bool menuOpen = SceneQuickMenu.IsOpen || UttProfilePanel.IsOpen; // menu E: không chọn/xoay mô hình, Esc chỉ đóng menu
        bool menuHandledEscape = menuOpen || webMenuWasOpen;
        webMenuWasOpen = menuOpen;
        if (menuOpen)
        {
            webLeftDown = webRightDown = false;
            return;
        }
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !typing && !menuHandledEscape) ClearSelection();
        var mouse = Mouse.current;
        if (mouse == null) return;
        var camera = cameraController.GetComponent<Camera>();
        Vector2 position = mouse.position.ReadValue();
        Vector2 delta = mouse.delta.ReadValue();
        bool inView = camera.pixelRect.Contains(position) && !UttWebPresentation.PointerOverUi;
        if (mouse.leftButton.wasPressedThisFrame)
        {
            webPress = position;
            webLeftDown = inView;
            webDragged = false;
        }
        if (mouse.rightButton.wasPressedThisFrame) webRightDown = inView;
        if (mouse.rightButton.wasReleasedThisFrame) webRightDown = false;
        if (webLeftDown && (position - webPress).sqrMagnitude > 36f) webDragged = true;
        if (inView)
        {
            if (webLeftDown && webDragged && mouse.leftButton.isPressed) cameraController.Orbit(delta);
            if (webRightDown && mouse.rightButton.isPressed) cameraController.Pan(delta);
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel != 0)
            {
                float before = cameraController.current.distance;
                float steps = RailwayBimCamera.ScrollSteps(wheel);
                cameraController.Zoom(steps);
                RailwayBimExplorer.Audit(FormattableString.Invariant($"Zoom,{SelectedBridge?.assetId ?? SelectedID},{before:F5},{cameraController.current.distance:F5},{steps:F3}"));
            }
        }
        if (!mouse.leftButton.wasReleasedThisFrame) return;
        bool click = webLeftDown && !webDragged && inView;
        webLeftDown = false;
        if (!click) return;
        Ray ray = camera.ScreenPointToRay(position);
        if (Measuring)
        {
            if (Physics.Raycast(ray, out var hit, 2000)) MeasurePoint(hit.point);
            return;
        }
        bool cycle = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
        suppressFocus = true;
        bool picked;
        try { picked = Pick(ray, cycle); }
        finally { suppressFocus = false; }
        if (!picked) return;
        if (RailwayBimExplorer.BeginMetric() != 0)
            RailwayBimExplorer.Audit("ModelPick," + (SelectedBridge?.assetId ?? SelectedID) + "," + outline.HighlightedRenderers);
        string pickedId = SelectedBridge?.assetId ?? SelectedID;
        if (webLastID == pickedId && Time.unscaledTime - webLastClick < .35f)
        {
            if (SelectedBridge == null && Level < 2) EnterDetails();
            else FocusSelected();
        }
        webLastID = pickedId;
        webLastClick = Time.unscaledTime;
    }
    public bool UsePointer(Ray ray)
    {if(!Ready)return false;if(Measuring){if(!Physics.Raycast(ray,out var hit,2000))return false;MeasurePoint(hit.point);return true;}return Pick(ray);}
    void MeasurePoint(Vector3 point)
    {if(!measureStart.HasValue)measureStart=point;else{LastMeasurement=Vector3.Distance(measureStart.Value,point);EnsureLine();measureLine.SetPositions(new[]{measureStart.Value,point});Message("Khoảng cách trên mô hình: "+LastMeasurement.ToString("F3")+" m");measureStart=null;}}
    void EnsureLine(){if(measureLine!=null)return;var g=new GameObject("Measurement");g.transform.SetParent(transform);measureLine=g.AddComponent<LineRenderer>();measureLine.sharedMaterial=outline.outlineMaterial;measureLine.positionCount=2;measureLine.startWidth=measureLine.endWidth=.015f;}
    public void Measure(){Measuring=!Measuring;measureStart=null;Message(Measuring?"Chọn hai điểm 3D để đo":"Đã tắt công cụ đo");}
    public void Message(string text){LastStatus=text;Status?.Invoke(text);}
    void OnDestroy(){ViewModes.Reset();interiors.Reset();infrastructureStore?.Dispose();bridgeStore?.Dispose();Store?.Dispose();Store=null;}
}
