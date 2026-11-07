# WATTSHIFT — Game Design v0.1

## Pitch
Premium-feeling stylized isometric 3D puzzle/management game where the player modernizes a compact neighborhood with limited resources while maintaining comfort, safety and essential services. Serious energy-saving message, no tedious lecture screens.

## Player loop
Inspect a building -> detect unnecessary consumption -> choose retrofit or schedule -> see immediate city-wide visual change -> read short factual rationale -> receive score and budget -> unlock the next neighborhood puzzle.

## Game scope (MVP)
One handcrafted 3D neighborhood (10-15 buildings, 1 school, 1 clinic, residential blocks, road segments, park and 8-15 streetlights). Three progressively difficult missions across this same neighborhood. Desktop mouse and touch. Main menu, tutorial, pause, settings, save/load, victory screen, localized Russian UI; optional Belarusian localization later.

## Mission 1 — After hours
School corridor lights and computers left on, offices idle. Discover the sources and schedule/turn off safely; clinic remains powered. Goal: introduce interaction and essential load protection.

## Mission 2 — Streetlighting
Replace incandescent/legacy lamps with LED, install occupancy/time-controlled lighting in suitable areas; maintain safe illumination of crossings and critical zones. Objective: reduce demand while meeting safety and comfort thresholds.

## Mission 3 — Evening peak
Limited budget + evening peak. Combine insulation/scheduling, efficient appliances, LED upgrades, small renewable contribution if available. Choose priorities and balance energy, comfort, safety, cost. Solar generation is context-dependent (time/weather); don't imply it produces at night.

## Core model
Each Building has baseline appliances/devices with kW, usage schedules, essential flag and upgrade variants. kWh = sum(power_kW * usage_hours) over the time window. Separate kW (instantaneous load), kWh (energy) and money (cost). Avoid invented 'physics' claims; reference regional public sources before displaying real-life stats. For MVP use clearly labeled educational example numbers with configurable assumptions.

### Score
Meeting all minimum safety/comfort rules is a **hard condition** for victory. Then rank by energy saved relative to baseline, cost, and optional bonuses. No rewarding dark crosswalks, frozen hospitals or disconnecting refrigerators.

## UX and art direction
Elegant stylized town diorama, cohesive materials from Kenney City Kits. Camera orbit limited, smooth pan/zoom, selection outline, contextual building panel and subtle particles. Lighting changes in building window emissive materials and streetlight cones. No full-screen popups on every action. Design mobile UI with large touch targets.

## Not in MVP
Crowd simulation, free-roam avatar, many city maps, traffic AI, detailed electrical grid solver, network accounts, server, multiplayer, microtransactions, photorealistic PBR textures.
