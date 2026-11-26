# The Silence

A grimdark space roguelike for Steam. Lead a fleet of up to four customisable capital ships and about
eight small craft across a hostile sector, racing two rival factions to a relic that once ended a
civilisation. Combat is turn-based on a hex grid; your cards come from the modules fitted to your ships.

- **Engine:** Unity 6 LTS, Universal 2D (URP 2D Renderer), Pixel Perfect Camera at 32 PPU, 1920×1080 reference.
- **Art:** pixel art from PixelLab now, commissioned art later.
- **Status:** milestone M1, grey-box combat.

## Getting started (on your PC)

1. Install Unity Hub and the latest Unity 6 LTS, Git and Git LFS (`git lfs install` once).
2. In Unity Hub, create a **Universal 2D** project named `TheSilence`, open it once, then close Unity.
3. In that project folder, pull this repo in on top of it:
   ```
   git init
   git remote add origin https://github.com/KadePx/TheSilence.git
   git fetch origin
   git checkout -b main --track origin/main
   ```
4. Open the project in Unity. In Project Settings → Editor, check **Asset Serialization: Force Text** and
   **Version Control: Visible Meta Files**. Add the **Input System** and **Test Framework** packages if missing.
5. Window → General → Test Runner → EditMode → Run All. Everything should pass.
6. Commit the generated `ProjectSettings/`, `Packages/` and `.meta` files and push.

## Layout

| Path | What |
| --- | --- |
| `Assets/_Project/Scripts/Core` | Rules engine, pure C# with no Unity references (`Silence.Core`) |
| `Assets/_Project/Scripts/Data` | ScriptableObject definitions for ships, modules, cards (`Silence.Data`) |
| `Assets/_Project/Scripts/View` | Board, sprites, UI; animates events from Core (`Silence.View`) |
| `Assets/_Project/Scripts/Meta` | Run map, events, saves, progression (`Silence.Meta`) |
| `Assets/_Project/Editor` | Editor tools: pixel import preset, Facing Baker (`Silence.Editor`) |
| `Assets/_Project/Art` | Sprites by faction and unit; `_n` files are normal maps |
| `Assets/Tests/EditMode` | NUnit tests for Core (`Silence.Tests`) |
| `DotnetTests/` | Runs the Core tests without Unity: `dotnet test DotnetTests` |
| `Docs/` | Game spec, paper prototype, fleet and asset plan, Unity setup plan |
| `ArtSource/pixellab-log.csv` | Job ID, seed and candidate for every generated asset |
| `Tools/rotsprite.py` | RotSprite rotation for the 60° hex facings |

## Facings

Only the East sprite is generated. NE and SE are exact 60° RotSprite rotations on a 1.25× canvas
(`python3 Tools/rotsprite.py in.png out.png 60 160` for NE, `-60` for SE). W, SW and NW are the E, NE and SE
sprites turned 180°, done at runtime (`HexDirection.SpriteFor`).
