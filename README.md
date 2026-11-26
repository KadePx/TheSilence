# The Silence

A grimdark space roguelike for Steam. Lead a fleet of up to four customisable capital ships and about
eight small craft across a hostile sector, racing two rival factions to a relic that once ended a
civilisation. Combat is turn-based on a hex grid; your cards come from the modules fitted to your ships.

- **Engine:** Unity 6 LTS, Universal 2D (URP 2D Renderer), Pixel Perfect Camera at 32 PPU, 1920×1080 reference.
- **Art:** pixel art from PixelLab now, commissioned art later.
- **Status:** playable alpha to milestone M2 (one sector run loop). See `Docs/AlphaReport.md` for what works and what is rough.

## Playing the alpha

1. Open the project in Unity 6 (6000.6.10f1), open `Assets/_Project/Scenes/Menu.unity` and press Play.
2. **New run** starts a seeded run across one sector: fight, take salvage, answer events, refit at shipyards,
   beat Marshal Venn at the gate. The run is saved after every step on the map; **Continue run** resumes it.
3. **Skirmish** plays the two paper-prototype scenarios on their own.

Battle controls: click a card, then its target (weapons need the target in their arc and range; turning
cards show heading arrows). Click a dart to move it. Right-click or Esc cancels. Keys 1-9 pick cards, E ends
the turn, Space fast-forwards. F9 lets the bot play a turn; F10 lets the autopilot play the whole run.

Tests: menu **Silence > Run Tests (summary)** (or Window > General > Test Runner > EditMode).

## Layout

| Path | What |
| --- | --- |
| `Assets/_Project/Scripts/Core` | Rules, pure C# with no Unity references: `Battle/` (combat) and `Run/` (run loop) (`Silence.Core`) |
| `Assets/_Project/Scripts/Data` | Content loader for the JSON files, run save format, art library (`Silence.Data`) |
| `Assets/_Project/Scripts/View` | UI kit, hex board, battle screen; animates events from Core (`Silence.View`) |
| `Assets/_Project/Scripts/Meta` | Session and save file, main menu, sector map and its dialogs, autopilot (`Silence.Meta`) |
| `Assets/_Project/Data/Resources/Content` | Encounters, events, modules, reward pool and tuning numbers as JSON |
| `Assets/_Project/Editor` | Editor tools: pixel import preset, art library and font baking, test summary (`Silence.Editor`) |
| `Assets/_Project/Art` | Sprites by faction and unit; `_n` files are normal maps |
| `Assets/Tests/EditMode` | NUnit tests for Core (`Silence.Tests`) |
| `Docs/` | Game spec, paper prototype, fleet and asset plan, Unity setup plan |
| `ArtSource/pixellab-log.csv` | Job ID, seed and candidate for every generated asset |
| `Tools/rotsprite.py` | RotSprite rotation for the 60° hex facings |
| `Tools/make_sfx.py` | Synthesizes the placeholder sound effects as WAV files |

## Facings

Only the East sprite is generated. NE and SE are exact 60° RotSprite rotations on a 1.25× canvas
(`python3 Tools/rotsprite.py in.png out.png 60 160` for NE, `-60` for SE). W, SW and NW are the E, NE and SE
sprites turned 180°, done at runtime (`HexDirection.SpriteFor`).
