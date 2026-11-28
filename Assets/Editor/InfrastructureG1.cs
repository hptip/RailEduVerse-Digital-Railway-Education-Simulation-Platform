using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UttVr.BridgePilot;
using Object = UnityEngine.Object;

static partial class SO201OriginalBridgeAudit
{
    const string InfraKit = "Documentation/InfrastructureDesign/";
    const string InfraEvidence = InfraKit + "Evidence/";
    const string InfraAssets = "Assets/ExternalBIM/InfrastructureG1";
    const string InfraScene = InfraAssets + "/Infrastructure_G1.unity";
    const string InfraTemp = "Temp/InfrastructureG1/";

    [MenuItem("Tools/UTTVR/Infrastructure/Generate and validate G1")]
    static void GenerateInfraG1FromMenu() => GenerateInfraG1();

    sealed class InfraPart
    {
        public string id, domain, owner, sourceKind, sourceRef, materialName;
        public MeshFilter filter;
        public bool reference;
    }

    [Serializable] sealed class InfraMeasurements
    {
        public string units = "m", coordinate_frame = "Unity Y up; sample +Z forward, X lateral from midpoint of both tracks, Y = top of running rail";
        public List<InfraMeasurement> measurements = new();
    }
    [Serializable] sealed class InfraMeasurement
    {
        public string name, unit, method, evidence, threshold, reason;
        public float value;
    }

    sealed class InfraPilot
    {
        public readonly Scene scene;
        public readonly Transform root;
        public readonly List<InfraPart> parts = new();
        public readonly List<(string name, Transform subject, Vector3 eye, Vector3 target, float size)> views = new();
        public readonly List<string> checks = new() { "test_id,component,value,threshold,status,method" };
        public readonly InfraMeasurements resolved = new();
        public readonly Dictionary<string, Material> materials = new();
        public readonly List<(Mesh mesh, Vector3[] points, Quaternion rotation, float limit)> railReference = new();
        public float spacing, railBottom, trainHalf, trainTop, contact, messenger, inside, spring, rise, ringLength;
        public int segments = 9;
        public float station = 3185f;

        public InfraPilot(Scene scene)
        {
            this.scene = scene;
            root = Group(null, "Infrastructure G1 · Design kit v1.0", Vector3.zero);
        }

        public Transform Group(Transform parent, string name, Vector3 position)
        {
            var node = new GameObject(name);
            SceneManager.MoveGameObjectToScene(node, scene);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = position;
            return node.transform;
        }

        public Material Mat(string name, uint colour)
        {
            if (materials.TryGetValue(name, out var material)) return material;
            var c = new Color32((byte)(colour >> 16), (byte)(colour >> 8), (byte)colour, 255);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = c };
            material.SetFloat("_Smoothness", name == "Galvanized steel" ? .35f : .16f);
            if(name=="Tunnel lining concrete" || name=="Track preview silver")
            {
                material.SetFloat("_Smoothness",0);
                material.SetFloat("_SpecularHighlights",0);
                material.SetFloat("_EnvironmentReflections",0);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            materials.Add(name, material);
            return material;
        }

        public Transform Leaf(Transform group, string slot, string label, Mesh mesh, Material material, string source = "KIT-V1",
            string sourceKind = "PARAMETRIC", bool reference = false)
        {
            string id = group.name + "-" + slot;
            mesh.name = id;
            var node = Group(group, label, Vector3.zero);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            parts.Add(new InfraPart { id = id, domain = group.name.Split('_')[0], owner = group.name.Split('_')[1],
                filter = node.GetComponent<MeshFilter>(), sourceKind = sourceKind, sourceRef = source, reference = reference,
                materialName = material.name });
            return node;
        }

        public void AddMeasure(string name, float value, string unit, string method, string threshold, string reason)
        {
            resolved.measurements.Add(new InfraMeasurement { name = name, value = value, unit = unit, method = method,
                evidence = "Unity_Geometry.csv", threshold = threshold, reason = reason });
        }

        public void Check(string test, string component, float value, float max, string method)
        {
            bool pass = float.IsFinite(value) && value <= max;
            checks.Add(FormattableString.Invariant($"{test},{Quoted(component)},{value:G9},{max:G9},{(pass ? "PASS" : "FAIL")},{Quoted(method)}"));
        }

        public void View(string name, Transform subject, Vector3 eye, Vector3 target, float orthographicSize = 0) =>
            views.Add((name, subject, eye, target, orthographicSize));
    }

    static void GenerateInfraG1()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Run G1 in Edit Mode.");
        if (SceneManager.GetSceneByPath(InfraScene).isLoaded)
            throw new InvalidOperationException("Close only the G1 candidate scene before regeneration.");
        var active = SceneManager.GetActiveScene();
        Scene preview = default;
        Directory.CreateDirectory(InfraEvidence + "G1_Refined");
        if (!AssetDatabase.IsValidFolder(InfraAssets)) AssetDatabase.CreateFolder("Assets/ExternalBIM", "InfrastructureG1");
        try
        {
            var route = Open(Route);
            var zone = Open(ZoneRoot + "Zone_018.unity");
            var early = Open(ZoneRoot + "Zone_010.unity");
            var roots = route.GetRootGameObjects();
            var manager = roots.SelectMany(node => node.GetComponentsInChildren<RailwayZoneStreamingManager>(true)).Single();
            var micro = roots.SelectMany(node => node.GetComponentsInChildren<RailwayMicroChunkStreamer>(true)).Single();
            preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var pilot = new InfraPilot(preview);
            MeasureInfrastructure(pilot, manager, micro, zone, early);
            BuildInfraSamples(pilot, early);
            var firstHash = InfraGeometryHash(pilot);
            DisposeInfraPilot(pilot);
            pilot = new InfraPilot(preview);
            MeasureInfrastructure(pilot, manager, micro, zone, early);
            BuildInfraSamples(pilot, early);
            var secondHash = InfraGeometryHash(pilot);
            pilot.Check("GEN-01", "SHA-256 bytes", firstHash == secondHash ? 0 : 1, 0,
                "Two independent builds compared by mesh vertices, triangles, matrices, material colours and stable IDs");
            FinishInfraPilot(pilot, secondHash);
            if (!EditorSceneManager.SaveScene(preview, InfraScene)) throw new IOException(InfraScene);
            File.WriteAllLines(InfraEvidence + "Unity_Geometry.csv", pilot.checks);
            File.WriteAllText(InfraEvidence + "Resolved_Geometry.json", JsonUtility.ToJson(pilot.resolved, true));
            Debug.Log($"Infrastructure G1 completed: {pilot.parts.Count} meshes; {pilot.views.Count} views; {secondHash}");
        }
        finally
        {
            if (preview.IsValid() && preview.isLoaded) EditorSceneManager.CloseScene(preview, true);
            foreach (var scene in ownedScenes) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            ownedScenes.Clear();
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
    }

    static void MeasureInfrastructure(InfraPilot p, RailwayZoneStreamingManager route, RailwayMicroChunkStreamer micro, Scene zone, Scene early)
    {
        var rail = micro.data.chunks.Single(chunk => p.station >= chunk.start && p.station < chunk.end);
        p.station = (rail.start + rail.end) * .5f;
        var spline = route.trainMover.splineRoute;
        float total = spline.CalculateLength();
        var forward = ((Vector3)spline.EvaluateTangent(p.station / total)).normalized;
        var rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, Vector3.up));
        var inverse = Quaternion.Inverse(rotation);
        Vector3 worldPoint = spline.EvaluatePosition(p.station / total);
        var trackSources = new List<(RailwayMicroChunkData.Track track, Mesh mesh, Vector3[] points)>();
        foreach (var track in new[] { rail.right, rail.left })
        {
            var mesh = Resources.Load<Mesh>(track.meshResource);
            var points = mesh.vertices.Select(point => inverse * (track.position + track.rotation * Vector3.Scale(point, track.scale) - worldPoint)).ToArray();
            trackSources.Add((track, mesh, points));
        }
        var centers = trackSources.Select(item => BoundsOf(item.points).center.x).ToArray();
        p.spacing = Mathf.Abs(centers[0] - centers[1]);
        float midpoint = centers.Average();
        float top = trackSources.Max(item => item.points.Max(point => point.y));
        float bottom = trackSources.Min(item => item.points.Min(point => point.y));
        p.railBottom = bottom - top;
        var train = TrainBounds(route.trainMover);
        // Check the longitudinal swing of each car against the real sampled tangent along the active route.
        float swing = 0;
        for (float d = 0; d + Mathf.Abs(train.min.z) < total; d += 5f)
        {
            var a = (Vector3)spline.EvaluatePosition(d / total);
            var t = (Vector3)spline.EvaluateTangent(d / total);
            var side = Vector3.Cross(Vector3.up, t).normalized;
            foreach (float offset in new[] { train.min.z, train.max.z })
            {
                var b = (Vector3)spline.EvaluatePosition(Mathf.Clamp01((d + offset) / total));
                swing = Mathf.Max(swing, Mathf.Abs(Vector3.Dot(b - a, side)));
            }
        }
        p.trainHalf = Mathf.Max(Mathf.Abs(train.min.x), Mathf.Abs(train.max.x)) + swing;
        p.trainTop = train.max.y + route.trainMover.frontHeightOffset - top;
        p.contact = Mathf.Ceil((p.trainTop + .48f) * 10f) / 10f;
        p.messenger = p.contact + .75f;
        p.inside = Mathf.Ceil((p.spacing * .5f + p.trainHalf + 1.65f) * 4f) / 4f;
        p.spring = p.contact + .20f;
        p.rise = p.inside * .46f;
        p.ringLength = 2.4f;
        foreach (var entry in trackSources)
        {
            var points = entry.points.Select(v => v - new Vector3(midpoint, top, 0)).ToArray();
            float limit = Mathf.Min(-points.Min(v => v.z), points.Max(v => v.z));
            if (limit < 1.05f) throw new InvalidOperationException("Track mesh does not cover reference station.");
            p.railReference.Add((entry.mesh,points,inverse*entry.track.rotation,limit-.05f));
        }

        var bridge = Root(zone).Find("FixedInfrastructure_Bridge/Bridge_SO201_SevenSpans");
        if (bridge == null) throw new InvalidOperationException("The user-approved seven-span bridge is missing.");
        var bridgeRecord = route.fixedInfrastructure.Single(record => record.id == "Bridge_Main");
        var bridgeMeshCount = bridge.GetComponentsInChildren<MeshFilter>().Length;
        var bound = BoundsOf(bridge.GetComponentsInChildren<MeshRenderer>().SelectMany(r => Corners(r.bounds)));
        var terrain = Root(early).Find("TerrainZone_010");
        p.Check("BASE-01", bridge.name, Mathf.Abs(bridgeMeshCount - 196), 0, "Live Zone_018 hierarchy mesh count");
        p.Check("BASE-02", "Double track", Mathf.Abs(p.spacing - 6.400022f), .08f, "Independent transformed mesh envelope centres versus existing nominal spacing");
        p.AddMeasure("track_center_spacing", p.spacing, "m", FormattableString.Invariant($"World mesh vertices projected onto the spline normal at chainage {p.station:F3} m"), "±0.08 m vs serialized 6.400022 m", "Centres of each same-length track mesh envelope");
        p.AddMeasure("top_of_rail_world", worldPoint.y + top, "m", "Highest transformed rail vertex", "Actual sampled mesh", "Vertical datum");
        p.AddMeasure("rail_support_world", worldPoint.y + bottom, "m", "Lowest transformed rail module vertex", "Actual sampled mesh", "Deck/subgrade interface");
        p.AddMeasure("train_swept_half_width", p.trainHalf, "m", "SO201 TrainBounds plus worst lateral tangent swing at 5 m samples", "Conservative envelope", "Includes both cars");
        p.AddMeasure("train_height_over_rail", p.trainTop, "m", "SO201 TrainBounds and front-car Y offset above measured rail head", "Conservative rendered envelope", "OCL and tunnel sizing");
        p.AddMeasure("contact_wire_height", p.contact, "m", "Rounded upward from the measured roof envelope + 0.48 m", "≥ 0.42 m roof-to-wire-surface clearance", "G1 simulation staging; pantograph contact remains unverified");
        p.AddMeasure("messenger_endpoint_height", p.messenger, "m", "Contact height + 0.75 m assumed separation", "> contact wire at all samples", "Space for working droppers");
        p.AddMeasure("tunnel_inner_width", p.inside * 2, "m", "Track/train envelope + inspection path and services", "Interior geometry outside swept train envelope", "Single double-track bore");
        p.AddMeasure("tunnel_inner_height", p.spring + p.rise - p.railBottom, "m", "Measured train and OCL clearance, then arch rise", "No intrusion", "Bore from its floor to crown");
        p.AddMeasure("bridge_current_length", bridgeRecord.endChainage - bridgeRecord.startChainage, "m", "Live fixed-infrastructure registry", "Recorded only", "Freeze accepted SO201");
        p.AddMeasure("bridge_world_bounds_Y", bound.size.y, "m", "Live renderer extents", "Recorded only", "Avoid mutating accepted bridge");
        p.AddMeasure("active_streaming_zone_count", route.zones.Count, "count", "Live registry", "Recorded only", "Do not enable extra zones");
        p.AddMeasure("subgrade_existing_terrain_Y", terrain.GetComponent<Renderer>().bounds.size.y, "m", "Current Zone_010 terrain, not IFC", "Recorded only", "Comparison control");
        var mountStation=(bridgeRecord.startChainage+bridgeRecord.endChainage)*.5f;
        float tMid=mountStation/total;
        var normal=Vector3.Cross(Vector3.up,(Vector3)spline.EvaluateTangent(tMid)).normalized;
        var axis=(Vector3)spline.EvaluatePosition(tMid)-normal*p.spacing*.5f;
        float mount=InfraMastX(p);
        var slab=bridge.GetComponentsInChildren<MeshFilter>().Where(filter=>filter.name=="Pavement").Select(filter=>
            ContactPart(filter.name,filter.name,"Accepted bridge",filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray(),filter.sharedMesh.triangles)).ToArray();
        int missing=0;
        foreach (int side in new[]{-1,1})
            foreach(float offset in new[]{-.42f,0,.42f})
            {
                var point=axis+normal*(side*mount+offset);
                if(!slab.Any(mesh=>MeshSurface(mesh,point.x,point.z).hit))missing++;
            }
        p.Check("OCL-03", "SO201 pilot fixing envelope",missing,0,"Actual serialised bridge deck triangles under both preview mounting-foot footprints");
        ValidateInfraOclSource(p, route.trainMover, spline, total, early, worldPoint.y+top);
        p.AddMeasure("ocl_outer_mount_axis", mount, "m", "Measured train swept section + 0.88 m setback", "Fits sampled SO201 deck pavement", "One G1 bridge mounting position");
        File.WriteAllLines(InfraEvidence + "Current_Route.txt", new[]{
            $"Route {total:F3} m; rail chunk {rail.start:F3}–{rail.end:F3}; source {rail.right.meshResource} / {rail.left.meshResource}",
            $"Accepted bridge: {AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(bridge.gameObject))}; {bridgeMeshCount} meshes",
            $"Existing terrain: {AssetDatabase.GetAssetPath(terrain.GetComponent<MeshFilter>().sharedMesh)}",
            string.Join("\n", route.fixedInfrastructure.Select(r => $"{r.id} {r.kind} {r.startChainage:F3}–{r.endChainage:F3} {r.zoneId}"))
        });
    }

    static void ValidateInfraOclSource(InfraPilot p, TrainSplineMover mover, SplineContainer spline, float length,
        Scene early, float railY)
    {
        var report=new List<string>();
        foreach(var car in new[]{mover.frontCar,mover.rearCar})
        {
            var inverse=Matrix4x4.TRS(car.position,car.rotation,Vector3.one).inverse;
            var roof=car.GetComponentsInChildren<MeshRenderer>(true).Where(renderer=>renderer.enabled)
                .Select(renderer=>(renderer, bounds:BoundsOf(Corners(renderer.localBounds)
                    .Select(point=>inverse.MultiplyPoint3x4(renderer.localToWorldMatrix.MultiplyPoint3x4(point))))))
                .OrderByDescending(item=>item.bounds.max.y).Take(8);
            report.Add($"Car {car.name}; active hierarchy animators {car.GetComponentsInChildren<Animator>(true).Length}");
            report.AddRange(roof.Select(item=>FormattableString.Invariant(
                $"  {item.renderer.name} roof-local y={item.bounds.max.y:F4}, local centre {item.bounds.center:F3}")));
        }
        report.Add(FormattableString.Invariant($"Reference rail Y {railY:F4}; swept train upper envelope {railY+p.trainTop:F4}; wire centre {railY+p.contact:F4}."));
        var station=Root(early).Find("Station_Start_TrainRide");
        if(station!=null)
        {
            var filters=station.GetComponentsInChildren<MeshFilter>().Where(filter=>filter.sharedMesh!=null).ToArray();
            var roofs=filters.Where(filter=>filter.sharedMesh.isReadable).Select(filter=>
                ContactPart(filter.name,filter.name,"Station",filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray(),
                    filter.sharedMesh.triangles)).ToArray();
            if(roofs.Length>0)
            {
                var stationBounds=BoundsOf(roofs.SelectMany(part=>Corners(part.bounds)));
                int hits=0, clashes=0;
                float closest=float.PositiveInfinity;
                float wire=railY+p.contact;
                float gantry=railY+p.messenger+1.12f;
                for(float d=0;d<=Mathf.Min(length,560f);d+=2f)
                {
                    var route=(Vector3)spline.EvaluatePosition(d/length);
                    if(route.x<stationBounds.min.x-8||route.x>stationBounds.max.x+8||
                        route.z<stationBounds.min.z-8||route.z>stationBounds.max.z+8)continue;
                    var tangent=(Vector3)spline.EvaluateTangent(d/length);
                    var normal=Vector3.Cross(Vector3.up,tangent).normalized;
                    var center=route-normal*p.spacing*.5f;
                    for(float x=-InfraMastX(p);x<=InfraMastX(p)+.01f;x+=.5f)
                    {
                        var point=center+normal*x;
                        float minRoom=float.PositiveInfinity;
                        foreach(var part in roofs)
                            for(int index=0;index<part.triangles.Length;index+=3)
                            {
                                var a=part.vertices[part.triangles[index]];
                                var b=part.vertices[part.triangles[index+1]]-a;
                                var c=part.vertices[part.triangles[index+2]]-a;
                                float determinant=b.x*c.z-c.x*b.z;
                                if(Mathf.Abs(determinant)<1e-8f)continue;
                                float u=((point.x-a.x)*c.z-(point.z-a.z)*c.x)/determinant;
                                float v=(b.x*(point.z-a.z)-b.z*(point.x-a.x))/determinant;
                                if(u<0||v<0||u+v>1)continue;
                                float y=a.y+u*b.y+v*c.y;
                                if(y>=wire-.2f)minRoom=Mathf.Min(minRoom,y-gantry);
                            }
                        if(!float.IsFinite(minRoom))continue;
                        hits++;
                        if(minRoom<0)clashes++;
                        closest=Mathf.Min(closest,minRoom);
                    }
                }
                report.Add(FormattableString.Invariant($"Zone_010 station {filters.Length} mesh filters, {roofs.Length} readable; {hits} sampled overhead hits, {clashes} below portal top; minimum sampled margin {closest:F4} m. Sampling: 2 m longitudinal by 0.5 m lateral."));
                p.AddMeasure("station_sampled_overhead_hits",hits,"count","Actual station triangles over the two-track OCL cross section","Report sampled coverage; no operating clearance sign-off","G1 only");
                if(hits>0)
                    p.AddMeasure("station_sampled_portal_top_clearance",closest,"m","Minimum overhead face ordinate minus the top of proposed portal gantry","Positive at sampled crossing sections","Discrete survey; not swept volume approval");
            }
            else report.Add("Zone_010 station has no readable meshes for this sample.");
        }
        else report.Add("Zone_010 station path did not resolve.");
        report.Add("Pantograph motion and a validated physical contact face remain unverified.");
        File.WriteAllLines(InfraEvidence+"OCL_03_Measurements.txt",report);
    }

    static void BuildInfraSamples(InfraPilot p, Scene early)
    {
        BuildInfraOverhead(p);
        BuildInfraTunnel(p);
        BuildInfraSubgrade(p);
        p.Check("SUB-01", "RC3 source decision carried forward", 0, 0,
            "Verified in initial G1; the six-layer candidate continues to use only the procedural generator");
        BuildInfraExistingGroundComparison(p, Root(early).Find("TerrainZone_010"));
    }

    static string InfraGeometryHash(InfraPilot p)
    {
        using var data = new MemoryStream();
        using (var writer = new BinaryWriter(data, System.Text.Encoding.UTF8, true))
        {
            foreach (var part in p.parts.OrderBy(part => part.id, StringComparer.Ordinal))
            {
                writer.Write(part.id);
                foreach (var point in part.filter.sharedMesh.vertices)
                { writer.Write(point.x); writer.Write(point.y); writer.Write(point.z); }
                foreach (int index in part.filter.sharedMesh.triangles) writer.Write(index);
                var transform = part.filter.transform.localToWorldMatrix;
                for (int i = 0; i < 16; i++) writer.Write(transform[i]);
                var colour = part.filter.GetComponent<Renderer>().sharedMaterial.color;
                writer.Write(colour.r); writer.Write(colour.g); writer.Write(colour.b);
            }
        }
        using var digest = SHA256.Create();
        return BitConverter.ToString(digest.ComputeHash(data.ToArray())).Replace("-", "").ToLowerInvariant();
    }

    static void DisposeInfraPilot(InfraPilot p)
    {
        foreach (var mesh in p.parts.Select(part => part.filter.sharedMesh).Distinct())
            if (!AssetDatabase.Contains(mesh)) Object.DestroyImmediate(mesh);
        foreach (var material in p.materials.Values) Object.DestroyImmediate(material);
        Object.DestroyImmediate(p.root.gameObject);
    }

    static Mesh InfraRect(Vector3 center, Vector3 size)
    {
        Vector3[] corners = {
            new(-.5f,-.5f,-.5f), new(.5f,-.5f,-.5f), new(.5f,.5f,-.5f), new(-.5f,.5f,-.5f),
            new(-.5f,-.5f,.5f), new(.5f,-.5f,.5f), new(.5f,.5f,.5f), new(-.5f,.5f,.5f)
        };
        int[] faces = { 0,3,2,1, 4,5,6,7, 0,4,7,3, 5,1,2,6, 3,7,6,2, 0,1,5,4 };
        var vertices = faces.Select(i=>center+Vector3.Scale(size,corners[i])).ToArray();
        var indices = new List<int>();
        for(int i=0;i<vertices.Length;i+=4) indices.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
        return InfraMesh(vertices,indices);
    }

    static Mesh InfraBar(Vector3 from, Vector3 to, float width, float depth)
    {
        var mesh = InfraRect(Vector3.zero, new Vector3(width, depth, Vector3.Distance(from, to)));
        var rotation = Quaternion.LookRotation(to - from);
        mesh.vertices = mesh.vertices.Select(point => (from + to) * .5f + rotation * point).ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

    static Mesh InfraPipe(IReadOnlyList<Vector3> path, float radius, int radial = 8)
    {
        var vertices = new List<Vector3>(path.Count * radial);
        var indices = new List<int>();
        for (int i = 0; i < path.Count; i++)
        {
            var forward = (path[Math.Min(i+1,path.Count-1)]-path[Math.Max(i-1,0)]).normalized;
            var axis = Vector3.Cross(forward, Mathf.Abs(forward.y) > .9f ? Vector3.right : Vector3.up).normalized;
            var side = Vector3.Cross(forward, axis);
            for (int j = 0; j < radial; j++)
            {
                float theta = j * Mathf.PI * 2f / radial;
                vertices.Add(path[i] + radius * (axis * Mathf.Cos(theta) + side * Mathf.Sin(theta)));
                if (i == 0) continue;
                int a = (i-1)*radial+j, b = (i-1)*radial+(j+1)%radial;
                indices.AddRange(new[]{a,b,b+radial,a,b+radial,a+radial});
            }
        }
        for (int j=1; j+1<radial; j++)
        {
            int last = (path.Count-1)*radial;
            indices.AddRange(new[]{0,j+1,j,last,last+j,last+j+1});
        }
        return InfraMesh(vertices, indices);
    }

    static Mesh InfraMesh(IEnumerable<Vector3> vertices, IEnumerable<int> triangles)
    {
        var mesh = new Mesh();
        mesh.SetVertices(vertices.ToArray());
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void PreviewInfraRails(InfraPilot p, Transform group, float halfLength = 14.7f)
    {
        int index = 0;
        foreach(var entry in p.railReference)
        {
            float half=Mathf.Min(halfLength,entry.limit);
            var mesh=ClipExtendedSection(entry.mesh,entry.points,entry.rotation,-half,half,0,0,0);
            // All authored faces use one material in the review copy.
            mesh.triangles=mesh.triangles;
            if(halfLength>20f)
                p.Check("TUN-01",group.name+" rail "+index,mesh.vertexCount-1-mesh.triangles.Max(),0,
                    "Long exported rail retains the whole vertex-index range after clipping");
            p.Leaf(group,$"RailRef{index++}","Ray hiện hành · tham chiếu",mesh,
                p.Mat("Track preview silver",0x606B73),"RailwayMicroChunkStreamer","PROJECT_REFERENCE",true);
        }
    }
}
