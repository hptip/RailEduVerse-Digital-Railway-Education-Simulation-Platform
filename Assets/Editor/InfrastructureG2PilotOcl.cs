using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

static partial class SO201OriginalBridgeAudit
{
    static void BuildPilotOverhead(RoutePilot p,PilotSectors sectors)
    {
        MeasurePilotTrain(p);
        var bridge=p.manager.fixedInfrastructure.Single(work=>work.kind=="Bridge");
        var stationBounds=BoundsOf(p.station.SelectMany(mesh=>Corners(mesh.bounds)));
        float nearest=Enumerable.Range(0,600).OrderBy(d=>
        {
            var delta=p.Frame(d).point-stationBounds.center; delta.y=0;
            return delta.sqrMagnitude;
        }).First();
        float stationExtent=0;
        var stationAxis=p.Frame(nearest).rotation*Vector3.forward;
        foreach(var corner in Corners(stationBounds))
            stationExtent=Mathf.Max(stationExtent,Mathf.Abs(Vector3.Dot(corner-p.Frame(nearest).point,stationAxis)));
        p.stationStart=Mathf.Max(0,nearest-stationExtent);
        p.stationEnd=Mathf.Min(p.length,nearest+stationExtent);
        p.Record("OCL-STATION-EXTENT","Actual station","INFO",
            FormattableString.Invariant($"Renderer longitudinal projection {p.stationStart:F3}–{p.stationEnd:F3} m, {p.station.Sum(part=>part.triangles.Length/3)} world triangles"));
        var ranges=new List<(string key,string kind,float start,float end,float step)>{
            ("Station","Station",p.stationStart,p.stationEnd,35f),
            ("Open","OpenTrack",sectors.overheadStart,sectors.overheadEnd,42f),
            ("Bridge","Bridge",bridge.startChainage+10f,bridge.endChainage-10f,38f)};
        foreach(var tunnel in p.manager.fixedInfrastructure.Where(item=>item.kind=="TunnelMountain"))
            ranges.Add((tunnel.id,"Tunnel",tunnel.startChainage+3f,tunnel.endChainage-3f,30f));
        foreach(var range in ranges)
        {
            var source=p.templates.Values.Single(parts=>parts.Any(part=>part.id.StartsWith("OCL_",StringComparison.Ordinal)&&
                part.id.Contains("_"+range.kind+"-",StringComparison.Ordinal)));
            int intervals=Mathf.Max(1,Mathf.RoundToInt((range.end-range.start)/range.step));
            var nodes=Enumerable.Range(0,intervals+1).Select(index=>Mathf.Lerp(range.start,range.end,index/(float)intervals)).ToArray();
            if(range.kind=="Tunnel")
            {
                AddPilotTransition(p,range.key,range.start,true);
                AddPilotTransition(p,range.key,range.end,false);
            }
            int stationClashes=0,bridgeUnsupported=0,otherFoundations=0,overheadTriangles=0;
            int bridgeCollisions=0;
            const float clearanceTarget=.02f;
            float stationClear=clearanceTarget;
            float maxFootGap=0;
            var canopy=range.kind=="Station"?p.station.Select(part=>PilotTriangles(part.vertices,part.triangles)).SelectMany(items=>items).ToArray():Array.Empty<PilotTriangle>();
            var bridgeEdges=range.kind=="Bridge"?p.bridgeEdges.SelectMany(part=>PilotTriangles(part.vertices,part.triangles)).ToArray():Array.Empty<PilotTriangle>();
            bool InspectStation(Mesh mesh,bool installed)
            {
                if(canopy.Length==0)return false;
                bool hit=IntersectsPilotStation(mesh,canopy);
                if(installed)
                    stationClear=Mathf.Min(stationClear,hit?0:PilotClearance(mesh,canopy,clearanceTarget));
                return hit;
            }
            var supports=source.Where(part=>part.slot.StartsWith("P0",StringComparison.Ordinal)).ToArray();
            var stationFootShift=new float[nodes.Length];
            var accepted=Enumerable.Repeat(true,nodes.Length).ToArray();
            if(range.kind=="Station")
            {
                for(int i=0;i<nodes.Length;i++)
                {
                    float original=nodes[i];
                    int steps=Mathf.CeilToInt((p.stationEnd-p.stationStart)*2);
                    var candidates=Enumerable.Range(0,steps+1).SelectMany(offset=>new[]{original+offset*.5f,original-offset*.5f})
                        .Distinct().Where(d=>d>=p.stationStart && d<=p.stationEnd &&
                            (i==0||d-nodes[i-1]>=12f)&&(i==nodes.Length-1||nodes[i+1]-d>=12f)).ToArray();
                    bool fit=false;
                    float stationHalfWidth=stationBounds.extents.magnitude;
                    float maxShift=Mathf.Min(13f,stationHalfWidth-5.6f);
                    for(float shift=0;shift<=maxShift&&!fit;shift+=1f)
                    {
                        float localShift=shift;
                        var trial=candidates.Where(d=>StationSupportClear(p,d,supports,canopy,localShift))
                            .DefaultIfEmpty(float.NaN).First();
                        if(!float.IsFinite(trial))continue;
                        nodes[i]=trial;
                        stationFootShift[i]=shift;
                        fit=true;
                    }
                    p.Record("OCL-STATION-SITING",i.ToString(),fit?"INFO":"REVIEW",
                        FormattableString.Invariant($"Initial {original:F3} m; actual {nodes[i]:F3} m; mast width addition {2*stationFootShift[i]:F3} m. Portal family retained; station geometry unmodified."));
                    accepted[i]=fit;
                }
            }
            Vector3 WallAnchor(Vector3 point)
            {
                float ratio=Mathf.Clamp01((point.y-p.spring)/p.rise);
                float seam=p.bore*Mathf.Sqrt(1f-ratio*ratio);
                float weight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(p.bore-1.25f,p.bore-.06f,Mathf.Abs(point.x)));
                point.x-=Mathf.Sign(point.x)*weight*(p.bore-seam);
                return point;
            }
            for(int i=0;i<nodes.Length;i++)
            {
                float d=nodes[i];
                foreach(var part in supports)
                {
                    float shift=stationFootShift[i];
                    Func<Vector3,Vector3> placement=range.kind=="Tunnel"?WallAnchor:
                        shift>0?point=>StationWiden(point,shift):null;
                    var mesh=p.Warp(part,d,-10.5f,1f,placement);
                    if(part.slot.EndsWith("-Foundation",StringComparison.Ordinal))
                    {
                        var positions=mesh.vertices;
                        float floor=positions.Min(point=>point.y);
                        var anchors=positions.Where(point=>point.y<=floor+.001f).Distinct().ToArray();
                        var foundations=range.kind=="Bridge"?p.bridge:p.ground;
                        float[] surfaces=anchors.Select(anchor=>p.Surface(foundations,anchor)).ToArray();
                        if(surfaces.Any(value=>!float.IsFinite(value)))
                        {
                            if(range.kind=="Bridge")bridgeUnsupported++;
                            else otherFoundations++;
                        }
                        else
                        {
                            float gap=surfaces.Max()-floor;
                            maxFootGap=Mathf.Max(maxFootGap,Mathf.Abs(gap));
                            if(gap<0)
                            {
                                for(int j=0;j<positions.Length;j++)
                                    if(positions[j].y<=floor+.001f)positions[j].y+=gap;
                                mesh.vertices=positions; mesh.RecalculateBounds();
                            }
                        }
                    }
                    if(canopy.Length>0)
                    {
                        overheadTriangles+=mesh.triangles.Length/3;
                        if(InspectStation(mesh,accepted[i]))
                        {
                            stationClashes++;
                            p.Record("OCL-STATION-HIT",part.slot,"INFO",FormattableString.Invariant($"Triangle contact at {d:F3} m."));
                        }
                    }
                    if(bridgeEdges.Length>0&&IntersectsPilotStation(mesh,bridgeEdges))bridgeCollisions++;
                    if(accepted[i])
                        p.Put("OCL",$"{range.key}-P{i:D3}",d,part,mesh,Matrix4x4.identity,i==1&&range.kind=="OpenTrack");
                }
            }
            float largestSeam=0;
            var endpoints=new Dictionary<(int side,bool heavy),Vector3>();
            for(int span=0;span<intervals;span++)
            {
                if(!accepted[span]||!accepted[span+1])continue;
                float a=nodes[span],b=nodes[span+1];
                foreach(int side in new[]{-1,1})
                {
                    string tag=side<0?"WireL":"WireR";
                    Vector3 At(float u,bool heavy)=>p.World(Mathf.Lerp(a,b,u),side*p.spacing*.5f-.105f,
                        heavy?p.messenger-1.12f*u*(1f-u):p.wire);
                    foreach(bool heavy in new[]{false,true})
                    {
                        var part=source.Single(item=>item.slot==tag+(heavy?"-Messenger":"-Contact"));
                        var mesh=InfraPipe(Enumerable.Range(0,25).Select(i=>At(i/24f,heavy)).ToArray(),heavy?.017f:.016f);
                        p.temporary.Add(mesh);
                        if(endpoints.TryGetValue((side,heavy),out var previous))largestSeam=Mathf.Max(largestSeam,(At(0,heavy)-previous).magnitude);
                        endpoints[(side,heavy)]=At(1,heavy);
                        if(InspectStation(mesh,true))stationClashes++;
                        p.Put("OCL",$"{range.key}-W{span:D3}",(a+b)*.5f,part,mesh,Matrix4x4.identity,false);
                    }
                    for(int drop=1;drop<=5;drop++)
                    {
                        float u=drop/6f;
                        var part=source.Single(item=>item.slot==tag+"-Dropper"+drop);
                        var clamp=source.Single(item=>item.slot==tag+"-Clamp"+drop);
                        var thread=InfraPipe(new[]{At(u,false),At(u,true)},.011f);
                        var tip=At(u,true);
                        var clip=InfraCollar(tip-Vector3.forward*.046f,tip+Vector3.forward*.046f,.024f,.039f);
                        p.temporary.Add(thread);
                        p.temporary.Add(clip);
                        if(InspectStation(thread,true))stationClashes++;
                        if(InspectStation(clip,true))stationClashes++;
                        p.Put("OCL",$"{range.key}-W{span:D3}",Mathf.Lerp(a,b,u),part,thread,Matrix4x4.identity,false);
                        p.Put("OCL",$"{range.key}-W{span:D3}",Mathf.Lerp(a,b,u),clamp,clip,Matrix4x4.identity,false);
                    }
                }
            }
            p.Check("OCL-SEAM",range.key,largestSeam,.001f,"Shared world coordinates for both ends of every consecutive contact and messenger wire");
            p.Check("OCL-GROUND",range.key,otherFoundations,0,"Actual terrain triangle under each corner of a non-bridge foundation");
            var spacings=nodes.Skip(1).Zip(nodes,(current,previous)=>current-previous).ToArray();
            p.Record("OCL-PLACEMENT",range.key,"INFO",FormattableString.Invariant(
                $"{nodes.Length} supports between {nodes[0]:F3} and {nodes[^1]:F3} m, spacing {spacings.Min():F3}–{spacings.Max():F3} m; max measured reference foot level delta {maxFootGap:F3} m"));
            if(range.kind=="Bridge")
            {
                p.Check("OCL-BRIDGE",range.key,bridgeUnsupported,0,"Four corners per mast on real SO201 pavement; no bridge geometry changes");
                if(bridgeEdges.Length>0)
                    p.Check("OCL-BRIDGE-EDGE",range.key,bridgeCollisions,0,
                        $"Support triangle intersections against {p.bridgeEdges.Count} accepted SO201 edge meshes.");
                else p.Record("OCL-BRIDGE-EDGE",range.key,"NOT_VERIFIED","No separately readable parapet/cornice mesh matched the accepted bridge.");
            }
            if(range.kind=="Station")
            {
                float uncovered=accepted.All(fit=>fit)
                    ?Mathf.Max(0,nodes[0]-p.stationStart)+Mathf.Max(0,p.stationEnd-nodes[^1])
                    : p.stationEnd-p.stationStart;
                p.Check("OCL-STATION-COVERAGE",range.key,uncovered,.01f,
                    "Measured uncovered chainage in the projected station footprint; failed supports are excluded.");
                p.Check("OCL-STATION-INTERSECTION",range.key,stationClashes,0,$"Segment/triangle checks against actual station render mesh, all posts, portal members, cables, clamps and droppers; {overheadTriangles} support triangles");
                if(accepted.All(fit=>fit))
                    p.Check("OCL-STATION-POSITIVE-CLEARANCE",range.key,-stationClear,-clearanceTarget,
                        "True vertex-to-triangle and edge-to-edge surface distance, capped at 20 mm; engineering clearance remains outside pilot scope.");
                else p.Record("OCL-STATION-POSITIVE-CLEARANCE",range.key,"NOT_VERIFIED","Incomplete station coverage; cannot certify a continuous gap.");
            }
        }
        p.Record("OCL-PANTOGRAPH","Both cars","NOT_VERIFIED","Existing combined mesh does not identify a verified animated pantograph head/contact motion. Visual geometry only, no electrical operation.");
    }

    static Vector3 StationWiden(Vector3 point,float shift)
    {
        // Keep the standard hanger and contact wire in place. Move each outboard upright
        // as one unit, extending the clear span only between the hangers and uprights.
        float stretch=Mathf.InverseLerp(4.12f,5.60f,Mathf.Abs(point.x));
        point.x+=Mathf.Sign(point.x)*shift*stretch;
        return point;
    }

    static bool StationSupportClear(RoutePilot p,float d,PilotPart[] supports,PilotTriangle[] canopy,float shift)
    {
        foreach(var part in supports)
        {
            var mesh=p.Warp(part,d,-10.5f,1f,shift>0?point=>StationWiden(point,shift):null);
            bool blocked=IntersectsPilotStation(mesh,canopy);
            if(part.slot.EndsWith("-Foundation",StringComparison.Ordinal))
            {
                float baseY=mesh.bounds.min.y;
                blocked|=mesh.vertices.Where(point=>point.y<=baseY+.001f)
                    .Any(point=>!float.IsFinite(p.Surface(p.ground,point)));
            }
            p.temporary.RemoveAt(p.temporary.Count-1);
            UnityEngine.Object.DestroyImmediate(mesh);
            if(blocked)return false;
        }
        return true;
    }

    static void AddPilotTransition(RoutePilot p,string key,float boundary,bool before)
    {
        var source=p.templates["OCL_Zone_017_OpenTrack"];
        string label=key+(before?"-Enter":"-Exit");
        float exterior=boundary+(before?-36f:36f);
        foreach(var part in source.Where(part=>part.slot.StartsWith("P0",StringComparison.Ordinal)))
            p.Put("OCL",label,exterior,part,p.Warp(part,exterior,-10.5f),Matrix4x4.identity,false);
        foreach(int side in new[]{-1,1})
            foreach(bool heavy in new[]{false,true})
            {
                var part=source.Single(item=>item.slot==(side<0?"WireL":"WireR")+(heavy?"-Messenger":"-Contact"));
                var points=Enumerable.Range(0,25).Select(i=>i/24f).Select(u=>p.World(Mathf.Lerp(exterior,boundary,u),
                    side*p.spacing*.5f-.105f,heavy?p.messenger-1.12f*u*(1f-u):p.wire)).ToArray();
                var mesh=InfraPipe(points,heavy?.017f:.016f);
                p.temporary.Add(mesh);
                p.Put("OCL",label,(exterior+boundary)*.5f,part,mesh,Matrix4x4.identity,false);
            }
    }

    static void MeasurePilotTrain(RoutePilot p)
    {
        var train=p.manager.trainMover;
        double top=double.NegativeInfinity,wide=0;
        var cars=new[]{(train.frontCar,0f,train.frontHeightOffset),(train.rearCar,train.carSpacing,train.frontHeightOffset+train.rearYOffset)};
        var bounds=cars.Select(item=>
        {
            var inverse=Matrix4x4.TRS(item.Item1.position,item.Item1.rotation,Vector3.one).inverse;
            return BoundsOf(item.Item1.GetComponentsInChildren<MeshRenderer>(true).Where(renderer=>renderer.enabled)
                .SelectMany(renderer=>Corners(renderer.localBounds).Select(point=>inverse.MultiplyPoint3x4(renderer.localToWorldMatrix.MultiplyPoint3x4(point)))));
        }).ToArray();
        for(float d=train.carSpacing;d<=p.length;d+=1f)
            for(int car=0;car<cars.Length;car++)
            {
                float pivot=d-cars[car].Item2;
                var pose=p.Frame(pivot);
                foreach(var point in Corners(bounds[car]))
                    foreach(int side in new[]{-1,1})
                    {
                        var local=pose.point+pose.rotation*(point+Vector3.right*(side*p.spacing*.5f))+
                            Vector3.up*(cars[car].Item3-p.railOffset);
                        var section=p.Frame(pivot+point.z);
                        var delta=Quaternion.Inverse(section.rotation)*(local-section.point);
                        top=Math.Max(top,delta.y);
                        wide=Math.Max(wide,Math.Abs(delta.x-side*p.spacing*.5f));
                    }
            }
        p.Check("OCL-TRAIN-VERTICAL","Two car envelopes on both tracks",top-(p.wire-.016f),-.42f,
            "Full route at 1 m increments; front and rear pose offset and corner projections relative to track cross section");
        p.Check("OCL-TRAIN-LATERAL","Two tracks versus the inner tunnel wall",wide+p.spacing*.5f-p.bore,-1.5f,
            "Swept width with longitudinal tangent rotation; double track center reference");
    }

    readonly struct PilotTriangle
    {
        public readonly Vector3 a,b,c;
        public readonly Bounds bounds;
        public PilotTriangle(Vector3 a,Vector3 b,Vector3 c)
        {
            this.a=a;this.b=b;this.c=c;
            bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);
        }
    }

    static IEnumerable<PilotTriangle> PilotTriangles(Vector3[] vertices,int[] indices)
    {
        for(int i=0;i<indices.Length;i+=3)yield return new PilotTriangle(vertices[indices[i]],vertices[indices[i+1]],vertices[indices[i+2]]);
    }

    static bool IntersectsPilotStation(Mesh candidate,PilotTriangle[] station)
    {
        var meshBounds=candidate.bounds;
        var nearby=station.Where(item=>meshBounds.Intersects(item.bounds)).ToArray();
        if(nearby.Length==0)return false;
        bool Cross(Vector3 a,Vector3 b,PilotTriangle t)
        {
            Vector3 dir=b-a,e1=t.b-t.a,e2=t.c-t.a;
            Vector3 p=Vector3.Cross(dir,e2);
            float det=Vector3.Dot(e1,p);
            if(Mathf.Abs(det)<1e-8f)return false;
            Vector3 s=a-t.a;
            float u=Vector3.Dot(s,p)/det;
            if(u<0||u>1)return false;
            Vector3 q=Vector3.Cross(s,e1);
            float v=Vector3.Dot(dir,q)/det;
            if(v<0||u+v>1)return false;
            float distance=Vector3.Dot(e2,q)/det;
            return distance>=0 && distance<=1;
        }
        foreach(var triangle in PilotTriangles(candidate.vertices,candidate.triangles))
            foreach(var roof in nearby)
            {
                if(!triangle.bounds.Intersects(roof.bounds))continue;
                if(Cross(triangle.a,triangle.b,roof)||Cross(triangle.b,triangle.c,roof)||Cross(triangle.c,triangle.a,roof)||
                    Cross(roof.a,roof.b,triangle)||Cross(roof.b,roof.c,triangle)||Cross(roof.c,roof.a,triangle))return true;
            }
        return false;
    }

    static float PilotClearance(Mesh candidate,PilotTriangle[] station,float limit)
    {
        Bounds region=candidate.bounds;
        region.Expand(limit*2);
        var nearby=station.Where(item=>region.Intersects(item.bounds)).ToArray();
        if(nearby.Length==0)return limit;
        float best=limit*limit;
        float SegmentPoint(Vector3 a,Vector3 b,Vector3 point)
        {
            Vector3 ab=b-a;
            float t=Mathf.Clamp01(Vector3.Dot(point-a,ab)/Mathf.Max(ab.sqrMagnitude,1e-12f));
            return (point-(a+ab*t)).sqrMagnitude;
        }
        float PointTriangle(Vector3 point,PilotTriangle triangle)
        {
            Vector3 n=Vector3.Cross(triangle.b-triangle.a,triangle.c-triangle.a).normalized;
            float elevation=Vector3.Dot(point-triangle.a,n);
            var foot=point-elevation*n;
            bool Inside(Vector3 a,Vector3 b)=>Vector3.Dot(Vector3.Cross(b-a,foot-a),n)>=0;
            if(n.sqrMagnitude>.5f&&Inside(triangle.a,triangle.b)&&Inside(triangle.b,triangle.c)&&Inside(triangle.c,triangle.a))
                return elevation*elevation;
            return Mathf.Min(SegmentPoint(triangle.a,triangle.b,point),SegmentPoint(triangle.b,triangle.c,point),
                SegmentPoint(triangle.c,triangle.a,point));
        }
        float Edges(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            Vector3 u=b-a,v=d-c,w=a-c;
            float uu=Vector3.Dot(u,u),uv=Vector3.Dot(u,v),vv=Vector3.Dot(v,v),uw=Vector3.Dot(u,w),vw=Vector3.Dot(v,w);
            float determinant=uu*vv-uv*uv;
            if(determinant<=1e-12f)return float.PositiveInfinity;
            float s=(uv*vw-vv*uw)/determinant,t=(uu*vw-uv*uw)/determinant;
            return s<0||s>1||t<0||t>1?float.PositiveInfinity:(w+s*u-t*v).sqrMagnitude;
        }
        foreach(var part in PilotTriangles(candidate.vertices,candidate.triangles))
        {
            var bounds=part.bounds;
            bounds.Expand(limit*2);
            foreach(var s in nearby)
            {
                if(!bounds.Intersects(s.bounds))continue;
                best=Mathf.Min(best,PointTriangle(part.a,s),PointTriangle(part.b,s),PointTriangle(part.c,s),
                    PointTriangle(s.a,part),PointTriangle(s.b,part),PointTriangle(s.c,part));
                Vector3[] a={part.a,part.b,part.c},b={s.a,s.b,s.c};
                for(int i=0;i<3;i++)for(int j=0;j<3;j++)
                    best=Mathf.Min(best,Edges(a[i],a[(i+1)%3],b[j],b[(j+1)%3]));
            }
        }
        return Mathf.Sqrt(best);
    }
}
