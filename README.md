# Astral Tide

A calm Unity WebGL voyage across a starry sea. You sail a small skiff toward a glowing landmark on the horizon, one leg at a time. You can't fail. All names, art and music are original.

**M2** has the whole voyage: five legs, each ending at a landmark, all on one sea and sky shader recoloured per leg.

| Leg | Mood | Mechanic | Landmark |
|---|---|---|---|
| 1. Dusk Shallows | Lavender twilight, a gentle swell | Star gusts | Lantern Spire |
| 2. Aurora Reach | Teal night under aurora curtains | Glowing currents that carry you along | Aurora Gate (sail into the arch) |
| 3. Ember Drift | Deep indigo, live shooting stars | Drifting meteors: a bump only slows you and rocks the ship | Fallen Star crater isle |
| 4. Frost Mirror | Silver and ice blue, perfectly still water | Ice floes, and long chains of star gusts that stack the boost | Glacier Bell (it rings) |
| 5. The Cosmic Sea | Purple nebula, big rolling waves | Star buoys to sail through, a boost run, twin cyan wake lines | White Star |

- **Star fragments:** five per leg. Each one lights a star of that leg's constellation, low in the sky ahead; find all five and the lines join up.
- **Progress** saves in PlayerPrefs (`at.unlocked`, `at.frag.<leg>`, `at.best.<leg>`, `at.complete`). Arriving at a landmark opens the next leg. The title has **Continue** and a **leg select**. The final end card comes after leg 5.
- **Waves:** a four-component swell (`AstralWaves.hlsl`) displaces a dense ring mesh around the camera. `AstralTide.Waves` sums the same waves on the CPU, and the ship samples its bow, stern and both sides for heave, pitch and roll. Its reflection mirrors about the local water height. Dusk Shallows is calm, Frost Mirror still and The Cosmic Sea rolls.
- **Hull fix:** a depth-only lid (`AstralTide/WaterMask`) is built from the hull's waterline outline. It is drawn between the ship and the sea, so water never shows inside the hull. The chase camera is about 10% further out than in M1.
- You can't fail: the fog and the gentle pull keep you on course, and bumps only slow you down.

## Controls

| Action | Keyboard | Gamepad | Touch |
|---|---|---|---|
| Steer | A and D, or Left and Right | Left stick | Drag left and right |
| Sail up or down (speed) | W and S, or Up and Down | Right trigger and left trigger | Drag up and down |
| Look around | Hold the right mouse button and move the mouse | Right stick | Two-finger drag |
| Pause | Esc | Start | Pause button (top left) |
| Pause menu | Enter resume · R restart the leg · L leg select | A resume · Y restart · B leg select | Tap a button |
| Title: choose a leg | Up and Down, or W and S | D-pad or left stick | Tap a leg |
| Set out / sail on | Enter, Space or click | A | Tap |

## How a leg plays

- **Star compass:** a thin strip at the top of the screen. The bow is in the middle, and the gold marker shows the goal. When you are within 5° of the goal, the marker pulses and a soft chime plays. The chime plays again only after you have been off by 12° or more for half a second.
- **Gentle pull:** if your heading drifts more than about 40° off the goal, or you wander far from the course line, the rudder eases you back. You can still steer against it.
- **Fog at the edges:** about 150 m off the course line (or behind the start), the water fogs over. Past about 190 m, the fog takes the helm and turns you back toward the light until you're on course again.
- **Star gusts:** sparkling gusts along the way. Sailing through one gives a short burst of speed. On Frost Mirror, catching them one after another chains a longer boost.
- **Arrival:** near the landmark, the ship eases to a stop. The landmark flares, the arrival swell plays (the leg music ducks under it), and a card shows your time and fragments. It also names the leg that has just opened. Each landmark has its own arrival beat in `Voyage.OnLandmarkArrive`, which is the place to extend arrivals later.

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

### M2 art and audio

- **Skies:** `AT_<Leg>_SkyPano` is swapped into the panoramic skybox per leg, and the sea reflects the same panorama. Shooting stars on Ember Drift are drawn live by the star veil. Without a panorama, the procedural dome draws the leg's aurora or nebula itself.
- **Landmarks:** `AT_AuroraGate`, `AT_FallenStar`, `AT_GlacierBell` and `AT_WhiteStar` use URP/Lit with emission at 4–4.5 (the White Star at 9), plus a point light and halo at their `Anchor_*`. The Glacier Bell's `Bell`/`BellInner` and `Clapper` swing on arrival. The White Star flares.
- **Props:** `AT_MeteorRock`, `AT_IceFloe`, `AT_StarFragment` and `AT_StarBuoy` share one atlas material. Each has a mirrored copy.
- **Currents:** `AT_Current` is scrolled along the currents in the sea shader. The constellations use `AT_ConstellationNode` and `AT_ConstellationLine` (on a LineRenderer).
- **Audio:** one music loop per leg, crossfaded on a leg change. `current_rush_loop` plays while you ride a current. One-shots: `meteor_bump`, `ice_creak`, `star_fragment_chime`, `constellation_complete`, `boost_whoosh` and `glacier_bell`. The bump, bell and fragment chime have procedural stand-ins.
- Everything is still found by name, with fallbacks.

## Building

The setup is the same as Salt and Glory: Unity 6000.3.26f1, URP and WebGL. `BuildWeb.Build` builds the scene from code, then builds:

```
Unity -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod BuildWeb.Build -logFile build.log
```

- Release goes to `docs/`. It uses gzip with the decompression fallback and has threads off. It also gets a `.nojekyll`, the title and favicon patch (`Web/favicon.png`), and the touch and scroll guards.
- `AT_QA=1` makes a release-like build in `Builds/WebGLQA` that includes the `window.atUnity` QA hook (`Voyage.QAStatus`, `QAPlace`, `QALeg`, `QAApproach`, `QAClearSave`, `QATitle`).
- `AT_DEV=1` makes a development build. Its dev keys are F2 (near the landmark), F3 (next fragment), F4 (drift to the edge) and F5 (next leg).
- The release build contains neither the QA hook nor the dev keys.
