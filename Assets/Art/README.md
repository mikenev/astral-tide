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
