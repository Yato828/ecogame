# Project status

Updated: 2026-11-07, Europe/Minsk
G0: PASS — four user-supplied original ZIPs CRC/path/license checked; CC0 verified; 213 distinct models / 639 format files inventoried
G1: VISUALLY APPROVED BY USER in continuation request; original models/materials/composition retained
G1.1: PASS automated camera/input checks in actual Unity Play Mode; real-device feel unverified
G2: IMPLEMENTED AND AUTOMATICALLY VERIFIED — first mission, computed kW/kWh, configurable data, budget/upgrades, critical/comfort constraints, window feedback, versioned save/load
G3–G4: NOT STARTED; await user approval before G3
Unity: 6000.3.27f1 arm64 (Unity 6.3 LTS), actually launched; packages resolved and scripts compiled
URP: 17.3.0; Input System: 1.17.0
Project: current workspace root; scene Assets/_WATTSHIFT/Scenes/CitySlice.unity
Assets: 213 canonical FBX imported with original textures/licenses; 18 source meshes verified in scene; 8 distinct buildings, school/clinic roles, roads, park, 4 lamps
Input/UI: smooth wheel zoom, latched RMB/touch gestures, WASD, ground-focus bounds, UI isolation; snapshot-driven mission HUD and configurable upgrade buttons
G2 screenshots: docs/evidence/g2/01-mission-school-before.png, 02-mission-complete.png, 03-mission-portrait.png, 04-mission-4x3.png
Final tests: 22/22 Edit Mode + 7/7 Play Mode PASS; docs/evidence/g2/editmode-final.xml and playmode-acceptance.xml with corresponding logs
Test workspace: /private/tmp/WATTSHIFT_Validation because user's Editor kept original project open; final Assets/Packages/ProjectSettings match source exactly
Performance: previous G1 Editor sample only; G2 60/30 FPS, browser/device performance NOT MEASURED
Browser/native builds: NOT RUN; physical devices: NOT TESTED
Modules: MacStandaloneSupport present; Web and Windows absent
Known limits: physical gesture feel untested; portrait leaves a narrow city viewport; 4:3 small text needs real-screen review; nonblocking licensing token update and Metal memoryless-depth messages in logs
Git: starter kit has no Git repository; no commit created
Configuration: Assets/_WATTSHIFT/Data/Mission1; save: Application.persistentDataPath/wattshift-mission1-v1.json
Golden path: School → Свет по расписанию180 → Компьютеры после занятий260 → Проверить цель;152.52→102.52kWh,650→210credits,comfort100%,critical loads preserved
Preservation:523 approved art/settings files unchanged;5 original transforms and50 prefab pose sets unchanged; source-glass feedback added separately
Reports: docs/G11_G2_REPORT.md and docs/evidence/g2/qa-review.md (earlier G0/G1 reports retained as historical evidence)
Next: user reviews camera feel and first playable mission; stop before G3 until approval.
