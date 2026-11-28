using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

static partial class SO201OriginalBridgeAudit
{
    static float InfraMastX(InfraPilot p) => Mathf.Max(p.spacing * .5f + p.trainHalf + .88f, 6.05f);

    static void BuildInfraOverhead(InfraPilot p)
    {
        var steel = p.Mat("Galvanized steel", 0x83949F);
        var concrete = p.Mat("Concrete", 0xD0D3D1);
        var insulator = p.Mat("Ceramic insulator", 0x55453A);
        var wire = p.Mat("Copper alloy", 0xB37C44);
        var fixture = p.Mat("Fastener steel", 0x666E76);
        const float span = 21f;
        const float sag = .28f;
        p.AddMeasure("ocl_review_span", span, "m", "Fit within measured rail preview segment", "G1 sample only", "Staging span, not permanent support spacing");
        p.AddMeasure("messenger_sag", sag, "m", "Parabolic demonstration cable from two measured anchor locations", "< messenger separation", "Leaves a positive dropper length");
        int variant = 0;
        foreach (string kind in new[] { "OpenTrack", "Bridge", "Tunnel", "Station" })
        {
            string owner = kind == "Tunnel" ? "Zone_013" : kind == "Bridge" ? "Zone_018" : kind == "Station" ? "Zone_010" : "Zone_017";
            var group = p.Group(p.root, $"OCL_{owner}_{kind}", new Vector3(variant++ * 39f, 0, 0));
            PreviewInfraRails(p, group);
            bool tunnel = kind == "Tunnel";
            bool station = kind == "Station";
            float mount = InfraMastX(p);
            if (tunnel) mount = p.inside - .055f;
            float ground = p.railBottom - .15f;
            float H = p.messenger + .58f;
            float baseY = kind == "Bridge" ? p.railBottom - .12f : ground;
            float last = span * .5f;
            p.Leaf(group, "BaseReference", "Bệ lắp mẫu", InfraRect(new Vector3(0, baseY-.21f, 0), new Vector3(mount*2f+1.65f,.42f,27)), concrete,
                   "Preview-fixture", "PREVIEW_CONTEXT", true);
            if(tunnel)
                foreach(int side in new[]{-1,1})
                    p.Leaf(group,side<0?"WallLeft":"WallRight","Mặt thành thử gá",InfraRect(
                        new Vector3(side*(p.inside+.16f),p.spring*.5f+baseY*.5f,0),
                        new Vector3(.32f,p.spring-baseY,23.5f)),concrete,"Tunnel pilot section","PREVIEW_CONTEXT",true);
            for (int stationIndex = 0; stationIndex < 2; stationIndex++)
            {
                float z = stationIndex == 0 ? -last : last;
                if (station)
                {
                    p.Leaf(group, $"P{stationIndex}-PortalCrossbeam", "Dầm cổng", InfraBar(new Vector3(-mount,H,z),new Vector3(mount,H,z),.25f,.24f),steel);
                    for (int i = 0; i < 10; i++)
                    {
                        float x = Mathf.Lerp(-mount,mount,i/10f);
                        float next = Mathf.Lerp(-mount,mount,(i+1)/10f);
                        p.Leaf(group,$"P{stationIndex}-Diagonal{i:00}","Giằng dàn cổng", InfraBar(
                            new Vector3(x,H+(i%2==0?.10f:.47f),z),new Vector3(next,H+(i%2==0?.47f:.10f),z),.065f,.065f),steel);
                    }
                    p.Leaf(group,$"P{stationIndex}-UpperChord","Cánh trên dàn cổng",InfraBar(new Vector3(-mount,H+.48f,z),new Vector3(mount,H+.48f,z),.12f,.12f),steel);
                }
                foreach (int side in new[] {-1,1})
                {
                    string slot = $"P{stationIndex}{(side < 0 ? "L" : "R")}";
                    float x = mount * side, tx = side * p.spacing * .5f;
                    float offset = stationIndex == 0 ? -.105f : .105f;
                    Vector3 contact = new Vector3(tx + offset, p.contact, z);
                    Vector3 messenger = new Vector3(tx+offset,p.messenger,z);
                    if (!tunnel)
                    {
                        float block = kind == "Bridge" ? .24f : .58f;
                        p.Leaf(group,slot+"-Foundation","Móng bê tông",InfraRect(new Vector3(x,baseY+block*.5f,z),new Vector3(.84f,block,.83f)),concrete);
                        float plateY = baseY + block;
                        p.Leaf(group,slot+"-BasePlate","Bản đế",InfraRect(new Vector3(x,plateY+.026f,z),new Vector3(.61f,.052f,.61f)),steel);
                        int anchor = 0;
                        foreach (float dx in new[] {-.222f,.222f})
                            foreach (float dz in new[] {-.222f,.222f})
                            {
                                var a = new Vector3(x+dx,plateY-.10f,z+dz);
                                var b = a + Vector3.up*.27f;
                                p.Leaf(group,$"{slot}-Anchor{anchor:00}","Bu lông neo",InfraPipe(new[]{a,b},.024f,10),fixture);
                                p.Leaf(group,$"{slot}-Nut{anchor++:00}","Đai ốc neo",InfraPipe(new[]{b-.046f*Vector3.up,b-.012f*Vector3.up},.044f,6),steel);
                            }
                        var mast=InfraHColumn(x, z, plateY+.052f, H+.52f);
                        p.Leaf(group,slot+"-Mast","Thân thép chữ H",mast,steel);
                        var profile=ContactPart(slot,"H profile",group.name,mast.vertices,mast.triangles);
                        var axis=MeshSurface(profile,x,z);
                        var recess=MeshSurface(profile,x+.15f,z);
                        var flange=MeshSurface(profile,x+.15f,z+.14f);
                        p.Check("OCL-01",group.name+" "+slot,
                            axis.hit&&!recess.hit&&flange.hit?Mathf.Abs(axis.min-plateY-.052f):float.PositiveInfinity,.002f,
                            "H web and flange present, side recess empty; bottom plane touches base plate");
                    }
                    else
                    {
                        p.Leaf(group,slot+"-WallPad","Bản neo thành hầm",InfraRect(new Vector3(x,p.messenger,z),new Vector3(.19f,.43f,.53f)),steel);
                        for (int j=0;j<4;j++)
                            p.Leaf(group,$"{slot}-WallAnchor{j}","Neo thành",InfraPipe(new [] {
                                new Vector3(x-side*.15f,p.messenger+(j<2?-.14f:.14f),z+(j%2==0?-.17f:.17f)),
                                new Vector3(x+side*.14f,p.messenger+(j<2?-.14f:.14f),z+(j%2==0?-.17f:.17f))
                            },.026f,8),fixture);
                    }
                    var armStart = new Vector3(x,p.messenger+.14f,z);
                    if (station)
                    {
                        p.Leaf(group,slot+"-DropArm","Thanh treo cổng",InfraPipe(new[]{new Vector3(tx,p.messenger,z),new Vector3(tx,H,z)},.046f),steel);
                        armStart = new Vector3(tx+.75f*side,H,z);
                    }
                    var intermediate = Vector3.Lerp(armStart,messenger,.24f);
                    var braceStart = new Vector3(armStart.x, station ? p.messenger+.18f : p.contact+.16f,z);
                    var braceJoint = Vector3.Lerp(armStart,messenger,.80f);
                    var braceKnee = Vector3.Lerp(braceStart,braceJoint,.37f);
                    var locatorBase = Vector3.Lerp(armStart,messenger,.83f);
                    var locatorHinge = locatorBase + Vector3.down*.23f;
                    var armMesh = InfraBar(armStart,messenger,.095f,.087f);
                    var braceMesh = InfraPipe(new[]{braceStart,braceKnee,braceJoint},.032f,10);
                    p.Leaf(group,slot+"-Cantilever","Cần đỡ",armMesh,steel);
                    p.Leaf(group,slot+"-Brace","Thanh giằng",braceMesh,steel);
                    var mainAxis = (messenger-armStart).normalized;
                    p.Leaf(group,slot+"-InsulatorCore","Thân cách điện",InfraPipe(new[]{intermediate-mainAxis*.22f,intermediate+mainAxis*.22f},.057f),insulator);
                    for (int rib = 0; rib < 6; rib++)
                    {
                        var at = intermediate + mainAxis*((rib-2.5f)*.064f);
                        p.Leaf(group,$"{slot}-Rib{rib}","Vành cách điện",InfraCollar(at-mainAxis*.022f,at+mainAxis*.022f,.058f,.108f),insulator);
                    }
                    p.Leaf(group,slot+"-EndSleeve","Ống nối bộ cách điện",
                        InfraCollar(intermediate+mainAxis*.16f,intermediate+mainAxis*.25f,.058f,.081f),steel);
                    p.Leaf(group,slot+"-BraceClevis","Khớp chốt giằng",
                        InfraCollar(braceJoint-Vector3.forward*.11f,braceJoint+Vector3.forward*.11f,.051f,.078f),fixture);
                    p.Leaf(group,slot+"-ArmPlate","Bản mã liên kết cần đỡ",
                        InfraRect(armStart,new Vector3(.26f,.36f,.25f)),fixture);
                    if (!station)
                        p.Leaf(group,slot+"-BracePlate","Bản mã liên kết giằng",
                            InfraRect(braceStart,new Vector3(.26f,.27f,.23f)),fixture);
                    else
                        p.Leaf(group,slot+"-PortalHangerBrace","Giằng treo cổng",
                            InfraBar(braceStart,new Vector3(tx,p.messenger+.18f,z),.063f,.063f),steel);
                    if(tunnel)
                        p.Leaf(group,slot+"-TunnelMountRail","Bản gá dọc thành hầm",
                            InfraBar(braceStart,armStart,.21f,.19f),fixture);
                    p.Leaf(group,slot+"-LocatorHanger","Chốt treo tay định vị",
                        InfraCollar(locatorHinge,locatorBase,.034f,.065f),fixture);
                    p.Leaf(group,slot+"-Locator","Tay định vị",InfraPipe(new[]{locatorHinge,Vector3.Lerp(locatorHinge,contact,.35f)+Vector3.down*.07f,contact},.029f,10),steel);
                    p.Leaf(group,slot+"-Clamp","Kẹp tiếp xúc",
                        InfraCollar(contact-Vector3.forward*.10f,contact+Vector3.forward*.10f,.026f,.052f),fixture);
                    var armContact=MeshSurface(ContactPart(slot,"arm",group.name,armMesh.vertices,armMesh.triangles),braceJoint.x,braceJoint.z);
                    var braceContact=MeshSurface(ContactPart(slot,"brace",group.name,braceMesh.vertices,braceMesh.triangles),braceJoint.x,braceJoint.z);
                    p.Check("OCL-02",group.name+" "+slot+" pin at arm triangle",armContact.hit
                        ? Mathf.Abs(braceJoint.y-(armContact.min+armContact.max)*.5f) : float.PositiveInfinity,.04f,
                        "Vertical sample at physical clevis axis against arm triangles");
                    p.Check("OCL-02",group.name+" "+slot+" brace at pin",braceContact.hit
                        ? Mathf.Min(Mathf.Abs(braceJoint.y-braceContact.min),Mathf.Abs(braceJoint.y-braceContact.max)) : float.PositiveInfinity,.04f,
                        "Vertical sample at physical clevis axis against brace end-cap triangles");
                }
            }
            foreach (int side in new[] {-1,1})
            {
                string slot= side < 0 ? "WireL" : "WireR";
                Vector3 At(float u,bool heavy) => new Vector3(side*p.spacing*.5f+Mathf.Lerp(-.105f,.105f,u),
                    heavy?p.messenger-4*sag*u*(1-u):p.contact,Mathf.Lerp(-last,last,u));
                var samples = Enumerable.Range(0,25).Select(i=>i/24f).ToArray();
                var contactMesh=InfraPipe(samples.Select(u=>At(u,false)).ToArray(),.016f);
                var messengerMesh=InfraPipe(samples.Select(u=>At(u,true)).ToArray(),.017f);
                p.Leaf(group,slot+"-Contact","Dây tiếp xúc",contactMesh,wire);
                p.Leaf(group,slot+"-Messenger","Dây chịu lực",messengerMesh,wire);
                for(int j=1; j<=5; j++)
                {
                    float u=j/6f;
                    Vector3 a=At(u,false),b=At(u,true);
                    p.Leaf(group,$"{slot}-Dropper{j}","Dây treo",InfraPipe(new[]{a,b},.011f),fixture);
                    p.Leaf(group,$"{slot}-Clamp{j}","Kẹp treo",InfraCollar(b-Vector3.forward*.046f,b+Vector3.forward*.046f,.024f,.039f),steel);
                    p.Check("OCL-02",group.name+" "+slot+j,Mathf.Abs(b.y-MeshSurface(ContactPart(slot,"messenger",group.name,messengerMesh.vertices,messengerMesh.triangles),b.x,b.z).max),.021f,"Dropper top versus wire triangle surface");
                }
                p.Check("OCL-02",group.name+" contact clearance",p.trainTop-(contactMesh.bounds.min.y),-.42f,"Measured roof envelope versus bottom vertex of contact cable");
            }
            p.View("OCL_"+kind,group,new Vector3(16,H+9,-27),new Vector3(0,2.9f,0),14.4f);
            if(kind=="OpenTrack") p.View("OCL_Detail",group,new Vector3(mount+4,4,-15),new Vector3(mount-.3f,3,-last),4.2f);
            if(kind=="OpenTrack") p.View("OCL_ArmAssembly",group,new Vector3(8.5f,H+1.5f,-15.8f),new Vector3(4.7f,p.contact+.35f,-last),2.2f);
        }
    }

    static Mesh InfraCollar(Vector3 start,Vector3 end,float tipRadius,float bodyRadius)
    {
        const int sides=12;
        var axis=(end-start).normalized;
        var cross=Vector3.Cross(axis,Mathf.Abs(axis.y)>.8f?Vector3.forward:Vector3.up).normalized;
        var binormal=Vector3.Cross(axis,cross);
        float[] steps={0,.17f,.29f,.71f,.83f,1};
        var points=new List<Vector3>(steps.Length*sides+2);
        var faces=new List<int>();
        for(int row=0;row<steps.Length;row++)
        {
            var center=Vector3.Lerp(start,end,steps[row]);
            float radius=row==0||row==steps.Length-1?tipRadius:bodyRadius;
            for(int col=0;col<sides;col++)
            {
                float angle=2f*Mathf.PI*col/sides;
                points.Add(center+radius*(cross*Mathf.Cos(angle)+binormal*Mathf.Sin(angle)));
                if(row==0)continue;
                int a=(row-1)*sides+col, b=(row-1)*sides+(col+1)%sides;
                faces.AddRange(new[]{a,b,a+sides,b,b+sides,a+sides});
            }
        }
        int cap=points.Count;
        points.Add(start);
        points.Add(end);
        int last=(steps.Length-1)*sides;
        for(int col=0;col<sides;col++)
        {
            int next=(col+1)%sides;
            faces.AddRange(new[]{cap,next,col,cap+1,last+col,last+next});
        }
        return InfraMesh(points,faces);
    }

    static Mesh InfraHColumn(float x,float z,float bottom,float top)
    {
        float a=.21f,b=.16f,t=.047f,w=.044f;
        var profile=new[]{new Vector2(-a,-b),new Vector2(a,-b),new Vector2(a,-b+t),new Vector2(w,-b+t),
            new Vector2(w,b-t),new Vector2(a,b-t),new Vector2(a,b),new Vector2(-a,b),new Vector2(-a,b-t),
            new Vector2(-w,b-t),new Vector2(-w,-b+t),new Vector2(-a,-b+t)};
        var pv = profile.Select(v=>new Vector3(x+v.x,bottom,z+v.y)).Concat(profile.Select(v=>new Vector3(x+v.x,top,z+v.y))).ToArray();
        var tris=new List<int>();
        for(int i=0;i<12;i++){int next=(i+1)%12;tris.AddRange(new[]{i,next+12,next,i,i+12,next+12});}
        foreach(var r in new[]{new[]{0,1,2,11},new[]{10,3,4,9},new[]{8,5,6,7}})
        {
            tris.AddRange(new[]{r[0],r[1],r[2],r[0],r[2],r[3],r[0]+12,r[2]+12,r[1]+12,r[0]+12,r[3]+12,r[2]+12});
        }
        return InfraMesh(pv,tris);
    }

    static void BuildInfraTunnel(InfraPilot p)
    {
        var concrete=p.Mat("Tunnel lining concrete",0x92948E);
        var portalConcrete=p.Mat("Portal face concrete",0xBEC1B9);
        var joint=p.Mat("Lining joint",0x828687);
        var steel=p.Mat("Galvanized steel",0x9DAFB9);
        var safety=p.Mat("Emergency green",0x298451);
        var lamp=p.Mat("Tunnel luminaires",0xE5E7D5);
        lamp.EnableKeyword("_EMISSION");
        lamp.SetColor("_EmissionColor",new Color(.26f,.26f,.25f));
        const int rings=15;
        float end=rings*p.ringLength*.5f, start=-end;
        const float thickness=.41f;
        p.AddMeasure("lining_thickness",thickness,"m","Parametric panel thickness for mock-up","> 0 with no bore intrusion","G1 visual sizing, not engineering");
        p.AddMeasure("lining_panel_count",p.segments+2,"count","Arch tessellation split by physical segment plus two sidewalls","11 independent meshes per ring","Explicit joints");
        p.AddMeasure("lining_ring_length",p.ringLength,"m","Fifteen pitches of the existing precast ring","Panel length < pitch with joint gap","G1 repeatable template");
        p.AddMeasure("tunnel_refined_length",end-start,"m","15 serialized ring pitches","G1 sample only","Excludes projecting portals");
        foreach(bool forest in new[]{true,false})
        {
            string suffix=forest?"Forest":"Rock";
            var group=p.Group(p.root,forest?"Tunnel_Zone_013_Forest":"Tunnel_Zone_015_Rock",new Vector3(forest?0:58,0,74));
            PreviewInfraRails(p,group,24.5f);
            Vector2 Curve(float u,float shell) => new Vector2((p.inside+shell)*Mathf.Cos(Mathf.PI*u),
                p.spring+(p.rise+shell)*Mathf.Sin(Mathf.PI*u));
            Mesh Arc(float u0,float u1,float shell,float z0,float z1,float bevel,float width=thickness,bool rear=false)
            {
                var vertices = new List<Vector3>();
                int steps=u1-u0>.5f?64:8;
                for (int i=0;i<=steps;i++)
                {
                    float u=Mathf.Lerp(u0,u1,i/(float)steps);
                    var a=Curve(u,shell);
                    var b=Curve(u,shell+width);
                    vertices.AddRange(new[]{new Vector3(a.x,a.y,z0+bevel*a.y),new Vector3(b.x,b.y,z0+bevel*b.y),
                        new Vector3(a.x,a.y,z1),new Vector3(b.x,b.y,z1)});
                }
                if(rear)
                {
                    for(int i=0;i<vertices.Count;i++)
                    {
                        var point=vertices[i];
                        point.z=-point.z;
                        vertices[i]=point;
                    }
                    var mirrored=InfraTubeStrip(vertices);
                    mirrored.triangles=mirrored.triangles.Reverse().ToArray();
                    mirrored.RecalculateNormals();
                    return mirrored;
                }
                return InfraTubeStrip(vertices);
            }
            float floor=p.railBottom-.10f;
            p.Leaf(group,"Invert","Bản đáy",InfraRect(new Vector3(0,floor-.24f,0),new Vector3(2*p.inside+.8f,.48f,end-start+.4f)),concrete);
            float seam=.014f;
            float previousEnd=float.NaN;
            for(int ring=0;ring<rings;ring++)
            {
                float z=start+ring*p.ringLength;
                for(int panel=0;panel<p.segments;panel++)
                {
                    float u=panel/(float)p.segments;
                    float v=(panel+1)/(float)p.segments;
                    var panelMesh=Arc(u+.00045f,v-.00045f,0,z+seam*.5f,z+p.ringLength-seam*.5f,0);
                    p.Leaf(group,$"R{ring:D2}-A{panel:D2}","Tấm vòm bê tông lắp ghép",panelMesh,concrete);
                    if(panel==p.segments/2)
                    {
                        var depth=panelMesh.vertices.Select(point=>point.z);
                        if(ring>0)
                            p.Check("TUN-02",group.name+" ring "+ring,Mathf.Abs(depth.Min()-previousEnd-seam),.0001f,
                                "Clear longitudinal joint measured from consecutive crown panel vertices");
                        previousEnd=depth.Max();
                    }
                    if(panel%2==0)
                    {
                        var bolt=Curve((u+v)*.5f,-.016f);
                        Vector3 center=new Vector3(bolt.x,bolt.y,z+.17f);
                        var axis=new Vector3(bolt.x,bolt.y-p.spring,0).normalized;
                        p.Leaf(group,$"R{ring:D2}-Bolt{panel:D2}","Đầu bu lông tấm vòm",
                            InfraPipe(new[]{center-axis*.018f,center+axis*.032f},.036f,6),steel);
                    }
                }
                foreach(int side in new[]{-1,1})
                {
                    string tag=side<0?"L":"R";
                    float x=side*(p.inside+thickness*.5f);
                    p.Leaf(group,$"R{ring:D2}-Wall{tag}","Tấm thành hầm",InfraRect(new Vector3(x,(p.spring+floor)*.5f,z+p.ringLength*.5f),
                        new Vector3(thickness,p.spring-floor,p.ringLength-seam)),concrete);
                    var lampCenter=new Vector3(side*(p.inside-.27f),p.spring-.4f,z+p.ringLength*.5f);
                    var lampNode=p.Leaf(group,$"R{ring:D2}-Lamp{tag}","Đèn trung tính",
                        InfraRect(lampCenter,new Vector3(.13f,.14f,.92f)),lamp);
                    if(ring==2 || ring==5 || ring==8 || ring==11)
                    {
                        // Mesh vertices use module coordinates, so the luminaire Transform is at the module origin.
                        var source=p.Group(lampNode,"Nguồn sáng trắng",lampCenter+new Vector3(-side*.13f,-.08f,0));
                        var light=source.gameObject.AddComponent<Light>();
                        light.type=LightType.Point;light.range=5.2f;light.intensity=1.06f;
                        light.shadows=LightShadows.None;
                        p.Check("TUN-04",group.name+" luminaire "+ring+tag,
                            Vector3.Distance(group.InverseTransformPoint(source.position),lampCenter),.17f,
                            "Actual position of the light source next to its rendered luminaire");
                    }
                }
            }
            const float slant=.29f;
            p.Leaf(group,"PortalArch","Cửa vòm xiên",Arc(0,1,.02f,start-4.65f,start+.72f,slant,1.08f),portalConcrete);
            p.Leaf(group,"PortalRearArch","Cửa vòm phía sau",Arc(0,1,.02f,start-4.65f,start+.72f,slant,1.08f,true),portalConcrete);
            p.Leaf(group,"PortalApproach","Bản nền tiếp cận",InfraRect(new Vector3(0,floor-.16f,start-3.3f),
                new Vector3(2*p.inside+.84f,.30f,6.6f)),joint);
            p.Leaf(group,"PortalRearApproach","Bản nền tiếp cận phía sau",InfraRect(new Vector3(0,floor-.16f,end+3.3f),
                new Vector3(2*p.inside+.84f,.30f,6.6f)),joint);
            foreach(int side in new[]{-1,1})
            {
                string tag=side<0?"L":"R";
                float x=side*(p.inside+.55f);
                float middleY=(p.spring+floor)*.5f;
                p.Leaf(group,"PortalWall"+tag,"Trụ mép vòm",InfraPortalJamb(x,middleY,start,p.spring,floor,false),portalConcrete);
                p.Leaf(group,"PortalRearWall"+tag,"Trụ mép vòm phía sau",InfraPortalJamb(x,middleY,start,p.spring,floor,true),portalConcrete);
                float clearEdge=p.spacing*.5f+p.trainHalf;
                float walkX=side*(clearEdge+.85f);
                p.Leaf(group,"Walkway"+tag,"Lối đi bảo trì",InfraRect(new Vector3(walkX,floor+.22f,0),new Vector3(1.21f,.44f,end-start)),concrete);
                p.Leaf(group,"Guardrail"+tag,"Lan can",InfraPipe(new[]{new Vector3(side*(clearEdge+.44f),floor+1.39f,start),new Vector3(side*(clearEdge+.44f),floor+1.39f,end)},.036f),steel);
                p.Leaf(group,"Duct"+tag,"Máng cáp",InfraRect(new Vector3(side*(p.inside-.25f),floor+.22f,0),new Vector3(.28f,.27f,end-start)),joint);
                p.Leaf(group,"DuctCover"+tag,"Nắp máng cáp",InfraRect(new Vector3(side*(p.inside-.25f),floor+.38f,0),new Vector3(.31f,.05f,end-start)),steel);
                p.Leaf(group,"Drain"+tag,"Rãnh thoát nước",InfraChannel(side*(p.inside-.65f),floor+.03f,end-start,.29f,.15f),joint);
                for(int guard=0;guard<rings;guard++)
                    p.Leaf(group,$"GuardPost{tag}{guard}","Trụ lan can",InfraPipe(new[]{
                        new Vector3(side*(clearEdge+.44f),floor+.43f,start+guard*p.ringLength),
                        new Vector3(side*(clearEdge+.44f),floor+1.39f,start+guard*p.ringLength)},.036f),steel);
                p.Leaf(group,"ExitSign"+tag,"Biển thoát hiểm xanh",InfraRect(new Vector3(side*(p.inside-.2f),floor+2.38f,-3.9f),new Vector3(.20f,.31f,.84f)),safety);
                float signX=side*(p.inside-.32f);
                var arrow=new Vector3(signX,floor+2.38f,-3.9f);
                p.Leaf(group,"ExitIcon"+tag,"Mũi tên thoát hiểm",InfraBar(arrow+Vector3.back*.25f,arrow+Vector3.forward*.20f,.018f,.045f),lamp);
                foreach(int arrowSide in new[]{-1,1})
                    p.Leaf(group,$"ExitArrow{tag}{arrowSide}","Cánh mũi tên",InfraBar(arrow+Vector3.forward*.20f,arrow+Vector3.up*(arrowSide*.09f),.02f,.04f),lamp);
                p.Leaf(group,"Cabinet"+tag,"Tủ kỹ thuật và thông tin",InfraRect(new Vector3(side*(p.inside-.34f),floor+1.08f,.8f),new Vector3(.42f,1.29f,.60f)),steel);
                p.Leaf(group,"CabinetDoor"+tag,"Mặt cửa tủ",InfraRect(new Vector3(side*(p.inside-.56f),floor+1.08f,.8f),new Vector3(.035f,1.20f,.54f)),joint);
                p.Leaf(group,"CabinetHandle"+tag,"Tay nắm cửa tủ",InfraRect(new Vector3(side*(p.inside-.59f),floor+1.08f,.62f),new Vector3(.041f,.145f,.031f)),steel);
                p.Leaf(group,"Emergency"+tag,"Hộp thiết bị an toàn",InfraRect(new Vector3(side*(p.inside-.2f),floor+1.97f,3.7f),new Vector3(.25f,.5f,.42f)),p.Mat("Safety equipment",0xA93935));
                float trackX = side * p.spacing * .5f;
                p.Leaf(group,"InternalContact"+tag,"Dây tiếp xúc",InfraPipe(new[]{new Vector3(trackX,p.contact,start),new Vector3(trackX,p.contact,end)},.015f),p.Mat("Copper alloy",0xB37C44));
                p.Leaf(group,"InternalMessenger"+tag,"Dây chịu lực",InfraPipe(new[]{new Vector3(trackX,p.messenger,start),new Vector3(trackX,p.messenger,end)},.016f),p.Mat("Copper alloy",0xB37C44));
                for(int arm=0;arm<5;arm++)
                {
                    float at=(arm-2)*7.2f;
                    p.Leaf(group,"Bracket"+tag+arm,"Giá đỡ thành hầm",InfraPipe(new []{new Vector3(side*p.inside,p.messenger-.1f,at),new Vector3(trackX,p.messenger,at)},.047f),steel);
                    p.Leaf(group,"InternalDropper"+tag+arm,"Dây treo",InfraPipe(new[]{new Vector3(trackX,p.contact,at),new Vector3(trackX,p.messenger,at)},.011f),steel);
                }
                float wing=forest?3.25f:5.45f;
                Vector3 a=new Vector3(side*(p.inside+.64f),floor+1.47f,start-2.4f);
                Vector3 b=new Vector3(side*(p.inside+wing),floor+.61f,start-7.7f);
                p.Leaf(group,"Wing"+tag,"Tường cánh cửa hầm",InfraBar(a,b,forest?.46f:.70f,2.4f),portalConcrete);
                Vector3 cap=Vector3.up*1.15f;
                p.Leaf(group,"WingCoping"+tag,"Mũ tường cánh",InfraBar(a+cap,b+cap,forest?.61f:.85f,.18f),portalConcrete);
                p.Leaf(group,"PortalDrain"+tag,"Rãnh thoát nước ngoài hầm",InfraChannel(side*(p.inside+.67f),floor-.04f,7.6f,.42f,.20f,start-3.8f),joint);
            }
            BuildInfraMountain(p,group,forest,start,end,Curve);
            p.Check("TUN-01",group.name,p.spacing*.5f+p.trainHalf-p.inside,-1.5f,"Swept side envelope versus wall start");
            p.Check("TUN-02",group.name,seam,.02f,"Panel ring joint, explicit longitudinal seam");
            p.Check("TUN-04",group.name,p.trainTop-(p.contact-.016f),-.40f,"Equipment above rail centre");
            p.View("Tunnel_"+suffix+"_Portal",group,new Vector3(2.7f,p.spring*.98f,start-25f),new Vector3(0,6.3f,start),17f);
            p.View("Tunnel_"+suffix+"_Elevated",group,new Vector3(31,50,-57),new Vector3(0,7,0),32f);
            p.View("Tunnel_"+suffix+"_Slope",group,new Vector3(-42,18,-48),new Vector3(-4,6,-4),29f);
            if(forest)
            {
                p.View("Tunnel_Interior",group,new Vector3(0,2.9f,start+.9f),new Vector3(0,3.1f,end));
                p.View("Tunnel_LiningDetail",group,new Vector3(5,2.8f,start+3f),new Vector3(4.5f,6.7f,start+10f));
            }
        }
    }

    static Mesh InfraPortalJamb(float x,float middle,float start,float spring,float floor,bool rear)
    {
        var mesh=InfraRect(new Vector3(x,middle,start-1.88f),new Vector3(1.06f,spring-floor,5.20f));
        mesh.vertices=mesh.vertices.Select(point=>
        {
            if(point.z<start-1.88f)point.z+=.29f*point.y;
            if(rear)point.z=-point.z;
            return point;
        }).ToArray();
        if(rear)mesh.triangles=mesh.triangles.Reverse().ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh InfraTubeStrip(List<Vector3> vertices)
    {
        var tris=new List<int>();
        for(int i=0;i+7<vertices.Count;i+=4)
            for(int side=0;side<4;side++)
            {
                int[] order={0,2,3,1};
                int a=i+order[side],b=i+order[(side+1)%4];
                tris.AddRange(new[]{a,b,b+4,a,b+4,a+4});
            }
        tris.AddRange(new[]{0,1,3,0,3,2});
        int e=vertices.Count-4;
        tris.AddRange(new[]{e,e+2,e+3,e,e+3,e+1});
        return InfraMesh(vertices,tris);
    }

    static Mesh InfraChannel(float x,float y,float length,float width,float depth,float centerZ=0)
    {
        Vector2[] section={new(x-width*.5f,y+depth),new(x-width*.5f+.065f,y+.07f),new(x+width*.5f-.065f,y+.07f),new(x+width*.5f,y+depth)};
        var vertices=section.Select(a=>new Vector3(a.x,a.y,centerZ-length*.5f)).Concat(section.Select(a=>new Vector3(a.x,a.y,centerZ+length*.5f))).ToArray();
        var tris=new List<int>();
        for(int i=0;i<3;i++)tris.AddRange(new[]{i,i+4,i+1,i+1,i+4,i+5});
        return InfraMesh(vertices,tris);
    }
}
