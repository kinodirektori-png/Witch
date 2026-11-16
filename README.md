# Witch

A 2D educational mobile game (Unity 2022.3 LTS, iOS) where a young witch harvests everyday natural
ingredients (lemons, salt, minerals), combines them on her desk and in the cauldron, and discovers the
real chemistry behind the "magic": scientific names, chemical formulas and reactions.

## Project layout

| Folder | Contents |
| --- | --- |
| `Assets/Scripts/Data` | ScriptableObjects (`RecipeData`, ...) |
| `Assets/Scripts/Gameplay` | Drag and drop, cauldron and reaction logic |
| `Assets/Scripts/UI` | Recipe book, HUD, popups |
| `Assets/Prefabs` | Prefabs |
| `Assets/Scenes` | Scenes (`MainLab`) |

## Conventions

- PascalCase for classes and public members, camelCase for private fields.
- Small, single-purpose components (e.g. dragging is separate from reaction checking).
- Work is tracked in GitHub Issues; one PR per issue.
