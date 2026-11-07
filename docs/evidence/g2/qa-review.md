# G2 QA review — 2026-11-07

**PASS: desktop G2 vertical demonstration. Await user review before G3.** G3/G4, browser portability and physical-device usability are not accepted by this review.

## Evidence reviewed

- `editmode-final.xml`: **22 passed, 0 failed**. Independent arithmetic covers kW versus integrated kWh, fractional/midnight/full-day schedules and exact boundaries. Other cases cover essential/comfort protection, budget and duplicate atomicity, mission success/failure, immutable configuration, validated transactional restoration and file save/read/overwrite/corruption handling. Includes the essential `24→0` empty-schedule regression.
- `playmode-acceptance.xml`: **7 passed, 0 failed**. Covers mouse selection/UI isolation, smooth bounded zoom, RMB/WASD movement, touch pan/pinch transitions, the first mission through actual UI, rejected actions, scene-reload restoration and layout checks at landscape/4:3/portrait sizes.
- Actual Unity screenshots `01-mission-school-before.png`, `02-mission-complete.png`, `03-mission-portrait.png`, `04-mission-4x3.png` visually inspected. Final landscape/4:3 actions use readable 2×2 button layouts. Brand is visible. Genuine models retain their materials; the corrected overlay lights glass without repainting slate facades or roof rims. School lighting responds to scheduling; clinic lighting remains.

Displayed baseline/result values agree with the configured mission: **152.5→102.5 kWh**, **26→16 kW**, **50 kWh / 32.8% saved**, **650→210 educational budget units**, **100% comfort**. UI distinguishes instantaneous power at 20:00 from scheduled energy over 17:00–23:00 and labels educational assumptions.

Source review found no remaining blocking arithmetic, budget, essential-load or restore-atomicity defect. Earlier duplicate-UIDocument, broad window-mask and empty essential-schedule defects were corrected. Tests were executed by the integrator; this QA agent reviewed results and did not launch another Editor.

## Remaining verification

Touch tests inject events in Editor; they do not validate real fingers, browser gesture handling, safe-area behavior or device feel. Portrait HUD leaves limited visible city area, and 4:3 explanatory text is small; manually assess these on actual target devices. Browser/native builds, memory, draw calls and sustained desktop/mobile FPS targets are **NOT TESTED** here. No claim of production readiness or 60/30 FPS acceptance is made.
