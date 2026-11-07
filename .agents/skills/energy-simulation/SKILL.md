---
name: wattshift-energy-simulation
description: Implement or test energy-saving mechanics and safety constraints for WATTSHIFT missions.
---

# WATTSHIFT / energy-simulation

Keep kW instantaneous load distinct from kWh cumulative energy. Use time-window scheduling, deterministic sample values and ScriptableObjects. Essential systems (clinic, crossing light, refrigerator) must not be switch-off shortcuts. Write edit-mode tests for savings, energy arithmetic, comfort constraints, cost and mission scores. Simulation must not depend on scene.

Return: files changed (if any), validation performed, hard evidence, remaining risk and next small task. Never inflate completion status.
