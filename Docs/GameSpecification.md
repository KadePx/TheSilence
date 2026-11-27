# The Silence: Game Specification

Nov 26, 2026 · @Kade

## Overview

The Silence is a grimdark space roguelike for PC (Steam), built solo in Unity with commissioned art. Version 0.4: design direction is locked for prototyping; every number is a first guess and every name is a placeholder unless marked final.

**Mockups:** [Refit bay](https://claude.ai/artifact/8DniDJAobxSTcQ4BLRqnhJ) (all factions, modules, tiers, damage, rotation) · [Encounter](https://claude.ai/artifact/H3aiTJ9wV7jWo1tXcmQN23) (pre-battle hail and battle screen).

### Pitch

You command one of three civilizations racing across a dying frontier toward **the Silence**, an ancient weapon whose only known use wiped out the last great civilization. Its location was suddenly broadcast to everyone. It is probably a trap, but nobody can let a rival reach it first.

You build a fleet of up to **4 customizable capital ships** and **\~8 small craft**. Every module you fit adds cards to a **shared command deck**. Battles are turn-based on a small hex battlefield where **facing, momentum and heat** matter. You travel branching star maps, race rival fleets on a visible track, and face named nemesis commanders who remember you.

**Inspirations:** Slay the Spire, FTL, Into the Breach, Battlefleet Gothic, Shadow of Mordor's nemesis system, Darkest Dungeon's inked presentation.

### Design pillars

1. **Your fleet is your deck.** Every card comes from a ship, module, officer or mutation. Lose the ship and you lose its cards; damage shows up as junk cards.
2. **Every turn is a readable puzzle.** Enemies telegraph their actions and targets. Position, facing and heat decide how well your cards work.
3. **The race is always on.** Exploring costs time, and rivals advance on a track you can see.
4. **Three factions, three ways to play.** Shared rules, but each faction has its own core resource and route through the story.
5. **Grim, cold, consequential.** Crew die, choices are ugly, and the setting doesn't care about you.

## Setting and story

The last great civilization (*the Antecessors*) vanished in a single event tied to a weapon. That weapon's location has just been broadcast across the whole galaxy at once. Everyone suspects a trap; everyone goes anyway.

**Twist (proposal, revealed in the true ending):** the Silence was not the Antecessors' weapon. It is what killed them. It lures any civilization advanced enough to reach it, and the broadcast means it is hungry again. The Chorus, as one collective mind, may be the only faction able to "hear" it.

**Story delivery:** short faction intro and outro, story beats at sector transitions, events and derelict logs, Antecessor ruins that slowly reveal the truth, and nemesis dialogue in pre-battle hails.

| Ending | Condition |
| --- | --- |
| Faction victory ×3 | You take the Silence. Each ending is grim in its own way. |
| Rival victory ×3 | Shown when you lose a run, based on who led the race. |
| True ending | Unlocked after winning with all three factions. Reveals what the Silence is and who broadcast it. |

## Factions

Three factions share the rules but each has its own core resource. The Ascendancy and Concord are available from the start; the Chorus unlocks after the first win.

### The Ascendancy (Empire)

- **Fantasy:** clean, sleek and ruthlessly efficient. One pure super colony (*Lumen*) stays untouched while vat-bred armies die for it. Absolute obedience to **the Prime**.
- **People:** *Ordinals* are numbered combat clones (crew, boarders, ammunition). *Wayminds* are homunculi grown for one task each (navigation, targeting, reactor tending).
- **Core mechanic, Expenditure:** Ordinals are a fleet-wide resource (24 to start). Many cards carry an extra cost, *Expend X Ordinals*, for a big effect. They refill at shipyards and from harvest events. At 0, every card costs +1 command.
- **Edicts (mandatory):** the Prime sends objectives mid-run ("Lose no Waymind this sector"). You choose how to carry them out, never whether. Failure means lost favor, an *Inquisitor* aboard who adds junk, or an Ordinal purge; carrying one out well earns rewards.
- **Tone:** no redemption arc; the choices are about how ruthless and how efficient.
- **In combat:** precise long-range lances, strong fore armor, excellent focus fire; weak at recovering from attrition without spending crew.

### The Concord (Free Collective)

- **Fantasy:** a sincere alliance of humans and alien species, each with real cultures and roles, governed by a **Council** of political blocs. Broadly the "good" faction.
- **Core mechanic, Officers:** named specialists (engineer, gunner, navigator, diplomat, medic, pilot lead) assigned to capital ships. Each adds cards and a passive ability; pairings create synergies. Officers can be wounded or killed permanently.
- **Council Approval:** 3–4 blocs (e.g. Militarists, Traders, Pacifist Union, Species Rights League). Event choices raise or lower their approval, which opens or closes shops, officers, event options and ending details.
- **In combat:** flexible, strong shields, drone and fighter synergies, combo-heavy. Each ship hits less hard, but the fleet works well together.

### The Chorus (Hive)

- **Fantasy:** a single collective that is **sentient as a whole**, one mind speaking through countless bodies, at war with everyone.
- **Ranks:** *Cantor* (command organism, flagship) → *Choristers* (line ships) → *Motes* (swarm craft).
- **Core mechanic, Growth:** no shipyards. Biomass from defeated enemies mutates ships, replacing modules and evolving cards. Small craft spawn during combat.
- **Synapse:** units near the Cantor are buffed. Cut off or lose the Cantor and units go **feral** (random, cheaper, harder to control). Enemy hives share the weakness.
- **Voice:** events are written as "we". No portraits; the Chorus appears as a pulsing pattern of light.

## Run structure

A run is 3 sectors plus the Approach (the final). Each sector is a branching node map ending in a boss, and every node you visit moves the rival fleets forward.

### Map nodes

- Battle · Elite · Nemesis (when one is hunting you)
- Event / Derelict · Shipyard (repair, refit, rebuild) · Black Market · Relic Site
- Hazard: nebula, ion storm or minefield, as a battle modifier or an event
- Faction nodes: Ascendancy *Harvest Colony*, Concord *Council Session*, Chorus *Spawning Ground*

Each faction has its own sector sequence; shared sectors are where rivals collide.

### Resources

| Resource | Use |
| --- | --- |
| Scrap | Currency |
| Fuel | Moves and shortcuts |
| Supplies | Repairs |
| Crew | Ordinals (Ascendancy) · officers (Concord) · biomass (Chorus) |
| Favor | Edicts (Ascendancy) · Council approval (Concord) |

### Race track

A track along the top of the map shows your position and both rivals'. Rivals move at a base rate per node, plus or minus events.

- **Shortcuts:** risky jump lanes that cost fuel or hull, or carry hazards.
- **Sabotage:** events or cards that delay rivals. Rivals sabotage you too: ambushes, mines and blockades when they're close.
- **Interception:** beating a rival fleet slows it; losing or fleeing speeds it up.
- **Finale:** arrive first and you face the Silence's defenses before rivals arrive. Arrive second and the rival is dug in and waking it: harder, with an extra phase. Lose the run and the epilogue shows the race leader reaching it.

### Nemesis commanders

- Each rival faction fields 3–4 named commanders with traits, tactics and dialogue.
- A commander who survives a fight with you **returns stronger**: adapted to your fleet (e.g. +3 shields against Lance cards), with a grudge meter (0–3) and **visible marks** — scars, implants, refits on portrait and ship.
- Killing one gives a trophy relic; some can be spared or captured. Carry-over between runs is a stretch goal.

### Pre-battle hail

Before a nemesis battle the commander speaks 3–4 lines about your shared history, then you pick one of 3–4 replies, each tagged with its effect. Replies depend on your faction. A dossier shows history, adaptations, scars and field intel.

Example: Ascendancy vs Concord nemesis Marshal Oru Venn.

| Reply | Tag | Effect |
| --- | --- | --- |
| "By decree of the Prime: power down and be catalogued." | Edict | Venn is Enraged: his ships deal +2 damage, but every intent targets your flagship. Edict progress. |
| "Open fire." | First strike | +1 command on turn 1. Grudge rises to 3/3; if he escapes he returns as rank III. |
| Board their relay and cut the channel. | Expend 3 Ordinals | His gunship wing starts the battle feral. |
| Say nothing. | Silence | No effect. |

## Combat rules

Combat runs on a 9 × 7 pointy-top hex grid. Capital ships are played through cards and drift by momentum; small craft are moved by hand.

### Battlefield

- **Terrain:** asteroids block movement and fire; debris blocks movement only; nebulae block sensors and weaken shields; gravity wells pull drift.
- **Modifiers** come from hazards, e.g. an ion storm (shields regenerate 1 less).

### Units

|  | Capital ships (max 4) | Small craft (max \~8) |
| --- | --- | --- |
| Control | Through cards | Moved directly each turn |
| Facing | 6 hex facings; shields and armor per arc (Fore / Port / Starboard / Aft) | None |
| Movement | Drifts straight ahead by Speed (0–3) each turn. The helm changes a ship's Speed by one step once a turn, for free (speeding up adds 1 Heat). Cards turn the ship or shove it a hex | Free move up to Speed |
| Attacks | Weapon cards; target must be in the card's arc and range | Automatic attack on an adjacent target after moving |
| Source | Bought, found, or rebuilt (flagship only) | Hangar modules; losses carry over between battles |

Arcs: Fore is the front side, Port and Starboard are the two flank sides each, Aft is the rear side. An attack hits the arc facing the attacker; on a boundary, the defender chooses.

### Turn sequence

1. **Intent:** enemy actions, targets and arcs are revealed and drawn on the board; drift paths are previewed.
2. **Command:** draw up to 5 cards and gain 3 command. Play cards and move small craft in any order.
3. **Drift:** every capital ship moves straight ahead by Speed. Entering an occupied hex is a collision: both take the mover's Speed × 2 damage and the mover stops.
4. **Enemy action:** intents resolve in order. An attack misses if you left its arc or range.
5. **End:** shields regenerate and ships vent heat, the heat check runs, junk in hand triggers, the hand is discarded, unspent command is lost.

### Damage

1. Shields on the hit arc absorb first (each arc regenerates separately).
2. Armor on that arc reduces the rest, once per attack.
3. What remains hits Hull.
4. **Critical hit:** 5 or more hull damage in one attack hits the module under that arc (see Damage, junk and repair).
5. At 0 hull the ship is destroyed and **all its cards leave** the deck, hand and discard.

### Heat

Weapons and overcharges add heat; venting and cooling remove it. After venting: heat above cap adds *Fire on Deck*; heat at cap + 4 or more causes **Reactor Strain** (6 hull damage ignoring shields and armor, heat resets to cap).

### Flagship rule

- If the flagship dies, the battle continues.
- Win with at least one capital ship left and you may **rebuild it** at a high cost (scrap, crew, and lost modules, max-hull or a relic; to be tuned). The Ascendancy pays in Ordinals, the Concord's Council votes, the Chorus regrows the Cantor from a seed.
- Lose the flagship and every other capital ship and the run ends. Other lost capital ships are gone; shipyards rarely sell new hulls.

### The deck

One **shared command deck** for the fleet; each card is tagged to its ship and module and can't be played if that ship is gone or the module is offline. Cards come from the flagship's bridge, modules (1–3 each), faction rewards (Edicts, Council, mutations), officers, Wayminds and mutations; relics change rules passively.

## Ships, modules and tiers

Each flagship has 4 slots with 3 module choices each, and every module has 3 tiers (Mk I–III; Stage I–III for the Chorus). A tier visibly adds parts to the ship and raises the numbers on that module's cards: `a/b/c` below means the value at tier I/II/III. Each faction gets 5 hulls at launch (escort, frigate, cruiser, battleship, carrier); shared synergy tags (*Lance*, *Swarm*, *Ordnance*, *Heat*) let relics and officers build archetypes.

### Ascendancy: lance-cruiser "Sixth Verdict"

Base: Hull 30 · Fore armor 3 · Shields 4 · Heat cap 10 · Speed 3 · Command 3. Hull cards: Burn (0) move 1 hex ahead now, +1 Heat · Hard About (1) turn 60° (twice at speed 0–1) · Brace (1) +4 shields every arc.

| Slot | Module | Stats | Cards (cost) | Tier visuals |
| --- | --- | --- | --- | --- |
| Prow | Spinal Lance | Heat cap +2 | Lance Volley ×2 (2): 9/12/15 dmg, fore, range 5, +3 Heat; Expend 3 Ordinals: +6, ignore shields | More rings, longer barrel · twin capacitor pods |
| Prow | Ram Prow | Armor +3, Speed −1 | Ramming Speed (2): Speed +2, collision deals speed ×5/6/8, take half · Prow First (1): face nearest enemy, +3/4/5 fore armor | Edge teeth · flanking spurs |
| Prow | Torpedo Tubes | — | Torpedo Salvo ×2 (1): 2/3/3 torpedoes, 6/6/8 each | Reload racks · 5 tubes |
| Broadside | Macro Batteries | — | Broadside Battery ×2 (1): 4/5/6 dmg, side arc, range 3, +1 Heat | Mantlets, longer barrels · 5th turret |
| Broadside | Beam Galleries | Heat cap −2 | Raking Beam (2): 3/4/5 to every enemy in a line from a side arc, range 5, +3 Heat | Wider galleries · focusing domes |
| Broadside | Flak Arrays | Shields +1 | Flak Lattice (1): destroy 3/4/5 adjacent small craft or torpedoes · Screening Fire (0): next 1/1/2 torpedoes destroyed | Second dome row · gold tracking fins |
| Dorsal | Command Spire | Command +1 | Precision Edict (1): mark an enemy, hits deal +3/4/5 | Side spires · gilded crown |
| Dorsal | Waymind Crown | — | Waymind Targeting (1): next weapon ignores armor, crits · Scry the Void (0): see 2/2/3 intents, draw 1/2/2 | Second dish · listening array |
| Dorsal | Radiator Stack | Heat cap +4 | Vent Plasma (0): −5/7/9 Heat, Exhaust · Overcharge (1): next weapon +5/6/8, +3 Heat | More fins · longer fins |
| Aft | Dart Hangar | — | Launch Darts (1): 2/3/3 darts · Dart Sortie (1): darts move +2/2/3, attack twice | More darts · launch rails |
| Aft | Boarding Pods | — | Boarding Action (2): Expend 4/3/3 Ordinals, enemy within 2 loses its intent · Purge Their Decks (1): Expend 2, 6/8/10 hull dmg adjacent | Extra pod per side · larger pods |
| Aft | Armored Plating | Hull +8, Speed −1 | Hold the Line (1): Brace 2/2/3 ships | Forward layer · gilded edges, spikes |

### Concord: trimaran carrier "Long Promise"

Base: Hull 36 · Armor 2 · Shields 3 · Heat cap 8 · Speed 2 · Command 3. Hull cards: Burn (0) · Hard About (1) · Patch Job (1) repair 4 hull, 7 with an engineer.

| Slot | Module | Stats | Cards (cost) | Tier visuals |
| --- | --- | --- | --- | --- |
| Bow | Survey Prow | — | Long Scan (0): reveal all intents, draw 1/1/2 · Spotter Data (1): next weapon +1 range, +2/3/4 | Side booms · antenna array |
| Bow | Mass Driver | Heat cap −1 | Mass Driver ×2 (2): 10/12/14 dmg, fore, range 6, push 1/1/2 | Longer rails · capacitor pack |
| Bow | Grapple Bow | Armor +1 | Grapple (1): pull enemy within 2/2/3 adjacent, no drift · Tow Line (1): move ally 2/3/3 hexes, turn it | Serrated jaws · outrigger harpoons |
| Drum | Habitat Drum | — | Rotate Crews (1): move an officer, draw 2/2/3 · Council Vote (0): draw 2/2/3 or +1 command next turn | Second drum · docking pods |
| Drum | Shield Projector | Shields +3 | Ring Shield (1): +6/8/10 shields every arc | Inner field · emitter vanes |
| Drum | Gun Ring | — | Ring Barrage (2): 3/4/5 to each enemy within 3, any arc | Third barrel · two more turrets |
| Outriggers | Cargo Pods | Hull +4 | Jury-Rig (0): exhaust a junk card, +1/1/2 command · Dump Cargo (0): Speed +2/2/3, Exhaust | Second row · cranes |
| Outriggers | Missile Racks | Heat cap +1 | Ripple Salvo (2): 4/5/6 missiles of 3, split freely | More cells · heavy pods |
| Outriggers | Flight Decks | — | Launch Wing (1): 3/4/4 mixed craft · Combat Patrol (1): craft intercept first 1/1/2 attacks | More craft · control towers |
| Aft | Drone Bay | — | Patch Crews (1): repair 3/4/5 hull on every ship | More drones · heavy lifter |
| Aft | Salvage Plating | Hull +10, Speed −1 | Weld It Shut (1): exhaust a Hull Breach, +3/4/5 armor | More layers · welded spikes |
| Aft | Cutting Beam | Heat cap −1 | Cutting Beam (1): 7/9/11 to adjacent enemy, ignores armor | Second arm · heavy emitter |

### Chorus: command organism "The First Cantor"

Base: Biomass 34 · Carapace 2 · Regrowth 0 · Strain cap 8 · Speed 2 · Synapse 3. Body cards: Coil (0) turn 60°, Speed −1 · Regrow (1) +4 biomass (6 with consumed biomass) · Hymn (1) units within 2 of the Cantor deal +1.

| Slot | Module | Stats | Cards (cost) | Stage visuals |
| --- | --- | --- | --- | --- |
| Maw | Acid Maw | — | Corrosive Spit ×2 (1): 4/5/6 dmg, fore, target loses 2/2/3 armor permanently | Longer jaws · inner mandibles |
| Maw | Spine Lance | Armor +1 | Impale (2): 11/13/16 dmg, fore, range 2, target can't drift | Barbs · flanking spears |
| Maw | Brood Maw | Biomass −4 | Disgorge (1): spawn 3/4/5 Motes · Swallow (1): destroy adjacent craft, +2/3/4 biomass | More motes · ring of teeth |
| Crown | Synapse Crown | Synapse +1 | Command Pulse (1): every Chorus unit acts again, Motes +0/+1/+2 dmg | More spires · longer spires |
| Crown | Bone Spines | Carapace +1 | Spine Volley (1): 2 dmg 4/5/6 times at random enemies within 4 | More spines · second row |
| Crown | Gas Bladders | Regrowth +2 | Spore Cloud (1): fog the 3/4/6 hexes around the Cantor, no targeting through it | Fourth sac · larger sacs |
| Flanks | Brood Sacs | — | Hatch (0): spawn 2/2/3 Motes · Brood Surge (1): spawn 2/3/3; Feral 3/4/5 uncontrolled | More sacs |
| Flanks | Chitin Plates | Carapace +2, Speed −1 | Harden (1): +5/6/8 armor every arc | Second layer · spikes |
| Flanks | Grasping Tendrils | — | Grasp (1): adjacent enemy can't move or turn, 2/3/4 dmg per turn held | More, longer tendrils |
| Tail | Trailing Tendrils | — | Taste the Void (0): see aft-arc intents, draw 1/1/2 | More tendrils · glowing tips |
| Tail | Propulsion Fins | Speed +1 | Lunge (1): move 3/3/4 hexes any direction, keep facing | Extra fin pair · glowing vents |
| Tail | Stinger | — | Tail Strike (1): 6/7/9 dmg, aft arc, Poison 2/3/3 | Barbs · twin stinger |

## Cards, damage and progression

A module's condition changes both the ship's art and the deck: damaged modules cost more to use, offline ones stop contributing cards and add junk.

### Card anatomy

| Field | Notes |
| --- | --- |
| Cost | Command 0–3; +1 while the module is damaged |
| Name | Short, evocative |
| Art | A crop of the source module from the ship art |
| Text | Rules with key numbers highlighted and tier values filled in |
| Tags | Type · archetype (e.g. Weapon · Lance) |
| Source | Ship · slot · module |
| Badges | Tier, copies (×2), state stamp (Damaged / Offline) |

Keywords: Exhaust (gone for the battle), Expend X Ordinals, Feral, Poison, Intercept. Types: Weapon, Maneuver, Defense, Sensor, Reactor, Hangar/Swarm, Boarding, Order/Edict, Council/Officer, Growth/Synapse, Repair, Control, Junk.

### Module condition

| Condition | On the ship | Cards | Stats |
| --- | --- | --- | --- |
| Intact | Normal | Normal | Normal |
| Damaged | Scorch, glowing breaches, smoke, fire on that module | Its cards cost +1; add 1 fire-type junk | Hull −4 |
| Offline | Module dark, lights off | Its cards leave the deck; add fire-type and breach-type junk | Module stats lost, Hull −10 |

### Junk cards

| Faction | Fire-type | Breach-type |
| --- | --- | --- |
| Ascendancy | Fire on Deck: in hand at end of turn, +2 Heat; pay 1 to put it out (Exhaust) | Hull Breach: unplayable; in hand at end of turn, lose 2 Ordinals |
| Concord | Fire on Deck: +2 Heat and an officer is wounded; pay 1 to put it out | Hull Breach: unplayable; lose 1 crew, Council approval −1 |
| Chorus | Ichor Bleed: lose 2 biomass; pay 1 to seal (Exhaust) | Severed Nerve: unplayable; while in hand, Synapse −1 |

Also: *Jammed Mount* (a weapon in hand costs +1), *Reactor Breach* from heat, and junk added by the Inquisitor.

**Repair:** shipyards (scrap + supplies), repair cards, Concord drones and engineers, Chorus regrowth. Small-craft losses carry over; shipyards sell cheap replacements and the Chorus regrows craft from biomass.

### Meta-progression and difficulty

- **No permanent power** (Slay the Spire model): unlocks widen the pools of hulls, modules, relics, events, officers and mutations.
- **Factions:** Ascendancy and Concord from the start; the Chorus after the first win (or on reaching Sector 3; to be tuned).
- **Threat Level 1–20** per faction, each adding a modifier: faster rivals, stronger nemeses, extra junk, costlier rebuilds.
- A **codex** of lore fills as you play. Seeded and daily runs are a stretch goal.

## Art direction and UI

The art is high-fidelity pixel art seen from directly above, with every hex facing hand-drawn rather than rotated; mirroring means a symmetric ship needs only 2 drawn facings.

### Style

- High-fidelity pixel art, top-down: crisp dark 1px outline, a few shades per material, no anti-aliasing.
- **Hex facings are drawn, not rotated:** 6 facings at 60° steps (E, NE, NW, W, SW, SE).
- **Mirroring cuts the work:** draw E, NE and SE (the diagonals by an exact 60° RotSprite rotation of E, then cleaned up). W, SW and NW are those three turned 180°, which is pixel-exact and keeps asymmetric ships correct (the Concord's faceted outrigger stays on the same side). Symmetric ships can draw only 2, since SE is NE flipped. Confirmed in art tests 01–02. PixelLab's own rotation tool turns only \~20° for its diagonal, so it is not used for facings.
- **Lighting baked from directly overhead** so flipped sprites stay correct. Optional pixel normal maps for Unity 2D lights; test early that flipped sprites light correctly.
- URP Pixel Perfect Camera with integer scaling. The refit bay shows the same sprites at a larger integer zoom; sprite sizes are set in the art test.
- PixelLab (once signed in) for fast placeholder sprites and direction variants; final art commissioned.

### Modules as layers

- Each hull has a socket per slot for every facing; each module is a sprite per tier per drawn facing that snaps onto it.
- **Workload per module:** about 6 sprites for a symmetric ship (2 facings × 3 tiers), more for asymmetric ones.
- Damage is shared overlay sprites per faction (scorch, glowing breaches, small animated fire and smoke) placed at the hit module's socket; offline modules switch off their glowing pixels.

### Faction looks

| Faction | Silhouette | Palette | Light | Damage |
| --- | --- | --- | --- | --- |
| Ascendancy | Long knife-like hulls, prow-first, stepped cathedral decks, perfect symmetry | White ceramic #eef1f6, cold grey #a2aaba, gold seams #c89e40 | Icy cyan #7fdcf2 | Orange fire, black scorch |
| Concord | Trimaran: central hull and two mismatched outriggers (one angular, one rounded) on copper struts | Navy #26375e, ivory #ebe0c6, copper #d0864a; sigil of seven linked rings | Warm amber #ffc46a | Flame and sparks |
| Chorus | Segmented organism, clear size ranks (Cantor > Chorister > Mote) | Violet #7e3c8c / #43185a, bone #c4b698 | Teal synapse #5fffc8; feral pink #ff5a9a | Pink ichor, no fire |

The Concord is the only faction whose ships look lived in and hopeful: different species build different parts, all in one livery.

### Portraits

Pixel-art busts on a comms screen with scan lines, a slight glitch, a color cast and a vignette. Nemeses wear their history. Example: Marshal Oru Venn, a grey-blue alien with a translucent teal crest in Concord navy and ivory, with a burn scar and a copper-ringed red optic where your lance took his right eye. The Chorus has no portraits, only a pulsing pattern of light.

### Interface

- **Plates:** etched gunmetal (#2c2f36 to #16171b) with noise and grime, 12px chamfered corners, a rivet in each corner. **Wells:** recessed dark panels for data.
- **Brass** (#c9a24a) marks anything interactive; the Concord tints it #e0a05a and the Chorus #5fd8b0. Good #8cc47a, bad #d9573f, warning #f0a040.
- **Buttons:** chamfered steel; selected = brass with dark text; damaged = red; offline = grey.
- **Cards:** etched plate, brass hexagonal cost badge, condensed caps name, 16:9 art crop of the source module, stencil footer. Variants: fresh (brass outline), damaged (red tint and stamp), offline (greyscale, out of deck), junk (rust tint).
- **Bars:** hull green, shields per arc cyan, heat as segmented cells with a cap marker. **Intent panels:** dark well with a red (attack) or orange (torpedo/delayed) left edge.

| Role | Font (Google Fonts; check licenses before shipping) |
| --- | --- |
| Logo, story moments | Grenze Gotisch 800 |
| Headings, ship and card names | Saira Extra Condensed 700, uppercase |
| Labels, stamps | Saira Stencil One, letter-spaced |
| Rules and body | Barlow Semi Condensed 400–600 |
| Numbers | Share Tech Mono |

### Screens

- **Refit bay:** large top-down ship in drydock with facing controls, a stats row, 2×2 slot panels (module, tier, condition), and the command deck rebuilt live below.
- **Pre-battle hail:** portrait with nameplate, rank, grudge and traits; transcript; tagged replies; an "effect on this battle" box; dossier and race track.
- **Battle screen:** top bar (turn, command pips, pile counts, faction resource); left, the selected ship's hull, shields per arc, heat, speed, facing and modules; center, the hex board with every intent drawn and labeled and faded drift ghosts; right, commander, intents in resolve order and the log; bottom, the hand and End Turn. Selecting a card highlights its arc or effect and a line says exactly what it would do.
- **Star map:** branching node map with the race track along the top (not yet mocked up).

Readability rules: every enemy intent is visible on the board and listed in order; players never count hexes; state shows in form as well as color (stamps, outlines, glow on or off).

## Audio and technical plan

The game is built in Unity 6 LTS with the combat rules in plain, deterministic C#, separate from the visuals, and all content defined as data.

### Audio (proposal, not yet discussed)

Heavy low-end ship sounds with a signature per faction: an Ascendancy choral hum, a warm Concord mechanical rumble, wet organic Chorus pulses. Hails use radio filtering and glitches. Sparse dark ambient on the map; percussion builds in battle.

### Stack

- **URP 2D Renderer with Pixel Perfect Camera; optional 2D lights with pixel normal maps; Sprite Renderer **
- **Combat core in plain C#** (no MonoBehaviours), seeded and deterministic. The view layer listens to its events. This enables unit tests, replays, balance simulation and later daily runs.
- Content as **ScriptableObjects**; text in TextMeshPro; events and hails in Ink or Yarn Spinner.
- Saves: JSON run state at each map node; mid-battle save later.
- Steamworks.NET (achievements, cloud saves) near the demo; Git + Git LFS.

### Data model

```
FactionDef    id, name, accent, tier names, core resource, junk cards, hail replies
HullDef       id, faction, sprite + normal map, base stats (hull, armor/shields per arc,
              heat cap, vent, max speed, command), slots [SlotDef], base cards [CardDef]
SlotDef       id, label, socket position, allowed modules, card-art crop, damage anchors
ModuleDef     id, slot type, name, description, stat modifiers, cards, tiers [TierDef x3],
              layer (under/over hull)
TierDef       sprite, normal map, emissive mask, card value overrides
CardDef       id, name, cost, type, tags, text template, effects [EffectDef], exhaust,
              extra costs (Expend X), target rule (arc, range, ally/enemy)
EffectDef     Damage, Heal, Shield, Heat, Move, Turn, Speed, Spawn, Draw, GainCommand,
              ApplyStatus, AddCardToDeck, RemoveIntent, Push, Pull ...
EnemyDef      hull, AI profile, intent cycle [IntentDef]
IntentDef     action, targeting rule, preferred arc, value
CommanderDef  name, faction, portrait layers, traits, adaptations, hail lines
RelicDef, EventDef, SectorDef, NodeDef
```

### Runtime state

```
RunState      seed, faction, sector, node, race positions, resources, fleet, deck,
              relics, nemesis states, favor/approval, threat level
ShipState     hull, current hull, shields per arc, heat, speed, facing, hex,
              modules [def, tier, condition], crew/officers
CardInstance  def, source ship, source module, tier, damaged flag
BattleState   grid, units, intents, draw/discard/exhaust, command, turn, phase, log
```

### Project structure

```
Assets/_Project/
  Scripts/Core/   pure C#: combat, deck, rules, RNG, AI
  Scripts/Data/   ScriptableObject definitions
  Scripts/View/   board, ship rigs, cards, UI
  Scripts/Meta/   run, map, events, saves
  Data/           Factions, Hulls, Modules, Cards, Enemies, Commanders, Relics, Events
  Art/            Ships/<Faction>/<Hull>, Modules/<Faction>/<Module>/T1-T3,
                  Decals, Portraits, UI
Assets/Tests/EditMode/   rule tests, balance sims
```

### Rendering notes

- A ship is a root object: hull sprite, module sprites at sockets, a decal layer and an emissive layer. Changing facing swaps in that facing's sprites (flipped where mirrored) instead of rotating.
- Card art: a runtime camera crop of the module region, or baked.
- Drift ghosts: translucent copies at the predicted hex. Intent lines: dashed line renderers with arrowheads and screen-space label chips.

## Scope, milestones and decisions

The next step is the paper prototype (M0); nothing goes into Unity until combat proves fun on paper.

### Content targets (1.0, rough)

- **Per faction:** 5 hulls, 4 small-craft types, 12+ modules per hull family (×3 tiers), \~70 faction cards, 10 faction relics, 15 unique events, 4 nemesis commanders.
- **Neutral:** \~40 cards, \~30 relics, \~40 events, 10 hazard types, pirate and derelict enemy sets.
- **Bosses:** 3 sector bosses per route and 1 final encounter with variants by race outcome.

### Milestones

| # | Milestone | Exit test |
| --- | --- | --- |
| M0 | Paper prototype (separate doc) | Turn 3 is still interesting; facing, drift and heat all change decisions |
| M1 | Grey-box combat in Unity: Ascendancy, 2 hulls, \~20 cards, 3 enemy types | Combat is fun with placeholder shapes |
| M2 | Run loop: 1 sector, rewards, refits, 5 events, 1 boss, save/load | A 20-minute run feels complete |
| M3 | Race, nemesis, Concord (officers, Council) | The race changes routing; a nemesis return feels personal |
| M4 | Vertical slice: Sector 1 for 2 factions, test art and audio, Steam page | Wishlists start |
| M5 | Steam Next Fest demo | Player feedback |
| M6 | Content: 3 sectors + Approach, Chorus, Threat Levels, endings | Feature complete |
| M7 | Early Access / 1.0 | Polish, balance, achievements, cloud saves |

### Decisions log

- Combat: deck-building core with light positioning (facing, momentum, hex grid) and heat.
- Fleet: up to 4 capital ships and \~8 small craft; capital ships deeply customizable.
- One shared deck, cards tied to their ship and module.
- Small craft moved by hand; losses carry over.
- Flagship rebuildable only after a win with a capital ship left, at a high cost.
- Enemies: mostly the two rival factions, plus pirates and hazards; named nemeses.
- Visible race track; endings: faction wins, rival wins, hidden true ending.
- Ascendancy: sleek and ruthless; Edicts mandatory. Chorus: sentient collective, unlocked after the first win.
- Meta: no permanent power, plus difficulty tiers.
- Modules: 3 visible tiers; per-module damage states with junk cards.
- Pre-battle hails with tagged replies; nemeses show scars and refits.
- Art: high-fidelity pixel art, top-down, hand-drawn hex facings (2 drawn + mirrors for symmetric ships). Illustrated 2D was tried and dropped.
- Concord redesigned as navy, ivory and copper trimarans (replaced the rust patchwork).
- UI: one shared etched-gunmetal interface with faction-tinted accents.
- Unity, Steam, solo developer, commissioned art.
- Momentum: every capital ship has a helm (Speed up or down one step a turn, free; +1 Heat to speed up). Without a brake, ships drifted into rocks and enemies about eight times a battle.
- Heat is a resource to spend: the flagship's cap is 6 (8 with the Radiator Stack) and she vents 1 a turn. Redline trades heat for command; Thermal Purge turns heat into damage.
- Enemy repair ships mend another ship in full and themselves for half.
- Shipyards can strike any card from the deck (30 scrap, floor of 8 cards).
- Race: two jump lanes per sector skip a node for 4 hull a ship; while the Concord leads by a node it mines the ordinary lanes (2 hull a ship per jump); shipyards can seed false beacons once a visit (25 scrap, both rivals lose 0.8 node).
- Nemesis: one of three Concord commanders hunts each run from a battle node. Below rank III the ship breaks off at 40 percent hull once the enemy has acted, so killing it takes one turn of burst. A survivor comes back a rank higher (+6 hull, more escorts), refitted against whatever hurt it most, with a scar named for it. Slipping past costs 0.3 node. With no lane left the commander falls back to the main fleet (they do not join the gate boss: tried, and it made the gate unwinnable).
- Hail replies are four fixed answers for the Ascendancy: Edict (enemy +2 damage, all aimed at the flagship, salvage x1.5), Open fire (+1 command on turn 1, grudge to 3), Board the relay (4 Ordinals, escorts idle for 2 turns), Say nothing.
- Relics are passive numbers. Elites carry one from a pool of seven; each commander's wreck gives a trophy.
- The Concord is playable (Long Promise and an Outrider, four gunships, no Expend cards). Officers are passive bonuses that a critical hit on the flagship wounds; the Council is an approval meter (0 to 6) earned by clean wins and spent on six motions between jumps. On its route the enemy is the Ascendancy, with its own boss (High Inquisitor Calix Mor) and three commanders.
- Hazards: a third of the fights past the opening are in an ion storm (no shield regeneration), a nebula (all weapons reach 1 less) or a dense field (more rock), and pay 1.3 times the salvage. Not at the gate or in a nemesis fight.
- Elites offer two relics and one is taken. A sector draws five of the nine written events (all nine made events crowd out fights, and the bot's win rate rose to 67 percent).
- Story is told where the player already looks: a briefing before the first jump, an aftermath line with each fight's salvage, an epitaph for each commander, and events.
- A commander who has met you before can be taunted: they do not break off and hit 1 harder. Commanders with a scar show a scarred portrait.
- Music is four synthesized loops (menu, map, battle, hunt), cross-faded by screen; volume is in Settings.
- A run is two sectors (the Ion Reef, then the Kessane Drift); `sectors` in the run file sets how many. Between sectors the fleet is fully mended and takes one of three relics; the race carries over as a lead of at most 1.5 nodes; a surviving commander follows, a dead one is replaced. Enemy capital ships have 12 percent more hull per sector past the first; gate fights are written per sector and not scaled.
- Each faction's run is its own set of content files; rules that differ between factions are data in the `faction` block, not branches in code.

### Open questions

- [ ] Final names: game, factions, the Silence, Antecessors, Lumen, the Prime, commanders
- [ ] Grid size and unit count (test in M0/M1)
- [ ] Exact flagship rebuild cost
- [ ] How hard the race punishes exploring (tune in M3)
- [ ] How much nemeses carry over between runs
- [ ] Whether hand-moved small craft stays, or becomes wing orders if turns drag
- [ ] Concord officer roster and Council blocs
- [ ] Audio direction
- [ ] The true ending's exact reveal

## Appendix: flagship prompts and glossary

### Flagship image prompts

Add this suffix to each prompt so all three match:

```
Orthographic top-down, ship pointing right, centered, full ship visible, no perspective, no text, no background scenery, pixel art, limited palette, no anti-aliasing, lit from directly overhead, transparent background. Also render the same ship facing north-east (60°).
```

**Ascendancy, "Sixth Verdict"**

```
Top-down view of a grimdark sci-fi capital warship, high-detail pixel art game sprite, crisp dark outline, on a dark background. Long knife-like hull in pristine white ceramic with thin gold seams, stepped cathedral-like decks along the spine, octagonal command spire at the center. Spinal lance cannon with gold capacitor rings at the prow, turret sponsons along both flanks, icy cyan engine glow at the stern. Perfect symmetry, sterile and ruthless. Light from upper left, soft drop shadow, subtle grime.
```

**Concord, "Long Promise"**

```
Top-down view of a sci-fi trimaran starship, high-detail pixel art game sprite, crisp dark outline, on a dark background. Navy-blue central hull with ivory deck plating and copper trim, a large circular habitat drum at its center. Two outrigger hulls joined by copper struts: one angular and faceted, one smooth and rounded, built by different alien species but painted in the same navy-and-ivory livery. Glass canopy at the bow, rows of warm amber windows, amber engine glow, a small emblem of seven linked rings. Lived-in, hopeful, worn but cared for. Light from upper left, soft drop shadow, light grime.
```

**Chorus, "The First Cantor"**

```
Top-down view of a biological alien hive-ship, high-detail pixel art game sprite, crisp dark outline, on a dark background. Glossy violet segmented body like a giant space crustacean, overlapping carapace scales, a crown of pale bone spikes radiating from the back with glowing teal tips. A glowing teal nerve line runs down the spine with branching veins. Bone mandibles at the head, a cluster of teal eyes, long trailing tendrils at the tail. Alien, unified, menacing. Light from upper left, soft drop shadow, wet sheen.
```

For a module parts sheet, add: *separate parts sheet: hull, prow weapon, flank weapons, dorsal module, aft module, each isolated on transparent background*.

### Glossary

| Term | Meaning |
| --- | --- |
| Arc | A ship side (Fore, Port, Starboard, Aft) for firing and defense |
| Command | Points spent to play cards each turn (base 3) |
| Drift | Automatic straight-ahead movement by Speed each turn |
| Exhaust | The card is removed for the rest of the battle |
| Expend X Ordinals | Ascendancy extra cost paid in crew |
| Feral | Chorus units cut off from synapse: random and uncontrolled |
| Intent | An enemy's announced next action |
| Junk | Unwanted cards added by damage or heat |
| Module | A swappable ship part in a slot; adds cards and stats |
| Nemesis | A named enemy commander who remembers you |
| Tier | Module upgrade level (Mk I–III / Stage I–III) |
