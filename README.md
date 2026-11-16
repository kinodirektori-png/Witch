# Witch

A 2D educational mobile game (Unity 2022.3 LTS, iOS) where a young witch harvests everyday natural
ingredients (lemons, salt, minerals), combines them on her desk and in the cauldron, and discovers the
real chemistry behind the "magic": scientific names, chemical formulas and reactions.

## Quick start

1. Open the project in Unity 2022.3 LTS.
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

## Conventions

- PascalCase for classes and public members, camelCase for private fields.
- Small, single-purpose components (e.g. dragging is separate from reaction checking).
- Work is tracked in GitHub Issues; one PR per issue.
