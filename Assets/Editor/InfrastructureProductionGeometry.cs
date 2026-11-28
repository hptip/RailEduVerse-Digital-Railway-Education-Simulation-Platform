using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using Object=UnityEngine.Object;

static partial class SO201OriginalBridgeAudit
{
    static Mesh TrimLegacyInfrastructure(MeshFilter filter,Func<Vector3,float> chainage,List<(float start,float end)> exclusions)
    {
        var mesh=filter.sharedMesh;
        if(mesh.vertexCount==0)return null;
        var vertices=mesh.vertices;
        var normals=mesh.normals;
        var uv=mesh.uv;
        var colors=mesh.colors;
        var distances=vertices.Select(point=>chainage(filter.transform.TransformPoint(point))).ToArray();
        float first=distances.Min(),last=distances.Max();
        var cuts=exclusions.Where(interval=>interval.start<=last&&interval.end>=first).OrderBy(interval=>interval.start).ToArray();
        if(cuts.Length==0)return null;
        var safe=new List<(float min,float max)>();
        float cursor=float.NegativeInfinity;
        foreach(var cut in cuts)
        {
            if(cut.start>cursor)safe.Add((cursor,cut.start));
            cursor=Mathf.Max(cursor,cut.end);
        }
        safe.Add((cursor,float.PositiveInfinity));
        var v=new List<Vector3>();var n=new List<Vector3>();var u=new List<Vector2>();var c=new List<Color>();
        var faces=Enumerable.Range(0,mesh.subMeshCount).Select(_=>new List<int>()).ToArray();
        List<(Vector3 position,Vector3 normal,Vector2 uv,Color color,float d)> Clip(
            List<(Vector3 position,Vector3 normal,Vector2 uv,Color color,float d)> input,float edge,bool keepGreater)
        {
            if(input.Count==0||float.IsInfinity(edge))return input;
            var result=new List<(Vector3,Vector3,Vector2,Color,float)>(5);
            var prev=input[^1];
            foreach(var next in input)
            {
                bool left=keepGreater?prev.d>=edge:prev.d<=edge;
                bool right=keepGreater?next.d>=edge:next.d<=edge;
                if(left!=right)
                {
                    float t=(edge-prev.d)/(next.d-prev.d);
                    result.Add((Vector3.Lerp(prev.position,next.position,t),Vector3.Lerp(prev.normal,next.normal,t).normalized,
                         Vector2.Lerp(prev.uv,next.uv,t),Color.Lerp(prev.color,next.color,t),edge));
                }
                if(right)result.Add(next);
                prev=next;
            }
            return result;
        }
        for(int sub=0;sub<mesh.subMeshCount;sub++)
        {
            var indices=mesh.GetTriangles(sub);
            for(int i=0;i<indices.Length;i+=3)
            {
                var original=new List<(Vector3,Vector3,Vector2,Color,float)>(3);
                for(int j=0;j<3;j++)
                {
                    int k=indices[i+j];
                    original.Add((vertices[k],normals.Length>k?normals[k]:Vector3.up,uv.Length>k?uv[k]:Vector2.zero,
                        colors.Length>k?colors[k]:Color.white,distances[k]));
                }
                foreach(var interval in safe)
                {
                    var fragment=Clip(Clip(original,interval.min,true),interval.max,false);
                    if(fragment.Count<3)continue;
                    int offset=v.Count;
                    foreach(var point in fragment){v.Add(point.Item1);n.Add(point.Item2);u.Add(point.Item3);c.Add(point.Item4);}
                    for(int j=1;j+1<fragment.Count;j++)faces[sub].AddRange(new[]{offset,offset+j,offset+j+1});
                }
            }
        }
        var output=new Mesh{name=mesh.name+" · retained legacy outside replacement",
            indexFormat=v.Count>ushort.MaxValue?IndexFormat.UInt32:IndexFormat.UInt16,subMeshCount=mesh.subMeshCount};
        output.SetVertices(v);output.SetNormals(n);output.SetUVs(0,u);
        if(colors.Length>0)output.SetColors(c);
        for(int sub=0;sub<faces.Length;sub++)output.SetTriangles(faces[sub],sub);
        output.RecalculateBounds();
        return output;
    }

    static void ValidateProductionGrade(RailwayZoneStreamingManager manager,SplineContainer spline,
        InfrastructureDetailedLoader.Occurrence[] subset,List<string> rows)
    {
        var config=JsonUtility.FromJson<PilotDesign>(File.ReadAllText(InfraKit+"Design_Configuration.json")).pilotFinalization;
        var surface=subset.Where(item=>item.domain=="Subgrade"&&item.assetId.EndsWith("-Layer00",StringComparison.Ordinal))
            .OrderBy(item=>item.chainage).Select(item=>
                ContactPart(item.assetId,item.domain,item.owner,item.mesh.vertices.Select(item.placement.MultiplyPoint3x4).ToArray(),item.mesh.triangles)).ToArray();
        float length=spline.CalculateLength();
        foreach(float seam in config.subgradeBands.Skip(1).Select(band=>band.from))
        {
            float worst=0;
            int count=0;
            foreach(float lateral in new[]{-7.2f,-6.4f,-3.2f,0f,.8f})
            {
                float SurfaceAt(float d)
                {
                    Vector3 center=spline.EvaluatePosition(d/length);
                    var forward=Vector3.ProjectOnPlane((Vector3)spline.EvaluateTangent(d/length),Vector3.up).normalized;
                    var point=center+Vector3.Cross(Vector3.up,forward)*lateral;
                    return surface.Select(part=>MeshSurface(part,point.x,point.z).max).Max();
                }
                float a=SurfaceAt(seam-.05f),b=SurfaceAt(seam+.05f);
                if(float.IsFinite(a)&&float.IsFinite(b)){worst=Mathf.Max(worst,Mathf.Abs(a-b));count++;}
            }
            bool pass=count==5&&worst<=.002f;
            string method=Quoted($"Terrain section and Layer00 triangles in five lateral positions 50 mm either side; {count}/5 valid samples");
            rows.Add(FormattableString.Invariant($"SUB-TRANSITIONS,{seam:F3},{worst:G8},0.002,{(pass?"PASS":"FAIL")},{method}"));
            if(!pass)throw new InvalidOperationException("Subgrade rail support seam failed at "+seam);
        }
    }

    static void CaptureProductionSamples(List<(Scene scene,Transform root,string id)> installed,SplineContainer spline,float length)
    {
        foreach(var pair in new[]{("Forest_02",1160f,"Zone_012"),("Forest_01",2240f,"Zone_014"),
            ("Subgrade_Reinforced",525f,"Zone_011"),("Subgrade_Wall",641f,"Zone_011"),
            ("OCL_Open",3220f,"Zone_016"),("SO201_OCL",3950f,"Zone_018")})
        {
            var zone=installed.Single(item=>item.id==pair.Item3);
            var center=(Vector3)spline.EvaluatePosition(pair.Item2/length);
            var forward=Vector3.ProjectOnPlane((Vector3)spline.EvaluateTangent(pair.Item2/length),Vector3.up).normalized;
            var frame=Quaternion.LookRotation(forward);
            var target=center+Vector3.down*.5f;
            var eye=center+frame*new Vector3(30,50,-65);
            InfraShot(zone.scene,pair.Item1,zone.root,zone.root.InverseTransformPoint(eye),
                zone.root.InverseTransformPoint(target),34,ProdEvidence+"Screenshots/");
        }
    }
}
