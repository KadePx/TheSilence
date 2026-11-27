# The Silence: Alpha Report

Nov 26, 2026 · @Kade

## Summary

The alpha is playable to milestone M2. From the `Menu` scene you can start a seeded run, cross one sector's
branching map, fight battles with cards, facing, drift, heat and enemy intents, take salvage, refit at
shipyards, answer five written events, fight Marshal Venn at the gate, and quit and resume from the save.
132 EditMode tests pass. `main` holds both milestones.

**To play:** open `Assets/_Project/Scenes/Menu.unity` and press Play. Controls are in the README.

| | |
| --- | --- |
| Tests | 132 EditMode tests, all passing (87 before this work) |
| Stand-in bot, whole runs | 200 seeds: 32% win, 55% lost, 13% outpaced at the gate |
| PixelLab | 115.1 generations spent of the 1,000 cap (1,660.4 left on the account) |
| Not done | Module tiers, relics, fuel and supplies, nemesis returns, hail replies, music. See "Still rough" |

## What to look at first

1. **Balance is tuned against a weak bot, not a person.** The numbers below say the run is neither free nor
   hopeless for a greedy bot. Nobody has played it by hand yet. Expect it to be easier for you than for the bot.
2. **Battles run long for the bot** (10 to 24 turns against a 6 to 10 target). The bot steers badly; I would
   wait for your own turn counts before cutting enemy hull.
3. **The race rarely favours the bot** (Concord ahead at the gate in 48 of 64 bot wins). It loses time to
   long battles. If it is as lopsided for you, lower `concordRate` in `run.json`.

## Milestone M1: grey-box combat

Sprint tasks 8 to 10 of `Docs/UnitySetupPlan.md`, on top of the rules from tasks 1 to 7.

### Built

- **Rule fixes.** The Blade has its own helm card, Come About (0), so it can steer when the flagship is gone.
  The starter deck has two Hard About and is 12 cards.
- **Enemies that aim.** Enemy capital ships read an intent cycle from their unit template. Maneuver picks
  facing, move and speed for where both ships will be after the next drift, and ships trim course by one
  turn and one speed step after acting. Before this the bot won scenario 1 200 times out of 200 without
  losing a point of hull, because frigates drifted out of their own firing solutions.
- **Board view** (`Silence.View.BoardView`): hex grid over the Ion Reef background, ships by facing through
  `HexDirections.SpriteFor`, drift ghosts, intent lines (solid when on course to hit, faint when it would miss
  as things stand), an intent chip over each enemy, arc and range highlight for the selected card, hull bar
  and per-arc shield ticks on every capital ship.
- **Hand and HUD** (`BattleScreen`): play by click and target, heading arrows for turning cards, dart
  movement, command pips, piles, selected-ship panel (hull, shields and armor per arc, heat cells with cap
  marker, speed, heading, enemy's next steps), intents in resolve order, log, a prompt line that states the
  exact damage a shot would do, End Turn, and win, loss and withdrawal screens.
- **Skirmish**: both paper scenarios from the `Battle` scene or the menu, with damage, junk, Ordinals and
  dart losses carried from scenario 1 into scenario 2.
- **Look**: the spec's gunmetal plates, wells and brass are drawn in code (`Skin`); the five spec fonts are
  baked for TextMeshPro. All five are under the SIL Open Font License 1.1; each `OFL.txt` is beside its font
  in `Assets/_Project/Fonts/`.
- **Sound**: 17 synthesized effects from `Tools/make_sfx.py`.

### Screenshots

- [Battle, turn 1](../ArtSource/Screenshots/m1_01_battle.png)
- [Card selected: arc highlight, target ring, damage preview](../ArtSource/Screenshots/m1_02_targeting.png)
- [Choosing a heading for Hard About](../ArtSource/Screenshots/m1_03_heading.png)
- [Victory screen](../ArtSource/Screenshots/m1_04_victory.png)

### Balance: the two paper scenarios

300 seeds each, `SimpleBot`, full-health fleet, unlimited hangar.

| Scenario | Win | Loss | Withdrawal | Turns to win | Flagship hull left | Escort survives |
| --- | --- | --- | --- | --- | --- | --- |
| 1 Intercept at the Ring | 100% | 0% | 0% | 9.3 | 26.7 of 30 | 90% |
| 2 The Long Promise | 99% | 0% | 1% | 16.1 | 26.3 of 30 | 94% |

Before the changes: scenario 1 was 100% with 30.0 hull left; scenario 2 was 91% with 9% withdrawals (stalls).
Taken alone these are still easy. They are the opening fights; the pressure comes from damage carrying across a run.

### Numbers changed from the paper prototype

| What | Paper | Now | Why |
| --- | --- | --- | --- |
| Flagship shield regen | 2 | 1 | The paper's own knob for "arcs don't matter" |
| Frigate | hull 16, Broadside 5 | hull 18, Broadside 9 | 5 could not get through a fresh arc (4 shields + 2 armor) |
| Frigate cycle | Broadside, Maneuver, Torpedo | Maneuver, Broadside, Torpedo | The opening broadside was always out of range |
| Torpedo | 6 | 9 | Same reason as the broadside |
| Gunship | hull 2, 2 damage | hull 3, 3 damage | One dart hit killed them before they acted |
| Carrier | hull 40, shields 5 regen 2, Patch Up 6 | hull 32, shields 4 regen 1, Patch Up 4 | It out-repaired the bot: 16% stalls |
| Turn limit | 40 | 25 (boss 35) | Withdrawals should end a stall sooner |
| Hard About in starter | 1 | 2 | One turn card in ten made the lance nearly unusable |
| Sacrificial Ram | Destroys the target | Twice the escort's remaining hull, straight to hull | It one-shot the sector boss |

## Milestone M2: the run loop

### Built

- **Run rules in Core** (`Silence.Core.Run`): `RunState`, a seeded branching map of 8 layers plus the boss,
  travel, the race, battles built from the fleet and carried back into it, rewards, events, shipyard.
  They are pure like the battle rules, so `RunBot` can play whole runs headless.
- **Map** (`Map` scene): three entry fights, 2 to 4 nodes a layer, battle, elite, event and shipyard nodes,
  a yard always at the halfway layer and before the boss, each event at most once per sector, no layer that
  is all one kind. Hovering a node says what it is.
- **Race track**: you move one node a jump; the Concord and the Chorus move 0.8 to 1.0 and 0.7 to 0.9. Long
  battles (over 12 turns), withdrawals and each shipyard service cost time; some event choices buy it back.
  If the Concord is level or ahead when you reach the gate, Venn's guard gets an extra ship.
- **Carry-over**: hull, lost ships, junk cards, Ordinals and darts all persist. A flagship lost in a won
  battle is rebuilt for 8 Ordinals and 4 maximum hull, and returns at half hull. A lost Blade stays lost
  (with its cards) until a yard rebuilds it.
- **Salvage**: scrap, 3 Ordinals, and one card of three (four after an elite), or field repairs of 5 hull
  per ship instead of a card.
- **Shipyard**: repair, strip junk, buy one of three cards, load darts, decant Ordinals, rebuild the escort,
  and refit one module per slot. A refit swaps that module's cards and stats (11 modules over the 4 slots).
- **Five events** with art and three choices each: The Silent Colony, Derelict in the Reef, The Waymind's
  Request, Edict of the Prime, The Refugee Convoy. The effect line under each choice is generated from its
  outcomes, so it cannot drift from the rules. One choice starts a battle; one enrages the boss.
- **Boss**: Marshal Oru Venn in the Long Promise, with a portrait and a hail. His Mass Driver (12, fore arc,
  range 6) is dodged by leaving his fore arc. Stalling at the gate past turn 35 ends the run as "Outpaced".
- **Save and load**: the run is written as JSON after every map step and loaded by Continue. A battle in
  progress is not saved: resuming restarts that node's fight, which is built from the same seed.
- **Menu and run over**: title key art, Continue, New run with a seed box, Skirmish; a run-over screen with
  an epilogue that depends on the result and on who led the race, stats and a score.
- **Data**: encounters (10), events, modules, the reward pool (19 cards) and every run number are JSON in
  `Assets/_Project/Data/Resources/Content/`. Cards and unit templates stay in code, because their effects
  and AI are code.
- **New enemies**: skiff (fast, fore guns), tender (repairs the line), and Venn's flagship.

### Screenshots

- [Main menu](../ArtSource/Screenshots/m2_01_menu.png)
- [Sector map and race track](../ArtSource/Screenshots/m2_02_map.png)
- [Pre-battle briefing](../ArtSource/Screenshots/m2_03_briefing.png)
- [Salvage: a card or field repairs](../ArtSource/Screenshots/m2_04_salvage.png)
- [Event: Edict of the Prime](../ArtSource/Screenshots/m2_05_event.png)
- [Shipyard](../ArtSource/Screenshots/m2_06_shipyard.png)
- [Venn's hail](../ArtSource/Screenshots/m2_07_boss_hail.png)
- [Boss battle](../ArtSource/Screenshots/m2_08_boss_battle.png)
- [Run over](../ArtSource/Screenshots/m2_09_run_over.png)

### Balance: whole runs

200 seeds, `RunBot` routing and `SimpleBot` fighting.

| Result | Runs | Share |
| --- | --- | --- |
| Won the gate | 64 | 32% |
| Fleet lost | 111 | 55% |
| Outpaced (stalled at the boss) | 25 | 13% |

Average run: 7.9 nodes, 3.8 battles won, 57 turns fought. On wins the flagship ends on 13.4 hull of 30 with
9.1 Ordinals, and the Blade was lost in 18 of 64. Losses by layer: 11, 8, 18 and 39 in layers 3 to 6, and
35 at the boss. The first version of the content gave the bot 1 win in 200.

| Encounter | Tier | Fights | Win | Loss | Withdraw | Turns | Flagship hull lost |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Outrider Scouts | opening | 145 | 100% | 0% | 0% | 5.2 | 0.0 |
| Reef Picket | opening | 128 | 95% | 0% | 4% | 6.6 | 1.2 |
| Skiff Wolfpack | standard | 125 | 92% | 2% | 4% | 10.6 | 3.7 |
| Intercept at the Ring | standard | 119 | 84% | 15% | 0% | 10.7 | 7.3 |
| Concord Gun Line | standard | 101 | 80% | 14% | 4% | 11.6 | 9.5 |
| Repair Convoy | standard | 120 | 74% | 9% | 16% | 15.1 | 7.2 |
| The Long Promise's Wake | elite | 37 | 54% | 37% | 8% | 17.3 | 11.8 |
| Hunter Squadron | elite | 38 | 47% | 39% | 13% | 17.5 | 16.5 |
| Marshal Venn at the Gate | boss | 124 | 51% | 28% | 20% | 23.5 | 19.3 |

Win rates here include fights entered already damaged, which is why standard fights lose 9 to 15%.

## Review against your four criteria

### 1. Gameplay balance

- Fixed: enemies could not hurt the flagship; the carrier stalled; Sacrificial Ram killed the boss outright;
  the first run content was unwinnable for the bot.
- Open: the Repair Convoy still stalls 16% of the time for the bot. Scrap piles up unspent (88 at the end of
  an average bot run), which may mean yards are too rare on its routes or too expensive to matter.
- Open: darts kill almost every torpedo. With 6 darts a run and 5 scrap each that may be fair, or may make
  torpedo frigates toothless. This needs a person.

### 2. Replayability

- A seed fixes the map, encounters, terrain scatter, rewards and shop stock. Route choice is real: layers
  never offer only one kind of node.
- Thin spots: 5 events means every run sees most of them; 10 encounters; one boss; one player faction and
  fleet. Module refits and the card-or-repair choice are the main build decisions. There are no relics.

### 3. Is the UI intuitive without explanation

- Every enemy intent is on the board (line and chip) and in the list. Hovering an enemy shows its full
  intent and next steps. Selecting a card lights its arc and range and rings legal targets. The prompt line
  gives the damage sum before you commit. A "Standing orders" box keeps the four core rules on screen.
- Known gaps: there is no tutorial; cards in hand do not say why they are dimmed until clicked; the map does
  not show which path you took apart from brass lanes; darts are selected by clicking them, which the
  prompt says but nothing else teaches.

### 4. Theme cohesion

- One gunmetal and brass interface across menu, map and battle, in the spec's fonts. Event text is written
  in a cold Ascendancy voice; event art, the shipyard backdrop and Venn's portrait were generated against
  the title key art for style.
- Off-theme: card art is the source ship's sprite rather than a module crop. The PixelLab card frame
  (`card_frame_base.png`) is not used: at 1x its text well is too small to read and 1.5x is not pixel-clean,
  so cards are drawn in code to match the plates.

## How it was verified

- **Tests**: 132 EditMode tests through the editor after each change (45 new: 17 for the alpha battle rules,
  1 for the ram, 27 for the run loop including save round-trip and a bot-run guard).
- **Played through the real screens**: an autopilot (F10) drove two complete runs, menu to run-over, through
  `MapScreen` and `BattleScreen` at speed, with no runtime errors or warnings in the console. Both ended in defeat
  at the boss.
- **Clicks**: buttons, cards, the board and End Turn were clicked through the real uGUI raycaster at screen
  positions (menu, skirmish dialog, a card, a ship, End Turn) and did the right thing. Card targeting,
  heading arrows and the shipyard services were driven through the same methods the mouse calls.
- **Save and load**: saved in a shipyard after a repair and a refit, stopped Play Mode, started again and
  continued: same seed, node, scrap, modules and deck.
- **Not verified**: nobody has played with a physical mouse. Hover effects (card lift, node tooltips, intent
  emphasis) were not exercised. No standalone build was made; it has only run in the editor at 16:9.
  Sound was generated and wired but never listened to.

## Assumptions I made

- **Run rules live in Core, not Meta**, so they stay deterministic and testable. Meta holds the session,
  the save file and the screens. Meta references View for the UI kit. `CLAUDE.md` is updated to say so.
- **Resources are trimmed to scrap, Ordinals and darts.** Fuel, supplies and favor are not in.
- **Module tiers are not in.** A slot holds one module; refitting swaps it.
- **Reward cards are not tied to modules**, only to a ship (as in the paper prototype).
- **Withdrawal** from a normal fight costs a node of time and pays nothing; at the boss it ends the run.
- **The camera** uses a fixed 1080-unit-tall framing instead of the Pixel Perfect Camera component, so the
  board lines up with the HUD at any window size. It is identical at 1080p and 4K.
- **Placeholder rules kept**: darts deal 2; heat at cap + 4 is Reactor Strain; collisions go through shields
  and armor; boundary arcs pick the stronger arc.

## PixelLab spend

115.1 generations this session against the 1,000 cap. Every job is in `ArtSource/pixellab-log.csv`.

| Asset | Generations |
| --- | --- |
| Outrider re-rolled as a two-hull catamaran (replaces the three-hulled placeholder) | 15 |
| Concord skiff, Concord tender | 20 |
| 16 map-node and resource icons, one call | 10 |
| 5 event scenes, shipyard backdrop | 60 |
| Marshal Venn portrait | 10 |
| Normal map for the new Outrider | 0.1 |
| Explosion, hull hit, shield flare (VFX workbench) | 0 |

Beams, shots and engine glow are drawn in code rather than from the workbench.

## Still rough

- Module tiers, relics, officers, Edicts as a system, hail replies and nemesis memory (all M3 or later).
- Only the Ascendancy is playable, with one fleet.
- The Chorus marker on the race track does nothing yet except change the epilogue.
- No music; sound effects are synthesized placeholders.
- No settings screen, no resolution options, no controller support.
- Animation is functional: ships slide and swap sprites; there is no engine flame or damage overlay.
- F9 (bot plays a turn) and F10 (autopilot) are left in for testing.
- There is no .NET SDK on this PC and `DotnetTests/` never existed, so tests run only through Unity. The README and `CLAUDE.md` no longer mention it.

## UI pass (after the alpha)

Kade's feedback on the alpha: the interface felt out of place next to the pixel art and sometimes showed too much.

### What changed

- **Pixel fonts and a pixel panel kit from PixelLab** replace the smooth Google fonts and the plates drawn in code:
  four fonts (heading, body, label, blackletter logo) and a kit of plates, wells, buttons, bars and chips, plus a
  larger card frame. 90 generations, all in `ArtSource/pixellab-log.csv`.
- **Whole-number scaling.** The alpha scaled everything by the window height, so at 2560x1440 the art was drawn at
  1.33x and landed on uneven pixels. The UI and camera now scale by 1, 2 or 3 only and letterbox the rest.
- **Battle screen:** the two full-height side panels are gone and the board has the full width. Each capital ship is
  one compact card (hull, heat, shields and armor per arc, speed, heading). Enemy orders are one short row each.
  The log and the rules are behind Log and Help buttons. Whatever the pointer is on shows in one small card.
- **Cards** use the PixelLab frame at 1:1 and are never scaled. Card art is a close crop of the part of the ship the
  card comes from, so a hand no longer shows five copies of the same ship.
- **Map:** the fleet wall is replaced by floating cards, the top bar and race track are slimmer, reachable nodes
  have a pixel hexagon that blinks instead of a smooth ring that swells.
- **Dialogs** (briefing, salvage, events, shipyard, deck, run over, skirmish, end of battle) are laid out again on
  the kit. The deck view shows cards at full size instead of shrunk.

### Screenshots

`ArtSource/Screenshots/ui_iter2_battle.png`, `ui_iter3_battle_busy.png`, `ui_btl_target.png`, `ui_btl_help.png`,
`ui_btl_end.png`, `ui_iter3_menu.png`, `ui_iter4_map.png` and `ui_dlg_*.png` (one per dialog).

### How it was checked

132 EditMode tests pass. Every screen and dialog was put on screen in Play Mode at 1920x1080 and captured. The
states were driven from script, not with a mouse, so hover (card lift, order rows, node tooltips) is still untested.

### Still open

- At 1440p the game is a 1080p picture with black bars. Fractional scaling would fill the screen but blur the art.
- The old Google fonts are still in `Assets/_Project/Fonts` but nothing uses them.
- Buttons brighten on hover by tint; painted hover and pressed states were not commissioned.
- The pixel fonts have no degree sign, so card text says "60 deg".

## Clarity pass and tutorial

Kade's feedback on the UI pass: better, but it is not clear when you can move and what you can do.

### What you can do, on screen

- The top bar says whose move it is: YOUR ORDERS, DRIFT, ENEMY ACTS.
- Bottom left lists what is still open this turn: how many cards you can afford, how many darts are ready, and that
  ships move by card and then drift. Under End Turn it says what is still unused, and End Turn blinks once nothing is.
- Each ship's drift ghost is labelled DRIFT with the distance. Darts that can still act are labelled READY.
- Clicking one of your own ships explains that ships are not moved by hand and names the cards in hand that move it.
- Hard About goes straight to the heading arrows when there is only one ship to turn.
- Card types are colour-coded in the footer: cyan moves, red shoots, green defends, orange is the reactor.

### Tutorial

Main menu > Tutorial, or the Tutorial button in the skirmish menu. One flagship against a moored Concord picket
(`Scenarios.Tutorial`), with an unshuffled deck so the first two hands are always the same. Thirteen steps:
enemy orders, drift, the lance (arc and range), Brace and shields, ending the turn, arcs on the ship card, Hard
About, the side guns, launching darts, moving a dart, heat, then two or three turns of free play to finish the
picket. While a step is up only the action it asks for is accepted; anything else gets a one-line nudge.

### How it was checked

137 EditMode tests pass, 5 of them new: the fixed first hand, the picket never moving, and every action the
tutorial asks for being legal and having the stated result (the lance leaves the picket on 6 hull, braced shields
take the whole broadside, either turn direction brings the side guns to bear). The tutorial was then played start
to finish in Play Mode through the same entry points the mouse uses, including two deliberately wrong inputs, with a
screenshot at every step (`ArtSource/Screenshots/tutorial_*.png`). It was not played with a physical mouse.

## Core review: easy to get into, room to go deep

A review of the core loop against two goals: a new player can get going quickly, and there is depth to find.
Method: read every card, module, encounter and rule; then 150 bot runs with each battle's event log mined for what
actually happens (`Tools/sim/balance_sim.cs`); then changes; then the same measurement again.

### What the first measurement showed

| Finding | Number | Reading |
| --- | --- | --- |
| The opening is gentle | 0% losses in the first three layers; 0.6 flagship hull lost in the first fight | Good. Keep. |
| Ships could not slow down | 12 collisions a battle, 8 of them your own ships hitting rocks and enemies | Speed only ever went up (Burn). This is the "when can I move" problem. |
| Burn was dead | played 1% of the times it was drawn | Speeding up with no brake is a trap. |
| Heat did not matter | 0.13 overheats a battle; Vent Plasma played 3%, Cold Discipline 4% | A whole system with no decisions in it. |
| Battles ran long | 12 turns on average, 23 for the boss, against a 6 to 10 target | Repair ships out-healed steady fire. |
| Scrap piled up | 89 unspent at the end of a run | Not enough worth buying. |

### What changed

Easier to get into:

- **A helm on every ship.** Minus and plus beside SPEED on each ship card change speed one step, once a turn, free
  (speeding up adds 1 heat). The drift marker updates at once. The tutorial has a new step for it.
- **Burn** now moves a ship one hex ahead immediately, so it is a positioning tool, not a speed setting.
- **Keyword glossary.** Hovering a card spells out the rules words on it (arc, range, heat, expend, exhaust...).
- **The tutorial is pointed out once** the first time New run is pressed. A second press skips it.

More depth:

- **Heat is a resource.** The flagship's heat cap is 6 (8 with the Radiator Stack) and she vents 1 a turn, down from 2.
  Sustained fire now overheats in three or four turns unless you ease off or vent.
- **Redline** (0): +1 command, +4 heat. **Thermal Purge** (1): dump all heat into an enemy within 2 as damage that
  ignores shields, up to 8. Running hot is now a plan, not only a mistake. Both are in the reward pool and the shops.
- **Strike a card** at a shipyard (30 scrap, floor of 8 cards). Deck thinning, and somewhere for scrap to go.
- **Repair ships** mend another ship in full but themselves for half, so killing the escort first or the tender
  first is a real choice, and a lone carrier can be worn down.

### The same measurement afterwards

| | Before | After |
| --- | --- | --- |
| Bot runs won / fleet lost / outpaced (150) | 48 / 84 / 18 | 73 / 45 / 32 |
| Collisions per battle | 12.0 | 2.0 |
| Average battle length | 12.1 turns | 11.3 turns |
| Overheats per battle | 0.13 | 0.40 |
| Vent Plasma played when drawn | 3% | 19% |
| Cold Discipline played when drawn | 4% | 14% |
| Losses in the first three layers | 0% | 0% |

The bot's results moved from mostly losing its fleet to mostly winning or running out of time. It is a weak player:
it brakes for obstacles but does not plan arcs, and it never took Redline or Thermal Purge, so those two cards are
tested by unit tests only.

### Still open

- **Battles are still long** (11 turns; the boss 20). The bot times out on 40% of carrier fights and 26% of boss
  fights. A person who strips shields with one ship and lands the lance with the other should do much better, but
  nobody has shown that yet. If your own fights drag, the first knobs are enemy shield regeneration and carrier armor.
- **Run win rate for the bot went up to 49%.** With a helm a person will do better than that. The run may now be too
  easy; I have not raised enemy numbers without a human result to aim at.
- **Depth is still one fleet and one faction.** 27 cards and 11 modules. The next real depth is content: module
  tiers, relics, a second hull, Concord as a playable side (M3).
- Burn is still rarely played by the bot (4%). It may need a stronger effect, or the bot may simply not see its use.
- No first-time tips on the map, shipyard or events yet; only the battle has a tutorial.
- 152 EditMode tests pass (15 new). The helm, the glossary, the strike picker and the tutorial's new step were each
  put on screen and driven from script; none was used with a physical mouse.


## M3, part 1: the race and the nemesis

The M3 exit test is "the race changes routing; a nemesis return feels personal". This part builds both for the
Ascendancy. The Concord as a playable side (officers, Council) is the second part and is not in yet.

### The race

- **Jump lanes.** Two dotted cyan lanes per sector skip a layer. You gain two nodes on the rivals for one jump of
  their time; every ship pays 4 hull and you never see the node you skipped.
- **Mined lanes.** While the Concord leads by a full node, every ordinary jump costs each ship 2 hull. The info bar
  says so, the open lanes turn red, and jump lanes are clear. Falling behind now bleeds the fleet, not only the ending.
- **False beacons.** A shipyard service, once per visit: 25 scrap, both rivals lose 0.8 node, no dock time.

### The nemesis

- Each run draws one of three Concord commanders: Captain Ilse Marrow (a fore gun that reaches 5), Wing-Speaker
  Tobiah Rask (gunship wings and Joint Fire) and Engineer-Prime Dessa Koyl (plate, a short cutting beam, repairs).
  They wait on a battle node, marked on the map with a red crosshair.
- **Hail.** Reaching them opens a hail: portrait, what they say (it changes with what happened last time and quotes
  the scar you gave them), a dossier, their fleet, and four replies with their effect spelled out: Edict, Open fire,
  Board the relay (4 Ordinals), Say nothing.
- **Breaking off.** Below rank III the commander's ship leaves the field at 40 percent hull, after the enemy has
  acted. Killing it means taking it from above that line to nothing in one turn. The ship's card says the number.
- **Coming back.** A survivor moves to a node further on, a rank higher: +6 hull, the next rank's escorts, and a refit
  against whatever did the most damage (lance baffles, flank plating, point defence or sealed decks), with a scar named
  for it in the dossier. At rank III the ship fights to the end.
- **Slipping past** costs 0.3 node and moves them ahead of you. With no lane left they fall back to the Concord main
  fleet and the Concord gains 0.3 node.
- **Killing them** takes their trophy relic, an extra reward card, and sets the Concord back 1.5 nodes.

### Relics

Ten passive relics: seven in a pool (every elite carries one) and one trophy per commander. They are listed under the
fleet cards on the map; hovering one shows what it does.

### Numbers (200 bot runs, `Tools/sim/race_nemesis_sim.cs`)

| | Before M3 | After |
| --- | --- | --- |
| Runs won / fleet lost / outpaced | 107 / 46 / 47 | 123 / 39 / 38 |
| Runs that met their commander | - | 169 |
| Nemesis fights: won / fleet lost | - | 229 / 15 of 256 |
| Commander killed / broke off / stand-off | - | 34 / 196 / 26 |
| Runs that reached rank III | - | 60 |
| Jump lanes taken | - | 44 |
| Runs that saw mined lanes | - | 35 |

The first version let a commander with no lane left join the gate boss. The bot then won 14 of 141 gate fights, so
that rule was replaced with the fall-back. The bot almost never kills a commander because it does not plan burst;
a person with an Expended lance volley can (Quiet Ledger breaks off at 8 hull of 20, and an Expended volley does 15
through shields).

### Art

Three commander portraits and the nemesis map icon: 40 generations, logged in `ArtSource/pixellab-log.csv`.

### How it was checked

181 EditMode tests pass, 26 of them new (`NemesisTests.cs`). In Play Mode the map with the nemesis node and jump
lanes, the hail, a nemesis battle answered with Open fire, the commander breaking off, the reward and the map with
mined lanes and a relic were each put on screen and captured (`ArtSource/Screenshots/m3_*.png`). The states were
driven from script, not with a mouse.

### Still open

- The bot wins 61 percent of runs. A rank I nemesis fight is easier than the battle node it replaces (229 of 256 won).
  Commander hull or escorts may need to go up once a person has played them.
- The commanders reuse the frigate, tender and trimaran sprites. Scars and refits are text in the dossier and on the
  ship's card, not yet marks on the portrait or the hull.
- Hail replies are the same four for every commander. The spec wants replies that depend on shared history.
- Interception (a rival fleet you can fight to slow it) is covered only by the nemesis, and only for the Concord.
  The Chorus marker still does nothing but change the epilogue.
- No nemesis carries over between runs.

## M3, part 2: the Concord as a playable side

New run now asks which fleet to take out. The Concord is a second, complete route through Sector I: its own
flagship, deck, modules, officers and Council, fighting the Ascendancy instead of being fought by it.

### The fleet

- **Long Promise** (trimaran carrier): hull 36, armor 2, shields 3, Speed 2, heat cap 7. Tougher and slower than the
  Sixth Verdict. Her escort is an **Outrider** with side guns. The hangar flies four **gunships** (3 hull, 3 damage)
  instead of six darts.
- **15 new cards.** Mass Driver (10 damage, fore, range 5, shoves the target a hex or slams it into what is behind),
  Deck Guns, Patch Job, Council Vote (draw 1, +1 command next turn), Ring Shield, Ring Barrage (3 to everything within
  2), Launch Wing, Combat Patrol (a gunship beside a ship takes the next shot at it), Ripple Salvo (four hits of 3 that
  move on when the target dies), Patch Crews, Cutting Beam (7, adjacent, ignores armor), Long Scan, Spotter Data
  (next weapon +3 damage and +1 range), Outrider Guns, Torpedo Run.
- **9 modules** in four slots (Bow, Drum, Outriggers, Aft), two or three to choose between in each.
- The Concord has no Expend cards. Its crew pool works the same way underneath (at 0 every card costs 1 more) but
  nothing spends it on purpose.

### Officers

Six named officers, two aboard at the start, four berths. Each is a passive bonus: the engineer adds 2 to every
repair card, the navigator draws a sixth card on turn 1, the gunnery officer adds 1 to the flagship's weapons, the
flight boss adds 1 to every gunship attack, the quartermaster brings 2 crew per victory, the shield technician adds
1 shield. **Every critical hit on the flagship wounds one**, and a wounded officer gives nothing until a shipyard's
sickbay (15 scrap) or a Council motion sees to them. Losing the flagship wounds them all.

### The Council

Approval, 0 to 6. A clean win earns 1 (an elite 2), a win that cost a ship earns none, a stand-off loses 1. It is
spent between jumps on six motions: emergency repairs, relief crews for the wounded, gunships from reserve, a
requisition of scrap, commissioning an officer, and running dark to set both rivals back. Events on the Concord's
route also move approval, and one hail reply (jamming the commander's relay) is paid for in it.

### The Ascendancy as the enemy

Blades, lance-cruisers and vat-wardens that launch darts; ten encounters; the High Inquisitor Calix Mor on the Ninth
Sentence at the gate; five events written for a fleet that votes; and three Ascendancy commanders to be hunted by:
Inquisitor-Adjunct Seraphine Vael, Lictor-Captain Dorn Achter and Vat-Master Ioun Rell, each with their own hails,
escorts and trophy.

### Numbers (200 bot runs each)

| | Ascendancy | Concord |
| --- | --- | --- |
| Runs won / fleet lost / outpaced | 123 / 39 / 38 | 103 / 54 / 43 |
| Turns fought per run | 49 | 70 |
| Nemesis fights won | 229 of 256 | 218 of 243 |
| Commander killed / broke off | 34 / 196 | 24 / 195 |
| Runs that saw mined lanes | 35 | 76 |
| Gate fights won | 123 of 173 | 103 of 173 |

The first Concord numbers were 165 runs won and 2 fleets lost: repairs from three sources out-healed everything the
Ascendancy could do. The enemy's guns went up, the engineer's bonus and Patch Crews came down, and emergency repairs
now cost 2 approval.

### How it was checked

202 EditMode tests pass, 21 of them new (`ConcordTests.cs`): every reference in both factions' content resolves,
each new card does what its text says, officers, wounds, the Council, events, saves, and whole bot runs. In Play
Mode the faction picker, the Concord map with officers and approval, the Council's order paper, a vote, and a Concord
battle were put on screen and captured (`ArtSource/Screenshots/m3_faction_picker.png`, `m3_concord_*.png`,
`m3_council.png`). The autopilot then played that run to the end in the editor and won it, with no errors logged.
None of it was played with a physical mouse.

### Art

Four Ascendancy portraits (the Inquisitor and the three commanders) and three Concord resource icons: 50
generations, logged. 529.6 of the 1,000 are spent in all.

### Still open

- **Concord battles run long**: 70 turns a run against 49. The Mass Driver is the only big hit and it costs 2 of 3
  command. Long fights also hand the Ascendancy the race (mined lanes in 76 runs of 200).
- Card art on Concord cards is cropped from the Long Promise by module (bow, drum, outriggers, aft), the same way the
  Ascendancy's is; the bridge cards all share the drum (`ArtSource/Screenshots/m3_concord_deck.png`).
- The Concord enemies of an Ascendancy run and the Concord player ships share sprites, as do the Ascendancy ones.
  The Outrider, the Blade and the gunship have no card-size drawing.
- The tutorial and the skirmish menu are Ascendancy only.
- Officers have no portraits, and no posts: the spec's Rotate Crews (moving an officer between modules) is not built.
- The Council is a list of motions. Blocs, and votes that can fail, are not built.
- There is one hail reply set per faction, not per commander.

## M4, part 1: more to find, more story, one look

Aimed at three things: runs that differ more from each other, story in the places a player already looks, and
screens that match each other.

### Replayability

- **Hazards.** About a third of the fights past the opening are in an ion storm (nobody's shields regenerate), a
  nebula (every weapon on both sides reaches 1 hex less) or a dense field (twice the rock). The node says so in violet,
  the briefing before the fight says what it does, the battlefield is tinted, the rule is shown once when the fight
  starts, and weapon cards show their shortened range. Hazard fights pay 1.3 times the salvage.
- **A choice of relic.** An elite now offers two relics and you take one, before the rest of the salvage.
- **Three more relics** (15 in all), each with its own icon: the Lost Compass (mined lanes cost nothing), the Singing
  Shard (+4 hull) and Clean Hand's Bell as Achter's trophy.
- **Four more events for each faction** (nine each): the Marker, Weather, the Kessane Line and Lanternhouse. A sector
  draws five of the nine, so no run sees them all. Events can now hand over a relic.

### Story

- **An opening briefing**, once per run, in each faction's voice: what the broadcast was, who else is going, who
  holds the gate. Under it, five lines on how the map works. This is also the map's first-time help.
- The four new events carry the setting: an Antecessor marker older than the Reef repeating the broadcast, the Chorus
  passing as "weather", the old Kessane battlefield that both factions keep referring to, a market that sells to anyone.
- **An aftermath line on every encounter**, shown with the salvage (what was in the picket's log, what the vat-ship
  was carrying).
- **Every commander answers the reply you gave** (four answers each, 24 lines), on the open channel as the fight
  starts, and the hail now shows their ship beside the dossier.
- **An epitaph for every commander**, and one journal line each for the first time the rival fleet and the Chorus
  get ahead of you.

### Visual coherence: what was wrong and what was done

| Found | Fix |
| --- | --- |
| The Concord's Council event showed the Ascendancy's kneeling Ordinals | Its own illustration; the Refugee Flotilla too |
| All six commanders and the Concord route's boss flew ordinary frigate, tender and cruiser sprites | A ship for each (seven sprites, three facings each), shown in the hail and marked with a red hex on the board |
| Every relic was the same small icon, listed as text | One icon per relic, shown as a shelf of icons and at double size when choosing |
| The boss spoke as plain text, commanders from a comms well | Both use the comms well |
| The faction picker was two small ships in a lot of empty plate | Flagships at double size, with escort, fit and what the fleet starts with |
| Events with four choices ran off the bottom of the dialog | The dialog grows with the number of choices |

### Numbers (200 bot runs each)

| | Ascendancy | Concord |
| --- | --- | --- |
| Runs won / fleet lost / outpaced | 121 / 39 / 40 | 100 / 56 / 44 |
| Turns fought per run | 48 | 67 |
| Relics per run | 0.86 | 0.82 |

With all nine events on the map the bot won 134 of 200 as the Ascendancy, because events were replacing fights. A
sector now draws five. Deck Guns went from 4 to 5 and Launch Wing from three gunships out to four to shorten Concord
fights; the bot's fights did not get shorter, so that is still open.

### How it was checked

209 EditMode tests pass (7 new: hazards, relic choice, the mine-proof relic, relic events, briefing and story
flags). In Play Mode the briefing, a four-choice event, the relic choice, the map with hazard labels and the relic
shelf, a nebula fight, the new faction picker, a return hail and a commander's ship on the board were put on screen
and captured (`ArtSource/Screenshots/m4_*.png`). The autopilot played a full run with hazards and relic choices to
a win with no errors logged. Not played with a physical mouse.

### Art

Six event illustrations, sixteen relic icons (one unused: it read as a trooper helmet) and seven ships: 140
generations. 669.6 of the 1,000 are spent.

### Still open

- Hail replies are still one set per faction (the commanders' answers differ; your options do not).
- Scars are text and the red hex; the portraits do not change.
- The Concord and Ascendancy line ships are shared between player and enemy.
- No music. The tutorial and skirmish are Ascendancy only.
- Hazards do not reach the gate or a nemesis fight.

## M4, part 2: sound, scars and a way to finish it

- **Music.** Four seamless loops in D minor, synthesized by `Tools/make_music.py` (PixelLab music needs a higher tier):
  a drone with four seconds of shaped nothing for the menu, slow pads and a bell figure for the map, a pulse for
  fights, and a faster, sourer one for a commander or the gate. `Music.Play(name)` cross-fades when the screen changes.
- **Settings**, from the main menu: music and effects volume in ten steps, remembered.
- **Scars you can see.** Each of the six commanders has a second portrait (burn scar, dressing, soot) that replaces
  the first once they carry a scar from you.
- **A fifth reply, for old acquaintances only: Taunt.** They will not break off this time, and their attacks deal +1.
  It is how you end a commander without needing one turn of burst, at a price. Each has their own answer to it.
- **A Concord skirmish** (Lance Patrol), so the fleet can be tried without starting a run. It uses the deck a Concord
  run starts with; a test holds the two together.

### Numbers

With the bot taunting returning commanders when its fleet is healthy, it kills 53 commanders in 200 Ascendancy runs
(30 before) and wins 116.

### How it was checked

211 EditMode tests pass (2 new). On screen: the settings dialog with the menu loop playing, a return hail with the
scarred portrait and five replies, a taunted fight (the hunt loop playing, no break-off, the commander's answer on
the channel) and the skirmish menu (`ArtSource/Screenshots/m4_settings.png`, `m4_hail_scarred.png`,
`m4_skirmish_menu.png`). **Nobody has listened to the music.** The loops were checked by number only: no clipping, no
seam at the loop point, sensible levels. They may be dull or grating; the volume control is there for that.

### Art

Six scarred portraits: 20 generations. 689.6 of the 1,000 are spent.

## A second sector

A run is now two sectors. Taking the Ion Reef gate no longer ends it: the fleet goes through into the Kessane Drift,
the old battlefield both factions keep referring to.

- **Between sectors.** The gate's yards mend every hull, replace a lost escort, strip the junk, return the wounded
  and make up the hangar and the crew. You take one of three relics from the wreck of the gate fleet, read what lies
  beyond, and jump. The race carries over as a lead of at most a node and a half either way.
- **The Kessane Drift** has its own map and battlefield art, a new map, new jump lanes and hazards, and draws the
  events the first sector did not use before repeating any.
- **Better-found enemies.** Capital ships there have 12 percent more hull. (25 percent and +1 damage was tried first:
  the bot won 1 run in 200.)
- **The commander follows you.** One who got away or fell back is waiting in the Drift with the rank, scar and refit
  they left with. If you killed yours, another takes up the hunt.
- **A second gate.** The Ascendancy meets Fleet-Speaker Anselma Dray on the dreadnought Common Cause, who opens by
  naming the man you killed at the first gate. The Concord meets Archon Sevrin Thale on the Hand of the Prime. Each
  has a portrait, a ship, a hail and their own three endings.
- The number of sectors is one line of data (`sectors` in each run file); the rules take any number.

### Numbers (200 bot runs each, two sectors)

| | Ascendancy | Concord |
| --- | --- | --- |
| Runs won / fleet lost / outpaced | 44 / 102 / 54 | 64 / 82 / 54 |
| Turns fought per run | 81 | 103 |

A two-sector run is meant to be harder to finish than the single sector was, and the bot is a weak player, but
these are low and the Ascendancy is now the harder side: 27 of its fleets die at Dray's gate and 23 to a rank II
commander, which the bot goads into staying and then cannot finish.

### How it was checked

215 EditMode tests pass (4 new: the gate opening onto a second sector, unseen events first, the commander carrying
over or being replaced, scaling and the last gate ending the run). On screen: the between-sector screen, the Drift's
map, Dray's hail and her fight (`ArtSource/Screenshots/m5_*.png`). The autopilot then played a two-sector Concord
run in the editor from the first jump to the Archon's gate and won it, with no errors logged.

### Art

Two backgrounds, two portraits, two ships: 90 generations. 779.6 of the 1,000 are spent.

### Still open

- Balance of the second sector rests on bot runs alone, and on one measurement.
- The Drift reuses the first sector's encounters with tougher hulls; it has its own gate but not its own line fights.
- The Chorus is still only a marker on the track and an event.

## Colour-coded text and tooltips

Kade's first reaction to playing M3 and M4: an event said the rivals moved by 0.8 node and there was no way to find
out what that meant. Text should be colour-coded by what it does in play, and almost anything should explain itself
on hover.

- **One vocabulary** (`Gloss`, in `View/Tips.cs`): 27 rules words, each with one colour and one explanation. Green is
  hull, red is damage and danger, orange is heat, cyan is shields, brass is scrap, blue is crew, pink is the race,
  gold is relics, violet is hazards, rust is junk.
- **Everywhere at once.** Body text and labels are marked as they are created, so the same word is the same colour
  on the map, in events, the shipyard, the Council, hails, the briefing and in battle. Card text is coloured too.
- **Hover a coloured word** and a tooltip explains it, with the names of this run filled in. The race tooltip says
  what a node is, what moves the rivals, and what falling behind costs.
- **Hover a thing** for a tooltip of its own: every map node (what is there, what it pays, why it can or cannot be
  reached), event choices, shipyard services, Council motions, hail replies, relics, officers, modules, the race
  track, cards outside battle, and in battle the four arc readouts on a ship card, the helm, command pips, each enemy
  order and End Turn.
- Event wording: "Rivals lose 0.8 node" is now "Rivals fall back 0.8 node", and a cost in time says "Costs time".

### How it was checked

215 EditMode tests pass. The tooltips were put on screen by pointing the tooltip layer at a spot from script (it
normally follows the mouse): a word in the top bar, the race effect of an event choice, a shipyard service row, the
heat label and an arc readout in battle (`ArtSource/Screenshots/tips_*.png`). They have not been tried with a real
mouse, so how they feel while moving the pointer around (flicker, getting in the way) is untested.

### Also fixed

Scripted checks were writing to the same save file a real run uses, so they could overwrite a run in progress. They
now use their own file.

## Ordinals redrawn to Kade's brief

The Ordinals icon in the top bar was a row of trooper helmets, and the Edict art had drifted the same way before.
Kade's brief: white armour with streaks of dark blue, a smooth helmet with a long feature down from the chin, no
visible face.

- A design reference was generated to that brief first (`ArtSource/design/ordinal_reference.png`) and then used as
  the reference image for everything else, so the pieces agree with each other.
- **The Ordinals icon** is one smooth, faceless helmet with the chin spike and a dark blue streak.
- **The Edict of the Prime** is a close rank of five kneeling Ordinals to the same design, under the sunburst. They
  have picked up gold epaulettes from the reference, and the blue is on the helmets more than the armour.
- The brief and the prompt wording are written into `Docs/FleetAndAssetPlan.md` so later art follows them.

Two attempts were rejected on the way (three heads in a row were a muddle at 48 pixels; a wide shot came out as the
old robed ranks). 50 generations; 829.6 of the 1,000 are spent.

Not checked in the running game: the editor was in Play Mode when the files were swapped, so they were left to
re-import on their own. Both replace existing files under the same names.

## Edicts of the Prime, and the Drift's own fights

### Edicts

The Concord has its Council; the Ascendancy now has the thing the spec gives it: orders it cannot refuse.

- At the start of each sector two Edicts are read out and you take one. Leaving without choosing takes the first.
- There are seven: expend 10 Ordinals in battle, end no battle in a stand-off, destroy the commander who hunts you,
  reach the gate ahead of the Concord, buy no more than 2 shipyard services, win an elite, do not lose the escort.
  Each is written twice: the Prime's wording, and what it means in play.
- The Edict in force sits under the fleet cards with how it stands (for example "6 of 10 Ordinals expended"), in green,
  amber or red. Hovering it gives the order, the reward and the penalty.
- It is **judged on arriving at the gate**, before that fight. Obeyed, it pays: scrap, more room in the vats, flagship
  hull or a relic, depending on the Edict. Broken, 4 Ordinals are taken and an **Inquisitor comes aboard**: a junk card
  that costs 1 command every time it is drawn, until a shipyard strips it out. The gate briefing says which it was.
- The second sector brings a new pair.

### The Kessane Drift's own fights

The second sector no longer reuses the first sector's battles with more hull. It has eight fights of its own for each
faction, with their own briefings and aftermath lines, built around four new ship types (on existing hulls):

- Against the Ascendancy: the Concord **line frigate** (two broadsides in three turns, no torpedo) and **torpedo skiff**.
- Against the Concord: the Ascendancy **Blade lancer** (a Blade that reaches 4 hexes) and **bastion cruiser** (slow,
  plated on every side, two broadsides in three turns).

The flat 12 percent hull bonus in the second sector is gone: the Drift is harder because of what is in it.

### Numbers (200 bot runs each, two sectors)

| | Ascendancy | Concord |
| --- | --- | --- |
| Runs won / fleet lost / outpaced | 38 / 111 / 51 | 60 / 90 / 50 |

About where they were before this round (44 and 64). The Ascendancy is still the harder side for the bot: 27 fleets
die at Dray's gate and 20 at Venn's.

### How it was checked

225 EditMode tests pass, 10 of them new (`EdictTests.cs`): each kind of Edict is counted and judged as it says, the
penalty and the Inquisitor card, a new Edict per sector, every Drift fight opening in reach and playable to an end, and
neither sector borrowing the other's fights. On screen: the Edict choice, and the Edict panel with its tooltip
(`ArtSource/Screenshots/m5_edict_*.png`). The autopilot played a run through both sectors with an Edict in force and
no errors. The redrawn Ordinals icon was seen in the top bar in the same session.

### Still open

- The new ship types reuse the frigate, skiff, Blade and cruiser sprites.
- An Edict is judged only at the gate; nothing tells you mid-sector that one has just become impossible beyond the
  panel turning red.
- The bot picks the Edict it is most likely to keep and does not play toward it, so how often a person keeps one is unknown.

## Polish pass: two review cycles

Kade asked for a few cycles of cleaning up gameplay and reviewing the UI for clutter, intuition and cohesion. Each
cycle: put every screen up on the play-test save, list what is wrong, fix, look again.

### What changed on screen

- **Map, race panel.** Each line is one colour again (it was glossed word by word, so "Concord" was red and "0.7 nodes"
  pink in the same line). The heading is "The race"; the hunt line moved to the header row, where it no longer clips.
- **Map, journal.** The last three things that happened, without the relic and Edict lines the panels above already show.
- **Map, fleet card.** Module slot names are legible (dim grey, not dark brass). The label of the node you are on sits
  clear of its ring.
- **Battle briefing.** Enemy orders are readable again (they were cut to "Maneuver..."): the first four steps, then
  "more". Less dead space under the intro.
- **Reward.** Taller, with the note, the relic line and the cards each on their own row. "Command" in "command deck" is
  no longer coloured as the resource.
- **Council.** The motions start under the rules line instead of after a gap. "Fall back 0.8 node" everywhere a rival
  is slowed (it said "lose" in two places and "fall back" in the events).
- **Sector clear.** The yard line is one green sentence, not six coloured words.
- **Battle.** The three hint lines bottom left show on turns 1 and 2 (and in the tutorial); after that only craft that
  can still move are listed, since End Turn already says what is open. Help spells out "minus and plus" (the body font's
  hyphen vanished between spaces) and names the fleet's own craft. Prompts say "gunship" in a Concord battle. Shared
  junk cards say "crew" there, not "Ordinals". The end screen is titled "Driven off" when the commander ran.

### What changed in the rules

- **A broken Edict is announced at once.** The journal says "The Edict cannot now be obeyed" the moment it happens,
  once. (This was on the open list.) Tested.
- **The gate briefing no longer says the rival "reached the gate before you" when you are level**; the guard is
  reinforced either way, and it now says the rival is at the gate with you.
- **Admiral Dray is a little softer.** The Ascendancy won 36 of 200 bot runs against the Concord's 60, and Dray alone
  ended 32. Common Cause has 38 hull (was 42) and her reinforcement is a skiff, not a second frigate.

### Bot results after the pass (200 runs each, two sectors)

| | Won | Lost | Outpaced | Lost at the second gate |
|---|---|---|---|---|
| Ascendancy | 55 (was 36) | 95 | 50 | Dray 14 (was 32) |
| Concord | 60 | 90 | 50 | Thale 14 |

225 EditMode tests pass.

### Still open

- Several Concord cards share one art crop (the ring), so a Concord hand looks samey.
- Large ships on adjacent hexes overlap each other on the board.
- The run-over screen shows the last journal lines but not the relics held or what became of the commander.
- Balance still rests on bot runs; nothing here was checked with a physical mouse.
