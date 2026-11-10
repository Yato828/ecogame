# Current status — Gameplay Rescue

New default player flow: 17 data-driven situations (5/5/4 + 3 final), one source/choice/safety template, Iskra introduction, retry explanations, chapter stars, mistake-based final tips, real source-model feedback. Existing seven upgrades run through the original simulation; no budget/time/scanner barrier in the player UI. Scene, models, materials, lighting assets and packages unchanged.

Progress uses the existing schema-3 CampaignSaveService with an additive Tasks field and separate wattshift-player-campaign.json. Legacy v3/v4 campaign files remain untouched. Open chapters and stars derive from saved completed IDs/errors; tutorial replay temporarily isolates progress. G5 work present before this request was preserved in /private/tmp/wattshift-before-gameplay-rescue (tracked.patch + untracked.tar.gz). The incompatible NUnit attribute was removed; no reset or source deletion.

Validation: Unity 6000.3.27f1, URP 17.3.0, isolated Editor project /private/tmp/WATTSHIFT_Validation. PASS: 103/103 Edit Mode and 18/18 Play Mode. Final results: docs/evidence/rescue/editmode.xml and playmode.xml; actual Game View captures alongside them. Includes retained legacy simulation/UX/camera tests and new full campaign/save/UI tests. Raw logs and first failed passes stay local/ignored. No new platform builds requested or produced for this rescue. Physical touch devices, audible playback and child usability/session duration NOT TESTED. Web/Windows still BLOCKED by missing build modules. G5 release acceptance remains incomplete; no G6 work.

Open Assets/_WATTSHIFT/Scenes/CitySlice.unity and enter Play Mode. Choose Начать → school ! → a solution → read explanation → Дальше. Source tasks first ask for the unnecessary load. Wrong answers explain the better choice without blocking retry. No waiting for a clock. After all four sections, advice reflects mistakes. The 20–25 minute play-time target is unverified; no filler or artificial delays were added.

## Historical G4 baseline (superseded player interface)

# WATTSHIFT status

Updated: 2026-11-09, Europe/Minsk.

G0/G1: original four Kenney CC0 archives verified; G1 visually approved by user. Same approved models/materials and district composition retained.
G1.1/G2/G3: historical accepted work; baseline commit 7728d9c, local checkpoint tag checkpoint/g3-before-g4. G3 had 66 EditMode and 10 ordinary PlayMode passes.
G4-A: IMPLEMENTED — 0–100 decision rating 40/30/20/10, partial rewards, configurable 25/60/85 star thresholds and independent best stars/score/savings. One star unlocks next mission; no safe useful action or critical safety failure gives no reward.
G4-B: IMPLEMENTED — source-object-anchored yellow/red/green markers, grouped nearby infrastructure, UI obstruction avoidance and click-to-open; scanner optional.
G4-C/D/E: IMPLEMENTED — enlarged/compact cards, forecasts, UI 90/100/115/130%, short persisted/skippable tutorial, immediate first problem, pause/1×/2×/4×/early finish, on-screen zoom, clearer results and next-star explanation.
G4 tests: 93/93 EditMode PASS (all 66 old domain cases retained); 12/12 PlayMode PASS (five camera + seven UX/campaign scenarios). Five viewport shapes and four scales checked. Final scanner/marker agreement, isolated virtual input and disclosure touch target verified; accepted playmode-input-isolated.xml.
G4 macOS: ARM64 Development build SUCCEEDED, zero BuildReport errors/warnings; real native Metal run completed all three missions 94/98/98, 9 stars, save/load PASS, zero observed game console errors/warnings. A separate fresh process restored the completed clinic, budget 0 and 9 stars. Counters now use render-window sampling. Foreground native run passed; a direct High run showed intermittent long frames (27.97FPS), retained as an unresolved performance limitation.
G4 Web/Windows: BLOCKED — Build Support modules absent. Installed Hub catalogue confirms webgl and windows-mono are available but not installed; exact instructions docs/WEB_MODULE_SETUP.md. No Web/Windows runtime claims.
G4 physical-device/human acceptance: NOT TESTED — actual MacBook trackpad feel, Android/iPhone, human audio/readability, browser storage/audio/HTTP behavior, sustained thermal/leak tests. Full portable Definition of Done NOT PASSED. G5 NOT STARTED.

Unity 6000.3.27f1 arm64; URP 17.3.0; InputSystem 1.17.0. Test project /private/tmp/WATTSHIFT_Validation; user's Editor not closed. Scene Assets/_WATTSHIFT/Scenes/CitySlice.unity. Current data Data/CampaignG4; G3 source data retained in Data/Campaign. Power is kW; actual energy is chronologically integrated kWh; projections are labelled forecasts, not past savings. Safety/service percentages are educational assumptions.

Save: Application.persistentDataPath/wattshift-campaign-v4.json, compatible container schema 3 and simulation history 2. Existing v3 file replay-verified with legacy missions then mapped/replayed under G4, never deleted. Best records/awards do not decrease/farm on replay. Tutorial, interface and audio/quality settings persist; all automated runs isolate real player files/settings.

Builds/G4/macOS/WATTSHIFT.app and WATTSHIFT-macOS-arm64.zip are ignored local build artifacts, not notarized distribution. Actual native profiling uses Apple M4/Metal 1600×900 with paused 20:00 city, four lights, markers and open card, ~10 sec/tier. Frame statistics/Unity memory/counters are scoped development-player observations, not phone/browser guarantees; details docs/G4_REPORT.md.

Unity build normalized five files: neutral/deprecated default-volume schema, generated URP prefilter/runtime metadata, explicit default Standalone batching and disabled diagnostics schema. Authored High rendering parameters and scene poses retained. Evidence docs/evidence/g4/unity-build-normalization.json. No art or package upgrade.

Docs: G4_REPORT.md, WEB_MODULE_SETUP.md, evidence/g4/*.xml, native/native-smoke.json, native-restart/native-smoke.json and real ScreenCapture PNGs. Early failures retained separately; raw .logs/builds/caches ignored. Git origin https://github.com/Yato828/ecogame.git, main; final requested commit feat: polish WATTSHIFT gameplay and UX and actual push/hash reported in handoff. No forced history changes.

Next: user gameplay/device review before G5. Web/Windows modules and physical-device acceptance remain open. Web/device gates remain explicit blockers for portability acceptance.

Packaged binary acceptance: docs/evidence/g4/native-final-build/native-smoke.json (Passed,94/98/98,9stars). Same final binary restore: native-restart/native-smoke.json. Final foreground FPS approximately60/59.8/59.8; earlier intermittent High stalls remain an explicit limitation. Source/test parity934files;471unique GUIDs; five expected Unity normalization files only.
