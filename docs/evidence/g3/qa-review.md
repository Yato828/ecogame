# G3 targeted QA

## G3-A domain review

Added `Assets/_WATTSHIFT/Tests/Editor/G3TimelineTests.cs` (20 NUnit cases). Existing G2 tests remain unchanged.

- Source-reviewed: copied domain configuration, timeline integration, transactional history replay, mission evaluation, verified campaign rewards.
- Test coverage: independent 8 kW baseline arithmetic; purchase at 18:00 yields 33 kWh and at 22:00 yields 45 kWh against 48 kWh baseline; no retroactive saving; actual observation/end/count/building requirements; deterministic timestamp restore; malformed history rejection; budget, repeat and exclusive purchase failures; critical-load protection; LED illumination/service floor; verified campaign replay rewards and forged completion rejection.
- NOT TESTED by QA child: Unity compilation, NUnit execution, Play Mode, screenshots, browser builds, touch devices and performance. Lead integrator owns Editor execution and records its results separately.

Resolved during integration: `CampaignProgress.Record` now enforces `IsUnlocked(index)` itself, in addition to replaying and validating a completed run. The final persistence tests cover direct locked-mission calls.

No claim of gate completion follows from source review alone.

## G3-A screenshot review

QA inspected actual `a-01-main-menu.png` through `a-05-portrait-inspection.png` using the image viewer.

- PASS (captured frames only): menu/results text and active controls are readable; no blocking panel overlap; 18:00 pause state and resume controls are visible; night city retains readable geometry and street lighting; portrait inspection action is unobscured and large.
- Nonblocking visual issue: disabled upgrade buttons in `a-03-observed-night.png` have very low text contrast.
- NOT PROVEN by these screenshots: scanner model highlighting is not clearly distinguishable, real six-minute duration, camera feel, touch usability, and frame rate.
- Lead integrator reports domain EditMode 42/42 passing (`editmode-a.xml`). QA child has not independently executed Unity. Final first-shift Play Mode rerun and real wall-clock run remain owned by the integrator.

## Final integration review (lead + independent QA)

The integrator ran 66/66 domain cases and 10/10 ordinary PlayMode cases successfully; fresh-Editor loading passed separately. The final 24 persistence cases include transactional malformed-record rejection, repeat rewards, score improvements, defensive copies, atomic save backup and failed writes, malformed JSON/version and oversized-file failures. No production logic defect was found in the bounded final persistence/audio/quality review.

A material keyboard-modal issue was found in source review: settings initially left underlying controls focusable. It was corrected by disabling the underlying HUD/menu/context roots and moving focus into settings, restoring background controls on close. The added integration assertions check parent disabled state, camera isolation and focused element ancestry. Use the final acceptance XML selected in STATUS.md for execution evidence of that correction.

Visual issues recorded above are resolved in the current source-mesh scanner hulls and explicit disabled-button colors. The integrator inspected actual night streets/clinic, settings, portrait error and restored-campaign PNGs. Low-tier screenshot was recaptured after shader warmup. Physical touch/trackpad feel, human audio assessment, final build memory and browser/native performance remain unverified. The live first shift already passed on actual time; remaining shift evidence is recorded in its own XML and clock text.
