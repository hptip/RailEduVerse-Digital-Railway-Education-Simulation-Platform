"""Standard-library-only evidence checker; does NOT test Unity geometry or certify design."""
import argparse,csv,json,hashlib,sys,re
from pathlib import Path
KIT=Path(__file__).resolve().parents[1]
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def rows(p):
    with p.open(encoding='utf-8-sig',newline='') as f: return list(csv.DictReader(f))
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''): h.update(b)
    return h.hexdigest()
def preflight(project):
    errors=[]
    for s in load(KIT/'Source_Manifest.json')['sources']:
        p=project/s['path']
        if not p.is_file(): errors.append('MISSING '+s['path'])
        elif p.stat().st_size!=s['bytes'] or digest(p)!=s['sha256']: errors.append('HASH_MISMATCH '+s['path'])
    for name in ['00_Overview.png','01_OCL_Design.png','02_Tunnel_Design.png','03_Subgrade_BIM_Viewer.png']:
        if not (KIT/'References'/name).is_file(): errors.append('MISSING reference '+name)
    return errors
def gate(stage,evidence):
    errors=[]; target=int(stage[1]); criteria=rows(KIT/'Acceptance_Criteria.csv')
    results=rows(evidence/'Acceptance_Results.csv'); idx={}
    valid={x['test_id'] for x in criteria}
    for r in results:
        k=r['test_id']
        if k in idx: errors.append('DUPLICATE test '+k)
        if k not in valid: errors.append('UNKNOWN test '+k)
        idx[k]=r
    for c in criteria:
        if int(c['gate'][1])>target: continue
        k=c['test_id']; r=idx.get(k,{})
        state=r.get('status','MISSING')
        if c['method']=='optional' and state=='NOT_TESTED': continue
        if state!='PASS': errors.append(k+' '+state); continue
        # Evidence is one relative path under Evidence, not a claim in free text.
        v=r.get('evidence','').strip()
        path=(evidence/v).resolve()
        if not v or not path.is_relative_to(evidence.resolve()) or not path.is_file() or path.stat().st_size==0:
            errors.append(k+' missing/invalid evidence file')
        if c['method']=='geometry' and not all(r.get(x,'').strip() for x in ('measured','threshold')):
            errors.append(k+' missing measured/threshold (use documented qualitative condition where needed)')
    if target>=1:
        for n in ('Resolved_Geometry.json','Candidate_Manifest.csv','Visual_Review.md'):
            if not (evidence/n).is_file(): errors.append('MISSING '+n)
        rg=evidence/'Resolved_Geometry.json'
        if rg.is_file():
            data=load(rg)
            if not data.get('coordinate_frame') or not data.get('measurements'): errors.append('Unresolved geometry measurements')
    if target>=2:
        a=evidence/'Approval.json'
        if not a.is_file(): errors.append('MISSING user approval')
        else:
            data=load(a); fp=data.get('candidate_fingerprint','')
            if data.get('approved') is not True or not data.get('user_confirmation','').strip() or not re.fullmatch('[0-9a-fA-F]{64}',fp) or not {'OCL','Tunnel','Subgrade','BIMViewer'}.issubset(set(data.get('scope',[]))): errors.append('Approval incomplete')
            # Bind approval to a frozen manifest of actual candidate artifacts; Codex must verify it against the generator and output.
            manifest_name=data.get('candidate_manifest','Candidate_Manifest.csv')
            mf=(evidence/manifest_name).resolve()
            if not mf.is_relative_to(evidence.resolve()) or not mf.is_file():
                errors.append('Approved manifest is missing or outside Evidence')
            elif fp.lower()!=digest(mf):
                errors.append('Approved manifest fingerprint mismatch')
        reg=evidence/'Asset_Registry.csv'
        if not reg.is_file(): errors.append('MISSING Asset_Registry.csv')
        else:
            rr=rows(reg); seen=set(); slots=set(); domains=set()
            for r in rr:
                k=r.get('asset_id','').strip(); slot=(r.get('domain',''),r.get('physical_slot',''))
                required=['asset_id','domain','owner_zone','source_kind','source_ref','geometry_ref','detailed_ref','physical_slot']
                if not all(r.get(x,'').strip() for x in required): errors.append('Incomplete asset '+k)
                if k in seen or slot in slots: errors.append('Duplicate asset or physical slot '+k)
                seen.add(k);slots.add(slot);domains.add(r.get('domain'))
            if not {'OCL','Tunnel','Subgrade'}.issubset(domains): errors.append('Missing domain in Asset_Registry')
    if target>=3:
        for n in ('Final_Implementation_Report.md','Changed_Files_And_Rollback.md'):
            if not (evidence/n).is_file(): errors.append('MISSING '+n)
    return errors
def main():
    ap=argparse.ArgumentParser(description=__doc__); sub=ap.add_subparsers(dest='mode',required=True)
    p=sub.add_parser('preflight'); p.add_argument('--project',type=Path,required=True)
    g=sub.add_parser('gate'); g.add_argument('--stage',choices=['G0','G1','G2','G3'],required=True);g.add_argument('--evidence',type=Path,default=KIT/'Evidence')
    a=ap.parse_args()
    try: errs=preflight(a.project) if a.mode=='preflight' else gate(a.stage,a.evidence)
    except (OSError,ValueError,KeyError,TypeError) as e: errs=[str(e)]
    print('EVIDENCE CHECK '+('FAIL' if errs else 'PASS')+' — NOT a Unity geometry test')
    for e in errs[:40]: print('- '+e)
    if len(errs)>40: print('... '+str(len(errs)-40)+' more issues')
    return 1 if errs else 0
if __name__=='__main__': sys.exit(main())
