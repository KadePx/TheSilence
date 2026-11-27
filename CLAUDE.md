# CLAUDE.md

Guidance for Claude Code working in this repository.

## Project

The Silence: a grimdark space roguelike (Slay the Spire structure, fleet management, turn-based hex combat
with cards from ship modules). Unity 6 LTS, Universal 2D, pixel art. Solo developer. Design docs in `Docs/`;
`Docs/GameSpecification.md` is the source of truth for rules, `Docs/UnitySetupPlan.md` for architecture and
the sprint plan.

## Architecture rules

- `Silence.Core` is pure, deterministic C#: no `UnityEngine`, no `System.Random`, no floats in rules, no
  `DateTime`, no static mutable state. All randomness goes through `Silence.Core.Random.Pcg32` held in the
  battle or run state, so battles and whole runs can be replayed from a seed and a list of actions.
- Core has two halves: `Core/Battle` (one fight) and `Core/Run` (the run loop: map, travel, rewards, events,
  shipyard, in `RunRules`; the saved state is `RunState`). The run rules live in Core, not Meta, so tests
  and balance sims can play whole runs headless (`RunBot`).
- The view never mutates state. It sends an action to Core; Core applies it and emits events; the view
  animates those events. The same holds for the map: every click is a `RunRules` call.
- Dependencies flow Core <- Data <- View <- Meta <- Editor. Meta uses View's UI kit (`Ui`, `Skin`,
  `CardView`) and hands battles to the Battle scene through `BattleLauncher`. Tests reference Core and Data only.
- Content: cards and unit templates are code in Core (`PaperCards`, `PaperUnits`, and `ConcordCards`, `ConcordUnits`
  for the Concord's fleet and the Ascendancy as an enemy; their effects and AI are code). Encounters, events, modules, the reward pool, nemesis commanders, hail replies, relics and run tuning are JSON in
  `Assets/_Project/Data/Resources/Content/`, loaded by `Silence.Data.ContentLoader`. Each playable faction has its
  own four files (`run`, `encounters`, `events`, `nemeses`; the Concord's are prefixed `concord_`) and its own
  `RunContent`; `RunState.Faction` says which one a run uses. Names, voice and what differs between factions are in
  the `faction` block of its run file, not in code.
- Screens build their UI in code at runtime (no scene wiring). Scenes hold only a camera and a light; a
  `RuntimeInitializeOnLoadMethod` adds the right screen object by scene name.
- Sprites, fonts and sounds are looked up by name through `ArtLibrary` (`Resources/ArtLibrary.asset`).
  After adding art or audio run **Silence > Rebuild Art Library**; after adding a font, **Silence > Bake Fonts**.
- C# 9 (Unity's language level).

## Hex conventions

- Pointy-top hexes, axial `(q, r)` in code, board stored as offset odd-r `(col, row)`; board is 9×7.
- Facings `E, NE, NW, W, SW, SE` = screen angles `0, -60, -120, 180, 120, 60` with y down.
- Arcs: Fore within 30° of facing, Aft within 30° of the reverse, Port = ship's left, Starboard = right.
  A hex exactly on 30° or 150° is in both arcs and the defender chooses (`Arcs.Of` returns both flags).

## Commands

- There is no .NET SDK on the dev machine and no `DotnetTests/` project: run tests through Unity.
- All EditMode tests, short summary: menu **Silence > Run Tests (summary)**, result in `Temp/test-summary.txt`.
- All tests in batch mode (editor closed): `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`
- Rotate a sprite to a hex facing: `python Tools/rotsprite.py in.png out.png 60 <canvas>` (canvas = 1.25x the sprite; -60 for SE)
- Regenerate the placeholder sound effects: `python Tools/make_sfx.py`
- Regenerate the placeholder music loops: `python -P Tools/make_music.py` (four seamless WAV loops; `Music.Play(name)`)
- In Play Mode: F9 lets the bot play the current battle turn, F10 toggles the autopilot (whole runs).

- Balance and mechanic-usage numbers: evaluate `Tools/sim/balance_sim.cs` in the editor (Unity pipeline `script_eval`
  with `file`). 150 bot runs; reports win rates per encounter, battle length, dodges, heat, and how often each card is
  played when drawn. Writes `Temp/sim.txt`. The bot is weak, so read trends, not absolutes.
- Race and nemesis numbers: `Tools/sim/race_nemesis_sim.cs` the same way (200 bot runs; nemesis fights by commander,
  jump lanes taken, mined lanes, where runs are lost). Writes `Temp/m3sim.txt`. Put `concord` in
  `Temp/sim-faction.txt` first to run it for the Concord.

## Art

- Every PixelLab generation is logged in `ArtSource/pixellab-log.csv` (job ID, candidate index, seed).
- Naming: `<faction>_<unit>_<facing>[_<variant>].png`, faction prefixes `asc`, `con`, `cho`; normal maps end `_n`.
- Faction palettes and prompts are in `Docs/FleetAndAssetPlan.md`.
- Art and audio are in Git LFS; keep PNGs out of `Docs/` (screenshots go in `ArtSource/Screenshots/`).
- Effect sheets from the PixelLab VFX workbench are square cells in rows; their layout JSON is in `ArtSource/`.

## Interface

- The UI is pixel art on the same grid as the sprites. Everything scales by a whole number (`Ui.PixelScale`:
  1 up to 1440p, 2 at 4K) and whatever is left over is letterboxed; never scale a card, plate or label by a fraction.
- Fonts are the four PixelLab pixel fonts in `Assets/_Project/Fonts/Pixel` (Heading 16 px, Body 16 px, Label 12 px,
  Logo 32 px), baked as 1-bit bitmaps. `Ui.Label` snaps any requested size to a whole multiple of the font's native
  size, so ask for 16, 32 or 48 (12 or 24 for labels and numbers). They have no glyphs beyond A-Z, a-z, 0-9 and
  basic punctuation: no degree sign, brackets or ampersand.
- Plates, wells, buttons, bars and chips come from the kit in `Assets/_Project/Art/UI/Kit`, 9-sliced in `Skin`.
  Cards use `Art/UI/Cards/card_frame.png` (220x298) at 1:1.
- Keep screens quiet: nothing sits on screen permanently unless the player needs it every turn. Rules text goes
  behind Help, the battle log behind Log, detail on hover.

## Workflow

- Short feature branches, merged to `main` when they play. Every rule change gets a test.
