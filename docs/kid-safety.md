# Kid safety and privacy checklist

How Medea meets the App Store Kids Category, COPPA and the "Data Not Collected" privacy label (#22). Items marked
**test** are checked by `Assets/Tests/EditMode/KidSafetyTests.cs` and `DataAssetTests.cs`, so a change that breaks
them fails the test run.

## App Store privacy label

In App Store Connect > App Privacy, answer **"No, we do not collect data from this app."** The label then shows
**Data Not Collected**. This stays true while the checks below pass.

## Checklist

| Requirement | How Medea meets it | Checked by |
| --- | --- | --- |
| No third-party ads | No ads package in `Packages/manifest.json` | **test** `NoAdsOrAnalyticsPackages` |
| No third-party analytics or tracking | No analytics or diagnostics packages; no analytics, device id, ad id, location, camera or microphone APIs in game code | **test** `NoTrackingOrNetworkApisInGameCode` |
| No network use while playing | No `UnityWebRequest` in game code | **test** `NoTrackingOrNetworkApisInGameCode` |
| No links or purchases without a parental gate (Guideline 1.3) | Links only open through `ExternalLink`, which asks `ParentalGate` (multiply two numbers written as words, one try) | **test** `LinksOnlyOpenThroughTheParentalGate`, `ParentalGate_*` |
| No in-app purchases | No purchasing package | **test** `NoAdsOrAnalyticsPackages` |
| Saves contain no personal data | Save file holds only recipe/page names, counts and the current recipe, stored locally | **test** `SaveData_HasNoPersonalFields` |
| Dangerous real-world steps carry an "ask an adult" note | `RecipeData.safetyNote` shown in the formula popup and the recipe book; lore pages have their own safety note | **test** `EveryHeatedRecipe_HasASafetyNote` |
| Privacy policy | Draft in [privacy-policy.md](privacy-policy.md); publish it and add the link in App Store Connect | Manual |
| Age rating | Choose the Kids Category age band (likely 6-8 or 9-11) and answer the content questions with "None" | Manual |
| Content is age-appropriate | Kitchen-safe substances only (salt, lemon, baking soda, flour, water); facts reviewed in #17 | Manual (#17) |

## Before each release

1. Run the EditMode tests (Window > General > Test Runner). All kid-safety tests must pass.
2. Check `Packages/manifest.json` for new packages and read what any new package sends over the network.
3. Read new chemistry content: anything heated, sharp, stinging or not safe to eat needs a safety note.
4. If a link was added, try it: the grown-up question must appear first and a wrong answer must close it.

## Adding a link later

1. Build a full-screen panel with a `CanvasGroup`, a question label, a `TMP_InputField` and OK / Cancel buttons, and
   add the `ParentalGate` component to it with those references.
2. Add `ExternalLink` to the button, set its https address and gate, and hook the button's OnClick to
   `ExternalLink.Open`.
