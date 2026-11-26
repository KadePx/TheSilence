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
