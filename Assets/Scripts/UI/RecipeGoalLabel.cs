using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.UI
{
    /// <summary>
    /// HUD text that tells the player which ingredient the cauldron wants next, and gives friendly feedback
    /// when a wrong ingredient is dropped in. For recipes that need several ingredients it also shows progress,
    /// e.g. "Brew: Fizzy Bubbles (2/4)".
    /// </summary>
    public class RecipeGoalLabel : MonoBehaviour
    {
        [SerializeField] private CauldronManager cauldron;
        [SerializeField] private TMP_Text label;

        [Tooltip("{0} is replaced by the active recipe's common name.")]
        [SerializeField] private string goalFormat = "Brew: <b>{0}</b>";

        [Tooltip("Appended for multi-ingredient recipes. {0} = ingredients added, {1} = ingredients needed. Leave empty to hide.")]
        [SerializeField] private string progressFormat = " ({0}/{1})";

        [SerializeField] private string allDoneText = "All potions brewed!";
        [SerializeField] private string wrongIngredientText = "Hmm, that's not it. Try another one!";

        [Tooltip("Seconds the wrong-ingredient message stays before the goal comes back.")]
        [SerializeField, Min(0f)] private float feedbackDuration = 1.5f;

        private Coroutine feedbackRoutine;

        private void Awake()
        {
            if (label == null)
            {
                label = GetComponent<TMP_Text>();
            }
        }

        private void OnEnable()
        {
            if (cauldron == null)
            {
                Debug.LogWarning($"{nameof(RecipeGoalLabel)} on '{name}' has no {nameof(CauldronManager)} assigned.", this);
                return;
            }

            cauldron.ActiveRecipeChanged += HandleActiveRecipeChanged;
            cauldron.ContentsChanged += HandleContentsChanged;
            cauldron.onFailedReaction.AddListener(HandleFailedReaction);
            ShowGoal(cauldron.ActiveRecipe);
        }

        private void OnDisable()
        {
            if (cauldron == null)
            {
                return;
            }

            cauldron.ActiveRecipeChanged -= HandleActiveRecipeChanged;
            cauldron.ContentsChanged -= HandleContentsChanged;
            cauldron.onFailedReaction.RemoveListener(HandleFailedReaction);
        }

        private void HandleActiveRecipeChanged(RecipeData recipe)
        {
            StopFeedback();
            ShowGoal(recipe);
        }

        private void HandleContentsChanged(IReadOnlyList<RecipeData> contents)
        {
            // Don't cut the wrong-ingredient message short; the goal (with progress) comes back after it.
            if (feedbackRoutine == null)
            {
                ShowGoal(cauldron.ActiveRecipe);
            }
        }

        private void HandleFailedReaction(GameObject droppedItem)
        {
            StopFeedback();
            feedbackRoutine = StartCoroutine(FeedbackRoutine());
        }

        private IEnumerator FeedbackRoutine()
        {
            SetText(wrongIngredientText);
            yield return new WaitForSecondsRealtime(feedbackDuration);
            feedbackRoutine = null;
            ShowGoal(cauldron.ActiveRecipe);
        }

        private void StopFeedback()
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
                feedbackRoutine = null;
            }
        }

        private void ShowGoal(RecipeData recipe)
        {
            if (recipe == null)
            {
                SetText(allDoneText);
                return;
            }

            string text = string.Format(goalFormat, recipe.commonName);

            if (recipe.IsCombination && !string.IsNullOrEmpty(progressFormat))
            {
                int needed = RecipeDatabase.GetRequiredIngredients(recipe).Count;
                // In free-brew mode the contents may belong to another recipe; then nothing counts towards this one.
                int added = RecipeDatabase.CanStillBecome(cauldron.Contents, recipe)
                    ? needed - cauldron.GetMissingIngredients(recipe).Count
                    : 0;
                text += string.Format(progressFormat, added, needed);
            }

            SetText(text);
        }

        private void SetText(string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
