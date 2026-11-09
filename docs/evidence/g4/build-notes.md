# G4 native build and rendered automation

Tooling and protocol. Actual results belong to `mac-build.json` and the selected smoke output directory's `native-smoke.json` and PNGs.

`Wattshift.Editor.GateFourBuild.BuildMac` requires the existing active StandaloneOSX target and installed Mac module, uses the enabled CitySlice scene, temporarily selects ARM64, and builds a Development player at `Builds/G4/macOS/WATTSHIFT.app`. Architecture is restored afterward. It does not switch targets or attempt unavailable Web/Windows modules. The JSON records actual BuildReport result, duration, bytes, warnings and errors.

Launch the executable with a visible graphics session, without `-nographics`:

```sh
Builds/G4/macOS/WATTSHIFT.app/Contents/MacOS/WATTSHIFT -wattshiftSmoke -wattshiftEvidence -wattshiftEvidenceOutput /private/tmp/wattshift-g4-native -logFile /private/tmp/wattshift-g4-native-player.log
```

`BuildDiagnostics` must be installed by the G4 installer. Without `-wattshiftSmoke` it disables itself. It requires the evidence flag for save/settings isolation; the output defaults to Unity's temporary cache, and writing another directory requires the explicit output argument. Use an empty output directory per run to avoid stale PNGs.

The automation records actual native screenshots of menu, problem markers, expanded school card, each quality tier, resolved school and results. With the simulation paused and the school card open it samples approximately ten real seconds per Low/Medium/High tier after one warm-up second, at 20:00 with four active street lights. Median and P95 are observed frame intervals, FPS is rendered-frame count divided by elapsed wall time, and memory is Unity's allocated/reserved memory at the end of each window. Draw-call and batch counters use the median of positive observed samples across the measurement window, with sample count; zero/no-positive counters are reported unavailable and `-1`. Instrumentation and screenshot overhead make this development-player evidence, not a production benchmark. Scenario advancement after measurement uses deterministic `Tick(61)` and is not a real-time play-duration measurement.

It invokes speed 1/2/4, completes all three missions, verifies save/load, records scores/stars, writes its report and quits with code 0/1. A fresh process with additional `-wattshiftRestoreSmoke` checks the previous nine stars, completed clinic, hour and budget without repeating benchmark windows. MeshRenderer material/shader and mesh references are inspected from the loaded scene; arbitrary serialized references are not exhaustively audited.

Web download, browser console, Windows, Android, iPhone, touch gestures, human play and device thermal behavior remain unverified by this native automation. Compare actual measurements against `docs/PERFORMANCE.md`; do not infer browser/mobile acceptance from them.

The installed Unity 6000.3.27f1 Mac support assembly provided the architecture API; actual compilation and builds passed. BuildReport.totalWarnings/errors are int in this version.
