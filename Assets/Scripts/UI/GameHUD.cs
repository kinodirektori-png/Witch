using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.UI
{
    /// <summary>
    /// Top-of-screen HUD: the current goal (icon + name, with "2/4" progress for multi-ingredient recipes),
    /// how many recipes have been found, friendly feedback after a wrong ingredient, and a hint button that
    /// appears after a few wrong tries. Replaces the old RecipeGoalLabel.
    /// </summary>
    /// <remarks>
    /// Only displays state: the goal comes from <see cref="CauldronManager"/>, the found count from
    /// <see cref="RecipeDiscovery"/>, and hint logic lives in <see cref="HintTracker"/>.
    /// </remarks>
    public class GameHUD : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private CauldronManager cauldron;
        [SerializeField] private RecipeDiscovery discovery;
        [SerializeField] private HintTracker hintTracker;

        [Header("Goal")]
        [Tooltip("Shows the active recipe's itemIcon.")]
        [SerializeField] private Image goalIcon;

        [Tooltip("Used when the recipe has no itemIcon yet (placeholder art).")]
        [SerializeField] private Sprite fallbackIcon;

        [SerializeField] private TMP_Text goalLabel;

        [Tooltip("{0} is replaced by the active recipe's common name.")]
        [SerializeField] private string goalFormat = "Brew: <b>{0}</b>";

        [Tooltip("Appended for multi-ingredient recipes. {0} = ingredients added, {1} = ingredients needed. Leave empty to hide.")]
        [SerializeField] private string ingredientProgressFormat = " ({0}/{1})";

        [SerializeField] private string allDoneText = "All potions brewed!";
        [SerializeField] private string wrongIngredientText = "Hmm, that's not it. Try another one!";

        [Tooltip("Seconds the wrong-ingredient message stays before the goal comes back.")]
        [SerializeField, Min(0f)] private float feedbackDuration = 1.5f;

        [Header("Progress")]
        [SerializeField] private TMP_Text progressLabel;

        [Tooltip("{0} = recipes found, {1} = total recipes.")]
        [SerializeField] private string progressFormat = "Recipes found: {0} / {1}";

        [Header("Hint")]
        [Tooltip("The hint button. Hidden (via its CanvasGroup) until the HintTracker says a hint is available.")]
        [SerializeField] private Button hintButton;

        private CanvasGroup hintGroup;
        private Coroutine feedbackRoutine;

        private void Awake()
        {
            if (hintButton != null)
            {
                hintGroup = hintButton.GetComponent<CanvasGroup>();
                if (hintGroup == null)
                {
                    hintGroup = hintButton.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private void OnEnable()
        {
            if (cauldron != null)
            {
                cauldron.ActiveRecipeChanged += HandleActiveRecipeChanged;
                cauldron.ContentsChanged += HandleContentsChanged;
                cauldron.onFailedReaction.AddListener(HandleFailedReaction);
            }
            else
            {
                Debug.LogWarning($"{nameof(GameHUD)} on '{name}' has no {nameof(CauldronManager)} assigned.", this);
            }

            if (discovery != null)
            {
                discovery.ProgressChanged += RefreshProgress;
            }

            if (hintTracker != null)
            {
                hintTracker.HintAvailabilityChanged += SetHintVisible;
            }

            if (hintButton != null)
            {
                hintButton.onClick.AddListener(HandleHintClicked);
            }

            ShowGoal();
            RefreshProgress();
            SetHintVisible(hintTracker != null && hintTracker.HintAvailable);
        }

        private void Start()
        {
            // Sources finish their own Awake before Start, so refresh once more in case OnEnable ran first.
            ShowGoal();
            RefreshProgress();
        }

        private void OnDisable()
        {
            StopFeedback();

            if (cauldron != null)
            {
                cauldron.ActiveRecipeChanged -= HandleActiveRecipeChanged;
                cauldron.ContentsChanged -= HandleContentsChanged;
                cauldron.onFailedReaction.RemoveListener(HandleFailedReaction);
            }

            if (discovery != null)
            {
                discovery.ProgressChanged -= RefreshProgress;
            }

            if (hintTracker != null)
            {
                hintTracker.HintAvailabilityChanged -= SetHintVisible;
            }

            if (hintButton != null)
            {
                hintButton.onClick.RemoveListener(HandleHintClicked);
            }
        }

        private void HandleActiveRecipeChanged(RecipeData recipe)
        {
            StopFeedback();
            ShowGoal();
        }

        private void HandleContentsChanged(IReadOnlyList<RecipeData> contents)
        {
            // Don't cut the wrong-ingredient message short; the goal (with progress) comes back after it.
            if (feedbackRoutine == null)
            {
                ShowGoal();
            }
        }

        private void HandleFailedReaction(GameObject droppedItem)
        {
            StopFeedback();
            feedbackRoutine = StartCoroutine(FeedbackRoutine());
        }

        private void HandleHintClicked()
        {
            if (hintTracker != null)
            {
                hintTracker.RequestHint();
            }
        }

        private IEnumerator FeedbackRoutine()
        {
            SetGoalText(wrongIngredientText);
            yield return new WaitForSecondsRealtime(feedbackDuration);
            feedbackRoutine = null;
            ShowGoal();
        }

        private void StopFeedback()
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
                feedbackRoutine = null;
            }
        }

        private void ShowGoal()
        {
            RecipeData recipe = cauldron != null ? cauldron.ActiveRecipe : null;
            SetGoalIcon(recipe);

            if (recipe == null)
            {
                SetGoalText(allDoneText);
                return;
            }

            string text = string.Format(goalFormat, recipe.commonName);

            if (recipe.IsCombination && !string.IsNullOrEmpty(ingredientProgressFormat))
            {
                int needed = RecipeDatabase.GetRequiredIngredients(recipe).Count;
                // In free-brew mode the contents may belong to another recipe; then nothing counts towards this one.
                int added = RecipeDatabase.CanStillBecome(cauldron.Contents, recipe)
                    ? needed - cauldron.GetMissingIngredients(recipe).Count
                    : 0;
                text += string.Format(ingredientProgressFormat, added, needed);
            }

            SetGoalText(text);
        }

        private void SetGoalIcon(RecipeData recipe)
        {
            if (goalIcon == null)
            {
                return;
            }

            Sprite sprite = recipe != null && recipe.itemIcon != null ? recipe.itemIcon : fallbackIcon;
            goalIcon.sprite = sprite;
            goalIcon.enabled = recipe != null && sprite != null;
            goalIcon.preserveAspect = true;
        }

        private void RefreshProgress()
        {
            if (progressLabel == null)
            {
                return;
            }

            bool hasDiscovery = discovery != null && discovery.TotalCount > 0;
            progressLabel.gameObject.SetActive(hasDiscovery);
            if (hasDiscovery)
            {
                progressLabel.text = string.Format(progressFormat, discovery.DiscoveredCount, discovery.TotalCount);
            }
        }

        private void SetHintVisible(bool visible)
        {
            if (hintGroup == null)
            {
                return;
            }

            // CanvasGroup instead of SetActive so the layout doesn't jump and the hidden button can't be tapped.
            hintGroup.alpha = visible ? 1f : 0f;
            hintGroup.interactable = visible;
            hintGroup.blocksRaycasts = visible;
        }

        private void SetGoalText(string text)
        {
            if (goalLabel != null)
            {
                goalLabel.text = text;
            }
        }
    }
}
