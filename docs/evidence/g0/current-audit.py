import csv, hashlib, json, pathlib, struct, zipfile, collections
ROOT=pathlib.Path(__file__).resolve().parents[3]
META={'suburban_20':('Suburban','City Kit Suburban','city-kit-suburban'),'roads':('Roads','City Kit Roads','city-kit-roads'),'commercial_2.1':('Commercial','City Kit Commercial','city-kit-commercial'),'industrial_2.0':('Industrial','City Kit Industrial','city-kit-industrial')}
SELECT={'Suburban':['building-type-a','building-type-b','building-type-c','building-type-d','building-type-e','building-type-f','tree-large','tree-small','path-long','path-short','path-stones-long','planter','fence-low'], 'Commercial':['building-e','building-c','building-a','building-b'], 'Roads':['road-straight','road-crossing','road-crossroad','road-bend','road-end','road-bend-square','road-square','road-curve','road-intersection','tile-low','tile-high','light-curved'], 'Industrial':['building-a','solar-panel-flat','solar-panel-landscape-group']}
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def rel(p):return str(p.relative_to(ROOT))
def glb(p):
 d=p.read_bytes(); assert d[:4]==b'glTF'; n,k=struct.unpack_from('<II',d,12); assert k==0x4e4f534a; return json.loads(d[20:20+n])
def obj(p):
 lines=p.read_text().splitlines();v=[tuple(map(float,l.split()[1:4])) for l in lines if l.startswith('v ')]; faces=[l.split()[1:] for l in lines if l.startswith('f ')];lo=[min(x[i] for x in v) for i in range(3)];hi=[max(x[i] for x in v) for i in range(3)]; mats=[l[7:].strip() for l in lines if l.startswith('mtllib ')]; textures=[]
 for m in mats:
  mp=p.parent/m; assert mp.is_file()
  for l in mp.read_text().splitlines():
   if l.startswith('map_Kd '):
    tp=mp.parent/l[7:].strip();assert tp.is_file();textures.append(rel(tp))
 return dict(vertices=len(v),obj_triangles=sum(len(f)-2 for f in faces),bounds_min=lo,bounds_max=hi,material_files=[rel(p.parent/m) for m in mats],textures=textures,uv_count=sum(l.startswith('vt ') for l in lines),normal_count=sum(l.startswith('vn ') for l in lines))
rows=[];register=[];packs=[]
for suffix,(short,name,slug) in META.items():
 zip_path=ROOT/f'assets_staging/downloads/kenney_city-kit-{suffix}.zip'; stage=ROOT/f'assets_staging/unpacked/kenney_city-kit-{suffix}'
 with zipfile.ZipFile(zip_path) as z:
  assert z.testzip() is None
  assert all(not pathlib.PurePosixPath(n).is_absolute() and '..' not in pathlib.PurePosixPath(n).parts for n in z.namelist())
 lic=stage/'License.txt';text=lic.read_text();assert 'Creative Commons Zero, CC0' in text and 'Created/distributed by Kenney' in text
 geometry={p.stem:obj(p) for p in stage.rglob('*.obj')};gltfs={p.stem:glb(p) for p in stage.rglob('*.glb')}
 formats=collections.Counter(); mesh_paths=sorted(p for p in stage.rglob('*') if p.suffix.lower() in ('.fbx','.obj','.glb'))
 for p in mesh_paths:
  formats[p.suffix.lower()]+=1;g=geometry[p.stem];j=gltfs[p.stem]; tri=sum(j['accessors'][prim['indices']]['count']//3 for mesh in j['meshes'] for prim in mesh['primitives']);images=[i['uri'] for i in j.get('images',[]) if 'uri' in i]
  for uri in images:assert (stage/'Models/GLB format'/uri).is_file()
  rows.append(dict(source_pack=name,model_name=p.stem,file_format=p.suffix[1:].upper(),original_file=rel(p),sha256=sha(p),bytes=p.stat().st_size,companion_obj_vertices=g['vertices'],companion_obj_triangles=g['obj_triangles'],companion_glb_triangles=tri,bounds_min=json.dumps(g['bounds_min']),bounds_max=json.dumps(g['bounds_max']),obj_uv_count=g['uv_count'],obj_normal_count=g['normal_count'],material_files=';'.join(g['material_files']),texture_files=';'.join(g['textures']),glb_image_references=';'.join(images),g1_selected=p.stem in SELECT[short],unity_verified=False))
  if p.suffix.lower()=='.fbx' and p.stem in SELECT[short]:
   role=('school representation' if short=='Commercial' and p.stem=='building-e' else 'clinic representation' if short=='Commercial' and p.stem=='building-c' else 'future energy content, optional G1' if short=='Industrial' else 'G1 neighborhood')
   register.append(dict(source_pack=name,source_url=f'https://kenney.nl/assets/{slug}',license_file=rel(lic),original_file=rel(p),unity_asset_path=f'Assets/_WATTSHIFT/Art/Imported/{short}/{p.name}',sha256=sha(p),verified_in_scene='false',notes=f'{role}; archive_sha256={sha(zip_path)}; license_sha256={sha(lic)}; OBJ bounds={g["bounds_min"]}..{g["bounds_max"]}; OBJ triangles={g["obj_triangles"]}; GLB triangles={tri}; FBX metrics pending Unity; default texture=Textures/colormap.png; selected from inspected original preview; archive user-supplied; remote checksum not independently authenticated'))
 pack=dict(source_pack=name,source_url=f'https://kenney.nl/assets/{slug}',archive=rel(zip_path),archive_bytes=zip_path.stat().st_size,archive_sha256=sha(zip_path),license_file=rel(lic),license_sha256=sha(lic),license='CC0-1.0',zip_crc='PASS',path_safety='PASS',formats=dict(formats),distinct_models=len(geometry),texture_files=[],selected_models=SELECT[short])
 for p in sorted(stage.rglob('*.png')):
  if '/Models/' in str(p):
   d=p.read_bytes();w,h=struct.unpack('>II',d[16:24]);pack['texture_files'].append(dict(path=rel(p),sha256=sha(p),width=w,height=h))
 packs.append(pack)
def csv_write(path,rows):
 with path.open('w',newline='') as f:
  w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
csv_write(ROOT/'assets_manifest/model_inventory.csv',rows)
csv_write(ROOT/'assets_manifest/asset_register.csv',register)
packrows=[dict(source_pack=p['source_pack'],source_url=p['source_url'],source_page_license='CC0',archive_downloaded=True,archive_sha256=p['archive_sha256'],bundled_license_file=p['license_file'],model_names=';'.join(sorted({r['model_name'] for r in rows if r['source_pack']==p['source_pack']})),file_formats=';'.join(p['formats']),selection_status='APPROVED_FOR_G1_IMPORT',blocker='') for p in packs]
csv_write(ROOT/'assets_manifest/pack_registration.csv',packrows)
(ROOT/'docs/evidence/g0/current-asset-audit.json').write_text(json.dumps(dict(date='2026-11-07',provenance='User-supplied original official archives; no independent remote checksum authentication',packs=packs,model_files=len(rows),distinct_models=sum(p['distinct_models'] for p in packs),g1_selected_fbx_files=len(register),unity_import_verified=False),indent=2)+'\n')
sources=json.loads((ROOT/'assets_manifest/sources.json').read_text())
for s in sources:
 for p in packs:
  if s['name']==p['source_pack']:s.update(downloaded=True,verified_in_unity=False,last_checked='2026-11-07',source_page_license='CC0; bundled License.txt also verified',download_status='PASS: user-supplied original official ZIP, copied from /Users/yato/Downloads; CRC/path safety verified',archive_sha256=p['archive_sha256'],archive_path=p['archive'],bundled_license_verified=True,license_file=p['license_file'],license_sha256=p['license_sha256'],model_inventory_status=f'PASS: {p["distinct_models"]} distinct model names; FBX/OBJ/GLB inventoried',provenance='User reports official manual download; remote checksum not independently authenticated')
(ROOT/'assets_manifest/sources.json').write_text(json.dumps(sources,indent=2)+'\n')
print(json.dumps(dict(packs=[{k:p[k] for k in ['source_pack','archive_sha256','formats','distinct_models']} for p in packs],model_files=len(rows),selected=len(register)),indent=2))
