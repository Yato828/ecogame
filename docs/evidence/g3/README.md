# G3 evidence index

Accepted final domain run: `editmode-acceptance.xml` (66 pass). Accepted final UI run: `playmode-acceptance.xml` (10 pass, three Explicit cases excluded), including keyboard modality. Earlier `editmode-final.xml` and `playmode-final-fixed.xml` are preceding successful checkpoints.

`live-firstshift-monotonic.xml` covers the first actual six-minute shift before the full campaign integration. `live-remaining.xml` covers the second and third actual six-minute shifts. Short integration tests use deliberate time steps; these live runs do not skip their timed shifts. `restarted-editor-fixed.xml` restores `restart-campaign.json`, the actual campaign save exported by a preceding Editor process. This fixture contains only local game IDs/history and no account information.

PNG files come from Unity ScreenCapture in the real Game View. No image generation or image editing was used. Earlier failed XML results are retained for diagnosis; they are not acceptance results. Raw local `.log` files are ignored by Git because they can contain machine/account metadata.

All Unity runs target `/private/tmp/WATTSHIFT_Validation` to preserve the user's open Editor. Copy `Assets`, `Packages` and `ProjectSettings` from the workspace before reproduction, keeping generated `.meta` files. Run serially; two Editors must not use the same validation project.

```sh
UNITY="/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath /private/tmp/WATTSHIFT_Validation \
  -runTests -testPlatform EditMode -testResults /private/tmp/wattshift-editmode.xml \
  -logFile /private/tmp/wattshift-editmode.log -wattshiftEvidence
"$UNITY" -batchmode -projectPath /private/tmp/WATTSHIFT_Validation \
  -runTests -testPlatform PlayMode -testResults /private/tmp/wattshift-playmode.xml \
  -logFile /private/tmp/wattshift-playmode.log -wattshiftEvidence \
  -wattshiftEvidenceOutput /private/tmp/wattshift-screens
```

For a live run add `-testFilter Wattshift.Tests.GameplayIntegrationTests.RealTimeStreetAndClinicWalkthrough`; it lasts about twelve minutes. Run `RealTimeFirstShiftWalkthrough` separately for the first six-minute route. To verify a different Editor process restores progress, use `FreshEditorRestoresActualCompletedCampaign` after the ordinary Play Mode suite and keep the same evidence output directory containing `restart-campaign.json`.

The `-wattshiftEvidence` switch and each test's unique EditorTestSaveDirectory isolate saves/settings from the user's actual progress. Tests use temporary virtual input devices; they do not prove physical touch/trackpad feel. Audio synthesis/settings are exercised in Unity, but no human listening session is claimed.
