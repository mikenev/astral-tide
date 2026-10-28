# Astral Tide

A calm Unity WebGL voyage across a starry sea. You sail a small skiff toward a glowing landmark on the horizon, one leg at a time. You can't fail. All names, art and music are original.

**M1** has the sailing feel and one leg, **Dusk Shallows**. It is set in lavender twilight, with the first stars coming out and a few star gusts along the way. The leg ends at the **Lantern Spire**. Arriving plays a swell of light and music and shows the end card.

## Controls

| Action | Keyboard | Gamepad | Touch |
|---|---|---|---|
| Steer | A and D, or Left and Right | Left stick | Drag left and right |
| Sail up or down (speed) | W and S, or Up and Down | Right trigger and left trigger | Drag up and down |
| Look around | Hold the right mouse button and move the mouse | Right stick | Two-finger drag |
| Pause | Esc (R restarts the leg while paused) | Start | Pause button (top left) |
| Set out / sail again | Enter, Space or click | A | Tap |

## How a leg plays

- **Star compass:** a thin strip at the top of the screen. The bow is in the middle, and the gold marker shows the goal. When you are within 5° of the goal, the marker pulses and a soft chime plays. The chime plays again only after you have been off by 12° or more for half a second.
- **Gentle pull:** if your heading drifts more than about 40° off the goal, or you wander far from the course line, the rudder eases you back. You can still steer against it.
- **Fog at the edges:** about 150 m off the course line (or behind the start), the water fogs over. Past about 190 m, the fog takes the helm and turns you back toward the light until you're on course again.
- **Star gusts:** six sparkling gusts along the way. Sailing through one gives a short burst of speed.
- **Arrival:** within 75 m of the Spire, the ship eases to a stop. The lantern flares, the arrival swell plays (the leg music ducks under it), and the end card shows your time and the gusts you caught.

## Art and audio

- **Art** (Art Dev, `Assets/Art`):
  - **Skiff and Lantern Spire:** URP/Lit with emission. A point light sits in each lantern.
  - **Sky:** the Dusk Shallows panorama is the `Skybox/Panoramic` skybox. An additive star veil sits over it, using `AT_Starfield` plus procedural twinkles that come out as the leg goes on.
  - **Wake:** `AT_Wake` on a TrailRenderer.
  - **Star gusts:** `AT_Star` sparkles.
  - **Compass:** the 360° compass strip and the goal marker.
  - Linear colour space, HDR, and Bloom in a global volume (threshold 0.9).
- **Sea:** the sea shader (`AstralTide/Sea`) reflects the panorama, the star veil and the Spire's glow along a rippled reflection ray. Mirrored copies of the skiff and the Spire sit under the half-transparent surface.
- **Fallbacks:** everything is found by file name in `BuildWeb`. If a file is missing, the procedural stand-in is used: vertex-coloured meshes, a shader sky, a drawn compass and glow sprites.
- **Audio** (Audio Dev, `Assets/Audio`):
  - The `dusk_shallows_loop` music.
  - The `water_lapping_loop` and `wake_hiss_loop` beds. The hiss follows your speed.
  - One-shots: `sail_filling`, `star_gust_whoosh`, `on_course_chime` and `landmark_arrival_swell`.
  - Import settings come from `Assets/Editor/AudioImport.cs`: music streams, SFX decompress on load, and normalize is off.
  - Audio starts on the first input, because of WebGL autoplay rules.

## Building

The setup is the same as Salt and Glory: Unity 6000.3.26f1, URP and WebGL. `BuildWeb.Build` builds the scene from code, then builds:

```
Unity -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod BuildWeb.Build -logFile build.log
```

- Release goes to `docs/`. It uses gzip with the decompression fallback and has threads off. It also gets a `.nojekyll`, the title and favicon patch (`Web/favicon.png`), and the touch and scroll guards.
- `AT_QA=1` makes a release-like build in `Builds/WebGLQA` that includes the `window.atUnity` QA hook (`Voyage.QAStatus` / `QAPlace`).
- `AT_DEV=1` makes a development build. Its dev keys are F2 (near the Spire), F3 (next gust) and F4 (drift to the edge).
- The release build contains neither the QA hook nor the dev keys.
