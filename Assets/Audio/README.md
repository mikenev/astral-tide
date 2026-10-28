# Astral Tide audio (M1)

This is original procedural synthesis (oscillators, synthesized piano, filtered noise). It uses no samples, and nothing comes from Sea of Stars or any other game.
Format: OGG Vorbis, 44.1 kHz. Peaks are measured on the decoded files, and nothing clips. Looping is set on the AudioSource (`loop = true`).

| File | Length | Peak dBFS | Loop | Notes |
|---|---|---|---|---|
| `Music/dusk_shallows_loop.ogg` | 80.00 s (24 bars) | -3.0 | yes, seamless | Stereo. **72 BPM**, 4/4, D Lydian. Soft pads, piano, gentle strings, sparse star twinkles |
| `Music/landmark_arrival_swell.ogg` | 6.00 s | -3.9 | no | Stereo one-shot: a rising shimmer that blooms into a D major chord with piano and glints (includes the reverb tail) |
| `SFX/water_lapping_loop.ogg` | 10.00 s | -14.1 | yes, seamless | Mono bed, deliberately quieter |
| `SFX/wake_hiss_loop.ogg` | 8.00 s | -15.7 | yes, seamless | Mono bed, deliberately quieter; scale it with ship speed |
| `SFX/sail_filling.ogg` | 1.74 s | -6.1 | no | Soft swell of air into the canvas |
| `SFX/star_gust_whoosh.ogg` | 1.29 s | -6.1 | no | Whoosh with a sparkle |
| `SFX/on_course_chime.ogg` | 2.00 s | -6.1 | no | Soft rising fifth for when you're lined up |

`Previews/dusk_shallows_loop.mp3` (repo root, outside Assets) is a review copy only. MP3 adds padding, so it isn't gapless.
