# Astral Tide – M1 art (Dusk Shallows)

All art is original and made procedurally for this project (Blender low-poly models with hand-painted-style atlases, plus Pillow/numpy textures). Units are metres. `.meta` files are included, so the import settings below are already applied when the folder lands in a Unity 6000.3 project.

## Ship – `Ship/`
| File | What | Use |
|---|---|---|
| `AT_Skiff.fbx` | Small single-sail skiff with a bow lantern, ~1,080 tris, 38 child meshes under root `AT_Skiff`. ~4.6 m hull (5.2 m with the lantern pole), mast top 3.9 m. **Bow faces +Z, waterline at y=0** (keel goes to y≈-0.44). Scale 1, no materials imported. | Drop in and assign one material using the two maps below. Child `LanternGlass` sits at (0, 1.19, 2.57) – a good anchor for a small warm point light. |
| `AT_Skiff_Albedo.png` | 1024² painted atlas: indigo hull planks with cream trim and gold pinstripe, honey deck, cream sail with a violet star emblem, wood, rope, brass. Soft shading is painted in. | Base Map (sRGB). |
| `AT_Skiff_Emission.png` | Black except the lantern glass (warm gold). | Emission Map, colour white, intensity ~2–4 (HDR) so bloom picks it up. |

## Wake – `FX/`
| File | What | Use |
|---|---|---|
| `AT_Wake.png` | 256×1024 RGBA glowing foam trail. U = across the trail (soft edges, alpha 0 at U 0/1), V = along the trail, **tiles seamlessly in V**. Pale lavender-white. | TrailRenderer (Texture Mode = Tile, Alignment = TransformZ facing up) or a scrolling quad behind the stern. Use an additive or alpha-blended unlit particle material; fade the tail with the trail colour gradient. Import: Clamp U / Repeat V, alpha is transparency. |

## Lantern Spire – `Landmarks/LanternSpire/`
| File | What | Use |
|---|---|---|
| `AT_LanternSpire.fbx` | Tall octagonal lavender-stone tower on a rocky islet, gallery rail, glowing lantern room, blue roof and a star finial. ~890 tris, 34.6 m tall, islet ~18 m across, base at y=-1.5 (rock sits on the waterline). Scale 1. | One material with the maps below. Lantern room centre is (0, 26.3, 0) – put a point light / glow billboard there for the horizon beacon. Scale up freely for a bigger silhouette on the horizon. |
| `AT_LanternSpire_Albedo.png` | 1024² painted atlas: stone blocks with cream bands, dark rock, blue roof, brass, warm glass. | Base Map (sRGB). |
| `AT_LanternSpire_Emission.png` | Lantern glass (with dark mullions), star finial and window slits glow. | Emission Map, intensity ~3–6 (HDR). Animate intensity as the player gets close if you like. |

## Sky – `Sky/`
| File | What | Use |
|---|---|---|
| `DuskShallows/AT_DuskShallows_SkyPano.png` | 4096×2048 equirectangular panorama: peach horizon → lavender → deep violet, painterly brush clouds, first stars thickening with height. The lower half is a dim mirrored copy, a fallback for anything below the horizon. Seamless in U. | Material using `Skybox/Panoramic` (Mapping = Latitude Longitude Layout, Image Type = 360°), set it in Lighting > Environment > Skybox Material. Import: Repeat U / Clamp V, **mipmaps off** (stops a seam line), max size 4096. The sea shader can sample it for reflections, or use a reflection probe. |
| `AT_Star.png` | 128² white 4-point twinkle star with a soft halo (RGBA). | Sprite (Single). Particle or billboard for bright stars, star gusts and fragment glints. Tint per leg; use an additive material. |
| `AT_Starfield.png` | 1024² seamless RGBA star field (white stars on transparent). | Repeat wrap. An extra parallax layer, or the sea's reflection-sparkle layer, scrolled slowly. |

## HUD – `UI/Compass/`
| File | What | Use |
|---|---|---|
| `AT_StarCompass_Strip.png` | 2048×64 RGBA strip covering the full 360°, **seamless horizontally**, with no letters. **u = 0 is north** (big gold 8-point star). Going clockwise: E at u=0.25, S at 0.5, W at 0.75 (pale 4-point stars), 45° marks as small stars, 15° marks as tiny stars, 5° dots and a dotted baseline. | Default texture type, Repeat U / Clamp V, no mipmaps. Put it on a UI `RawImage` and set `uvRect = new Rect(heading/360 - w/2, 0, w, 1)`, where `w` = visible arc/360 (for example 0.25 for a 90° window). Display about 900×28–40 px at the top of the screen. |
| `AT_StarCompass_GoalMarker.png` | 64² warm gold downward pointer with a small star (RGBA). | Sprite. Put it above the strip at the goal bearing; brighten or pulse it when the ship is on course (matches the chime). |

## Render setup notes (URP)
- The models expect URP/Lit (or Simple Lit) with the Emission map on, plus **Bloom** in a Global Volume (threshold ~0.9, intensity ~0.6–1) for the lantern glows. Linear colour space is assumed.
- The models have flat-shaded faces and painted-in shading, so they read best with soft lighting: a low lavender-pink directional light plus ambient from the skybox.
- The wake, stars and compass use alpha. Use Unlit/Particles materials (Transparent, Additive or Alpha) for the FX.

---

# Astral Tide – M2 art (Aurora Reach, Ember Drift, Frost Mirror, The Cosmic Sea)

Same conventions as M1: original procedural art (Blender 4.2 low-poly models with painted 1024² atlases, Pillow/numpy textures), metres, scale 1, `.meta` files included, no materials imported. Every FBX has **y up, front facing ±Z** (the player approaches along Z), and **waterline at y=0** unless noted. Named `Anchor_*` empty transforms are children of each root, so code can find glow centres with `transform.Find("Anchor_…")`.

## Sky panoramas – `Sky/<Leg>/`
All are 4096×2048 equirectangular, seamless in U, with a dim mirrored lower half (the Frost Mirror one is a crisper, brighter mirror for its still water). Import exactly like `AT_DuskShallows_SkyPano`: `Skybox/Panoramic` (Latitude Longitude, 360°), Repeat U / Clamp V, **mipmaps off**, max size 4096.
| File | Mood |
|---|---|
| `AuroraReach/AT_AuroraReach_SkyPano.png` | Deep teal night, three wavy layers of teal-to-green aurora curtains (10–45° up) with soft vertical rays, stars above. |
| `EmberDrift/AT_EmberDrift_SkyPano.png` | Deep indigo with a soft rose-ember horizon glow, dense stars and 16 painted shooting-star streaks. Add live shooting stars on top with `AT_Star` particles. |
| `FrostMirror/AT_FrostMirror_SkyPano.png` | Silver-white horizon to ice blue to steel blue, thin silver cirrus strokes, sparse cool stars high up. |
| `CosmicSea/AT_CosmicSea_SkyPano.png` | Deep purple with a broad magenta/violet nebula band (blue hot spots, dark dust lanes) and a dense starfield. |

## Landmarks – `Landmarks/<Name>/`
Each has `<Name>.fbx` + `<Name>_Albedo.png` (Base Map, sRGB) + `<Name>_Emission.png` (Emission Map). Use one URP/Lit material per landmark, emission colour white at intensity ~3–6 (HDR) plus Bloom, as in M1.
| File | What | Size / anchors |
|---|---|---|
| `AuroraGate/AT_AuroraGate.fbx` | Two leaning stone obelisks on rock islets, joined by a glowing teal-green arch of light (a tube plus two flat ribbons), a crystal keystone and floating light shards. Crystals at the pillar bases. The ship sails through the opening. | 1,026 tris, 22 children. 39 m wide, 31.3 m tall, base y=-1.5. Opening ~24 m wide. `Anchor_GateCenter` (0, 15.8, 0) = arch centre; `Anchor_Keystone` (0, 28.7, 0). |
| `FallenStar/AT_FallenStar.fbx` | Jagged crater-rim isle with a molten glowing crater floor, a tilted white-gold 4-point star chunk half-buried in the middle, glowing shards around the rim and dark boulders. | 404 tris, 28 children. ~32 m across, 12.8 m tall. `Anchor_StarCore` (0.6, 3.8, 0.8) = core glow; `Anchor_CraterGlow` (0, 1.6, 0). |
| `GlacierBell/AT_GlacierBell.fbx` | Hexagonal ice tower on a spiky ice base, frost bands and glowing slit windows, an open six-pillar belfry with a glowing ice bell (open bottom, with a clapper), an ice spire and a glint crystal. | 776 tris, 37 children. 41.2 m tall, base ~22 m. `Anchor_BellGlow` (0, 28.2, 0) = bell centre (child meshes `Bell`, `BellInner` and `Clapper` can be swung for the ring); `Anchor_SpireTip` (0, 39.6, 0). |
| `WhiteStar/AT_WhiteStar.fbx` | Floating blinding-white star for The Cosmic Sea: a faceted core, four long main rays, four diagonal rays, front/back rays and eight small splinters, plus two thin halo rings facing ±Z. Fully emissive. | 740 tris, 22 children. Floats from y=14.7 to 45.4, 30.7 m wide. `Anchor_StarCenter` (0, 30, 0). **18 separate `Shard_00…Shard_17` children** plus `Core`, `HaloRing` and `HaloRingOuter`, so code can fling the shards outward for the "shatters like glass" moment. Use emission intensity ~8+ for the blinding look. |

## Obstacles and pickups – `Props/`
All four share one atlas: `AT_Props_Albedo.png` + `AT_Props_Emission.png` (1024²). One material for all of them is fine; use emission ~3–6.
| File | What | Size / pivot |
|---|---|---|
| `AT_MeteorRock.fbx` | Lumpy low-poly dark rock with glowing ember facets. | 80 tris, ~3.5×2.3×2.9 m, **pivot at the centre** (sit it half-submerged, or drift it at y≈0.3). |
| `AT_IceFloe.fbx` | Flat irregular ice slab with a bevelled top and a small ridge. | 100 tris, ~6.5×6.2 m, top at y≈0.3, a ridge up to 1.3, underside to y=-0.6. Pivot at the waterline. |
| `AT_StarFragment.fbx` | Faceted 4-point gold-white star crystal pickup. | 16 tris, 1 m across, 0.32 m thick, pivot at the centre, faces ±Z. Hover it ~1–1.5 m above the water and spin/bob it in code; add `AT_Star` as a glint. |
| `AT_StarBuoy.fbx` | Sail-through four-point star buoy: a glowing star frame (bottom point clipped at y=-0.9) with a round 6 m opening, a dark float at the bottom point and two side glints. | 356 tris, 18.5 m wide, 10.5 m tall. `Anchor_PassCenter` (0, 2.4, 0) = middle of the opening (radius 3 m), so the skiff (mast 3.9 m) fits. Faces ±Z. Scale down if you want small marker buoys instead. |

## FX and constellations
| File | What | Use |
|---|---|---|
| `FX/AT_Current.png` | 256×1024 RGBA glowing teal current stream: U = across (alpha 0 at the edges), V = along, **tiles seamlessly in V**, with flowing streaks and sparkles. | Same as `AT_Wake`: Clamp U / Repeat V. Put it on a long quad or spline ribbon lying on the water and scroll V over time (offset.y += speed·t), additive or alpha unlit. |
| `Sky/Constellation/AT_ConstellationLine.png` | 256×32 soft white-blue glowing line, tiles in U. | Default texture, Repeat U / Clamp V. Use a LineRenderer (Texture Mode Tile or Stretch) with an additive unlit material, width ~0.02–0.05 of the sky distance. Fade alpha in as fragments are collected. |
| `Sky/Constellation/AT_ConstellationNode.png` | 128² star node: bright core, small 4-point glint, thin ring (RGBA). | Sprite (Single), 100 PPU, no mipmaps. One per constellation star; dim until its fragment is found, then brighten or pulse. |

---

# Astral Tide – M3 art (Frost Mirror whale + ice floe material)

Same conventions as M1/M2: original procedural art (Blender 4.2 low-poly, Pillow textures), metres, scale 1, `.meta` files included.

## Whale – `Creatures/`
| File | What | Use |
|---|---|---|
| `AT_FrostWhale.fbx` | Scenery-only whale (not an obstacle): blunt head with a glowing mouth line and dark eyes, long tapering body, wide horizontal notched flukes, two long pectoral fins, small dorsal fin, steel-blue back / pale belly, a row of small glowing frost studs on the back. ~312 tris (Unity count). **~12.5 m long (z -6.45 tail tip … +6.05 nose), 6.2 m fin span, y -0.74 … 2.0. Faces +Z, pivot on the waterline at y=0** (back and dorsal ride above the water when idle). Hierarchy: `AT_FrostWhale` (root) › `Rig` (animated) › `WhaleBody` (mesh) + `Anchor_Spout` (0, 1.57, 4.55) = blowhole. Imports with `bakeAxisConversion` on, Generic rig, no materials. | One URP/Lit material: Base Map `AT_FrostWhale_Albedo`, Emission Map `AT_FrostWhale_Emission`, emission colour white, intensity ~1.5–2.5 (subtle) + Bloom. Spout: parent a short upward particle burst (pale white-blue, ~3 m) to `Anchor_Spout`. Scale 0.7–1.5 for variety. |
| `AT_FrostWhale_Albedo.png` / `_Emission.png` | 512² flat-band atlas (back, flank, belly, fin, glow, eye). Emission is black except the glow band. | Base Map (sRGB) / Emission Map. Use Point filtering or leave Bilinear. |

**Clip `Breach`** (4.0 s, 30 fps, no loop) animates only the `Rig` child: a short dive, then a leap that clears the water with a pitch-up and a roll (peak ~6 m up and ~6.5 m forward at 2 s), a side splash-down ~8.5 m ahead, and a glide back to the idle pose, so it starts and ends on the rest pose. The root never moves. Play it with an `Animator` (Generic) or `Animation`. Fire a splash at ~2.9 s, near `Rig` (z ≈ +8). Idle swim: leave the clip off and bob the root in code.

## Ice floe material – `Materials/` + `Shaders/`
| File | What | Use |
|---|---|---|
| `Shaders/AT_IceFloe.shader` (`AstralTide/IceFloe`) | Pipeline-agnostic unlit pass (renders in URP). It **needs no UVs**: it projects the ice tile top-down in world XZ, multiplies by the mesh's vertex colours (so the snow/side/under colours from `VoyageLegs.FloeMesh` still show), builds flat facet normals from screen derivatives, lights with one fixed key-light direction + ambient, and adds a bright HDR fresnel rim plus a top-edge sparkle as emission. | Works on any mesh. Properties: `_Tiling` (0.25 = 4 m tiles), `_Tint`, `_UseVertexColor`, `_LightDir`, `_Ambient`, `_RimColor` (HDR), `_RimPower`, `_EdgeGlow`. Bloom makes the rim glow. |
| `Materials/AT_IceFloe_Mat.mat` | Material using that shader, with `AT_IceFloe_Tile` assigned. | Assign it to the floe's `MeshRenderer.sharedMaterial`. |
| `Materials/AT_IceFloe_Tile.png` | 512² seamless pale blue-white faceted ice (cell pattern with bright crack lines). | Repeat wrap. |

Preview (box only): `/workspace/astraltide-previews/art/m3/whale_ice.png` (Blender renders; the floe there approximates the shader).
