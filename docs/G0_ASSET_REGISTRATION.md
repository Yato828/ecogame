# Gate 0 asset registration — PASS (preflight)

Inspection: 2026-11-07, Europe/Minsk. The previous HTTP-403 download blocker was resolved by user-supplied archives. PASS covers preflight/source inspection only; Unity import, materials, FBX transforms and screenshots belong to G1 and are not yet verified.

## Environment

Installed editor `/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app`: **6000.3.27f1**, bundle metadata; Hub identifies Unity 6.3 LTS, arm64. Integrator also executed Editor `-version`, which returned 6000.3.27f1. Editor batch startup log is `docs/evidence/g0/editor-preflight.log`; licensing/project opening is still unresolved at audit handoff. Bundled **URP 17.3.0**, Core 17.3.0, Shader Graph 17.3.0 and URP Config 17.0.3 exist. `MacStandaloneSupport` is present; Web/Windows modules absent at inspected PlaybackEngines locations. Approximately 55 GiB free. These observations do not demonstrate activation, package resolution or working builds. Original details: [environment.json](evidence/g0/environment.json). Historical HTTP failures remain as evidence; they no longer describe asset availability.

## Archives, licenses and inventory

At resumed inspection `assets_staging/downloads/` was empty; the integrator found the four explicitly supplied files in `/Users/yato/Downloads/` and copied them into staging. They were extracted into separate `assets_staging/unpacked/<archive-stem>/` directories. Provenance: **user-supplied original official downloads**; no independent remote checksum authentication is claimed.

| Pack | Official source | Distinct mesh names | Actual formats | ZIP bytes |
| --- | --- | ---: | --- | ---: |
| Suburban 2.0 | https://kenney.nl/assets/city-kit-suburban | 40 | 40 FBX, 40 OBJ, 40 GLB | 3,038,740 |
| Roads 2.2 | https://kenney.nl/assets/city-kit-roads | 95 | 95 FBX, 95 OBJ, 95 GLB | 2,852,289 |
| Commercial 2.1 | https://kenney.nl/assets/city-kit-commercial | 41 | 41 FBX, 41 OBJ, 41 GLB | 4,096,974 |
| Industrial 2.0 | https://kenney.nl/assets/city-kit-industrial | 37 | 37 FBX, 37 OBJ, 37 GLB | 5,045,077 |

**213 distinct mesh names / 639 model files inventoried.** Counts are actual contents, not advertised counts. All four ZIPs passed full CRC checks (`ZipFile.testzip()`) and absolute-path/`..` traversal checks. Each root `License.txt` explicitly names Kenney and **Creative Commons Zero, CC0**, allowing personal, educational and commercial use; credit is optional. Archive/license SHA-256 values, bytes, texture dimensions and selected names: [current-asset-audit.json](evidence/g0/current-asset-audit.json). Local hashes establish reproducibility, not remote authenticity.

Every FBX/OBJ/GLB filename, hash and byte count appears in `assets_manifest/model_inventory.csv`. Companion OBJ geometry was parsed for bounds, triangle/vertex/UV/normal counts and MTL references. All referenced OBJ MTL files and `map_Kd` textures exist. GLB JSON was parsed for indexed triangle counts and image references; all external GLB images exist. Default material is `colormap`, using `Textures/colormap.png`. All model palette PNGs are **512×512**. Alternate palette files were inventoried; do not mix variants without review.

## Actual visual inspection and selection

Viewed original `Preview.png` from all four packs using the image viewer, plus individual previews for Commercial `building-e`/`building-c`, Suburban `tree-large`, Roads `tile-low`, `road-straight`, `road-crossroad`, `road-bend`, `road-bend-square`, `road-square`, `road-curve`. These are source previews, not Unity evidence. They show consistent white/slate city styling, green roofs/foliage and compatible detail density.

Approved G1 import candidates (exact FBX paths/hashes in `asset_register.csv`; initial `verified_in_scene=false`):

- Six residential models: Suburban `building-type-a` through `building-type-f`. OBJ sizes X/Y/Z range 1.286–1.828 / 0.834–1.238 / 1.028–1.406.
- Civic roles: Commercial `building-e` (wide low-rise, size 1.640/0.893/1.008) as school; `building-c` (compact three-storey, 0.884/0.893/1.090) as clinic. These are roles assigned to genuine generic meshes, not purpose-built school/clinic assets. Label roles in UI. Optional `building-a`/`building-b` provide additional town buildings.
- Roads: `road-straight`, `road-crossing`, `road-crossroad`, `road-intersection`, `road-square`, `road-bend-square`, `road-curve`, `road-bend`, `road-end`. Matching 1×1 modules; surfaces Y=0–0.02. `road-bend` preview has a rounded profile; verify connection orientation in Unity before assembly rather than assuming a right-angle corner.
- Terrain: Roads `tile-low` (1×0.02×1), `tile-high` (1×0.25×1). Reusing/tinting these genuine source meshes for ground/park is permitted. Their default palette is gray; green park material is deliberate material treatment, not fabricated geometry.
- Lamps: Roads `light-curved`, size 0.050/0.675/0.225, repeat at least four times. Park: Suburban `tree-large`/`tree-small` (heights 0.767/0.567), `path-long`, `path-short`, `path-stones-long`, `planter`, `fence-low`. No bench exists in these four inventories; do not invent one or substitute a primitive.
- Industrial `building-a`, `solar-panel-flat`, `solar-panel-landscape-group`: optional future energy content, not required to clutter G1.

Selected OBJ structures are Y-up, near base Y=0 and centered X/Z. FBX axes/scale/materials must be checked in Unity. OBJ sometimes contains doubled coincident surfaces: `building-type-a` has 2,348 OBJ triangles but 1,174 unique position-triangles; companion GLB has 1,174 indexed triangles and `doubleSided=true`. OBJ counts are not FBX runtime cost. Selected tree GLBs have 42 triangles; curved lamp GLB 92 triangles. Runtime FBX counts await Unity inspection.

## Reproducibility and next gate

`python3 docs/evidence/g0/current-audit.py` repeats ZIP/license/reference checks and regenerates inventory, initial selected register and source records. Do not rerun after G1 import verification without preserving those updates: it deliberately initializes imports false.

Audit changed: `assets_manifest/sources.json`, `pack_registration.csv`, `model_inventory.csv`, `asset_register.csv`; this report; `docs/evidence/g0/current-audit.py`, `current-asset-audit.json`, `current-validation.txt`. Audit agent did not edit Unity scenes, packages or project settings.

G0: **PASS — asset preflight available for G1**. Later build prerequisite: install matching Web Build Support; Windows support also absent. Activation/import/render/build performance remain untested by this audit. Next: import genuine FBX meshes and palettes; verify URP materials, shadows, scale/pivots; assemble neighborhood and polished selection UI; supply actual Unity Editor/Game screenshots at desktop/mobile aspect. Stop for human visual approval before G2. No screenshots were created by this audit.
