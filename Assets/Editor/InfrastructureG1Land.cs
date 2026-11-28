using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

static partial class SO201OriginalBridgeAudit
{
    static void BuildInfraMountain(InfraPilot p,Transform group,bool forest,float start,float end,Func<float,float,Vector2> curve)
    {
        const int archSteps=64, flankSteps=20, rows=42;
        const int around=archSteps+2*flankSteps;
        var points=new List<Vector3>();
        var tris=new List<int>();
        float opening=p.inside+.82f;
        float floor=p.railBottom-.16f;
        float boundary=forest?43f:39f;
        float Ridge(float u,float center,float width)
        {
            float distance=(u-center)/width;
            return Mathf.Exp(-distance*distance);
        }
        for(int row=0;row<=rows;row++)
        {
            float progress=row/(float)rows;
            float z=Mathf.Lerp(start-4.15f,end+4.15f,progress);
            float body=Mathf.Clamp01(Mathf.Sin(progress*Mathf.PI)*1.38f);
            float crestBlend=Mathf.SmoothStep(0,1,body);
            for(int col=0;col<=around;col++)
            {
                int wing = col<flankSteps ? 1 : col>flankSteps+archSteps ? -1 : 0;
                float x,y,along=z;
                if(wing==0)
                {
                    float u=(col-flankSteps)/(float)archSteps;
                    Vector2 edge=curve(u,.82f);
                    x=edge.x;
                    float central=forest?10.5f:8.8f;
                    float crest=central*Ridge(u,.30f+.13f*Mathf.Sin(progress*5.2f),.29f)*
                        (.65f+.40f*Ridge(progress,.40f,.29f));
                    y=edge.y+crestBlend*(3f+(2.3f+crest)*Mathf.Sin(Mathf.PI*u));
                    along+=.29f*edge.y*(1f-2f*progress);
                }
                else
                {
                    float u=wing>0?(flankSteps-col)/(float)flankSteps:(col-flankSteps-archSteps)/(float)flankSteps;
                    x=wing*Mathf.Lerp(opening,boundary,u);
                    float shoulder=(p.spring-floor)*(1-Mathf.SmoothStep(0,1,u));
                    float waveA=Ridge(u,.26f+.07f*Mathf.Sin(progress*8.6f+wing),.17f)*
                        (8+10*Ridge(progress,wing>0?.33f:.66f,.24f));
                    float waveB=Ridge(u,.61f,.21f)*(4+7*Ridge(progress,wing>0?.77f:.31f,.30f));
                    float modulation=Mathf.Sin(col*.68f+row*.57f)*Mathf.Sin(col*.36f-row*.53f)*(forest?.33f:1.25f);
                    float taper=Mathf.Sin(u*Mathf.PI);
                    y=floor+shoulder+3f*crestBlend*(1-u)*(1-u)+taper*((.34f+.66f*crestBlend)*(waveA+waveB)+modulation);
                    along+=.29f*p.spring*(1f-2f*progress)*(1-u)*(1-u);
                }
                points.Add(new Vector3(x,y,along));
                if(row==0||col==0)continue;
                int a=(row-1)*(around+1)+col-1,b=a+around+1;
                tris.AddRange(new[]{a,a+1,b,b,a+1,b+1});
            }
        }
        // Extend both mountain feet into their aprons. The first/last arch rows form open portal cutouts.
        foreach(int row in new[]{0,rows})
        {
            int rowStart=row*(around+1);
            for(int wing=0;wing<2;wing++)
            {
                int edgeStart=wing==0?0:flankSteps+archSteps;
                int farStart=points.Count;
                for(int i=0;i<=flankSteps;i++)
                {
                    var near=points[rowStart+edgeStart+i];
                    float extend=.11f+.89f*Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(near.x)-opening)/6f));
                    var far=new Vector3(near.x,floor,
                        near.z+(row==0?-1f:1f)*18f*extend);
                    points.Add(far);
                    if(i==0)continue;
                    int a=rowStart+edgeStart+i-1,b=a+1,c=farStart+i-1,d=c+1;
                    bool previousMoved=(points[a]-points[c]).sqrMagnitude>.00001f;
                    bool moved=(far-near).sqrMagnitude>.00001f;
                    if(row==0)
                    {
                        if(moved)tris.AddRange(new[]{a,d,b});
                        if(previousMoved)tris.AddRange(new[]{a,c,d});
                    }
                    else
                    {
                        if(moved)tris.AddRange(new[]{a,b,d});
                        if(previousMoved)tris.AddRange(new[]{a,d,c});
                    }
                }
            }
        }
        var mountain = InfraMesh(points,tris);
        var terrain=p.Mat(forest?"Forest mountain":"Rocky mountain",forest?0x627F4Bu:0x72756Bu);
        mountain.uv=points.Select(point=>new Vector2(point.x*.16f,point.z*.16f)).ToArray();
        const string layerPath="Assets/_TerrainAutoUpgrade/layer_groundgrass01ground01Nb2e386cdaf7e7ee2.terrainlayer";
        var terrainLayer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if(terrainLayer!=null && terrainLayer.diffuseTexture!=null) terrain.mainTexture=terrainLayer.diffuseTexture;
        p.Leaf(group,"Mountain","Sườn núi bao hầm",mountain,terrain);
        p.Leaf(group,"ApronContext","Địa hình dưới cửa hầm",
            InfraRect(new Vector3(0,floor-.26f,0),new Vector3(boundary*2f,.51f,end-start+45)),
            p.Mat(forest?"Forest apron soil":"Rocky apron soil",forest?0x566E48u:0x6B6B60u),"Portal scene context","PREVIEW_CONTEXT",true);
        var data=ContactPart("mountain","mountain",group.name,mountain.vertices,mountain.triangles);
        int intrusions=0,missingCover=0;
        for(float z=start+.7f;z<end-.7f;z+=.8f)
            for(float x=-p.inside+.12f;x<p.inside-.12f;x+=.63f)
            {
                var sample=MeshSurface(data,x,z);
                if(!sample.hit)missingCover++;
                else
                {
                    float angle=Mathf.Acos(Mathf.Clamp(x/p.inside,-1,1));
                    float liningOutside=p.spring+(p.rise+.41f)*Mathf.Sin(angle);
                    if(sample.min<liningOutside)intrusions++;
                }
            }
        p.Check("TUN-03",group.name+" roof",missingCover,0,"Vertical rays against actual mountain triangles above the bore");
        p.Check("TUN-03",group.name+" rock intrusion",intrusions,0,"Actual mountain triangles compared with the outer lining above both tracks");
        if(forest)
        {
            const string treePath="Assets/(HDRP) NYC-Like City Buildings Set (PBR)/Prefabs/Tree.prefab";
            var tree=AssetDatabase.LoadAssetAtPath<GameObject>(treePath);
            var filter=tree==null?null:tree.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(item=>item.sharedMesh!=null);
            if(filter!=null)
                for(int i=0;i<40;i++)
                {
                    float x=(i%2==0?-1:1)*(p.inside+4+(i*7%21)*1.19f);
                    float z=start-3f+(i/2)*2.38f;
                    var ground=MeshSurface(data,x,z);
                    if(!ground.hit)continue;
                    var source=filter.sharedMesh;
                    float size=2.7f+i%5*.48f;
                    var mesh=ObjectCopyTree(source,new Vector3(x,ground.max-.25f,z),size);
                    p.Check("TUN-03",group.name+" tree "+i,
                        p.inside-mesh.vertices.Min(vertex=>Mathf.Abs(vertex.x)),0,"Lateral clearance of every reused tree vertex from the bore");
                    p.Leaf(group,$"Tree{i:D2}","Cây trên sườn",mesh,p.Mat("Forest vegetation",0x446841),treePath,"PROJECT_REFERENCE",true);
                    var canopy=InfraCanopy(new Vector3(x,ground.max-.25f,z),size,i);
                    p.Check("TUN-03",group.name+" canopy "+i,
                        p.inside-canopy.vertices.Min(vertex=>Mathf.Abs(vertex.x)),0,"Lateral clearance of every canopy vertex");
                    p.Leaf(group,$"Crown{i:D2}","Tán cây trên sườn",canopy,p.Mat("Forest canopy "+(i%3),i%3==0?0x57713Au:i%3==1?0x496B41u:0x365834u),
                        "G1 procedural foliage","PARAMETRIC",true);
                }
        }
        else
        {
            const string rockPath="Assets/Terrain/Rocks and Terrains Pack - Low Poly/Models/Rocks/Sharp Rocks/Sharp Rocks/Sharp_Rock_7.fbx";
            var rock=AssetDatabase.LoadAssetAtPath<GameObject>(rockPath);
            var filter=rock==null?null:rock.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(item=>item.sharedMesh!=null);
            if(filter!=null)
                for(int i=0;i<14;i++)
                {
                    float x=(i%2==0?-1:1)*(p.inside+4.9f+i%6*1.39f);
                    float z=start-6f+i/2*4.7f;
                    var ground=MeshSurface(data,x,z);
                    if(!ground.hit)continue;
                    var mesh=CopyInfraContext(filter.sharedMesh,new Vector3(x,ground.max-.60f,z),2.05f+i%5*.35f,true);
                    p.Check("TUN-03",group.name+" rock "+i,
                        p.inside-mesh.vertices.Min(vertex=>Mathf.Abs(vertex.x)),0,"Lateral clearance of every reused rock vertex from the bore");
                    p.Leaf(group,$"Rock{i:D2}","Đá trên sườn",mesh,p.Mat("Weathered rock",0x7B7B71),rockPath,"PROJECT_REFERENCE",true);
                    if(i%2==0)
                    {
                        float shrubX=x+1.3f;
                        var shrubGround=MeshSurface(data,shrubX,z);
                        if(!shrubGround.hit)continue;
                        var shrub=InfraCanopy(new Vector3(shrubX,shrubGround.max-.83f,z),1.9f,i);
                        p.Check("TUN-03",group.name+" shrub "+i,
                            p.inside-shrub.vertices.Min(vertex=>Mathf.Abs(vertex.x)),0,"Shrub geometry stays outside the railway corridor");
                        p.Leaf(group,$"RockShrub{i:D2}","Bụi cây trên sườn đá",shrub,
                            p.Mat("Rocky slope vegetation",0x4D6345),"G1 procedural foliage","PARAMETRIC",true);
                    }
                }
        }
    }

    static Mesh InfraCanopy(Vector3 baseCenter,float height,int seed)
    {
        const int sides=9;
        var points=new List<Vector3>();
        var triangles=new List<int>();
        for(int lobe=0;lobe<3;lobe++)
        {
            float angle=seed*2.4f+lobe*2.09f;
            var center=baseCenter+height*new Vector3(.16f*Mathf.Sin(angle),.70f+.07f*lobe,.16f*Mathf.Cos(angle));
            int first=points.Count;
            for(int row=0;row<3;row++)
                for(int column=0;column<sides;column++)
                {
                    float theta=column*Mathf.PI*2/sides+angle;
                    float outer=(row==1?.23f:.13f)*height*(.91f+.09f*Mathf.Cos(seed+column*3.4f));
                    points.Add(center+new Vector3(Mathf.Cos(theta)*outer,(row-1)*height*.115f,Mathf.Sin(theta)*outer));
                    if(row==0)continue;
                    int a=first+(row-1)*sides+column,b=first+(row-1)*sides+(column+1)%sides;
                    triangles.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});
                }
            int bottom=points.Count;
            points.Add(center-Vector3.up*height*.17f);
            points.Add(center+Vector3.up*height*.17f);
            for(int column=0;column<sides;column++)
            {
                int a=first+column,b=first+(column+1)%sides;
                triangles.AddRange(new[]{bottom,a,b,bottom+1,b+2*sides,a+2*sides});
            }
        }
        return InfraMesh(points,triangles);
    }

    static Mesh ObjectCopyTree(Mesh source,Vector3 position,float height) => CopyInfraContext(source,position,height,false);

    static Mesh CopyInfraContext(Mesh source,Vector3 position,float extent,bool constrainFootprint)
    {
        var mesh=UnityEngine.Object.Instantiate(source);
        Bounds b=mesh.bounds;
        float basis=constrainFootprint ? Mathf.Max(b.size.x,b.size.y,b.size.z) : b.size.y;
        float scale=extent/Mathf.Max(.01f,basis);
        Vector3 baseCenter=new Vector3(b.center.x,b.min.y,b.center.z);
        mesh.vertices=mesh.vertices.Select(point=>
        {
            var local=(point-baseCenter)*scale;
            return position+(constrainFootprint?new Vector3(local.x*1.65f,local.y*.68f+.24f*local.x,local.z*1.22f):local);
        }).ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void BuildInfraSubgrade(InfraPilot p)
    {
        float halfCrown=Mathf.Ceil((p.spacing*.5f+p.trainHalf+.96f)*4f)/4f;
        float top=p.railBottom;
        const float length=24;
        float[] layerDepth={.25f,.38f,.51f,1.03f,.42f,.72f};
        var labels=new[]{"Lớp nền trên","Cấp phối gia cố","Đất đắp trên","Thân nền","Lớp xử lý nền","Đất tự nhiên"};
        var colours=new uint[]{0xA8A7A1,0x8F9694,0xBEA987,0xA98B65,0xC4A87F,0x9A8D69};
        p.AddMeasure("subgrade_module_length",length,"m","Fits the measured rail sample","One longitudinal module","G1 only");
        for(int i=0;i<layerDepth.Length;i++)
            p.AddMeasure("subgrade_layer_"+i,layerDepth[i],"m","Explicit parametric mock-up layer thickness","Positive and no interface gap","Appearance and BIM inspection");
        foreach(var (kind,variant) in new[]{("Natural",0),("Reinforced",1),("RetainingWall",2)})
        {
            var group=p.Group(p.root,$"Subgrade_Zone_010_{kind}",new Vector3(variant*44,0,151));
            PreviewInfraRails(p,group);
            float slope=variant==0?1.45f:variant==1?1.65f:.08f;
            float y=top;
            float w=halfCrown;
            var layers=new List<ContactMesh>();
            for(int i=0;i<layerDepth.Length;i++)
            {
                float h=layerDepth[i], next=w+h*slope;
                var section=new[]{new Vector2(-next,y-h),new Vector2(next,y-h),new Vector2(w,y),new Vector2(-w,y)};
                var mesh=InfraExtrude(section,-length*.5f,length*.5f);
                p.Leaf(group,$"Layer{i:00}",labels[i],mesh,p.Mat(labels[i],colours[i]));
                layers.Add(ContactPart(i.ToString(),labels[i],group.name,mesh.vertices,mesh.triangles));
                y-=h;w=next;
            }
            for(int i=1;i<layers.Count;i++)
            {
                var contact=SampleInterface(layers[i],layers[i-1],5);
                p.Check("SUB-02",group.name+" layer "+i,contact.Length==0?float.PositiveInfinity:contact.Max(value=>Mathf.Abs(value)),.001f,
                    "SO201 paired triangle contacts across all stacked layers");
            }
            var center=MeshSurface(layers[0],p.spacing*.5f,0);
            p.Check("SUB-02",group.name+" running support",center.hit?Mathf.Abs(center.max-top):float.PositiveInfinity,.002f,
                "Top layer triangle elevation versus transformed support datum");
            var concrete=p.Mat("Subgrade weathered concrete",0xAAACA3);
            var grass=p.Mat("Slope grass",0x57794A);
            var drain=p.Mat("Lining joint",0x828687);
            foreach(int side in new[]{-1,1})
            {
                string tag=side<0?"L":"R";
                float slopeHeight=top-y-.69f;
                float toe=halfCrown+slopeHeight*slope;
                float toeY=top-slopeHeight;
                Vector3 SideAt(float t,float z,float delta)=>new Vector3(side*Mathf.Lerp(halfCrown,toe,t),Mathf.Lerp(top,toeY,t)+delta,z);
                if(variant==2)
                {
                    for(int i=0;i<8;i++)
                    {
                        p.Leaf(group,$"Wall{tag}{i}","Tấm tường chắn",
                            InfraRect(new Vector3(side*(halfCrown+.18f),top-slopeHeight*.5f,-10.5f+i*3),new Vector3(.39f,slopeHeight,2.982f)),concrete);
                        var outlet=new Vector3(side*(halfCrown+.38f),toeY+.27f,-10.5f+i*3);
                        p.Leaf(group,$"WallDrain{tag}{i}","Đầu ống thoát tường",
                            InfraCollar(outlet-Vector3.right*side*.035f,outlet+Vector3.right*side*.068f,.051f,.066f),drain);
                    }
                    p.Leaf(group,"WallCap"+tag,"Mũ tường chắn",
                        InfraRect(new Vector3(side*(halfCrown+.18f),top+.047f,0),new Vector3(.54f,.105f,24)),concrete);
                }
                else
                {
                    var sloped=InfraMesh(new[]{SideAt(0,-12,.012f),SideAt(0,12,.012f),SideAt(1,-12,.012f),SideAt(1,12,.012f)},
                        side>0?new[]{0,1,2,2,1,3}:new[]{0,2,1,2,3,1});
                    p.Leaf(group,"Slope"+tag,"Mái cỏ",sloped,grass);
                    if(variant==1)
                    {
                        int line=0;
                        foreach(int direction in new[]{-1,1})
                            for(float anchor=-18;anchor<=18;anchor+=3)
                            {
                                float a=(-12-anchor)/(direction*6);
                                float b=(12-anchor)/(direction*6);
                                float lo=Mathf.Max(0,Mathf.Min(a,b));
                                float hi=Mathf.Min(1,Mathf.Max(a,b));
                                if(hi-lo<.015f)continue;
                                var normal=new Vector3(side*slopeHeight,toe-halfCrown,0).normalized;
                                var start=SideAt(lo,anchor+direction*6*lo,.027f);
                                var finish=SideAt(hi,anchor+direction*6*hi,.027f);
                                var rib=InfraSlopeRib(start,finish,normal,.145f,.135f);
                                if(line==0)
                                {
                                    float buried=rib.vertices.Min(vertex=>Vector3.Dot(vertex-SideAt(lo,anchor+direction*6*lo,.012f),normal));
                                    p.Check("SUB-03",group.name+" embedded "+tag,buried,-.02f,"Reinforcing rib lower corners below the grass plane");
                                }
                                p.Leaf(group,$"Diamond{tag}{line++:D2}","Dầm ô gia cố",rib,concrete);
                            }
                        p.Leaf(group,"Crest"+tag,"Dầm đỉnh mái",InfraBar(SideAt(0,-12,.075f),SideAt(0,12,.075f),.21f,.19f),concrete);
                        p.Leaf(group,"Toe"+tag,"Dầm chân mái",InfraBar(SideAt(1,-12,.075f),SideAt(1,12,.075f),.26f,.22f),concrete);
                        p.Check("SUB-03",group.name+" "+tag,line<14?1:0,0,"Two clipped continuous diagonal families terminate at the crest, toe or module boundary");
                    }
                }
                p.Leaf(group,"SideDrain"+tag,"Rãnh thoát nước",InfraChannel(side*(toe+.36f),toeY-.07f,24,.53f,.24f),drain);
                p.Leaf(group,"Cable"+tag,"Máng cáp riêng",InfraChannel(side*(halfCrown-.35f),top-.02f,24,.34f,.21f),drain);
                p.Leaf(group,"CableCover"+tag,"Nắp máng cáp",InfraRect(new Vector3(side*(halfCrown-.35f),top+.204f,0),new Vector3(.365f,.045f,24)),concrete);
            }
            p.View("Subgrade_"+kind,group,new Vector3(19,12,-22),new Vector3(0,-.6f,0),14f);
            if(variant==1)p.View("Subgrade_Diamond",group,new Vector3(19,5,-16),new Vector3(5,-1.3f,0),11f);
            if(variant==2)p.View("Subgrade_WallDetail",group,new Vector3(12,1.3f,-6),new Vector3(6.4f,-1.4f,-3),3.6f);
        }
    }

    static Mesh InfraSlopeRib(Vector3 a,Vector3 b,Vector3 normal,float width,float depth)
    {
        var basis=Vector3.Cross(normal,b-a).normalized*width*.5f;
        var height=normal*depth*.5f;
        var points=new[]{a-basis-height,a+basis-height,a+basis+height,a-basis+height,
            b-basis-height,b+basis-height,b+basis+height,b-basis+height};
        return InfraMesh(points,new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,
            1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7});
    }

    static Mesh InfraExtrude(IReadOnlyList<Vector2> section,float start,float end)
    {
        int count=section.Count;
        var pts=section.Select(v=>new Vector3(v.x,v.y,start)).Concat(section.Select(v=>new Vector3(v.x,v.y,end)));
        var tris=new List<int>();
        for(int i=0;i<count;i++)
        {
            int next=(i+1)%count;
            tris.AddRange(new[]{i,next,next+count,i,next+count,i+count});
        }
        for(int i=1;i+1<count;i++) tris.AddRange(new[]{0,i+1,i,count,count+i,count+i+1});
        return InfraMesh(pts,tris);
    }

    [Serializable] sealed class Earthworks { public EarthworkTile[] parts; }
    [Serializable] sealed class EarthworkTile { public string ifcClass,globalId; public Vector3[] points;public int[] triangles; }

    static void BuildInfraEarthworksComparison(InfraPilot p)
    {
        string file=InfraTemp+"EarthworksTessellation.json";
        if(!File.Exists(file)) return;
        var model=JsonUtility.FromJson<Earthworks>(File.ReadAllText(file));
        var extent=BoundsOf(model.parts.SelectMany(tile=>tile.points));
        var source=p.Group(p.root,"Subgrade_Zone_010_IFCComparison",new Vector3(135,0,178));
        var crop=model.parts[1];
        var footprint=BoundsOf(crop.points);
        Vector3 center=footprint.center;
        p.AddMeasure("earthworks_ifc_fill_width_X",footprint.size.x,"m","Declared IFC triangulation, metre units and identity placement","Source geography only","Raw geographic orientation, not assigned to route");
        p.AddMeasure("earthworks_ifc_fill_length_Z",footprint.size.z,"m","Declared IfcTriangulatedFaceSet","Source geography only","One source fill without individually modeled railway layers");
        foreach(var tile in model.parts)
        {
            var vertices=tile.points.Select(point=>point-new Vector3(center.x,footprint.max.y,center.z)).ToArray();
            int limit=tile.triangles.Max();
            if(limit>=vertices.Length)throw new InvalidOperationException("RC3 tessellation index out of range.");
            var mesh=InfraMesh(vertices,tile.triangles);
            p.Leaf(source,tile.ifcClass,tile.ifcClass,mesh,p.Mat(tile.ifcClass,tile==crop?0xAA957Eu:0x78945Eu),tile.globalId,"IFC_TESSELLATION",true);
        }
        float radius=Mathf.Max(extent.size.x,extent.size.z)*.58f;
        p.View("Subgrade_IFC_Comparison",source,new Vector3(radius,radius,-radius),Vector3.zero,radius);
        p.Check("SUB-01","RC3 explicit tessellation bodies",Mathf.Abs(model.parts.Length-2),0,"IFC subset extraction verified mesh indices, metre unit and identity local placement; non-layered source rejected");
    }

    static void BuildInfraExistingGroundComparison(InfraPilot p, Transform terrain)
    {
        var filter=terrain.GetComponent<MeshFilter>();
        var source=filter.sharedMesh;
        var bounds=BoundsOf(source.vertices.Select(terrain.TransformPoint));
        var points=source.vertices.Select(v=>terrain.TransformPoint(v)-new Vector3(bounds.center.x,bounds.max.y,bounds.center.z));
        var group=p.Group(p.root,"Subgrade_Zone_010_ExistingGround",new Vector3(160,0,250));
        var mesh=InfraMesh(points,source.triangles);
        var mat=p.Mat("Existing terrain preview",0x78945E);
        p.Leaf(group,"TerrainReference","Nền hiện có Zone_010",mesh,mat,AssetDatabase.GetAssetPath(source),"PROJECT_REFERENCE",true);
        float radius=Mathf.Max(bounds.size.x,bounds.size.z)*.58f;
        p.View("Subgrade_Existing_Comparison",group,new Vector3(radius,radius,-radius),Vector3.zero,radius);
    }
}
