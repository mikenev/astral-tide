# Astral Tide audio (M1 and M2)

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
| `Music/aurora_reach_loop.ogg` | 72.73 s (20 bars) | -3.0 | yes, seamless | Stereo. **66 BPM**, E Lydian. Airy and shimmering: high pads, tremolo strings, frequent star glints, high piano |
| `Music/ember_drift_loop.ogg` | 72.00 s (24 bars) | -3.0 | yes, seamless | Stereo. **80 BPM**, B-flat Mixolydian. Warm, with a slight pulse from repeated piano eighths, a soft heartbeat thump and a gentle beat-locked swell |
| `Music/frost_mirror_loop.ogg` | 72.00 s (18 bars) | -3.1 | yes, seamless | Stereo. **60 BPM**, B Aeolian. Sparse and glassy: open fifths, glass bells, single piano notes, late strings |
| `Music/cosmic_sea_loop.ogg` | 75.79 s (24 bars) | -3.3 | yes, seamless | Stereo. **76 BPM**, C major. Majestic finale: block piano chords, a broad string theme doubling the piano, soft timpani |
| `SFX/current_rush_loop.ogg` | 9.00 s | -13.9 | yes, seamless | Mono bed, deliberately quieter. Water rush with swirls |
| `SFX/meteor_bump.ogg` | 0.92 s | -6.1 | no | Soft rocky thud with sparkle |
| `SFX/ice_creak.ogg` | 1.10 s | -5.9 | no | Stick-slip creak with small cracks |
| `SFX/star_fragment_chime.ogg` | 1.40 s | -5.9 | no | Three-note glint |
| `SFX/constellation_complete.ogg` | 4.00 s | -6.1 | no | Rising glint arpeggio into a C major bloom |
| `SFX/boost_whoosh.ogg` | 1.00 s | -5.9 | no | Rising whoosh with sparkle |
| `SFX/glacier_bell.ogg` | 5.00 s | -6.1 | no | Deep G bell with icy overtones (for the Glacier Bell landmark) |

`Previews/*.mp3` (repo root, outside Assets: one per music loop) are review copies only. MP3 adds padding, so they aren't gapless.
