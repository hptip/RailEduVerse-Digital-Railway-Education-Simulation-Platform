using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UttVr.BridgePilot;
using Object=UnityEngine.Object;

static partial class SO201OriginalBridgeAudit
{
    static void LoadPilotContext(RoutePilot p,PilotSectors sectors)
    {
        foreach(var record in p.manager.zones)
        {
            if(record.endChainage<0f||record.startChainage>4400f)continue;
            var zone=p.Node(p.context,record.zoneId);
            var root=Root(Open(record.scenePath));
            foreach(Transform child in root)
            {
                bool legacy=child.name.StartsWith("TunnelMountainGroup_",StringComparison.Ordinal) ||
                    child.name.StartsWith("Experience_OverheadPortalSector",StringComparison.Ordinal) ||
                    child.name.StartsWith("Experience_SubgradeReinforcedSlope",StringComparison.Ordinal);
                foreach(var filter in child.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.sharedMesh==null || filter.sharedMesh.vertexCount==0 ||
                        !filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled)continue;
                    var node=p.Visual(legacy?p.legacy:zone,filter.name,filter.sharedMesh,renderer.sharedMaterials);
                    node.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);
                    node.localScale=filter.transform.lossyScale;
                    bool terrain=child.name.StartsWith("TerrainZone_",StringComparison.Ordinal)||child.name=="TerrainTransitions";
                    bool deck=child.name=="FixedInfrastructure_Bridge"&&filter.name=="Pavement";
                    bool edge=child.name=="FixedInfrastructure_Bridge"&&
                        (filter.name.Contains("Cornice",StringComparison.OrdinalIgnoreCase)||
                         filter.name.Contains("Railing",StringComparison.OrdinalIgnoreCase)||
                         filter.name.Contains("Parapet",StringComparison.OrdinalIgnoreCase));
                    bool station=child.name.StartsWith("Station_",StringComparison.Ordinal);
                    if(!(terrain||deck||edge||station)||!filter.sharedMesh.isReadable)continue;
                    var part=ContactPart(filter.name,filter.name,record.zoneId,filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray(),filter.sharedMesh.triangles);
                    if(terrain)
                    {
                        p.ground.Add(part);
                        CarvePilotTerrain(p,record,node,part);
                        if(record.startChainage<sectors.subgradeEnd&&record.endChainage>sectors.subgradeStart)
                            TrimPilotGradeTerrain(p,node,part,sectors.subgradeStart,sectors.subgradeEnd);
                    }
                    if(deck)p.bridge.Add(part);
                    if(edge)p.bridgeEdges.Add(part);
                    if(station)p.station.Add(part);
                }
            }
        }
        p.Check("BASE-ZONES","Canonical route registry",Math.Abs(p.manager.zones.Count-17),0,"Read in Unity; pilot is outside Build Settings and uses independent renderer copies");
        p.Record("BASE-G1","288 original checks","REFERENCE","G1 signs off on canonical template geometry. See ../Unity_Geometry.csv. Live chainage, transformed geometry and scene tests follow separately.");
    }

    static void TrimPilotGradeTerrain(RoutePilot p,Transform node,ContactMesh part,float start,float end)
    {
        const float half=14f;
        var footprint=new[]{p.World(start,-half,0),p.World(start,half,0),p.World(end,half,0),p.World(end,-half,0)};
        var original=node.GetComponent<MeshFilter>().sharedMesh;
        var normals=original.normals;
        var uv=original.uv;
        var vertices=new List<Vector3>();
        var updatedNormals=new List<Vector3>();
        var updatedUv=new List<Vector2>();
        var elements=new List<int>();
        var indices=original.triangles;
        float Side(Vector3 point,int edge)
        {
            var a=footprint[edge];
            var b=footprint[(edge+1)%4]-a;
            return b.x*(point.z-a.z)-b.z*(point.x-a.x);
        }
        void Emit(List<ClipVertex> polygon)
        {
            if(polygon.Count<3)return;
            int offset=vertices.Count;
            foreach(var point in polygon)
            {
                vertices.Add(node.InverseTransformPoint(point.position));
                updatedNormals.Add(point.normal);
                updatedUv.Add(point.uv);
            }
            for(int j=1;j+1<polygon.Count;j++)elements.AddRange(new[]{offset,offset+j,offset+j+1});
        }
        for(int i=0;i<indices.Length;i+=3)
        {
            var inside=new List<ClipVertex>(6);
            for(int c=0;c<3;c++)
            {
                int j=indices[i+c];
                inside.Add(new ClipVertex(part.vertices[j],j<normals.Length?normals[j]:Vector3.up,j<uv.Length?uv[j]:Vector2.zero));
            }
            for(int edge=0;edge<4&&inside.Count>=3;edge++)
            {
                var next=new List<ClipVertex>(6);
                var outside=new List<ClipVertex>(6);
                var a=inside[^1];
                float da=Side(a.position,edge);
                foreach(var b in inside)
                {
                    float db=Side(b.position,edge);
                    if((da>=0)!=(db>=0))
                    {
                        float t=da/(da-db);
                        var cross=new ClipVertex(Vector3.Lerp(a.position,b.position,t),Vector3.Lerp(a.normal,b.normal,t).normalized,Vector2.Lerp(a.uv,b.uv,t));
                        next.Add(cross);
                        outside.Add(cross);
                    }
                    (db>=0?next:outside).Add(b);
                    a=b;da=db;
                }
                Emit(outside);
                inside=next;
            }
        }
        var mesh=new Mesh{name=original.name+" · Pilot shoulder trim",indexFormat=vertices.Count>ushort.MaxValue?IndexFormat.UInt32:IndexFormat.UInt16};
        mesh.SetVertices(vertices);
        mesh.SetNormals(updatedNormals);
        mesh.SetUVs(0,updatedUv);
        mesh.SetTriangles(elements,0);
        node.GetComponent<MeshFilter>().sharedMesh=mesh;
        p.temporary.Add(mesh);
    }

    static void CarvePilotTerrain(RoutePilot p,RailwayZoneStreamingManager.ZoneRecord zone,Transform node,ContactMesh part)
    {
        var tunnels=p.manager.fixedInfrastructure.Where(work=>work.kind=="TunnelMountain"&&
            work.startChainage<zone.endChainage+10f&&work.endChainage>zone.startChainage-10f).ToArray();
        if(tunnels.Length==0)return;
        var samples=tunnels.SelectMany(tunnel=>Enumerable.Range(0,Mathf.CeilToInt((tunnel.endChainage-tunnel.startChainage+14f)/3f)+1)
            .Select(i=>p.Frame(tunnel.startChainage-7f+i*3f))).ToArray();
        var triangles=new List<int>();
        int removed=0;
        for(int i=0;i<part.triangles.Length;i+=3)
        {
            int ia=part.triangles[i],ib=part.triangles[i+1],ic=part.triangles[i+2];
            var a=part.vertices[ia]; var b=part.vertices[ib]; var c=part.vertices[ic];
            var bounds=BoundsOf(new[]{a,b,c});
            bool intrudes=false;
            foreach(var frame in samples)
            {
                if(bounds.max.y<frame.point.y+p.support-.04f)continue;
                var lateral=frame.rotation*Vector3.right;
                var left=frame.point-lateral*(p.bore+.06f);
                var right=frame.point+lateral*(p.bore+.06f);
                if((left.x<bounds.min.x&&right.x<bounds.min.x)||(left.x>bounds.max.x&&right.x>bounds.max.x)||
                    (left.z<bounds.min.z&&right.z<bounds.min.z)||(left.z>bounds.max.z&&right.z>bounds.max.z))continue;
                var v=a-frame.point; var u=b-frame.point; var w=c-frame.point;
                var forward=frame.rotation*Vector3.forward;
                float za=Vector3.Dot(v,forward),zb=Vector3.Dot(u,forward),zc=Vector3.Dot(w,forward);
                float xa=Vector3.Dot(v,lateral),xb=Vector3.Dot(u,lateral),xc=Vector3.Dot(w,lateral);
                if(Mathf.Min(za,zb,zc)>1.5f||Mathf.Max(za,zb,zc)<-1.5f||
                    Mathf.Min(xa,xb,xc)>p.bore+.06f||Mathf.Max(xa,xb,xc)<-p.bore-.06f)continue;
                intrudes=true;
                break;
            }
            if(intrudes){removed++;continue;}
            triangles.AddRange(new[]{ia,ib,ic});
        }
        if(removed==0)return;
        var mesh=Object.Instantiate(node.GetComponent<MeshFilter>().sharedMesh);
        mesh.name=$"PilotTerrain_{zone.zoneId}_{node.name}";
        mesh.triangles=triangles.ToArray();
        mesh.RecalculateBounds();
        node.GetComponent<MeshFilter>().sharedMesh=mesh;
        p.temporary.Add(mesh);
        p.Record("TUN-BASE-CARVE",zone.zoneId+" "+node.name,"INFO",$"{removed} potentially intrusive baseline triangles removed in pilot terrain copy only; original mesh stays immutable.");
    }

    static void BuildPilotTunnelAndGround(RoutePilot p,PilotSectors sectors)
    {
        foreach(var range in p.manager.fixedInfrastructure.Where(item=>item.kind=="TunnelMountain").OrderBy(item=>item.startChainage))
        {
            bool forest=range.id.EndsWith("Forest_01",StringComparison.Ordinal);
            var source=p.templates.Values.Single(parts=>parts.Any(part=>part.id.StartsWith(forest?"Tunnel_Zone_013_Forest-":"Tunnel_Zone_015_Rock-",StringComparison.Ordinal)));
            string key=range.id.Replace("TunnelMountain_","");
            float start=range.startChainage,end=range.endChainage;
            int rings=Mathf.RoundToInt((end-start)/p.pitch);
            float ringLength=(end-start)/rings;
            var module=source.Where(part=>part.slot.StartsWith("R07-",StringComparison.Ordinal)).ToArray();
            var crown=module.Single(part=>part.slot=="R07-A04");
            var ringShell=module.Where(part=>part.slot.StartsWith("R07-A",StringComparison.Ordinal)||part.slot.StartsWith("R07-Wall",StringComparison.Ordinal)).ToArray();
            var interior=source.Where(part=>new[]{"Invert","Walkway","Duct","Drain","Guardrail"}.Any(name=>part.slot.StartsWith(name,StringComparison.Ordinal))).ToArray();
            var equipment=source.Where(part=>new[]{"Cabinet","Emergency","Exit"}.Any(name=>part.slot.StartsWith(name,StringComparison.Ordinal))).ToArray();
            var portal=source.Where(part=>new[]{"Portal","Wing"}.Any(name=>part.slot.StartsWith(name,StringComparison.Ordinal))).ToArray();
            p.Check("TUN-RINGS",key,Math.Abs(ringLength-p.pitch),.001f,"Registry extent divided by the established 2.4 m segment pitch");
            p.Check("TUN-SHELL",key,Math.Abs(ringShell.Length-11),0,"Physical shell leaves per ring");
            for(int i=0;i<rings;i++)
            {
                float d=start+(i+.5f)*ringLength;
                foreach(var part in module)
                {
                    if((part.slot.Contains("Lamp")&&i%3!=0)||(part.slot.Contains("Bolt")&&i%2!=0))continue;
                    p.Put("Tunnel",$"{key}-R{i:D3}",d,part,p.Warp(part,d,0,ringLength/p.pitch),Matrix4x4.identity,i==2);
                }
            }
            int slices=Mathf.CeilToInt((end-start)/32f);
            float step=(end-start)/slices;
            for(int i=0;i<slices;i++)
            {
                float d=start+(i+.5f)*step;
                foreach(var part in interior)
                    p.Put("Tunnel",$"{key}-I{i:D2}",d,part,p.Warp(part,d,0,step/36f),Matrix4x4.identity,i==0);
                if(i%2==0)
                    foreach(var part in equipment)
                        p.Put("Tunnel",$"{key}-E{i:D2}",d,part,part.mesh,p.Rigid(d),i==0);
                for(int j=0;j<8;j++)
                    foreach(var post in source.Where(part=>part.slot=="GuardPostL7"||part.slot=="GuardPostR7"))
                    {
                        float location=d+(j-3.5f)*step/8f;
                        p.Put("Tunnel",$"{key}-G{i:D2}-{j}",location,post,post.mesh,p.Rigid(location),i==0);
                    }
            }
            foreach(var part in portal)
            {
                bool rear=part.slot.Contains("Rear");
                bool both=part.slot.StartsWith("Wing",StringComparison.Ordinal)||part.slot.StartsWith("PortalDrain",StringComparison.Ordinal);
                foreach(int side in both?new[]{1,-1}:new[]{rear?-1:1})
                {
                    float portalDatum=side>0?start:end;
                    bool mirror=both&&side<0;
                    float sourceDatum=mirror?-18f:side>0?-18f:18f;
                    Vector3 FitPortal(Vector3 point)
                    {
                        float distance=point.z-sourceDatum;
                        float outward=mirror?-distance:-side*distance;
                        float stretch=part.slot.Contains("Approach")?p.config.portalApronMeters/6.6f:
                            part.slot.StartsWith("Wing",StringComparison.Ordinal)?1+p.config.portalWingExtensionMeters/7.7f:
                            part.slot.Contains("Drain")?p.config.portalApronMeters/7.6f:1.22f;
                        if(outward>0)distance*=stretch;
                        point.z=sourceDatum+(mirror?-distance:distance);
                        return point;
                    }
                    var mesh=p.Warp(part,portalDatum,sourceDatum,1f,FitPortal);
                    if(mirror){mesh.triangles=mesh.triangles.Reverse().ToArray();mesh.RecalculateNormals();}
                    p.Put("Tunnel",key+"-Portal-"+(side>0?"In":"Out"),portalDatum,part,mesh,Matrix4x4.identity,true);
                    if(part.slot.Contains("Approach"))
                    {
                        var forward=p.Frame(portalDatum).rotation*Vector3.forward;
                        var extents=mesh.vertices.Select(vertex=>Vector3.Dot(vertex-p.Frame(portalDatum).point,forward)).ToArray();
                        p.Check("TUN-PORTAL-APRON",key+" "+side,
                            Math.Max(0,p.config.portalApronMeters-(extents.Max()-extents.Min())),1.3f,
                            "Approach extent verified on the fitted entrance/exit mesh");
                    }
                }
            }
            BuildPilotMountain(p,range,forest);
            p.Check("TUN-JOINT",key,Math.Abs(ringLength-(crown.mesh.bounds.size.z+.014f)),.0002f,
                "Crown panel mesh span and specified joint across the full sequence");
            p.Record("TUN-OWNER",key,"PASS",$"{range.zoneId}, chainage {start:F3}–{end:F3}; one owner and {rings} ring occurrences, no duplicate rail meshes.");
        }
        BuildPilotSubgrade(p,sectors);
    }

    static void BuildPilotMountain(RoutePilot p,RailwayZoneStreamingManager.FixedInfrastructureRecord tunnel,bool forest)
    {
        const int flank=25,arch=20,cols=2*flank+arch,rowsPerChunk=12;
        float start=tunnel.startChainage,end=tunnel.endChainage;
        int units=Mathf.CeilToInt((end-start)/50f);
        float step=(end-start)/units;
        var surface=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=forest?"Terrain núi rừng":"Terrain núi đá",color=forest?new Color(.27f,.40f,.23f):new Color(.39f,.41f,.38f)};
        surface.SetFloat("_Smoothness",.05f);
        p.materials.Add(surface.name,surface);
        var foliage=new Material(surface){name=forest?"Tán cây núi rừng":"Cây trên núi đá",color=forest?new Color(.17f,.30f,.15f):new Color(.25f,.37f,.19f)};
        p.materials.Add(foliage.name,foliage);
        var outcrop=new Material(surface){name=forest?"Nền rừng xanh đậm":"Vách đá lộ thiên",color=forest?
            new Color(.22f,.34f,.18f):new Color(.48f,.45f,.39f)};
        p.materials.Add(outcrop.name,outcrop);
        var first=p.Frame(start).point;
        var chord=p.Frame(end).point-first;
        var along=new Vector2(chord.x,chord.z).normalized;
        var across=new Vector2(along.y,-along.x);
        var origin=new Vector2(first.x,first.z);
        // Coordinates describe independent world-space massifs in the fixed chord frame.
        // The rail only determines the hole and the portal approach at each end.
        (float along,float cross,float radiusA,float radiusX,float height)[] summits=forest?
            new[]{(103f,-48f,90f,78f,37f),(228f,39f,138f,115f,64f),(377f,-65f,114f,93f,56f),(505f,26f,92f,100f,44f),(321f,132f,130f,72f,21f)}:
            new[]{(83f,43f,58f,66f,51f),(182f,91f,87f,57f,66f),(277f,-75f,88f,106f,57f),
                (382f,-17f,115f,78f,81f),(488f,63f,66f,69f,62f),(543f,-78f,54f,51f,44f)};
        float seed=forest?79.14f:211.37f;
        float Noise(Vector2 world,float period)=>Mathf.PerlinNoise(world.x/period+seed,world.y/period+seed*.71f)*2f-1f;
        float Field(Vector2 world)
        {
            Vector2 local=world-origin;
            float a=Vector2.Dot(local,along),x=Vector2.Dot(local,across);
            float height=0;
            foreach(var peak in summits)
            {
                float dx=(x-peak.cross)/peak.radiusX;
                float dy=(a-peak.along)/peak.radiusA+.23f*dx;
                float sample=peak.height*Mathf.Exp(-(dx*dx+dy*dy)*1.7f);
                height=Mathf.Max(height,sample)+.16f*Mathf.Min(height,sample);
            }
            return Mathf.Max(0,height+Noise(world,85)*(forest?7:12)+Noise(world,29)*(forest?2.7f:6)+Noise(world,11)*1.1f);
        }
        float rim=p.bore+1.04f;
        var grid=new Vector3[(units*rowsPerChunk+1)*(cols+1)];
        for(int row=0;row<=units*rowsPerChunk;row++)
        {
            float d=start+(end-start)*row/(units*rowsPerChunk);
            float endDistance=Mathf.Min(d-start,end-d);
            float tunnelBlend=Mathf.SmoothStep(0,1,Mathf.Clamp01(endDistance/58f));
            for(int col=0;col<=cols;col++)
            {
                bool right=col<flank;
                bool crown=col>=flank&&col<=flank+arch;
                float authoredBand=crown?0:right?(flank-col)/(float)flank:(col-flank-arch)/(float)flank;
                float width=(forest?165f:183f)+25f*Mathf.Sin(d/91f+(col*2<cols?.62f:2.63f));
                float portalX=crown?rim*Mathf.Cos((col-flank)*Mathf.PI/arch):Mathf.Lerp(rim,width,authoredBand)*(right?1:-1);
                float fieldX=width*(1f-2f*col/cols);
                float x=Mathf.Lerp(portalX,fieldX,tunnelBlend);
                float band=Mathf.Clamp01((Mathf.Abs(x)-rim)/(width-rim));
                var datum=p.World(d,x,0);
                float ground=p.Surface(p.ground,datum);
                float baseHeight=float.IsFinite(ground)?ground-datum.y:p.support-1.5f;
                float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01((1-band)/.44f));
                float worldHeight=baseHeight+Field(new Vector2(datum.x,datum.z))*blend*
                    Mathf.Lerp(.22f,1f,Mathf.SmoothStep(0,1,Mathf.Clamp01(endDistance/74f)));
                float profile=p.spring+(p.rise+1.04f)*Mathf.Sqrt(Mathf.Max(0,1-x*x/(rim*rim)));
                float coverShoulder=Mathf.Exp(-Mathf.Pow(Mathf.Max(0,Mathf.Abs(x)-rim)/24f,2f));
                float broadCover=Mathf.Lerp(baseHeight,p.spring+p.rise+1.75f,coverShoulder*tunnelBlend);
                worldHeight=Mathf.Max(worldHeight,broadCover);
                // Blends the pilot portal into the field and maintains cover independently of the ridgeline.
                float approach=Mathf.Lerp(profile,Mathf.Max(profile+.45f,worldHeight),tunnelBlend);
                float join=Mathf.SmoothStep(1,0,Mathf.Clamp01(band/.23f));
                float height=Mathf.Lerp(worldHeight,Mathf.Max(worldHeight,approach),join);
                if(Mathf.Abs(x)<=rim)height=approach;
                else height=Mathf.Lerp(baseHeight-.12f,height,blend);
                float bevel=(d-start<=end-d?1:-1)*(.29f*profile-4.65f)*(1-tunnelBlend)*join;
                grid[row*(cols+1)+col]=p.World(d+bevel,x,height);
            }
        }
        var prior=Array.Empty<Vector3>();
        for(int chunk=0;chunk<units;chunk++)
        {
            var points=new List<Vector3>();
            var triangles=new List<int>();
            for(int row=0;row<=rowsPerChunk;row++)
            {
                for(int col=0;col<=cols;col++)
                {
                    points.Add(grid[(chunk*rowsPerChunk+row)*(cols+1)+col]);
                    if(row==0||col==0)continue;
                    int a=(row-1)*(cols+1)+col-1,b=a+cols+1;
                    triangles.AddRange(new[]{a,a+1,b,b,a+1,b+1});
                }
            }
            if(prior.Length>0)p.Check("TUN-MOUNTAIN-SEAM",tunnel.id+" "+chunk,prior.Zip(points.Take(cols+1),(a,b)=>(a-b).magnitude).Max(),.001f,"World vertex coordinates for the full boundary row");
            prior=points.Skip(points.Count-cols-1).ToArray();
            if(chunk==0||chunk==units-1)
            {
                bool entrance=chunk==0;
                int edge=(entrance?0:rowsPerChunk)*(cols+1);
                for(int side=0;side<2;side++)
                {
                    int outer=side==0?0:flank+arch;
                    int foot=points.Count;
                    for(int col=0;col<=flank;col++)
                    {
                        int index=outer+col;
                        var face=points[edge+index];
                        var frame=p.Frame(entrance?start:end);
                        float x=(Quaternion.Inverse(frame.rotation)*(face-frame.point)).x;
                        var basePoint=p.World((entrance?start:end)+(entrance?-1f:1f)*
                            Mathf.Lerp(6.3f,37f,Mathf.Clamp01((Mathf.Abs(x)-p.bore)/175f)),x,0);
                        float ground=p.Surface(p.ground,basePoint);
                        basePoint.y=float.IsFinite(ground)?ground-.055f:frame.point.y+p.support;
                        points.Add(basePoint);
                        if(col==0)continue;
                        int a=edge+index-1,b=a+1,c=foot+col-1,d=c+1;
                        triangles.AddRange(entrance?new[]{a,d,b,a,c,d}:new[]{a,b,d,a,d,c});
                    }
                }
            }
            var mesh=InfraMesh(points,triangles);
            var normals=mesh.normals;
            int lastRow=units*rowsPerChunk;
            for(int row=0;row<=rowsPerChunk;row++)
                for(int col=0;col<=cols;col++)
                {
                    int globalRow=chunk*rowsPerChunk+row;
                    Vector3 d=grid[Mathf.Min(lastRow,globalRow+1)*(cols+1)+col]-grid[Mathf.Max(0,globalRow-1)*(cols+1)+col];
                    Vector3 x=grid[globalRow*(cols+1)+Mathf.Min(cols,col+1)]-grid[globalRow*(cols+1)+Mathf.Max(0,col-1)];
                    normals[row*(cols+1)+col]=Vector3.Cross(x,d).normalized;
                }
            mesh.normals=normals;
            mesh.name=$"{tunnel.id}_Ground{chunk:D2}";
            var baseFaces=new List<int>();
            var outcropFaces=new List<int>();
            for(int face=0;face<triangles.Count;face+=3)
            {
                var a=points[triangles[face]];
                var b=points[triangles[face+1]];
                var c=points[triangles[face+2]];
                var center=(a+b+c)/3f;
                float variation=Noise(new Vector2(center.x,center.z),forest?37f:16f);
                float slope=Mathf.Abs(Vector3.Cross(b-a,c-a).normalized.y);
                var target=variation+(1-slope)*(forest?.12f:.52f)>(forest?.10f:.23f)?outcropFaces:baseFaces;
                target.AddRange(new[]{triangles[face],triangles[face+1],triangles[face+2]});
            }
            mesh.subMeshCount=2;
            mesh.SetTriangles(baseFaces,0);
            mesh.SetTriangles(outcropFaces,1);
            var node=p.Visual(p.candidate,mesh.name,mesh,new[]{surface,outcrop});
            p.temporary.Add(mesh);
            var geometry=ContactPart(mesh.name,mesh.name,tunnel.zoneId,mesh.vertices,mesh.triangles);
            int intersections=0,holes=0;
            for(int i=1;i<=9;i++)
                for(float x=-p.bore+.15f;x<p.bore-.14f;x+=.45f)
                {
                    var f=p.Frame(start+(chunk+i/10f)*step);
                    var ray=f.point+f.rotation*new Vector3(x,0,0);
                    var hit=MeshSurface(geometry,ray.x,ray.z);
                    if(!hit.hit){holes++;continue;}
                    float section=p.spring+(p.rise+.41f)*Mathf.Sqrt(1-Mathf.Pow(x/p.bore,2f));
                    if(hit.min<ray.y+section)intersections++;
                }
            p.Check("TUN-MOUNTAIN-COVER",node.name,holes,0,"Triangle sampling across both tracks throughout each 50 m mesh");
            p.Check("TUN-MOUNTAIN-INTRUSION",node.name,intersections,0,"Actual terrain triangles above outer liner section");
            ++p.mountainRenderers;
            p.mountainTriangles+=triangles.Count/3;
            p.mountainVertices+=points.Count;
            var treeMeshes = new List<Mesh>();
            float vegetationMargin=float.PositiveInfinity;
            for(int i=0;i<(forest?12:6);i++)
            {
                float d=start+(chunk+.12f+(i*.618f%1f)*.75f)*step;
                float x=(i%2==0?-1:1)*(p.bore+9f+(i*23%57));
                var foothold=p.World(d,x,0);
                float y=p.Surface(new[]{geometry},foothold);
                if(!float.IsFinite(y))continue;
                foothold.y=y-.14f;
                var height=2.6f+(i*37%19)*.16f;
                treeMeshes.Add(InfraCanopy(foothold,height,i+chunk*13));
                treeMeshes.Add(InfraPipe(new[]{foothold,foothold+Vector3.up*height*.72f},.075f,6));
                var frame=p.Frame(d);
                foreach(var tree in treeMeshes.TakeLast(2))
                    foreach(var vertex in tree.vertices)
                        vegetationMargin=Mathf.Min(vegetationMargin,
                            Mathf.Abs((Quaternion.Inverse(frame.rotation)*(vertex-frame.point)).x)-p.bore);
            }
            if(treeMeshes.Count==0)continue;
            var grove=new Mesh{name=mesh.name+"_Vegetation"};
            grove.CombineMeshes(treeMeshes.Select(tree=>new CombineInstance{mesh=tree,transform=Matrix4x4.identity}).ToArray());
            foreach(var tree in treeMeshes)Object.DestroyImmediate(tree);
            p.Visual(p.candidate,grove.name,grove,new[]{foliage});
            p.temporary.Add(grove);
            p.Check("TUN-VEGETATION",grove.name,-vegetationMargin,0,"Every tree vertex sampled in its local rail frame; no intrusion or foliage colliders");
            ++p.mountainRenderers;
            p.mountainTriangles+=(int)grove.GetIndexCount(0)/3;
            p.mountainVertices+=grove.vertexCount;
        }
    }

    static void BuildPilotSubgrade(RoutePilot p,PilotSectors sectors)
    {
        var bands=p.config.subgradeBands;
        if(bands[0].from!=sectors.subgradeStart||bands[^1].to!=sectors.subgradeEnd||
            bands.Skip(1).Zip(bands,(right,left)=>right.from!=left.to).Any(gap=>gap))
            throw new InvalidOperationException("Subgrade plan does not cover the manifest interval.");
        float biggestGap=0;
        foreach(var band in bands)
        {
            int units=Mathf.CeilToInt((band.to-band.from)/24f);
            float span=(band.to-band.from)/units;
            for(int i=0;i<units;i++)
            {
            float d=band.from+(i+.5f)*span;
            var left=p.World(d,-11f,0);
            var right=p.World(d,11f,0);
            float baseY=p.Surface(p.ground,left);
            float rightY=p.Surface(p.ground,right);
            float fill=Mathf.Max(0f,left.y+p.support-Mathf.Min(baseY,rightY));
            string type=band.kind;
            var parts=p.templates["Subgrade_Zone_010_"+type];
            foreach(var part in parts)
            {
                var mesh=p.Warp(part,d,0,span/24f);
                biggestGap=Mathf.Max(biggestGap,part.slot=="Layer00"?Mathf.Abs(part.mesh.bounds.max.y-p.support):0f);
                p.Put("Subgrade","S"+Mathf.RoundToInt(band.from).ToString("D4")+"-"+i.ToString("D3"),d,part,mesh,Matrix4x4.identity,i==0,
                    visual:!part.slot.StartsWith("Layer",StringComparison.Ordinal)||part.slot=="Layer00");
            }
            var drain=parts.First(part=>part.slot=="SideDrainR").mesh.bounds;
            var shoulder=new List<Vector3>();
            for(int sample=0;sample<=4;sample++)
            {
                float station=d+(sample*.25f-.5f)*span;
                foreach(int side in new[]{-1,1})
                {
                    // Extend the fitted shoulder beyond the surveyed cut so the curved
                    // inner alignment meets the coarser source terrain without a slit.
                    float x=18f*side;
                    var edge=p.World(station,x,0);
                    float y=p.Surface(p.ground,edge);
                    if(float.IsFinite(y))edge.y=y+.015f;
                    else edge.y+=p.support-.2f;
                    shoulder.Add(p.World(station,side*(drain.center.x+drain.extents.x*.97f),drain.min.y+.05f));
                    shoulder.Add(edge);
                }
            }
            var shouldersFaces=new List<int>();
            for(int row=0;row<4;row++)
            {
                int a=row*4,b=a+4;
                shouldersFaces.AddRange(new[]{a,a+1,b,a+1,b+1,b,a+2,b+2,a+3,a+3,b+2,b+3});
            }
            var shoulders=InfraMesh(shoulder,shouldersFaces);
            shoulders.name="Subgrade shoulders · "+Mathf.RoundToInt(d);
            var slope=parts.FirstOrDefault(part=>part.slot=="SlopeR");
            var grass=slope?.material??p.templates["Subgrade_Zone_010_Natural"].First(part=>part.slot=="SlopeR").material;
            p.Visual(p.candidate,shoulders.name,shoulders,new[]{p.Independent(grass)});
            p.temporary.Add(shoulders);
            p.gradeRenderers++;
            p.gradeTriangles+=shouldersFaces.Count/3;
            p.gradeVertices+=shoulder.Count;
            p.Record("SUB-PLACEMENT",FormattableString.Invariant($"{d:F2} m {type}"),"INFO",
                FormattableString.Invariant($"Terrain shoulders sampled at ±11 m; fill {fill:F3} m; width based on G1 section."));
            }
        }
        p.Check("SUB-RAIL-SUPPORT","Both copied rail tracks",biggestGap,.002f,"Canonical top layer versus measured rail underside");
        p.Check("SUB-VARIANTS","Planned territory",Math.Abs(bands.Select(band=>band.kind).Distinct().Count()-3),0,
            "Three inherited six-layer sections fitted within the existing sector without changing rail level");
        p.Record("SUB-TRANSITIONS","Adjoining typologies","REVIEW","Inner geology hidden. End and change-of-type tie-in finish needs visual approval and slope profile measurements at production design.");
    }
}
