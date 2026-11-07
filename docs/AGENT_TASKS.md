# Delegation and token economy

## Root / integrator
Owns scope, contracts, acceptance, scene/prefab merges, integration, Unity builds and final manual visual judgment. Never starts 5 agents editing same project at once. Delegate only independent, bounded jobs.

## Research/art pipeline (G0, then G1)
Read `assets_manifest/sources.json`. Download actual packs where permitted; verify CC0 licenses and model quality, inspect previews and file formats. Produce `asset_register.csv`, import checklist and selected model list. Own `Assets/_WATTSHIFT/Art/Imported` only after approval. No replacing missing models with cubes.

## Gameplay/simulation (G2)
Own `Scripts/Simulation`, `Data`, simulation tests. Implement kW/kWh model, safety/comfort, missions, economy, deterministic results. Do not modify scenes or other agent files.

## UI/interaction (G1/3)
Own `Scripts/UI`, UXML/Canvas prefabs assigned by root. Implement desktop/touch controls and RU localized strings, selection panels, upgrade UX. No edits to shared scenes without integration lock.

## Build/performance (G4)
Own `Scripts/Infrastructure`, testing scripts/report; profile and recommend settings. Avoid modifying scene/project settings concurrently with integrator.

## QA / reviewer (all gates)
Read-only review of logs, tests, screenshots and game flows; list concrete defects with severity and reproduction steps. No modifications unless root reassigns ownership.

## Efficient agent usage
1. Root reads 3 concise core docs, not every skill.
2. Each subagent receives **one** objective, allowed files, output path and acceptance criteria, plus a specific skill file.
3. Run max 2–3 independent agents concurrently (art research + simulation spec + QA check); do NOT parallelize dependent edits.
4. Ask agents to report short diffs/tests/blockers (<=250 words) and avoid repeating long context.
5. Prefer one high-quality implementation pass and one targeted review; don't run endless 'improvement' loops.
6. Keep `docs/STATUS.md` updated once per gate; use new sessions with the status summary instead of replaying all previous discussions.
7. Model assignment: high reasoning for architecture/integration and hard bugs; lower effort for asset inventory, formatting and straightforward tests.
8. If subagent capability unavailable in Codex client, execute tasks sequentially using separate chats; no fake claims of delegated work.
