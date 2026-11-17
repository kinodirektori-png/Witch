# Witch

[![Unity CI](https://github.com/kinodirektori-png/Witch/actions/workflows/unity-ci.yml/badge.svg)](https://github.com/kinodirektori-png/Witch/actions/workflows/unity-ci.yml)

A 2D educational mobile game (Unity 2022.3 LTS, iOS) where a young witch harvests everyday natural
ingredients (lemons, salt, minerals), combines them on her desk and in the cauldron, and discovers the
real chemistry behind the "magic": scientific names, chemical formulas and reactions.

## Quick start

1. Install Unity **2022.3.62f2** through Unity Hub (add the iOS Build Support module if you build for iPhone).
   Any 2022.3 LTS patch also works; Hub just asks to switch versions.
1. In Unity Hub, choose **Add > Add project from disk** and pick the cloned repo folder (the one that
   contains `Assets/`, `Packages/` and `ProjectSettings/`), then open it. The first import takes a few minutes.
2. Run **Witch > Build MainLab Test Scene** from the menu bar. If prompted, import TMP Essentials and run it again.
3. Set the Game view to a portrait resolution (e.g. 1080x1920), press Play, and drag the requested ingredient into the cauldron.

## Project layout

| Folder | Contents |
| --- | --- |
| `Assets/Scripts/Data` | ScriptableObjects (`RecipeData`, ...) |
| `Assets/Scripts/Gameplay` | Drag and drop, cauldron and reaction logic |
| `Assets/Scripts/UI` | Recipe book, HUD, popups |
| `Assets/Scripts/Editor` | Editor tools (e.g. the MainLab scene builder) |
| `Assets/Prefabs` | Prefabs |
| `Assets/Scenes` | Scenes (`MainLab`) |

## Roadmap

| Milestone | Goal | Target |
| --- | --- | --- |
| [M1: Core loop prototype](https://github.com/kinodirektori-png/Witch/milestone/1?closed=1) ✅ | Drag, brew, reaction feedback, test scene | Done |
| [M2: Recipe book & HUD](https://github.com/kinodirektori-png/Witch/milestone/2) | Project skeleton, CI, recipe book, HUD, two-ingredient reactions | Dec 2026 |
| [M3: Harvesting & sourcing](https://github.com/kinodirektori-png/Witch/milestone/3) | Gather ingredients from nature, save system | Jan 2027 |
| [M4: iOS vertical slice](https://github.com/kinodirektori-png/Witch/milestone/4) | Polished slice on TestFlight | Feb 2027 |
| [M5: Polish, accessibility & kid-safety](https://github.com/kinodirektori-png/Witch/milestone/5) | Audio, accessibility, privacy review | Mar 2027 |

Details, board setup and cadence: [docs/project-management.md](docs/project-management.md).

## Contributing

Work is tracked in GitHub Issues (one PR per issue, targeting `main`). See [CONTRIBUTING.md](CONTRIBUTING.md) for branching, the Definition of Done, labels and code conventions.
