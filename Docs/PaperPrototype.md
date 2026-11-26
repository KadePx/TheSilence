# The Silence: Paper Combat Prototype

Nov 26, 2026 · @Kade

Play 3–5 tabletop battles to find out whether cards, facing, momentum, heat and hand-moved small craft are fun together, before any Unity work. Every number is a first guess; change them freely.

## Setup and units

You play the Ascendancy (flagship, one escort, darts) against a Concord patrol on a 9 × 7 hex grid.

**You need:** printed hex paper, tokens with a facing arrow, index cards, a d6, a tracker sheet per capital ship (Hull, Shields ×4 arcs, Heat, Speed) and one fleet-wide Ordinals counter starting at 24.

### Player (Ascendancy)

| Unit | Hull | Armor F/P/S/A | Shields per arc | Heat cap | Vent | Max speed | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Lance-cruiser (flagship) | 30 | 3/2/2/1 | 4 (regen 2) | 10 | 2 | 3 | Hangar: max 4 darts out |
| Blade escort | 14 | 1/1/1/0 | 2 (regen 1) | 6 | 2 | 3 |  |
| Ordinal dart | 2 | — | — | — | — | 4 hexes | Attacks 1 adjacent enemy, or shoots down an adjacent torpedo; ignores facing |

At 0 Ordinals you can't Expend, and every card costs +1 command.

### Enemy (Concord patrol)

| Unit | Hull | Armor | Shields per arc | Speed | Intent cycle |
| --- | --- | --- | --- | --- | --- |
| Concord frigate | 16 | 1 all | 3 (regen 1) | 2 | Broadside 5 (side arc, range 3) → Maneuver (turn to nearest, move 2) → Launch torpedo |
| Gunship wing (3 craft) | 2 each | — | — | 4 | Move toward the nearest capital ship's aft; 2 dmg if adjacent |
| Carrier (scenario 2) | 40 | 2/2/2/1 | 5 (regen 2) | 1 | Launch 2 gunships → Patch Up (repair 6 on an ally, +3 shields) → Joint Fire (all hit one target, +2) |

**Torpedo:** moves 3 hexes toward its target each Enemy Action step, hits for 6 on entering the target's hex, hull 1.

**Enemy AI on paper:** take the move that best lines up the next attack (frigates want a side arc, gunships want the aft); when unsure, move toward the nearest target.

## Rules on paper

Each turn runs in five steps; enemies always show their plan before you act.

1. **Intent:** announce each enemy's next intent and target; draw arrows for moves and torpedo paths.
2. **Command:** draw up to 5 and take 3 command. Play cards and move darts in any order (each dart moves up to 4, then attacks once). A card needs its source ship alive and the target in its arc and range.
3. **Drift:** every capital ship moves straight ahead by Speed. Entering an occupied hex: both take the mover's Speed × 2 and the mover stops.
4. **Enemy action:** intents resolve in order. If you left an attack's arc or range, it misses.
5. **End:** shields regenerate, ships vent heat, heat check, junk in hand triggers, discard your hand; unspent command is lost.

**Movement and facing:** a capital ship moves forward along its facing (Speed 0–3). Turning happens only through cards, 60° per step. Arcs: Fore = the front side, Port and Starboard = two flank sides each, Aft = the rear side. An attack hits the arc facing the attacker; on a boundary, the defender chooses.

**Damage:** shields on the hit arc absorb first, then that arc's armor reduces the rest once, then hull. If 5 or more hull damage gets through in one attack, roll a d6: 1–2 add Jammed Mount, 3–4 add Fire on Deck, 5 add Hull Breach, 6 engine hit (Speed −1, no Burn next turn). At 0 hull the ship is destroyed: **pull its cards out of the deck**, hand and discard.

**Heat:** after venting, heat above cap adds Fire on Deck; heat at cap + 4 or more is Reactor Strain (6 hull damage ignoring shields and armor, heat resets to cap).

## The deck

One shared deck: 10 starter cards, plus 1–2 reward cards added between battles. Each card names its source ship and module.

### Starter deck (10)

| Card | Cost | Source | Effect |
| --- | --- | --- | --- |
| Lance Volley ×2 | 2 | Flagship · fore lance | 9 dmg, fore arc, range 4, +3 Heat. Expend 3 Ordinals: +6 and ignore shields |
| Broadside Battery ×2 | 1 | Flagship · broadside | 4 dmg, port or starboard arc, range 3, +1 Heat |
| Burn | 0 | Flagship · bridge | One capital ship: Speed +1 (max 3), +1 Heat |
| Hard About | 1 | Flagship · bridge | Turn one capital ship 60°; twice at Speed 0–1 |
| Brace | 1 | Flagship · bridge | One ship: +4 shields on every arc this turn |
| Escort Guns | 1 | Escort · fore guns | 5 dmg, escort's fore arc, range 3, +2 Heat |
| Launch Darts | 1 | Flagship · hangar | Launch 2 darts next to the flagship (max 4 out); they act this turn |
| Vent Plasma | 0 | Flagship · reactor | Remove 4 Heat from one ship. Exhaust |

### Reward pool (10)

| Card | Cost | Source | Effect |
| --- | --- | --- | --- |
| Overcharge Lances | 1 | Flagship · lance | Next Lance Volley this turn +5, +3 Heat |
| Decimation Order | 0 | Flagship · bridge | Expend 5 Ordinals: +2 command, draw 3. Exhaust |
| Sacrificial Ram | 2 | Escort · engines | Escort moves up to Speed + 2; entering an enemy's hex deals its remaining hull and destroys it. Exhaust |
| Boarding Action | 2 | Flagship · hangar | Expend 4 Ordinals: an enemy within 2 hexes loses its intent |
| Waymind Targeting | 1 | Flagship · sensors | Next weapon ignores armor and crits on any hull damage |
| Precision Edict | 1 | Flagship · bridge | Mark an enemy; each weapon that hits it this turn deals +3 |
| Cold Discipline | 1 | Flagship · reactor | Remove 3 Heat from every ship, draw 1 |
| Screening Maneuver | 1 | Escort · engines | Escort turns 60° and moves 1; takes the next attack aimed at an adjacent ally |
| Dart Sortie | 1 | Flagship · hangar | Every dart moves +2 and attacks twice |
| Flak Lattice | 1 | Escort · point defense | Destroy up to 3 small craft or torpedoes adjacent to the escort |

### Junk cards

| Card | Effect |
| --- | --- |
| Hull Breach | Unplayable. In hand at end of turn: lose 2 Ordinals |
| Fire on Deck | In hand at end of turn: +2 Heat to the flagship. Pay 1 to put it out (Exhaust) |
| Jammed Mount | When drawn, a weapon card in hand costs +1 this turn |

## Scenarios and notes

Play scenario 1, then scenario 2 with the damage carried over, and write down the answers below after each.

### Scenarios

1. **Intercept at the Ring:** flagship and escort on the left edge, facing east, Speed 1, darts in the hangar. Enemy: 2 frigates and 1 gunship wing on the right edge. Two asteroid hexes in the center. Win by destroying both frigates.
2. **The Long Promise:** the carrier plus 1 frigate. Add 2 reward cards first, and carry over damage, junk and dart losses from scenario 1.

### After each battle

- [ ] Turns and minutes (target 6–10 turns, 10–15 minutes)
- [ ] Did facing and drift change your decisions every turn?
- [ ] Did heat make you hold back, or did you go over cap on purpose?
- [ ] Did spending Ordinals feel like a real cost?
- [ ] Was moving darts fun or busywork (seconds per turn)?
- [ ] Could you always see the threat coming?
- [ ] What happened when you pulled a lost ship's cards?
- [ ] Best moment and most boring moment

### Knobs to turn

| Problem | Try |
| --- | --- |
| Too slow | 2 command and 4 cards, or a 7 × 5 grid |
| Drift is a chore | Speed changes only through cards, or free turns at Speed 0 |
| Heat never matters | Lower the caps by 2, or remove passive venting |
| Darts are busywork | Move them as wings of 2 |
| Arcs don't matter | Halve shield regeneration |

The full game spec is in [The Silence: Game Specification](https://claude.ai/artifact/QQuth3g9DvWgWkHRzLryas).
