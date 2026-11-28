import itertools,struct,zlib,collections,json
from pathlib import Path
p=Path('Assets/ExternalBIM/OverheadLine/Source/electric+pole.fbx')
b=p.read_bytes()
assert b.startswith(b'Kaydara FBX Binary')
version=struct.unpack_from('<I',b,23)[0]
assert version < 7500
def prop(k):
 t=chr(b[k]); k+=1
 formats={'Y':'h','C':'?','I':'i','F':'f','D':'d','L':'q'}
 if t in formats:
  fmt='<'+formats[t]
  return struct.unpack_from(fmt,b,k)[0],k+struct.calcsize(fmt)
 if t in 'SR':
  n=struct.unpack_from('<I',b,k)[0]
  raw=b[k+4:k+4+n]
  return (raw.decode(errors='replace') if t=='S' else len(raw)),k+4+n
 if t in 'fdlibc':
  n,flag,size=struct.unpack_from('<III',b,k); raw=b[k+12:k+12+size]
  if flag: raw=zlib.decompress(raw)
  fmt={'f':'f','d':'d','l':'q','i':'i','b':'?','c':'b'}[t]
  return struct.unpack('<'+str(n)+fmt,raw),k+12+size
 raise ValueError(t)
def nodes(k,limit):
 while k+13<=limit:
  e,n,_size,ln=struct.unpack_from('<IIIB',b,k)
  if e==0:break
  assert e>k and e<=limit
  name=b[k+13:k+13+ln].decode(errors='replace')
  pos=k+13+ln
  props=[]
  for _ in range(n):
   value,pos=prop(pos);props.append(value)
  yield name,props,list(nodes(pos,e))
  k=e
tree=list(nodes(27,len(b)))
units=next((p[-1] for name,_,kids in tree if name=='GlobalSettings' for _,_,props in kids for _,p,_ in props if p and p[0]=='UnitScaleFactor'),None)
objects=next(group for name,_props,group in tree if name=='Objects')
records=[]
for role,params,children in objects:
 if role!='Geometry' or params[-1]!='Mesh':continue
 vertices=next(p[0] for n,p,kids in children if n=='Vertices')
 polygons=next(p[0] for n,p,kids in children if n=='PolygonVertexIndex')
 unique=[vertices[i::3] for i in range(3)]
 records.append(dict(id=params[0],name=params[1].split('\0')[0],vertices=len(vertices)//3,polygons=sum(1 for p in polygons if p<0),
  size=[max(c)-min(c) for c in unique], bounds=[(min(c),max(c)) for c in unique]))
out=dict(version=version,unitScaleFactorInCm=units,meshes=records,
 materialNames=[p[1].split('\0')[0] for n,p,c in objects if n=='Material'],
 modelNames=[p[1].split('\0')[0] for n,p,c in objects if n=='Model'])
Path('Temp/InfrastructureG1/FBX_Inventory.json').write_text(json.dumps(out,ensure_ascii=False),encoding='utf-8')
print(f'{len(records)} FBX meshes, {len(out["materialNames"])} materials, UnitScaleFactorCm={units}')
print(sorted([(r['name'],r['vertices'],[round(x,3) for x in r['size']]) for r in records],key=lambda r:-max(r[2]))[:8])
