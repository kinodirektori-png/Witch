using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.UI
{
    /// <summary>
    /// One page entry in the recipe and lore book: a brewed (or still locked) recipe, or a lore page about a plant,
    /// crystal, herb or mineral. <see cref="RecipeBookUI"/> clones a template of this and fills it in.
    /// </summary>
    /// <remarks>
    /// Lay the texts out with a VerticalLayoutGroup on this object (TMP texts report their preferred height), so long
    /// descriptions grow the entry instead of overflowing. The icon and "new" badge can ignore the layout.
    /// </remarks>
    public class BookEntryView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;

        [Tooltip("Formula or equation for recipes, category for lore pages.")]
        [SerializeField] private TMP_Text formulaLabel;

        [SerializeField] private TMP_Text bodyLabel;

        [Tooltip("Shown on entries that appeared since the book was last read.")]
        [SerializeField] private GameObject newBadge;

        [Tooltip("Optional action, e.g. \"Gather\" on lore pages.")]
        [SerializeField] private Button actionButton;

        [SerializeField] private TMP_Text actionLabel;

        [Tooltip("Optional background tinted differently for locked entries.")]
        [SerializeField] private Graphic background;

        [Header("Look")]
        [SerializeField] private Sprite fallbackIcon;
        [SerializeField] private Color unlockedBackground = new Color(1f, 0.97f, 0.88f);
        [SerializeField] private Color lockedBackground = new Color(0.85f, 0.82f, 0.78f);
        [Tooltip("Icon colour for locked entries: a dark silhouette.")]
        [SerializeField] private Color silhouetteColor = new Color(0.15f, 0.12f, 0.2f, 0.85f);
        [SerializeField] private string lockedTitle = "???";

        private Action pendingAction;

        private void Awake()
        {
            if (actionButton != null)
            {
                actionButton.onClick.AddListener(HandleAction);
            }
        }

        private void OnDestroy()
        {
            if (actionButton != null)
            {
                actionButton.onClick.RemoveListener(HandleAction);
            }
        }

        /// <summary>Shows a recipe: full details once brewed, a silhouette with a hint while locked.</summary>
        public void ShowRecipe(RecipeData recipe, bool discovered, bool isNew)
        {
            if (recipe == null)
            {
                return;
            }

            SetIcon(recipe.itemIcon, discovered);
            SetBadge(isNew);
            SetAction(null, null, false);

            if (!discovered)
            {
                SetText(titleLabel, lockedTitle);
                SetText(subtitleLabel, null);
                SetText(formulaLabel, null);
                SetText(bodyLabel, $"Brew it to find out!\n<size=85%>{DescribeIngredients(recipe)}</size>");
                return;
            }

            SetText(titleLabel, recipe.commonName);
            SetText(subtitleLabel, recipe.technicalName);
            SetText(formulaLabel, recipe.HasReaction ? recipe.reaction.RichTextEquation : recipe.RichTextFormula);

            var body = new StringBuilder();
            AppendLine(body, recipe.KidExplanation);
            AppendLine(body, $"<b>Made from:</b> {DescribeIngredients(recipe, includePrefix: false)}");
            AppendLine(body, string.IsNullOrEmpty(recipe.biologicalSource) ? null : $"<b>Where it comes from:</b> {recipe.biologicalSource}");
            SetText(bodyLabel, body.ToString());
        }

        /// <summary>
        /// Shows a lore page. <paramref name="onGather"/> is the "Gather" action (#18); pass null to hide it, or
        /// <paramref name="gatherAvailable"/> false to show it as "coming soon".
        /// </summary>
        public void ShowLore(LoreEntryData entry, bool unlocked, bool isNew, Action onGather, bool gatherAvailable)
        {
            if (entry == null)
            {
                return;
            }

            SetIcon(entry.illustration, unlocked);
            SetBadge(isNew);
            SetText(formulaLabel, CategoryName(entry.category));

            if (!unlocked)
            {
                SetText(titleLabel, lockedTitle);
                SetText(subtitleLabel, null);
                SetText(bodyLabel, entry.GetUnlockHint());
                SetAction(null, null, false);
                return;
            }

            SetText(titleLabel, entry.commonName);
            SetText(subtitleLabel, string.IsNullOrEmpty(entry.scientificName) ? null : $"<i>{entry.scientificName}</i>");

            var body = new StringBuilder();
            AppendLine(body, entry.description);
            AppendLine(body, string.IsNullOrEmpty(entry.whereFound) ? null : $"<b>Where to find it:</b> {entry.whereFound}");
            AppendLine(body, entry.gatherYield == null ? null : $"<b>Gives you:</b> {entry.gatherYield.commonName}");
            AppendLine(body, string.IsNullOrEmpty(entry.safetyNote) ? null : $"<b>Safety:</b> {entry.safetyNote}");
            SetText(bodyLabel, body.ToString());

            bool canGather = entry.gatherYield != null && onGather != null;
            SetAction(canGather ? onGather : null, gatherAvailable ? "Gather" : "Gathering opens soon", canGather, gatherAvailable);
        }

        /// <summary>e.g. "You need: Baking Soda x3, Citric Acid, water and heat."</summary>
        public static string DescribeIngredients(RecipeData recipe, bool includePrefix = true)
        {
            var counts = new List<KeyValuePair<RecipeData, int>>();
            foreach (RecipeData ingredient in RecipeDatabase.GetRequiredIngredients(recipe))
            {
                int index = counts.FindIndex(pair => pair.Key == ingredient);
                if (index >= 0)
                {
                    counts[index] = new KeyValuePair<RecipeData, int>(ingredient, counts[index].Value + 1);
                }
                else
                {
                    counts.Add(new KeyValuePair<RecipeData, int>(ingredient, 1));
                }
            }

            var parts = new List<string>();
            foreach (KeyValuePair<RecipeData, int> pair in counts)
            {
                string name = pair.Key != null ? pair.Key.commonName : "?";
                if (recipe != null && recipe.conditions != null &&
                    recipe.conditions.TryGetRequiredState(pair.Key, out PhysicalState state) && state == PhysicalState.Liquid)
                {
                    name += " (as juice or liquid)";
                }

                parts.Add(pair.Value > 1 ? $"{name} x{pair.Value}" : name);
            }

            if (recipe != null && recipe.conditions != null)
            {
                if (recipe.conditions.needsWater)
                {
                    parts.Add("water");
                }

                if (recipe.conditions.needsHeat)
                {
                    parts.Add("heat");
                }
            }

            string list = JoinKidList(parts);
            return includePrefix ? $"You need: {list}." : list;
        }

        private static string JoinKidList(List<string> parts)
        {
            if (parts.Count == 0)
            {
                return "nothing";
            }

            if (parts.Count == 1)
            {
                return parts[0];
            }

            return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
        }

        private static string CategoryName(LoreCategory category)
        {
            switch (category)
            {
                case LoreCategory.Plant: return "Plant";
                case LoreCategory.Crystal: return "Crystal";
                case LoreCategory.Herb: return "Herb";
                case LoreCategory.Mineral: return "Mineral";
                default: return category.ToString();
            }
        }

        private void SetIcon(Sprite sprite, bool unlocked)
        {
            if (background != null)
            {
                background.color = unlocked ? unlockedBackground : lockedBackground;
            }

            if (icon == null)
            {
                return;
            }

            Sprite shown = sprite != null ? sprite : fallbackIcon;
            icon.sprite = shown;
            icon.enabled = shown != null;
            icon.preserveAspect = true;
            icon.color = unlocked ? Color.white : silhouetteColor;
        }

        private void SetBadge(bool isNew)
        {
            if (newBadge != null)
            {
                newBadge.SetActive(isNew);
            }
        }

        private void SetAction(Action action, string label, bool visible, bool interactable = true)
        {
            pendingAction = action;
            if (actionButton == null)
            {
                return;
            }

            actionButton.gameObject.SetActive(visible);
            actionButton.interactable = interactable;
            if (actionLabel != null && label != null)
            {
                actionLabel.text = label;
            }
        }

        private void HandleAction() => pendingAction?.Invoke();

        private static void SetText(TMP_Text label, string text)
        {
            if (label == null)
            {
                return;
            }

            bool hasText = !string.IsNullOrEmpty(text);
            label.gameObject.SetActive(hasText);
            label.text = hasText ? text : string.Empty;
        }

        private static void AppendLine(StringBuilder builder, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            builder.Append(line);
        }
    }
}
