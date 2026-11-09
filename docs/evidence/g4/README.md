# G4 evidence

Accepted: editmode-final.xml (93 pass), playmode-input-isolated.xml (12 pass). All 66 old domain checks retained. Tests cover ratings, previews, actual G3 JSON migration, partial unlock, missions, marker/scanner agreement, cards/scales and camera.

mac-build.json and build-artifact.json identify the final ARM64 Development binary/archive. native/native-smoke.json records the direct run and all missions. native-foreground/native-smoke.json is the same binary explicitly activated through LaunchServices without concurrent packaging. native-restart/native-smoke.json restores nine stars and the clinic result in a separate process. All PNGs are unedited Unity ScreenCapture.

The direct High sample has long-frame variance; it is retained alongside the foreground result. native-counter-first.json predates median-window counters; its single zero High counter is not accepted as performance evidence. Earlier failed XMLs document fixes, not acceptance. Raw local logs and builds/caches are ignored. Source parity and expected Unity serialization normalization have separate JSON audits.

Build: GateFourBuild.BuildMac. Runtime command: build-notes.md; require -wattshiftSmoke and -wattshiftEvidence for isolation; add -wattshiftRestoreSmoke only after a completed run. Virtual-input tests/API-driven native scripts do not imply real touch/trackpad or human UX assessment.
