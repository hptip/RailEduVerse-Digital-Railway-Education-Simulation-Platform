using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UttVr.BridgePilot;

public sealed class InfrastructureDetailedLoader : MonoBehaviour
{
    [Serializable]
    public sealed class Occurrence
    {
        public string assetId, domain, owner, zone, assembly, sourceId, visualBatch;
        public int chunk;
        public float chainage;
        public Mesh mesh;
        public Material material;
        public Matrix4x4 placement;
    }

    public BridgeBimCatalog catalog;
    public Transform visualRoot;
    public string resourcePrefix;
    public Vector3 modelOrigin;
    [HideInInspector] public Occurrence[] occurrences = Array.Empty<Occurrence>();
    public int previewChunk;
    public string previewDomain = "Tunnel";
    public BridgeBimAdapter ActiveAdapter { get; private set; }
    public int LoadedCount => loaded.Count;
    public string IsolatedAssetId { get; private set; }
    internal bool ShowingVisual { get; private set; }

    readonly List<(Transform node, MeshFilter filter, MeshRenderer renderer, MeshCollider collider,
        Mesh original, Vector3 position)> loaded = new();
    readonly List<Mesh> sectionMeshes = new();
    readonly List<Renderer> suppressed = new();
    GameObject activeGroup;
    BridgeBimCatalog activeCatalog;
    readonly List<InfrastructureDetailChunk> activeResources = new();
    readonly HashSet<(string domain, int chunk)> activeChunks = new();

    public void Load(string domain, int chunk, IEnumerable<Transform> visualRoots = null) =>
        LoadChunks(new[] { (domain, chunk) }, visualRoots);

    internal bool HasChunks(IEnumerable<(string domain, int chunk)> chunks) => activeChunks.SetEquals(chunks);

    internal bool SetVisualPresentation(bool value)
    {
        if (activeGroup == null || !suppressed.Any(renderer => renderer != null) || ShowingVisual == value) return false;
        if (value) activeGroup.SetActive(false);
        foreach (var renderer in suppressed)
            if (renderer != null) renderer.enabled = value;
        if (!value) activeGroup.SetActive(true);
        ShowingVisual = value;
        return true;
    }

    internal void LoadChunks(IEnumerable<(string domain, int chunk)> chunks, IEnumerable<Transform> visualRoots)
    {
        Unload();
        var entries = new List<Occurrence>();
        foreach (var key in chunks)
        {
            var resource = string.IsNullOrEmpty(resourcePrefix) ? null :
                Resources.Load<InfrastructureDetailChunk>($"{resourcePrefix}/{key.domain}_{key.chunk:D3}");
            if (resource != null) activeResources.Add(resource);
            var items = resource != null ? resource.entries : occurrences.Where(item =>
                item.domain == key.domain && item.chunk == key.chunk && item.mesh != null).ToArray();
            entries.AddRange(items);
            activeChunks.Add(key);
        }
        if (entries.Count == 0) return;
        var ids = new HashSet<string>(entries.Select(item => item.assetId), StringComparer.Ordinal);
        activeCatalog = ScriptableObject.CreateInstance<BridgeBimCatalog>();
        activeCatalog.schema = catalog.schema;
        activeCatalog.modeledScale = catalog.modeledScale;
        var byId = catalog.entries.ToDictionary(item=>item.globalId,StringComparer.Ordinal);
        foreach(var entry in catalog.entries.Where(entry=>ids.Contains(entry.globalId)))
        {
            string parent=entry.parentGlobalId;
            while(!string.IsNullOrEmpty(parent)&&ids.Add(parent)&&byId.TryGetValue(parent,out var group))
                parent=group.parentGlobalId;
        }
        activeCatalog.entries=catalog.entries.Where(entry=>ids.Contains(entry.globalId)).ToArray();
        activeGroup = new GameObject(activeChunks.Count == 1
            ? $"Detailed · {activeChunks.First().domain} · C{activeChunks.First().chunk:D3}" : "Detailed · Selected chunks");
        activeGroup.hideFlags=HideFlags.DontSave;
        activeGroup.SetActive(false);
        activeGroup.transform.SetParent(transform,false);
        var adapter = activeGroup.AddComponent<BridgeBimAdapter>();
        adapter.catalog = activeCatalog;
        foreach(var item in entries)
        {
            var node = new GameObject(item.assetId);
            node.transform.SetParent(activeGroup.transform,false);
            node.transform.SetLocalPositionAndRotation((Vector3)item.placement.GetColumn(3)-modelOrigin,item.placement.rotation);
            var filter = node.AddComponent<MeshFilter>();
            filter.sharedMesh = item.mesh;
            var renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = item.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var collider = node.AddComponent<MeshCollider>();
            collider.sharedMesh = item.mesh;
            node.AddComponent<BridgeBimIdentity>().Configure(item.assetId,new Renderer[]{renderer});
            loaded.Add((node.transform,filter,renderer,collider,item.mesh,node.transform.localPosition));
        }
        ActiveAdapter = adapter;
        var batchNames = entries.Where(item => !string.IsNullOrEmpty(item.visualBatch) &&
            item.visualBatch != "HIDDEN_IN_VISUAL").Select(item => item.visualBatch).ToHashSet(StringComparer.Ordinal);
        foreach (var root in visualRoots ?? new[] { visualRoot })
        {
            if (root == null) continue;
            foreach (Transform target in root)
            {
                if (!batchNames.Contains(target.name) || !target.TryGetComponent<MeshRenderer>(out var renderer) ||
                    !renderer.enabled) continue;
                renderer.enabled = false;
                suppressed.Add(renderer);
            }
        }
        activeGroup.SetActive(true);
        if(!Application.isPlaying)adapter.InitializeBindings();
    }

    public IEnumerable<RailwayBimStages.Stage> LifecycleStages(string assetId) =>
        occurrences.Any(item=>item.assetId==assetId) ? RailwayBimStages.All : Array.Empty<RailwayBimStages.Stage>();

    public BridgeInfrastructureStore OpenUserStore() => new BridgeInfrastructureStore(catalog);

    public void Explode(float metres)
    {
        ResetView();
        if(loaded.Count==0)return;
        Vector3 center=loaded.Aggregate(Vector3.zero,(sum,item)=>sum+item.renderer.bounds.center)/loaded.Count;
        foreach(var item in loaded)
        {
            if(item.node==null)continue;
            Vector3 direction=(item.renderer.bounds.center-center).normalized;
            item.node.position+=direction*metres;
        }
        Physics.SyncTransforms();
    }

    public void Isolate(string assetId)
    {
        ResetView();
        IsolatedAssetId=assetId;
        foreach(var item in loaded)
            item.renderer.enabled=item.collider.enabled=item.node.name==assetId;
    }

    public void Section(Plane worldPlane)
    {
        ResetView();
        foreach(var item in loaded)
        {
            var vertices=item.original.vertices;
            var normals=item.original.normals;
            var uv=item.original.uv;
            var indices=item.original.triangles;
            var result=new List<Vector3>();
            var resultNormals=new List<Vector3>();
            var resultUv=new List<Vector2>();
            var faces=new List<int>();
            var distances=vertices.Select(vertex=>worldPlane.GetDistanceToPoint(item.node.TransformPoint(vertex))).ToArray();
            if(distances.All(distance=>distance>=0))continue;
            for(int i=0;i<indices.Length;i+=3)
            {
                int origin=result.Count;
                for(int edge=0;edge<3;edge++)
                {
                    int a=indices[i+edge],b=indices[i+(edge+1)%3];
                    if(distances[a]>=0)
                    {
                        result.Add(vertices[a]);
                        resultNormals.Add(normals.Length>0?normals[a]:Vector3.up);
                        resultUv.Add(uv.Length>0?uv[a]:Vector2.zero);
                    }
                    if((distances[a]<0)==(distances[b]<0))continue;
                    float t=distances[a]/(distances[a]-distances[b]);
                    result.Add(Vector3.LerpUnclamped(vertices[a],vertices[b],t));
                    resultNormals.Add(normals.Length>0?Vector3.LerpUnclamped(normals[a],normals[b],t).normalized:Vector3.up);
                    resultUv.Add(uv.Length>0?Vector2.LerpUnclamped(uv[a],uv[b],t):Vector2.zero);
                }
                for(int j=origin+1;j+1<result.Count;j++)
                    faces.AddRange(new[]{origin,j,j+1});
            }
            item.renderer.enabled=item.collider.enabled=faces.Count>0;
            if(faces.Count==0)continue;
            var mesh=new Mesh { indexFormat=result.Count>ushort.MaxValue?IndexFormat.UInt32:IndexFormat.UInt16 };
            mesh.SetVertices(result);
            mesh.SetNormals(resultNormals);
            mesh.SetUVs(0,resultUv);
            mesh.SetTriangles(faces,0);
            sectionMeshes.Add(mesh);
            item.filter.sharedMesh=item.collider.sharedMesh=mesh;
        }
        Physics.SyncTransforms();
    }

    public void ResetView()
    {
        IsolatedAssetId=null;
        foreach(var item in loaded)
        {
            item.node.localPosition=item.position;
            item.renderer.enabled=item.collider.enabled=true;
            item.filter.sharedMesh=item.collider.sharedMesh=item.original;
        }
        foreach(var mesh in sectionMeshes)Release(mesh);
        sectionMeshes.Clear();
        Physics.SyncTransforms();
    }

    public void Unload()
    {
        ResetView();
        if(activeGroup!=null)activeGroup.SetActive(false);
        Release(activeGroup);
        Release(activeCatalog);
        foreach(var renderer in suppressed)if(renderer!=null)renderer.enabled=true;
        suppressed.Clear();
        activeGroup=null;
        ShowingVisual=false;
        activeCatalog=null;
        ActiveAdapter=null;
        loaded.Clear();
        // Resource objects are shared by Unity. ReleaseUnusedAssets runs only after the
        // old renderers and colliders have been released by the zone viewer.
        activeResources.Clear();
        activeChunks.Clear();
    }

    void OnDestroy()=>Unload();
    static void Release(UnityEngine.Object value)
    {
        if(value==null)return;
        if(Application.isPlaying)Destroy(value);
        else DestroyImmediate(value);
    }
}
