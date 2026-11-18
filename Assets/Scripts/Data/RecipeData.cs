using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Witch.Data
{
    /// <summary>
    /// Describes one magical ingredient/potion and the real-world chemistry behind it.
    /// Create assets via: Assets > Create > Witch > Recipe Data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "Witch/Recipe Data", order = 0)]
    public class RecipeData : ScriptableObject
    {
        [Tooltip("Kid-friendly name, e.g. \"Table Salt\".")]
        public string commonName = "Table Salt";

        [Tooltip("Scientific name, e.g. \"Sodium Chloride\".")]
        public string technicalName = "Sodium Chloride";

        [Tooltip("Chemical formula as plain text, e.g. \"NaCl\" or \"H2O\". Digits are shown as subscripts in UI.")]
        public string chemicalFormula = "NaCl";

        [Tooltip("Where it comes from in nature, e.g. \"Salt crystals / Sea water\".")]
        public string biologicalSource = "Salt crystals / Sea water";

        [Tooltip("Icon shown in inventory, recipe book and while dragging.")]
        public Sprite itemIcon;

        [Header("Brewing")]
        [Tooltip("Ingredients that must be in the cauldron together (in any order) to brew this. " +
                 "Add the same ingredient several times if more is needed, e.g. 3 x Baking Soda. " +
                 "Leave empty for a basic ingredient: it is brewed by dropping it into the cauldron on its own.")]
        public List<RecipeData> ingredients = new List<RecipeData>();

        [Tooltip("Optional real reaction that makes this recipe. When set, its reactants replace the Ingredients list " +
                 "(each reactant dropped in as many times as its coefficient), and the popup shows its equation.")]
        public ReactionData reaction;

        /// <summary>True if this recipe is made by a <see cref="ReactionData"/> with at least one reactant.</summary>
        public bool HasReaction => reaction != null && reaction.GetIngredientSequence().Count > 0;

        /// <summary>True if this recipe is made by combining other ingredients (via a reaction or the Ingredients list).</summary>
        public bool IsCombination => HasReaction || (ingredients != null && ingredients.Exists(ingredient => ingredient != null));

        /// <summary>True if the ingredients must go into the cauldron in the listed order.</summary>
        public bool RequiresOrder => HasReaction && reaction.reactantsInOrder;

        /// <summary>
        /// The formula with digits wrapped in TextMeshPro subscript tags, e.g. "H2O" becomes "H&lt;sub&gt;2&lt;/sub&gt;O".
        /// Use this for TMP_Text labels; use <see cref="chemicalFormula"/> for plain text.
        /// </summary>
        public string RichTextFormula => ToRichTextFormula(chemicalFormula);

        /// <summary>
        /// Converts digits in a formula to TMP subscripts (consecutive digits share one tag). Only digits that follow
        /// an element, ")" or "]" are subscripted, so coefficients such as the 3 in "3 CO2" or "·5H2O" stay full size.
        /// </summary>
        public static string ToRichTextFormula(string formula)
        {
            if (string.IsNullOrEmpty(formula))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(formula.Length + 16);
            bool inSubscript = false;
            char previous = ' ';

            foreach (char character in formula)
            {
                bool isDigit = char.IsDigit(character);
                bool followsElementOrGroup = char.IsLetter(previous) || previous == ')' || previous == ']';
                if (isDigit && !inSubscript && followsElementOrGroup)
                {
                    builder.Append("<sub>");
                    inSubscript = true;
                }
                else if (!isDigit && inSubscript)
                {
                    builder.Append("</sub>");
                    inSubscript = false;
                }

                builder.Append(character);
                previous = character;
            }

            if (inSubscript)
            {
                builder.Append("</sub>");
            }

            return builder.ToString();
        }

        private void OnValidate()
        {
            commonName = commonName?.Trim();
            technicalName = technicalName?.Trim();
            chemicalFormula = chemicalFormula?.Trim();
            biologicalSource = biologicalSource?.Trim();

            if (reaction != null && ingredients != null && ingredients.Exists(ingredient => ingredient != null))
            {
                Debug.LogWarning($"Recipe '{name}' has both a Reaction and an Ingredients list. The reaction's reactants " +
                                 "are used; clear the Ingredients list to avoid confusion.", this);
            }
        }
    }
}
