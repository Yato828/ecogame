# WATTSHIFT status

Updated: 2026-11-08, Europe/Minsk.

- G0: PASS — four original user-supplied Kenney CC0 archives checked; 213 distinct models/639 format files inventoried. Provenance: assets_manifest/.
- G1: VISUALLY APPROVED BY USER. Genuine source models/materials/composition retained.
- G1.1/G2: accepted previous implementation, 22/22 EditMode and 7/7 PlayMode; historical docs/G11_G2_REPORT.md.
- G3-A: PASS — chronological energy, 360-second 17:00–23:00 shift, events, scan→inspect→choose→observe→results. Actual first-shift live walkthrough 362.5 seconds; 2 stars using sensors+computer sleep.
- G3-B: PASS — three distinct scenarios, safe crossing/path lighting, clinic load priorities, sequential unlock, stars/rewards, repeated-run protection.
- G3-C: PASS — RU menus/HUD/context/results, persisted audio/quality, original cached audio, upgrade feedback, trackpad drag, responsive/error UI. Keyboard/modal focus corrected and included in acceptance coverage.
- G3-D: PASS in Editor — 66/66 EditMode; 10/10 ordinary PlayMode including keyboard modality; 1/1 fresh-Editor saved-campaign restore. Actual-time street and clinic shifts both passed at 361.7 seconds each, 3 stars each.
- G4: NOT STARTED. Stop for user gameplay/visual approval; no production-ready claim.

Unity 6000.3.27f1 arm64; URP 17.3.0; InputSystem 1.17.0. Editor actually executed. Test workspace `/private/tmp/WATTSHIFT_Validation` preserves user's open Editor. No package or ProjectSettings changes during G3.

Current scene: `Assets/_WATTSHIFT/Scenes/CitySlice.unity`. Same 8 genuine source buildings and 4 source lamp models; lamp groups add selectable infrastructure, no new buildings. Data: `Assets/_WATTSHIFT/Data/Campaign`. Save: `Application.persistentDataPath/wattshift-campaign-v3.json` (campaign schema 3, simulation history 2, atomic tmp/backup). Old G2 save retained separately, no automatic migration. Tests isolate saves/settings.

Campaign scenarios have independent starting states/budgets. Best results, stars, unlocks and score points persist; exact upgrades are not carried between scenarios. All power/cost/service/target/timing values are authored data. kW is instantaneous, kWh is actual accumulated energy; late decisions do not rewrite past use. Medical loads and minimum lighting/service are protected.

Source audit: 556 tracked art/Packages/ProjectSettings files unchanged; original ordinary transforms and prefab pose overrides unchanged; 408 unique GUIDs, no missing .meta or duplicates. Publication scan 676 text files, no secret-pattern suspects. Kenney bundled CC0 licenses retained; audio is original synthesis, not falsely attributed to Kenney.

Performance evidence: Apple M4 Editor sample at G3-A, 47105 frames, median 7.0 ms/P95 12.7 ms. No final native/browser/device FPS or memory claim. Targets 60 desktop/30 mobile FPS remain unverified. Browser/native builds NOT RUN; physical devices and real trackpad NOT TESTED; human audio/UX assessment pending. MacStandaloneSupport installed, Web/Windows absent. docs/WEB_MODULE_SETUP.md gives module/profile handoff. Service/license-token and Metal messages remain in local Editor logs; accepted tests distinguish them from game exceptions.

Reports/evidence: docs/G3_REPORT.md, docs/evidence/g3/. Final domain: editmode-acceptance.xml; final UI: playmode-acceptance.xml. Live evidence: live-firstshift-monotonic.xml and live-remaining.xml; process restart: restarted-editor-fixed.xml. Tested Assets/Packages/ProjectSettings match workspace byte-for-byte (810 files), no extra files; artifact-validation.json. Earlier failed XMLs retained for diagnosis; raw .log files ignored by Git.

Git: main; origin https://github.com/Yato828/ecogame.git; initial 5f249d0a8354d6afa7a4eb0fbc882827a816878b pushed. Remote fetched and remains at that commit. Final G3 commit/push requested after successful validation; hash and actual push result are given in the handoff. No force push or history deletion.

Next: user review of G3 gameplay/visual quality. Stop before G4.
