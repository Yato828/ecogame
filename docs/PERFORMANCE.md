# Performance plan and acceptance

Primary targets are budgets, not observed benchmarks. Use profiling before claiming success.

- Web desktop: aim 60 FPS at 1080p midrange GPU; low tier aim >=30 FPS at 720p with downscaled render if needed.
- Mobile web: aim 30 FPS on representative newer phone; avoid assuming iOS WebGL behavior equals native Unity iOS.
- Initial Web download target < 80 MB compressed (stretch < 50 MB); first scene memory target < 300 MB on mobile, to validate on devices and tune.
- Minimize textures: stylized shared palette + few atlases; no 4K textures by default; no excess normal maps. ASTC/ETC2/mobile/web texture formats to be tested by actual builds.
- Static batching where useful; SRP Batcher compatible shaders; instancing for repeated lights/trees if appropriate.
- Bake GI/static lightmaps when supported and necessary; only few realtime/dynamic lights; emulated emissive window glow without dozens of shadow-casting point lights.
- Limit shadows, shadow distance, postprocessing and MSAA in lower quality tiers. Avoid expensive screen-space effects on mobile.
- Avoid runtime allocations/GC spikes from simulation/UI updates; cap update frequency and cache references.
- Provide quality Low/Medium/High and auto default; test with Unity Profiler, Frame Debugger, browser devtools, real desktop, real Android and iPhone.
- Touch: >=44 CSS pixel-equivalent touch targets where feasible, safe-area support, readable labels, reduced UI density.
- Test actual browser matrix and record crashes, load time, FPS, device+OS+browser details. No 'runs on every device' marketing claim.

## G4 measured scope (2026-11-09)

ARM64 Development macOS Player, Apple M4, Metal, 1600x900; paused 20:00 district with four streetlights, problem markers and expanded card; ~10 seconds/tier. Foreground rerun observed approximately 60 FPS on each tier, median 16.7 ms/P95 ~17.5 ms; draw-call medians 106/175/182 and Unity allocated ~89.2-89.4 MiB, reserved ~220.2 MiB. Not full process RSS or GPU memory. Raw: docs/evidence/g4/native-foreground/native-smoke.json.

An earlier direct-executable run of the same binary measured High 27.97 FPS with intermittent long frames (P95 71.75 ms). Both are retained; launch/focus/scheduling influence remains unproven. Do not claim sustained 60 FPS, thermal/leak stability or mobile/browser targets. High graphics are preserved. Low/Medium reduce scale, shadows/MSAA/cascades and per-object light limits; observed draw counts are lower.

Counters use median positive ProfilerRecorder samples with sample count; zero-only counters are unavailable. Anchors are cached, simulation/UI updates run at 5 Hz, forecasts use the same integrator, offer controls persist across clock ticks. No speculative LOD/pooling rewrite was introduced without measured need. Browser/device and active-play profiling remain separate from this paused-render sample.
