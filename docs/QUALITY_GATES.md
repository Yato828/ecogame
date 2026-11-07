# Quality gates — do not skip

G0 Preflight: verify Unity installed editor and exact Unity version; confirm URP package/build modules and available workspace; confirm asset links/licenses. Note if download/network blocked. Output an asset register of actual downloaded models. No fake import claims.

G1 Visual vertical slice: render a single coherent *real Unity 3D* neighborhood with at least 6 distinct approved building models, roads, park, school/clinic representation, 4 streetlights, controlled isometric camera, cohesive materials, real shadows and stable framerate. Click building -> polished context panel. Provide actual screenshots from Editor/Game view and import manifest. Stop for visual approval. No fake 2D backgrounds.

G2 Core simulation: baseline vs optimized kW/kWh calculations, comfort/safety constraints, upgrade economy, 1 playable mission and unit tests. Interactive lights/window emissive states must respond. Save/load works.

G3 Full content: 3 missions with distinct decisions; tutorial, failure hints, scoring, completion flow, RU UI, sound, accessible input.

G4 Polish and portability: low/medium/high graphics, adaptive desktop/touch UI, no missing refs/console exceptions, Web build, Windows/macOS when editor targets available, browser/device testing and profiling report.

Definition of Done: Unity opens without unresolved packages; every gameplay button works; all model textures/materials attached; functional tutorial from start to win; 3 missions; stable save; no console errors during golden path; build is tested on an actual browser. Record any unmet acceptance criterion as FAIL/BLOCKED, not PASS.
