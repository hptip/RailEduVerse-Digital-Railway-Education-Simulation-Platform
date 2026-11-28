using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UttVr.BridgePilot;
using Object = UnityEngine.Object;

static partial class SO201OriginalBridgeAudit
{
    static void FinishInfraPilot(InfraPilot p, string fingerprint)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)throw new InvalidOperationException("URP Lit unavailable");
        const string catalogPath = InfraAssets + "/G1_CanonicalGeometry.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<BridgeBimCatalog>(catalogPath);
        if(catalog==null)
        {
            catalog=ScriptableObject.CreateInstance<BridgeBimCatalog>();
            AssetDatabase.CreateAsset(catalog,catalogPath);
        }
        var previous=AssetDatabase.LoadAllAssetsAtPath(catalogPath).Where(asset=>asset!=catalog)
            .ToDictionary(asset=>asset.name, StringComparer.Ordinal);
        T Persist<T>(T item) where T : Object
        {
            if(previous.TryGetValue(item.name,out var saved))
            {
                if(item is Mesh mesh && saved is Mesh destination)
                {
                    destination.Clear(false);
                    destination.indexFormat=mesh.indexFormat;
                    destination.vertices=mesh.vertices;
                    destination.normals=mesh.normals;
                    destination.uv=mesh.uv;
                    destination.subMeshCount=mesh.subMeshCount;
                    for(int index=0;index<mesh.subMeshCount;index++)
                        destination.SetTriangles(mesh.GetTriangles(index),index);
                    destination.RecalculateBounds();
                    destination.UploadMeshData(false);
                }
                else EditorUtility.CopySerialized(item,saved);
                Object.DestroyImmediate(item);
                EditorUtility.SetDirty(saved);
                return (T)saved;
            }
            AssetDatabase.AddObjectToAsset(item,catalog);
            return item;
        }

        var mats = p.materials.ToDictionary(pair=>pair.Key,pair=>Persist(pair.Value));
        var entries = new List<BridgeBimEntry>{new() {globalId="INFRA-G1",name="UTTVR · Mẫu hạ tầng",ifcClass="Route"}};
        foreach(Transform group in p.root)
            entries.Add(new BridgeBimEntry{globalId=group.name,parentGlobalId="INFRA-G1",name=group.name,ifcClass="Assembly"});
        var physical=p.parts.Where(part=>!part.reference).ToArray();
        var rows=new List<string>{"asset_id,domain,owner_zone,source_kind,source_ref,geometry_ref,visual_ref,detailed_ref,physical_slot"};
        foreach(var part in p.parts)
        {
            var node=part.filter.gameObject;
            var renderer=node.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=mats[part.materialName];
            var mesh=Persist(part.filter.sharedMesh);
            part.filter.sharedMesh=mesh;
            var collider=node.AddComponent<MeshCollider>();
            collider.sharedMesh=mesh;
            if(part.reference)continue;
            var identity=node.AddComponent<BridgeBimIdentity>();
            identity.Configure(part.id,new Renderer[]{renderer});
            entries.Add(new BridgeBimEntry{globalId=part.id,assetId=part.id,parentGlobalId=node.transform.parent.name,ifcClass=part.domain,
                name=node.name,sourceName=part.sourceRef,modeledBounds=mesh.bounds.size,
                propertyLines=new[]{$"{part.sourceKind} | {part.sourceRef}","G1 | Mẫu kiểm thử hình học, chờ duyệt."}});
            rows.Add(string.Join(",",new[]{part.id,part.domain,part.owner,part.sourceKind,part.sourceRef,catalogPath+"#"+mesh.name,
                InfraScene+"#"+part.id,InfraScene+"#"+part.id,part.id}.Select(Quoted)));
        }
        catalog.schema="UTTVR-INFRA-G1";
        catalog.modeledScale=1;
        catalog.sourceSha256=fingerprint;
        catalog.entries=entries.ToArray();
        var adapter=p.root.gameObject.AddComponent<BridgeBimAdapter>();
        adapter.catalog=catalog;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        AssetDatabase.ImportAsset(catalogPath,ImportAssetOptions.ForceSynchronousImport);
        File.WriteAllLines(InfraEvidence+"Asset_Registry.csv",rows);
        File.WriteAllText(InfraEvidence+"Unity_Geometry_Fingerprint.txt",fingerprint);
        p.Check("BIM-01","Unique leaf IDs",physical.Length-physical.Select(item=>item.id).Distinct().Count(),0,"Stable occurrence IDs from module/physical slot; no rows for references/groups");
        p.Check("BIM-01","Colliders match canonical mesh",physical.Count(item=>item.filter.GetComponent<MeshCollider>().sharedMesh!=item.filter.sharedMesh),0,"Detailed collider is the same physical mesh");
        p.Check("GEN-02","Uniform unit scale",p.parts.Max(item=>(item.filter.transform.lossyScale-Vector3.one).sqrMagnitude),.000001f,"Asset positions in metres; no second scale on scene instances");
        p.Check("GEN-02","Meshes and material",p.parts.Count(item=>item.filter.sharedMesh.vertexCount==0 || item.filter.GetComponent<Renderer>().sharedMaterial==null),0,"Serialized preview geometry, not empty BIM labels");
        p.Check("GEN-02","Finite geometry",p.parts.Count(item=>item.filter.sharedMesh.vertices.Any(v=>
            !float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z))),0,"All source and generated mesh coordinates are finite");
        foreach(var shot in p.views) InfraShot(p.scene,shot.name,shot.subject,shot.eye,shot.target,shot.size);
        var forest=p.root.Find("Tunnel_Zone_013_Forest");
        using(var section=new InfraReview(forest))
        {
            section.Section(Vector3.forward,0);
            InfraShot(p.scene,"Tunnel_Section",forest,new Vector3(0,5.1f,-48),new Vector3(0,5.1f,0),13f);
        }
        ValidateReviewModes(p, adapter);
    }

    static void ValidateReviewModes(InfraPilot p, BridgeBimAdapter adapter)
    {
        var group=p.root.Find("Subgrade_Zone_010_Reinforced");
        var leaf=p.parts.Single(part=>part.id==group.name+"-Layer02");
        var mesh=leaf.filter.sharedMesh;
        var pose=leaf.filter.transform.localPosition;
        var renderer=leaf.filter.GetComponent<MeshRenderer>();
        Material material=renderer.sharedMaterial;
        typeof(BridgeBimAdapter).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(adapter,null);
        using var preview=new InfraReview(group);
        bool selected=adapter.SelectAsset(leaf.id,false) && adapter.Selected.assetId==leaf.id;
        var faceOrigin=group.TransformPoint(new Vector3(0,mesh.bounds.center.y,-15f));
        var hit=preview.Pick(new Ray(faceOrigin,Vector3.forward));
        bool fromMesh=hit!=null && hit.GlobalId==leaf.id && adapter.SelectAsset(hit.GlobalId,false);
        p.Check("BIM-02","Catalog ↔ geometry",selected && fromMesh?0:1,0,"BridgeBimAdapter selection plus real collider ray returns same stable ID");
        preview.Explode(.72f);
        p.Check("BIM-02","Exploded transforms",(leaf.filter.transform.localPosition-pose).sqrMagnitude>.1f?0:1,0,"Layered module separation only");
        InfraShot(p.scene,"BIM_Exploded",group,new Vector3(16,13,-19),new Vector3(0,1,0),13.5f);
        preview.Section(Vector3.forward,0);
        var removed=preview.Pick(new Ray(group.TransformPoint(new Vector3(0,mesh.bounds.center.y,-8f)),Vector3.right));
        p.Check("BIM-02","Section picking",removed==null&&leaf.filter.sharedMesh!=mesh?0:1,0,"Real clipped mesh and matching MeshCollider; removed half cannot be picked");
        InfraShot(p.scene,"BIM_Section",group,new Vector3(8,5,-22),new Vector3(0,-.75f,2),12f);
        preview.Isolate(leaf.id,true);
        bool disabled=preview.Pick(new Ray(group.TransformPoint(new Vector3(0,p.railBottom-.10f,-15)),Vector3.forward))==null;
        p.Check("BIM-02","Isolation picking",disabled && leaf.filter.GetComponent<MeshCollider>().enabled?0:1,0,"Ghosted leaves cannot intercept ray, isolated leaf stays selectable");
        InfraShot(p.scene,"BIM_Isolation",group,new Vector3(19,10,-22),new Vector3(0,-.55f,0),13f);
        preview.Reset();
        bool reset=leaf.filter.sharedMesh==mesh&&renderer.sharedMaterial==material&&leaf.filter.transform.localPosition==pose
            && leaf.filter.GetComponent<MeshCollider>().enabled;
        p.Check("BIM-02","Reset",reset?0:1,0,"Reversible mesh, material, pose and collider");
        adapter.Clear();
    }

    sealed class InfraReview : IDisposable
    {
        readonly Transform group;
        readonly (MeshFilter filter, Mesh mesh, Renderer renderer, Material material, MeshCollider collider, bool enabled, Vector3 position)[] original;
        readonly List<Object> generated=new();
        public InfraReview(Transform group)
        {
            this.group=group;
            original=group.GetComponentsInChildren<MeshFilter>().Select(filter=>
            {
                var renderer=filter.GetComponent<Renderer>();
                var collider=filter.GetComponent<MeshCollider>();
                return (filter,filter.sharedMesh,renderer,renderer.sharedMaterial,collider,collider.enabled,filter.transform.localPosition);
            }).ToArray();
        }
        public void Explode(float amount)
        {
            Reset();
            var layers=original.Where(item=>item.filter.name.StartsWith("Lớp",StringComparison.Ordinal) ||
                item.filter.name is "Cấp phối gia cố" or "Đất đắp trên" or "Thân nền" or "Đất tự nhiên")
                .OrderBy(item=>item.mesh.bounds.center.y).ToArray();
            for(int i=0;i<layers.Length;i++) layers[i].filter.transform.localPosition+=Vector3.up*(i*amount);
            foreach(var item in original.Except(layers))
                item.filter.transform.localPosition+=Mathf.Abs(item.mesh.bounds.center.x)>4
                    ? Vector3.right*(Mathf.Sign(item.mesh.bounds.center.x)*amount*3f)
                    : Vector3.up*(layers.Length*amount);
            Physics.SyncTransforms();
        }
        public void Section(Vector3 axis,float position)
        {
            Reset();
            Quaternion orientation=Quaternion.FromToRotation(axis,Vector3.forward);
            Quaternion inverse=Quaternion.Inverse(orientation);
            foreach(var item in original)
            {
                var vertices=item.mesh.vertices.Select(point=>orientation*point).ToArray();
                var clipped=ClipExtendedSection(item.mesh,vertices,orientation,position,float.PositiveInfinity,0,0,0);
                if(clipped==null)
                {
                    item.renderer.enabled=false;
                    item.collider.enabled=false;
                    continue;
                }
                clipped.vertices=clipped.vertices.Select(point=>inverse*point).ToArray();
                clipped.normals=clipped.normals.Select(normal=>inverse*normal).ToArray();
                clipped.RecalculateBounds();
                generated.Add(clipped);
                item.filter.sharedMesh=clipped;
                item.collider.sharedMesh=clipped;
            }
            Physics.SyncTransforms();
        }
        public void Isolate(string id,bool ghost)
        {
            Reset();
            foreach(var item in original)
            {
                if(item.filter.GetComponent<BridgeBimIdentity>()?.GlobalId==id) continue;
                item.collider.enabled=false;
                if(!ghost) { item.renderer.enabled=false;continue; }
                var material=new Material(item.material);
                generated.Add(material);
                Color colour=material.color;colour.a=.17f;material.color=colour;
                material.renderQueue=(int)RenderQueue.Transparent;
                material.SetFloat("_Surface",1);
                material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite",0);
                material.SetOverrideTag("RenderType","Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                item.renderer.sharedMaterial=material;
            }
            Physics.SyncTransforms();
        }
        public BridgeBimIdentity Pick(Ray ray)
        {
            foreach(var hit in Physics.RaycastAll(ray,80f).OrderBy(hit=>hit.distance))
                if(hit.transform.IsChildOf(group)&&hit.collider.TryGetComponent<BridgeBimIdentity>(out var identity))return identity;
            return null;
        }
        public void Reset()
        {
            foreach(var item in original)
            {
                item.filter.sharedMesh=item.mesh;
                item.collider.sharedMesh=item.mesh;
                item.collider.enabled=item.enabled;
                item.renderer.sharedMaterial=item.material;
                item.renderer.enabled=true;
                item.filter.transform.localPosition=item.position;
            }
            foreach(var item in generated) Object.DestroyImmediate(item);
            generated.Clear();
            Physics.SyncTransforms();
        }
        public void Dispose()=>Reset();
    }

    static void InfraShot(Scene scene,string name,Transform subject,Vector3 eye,Vector3 target,float ortho,
        string output = InfraEvidence+"G1_Refined/", float nearClip = .05f)
    {
        if(name=="Subgrade_Existing_Comparison")return;
        var capture=EditorSceneManager.NewPreviewScene();
        var copy=Object.Instantiate(subject.gameObject);
        SceneManager.MoveGameObjectToScene(copy,capture);
        copy.transform.SetPositionAndRotation(subject.position,subject.rotation);
        foreach(var node in copy.GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;
        var cameraNode=new GameObject("Evidence Camera",typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraNode,capture);
        var camera=cameraNode.GetComponent<Camera>();
        camera.scene=capture;
        camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(capture);
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.63f,.72f,.77f);
        camera.cullingMask=1<<31;
        camera.nearClipPlane=nearClip;
        camera.farClipPlane=Mathf.Max(250f,ortho*8);
        camera.orthographic=ortho>0;
        camera.orthographicSize=ortho;
        camera.fieldOfView=72f;
        camera.transform.SetPositionAndRotation(subject.TransformPoint(eye),Quaternion.LookRotation(target-eye,Vector3.up));
        var lightNode=new GameObject("Evidence Key",typeof(Light));
        SceneManager.MoveGameObjectToScene(lightNode,capture);
        var light=lightNode.GetComponent<Light>();
        bool interior=name=="Tunnel_Interior" || name=="Tunnel_LiningDetail" ||
            name.EndsWith("_Cabin",StringComparison.Ordinal)||name.EndsWith("_Interior_OCL",StringComparison.Ordinal);
        light.type=LightType.Directional;light.intensity=interior?.62f:1.15f;light.cullingMask=1<<31;
        lightNode.transform.rotation=Quaternion.Euler(42,-135,0);
        var render=new RenderTexture(1280,800,24);
        var result=new Texture2D(1280,800,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=render};
            RenderPipeline.SubmitRenderRequest(camera,request);
            RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=render;
            result.ReadPixels(new Rect(0,0,1280,800),0,0);
            result.Apply();
            File.WriteAllBytes(output+name+".png",result.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;
            Object.DestroyImmediate(render);Object.DestroyImmediate(result);
            EditorSceneManager.ClosePreviewScene(capture);
        }
    }
}
