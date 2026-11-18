using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// Lives on the cauldron. Keeps a recipe dictionary (ingredient combination -> <see cref="RecipeData"/>), remembers
    /// what has been dropped in so far, and brews a recipe once the contents match it. Put it next to an
    /// <see cref="ItemDropZone"/>: ingredients that fit are accepted, ones that don't snap back to the shelf.
    /// </summary>
    /// <remarks>
    /// Drag-and-drop stays in <see cref="DraggableItem"/> / <see cref="ItemDropZone"/>; this class only decides
    /// what a dropped ingredient means. Visuals and HUD listen to its events.
    /// Kitchen items (with an <see cref="ItemState"/>) are poured into the <see cref="MixingVessel"/>: water dilutes,
    /// an ingredient in the wrong form is refused with a hint, and a finished recipe whose <see cref="BrewConditions"/>
    /// aren't met yet (needs water or heat) waits and brews as soon as the mixture changes to meet them.
    /// Items without an <see cref="ItemState"/> behave exactly as before.
    /// </remarks>
    public class CauldronManager : MonoBehaviour, IItemDropReceiver
    {
        /// <summary>Which recipes a dropped ingredient is checked against.</summary>
        public enum BrewingMode
        {
            /// <summary>Guided: only ingredients for the active recipe are accepted, recipes are brewed in order.</summary>
            ActiveRecipe,
            /// <summary>Free brewing: anything that is part of a known recipe is accepted; any finished recipe brews.</summary>
            AnyKnownRecipe
        }

        [Tooltip("The recipe dictionary. Each recipe's Reaction reactants (or its Ingredients list) is its combination; order " +
                 "does not matter unless the reaction says so. A recipe without either is brewed by dropping it in on its own. " +
                 "In Active Recipe mode they are brewed in this order.")]
        [SerializeField] private List<RecipeData> recipes = new List<RecipeData>();

        [Tooltip("Active Recipe: only accept ingredients for the current goal. Any Known Recipe: free brewing.")]
        [SerializeField] private BrewingMode brewingMode = BrewingMode.ActiveRecipe;

        [Tooltip("Index into Recipes of the recipe the cauldron currently wants.")]
        [SerializeField, Min(0)] private int activeRecipeIndex;

        [Tooltip("Move on to the next recipe after the active recipe is brewed.")]
        [SerializeField] private bool advanceOnSuccess = true;

        [Tooltip("Start again from the first recipe after the last one is brewed.")]
        [SerializeField] private bool loopRecipes = true;

        [Tooltip("Hide each ingredient once the cauldron accepts it. Turn off to keep ingredients reusable (e.g. for testing).")]
        [FormerlySerializedAs("consumeIngredientOnSuccess")]
        [SerializeField] private bool consumeIngredientsOnDrop = true;

        [Header("Kitchen")]
        [Tooltip("Holds what has been poured in (forms, water, heat). Defaults to one on this GameObject. Optional: " +
                 "without it, recipes that need water or heat can't be brewed.")]
        [SerializeField] private MixingVessel vessel;

        [Tooltip("Seconds the brewed mixture stays visible in the pot before it is cleared for the next recipe.")]
        [SerializeField, Min(0f)] private float clearVesselDelay = 2.5f;

        [Tooltip("Hint shown when an empty container is dropped in.")]
        [SerializeField] private string emptyContainerHint = "It's empty! Fill it up first.";

        [Tooltip("Hint shown when something is dropped in while a finished mixture waits for water or heat.")]
        [SerializeField] private string waitingForConditionFormat = "Almost there! {0}";

        [Header("Events")]
        [Tooltip("Raised after a successful reaction with the recipe that was brewed.")]
        public UnityEvent<RecipeData> onSuccessfulReaction = new UnityEvent<RecipeData>();

        [Tooltip("Raised when an ingredient is accepted but the recipe is not finished yet.")]
        public UnityEvent<RecipeData> onIngredientAdded = new UnityEvent<RecipeData>();

        [Tooltip("Raised when a wrong or unknown ingredient is dropped in.")]
        public UnityEvent<GameObject> onFailedReaction = new UnityEvent<GameObject>();

        [Tooltip("Raised with a kid hint when an item is refused for a reason (wrong form, empty glass).")]
        public UnityEvent<string> onIngredientRejected = new UnityEvent<string>();

        [Tooltip("Raised with a kid hint when the ingredients are complete but water or heat is still missing.")]
        public UnityEvent<string> onReactionWaiting = new UnityEvent<string>();

        /// <summary>C# event for code listeners (effects, HUD, recipe book). Same timing as onSuccessfulReaction.</summary>
        public event Action<RecipeData> SuccessfulReaction;

        /// <summary>Raised when the active recipe changes.</summary>
        public event Action<RecipeData> ActiveRecipeChanged;

        /// <summary>Raised whenever an ingredient is added or the cauldron is emptied (including after a brew).</summary>
        public event Action<IReadOnlyList<RecipeData>> ContentsChanged;

        /// <summary>Raised when an item is refused with a reason. Passes the item and a kid hint.</summary>
        public event Action<GameObject, string> IngredientRejected;

        /// <summary>Raised when a complete recipe waits for water or heat. Passes the recipe and a kid hint.</summary>
        public event Action<RecipeData, string> ReactionWaiting;

        private readonly List<RecipeData> contents = new List<RecipeData>();
        private RecipeDatabase database;
        private Coroutine clearVesselRoutine;

        /// <summary>The recipe the cauldron currently wants, or null if there is none.</summary>
        public RecipeData ActiveRecipe =>
            activeRecipeIndex >= 0 && activeRecipeIndex < recipes.Count ? recipes[activeRecipeIndex] : null;

        /// <summary>The most recently brewed recipe, or null before the first success.</summary>
        public RecipeData LastBrewedRecipe { get; private set; }

        /// <summary>How strong the mixture was at the last brew (0..1). Effects scale with it; 1 without a vessel.</summary>
        public float LastBrewConcentration { get; private set; } = 1f;

        /// <summary>A recipe whose ingredients are all in but which waits for water or heat, or null.</summary>
        public RecipeData PendingRecipe { get; private set; }

        /// <summary>What <see cref="PendingRecipe"/> is waiting for, as a kid hint (e.g. "Turn on the heat!").</summary>
        public string PendingHint { get; private set; } = string.Empty;

        /// <summary>The hint for the last refused item, or empty if it was simply the wrong ingredient.</summary>
        public string LastRejectHint { get; private set; } = string.Empty;

        public MixingVessel Vessel => vessel;

        public IReadOnlyList<RecipeData> Recipes => recipes;

        /// <summary>Ingredients currently in the cauldron, in the order they were added.</summary>
        public IReadOnlyList<RecipeData> Contents => contents;

        public BrewingMode Mode
        {
            get => brewingMode;
            set
            {
                brewingMode = value;
                EmptyCauldron();
            }
        }

        /// <summary>The recipe dictionary, built from <see cref="Recipes"/> on first use.</summary>
        public RecipeDatabase Database
        {
            get
            {
                if (database == null)
                {
                    RebuildDatabase();
                }

                return database;
            }
        }

        private void Awake()
        {
            if (vessel == null)
            {
                vessel = GetComponent<MixingVessel>();
            }

            RebuildDatabase();
        }

        private void OnEnable()
        {
            if (vessel != null)
            {
                vessel.MixtureChanged += HandleVesselChanged;
                vessel.TemperatureChanged += HandleTemperatureChanged;
            }
        }

        private void OnDisable()
        {
            if (vessel != null)
            {
                vessel.MixtureChanged -= HandleVesselChanged;
                vessel.TemperatureChanged -= HandleTemperatureChanged;
            }

            if (clearVesselRoutine != null)
            {
                StopCoroutine(clearVesselRoutine);
                clearVesselRoutine = null;
            }
        }

        /// <summary>Rebuilds the recipe dictionary. Call after changing <see cref="Recipes"/> or recipe ingredients at runtime.</summary>
        public void RebuildDatabase()
        {
            database = new RecipeDatabase(recipes);
            database.LogWarnings(this);
        }

        /// <summary>Adds a recipe to the dictionary at runtime (e.g. one unlocked in the recipe book).</summary>
        public void AddRecipe(RecipeData recipe)
        {
            if (recipe == null || recipes.Contains(recipe))
            {
                return;
            }

            recipes.Add(recipe);
            RebuildDatabase();
        }

        /// <summary>
        /// Checks the ingredient on <paramref name="droppedItem"/> (via its <see cref="ItemState"/> or
        /// <see cref="IngredientItem"/>) against the recipe dictionary. Returns true if the cauldron accepts it: water
        /// that dilutes the mixture, an ingredient that brings the contents one step closer to a recipe, or one that
        /// completes it (brewing now, or waiting for water/heat). Returns false (and the item snaps back) otherwise.
        /// </summary>
        public bool CheckReaction(GameObject droppedItem)
        {
            ItemState itemState = droppedItem != null ? droppedItem.GetComponent<ItemState>() : null;
            if (itemState != null)
            {
                if (itemState.IsEmpty)
                {
                    Reject(droppedItem, emptyContainerHint);
                    return false;
                }

                if (itemState.IsWater)
                {
                    return TryAddWater(droppedItem, itemState);
                }

                if (itemState.IsSqueezed)
                {
                    Reject(droppedItem, "There's no juice left in this one.");
                    return false;
                }
            }

            RecipeData ingredient = GetIngredient(droppedItem);
            if (ingredient == null)
            {
                OnFailedReaction(droppedItem);
                return false;
            }

            if (PendingRecipe != null)
            {
                // The pot already holds a finished mixture; more ingredients would only confuse what is missing.
                Reject(droppedItem, string.Format(waitingForConditionFormat, PendingHint));
                return false;
            }

            // What the cauldron would hold if we accepted this ingredient.
            var candidate = new List<RecipeData>(contents) { ingredient };

            if (!TryMatch(candidate, out RecipeData matchedRecipe))
            {
                OnFailedReaction(droppedItem);
                return false;
            }

            if (itemState != null && !PassesStateRules(candidate, ingredient, itemState, out string stateHint))
            {
                Reject(droppedItem, stateHint);
                return false;
            }

            contents.Add(ingredient);
            PourIntoVessel(droppedItem, itemState, ingredient);

            if (consumeIngredientsOnDrop)
            {
                droppedItem.SetActive(false);
            }

            if (matchedRecipe == null)
            {
                // On the right track, but the recipe needs more ingredients.
                OnIngredientAdded(ingredient);
                ContentsChanged?.Invoke(contents);
                return true;
            }

            BrewOrWait(matchedRecipe);
            return true;
        }

        bool IItemDropReceiver.TryReceiveItem(GameObject droppedItem) => CheckReaction(droppedItem);

        /// <summary>Ingredients still needed for <paramref name="recipe"/> (defaults to the active recipe).</summary>
        public List<RecipeData> GetMissingIngredients(RecipeData recipe = null)
        {
            return RecipeDatabase.GetMissingIngredients(contents, recipe != null ? recipe : ActiveRecipe);
        }

        /// <summary>Throws away whatever is in the cauldron (and its vessel) without brewing.</summary>
        public void EmptyCauldron()
        {
            bool hadContents = contents.Count > 0 || PendingRecipe != null;
            contents.Clear();
            ClearPending();

            // Right after a brew the vessel is cleared on a timer so kids can still see the result.
            if (vessel != null && clearVesselRoutine == null)
            {
                vessel.Empty();
            }

            if (hadContents)
            {
                ContentsChanged?.Invoke(contents);
            }
        }

        /// <summary>Makes the recipe at <paramref name="index"/> the active one.</summary>
        public void SetActiveRecipe(int index)
        {
            if (recipes.Count == 0)
            {
                activeRecipeIndex = 0;
                return;
            }

            activeRecipeIndex = Mathf.Clamp(index, 0, recipes.Count - 1);

            // Half-finished ingredients for the old goal don't count towards the new one.
            if (brewingMode == BrewingMode.ActiveRecipe)
            {
                EmptyCauldron();
            }

            ActiveRecipeChanged?.Invoke(ActiveRecipe);
        }

        /// <summary>Makes <paramref name="recipe"/> the active one if it is in the list.</summary>
        public void SetActiveRecipe(RecipeData recipe)
        {
            int index = recipes.IndexOf(recipe);
            if (index >= 0)
            {
                SetActiveRecipe(index);
            }
        }

        /// <summary>
        /// Placeholder hook for a successful brew. Override in a subclass for custom behaviour, or listen to
        /// <see cref="onSuccessfulReaction"/> / <see cref="SuccessfulReaction"/> (e.g. particle effects).
        /// Overrides should call base so listeners are still notified.
        /// </summary>
        protected virtual void OnSuccessfulReaction(RecipeData matchedRecipe)
        {
            Debug.Log($"Cauldron brewed {matchedRecipe.commonName} ({matchedRecipe.technicalName}, {matchedRecipe.chemicalFormula}).", this);
            SuccessfulReaction?.Invoke(matchedRecipe);
            onSuccessfulReaction.Invoke(matchedRecipe);
        }

        /// <summary>Hook for an accepted ingredient that doesn't finish a recipe yet. Override for custom feedback.</summary>
        protected virtual void OnIngredientAdded(RecipeData ingredient)
        {
            onIngredientAdded.Invoke(ingredient);
        }

        /// <summary>Placeholder hook for a wrong ingredient. Override for custom feedback.</summary>
        protected virtual void OnFailedReaction(GameObject droppedItem)
        {
            LastRejectHint = string.Empty; // a plain wrong ingredient: listeners show their generic message
            onFailedReaction.Invoke(droppedItem);
        }

        /// <summary>Hook for a complete recipe that waits for water or heat. Override for custom feedback.</summary>
        protected virtual void OnReactionWaiting(RecipeData recipe, string hint)
        {
            ReactionWaiting?.Invoke(recipe, hint);
            onReactionWaiting.Invoke(hint);
        }

        /// <summary>Refuses an item with a reason. Also counts as a failed try (for hints and HUD feedback).</summary>
        private void Reject(GameObject droppedItem, string hint)
        {
            // Set before OnFailedReaction so listeners (the HUD) can show the specific hint instead of a generic one.
            LastRejectHint = hint ?? string.Empty;
            IngredientRejected?.Invoke(droppedItem, LastRejectHint);
            onIngredientRejected.Invoke(LastRejectHint);

            onFailedReaction.Invoke(droppedItem);
        }

        /// <summary>
        /// Decides whether <paramref name="candidate"/> contents are allowed and whether they finish a recipe.
        /// Returns false if the contents fit no allowed recipe. <paramref name="matchedRecipe"/> is the finished
        /// recipe, or null if more ingredients are needed.
        /// </summary>
        private bool TryMatch(List<RecipeData> candidate, out RecipeData matchedRecipe)
        {
            matchedRecipe = null;
            RecipeDatabase db = Database;

            if (brewingMode == BrewingMode.ActiveRecipe)
            {
                RecipeData active = ActiveRecipe;
                if (active == null || !db.Contains(active) || !RecipeDatabase.CanStillBecome(candidate, active))
                {
                    return false;
                }

                if (RecipeDatabase.IsComplete(candidate, active))
                {
                    matchedRecipe = active;
                }

                return true;
            }

            // Free brewing: an exact dictionary hit brews straight away, even if a bigger recipe also starts this way.
            if (db.TryGetRecipe(candidate, out RecipeData found))
            {
                matchedRecipe = found;
                return true;
            }

            return db.IsPartOfAnyRecipe(candidate);
        }

        private void BrewOrWait(RecipeData matchedRecipe)
        {
            ConditionResult result = BrewConditionEvaluator.Evaluate(matchedRecipe, vessel != null ? vessel.Mixture : null);
            if (result.IsMet)
            {
                Brew(matchedRecipe);
                return;
            }

            if (vessel == null && (matchedRecipe.conditions.needsWater || matchedRecipe.conditions.needsHeat))
            {
                Debug.LogWarning($"Recipe '{matchedRecipe.name}' needs water or heat, but cauldron '{name}' has no " +
                                 $"{nameof(MixingVessel)}. Add one so it can be brewed.", this);
            }

            PendingRecipe = matchedRecipe;
            PendingHint = result.Hint;
            ContentsChanged?.Invoke(contents);
            OnReactionWaiting(matchedRecipe, result.Hint);
        }

        private void TryBrewPending()
        {
            RecipeData pending = PendingRecipe;
            if (pending == null)
            {
                return;
            }

            ConditionResult result = BrewConditionEvaluator.Evaluate(pending, vessel != null ? vessel.Mixture : null);
            if (result.IsMet)
            {
                Brew(pending);
                return;
            }

            if (result.Hint != PendingHint)
            {
                // e.g. water was added, now it needs heat: tell the player the next step.
                PendingHint = result.Hint;
                ContentsChanged?.Invoke(contents);
                OnReactionWaiting(pending, result.Hint);
            }
        }

        private void ClearPending()
        {
            PendingRecipe = null;
            PendingHint = string.Empty;
        }

        private void HandleVesselChanged(MixingVessel changed) => TryBrewPending();

        private void HandleTemperatureChanged(float temperatureC) => TryBrewPending();

        private bool TryAddWater(GameObject droppedItem, ItemState water)
        {
            if (vessel == null)
            {
                Reject(droppedItem, "This pot can't hold water.");
                return false;
            }

            PrepareVesselForPour();
            KitchenItemData data = water.Contents;
            PlayPourFeedback(droppedItem, water);
            vessel.AddWater(data != null ? data.pourAmount : 1f); // pending recipes re-check via MixtureChanged
            return true;
        }

        /// <summary>
        /// True if <paramref name="ingredient"/> is in an acceptable form for at least one recipe the contents could
        /// still become. <paramref name="hint"/> explains the first problem otherwise.
        /// </summary>
        private bool PassesStateRules(List<RecipeData> candidate, RecipeData ingredient, ItemState itemState, out string hint)
        {
            hint = string.Empty;
            bool anyRecipe = false;

            IEnumerable<RecipeData> targets = brewingMode == BrewingMode.ActiveRecipe
                ? (IEnumerable<RecipeData>)new[] { ActiveRecipe }
                : Database.Recipes;

            foreach (RecipeData recipe in targets)
            {
                if (recipe == null || !RecipeDatabase.CanStillBecome(candidate, recipe))
                {
                    continue;
                }

                anyRecipe = true;
                ConditionResult result = BrewConditionEvaluator.CheckIngredient(recipe, ingredient, itemState.State, itemState.Contents);
                if (result.IsMet)
                {
                    return true;
                }

                if (string.IsNullOrEmpty(hint))
                {
                    hint = result.Hint;
                }
            }

            return !anyRecipe;
        }

        private void PourIntoVessel(GameObject droppedItem, ItemState itemState, RecipeData ingredient)
        {
            if (vessel != null)
            {
                PrepareVesselForPour();
                KitchenItemData data = itemState != null ? itemState.Contents : null;
                PhysicalState state = itemState != null ? itemState.State : PhysicalState.Solid;
                float amount = data != null ? data.pourAmount : 1f;
                Color tint = data != null ? data.placeholderColor : Color.white;
                vessel.AddSubstance(ingredient, state, amount, tint);
            }

            PlayPourFeedback(droppedItem, itemState);
        }

        private static void PlayPourFeedback(GameObject droppedItem, ItemState itemState)
        {
            if (droppedItem == null)
            {
                return;
            }

            var pour = droppedItem.GetComponent<PourSource>();
            if (pour != null)
            {
                pour.PlayPour();
            }

            if (itemState != null && itemState.Contents != null && itemState.Contents.emptiesWhenPoured)
            {
                itemState.Empty();
            }
        }

        /// <summary>A pour right after a brew first clears the old result so it doesn't mix into the next recipe.</summary>
        private void PrepareVesselForPour()
        {
            if (clearVesselRoutine != null)
            {
                StopCoroutine(clearVesselRoutine);
                clearVesselRoutine = null;
                vessel.Empty();
            }
        }

        private IEnumerator ClearVesselAfterDelay()
        {
            yield return new WaitForSeconds(clearVesselDelay);
            clearVesselRoutine = null;
            if (vessel != null && contents.Count == 0)
            {
                vessel.Empty();
            }
        }

        private void Brew(RecipeData matchedRecipe)
        {
            LastBrewedRecipe = matchedRecipe;
            LastBrewConcentration = vessel != null && vessel.Mixture.SubstanceParts > 0f ? vessel.Concentration : 1f;
            contents.Clear();
            ClearPending();

            if (vessel != null)
            {
                if (clearVesselRoutine != null)
                {
                    StopCoroutine(clearVesselRoutine);
                }

                // Keep the result visible for a moment so kids can see the colour, then clear it for the next recipe.
                clearVesselRoutine = isActiveAndEnabled && clearVesselDelay > 0f ? StartCoroutine(ClearVesselAfterDelay()) : null;
                if (clearVesselRoutine == null)
                {
                    vessel.Empty();
                }
            }

            ContentsChanged?.Invoke(contents);

            OnSuccessfulReaction(matchedRecipe);

            if (advanceOnSuccess && matchedRecipe == ActiveRecipe)
            {
                AdvanceRecipe();
            }
        }

        private void AdvanceRecipe()
        {
            if (recipes.Count == 0)
            {
                return;
            }

            int next = activeRecipeIndex + 1;
            if (next >= recipes.Count)
            {
                if (!loopRecipes)
                {
                    return;
                }

                next = 0;
            }

            SetActiveRecipe(next);
        }

        private static RecipeData GetIngredient(GameObject item)
        {
            if (item == null)
            {
                return null;
            }

            var ingredient = item.GetComponent<IngredientItem>();
            if (ingredient != null)
            {
                return ingredient.Recipe;
            }

            var itemState = item.GetComponent<ItemState>();
            return itemState != null ? itemState.Substance : null;
        }

        private void OnValidate()
        {
            if (recipes.Count > 0)
            {
                activeRecipeIndex = Mathf.Clamp(activeRecipeIndex, 0, recipes.Count - 1);
            }

            // Pick up Inspector edits made during Play mode.
            if (Application.isPlaying)
            {
                database = null;
            }
        }
    }
}
