---
name: wattshift-asset-pipeline
description: Curate, license-check and import real 3D assets for WATTSHIFT when sourcing or importing models and textures.
---

# WATTSHIFT / asset-pipeline

Use official URLs from `assets_manifest/sources.json`. Verify original downloads, CC0 file/license, contents, polygon density, pivot, orientation, formats, materials. Record source/date/hash/path. Select a consistent subset. Import actual meshes and verify pink-material errors/UVs; never replace missing visual content with primitive meshes or background screenshots. Report the screenshot evidence and asset register.

Return: files changed (if any), validation performed, hard evidence, remaining risk and next small task. Never inflate completion status.
