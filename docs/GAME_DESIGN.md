> Gameplay Rescue supersedes the legacy timed player loop below: one map, 17 situations in three chapters and a three-question final. Data: Assets/_WATTSHIFT/Resources/CityTasks.json. Three interaction variants share one card; seven original upgrades still apply actual simulation effects. Default UI shows actions/progress, without energy units, budget or clocks. Wrong choices explain and allow retry; completed IDs, mistakes, source discoveries and tutorial state persist separately from legacy campaign saves. Chapter stars are derived from completed tasks and the number of tasks with mistakes (0 → 3; 1–2 → 2; 3+ → 1). No claim of measured play duration.

# WATTSHIFT — Game Design v 0.1

## Pitch
Premium-feeling stylized isometric 3D puzzle/management game where the player modernizes a compact neighborhood with limited resources while maintaining comfort, safety and essential services. Serious energy-saving message, no tedious lecture screens.

## Player loop
Inspect a building -> detect unnecessary consumption -> choose retrofit or schedule -> see immediate city-wide visual change -> read short factual rationale -> receive score and budget -> unlock the next neighborhood puzzle.

## Game scope (MVP)
One handcrafted 3D neighborhood (10-15 buildings, 1 school, 1 clinic, residential blocks, road segments, park and 8-15 streetlights). Three progressively difficult missions across this same neighborhood. Desktop mouse and touch. Main menu, tutorial, pause, settings, save/load, victory screen, localized Russian UI; optional Belarusian localization later.

## G3 campaign — historical baseline

Each shift runs 17:00–23:00 in 360 active real seconds. Decisions may be paused; focus loss pauses play. Events use simulation hours. Investigation becomes meaningful after 18:00. Actual energy is integrated chronologically: an upgrade cannot undo earlier consumption. Victory requires investigation, the configured measures, at least 30 minutes of simulated observation after the final required intervention, and the end of the shift.

### Mission 1 — «Вечерний пик»

Investigate the school after classes. Compare corridor schedules, presence sensors, efficient lamps and computer sleep with a 650 unit budget. Lighting alternatives are mutually exclusive. School security and clinic loads remain protected. Target 24%; star thresholds 24/26/30%. Scheduling empty corridors plus computer sleep at 18:00 costs 440 and saves 50 kWh against 161.52 kWh baseline. Sensors plus computer sleep provide another valid, lower-star route.

### Mission 2 — «Тёмные улицы»

Investigate two groups represented by the four existing source streetlight models. Crossing illumination has higher priority than paths. LED changes electrical demand while preserving service; after 20:00 only paths permit safe adaptive dimming. Complete shutdown is rejected. Budget 380; target 4%; star thresholds 4/5/5.5%. Two LED groups cost 380 and save 6.3 kWh against 111.52 kWh baseline if installed at 18:00. Crossing LED plus later path regulation costs 330, leaves 50 and produces a lower score.

### Mission 3 — «Энергия жизни»

Investigate the clinic and distinguish critical medical systems, important working systems and noncritical auxiliary loads. Choose schedules/sensors, administrative sleep and efficient working-area lighting with 550 units. Critical medical power cannot be interrupted; working light and ventilation have service floors. Target 14%; star thresholds 14/17/20%. Auxiliary schedules 150 + administrative sleep 180 + working LED 220 at 18:00 save 30 kWh against 141.22 kWh baseline. Other safe routes earn fewer stars.

Missions unlock sequentially. Best verified runs persist; campaign points derive from best stars, and replay pays only the improvement. Each mission has its own educational starting scenario and local upgrade budget. Exact installed upgrades are not carried across different scenarios; this is a three-scenario campaign rather than a permanent free-city economy. Campaign points are a score, not extra money to spend in missions.

## Core model
Each Building has baseline appliances/devices with kW, usage schedules, essential flag and upgrade variants. kWh = sum(power_kW * usage_hours) over the time window. Separate kW (instantaneous load), kWh (energy) and money (cost). Avoid invented 'physics' claims; reference regional public sources before displaying real-life stats. For MVP use clearly labeled educational example numbers with configurable assumptions.

### Score
Meeting all minimum safety/comfort rules is a **hard condition** for victory. Then rank by energy saved relative to baseline, cost, and optional bonuses. No rewarding dark crosswalks, frozen hospitals or disconnecting refrigerators.

## UX and art direction
Elegant stylized town diorama, cohesive materials from Kenney City Kits. Camera orbit limited, smooth pan/zoom, selection outline, contextual building panel and subtle particles. Lighting changes in building window emissive materials and streetlight cones. No full-screen popups on every action. Design mobile UI with large touch targets.

## Not in MVP
Crowd simulation, free-roam avatar, many city maps, traffic AI, detailed electrical grid solver, network accounts, server, multiplayer, microtransactions, photorealistic PBR textures.

## Educational assumptions and limits

All powers, budgets, service floors, targets and timing are authored ScriptableObject data in `Data/Campaign`. Service percentages are dimensionless teaching constraints, not measured lux, certified medical requirements or real retrofit advice. Presence sensing uses a configurable occupancy multiplier, not simulated pedestrians. The road groups account for two fixtures each; values demonstrate tradeoffs rather than specifying real fixtures. No solar generation is claimed at night. No empirical real-life energy statistic is displayed.

The original eight buildings, four streetlight meshes, Kenney palettes, source URP assets and composition remain. Scanner hulls reuse genuine source meshes; window feedback reuses source glass faces. No generated images or primitive substitute art.

## G4 — current player experience and rating

The same district now starts with actionable problems at 17:00. The school and clinic schedules can address empty rooms from 17:00; streetlights still illuminate at 18:00, and safe adaptive path control is available after 20:00. The scanner is optional. Visible world-anchored markers open a short problem card directly; the enlarged card uses the same data/actions and offers a remaining-shift forecast from the actual simulation. Interface scale choices are 90/100/115/130%; selection/zoom remain blocked by UI.

Clock speeds 1/2/4 multiply simulation advancement, not event thresholds or device power. Pause freezes time. «Завершить» integrates all remaining time with the installed state, fires intervening events and evaluates the outcome; unresolved problems are not automatically fixed. No idle waiting is required.

Each configured device problem earns a share of 40 points when its final-state projected device consumption falls by at least 25% and safety/comfort remain valid. This indicates an installed remedy; actual chronological kWh are scored separately. Energy points: 30×clamp(observed saving percentage/reference saving percentage,0,1). References are 38/6/27% for the three scenarios. Safety and comfort contribute 20 only when both pass. Budget contributes 10×min(1,rational spend/actual spend), with a full 10 when actual spend is zero or below the rational reference; references 440/380/550. Total is rounded to 0–100. Configured weights are normalized if edited.

Stars require safe useful decisions and an ended shift. One star starts at 25 points with positive actual saving of at least 0.25%. Two stars additionally require 60 points, more than half the problems solved and at least half the reference energy saving. Three require 85 points and all mandatory problems resolved. Thus 1/2 school problems can earn 62 points but one star: the second-star conditions are not yet met. The result explains this and offers a short expandable score breakdown. One star unlocks the next mission; independent best stars, score and saving never decrease on replay.

G4 profiles are under `Data/CampaignG4` with mission IDs ending -v4. G3 source profiles are retained. Existing valid G3 progress is first replay-verified under its original rules, then compatible decisions are replayed under G4 rules; original JSON remains untouched. Empty optional JSON replay fields from older Unity serialization are treated as absent, while populated extra replays are fully verified.
