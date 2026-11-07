# WATTSHIFT — mandatory repo rules

Read `docs/GAME_DESIGN.md`, `docs/QUALITY_GATES.md` and `docs/ARCHITECTURE.md` before coding. Work in small accepted gates; Gate 1 is a visually verified 3D vertical slice.

- Unity 6.x LTS/installed compatible version, URP, C#, no external paid dependencies. Keep Unity YAML, .meta GUIDs and Packages/ProjectSettings consistent.
- Do not claim to have run Unity, imported a model, built WebGL, inspected screenshots or profiled a phone unless you actually did so; record evidence and blockers.
- Asset-first visuals: use approved, license-checked, **downloaded and inspected** 3D assets; never fabricate missing art with random primitives, sprites, unrelated image backdrops, or generated placeholder visuals and call it complete. Mark blocked if quality assets are missing. Small invisible primitives for colliders or prototyping may be used with clear TODO markers, but not shipped.
- Web desktop first, mobile web adaptive and separately tested; no guarantee every device. Desktop mouse + touch, safe areas, responsive layout, keyboard accessibility where practical.
- Commit self-contained, narrowly scoped changes. Assign each subagent distinct files; avoid concurrent edits of scenes, prefabs, ProjectSettings, packages, or shared interfaces.
- Prefer data-driven ScriptableObjects and composable, testable C# services; avoid monolithic managers and per-frame FindObjectOfType.
- Optimize for low draw calls, few material variants, pooled effects, baked lighting, LOD/culling, capped render scale, memory budget; do not sacrifice visible quality blindly.
- Report each stage: changed files, actual tests run, unverified items, screenshot paths (if created), blockers, next gate.
- Skills in `.agents/skills/`: invoke only the relevant one; don't read them all every task.
