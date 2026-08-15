# Ink Wuxia Hero SFX

These clips are designed for one-shot playback from animation events or combat-state code.

- `Hero_NormalAttack_SFX.mp3`
- `Hero_DashAttack_SFX.mp3`
- `Hero_Roll_SFX.mp3`
- `Hero_Parry_SFX.mp3`
- `Hero_Kill_SFX.mp3`

Recommended Unity settings: preload enabled, spatial blend chosen by camera style, and no looping.

## Runtime edits

The original three-second MP3 files are preserved in this folder. Each source
contained multiple separated sound events, so the first complete event was
isolated for gameplay and exported as mono 44.1 kHz / 16-bit PCM WAV under
`Trimmed/`:

- `Hero_NormalAttack_SFX.wav` — 0.400 s
- `Hero_DashAttack_SFX.wav` — 0.230 s
- `Hero_Roll_SFX.wav` — 0.320 s
- `Hero_Parry_SFX.wav` — 0.650 s
- `Hero_Kill_SFX.wav` — 0.370 s

The runtime clips use Decompress On Load, PCM, preload enabled, 2D playback,
and short fades to prevent clicks. `Assets/Scenes/Extra.unity` binds them to
the Player's normal attack, dash attack, roll, perfect parry, and kill events.
