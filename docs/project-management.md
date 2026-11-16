# Project management

How the Witch project is planned and tracked.

## Roadmap (milestones)

| Milestone | Goal | Target |
| --- | --- | --- |
| **M1: Core loop prototype** ✅ | Drag ingredients into the cauldron, validate recipes, reaction feedback, one-click test scene | Done (Nov 2026) |
| **M2: Recipe book & HUD** | Unity project skeleton, CI, recipe book, HUD, two-ingredient reactions, content fact-check | 14 Dec 2026 |
| **M3: Harvesting & sourcing** | Gather ingredients from nature; inventory and save system | 18 Jan 2027 |
| **M4: iOS vertical slice** | Polished 10-minute slice on TestFlight | 22 Feb 2027 |
| **M5: Polish, accessibility & kid-safety** | Audio, accessibility, privacy/COPPA, App Store Kids readiness | 29 Mar 2027 |

Dates are targets, reviewed at the end of each milestone. Scope moves before dates do: if a milestone is at risk, `P2` issues move to the next one.

## Project board setup

GitHub Project (board view), linked to this repo.

**Status columns:** Backlog → Ready → In progress → In review → Done

| Column | Entry rule |
| --- | --- |
| Backlog | Any new issue |
| Ready | Has acceptance criteria, priority, milestone and size; nothing blocking it |
| In progress | Someone is assigned and has a branch (limit: 2 per person) |
| In review | PR open and linked with `Closes #` |
| Done | PR merged |

**Custom fields**

- *Priority* (single select): P0, P1, P2. Mirrors the label.
- *Size* (single select): XS (<2h), S (half day), M (1-2 days), L (3-5 days). Anything bigger is split.
- *Iteration* (iteration field): 2-week sprints starting Monday 16 Nov 2026.

**Views**

1. *Board*: grouped by Status, filtered to the current iteration.
2. *Backlog*: table sorted by Priority then Milestone.
3. *Roadmap*: roadmap layout by Milestone / Iteration.

**Built-in workflows to enable:** item added → Backlog; PR opened → In review; item closed / PR merged → Done; auto-add new issues from `kinodirektori-png/Witch`.

## Branch protection for `main` (repo admin)

Settings → Branches → Add rule for `main` (or Settings → Rules → Rulesets):

- Require a pull request before merging, with 1 approval and stale approvals dismissed on new commits
- Require status checks: `Preflight`, and once issue #11 and #12 are done, `EditMode and PlayMode tests`
- Require conversation resolution
- Block force pushes and deletions

Also in Settings → General: enable **Automatically delete head branches** and allow **Squash merging** as the default.

## CI

`.github/workflows/unity-ci.yml` uses [GameCI](https://game.ci) to run all Unity tests and build an iOS Xcode project on every PR to `main`. It skips the Unity jobs with a warning until:

1. the Unity project skeleton is committed (#11), and
2. the owner adds `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` secrets ([activation guide](https://game.ci/docs/github/activation)) (#12).

## Cadence

- **Weekly:** groom the backlog (new issues get priority, milestone, size; Ready column has at least a week of work).
- **Every 2 weeks:** sprint review with a short demo video in the milestone, then plan the next iteration.
- **End of milestone:** close the milestone, write release notes (GitHub Release, tag `m2`, `m3`, ...), and a short retro in the milestone description.

## Retro: M1

- ✅ All 4 planned issues plus the test scene delivered in one day; one PR per issue.
- ⚠️ PR #8 was merged into #7's branch instead of `main`. No code was lost (it reached `main` via #7), but issue links and history were harder to follow. **Action:** PR template now has a "base branch is `main`" check, and CONTRIBUTING.md documents the stacked-PR rule.
- ⚠️ No labels/milestones on PRs, and no CI. **Action:** label scheme, templates and CI workflow added.
- ⚠️ The code has not yet been compiled in a committed Unity project. **Action:** #11 (P0) for M2.
