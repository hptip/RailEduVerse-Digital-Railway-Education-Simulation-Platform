using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UttVr.BridgePilot;
using Object = UnityEngine.Object;

static partial class SO201OriginalBridgeAudit
{
    const string PilotPath = "Assets/ExternalBIM/InfrastructureG2Pilot";
    const string PilotEvidence = InfraEvidence + "G2_Pilot/";
    const string PilotSnapshot = InfraKit + "Rollback/G2_Pilot_PreIntegration_20261127.zip";

    [Serializable] sealed class PilotSectors
    {
        public float overheadStart, overheadEnd, subgradeStart, subgradeEnd;
    }
    [Serializable] sealed class PilotFinalization
    {
        public PilotGradeBand[] subgradeBands;
        public float portalApronMeters,portalWingExtensionMeters;
    }
    [Serializable] sealed class PilotGradeBand { public string kind; public float from,to; }
    [Serializable] sealed class PilotDesign { public PilotFinalization pilotFinalization; }

    sealed class PilotPart
    {
        public string id, slot;
        public Mesh mesh;
        public Material material;
    }

    sealed class RoutePilot
    {
        public readonly Scene scene;
        public readonly Transform context, candidate, legacy, details;
        public readonly RailwayZoneStreamingManager manager;
        public readonly PilotFinalization config;
        public readonly SplineContainer spline;
        public readonly float length, railOffset, spacing, support, wire, messenger, bore, spring, rise, pitch, trainTop;
        public readonly Mesh housing;
        public readonly Dictionary<string,PilotPart[]> templates;
        public readonly List<string> tests=new(){"test_id,subject,value,limit,status,method"};
        public readonly List<string> registry=new(){"asset_id,domain,zone,chunk,chainage,canonical_asset_id,source_kind,visual_batch,detailed_binding,owner_zone"};
        public readonly List<string> performance=new(){"domain,occurrences,visual_renderers,triangles,vertices,index_vertex_bytes_estimate,realtime_lights,mesh_colliders"};
        public readonly Dictionary<(string domain, int chunk, Material mat),List<(Mesh mesh, Matrix4x4 matrix)>> batches=new();
        public readonly HashSet<string> ids=new(StringComparer.Ordinal);
        public readonly Dictionary<Mesh,Mesh> meshes=new();
        public readonly List<ContactMesh> ground=new(), bridge=new(), bridgeEdges=new(), station=new();
        public readonly Dictionary<string,ContactMesh> stationGeometry=new();
        public readonly Dictionary<string,Material> materials=new();
        public readonly List<BridgeBimEntry> catalog=new(){new(){globalId="G2-PILOT",name="Hạ tầng thử nghiệm",ifcClass="Route"}};
        public readonly List<InfrastructureDetailedLoader.Occurrence> occurrences=new();
        public readonly List<Mesh> temporary=new();
        readonly Dictionary<int,(Vector3 point,Quaternion rotation)> routeFrames=new();
        public double minLateral=double.PositiveInfinity;
        public float expansionY;
        public float stationStart,stationEnd;
        public int mountainRenderers,mountainTriangles,mountainVertices;
        public int gradeRenderers,gradeTriangles,gradeVertices;

        public RoutePilot(Scene scene, RailwayZoneStreamingManager manager, Scene g1, RailwayMicroChunkData rail)
        {
            this.scene=scene;
            this.manager=manager;
            config=JsonUtility.FromJson<PilotDesign>(File.ReadAllText(InfraKit+"Design_Configuration.json")).pilotFinalization;
            spline=manager.trainMover.splineRoute;
            length=manager.trainMover.totalRouteLength>0?manager.trainMover.totalRouteLength:spline.CalculateLength();
            var geometry=JsonUtility.FromJson<InfraMeasurements>(File.ReadAllText(InfraEvidence+"Resolved_Geometry.json"));
            float Measurement(string name)=>geometry.measurements.Single(item=>item.name==name).value;
            spacing=Measurement("track_center_spacing");
            float railDatum=Measurement("top_of_rail_world");
            railOffset=railDatum-((Vector3)spline.EvaluatePosition(3175f/length)).y;
            support=Measurement("rail_support_world")-railDatum;
            wire=Measurement("contact_wire_height");
            messenger=Measurement("messenger_endpoint_height");
            trainTop=Measurement("train_height_over_rail");
            bore=Measurement("tunnel_inner_width")*.5f;
            spring=wire+.2f;
            rise=bore*.46f;
            pitch=Measurement("lining_ring_length");
            context=Node(null,"01 · Hiện trường");
            legacy=Node(context,"Công trình trước Pilot");
            candidate=Node(context,"02 · Visual Pilot");
            details=Node(null,"03 · Detailed mẫu theo cụm");
            var prototype=g1.GetRootGameObjects().Single(root=>root.name.StartsWith("Infrastructure G1",StringComparison.Ordinal));
            templates=prototype.transform.Cast<Transform>().ToDictionary(group=>group.name,
                group=>group.GetComponentsInChildren<BridgeBimIdentity>(true).Select(identity=>new PilotPart{
                    id=identity.GlobalId,slot=identity.GlobalId.Substring(group.name.Length+1),
                    mesh=identity.GetComponent<MeshFilter>().sharedMesh,
                    material=identity.GetComponent<MeshRenderer>().sharedMaterial}).ToArray());
            if (rail == null) return;
            foreach(var chunk in rail.chunks)
            {
                if(chunk.start>4400f) continue;
                foreach(var track in new[]{chunk.right,chunk.left})
                {
                    var mesh=Resources.Load<Mesh>(track.meshResource);
                    var railNode=Visual(context,track.meshResource,mesh,track.materials);
                    railNode.SetPositionAndRotation(track.position,track.rotation);
                    railNode.localScale=track.scale;
                }
            }
        }

        public Transform Node(Transform parent,string name)
        {
            var go=new GameObject(name);
            SceneManager.MoveGameObjectToScene(go,scene);
            go.transform.SetParent(parent,false);
            return go.transform;
        }

        public Transform Visual(Transform parent,string name,Mesh mesh,Material[] mats)
        {
            var node=Node(parent,name);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials=mats;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            return node;
        }

        public (Vector3 point,Quaternion rotation) Frame(float distance)
        {
            int key=Mathf.RoundToInt(distance*1000f);
            if(routeFrames.TryGetValue(key,out var frame))return frame;
            float t=Mathf.Clamp01(distance/length);
            var forward=Vector3.ProjectOnPlane((Vector3)spline.EvaluateTangent(t),Vector3.up).normalized;
            var rotation=Quaternion.LookRotation(forward);
            var center=(Vector3)spline.EvaluatePosition(t)+Vector3.up*railOffset-(rotation*Vector3.right)*(spacing*.5f);
            frame=(center,rotation);
            routeFrames.Add(key,frame);
            return frame;
        }

        public Vector3 World(float d,float x,float y)
        {
            var frame=Frame(d);
            return frame.point+frame.rotation*new Vector3(x,y,0);
        }

        public float Surface(IEnumerable<ContactMesh> meshes,Vector3 world)
        {
            float top=float.NegativeInfinity;
            foreach(var mesh in meshes)
            {
                var contact=MeshSurface(mesh,world.x,world.z);
                if(contact.hit)top=Mathf.Max(top,contact.max);
            }
            return top;
        }

        public void Check(string id,string subject,double value,double limit,string method)
        {
            tests.Add(FormattableString.Invariant($"{id},{Quoted(subject)},{value:G9},{limit:G9},{(double.IsFinite(value)&&value<=limit?"PASS":"FAIL")},{Quoted(method)}"));
        }

        public void Record(string id,string subject,string status,string method) =>
            tests.Add($"{id},{Quoted(subject)},,,{status},{Quoted(method)}");

        public Material Independent(Material source)
        {
            if(materials.TryGetValue(source.name,out var mat))return mat;
            // Preserve G1 materials whose only dependency is the installed shader. The mountain uses its own surface.
            mat=new Material(source){name="Pilot · "+source.name};
            foreach(var property in mat.GetTexturePropertyNames())mat.SetTexture(property,null);
            materials.Add(source.name,mat);
            return mat;
        }

        public void Put(string domain,string occurrence,float d,PilotPart part,Mesh mesh,Matrix4x4 matrix,bool detail,bool visual=true)
        {
            int chunk=Mathf.FloorToInt(d/50f);
            var zone=manager.zones.LastOrDefault(item=>d>=item.startChainage-.001f)?.zoneId??"Unmapped";
            var key=(domain,chunk,Independent(part.material));
            if(visual)
            {
                if(!batches.TryGetValue(key,out var parts))batches[key]=parts=new();
                parts.Add((mesh,matrix));
            }
            string id=$"G2-{domain}-{occurrence}-{part.slot}";
            if(!ids.Add(id))throw new InvalidOperationException("Duplicate occurrence "+id);
            string owner=domain=="Tunnel"||occurrence.StartsWith("TunnelMountain_",StringComparison.Ordinal)
                ?manager.fixedInfrastructure.FirstOrDefault(item=>occurrence.Contains(item.id)||occurrence.StartsWith(item.id.Replace("TunnelMountain_",""),StringComparison.Ordinal))?.zoneId??zone
                :occurrence.StartsWith("Bridge",StringComparison.Ordinal)?"Zone_018":zone;
            string batch=visual?BatchName(key):"HIDDEN_IN_VISUAL";
            occurrences.Add(new InfrastructureDetailedLoader.Occurrence{assetId=id,domain=domain,owner=owner,zone=zone,
                assembly=occurrence,sourceId=part.id,visualBatch=batch,chunk=chunk,chainage=d,mesh=mesh,material=key.Item3,placement=matrix});
            registry.Add(FormattableString.Invariant($"{Quoted(id)},{domain},{zone},C{chunk:D3},{d:F3},{Quoted(part.id)},PARAMETRIC,{Quoted(batch)},ON_DEMAND,{owner}"));
            if(!detail)return;
            var local=Object.Instantiate(mesh);
            local.name=id;
            local.vertices=local.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            local.normals=local.normals.Select(matrix.MultiplyVector).ToArray();
            local.RecalculateBounds();
            var node=Visual(details,id,local,new[]{key.Item3});
            node.gameObject.AddComponent<MeshCollider>().sharedMesh=local;
            node.gameObject.AddComponent<BridgeBimIdentity>().Configure(id,new Renderer[]{node.GetComponent<MeshRenderer>()});
            catalog.Add(new BridgeBimEntry{globalId=id,assetId=id,parentGlobalId="G2-PILOT",name=part.slot,
                ifcClass=domain,sourceName=part.id,modeledBounds=local.bounds.size,
                propertyLines=new[]{FormattableString.Invariant($"PARAMETRIC | {zone} | {d:F3} m")}});
            temporary.Add(local);
        }

        public static string BatchName((string domain,int chunk,Material mat) key)=>$"{key.domain}_C{key.chunk:D3}_{key.mat.name}";
        public Matrix4x4 Rigid(float d)=>Matrix4x4.TRS(Frame(d).point,Frame(d).rotation,Vector3.one);

        public Mesh Warp(PilotPart part,float center,float sourceZ=0,float zScale=1f,Func<Vector3,Vector3> placement=null)
        {
            var mesh=Object.Instantiate(part.mesh);
            var vertices=mesh.vertices;
            var normals=mesh.normals;
            for(int i=0;i<vertices.Length;i++)
            {
                var p=placement==null?vertices[i]:placement(vertices[i]);
                float d=center+(p.z-sourceZ)*zScale;
                vertices[i]=World(d,p.x,p.y);
                if(normals.Length==vertices.Length)normals[i]=Frame(d).rotation*normals[i];
            }
            mesh.vertices=vertices;
            if(normals.Length==vertices.Length)mesh.normals=normals;
            if(placement!=null)mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            temporary.Add(mesh);
            return mesh;
        }
    }

    [MenuItem("Tools/UTTVR/Infrastructure/Generate isolated G2 Pilot")]
    static void GenerateG2Pilot()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer || !File.Exists(PilotSnapshot))
            throw new InvalidOperationException("Close Play Mode and verify the persistent pilot rollback before generation.");
        string previousScene=SceneManager.GetActiveScene().path;
        if(SceneManager.GetSceneByPath(PilotPath+"/Infrastructure_G2_Pilot.unity").isLoaded)
            throw new InvalidOperationException("Close the saved pilot scene before replacing its generated contents.");
        Directory.CreateDirectory(PilotEvidence+"Screenshots");
        if(!AssetDatabase.IsValidFolder(PilotPath)) AssetDatabase.CreateFolder("Assets/ExternalBIM","InfrastructureG2Pilot");
        Scene pilot=default,g1=default;
        try
        {
            var route=Open(Route);
            g1=EditorSceneManager.OpenPreviewScene(InfraScene);
            var manager=route.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var rail=route.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<RailwayMicroChunkStreamer>(true)).Single().data;
            var sectors=JsonUtility.FromJson<PilotSectors>(File.ReadAllText("Assets/ProjectLopHoc/Generated/QuestExperienceSectors/Manifest.json"));
            if(manager.zones.Count!=17)throw new InvalidOperationException("Unexpected live zone count.");
            pilot=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var p=new RoutePilot(pilot,manager,g1,rail);
            LoadPilotContext(p,sectors);
            BuildPilotTunnelAndGround(p,sectors);
            BuildPilotOverhead(p,sectors);
            SavePilot(p);
            CapturePilot(p,sectors);
            File.WriteAllLines(PilotEvidence+"Geometry_Test_Results.csv",p.tests);
            File.WriteAllLines(PilotEvidence+"Asset_Registry.csv",p.registry);
            File.WriteAllLines(PilotEvidence+"Performance_Estimate.csv",p.performance);
            Debug.Log($"G2 Pilot saved: {p.ids.Count} physical occurrences; {p.batches.Count} visual batches.");
        }
        finally
        {
            if(pilot.IsValid()&&pilot.isLoaded)EditorSceneManager.CloseScene(pilot,true);
            if(g1.IsValid()&&g1.isLoaded)EditorSceneManager.ClosePreviewScene(g1);
            foreach(var opened in ownedScenes)if(opened.IsValid()&&opened.isLoaded)EditorSceneManager.CloseScene(opened,true);
            ownedScenes.Clear();
            var original=SceneManager.GetSceneByPath(previousScene);
            if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
        }
    }
}
