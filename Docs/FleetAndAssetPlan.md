# The Silence: Fleet and Asset Plan

Nov 26, 2026 · @Kade

## Summary

The 1.0 game needs about **45 ship and unit types** (15 capital hulls, 12 small craft, 18 enemy, nemesis and boss units) plus roughly 400 other art assets. Through PixelLab that is an estimated **3,000–3,500 generations**, about 2 months of the Tier 1 allowance (2,000 per month). Effects are free through the VFX workbench.

**Already done:** 3 flagships (Ascendancy lance-cruiser, Concord trimaran, Chorus Cantor) with facings and normal maps, an Ordinal dart and a Concord gunship. 64.3 generations used.

### Decisions (confirmed)

| Decision | Choice |
| --- | --- |
| Screen scale | Native 1920×1080 at 1× pixel scale (2× at 4K); hexes about 96 px wide |
| Modules on the diagonals | Modules layered on the East sprite; Unity rotates the composite to NE/SE once per refit and caches it |
| Interface style | Pixel art UI in the etched-gunmetal look |
| Portraits | Pixel busts at 128 px |
| Audio | Minimal sound effects to start (weapons, hits, explosions, UI clicks); music later |

## Fleet roster

Each faction gets 5 capital hulls covering the same five roles, so fleets are comparable, plus 4 small-craft types. Sizes are the sprite canvas on the East facing; diagonals need about 1.25× that canvas. Names are placeholders.

### Capital roles (shared)

| Role | Slots | Job in combat |
| --- | --- | --- |
| Escort | 2 | Cheap, fast screen; intercepts and dies for the line |
| Frigate | 3 | Flexible line ship; most of a fleet early on |
| Cruiser | 4 | Flagship option; balanced and deep to customize |
| Battleship | 5 | Slow, armored, huge firepower; a late-run centerpiece |
| Carrier / support | 4 | Hangars, repair or synapse; force multiplier |

### The Ascendancy

| Unit | Type | Size | Notes |
| --- | --- | --- | --- |
| Blade | Escort | 64 px | Needle hull, one flank battery, point defense |
| Verdict-class | Frigate | 80 px | Short lance, two flanks; the workhorse |
| Sixth Verdict (done) | Cruiser | 128 px | Lance-cruiser flagship |
| Cathedral | Battleship | 160 px | Twin spires, triple lance, stepped decks; slow |
| Reliquary | Carrier | 144 px | Long hangar decks of Ordinal darts and boarding pods |
| Ordinal dart (done) | Interceptor | 32 px | Numbered clone fighter |
| Penitent | Bomber | 32 px | Heavier, one torpedo slung beneath |
| Censer | Gunboat | 40 px | Armored, slow, flak |
| Waymind drone | Support | 24 px | Repair and sensor jamming |

### The Concord

| Unit | Type | Size | Notes |
| --- | --- | --- | --- |
| Skiff | Escort | 64 px | Single hull with one small outrigger |
| Outrider | Frigate | 80 px | Catamaran, two mismatched hulls |
| Long Promise (done) | Cruiser / carrier | 128 px | Trimaran flagship |
| Council Ark | Battleship | 160 px | Four hulls around a great drum; council chamber |
| Tender | Support | 128 px | Repair yard ship with drone bays and cranes |
| Kestrel | Fighter | 32 px | Light, from one species' design |
| Hauler-bomber | Bomber | 32 px | Converted cargo hauler |
| Gunship (done) | Gunboat | 32 px | Navy and ivory, amber engines |
| Repair drone | Support | 24 px | Tethered, named by its crew |

### The Chorus

| Unit | Type | Size | Notes |
| --- | --- | --- | --- |
| Chorister Lancer | Escort | 64 px | Narrow body, one spine |
| Chorister | Frigate | 96 px | Line organism, acid maw |
| The First Cantor (done) | Cruiser / command | 128 px | Synapse hub; grows larger at higher rank |
| Chorister Bulwark | Battleship | 160 px | Heavy chitin, slow, crushing mandibles |
| Brood Mother | Carrier | 144 px | Swollen sacs that spawn Motes |
| Mote | Swarm | 16–24 px | Tiny, many on the board |
| Thrall | Bomber | 32 px | Suicide burster |
| Spitter | Gunboat | 32 px | Acid artillery |
| Leech | Support / boarding | 24 px | Latches on, drains |

## Enemies, nemeses and bosses

Rival factions reuse the player rosters above, so most enemies cost nothing extra; the additions are neutral threats, nemesis variants and bosses, about 18 units.

| Group | Units | Size | Notes |
| --- | --- | --- | --- |
| Pirates | Raider, Corsair, Pirate flagship | 64 / 96 / 128 px | Scrap-built, red and rust; mixes stolen parts of all three factions |
| Antecessor defenses | Sentinel turret, Warden platform, Seeker drone | 64 / 128 / 32 px | Ancient, black and pale-gold, inactive until woken |
| Hazards as units | Mine, Derelict hulk, Ion pylon | 24 / 160 / 64 px | Board objects that act or block |
| Nemesis variants | 4 commanders per rival faction | Their flagship + overlays | Scars, salvaged plates and refits added by repainting areas of the base ship, one layer per grudge level |
| Sector bosses | 3 per route (about 6 unique across routes) | 192–256 px | Rival flagships at full strength, an Antecessor guardian, a Chorus hive-queen |
| The Silence | Final encounter | 256 px+ | Multi-part boss across several hexes; variants by race outcome |

Bosses above 160 px may span two hexes; their facings follow the same 3-drawn rule.

## Per-unit deliverables and pipeline

Every unit is generated once facing East; everything else is derived from that sprite, mostly for free.

| Deliverable | How | Cost per unit |
| --- | --- | --- |
| East sprite | `create_image_pro` with the faction palette and a style reference (4 candidates at 86–128 px, 64 at 32 px) | 10 per call, often 1–2 calls |
| NE and SE facings | Exact 60° RotSprite rotation (our script), then a short cleanup | Free |
| W, SW, NW facings | 180° turns of E, NE, SE | Free |
| Normal map | `create_normal_map` lite, OpenGL convention | 0.1 |
| Engine glow | 4-frame loop via `animate_image`, or an emissive layer pulsed in Unity | \~1 or free |
| Damage | Shared per-faction overlay sprites (scorch, breach, fire, smoke) placed at module sockets | Shared set |
| Death | Explosion and debris from the VFX workbench | Free |

### Capital modules

Each capital hull has 4 slots × 3 modules × 3 tiers. Modules are separate layers drawn only on the East sprite, then cut out from repaints of the base hull.

1. Generate the base hull, East facing.
2. Repaint one slot area with the module description (`inpaint_image_pro_flash`, about 6 per module and tier).
3. Cut the repainted area out as its own layer with the free pixel tools.
4. In Unity, stack hull and module layers on the East facing, then rotate the result to NE and SE once per refit and cache it.

That is about 36 repaints (around 220 generations) per hull family. Only flagship-capable hulls (one per faction at launch) need the full set; the others use fixed loadouts with 1–2 options per slot.

## Everything else

The non-ship assets are about 400 items; effects are free and the rest is mostly cheap icons and UI.

| Area | Assets | Count | Tool | Est. generations |
| --- | --- | --- | --- | --- |
| Effects | Engine flames, lance beam, broadside shots, torpedo and missile trails, small/large explosions, shield hits per faction color, fire and smoke loops, warp in/out, Chorus ichor and spore cloud, boarding | \~25 | VFX workbench | 0 |
| Battle environment | Board backgrounds per sector (3 + the Approach), asteroids ×4, debris, nebula hex, gravity well, minefield, ion storm | \~15 | `create_image_pro`, opaque backdrops | \~150 |
| Star map | Node icons (battle, elite, nemesis, event, shipyard, market, relic, hazard, 3 faction nodes, boss), map background, race-track markers | \~18 | `create_image_pro` at 32–48 px | \~60 |
| Interface | Panels, buttons and their states, card frames (per type and state), cost badge, tooltips, bars | \~30 | `create_ui_asset`, `create_ui_states` | \~350 |
| Intent and status icons | Attack, torpedo, move, buff, debuff, spawn, unknown; statuses and keywords | \~30 | `create_image_pro` at 16–24 px | \~60 |
| Resource icons | Scrap, fuel, supplies, crew, favor, Ordinals, biomass, command | 8 | `create_image_pro` | \~20 |
| Portraits | 8 nemeses (+ scar variants), \~12 Concord officers, the Prime, Inquisitor, \~6 event characters | \~30 | `create_portrait_character`, then repaints for scars | \~400 |
| Chorus presence | Animated light pattern instead of a portrait | 1 | VFX workbench | 0 |
| Relic icons | Neutral and faction relics | \~60 | `create_1_direction_object` batches | \~150 |
| Card art | Module cards crop the ship art automatically; unique art for faction, event and Edict cards | \~60 | `create_image_pro` | \~300 |
| Event illustrations | Pixel scenes for events and story beats | \~40 | `create_image_pro`, opaque | \~400 |
| Audio | \~80 sound effects; \~10 music tracks (menu, map, battle per faction, boss, hail) | \~90 | `create_sound_effect`, `create_music` (test first) | \~200 |
| Fonts | Pixel fonts for UI and rules text | 2–3 | Licensed fonts, or `create_font` | — |

## Screens, map and key art

The game's non-battle screens are pixel art too, with a full-screen illustration at a few big moments. Large scenes are generated at 688×384 (25 generations, one image) and shown at 2× or larger.

| Screen | Art | Count | Est. generations |
| --- | --- | --- | --- |
| Main menu | Title key art (the Silence and three racing fleets), animated drift and engine glow, logo, menu panel | 1 scene + logo | \~50 |
| Faction select | One portrait-style scene per faction (homeworld or flagship hero shot) | 3 | \~75 |
| Star map | Background per sector (3 + the Approach), node icons, lanes, race track along the top, rival fleet markers | 4 backgrounds + \~18 icons | \~160 |
| Sector transitions | Full-screen story scene at the start of each sector | 4 per faction route (\~8 unique) | \~200 |
| Shipyard and refit | Drydock backdrop behind the refit bay | 1 per faction | \~75 |
| Hail | Comms-screen frame around the portrait | 1 | \~25 |
| Endings | One scene per ending (3 faction wins, 3 rival wins, the true ending) | 7 | \~175 |
| Run over, settings, codex | Simple backdrops reusing map art | 2 | \~50 |

About 810 more generations, mostly in M4 and M6; samples of the menu key art and the star map are being tested now.

## Asset spec sheet

Every sprite follows these rules so generated art, rotations and Unity import stay consistent.

- **View and light:** top-down, seen from directly above, lit evenly from overhead; East-facing (pointing right) as the source.
- **Outline and colors:** crisp dark outline, no anti-aliasing, only the faction palette.
- **Canvas:** transparent background; the ship centered; final facing canvases 1.25× the East size (128 px ships live on a 160 px canvas) with the pivot at the center.
- **Style reference:** the Ascendancy cruiser for outline, detail and shading on every new unit; the faction's own flagship for its colors.
- **Import:** Point (no filter), no compression, pixels per unit set so 1 pixel = 1 screen pixel at 1080p.

### Faction palettes

| Faction | Colors (hex) |
| --- | --- |
| Ascendancy | #06070a #14161c #2b2f3b #4f5668 #7c8499 #a9b0c0 #d3d8e2 #f4f6fa · gold #4a3712 #8a6a24 #c89e40 #e8c870 #fff0b8 · cyan #1f6276 #3aa0bc #7fdcf2 #c8f6ff |
| Concord | #06070a #070b16 #121b33 #26375e #4f6898 #7f98c4 · ivory #7a6c50 #bfb08c #ebe0c6 #fffaf0 · copper #3a1c0a #8a4a20 #d0864a #ffd09a · amber #a0600c #ffc46a #fff6dc |
| Chorus | #030105 #180820 #43185a #7e3c8c #c27ccc · membrane #a2448a #f0a0d0 · bone #6c604e #c4b698 #f4ecd8 · synapse #0f5c48 #1c9474 #44d8aa #98ffdc · feral #ff5a9a |

### Naming and folders

```
<faction>_<unit>_<facing>[_<variant>].png        asc_cruiser_NE.png, con_trimaran_E_dmg.png
<faction>_<unit>_<slot>_<module>_T<tier>.png     asc_cruiser_prow_lance_T2.png
<file>_n.png                                     normal maps

Assets/_Project/Art/Ships/<Faction>/<Unit>/
Assets/_Project/Art/Modules/<Faction>/<Unit>/
Assets/_Project/Art/Craft/  Effects/  Board/  Map/  UI/  Icons/  Portraits/  Events/
ArtSource/   PixelLab job IDs, seeds, prompts and candidates kept for re-runs
```

## Production order

Art is produced just ahead of the milestone that needs it, so the first Unity prototype needs only about 150 generations.

| Milestone | Art needed | Est. generations |
| --- | --- | --- |
| M1 Grey-box combat | 2 Ascendancy hulls (cruiser done + escort), Concord frigate and gunship (done), dart (done), torpedo, 2 asteroids, hex board background, intent icons, basic effects (shots, lance, explosion, shield hit) | \~150 |
| M2 Run loop | Map node icons and background, shipyard UI, card frames, resource icons, 5 event illustrations, 1 boss, Ascendancy modules for the cruiser (36 repaints) | \~700 |
| M3 Race, nemesis, Concord | Concord roster and trimaran modules, 4 Concord nemesis portraits with scar variants, officers, hail UI | \~800 |
| M4 Vertical slice | Sector 1 complete for 2 factions: pirates, Antecessor defenses, remaining effects, music and sound test, polish pass on all diagonals | \~500 |
| M6 Content | Chorus roster and modules, remaining bosses, the Silence, relics, remaining events, full audio | \~1,200 |

About 3,350 generations in total, spread across the milestones, so the monthly allowance is not the bottleneck. Keep a record of every PixelLab job ID, seed and prompt so any asset can be re-run in the same style.
