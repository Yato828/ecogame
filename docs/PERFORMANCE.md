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
