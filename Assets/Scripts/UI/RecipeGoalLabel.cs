using System.Collections;
using TMPro;
using UnityEngine;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.UI
{
    /// <summary>
    /// HUD text that tells the player which ingredient the cauldron wants next, and gives friendly feedback
    /// when a wrong ingredient is dropped in.
    /// </summary>
    public class RecipeGoalLabel : MonoBehaviour
    {
        [SerializeField] private CauldronManager cauldron;
        [SerializeField] private TMP_Text label;

        [Tooltip("{0} is replaced by the active recipe's common name.")]
        [SerializeField] private string goalFormat = "Brew: <b>{0}</b>";

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
            cauldron.onFailedReaction.RemoveListener(HandleFailedReaction);
        }

        private void HandleActiveRecipeChanged(RecipeData recipe)
        {
            StopFeedback();
            ShowGoal(recipe);
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
            SetText(recipe != null ? string.Format(goalFormat, recipe.commonName) : allDoneText);
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
