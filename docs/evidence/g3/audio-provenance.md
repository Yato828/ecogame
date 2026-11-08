# G3 UI audio provenance

The seven short UI sounds are original WATTSHIFT signal synthesis, authored in
`Scripts/Presentation/CampaignAudio.cs`. They contain only sine fundamentals and
a quiet second harmonic, with attack/release envelopes and short note sequences.
There are no sampled recordings, downloaded music, voice tracks, third-party
sound libraries or paid runtime dependencies.

Kenney Interface Sounds could not be downloaded through the earlier automatic
HTTP-403 route. These cues are therefore not represented as Kenney recordings.
The exact source recipe is retained in the project and the clips are generated
once into a cache: mono PCM, 22,050 Hz, 0.07–0.38 seconds. Selection, scanner,
inspection, upgrade, event, success and rejected-action cues have separate recipes.

Playback uses at most four owned AudioSource channels and event debounce; it is
not a per-frame synthesis effect. All cached clips and channels are released
when the component is destroyed. Volume and mute settings use project-specific
PlayerPrefs keys; Editor evidence/test-directory settings have a separate scope.

No listening session or real-device audio-quality assessment is claimed here.
Root integration owns Unity playback and automated checks.
