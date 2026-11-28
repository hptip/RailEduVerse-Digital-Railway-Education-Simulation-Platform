import re,json
from pathlib import Path
root=Path('Assets/ExternalBIM/Subgrade/Source/UT-Earthworks-01.ifc')
s=root.read_text(encoding='ascii')
assert re.search(r"#9=\s*IFCCARTESIANPOINT\(\(0\.,0\.,0\.\)\)",s)
assert re.search(r"#22=\s*IFCLOCALPLACEMENT\(\$,#8\)",s)
assert '.LENGTHUNIT.,$,.METRE.' in s
triples=r'\(([-+\d.Ee]+),([-+\d.Ee]+),([-+\d.Ee]+)\)'
records=[]
for prod,faces,coords in [(31,29,30),(33,36,37)]:
 p=re.search(rf'(?m)^#{prod}=\s*(\w+)\(\x27([^\x27]+)',s)
 v=re.search(rf'(?m)^#{coords}=\s*IFCCARTESIANPOINTLIST3D\((.*?);',s)
 f=re.search(rf'(?m)^#{faces}=\s*\w+\(#{coords},\$,\.[FT]\.,(\(\(.*?\)\))(?:,|\))',s)
 assert p and v and f
 pts=[tuple(map(float,m)) for m in re.findall(triples,v[1])]
 ids=[int(i)-1 for t in re.findall(triples,f[1]+')') for i in t]
 assert pts and ids and min(ids)>=0 and max(ids)<len(pts) and len(ids)%3==0
 records.append(dict(ifcClass=p[1],globalId=p[2],points=[dict(x=x,y=z,z=y) for x,y,z in pts],triangles=[idx for i in range(0,len(ids),3) for idx in (ids[i],ids[i+2],ids[i+1])]))
Path('Temp/InfrastructureG1/EarthworksTessellation.json').write_text(json.dumps(dict(parts=records)),encoding='utf-8')
print([(r['ifcClass'],len(r['points']),len(r['triangles'])//3) for r in records])

