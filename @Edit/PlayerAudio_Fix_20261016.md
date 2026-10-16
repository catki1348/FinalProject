# Player jump and defense audio

<!-- AI work record: Codex (GPT-6), completed 2026-10-16. Shortened the existing jump recording and changed defense feedback to play only on successful blocks. -->

- `Assets/@Scripts/Player/PlayerDefenseFeedback.cs`: subscribe only to `PlayerDefense.OnBlocked`. Guard input alone is silent. A normal block uses `SFX_Player_ShieldBlock.wav` (0.46 seconds); a perfect block uses `SFX_Player_PerfectBlock.wav` (0.72 seconds). The existing perfect-block visual effect remains connected.
- `Assets/@ExternalAssets/Sound/SFX_Player_Jump_Short.wav`: PCM derivative of `SFX_Player_Jump_1.mp3`, source interval 0.10–0.45 seconds, 5 ms fade-in and 15 ms fade-out. The original is 3.6 seconds and includes additional late sound bursts; the new one-shot lasts approximately 0.35 seconds.
- `Assets/@ExternalAssets/@Prefab/Player_Warrior.prefab`, `Player_Base.prefab`, and `P_Player_HeroKnightTest.prefab`: use the short jump clip. Stage1–5 have no overrides of the three relevant audio properties.
- Audio and prefab assets are under the repository's existing ignored `@ExternalAssets` folder; preserve them alongside code when transferring the fix.

## Validation

`PlayerAudio_Validation_20261016.txt` contains 12 passing runtime checks in the disposable `Assets/Scenes/Test001.unity` scene. Checks cover silent guard input, normal/perfect block audio selection through `PlayerHealth.TakeDamage`, prevented damage, empty-slot isolation, zero damage, event unsubscribe/resubscribe, short clip duration, readable PCM and zero-valued fade endpoints. The test scene was deleted after testing and the previous Play Mode start scene restored. The editor returned to Stage2 with no console errors. Actual listening and a full gameplay traversal were not performed.
