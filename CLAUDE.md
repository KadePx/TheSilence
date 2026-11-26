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
  battle or run state, so battles can be replayed from a seed and a list of actions.
- The view never mutates state. It sends an action to Core; Core applies it and emits events; the view
  animates those events.
- Dependencies flow Core ← Data ← View/Meta ← Editor. Tests reference Core and Data only.
- C# 9 (Unity's language level). Keep `DotnetTests/DotnetTests.csproj` at `LangVersion 9.0`.

## Hex conventions

- Pointy-top hexes, axial `(q, r)` in code, board stored as offset odd-r `(col, row)`; board is 9×7.
- Facings `E, NE, NW, W, SW, SE` = screen angles `0, -60, -120, 180, 120, 60` with y down.
- Arcs: Fore within 30° of facing, Aft within 30° of the reverse, Port = ship's left, Starboard = right.
  A hex exactly on 30° or 150° is in both arcs and the defender chooses (`Arcs.Of` returns both flags).

## Commands

- Run Core tests without Unity: `dotnet test DotnetTests`
- Run all tests in Unity (batch): `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`
- Rotate a sprite to a hex facing: `python3 Tools/rotsprite.py in.png out.png 60 160`

## Art

- Every PixelLab generation is logged in `ArtSource/pixellab-log.csv` (job ID, candidate index, seed).
- Naming: `<faction>_<unit>_<facing>[_<variant>].png`, faction prefixes `asc`, `con`, `cho`; normal maps end `_n`.
- Faction palettes and prompts are in `Docs/FleetAndAssetPlan.md`.
- Art and audio are in Git LFS; keep PNGs out of `Docs/`.

## Workflow

- Short feature branches, merged to `main` when they play. Every rule change gets a test.
