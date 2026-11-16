# Contributing to Witch

Thanks for helping kids discover the chemistry behind everyday "magic". This page explains how work moves from idea to `main`.

## Workflow at a glance

1. **Issue first.** Every change starts from an issue made with one of the templates (Feature, Bug, Chemistry content). It has a user story, testable acceptance criteria, a priority label and a milestone.
2. **Pick it up.** Move the card to *In progress* on the project board and assign yourself.
3. **Branch from `main`.** Name it `<type>/<issue>-<short-name>`, e.g. `feature/14-recipe-book`, `fix/31-drag-snap`, `chore/12-ci`.
4. **One PR per issue, targeting `main`.** Fill in the PR template and write `Closes #<issue>` so the issue closes on merge.
5. **Review.** At least one approval and green CI before merging. Use *Squash and merge* so `main` has one commit per issue.
6. **Done.** The branch is deleted automatically and the card moves to *Done*.

### Stacked PRs

Avoid them. If PR B truly depends on unmerged PR A, say so in both PR descriptions and **only merge B after A has merged and B has been retargeted to `main`**. (Lesson learned from #8, which merged into #7's branch instead of `main`. The code still reached `main` through #7, but the history and the issue links were harder to follow.)

## Definition of Done

An issue is done when:

- [ ] Every acceptance criterion is ticked
- [ ] Code compiles in Unity 2022.3 LTS without new warnings and CI is green
- [ ] Tests cover new logic (EditMode for pure logic, PlayMode for interactions)
- [ ] Inspector/scene setup steps are in the PR description, or the scene builder is updated
- [ ] Chemistry facts are checked against a reference and match `docs/chemistry-content.md`
- [ ] Anything visible to players has a screenshot or video in the PR
- [ ] It works in portrait on a phone-sized Game view

## Code conventions

- PascalCase for classes, public methods and properties; camelCase for private fields (`[SerializeField] private float dragAlpha`).
- Namespaces match folders: `Witch.Data`, `Witch.Gameplay`, `Witch.UI`, `Witch.Editor`.
- Small, single-purpose components: dragging (`DraggableItem`) is separate from deciding what a drop means (`IItemDropReceiver`).
- UI drag and drop must handle raycasts correctly: use a `CanvasGroup` to disable `blocksRaycasts` while dragging.
- Data lives in ScriptableObjects under `Assets/Scripts/Data`; content assets under `Assets/Data`.

## Labels

| Group | Labels | Meaning |
| --- | --- | --- |
| Priority | `P0: critical`, `P1: high`, `P2: normal` | P0 blocks the current milestone |
| Area | `gameplay`, `UI`, `visuals`, `audio`, `chemistry`, `ios`, `tooling`, `tests` | Which part of the game |
| Quality | `accessibility`, `kid-safety`, `polish` | Cross-cutting concerns |
| Type | `enhancement`, `bug`, `documentation` | What kind of change |

Every issue gets one priority label and at least one area label.

## Kid-safety rules (non-negotiable)

- No ads, tracking, analytics SDKs or accounts.
- No external links without a parental gate.
- Any reaction that could be dangerous to copy at home shows an "ask an adult / don't try at home" note.

See [docs/project-management.md](docs/project-management.md) for milestones, the project board and release process.
