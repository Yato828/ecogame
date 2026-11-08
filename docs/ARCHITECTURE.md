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

## G3 campaign adapters

`CitySimulationService` retains the original deterministic model and adds chronological advancement and purchase history. A snapshot carries both instantaneous power and observed energy; presentation never integrates its own kWh. `GameSession` owns the active monotonic clock, pause, investigation and event orchestration; it publishes scene/UI updates at 5 Hz. `CampaignProgress` reconstructs best runs through the same simulation before awarding/unlocking; `CampaignSaveService` wraps active city state plus verified records in schema 3 and writes atomically with backup. Saves contain IDs/data, never scene objects.

`CityAtmosphere`, `StreetlightView`, source-derived window meshes and scanner hulls present the snapshot. `CampaignAudio` caches original short signals; `QualityController` changes a runtime copy of the approved URP asset and restores the source references when destroyed. Their PlayerPrefs are isolated during Editor tests. Settings disable background UI and world input and leave game time paused on close. Independent mission scenario configurations live under `Assets/_WATTSHIFT/Data/Campaign`; the legacy G2 assets/save remain separate.
