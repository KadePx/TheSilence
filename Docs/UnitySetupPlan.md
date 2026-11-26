# The Silence: Unity Setup Plan

Nov 26, 2026 · @Kade

## Summary

The first sprint ends when one Ascendancy fleet can fight a Concord patrol on a hex board in Unity, with real cards, facing, drift, heat and enemy intents, using placeholder and PixelLab art. That is milestone M1 from the spec: prove the combat is fun before building anything else.

| Who | Does |
| --- | --- |
| You | Install Unity and Git on your PC, create the GitHub repo, open the project, playtest |
| Claude (here or Claude Code on your PC) | Write the C# for the rules, data, board and UI; write editor tools; run PixelLab batches; review and fix |
| PixelLab | Ship, craft, icon and UI art per the asset plan |

Working in Claude Code on your PC is the better fit from here: it can open the Unity project, run its tests from the command line, and use your local PixelLab connection directly. This cloud session can still write code and push it to the repo.

## Install and create the project

About an hour on your PC, mostly downloads.

1. Install **Unity Hub**, then the latest **Unity 6 LTS** editor from it, with the Windows build support module (add Mac/Linux build support later for Steam).
2. Install **Git** and **Git LFS** (`git lfs install` once).
3. Install an IDE: **Visual Studio** (Community, with the "Game development with Unity" workload) or **JetBrains Rider**.
4. In Unity Hub, create a project from the **Universal 2D** template, named `TheSilence`.
5. Open it once so Unity generates its folders, then close it before the first commit.
6. Optional but recommended: install **Claude Code** on your PC and run it in the project folder, with your PixelLab connection set up there.

## Repository and source control

A new private GitHub repo holds the project; art and audio go through Git LFS so the repo stays fast.

1. Create a private repo on GitHub (e.g. `TheSilence`), with no template files.
2. In the project folder: `git init`, add the Unity `.gitignore` (ignores `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, build folders).
3. Add `.gitattributes` tracking `*.png *.psd *.aseprite *.wav *.ogg *.mp3 *.ttf *.otf` with LFS, and marking `*.unity *.prefab *.asset *.mat *.meta` as text with `merge=unityyamlmerge`.
4. In Unity: Project Settings → Editor → Asset Serialization **Force Text**, and Version Control **Visible Meta Files**.
5. First commit, push to `main`. Work on short branches per feature and merge to `main` when it plays.
6. Add the existing design material under `Docs/`: the spec, paper prototype, asset plan exports, and `Tools/rotsprite.py`.

Once the repo exists, tell me its name and I'll attach it here, set up these files and push the starting structure.

## Packages and project settings

The Universal 2D template already includes URP, 2D sprite tools and the Pixel Perfect Camera; a few packages and settings make it right for 1080p pixel art.

### Packages

| Package | Why | When |
| --- | --- | --- |
| Input System | Mouse, keyboard, controller later | Sprint 1 |
| TextMeshPro (UGUI) | Crisp pixel fonts in UI | Sprint 1 |
| Test Framework | Edit-mode tests for the rules engine | Sprint 1 |
| Newtonsoft Json | Save files and data export | Sprint 2 |
| Addressables | Loading art by key, smaller builds | M4 |
| Ink (inkle) or Yarn Spinner | Events and hail dialogue | M2–M3 |
| Steamworks.NET | Achievements, cloud saves | M5 |

### Settings

- **Game view and builds:** 1920×1080 reference resolution, windowed and fullscreen, 16:9 with letterboxing for other ratios.
- **Pixel Perfect Camera:** Assets Pixels Per Unit = 32, reference resolution 1920×1080, Upscale Render Texture off, Crop Frame off; integer scaling to 2× at 4K.
- **Sprite import preset** (applied automatically to `Art/`): Filter Mode **Point**, Compression **None**, Pixels Per Unit **32**, Mesh Type **Full Rect**, pivot **Center**, Generate Mip Maps off.
- **Normal maps:** imported as Default textures (not Normal Map type, since PixelLab already outputs the OpenGL format) and assigned as the sprite's `_NormalMap` secondary texture.
- **2D lighting:** URP 2D Renderer with one global light plus point lights for engines and weapon flashes.
- **Color space:** Linear; Quality: no anti-aliasing, VSync on.

## Code architecture

The rules live in plain C# with no Unity code, so whole battles can be tested and replayed without opening a scene; Unity only draws the state and sends the player's actions.

&#91;embedded content: code architecture · 5 layers\]

The view never changes the battle directly: it sends an action, the core applies it and emits events (ship moved, damage dealt, card drawn), and the view animates them.

### Folders and assemblies

| Assembly | Folder | Depends on |
| --- | --- | --- |
| `Silence.Core` | `Assets/_Project/Scripts/Core/` | nothing (no UnityEngine) |
| `Silence.Data` | `Assets/_Project/Scripts/Data/` | Core |
| `Silence.View` | `Assets/_Project/Scripts/View/` | Core, Data |
| `Silence.Meta` | `Assets/_Project/Scripts/Meta/` | Core, Data |
| `Silence.Editor` | `Assets/_Project/Editor/` | all, editor only (Facing Baker, data tools) |
| `Silence.Tests` | `Assets/Tests/EditMode/` | Core, Data |

Other folders: `Assets/_Project/Data/` (the ScriptableObject assets), `Assets/_Project/Art/`, `Assets/_Project/Scenes/` (Boot, Battle, Map, Menu), `Docs/`, `ArtSource/`, `Tools/`.

## Art pipeline from PixelLab into Unity

Only East-facing sprites come out of PixelLab; Unity tools do the rest, so a new ship is one generation plus a button press.

1. **Generate** the East sprite in PixelLab with the faction palette and style reference (prompts and seeds in the asset plan). Pick the best candidate.
2. **Log it** in `ArtSource/pixellab-log.csv`: asset name, job ID, candidate index, seed, prompt. Anything can then be re-run in the same style.
3. **Drop the PNG** into `Art/Ships/<Faction>/<Unit>/`; the import preset makes it pixel-perfect.
4. **Facing Baker** (an editor tool, a C# port of our RotSprite script): one click makes the NE and SE sprites at 1.25× canvas, and the Unit asset gets all six facings (W, SW, NW are 180° turns, done at runtime).
5. **Clean up** the diagonal sprites by hand where needed (Aseprite, or PixelLab's free pixel tools).
6. **Normal map** from PixelLab (0.1 each), saved beside the sprite as `<name>_n.png`.
7. **Modules:** repainted variants are cut into layers and saved per slot and tier; at a refit, the game stacks them on the East sprite and the same RotSprite code bakes the diagonals at runtime, cached per loadout.

Tools to write in sprint 1: Facing Baker (editor), Runtime Facing Baker (for refits, later), and a sprite-sheet importer for small craft and effects.

## First sprint: grey-box combat (M1)

Ten tasks in order; each ends with something you can see or test. The rules are built and tested first without any graphics, then shown on screen.

| # | Task | Done when |
| --- | --- | --- |
| 1 | Repo, packages, settings, folders, assemblies (sections above) | Project opens clean; an empty test passes |
| 2 | Hex math: offset/axial/cube coordinates, neighbors, distance, direction, arc of a hex relative to a facing | Unit tests pass for all six facings |
| 3 | Battle state and seeded RNG: ships (hull, shields per arc, armor per arc, heat, speed, facing), small craft, torpedoes, terrain | A battle can be built from data and printed |
| 4 | Turn loop: Intent → Command → Drift → Enemy action → End, with collisions, shields regen, venting | Tests replay a scripted turn exactly |
| 5 | Deck: draw, hand, discard, exhaust, command points, card effects as composable effect blocks; the 20 paper-prototype cards as data | Every card has a passing test |
| 6 | Damage, crits, junk cards, heat thresholds, ship destruction removing its cards | Tests cover each rule in the spec |
| 7 | Enemy AI: the three intent cycles (frigate, gunship wing, carrier) and paper-prototype movement rules | A full battle runs headless to a win or loss |
| 8 | Board view: hex grid, ship sprites by facing, drift ghosts, intent lines and labels, arc highlight on card select | The battle is visible and matches the mockup layout |
| 9 | Hand and HUD: cards in hand, play by click and target, command pips, ship panel, intents list, End Turn | You can play scenario 1 start to finish |
| 10 | Playtest scenarios 1 and 2 from the paper prototype; tune numbers in data | Notes from the paper-prototype checklist answered |

Tasks 2–7 are pure C# and can be written and tested before any art exists. Tasks 8–9 use the art from the next section.

## First PixelLab batch

About 120 generations covers everything sprint 1 shows on screen; 114 are already spent on art that carries over.

| Asset | Status | Est. generations |
| --- | --- | --- |
| Ascendancy lance-cruiser, facings, normal map | Done | — |
| Ordinal dart | Done | — |
| Concord trimaran (Venn's flagship) and gunship | Done | — |
| Ascendancy Blade escort, 64 px | To do | 10 |
| Concord Outrider frigate, 80 px | To do | 10 |
| Torpedo and missile, 16–24 px | To do | 10 |
| Asteroids ×4, debris, 32–64 px | To do | 10 |
| Battle background (Ion Reef sector) | To do | 25 |
| Intent icons: attack, torpedo, move, buff, spawn, unknown | To do | 10 |
| Card frame and cost badge, pixel UI | To do | 25 |
| Normal maps for the new ships | To do | 0.2 |
| Effects: shots, lance beam, explosion, shield hit, engine flame | To do | 0 (VFX workbench) |
| Sound effects: lance, battery, torpedo, explosion, shield hit, UI click | To do | \~20 |

I can run this batch from here now, or from Claude Code on your PC once it's set up.
