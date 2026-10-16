# Player game-style sword slash

## Current revision: recognizable blade slash

<!-- AI work record: Codex (GPT-6), completed 2026-10-16. User found the purely synthesized version lacked sword identity. Replaced it with a short processed sword recording and non-tonal air layer. -->

- Current asset: `Assets/@ExternalAssets/Sound/SFX_Player_SwordSlash_Blade.wav`, 0.30 seconds, 48 kHz mono PCM16.
- Uses `SFX_Player_SwordSwing_2.mp3` at 0.28–0.58 seconds, retaining the blade transient. Filters remove low rumble, limit excessive high frequencies and emphasize the early blade edge. A quiet shaped noise layer adds air-cut movement. The synthetic falling sine accent has been removed entirely.
- Four-millisecond attack, 80 ms end fade, soft saturation and peak normalization to 0.74 keep the slash compact without a long metallic tail.
- All three player prefab `attackSfx` references now use this revision. Checked saved references, duration below cooldown, non-silent PCM, fade endpoints and no clipping. Unity console has no errors. Subjective listening remains unverified by the agent; a playable WAV is presented for user review.
- The following entry describes the earlier synthesized version, which is no longer assigned.

<!-- AI work record: Codex (GPT-6), completed 2026-10-16. Generated an original procedural game slash and assigned it to all three player prefab attackSfx properties. -->

- New asset: `Assets/@ExternalAssets/Sound/SFX_Player_SwordSlash_Game.wav` (0.24 seconds, 48 kHz, mono PCM16).
- Replaces `SFX_Player_SwordSwing_2.mp3` (1.8 seconds) in `Player_Warrior.prefab`, `Player_Base.prefab`, and `P_Player_HeroKnightTest.prefab` under `Assets/@ExternalAssets/@Prefab`.
- Sound design: deterministic white noise (seed 16102026), a broad descending band-pass from 4200 to 600 Hz (Q 0.75), soft low-pass noise body, and a short falling sine accent from 1580 toward 180 Hz. Fast 4 ms attack, exponential decay, 35 ms end fade. No recorded metal or ringing layer. Peak normalized to 0.72.
- Verification: all three saved prefabs reference the new clip; duration is shorter than their 0.4/0.52-second attack cooldowns. PCM is non-silent, has no clipped samples, and fades to near-zero at both ends. Stage1–5 have no `attackSfx` overrides. Unity console has no errors. No new runtime script was added. Listening and full gameplay validation were not performed.
- The new WAV, its `.meta`, and edited prefabs are inside the existing Git-ignored `@ExternalAssets` folder and must be included separately when transferring the project.
