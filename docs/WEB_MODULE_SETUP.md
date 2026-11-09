# Web module — prepared handoff, not a completed Web build

Inspected installation: Unity 6000.3.27f1 arm64, URP 17.3.0. `Unity.app/Contents/PlaybackEngines` contains only `MacStandaloneSupport`; Web and Windows modules are absent. No browser build, browser measurements or phone compatibility is claimed for G4. Existing PlayerSettings retain Web data caching, disabled threads, geometric memory growth from 32 MiB up to 2048 MiB, and the default Web template. These are inspected defaults, not validated mobile memory budgets; tune them only against real browser builds.

1. In the existing Editor open **File → Build Profiles → Add Build Profile → Web**.
2. Choose **Install with Unity Hub** for the missing platform. In Hub select the installed **6000.3.27f1** Editor and add **Web Build Support**. Keep this exact Editor version; do not upgrade the project as part of adding a module.
3. Once installed, confirm `WebGLSupport` is present under `/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/PlaybackEngines/`.
4. Reopen the project, create/activate the Web profile and include `Assets/_WATTSHIFT/Scenes/CitySlice.unity` as its enabled scene. Existing `EditorBuildSettings` already contains this scene. Keep the Web-first URP path and test all three runtime tiers.
5. After installing the module, build to ignored `Builds/Web`, use **Build And Run** or an HTTP server with the correct compression headers. Do not open the generated HTML through `file://`.
6. Record actual desktop browser/OS/hardware, load size/time, errors, memory and frame times; then test touch, safe areas, audio unlock and save persistence on real Android/iPhone browsers. Test private browsing/storage failure explicitly. Targets 60 desktop/30 mobile FPS remain goals until those runs.

Official Unity 6.3 references: [Platform Browser and module installation](https://docs.unity.com/en-us/engine/6000.3/manual/building-and-publishing/build-settings/platform-browser-reference), [Web development process and HTTP serving](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/intro/gettingstarted).

G4 Editor/macOS work is implemented; this Web/browser gate remains blocked by the missing module. No module download was started. Windows also requires its own Build Support module and separate runtime testing.

The installed Hub module catalogue lists **Web Build Support** (`webgl`) and **Windows Build Support (Mono)** (`windows-mono`). For Windows use **Hub → Installs → 6000.3.27f1 → Add modules → Windows Build Support (Mono)**, then **File → Build Profiles → Windows → Switch Platform**. Include only CitySlice and build to ignored `Builds/G4/Windows`. Creating a Windows build on macOS does not prove it runs on a Windows machine; that runtime gate remains separate.
