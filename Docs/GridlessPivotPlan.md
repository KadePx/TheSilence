# The Silence: Gridless Pivot Plan

Battles move off the hex grid onto an open field seen at a tilt, with ships on round bases, momentum, arcs and
debris that blocks fire. Everything outside the battle (run map, events, shipyard, deck, heat, Ordinals, edicts,
council, nemeses) stays as it is. This plan covers the rules, the code, the art, the order of work, and the
decisions still open.

Status: a playable one-fight prototype is on branch `iso-prototype` (menu, "Gridless battle"; rules in `Core/Open`).
The notes below build on what playing it showed.

## What the pivot changes, and what it keeps

| Area | Today (hex) | After the pivot |
| --- | --- | --- |
| Positions | Hex `(q, r)` on a 9×7 board | Whole-number points on a field about 40×24 spans (1 span ≈ 1 old hex) |
| Facing | 6 directions | 8 headings, one per drawn facing (see decision 1) |
| Size | One hex per ship | A round base per ship: escort 0.6, line ship 0.9, cruiser 1.2, boss 1.6–2.5 spans |
| Movement | Speed and turn cards | **Free helm** each turn within the ship's limits; cards give extra (see below) |
| Momentum | Drift ahead by Speed | Unchanged: drift Speed spans along the heading at the end of the side's turn |
| Arcs | Fore/port/starboard/aft by hex angle | Same four arcs by angle; boundary goes to fore and aft |
| Line of fire | Asteroid hexes block | Wrecks and rocks block; small debris gives cover (+1 armor) |
| Enemy orders | Declared before your turn | Kept, and shown on the field: target line, the arc it fires from, and its planned path |
| Shields and armor | Per arc | Unchanged (the prototype simplified to one pool; the real port keeps four arcs) |
| Hazards | Ion storm, dense field, nebula | Ion storm unchanged; dense field = more debris; nebula becomes cloud zones that cut range inside them |
| Craft | Darts and gunships on hexes | Small bases, no facing, move freely up to their reach, swarm a target |
| Everything else | | Unchanged: deck, heat, crits and junk, Ordinals, edicts, council, run loop, rewards |

### The helm (Kade's call, 2026-11-28)

Turning and speed are not cards. Each turn, before the drift, every one of your ships may:

- turn up to its **agility** from where it began the turn (escort 135°, line ship and cruiser 90°, boss 45–90°),
  one point (45°) less at its top speed. Turning back is free, so a mistake can be undone until the turn ends;
- change speed by **one step** up or down;
- a ship that began the turn stopped, and stays stopped, can pivot to any heading (so no ship is ever boxed in by
  rocks).

The current maneuver cards become boosts on top of the helm: Burn (+1 speed past the limit and move 1 span now),
Hard About and Come About (turn further this turn), Screening Maneuver (move an escort beside the flagship),
Sacrificial Ram (drive into contact). Enemy ships use the same limits.

## Decisions

Kade accepted all five recommendations on 2026-11-28. In the prototype now: 8 headings, the free helm, a 40×22 span
field with pan and zoom, and the pilot hulls. What the pilot showed is under "Art plan".

### As first proposed

1. **How many headings.** PixelLab draws 8 facings. With 24 rule headings (the prototype), the sprite is up to 22°
   off the true heading and the base's tick has to carry it. **Recommendation: 8 headings (45° steps)**, so the
   sprite always shows the true heading. Arcs stay 60° fore and aft. The helm then turns in points: an escort two
   points, a cruiser one.
2. **View angle.** PixelLab's "low top-down" view is a three-quarter view, not true isometric. **Recommendation:
   keep it**, and set the field's tilt to match what the art shows, so a heading on the field matches the sprite.
3. **Ship modules in battle.** Module layers on the hull work on one top-down sprite; on eight tilted facings each
   module would need eight drawings. **Recommendation:** no module layers on the battle sprite. The shipyard keeps
   today's top-down hull with modules; battle shows modules as icons on the ship's panel; each flagship gets one
   "refitted" 8-facing variant for the late run.
4. **Card pictures.** Cards crop the top-down hull today. **Recommendation:** crop the new east facing, so cards
   match the battle.
5. **Field size and camera.** **Recommendation:** about 40×24 spans; two zoom steps (whole map at 1×, close at 2×,
   mouse wheel or Z); pan by dragging or the arrow keys; Space centres on the selected ship. A small field map is
   only worth adding if the zoomed-out view is hard to read.

## Look and scale (Kade, 2026-11-28)

Settled from the mock-ups in `ArtSource/Screenshots/mock_large_battle*.png` (composer:
`ArtSource/MockBattle/compose_large_battle.py`):

- **Scale:** a large-scale fleet battle. Fields several screens wide (the mock-up's is 60×36 spans), ships at 1×,
  six or more ships a side, long moves (speeds 4–6), a minimap with the camera's window and reserves arriving.
- **Angle:** a strong tilt (0.5–0.6) with ships drawn in a real three-quarter view (`create_8_direction_object`).
- **The plane:** not free-floating and not a board. **Orbital rings and spokes centred on the field's landmark**
  (rings every 4 spans, stronger every 12, bearing ticks on the 12-span ring), over a **wide square lattice** every
  8 spans with crosses where its lines meet. Faint, so ships and orders carry the screen.
- **Ships above the plane:** each ship floats a little above its point on the plane, tied to it by a short line and a
  small marker in its side's colour, with a soft shadow on the plane.
- **Orders:** a ship's course is a long curved path you drag, ending in a ghost of where it will be; the whole fleet
  moves when the turn ends. The enemy's announced moves show as shorter red paths.



The prototype's `Core/Open` becomes the battle core. One battle model at the end: `Core/Hex` and the hex board go.

| Step | Work | Size |
| --- | --- | --- |
| 1 | Prototype: free helm, bigger seeded field, pan and zoom, so the feel can be judged before the port | Small |
| 2 | Geometry into `Core/Battle`: `Unit` gets a point, heading and base radius; `Arcs.Of`, range and line of fire move to `OpenMath`; per-arc shields kept | Large |
| 3 | Effects: rewrite the ones that use hex adjacency (Advance, Speed, Turn, Ram, Screen, Flak, Sweep, Boarding, Launch and Sortie); the rest take the new range check unchanged | Medium |
| 4 | Enemy AI: intents keep their cycle; Maneuver plans a path within the helm limits and shows it; torpedo runs become craft paths | Medium |
| 5 | Content: encounter files get `x, y, heading` for each unit (a script converts today's `col, row, facing`); `scatterAsteroids`/`scatterDebris` feed the field generator; hazards as zones | Small |
| 6 | Battle screen: replace `BoardView` with the prototype's field view (sprites, bases, arcs, drift, intents), keep `BattleHud` (cards, panels, log, end turn) | Large |
| 7 | Tutorial rewritten for the helm and the field | Medium |
| 8 | Bots and sims: `SimpleBot` and `RunBot` steer with the helm; rerun `balance_sim` and `race_nemesis_sim`, retune | Medium |
| 9 | Docs: combat rules in `GameSpecification.md`, hex conventions out of `CLAUDE.md` | Small |

Rules stay whole-number and seeded, so runs still replay. Every rule change gets a test; the hex tests that test
positions are rewritten, not dropped.

## Art plan

Every hull is redrawn in **8 facings** with `create_8_direction_object`, using today's east sprite as the reference
image so the designs we have stay recognisable. Cost depends on the longest side of the sprite: 10 generations up
to 84 px, 15 up to 112 px, 25 up to 168 px. Anything over 168 px (late bosses, The Silence) needs one image per
facing instead.

### Step A: pilot (done, 65 generations)

Result (sheet: `ArtSource/IsoPilot/pilot_sheet.png`):

| Method | Long Promise | Outrider | Blade |
| --- | --- | --- | --- |
| Expand today's east sprite to 8 facings (`import_object` + `expand_object_to_8_directions`, 25) | **Best**: keeps the quilted hull, drum and pod | — | — |
| Text + today's sprite as style image, low top-down (`create_object_pro_flash`, 6–8) | Lost the quilted hull | **Good**, kept | **Good**, kept |
| Same, high top-down | Became a different ship | Lost the catamaran | Usable, less like the original |

- **Kade's verdict (2026-11-28): the pilot looks too flat.** Kade prefers the first demo: a strong tilt (0.6) and
  hulls drawn by `create_8_direction_object` in a true three-quarter view. The pilot routes (expansion and text plus
  style) both come out close to top-down. **Route for production: `create_8_direction_object`, low top-down,** with a
  per-faction style object (`style_object_id`) so a fleet matches; to be tested next for keeping today's designs
  (its reference-image mode).
- ~~Route: expand the existing sprite for flagships; text plus style image for the rest.~~ Superseded.
- **View angle:** PixelLab's low top-down ships are barely foreshortened (nose-up about 0.9 of side-on), so the field
  was tilted to 0.85 to match. Kade found that flat; the field is back at 0.6 with the demo hulls.
- **Cost:** cheaper than planned. Text-plus-style hulls are 6–8 generations for all eight facings.

The plan as written before the pilot:

Three hulls from their current sprites: Long Promise (128 px), Outrider (80 px), Blade (64 px). Each in low and high
top-down, to settle decision 2. Judge them on a contact sheet for:

- the same ship in all eight facings (the demo frigate changed proportions between facings);
- faction palette and outline matching the rest of the game;
- reading at 1× when zoomed out.

If the reference image does not hold the design, fall back to generating from the description plus a style image
(the demo route), and keep a per-faction style object for consistency.

### Step B: the units in the game today (about 280 generations, about 420 with rerolls)

Revised after the pilot: the six 128 px ships by expansion (25 each, 150), the other twenty by text plus style image
(6–8 each, about 130). The table below is the pre-pilot estimate, kept for the unit list.

| Size class | Units | Cost each | Subtotal |
| --- | --- | --- | --- |
| 128 px (flagships, bosses) | Sixth Verdict, Ninth Sentence, Hand of the Prime, Long Promise, Common Cause, First Cantor | 25 | 150 |
| 96 px (line ships, nemeses) | Vat-warden, Bastion, Clean Hand, Final Tally, Tithe of Nine, Concord carrier, Line frigate, Mender's Oath, Open Hand, Quiet Ledger, Tender | 15 | 165 |
| 64–80 px (escorts) | Blade, Blade lancer, Skiff, Torpedo skiff, Outrider | 10 | 50 |
| 16–32 px (craft) | Ordinal dart, Ascendancy missile, Concord gunship, Concord torpedo | 10 | 40 |
| | | | **405** |

Normal maps for all eight facings cost 0.1 each (about 25 in total) and are only needed if battle lighting comes
back.

### Step C: the field (about 225 generations)

| Asset | Use in the rules | Count | Est. |
| --- | --- | --- | --- |
| Wrecks (Ascendancy, Concord, neutral, ancient) | Block fire and movement | 4 variants | 40 |
| Asteroid clusters, small and large | Block fire and movement | 6–8 picks | 20 |
| Debris fields (low, scattered) | Cover: +1 armor to shots through them | 3–4 | 10 |
| Derelict hulk, 256 px | One big blocker per map | 2 | 20 |
| Nebula cloud, ion storm, minefield | Hazard zones drawn as soft overlays | 3–4 | 40 |
| Field backdrops, 688×384, shown with parallax | One per sector (two exist: Kessane, Ion Reef) | 3 | 75 |
| Ship shadows, bases, arcs, beams, tracers | Drawn in code | — | 0 |
| Explosions, hits, shield flashes | VFX workbench, as today | — | 0 |

### Step D: the 1.0 roster (later, about 400 more)

The roster in `FleetAndAssetPlan.md` adds about 20 units (battleships, carriers, the Chorus, pirates, Antecessor
defences). At the same rates that is roughly 400 generations, plus single-facing sets for the 192–256 px bosses.

### Budget

| Step | Generations |
| --- | --- |
| A, pilot | ~90 |
| B, today's units with rerolls | ~420 (was ~600) |
| C, the field | ~225 |
| **Before the port can ship** | **~735** (was ~915) |
| D, the 1.0 roster | ~400+ |

The account has about 2,600 generations left this cycle (4,800 a cycle; resets 26 December), so Steps A to C fit in
this cycle. Every generation still goes in `ArtSource/pixellab-log.csv`. Ascendancy art follows the Ordinal brief and
the "nothing reads as Star Wars" check.

## Order of work

1. **Prototype step 1** (helm, bigger field, pan and zoom). Play it; confirm decisions 1–5.
2. **Art pilot** (Step A) in parallel, since it does not touch code.
3. **Port** (code steps 2–6) on a long-lived branch; the hex game stays playable on `main` until the port plays a
   whole run.
4. **Art Step B and C** as each unit and the field come over.
5. **Tutorial, bots, balance, docs** (code steps 7–9); merge when a whole run plays and the sims look sane.

## Risks

- **Facing consistency.** The cheapest way to get eight facings may not hold a design. The pilot answers this before
  most of the spending.
- **Readability at 1×.** A big field means small ships when zoomed out. Bases, hull bars and order lines in code
  have to carry it.
- **The bot.** Balance numbers rest on bot runs, and steering in open space is harder to write well than hex
  steps. Expect the first sims to be rough.
- **Battle length.** Prototype fights run about 17 turns with the bot. A bigger field makes that longer; the free
  helm and closer starting distances should pull it back.
- **Pixel-crisp overlays.** Lines and arcs drawn in code are smooth today. Drawing them into a 1× render texture and
  scaling it up keeps them on the pixel grid.
