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

        /// <summary>True if this recipe is made by combining other ingredients.</summary>
        public bool IsCombination => ingredients != null && ingredients.Exists(ingredient => ingredient != null);

        /// <summary>
        /// The formula with digits wrapped in TextMeshPro subscript tags, e.g. "H2O" becomes "H&lt;sub&gt;2&lt;/sub&gt;O".
        /// Use this for TMP_Text labels; use <see cref="chemicalFormula"/> for plain text.
        /// </summary>
        public string RichTextFormula => ToRichTextFormula(chemicalFormula);

        /// <summary>Converts digits in a formula to TMP subscripts (consecutive digits share one tag).</summary>
        public static string ToRichTextFormula(string formula)
        {
            if (string.IsNullOrEmpty(formula))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(formula.Length + 16);
            bool inSubscript = false;

            foreach (char character in formula)
            {
                bool isDigit = char.IsDigit(character);
                if (isDigit && !inSubscript)
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
        }
    }
}
