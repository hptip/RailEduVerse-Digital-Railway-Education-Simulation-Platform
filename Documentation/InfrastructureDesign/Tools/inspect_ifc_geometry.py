import sys,json,collections
from pathlib import Path
sys.path.insert(0,r'F:\BIM_Workspace\Tools\IfcConvert')
import ifcopenshell, ifcopenshell.geom
target=Path('Temp/InfrastructureG1/ifc.json')
report={}
for name,path in [('Tunnel','Assets/ExternalBIM/Tunnel/Source/LoD5.ifc'),('Subgrade','Assets/ExternalBIM/Subgrade/Source/UT-Earthworks-01.ifc')]:
 rec={}
 try:
  model=ifcopenshell.open(path)
  rec.update(schema=model.schema,products=dict(collections.Counter(p.is_a() for p in model.by_type('IfcProduct'))))
  settings=ifcopenshell.geom.settings()
  settings.set('use-world-coords',True)
  selected=[]
  for p in model.by_type('IfcProduct'):
   if not p.Representation or p.is_a('IfcSpace') or p.is_a('IfcOpeningElement'): continue
   try:
    shape=ifcopenshell.geom.create_shape(settings,p).geometry
    verts,faces=list(shape.verts),list(shape.faces)
    if not verts: continue
    coords=[verts[i::3] for i in range(3)]
    selected.append(dict(id=p.GlobalId,cls=p.is_a(),name=p.Name or '',min=[min(a) for a in coords],max=[max(a) for a in coords],triangles=len(faces)//3))
   except Exception as e:
    rec['geometryErrors']=rec.get('geometryErrors',0)+1
  rec['parts']=selected
 except Exception as e:
  rec['error']=str(e)
 report[name]=rec
 target.write_text(json.dumps(report,indent=2),encoding='utf-8')
 print(name,rec.get('schema'),len(rec.get('parts',[])),rec.get('error',''),flush=True)
