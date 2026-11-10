# G5 build and Web hosting preparation

Status: Web build, browser testing, mobile testing and public hosting are **BLOCKED**. The installed 6000.3.27f1 editor has MacStandaloneSupport only; Web and Windows modules are absent. This work does not install modules or publish anything. No Web payload, Vercel configuration or runtime/browser performance result was fabricated.

## Build entry points

- `Wattshift.Editor.GateFiveBuild.BuildMacRelease`: existing enabled CitySlice scene; macOS ARM64, `BuildOptions.None` (Development false); output `Builds/G5/macOS/WATTSHIFT.app`; actual returned BuildReport summary goes to `docs/evidence/g5/mac-release-build.json`. This document makes no independent claim that the entry point was run; inspect that JSON and the parent evidence for the actual run.
- `BuildWebDevelopment` / `BuildWebRelease`: outputs `Builds/G5/WebDevelopment` / `Builds/G5/WebRelease`; reports `web-development-build.json` / `web-release-build.json`. Each checks installed target support first, then requires that target already active. Neither switches targets. Missing support writes a Blocked report, then throws. Template selection is scoped to the actual supported build and restored; no compression, stripping or quality setting is changed.
- The custom `PROJECT:WATTSHIFT` template uses Unity's expanded data/framework/code/loader/company/product/version values, default `StreamingAssets` URL, Russian loading/error text, a responsive safe-area canvas, keyboard focus and `touch-action: none`. No external fonts, fake screenshot or image backdrop is used.

The template sets `autoSyncPersistentDataPath: true`, which Unity documents as automatically persisting writes in `Application.persistentDataPath` to browser storage. This alone does not prove persistence, successful quota allocation or reload recovery; the separate browser sync/error bridge and real reload test remain necessary. [Unity 6000.3 Web template configuration](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/building-distribution/templates/web-templates-build-configuration).

## Hosting after a real Web build exists

Run `python3 tools/prepare_web_host.py Builds/G5/WebRelease` to inspect files without writing configuration. It refuses absent/unexpanded HTML, missing referenced loader/framework/wasm/data payloads and symlinks outside the build root. It checks file presence and template references; it cannot establish that arbitrary bytes are valid Unity output. Use only actual BuildReport-backed Unity output for hosting.

Then `python3 tools/prepare_web_host.py Builds/G5/WebRelease --write-config` writes a `vercel.json` inside that validated output. `--serve --port 8080` serves the same inspected files on loopback with the same MIME/encoding/cache policy. It does not deploy. Publish the actual WebRelease directory as the static project root after validation; no SPA rewrites, fabricated entry page or build-directory placeholder is supplied.

Wasm uses `application/wasm`, JavaScript `application/javascript`, data `application/octet-stream`. Files ending in `.br` / `.gz` get matching `Content-Encoding`; `.unityweb` is left as octet-stream without Content-Encoding for Unity's JS fallback. HTML and named assets use `no-cache`; immutable caching requires an actual 32-hex hash segment in the filename. The configuration contains per-file rules from existing output rather than assumed compression filenames.

Use a stable production origin and directory URL, for example the chosen production domain's `/` or `/wattshift/`, across updates. Keep company/product IDs stable. Versioned deployment-preview origins and changed directory URLs are separate persistence contexts. Browser storage may be denied/cleared; do not treat a successful in-memory save as durable until the reload test passes. Unity documents the Web IndexedDB path derived from the URL directory: [Application.persistentDataPath](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/application/persistentdatapath).

## Actual validation in this subtask

- `python3 -m unittest discover -s tools/tests -p test_prepare_web_host.py -v`: 6 synthetic fixture tests passed (MIME/encoding including fallback, cache policy, missing build refusal without writes, unexpanded template refusal, required payload validation, and actual loopback HTTP response headers). The sandbox denied loopback binding on the first six-test attempt; the identical suite passed after approved sandbox escalation. Fixture bytes are explicitly not Unity output.
- `python3 tools/prepare_web_host.py Builds/G5/WebRelease`: correctly refused with exit 2, `BLOCKED: no generated index.html; run a real Unity Web build first`. No config created.
- Python bytecode compilation passed with its cache directed to `/private/tmp/wattshift-g5-pycache`; the first default-cache attempt was denied by sandbox filesystem restrictions.
- No Unity run, Web build, native build, browser run, screenshot, device test or profiling was performed by this subtask. Parent-run Unity/native evidence is separate.

Next gate: install the exact compatible Web module through an explicitly authorized workflow, select Web as an explicit operator action, produce development output, verify real browser startup/console/persistence and compression headers, then measure before changing stripping/compression. Produce release output only after those findings; phone coverage remains separately tested and limited to observed devices.
