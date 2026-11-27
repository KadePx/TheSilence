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
