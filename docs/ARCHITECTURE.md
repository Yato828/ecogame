# Architecture contract

## Unity structure (generate within project when starting)
Assets/_WATTSHIFT/{Art/Imported,Art/Materials,Art/Prefabs,Audio,Scenes,Scripts/Core,Scripts/Simulation,Scripts/UI,Scripts/Input,Scripts/Infrastructure,Data,Tests/Editor,Tests/PlayMode}

## Main services
- `CitySimulationService`: deterministic tick-based calculation of kW and kWh, from immutable configuration and a current city state.
- `BuildingDefinition : ScriptableObject`: device types, load curves, hours, comfort impact, upgrades; no scene references.
- `BuildingState`: runtime and serializable applied upgrades and enabled schedules.
- `UpgradeDefinition : ScriptableObject`: capital cost, applicability, effects, safety constraints.
- `MissionDefinition : ScriptableObject`: baseline, target, min safety, min comfort, allowed budget, optional educational statement.
- `MissionEvaluator`: pure rules, emits pass/fail with reasons.
- `SaveService`: versioned JSON in Application.persistentDataPath; no silent save corruption.
- `CityView/BuildingView`: presentation of event-driven state; no kWh calculations in views.
- `SelectionController`: raycast-select buildings with mouse/touch; ignore UI press events.
- `CameraRigController`: configurable pan/zoom/orbit limits, touch gestures.
- `QualityController`: scalable quality tiers and resolution scale; stores settings.
- `Localization`: RU strings in structured tables, later BY option.

## Standards
Composition over inheritance. Events fire on state changes, not continuous Update polling. Use Input System with compatible versions. Profiling labels on key actions. Unit tests for cost, time interval, essential constraints and save migrations. No serializing Unity scene references to saves.

## Content pipeline
Download source pack -> checksum -> license file -> unpack staging -> identify desired FBX/OBJ/glTF (do not assume format) -> import with materials -> prefab -> scene -> screenshot review. Store provenance in `assets_manifest/asset_register.csv` after import.
