using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Witch.Data
{
    /// <summary>
    /// A real chemical reaction: what goes in (reactants), what comes out (products), the balanced equation and a
    /// one-sentence explanation for kids. Link it from a <see cref="RecipeData"/> (its Reaction field) to make the
    /// cauldron brew that recipe when every reactant has been dropped in.
    /// Create assets via: Assets > Create > Witch > Reaction Data.
    /// </summary>
    /// <remarks>
    /// Reactant coefficients are also how many times the player drops that ingredient in, so
    /// "3 NaHCO3 + C6H8O7" means three scoops of baking soda and one of citric acid.
    /// </remarks>
    [CreateAssetMenu(fileName = "NewReaction", menuName = "Witch/Reaction Data", order = 1)]
    public class ReactionData : ScriptableObject
    {
        /// <summary>Arrow placed between reactants and products. LiberationSans (TMP default) has this glyph.</summary>
        public const string Arrow = "→";

        /// <summary>One side of the equation: "3 NaHCO3" is coefficient 3 of the substance with formula NaHCO3.</summary>
        [Serializable]
        public class Term
        {
            [Tooltip("How many of this substance take part. For reactants this is also how many times it is dropped in.")]
            [Min(1)] public int coefficient = 1;

            [Tooltip("The ingredient asset. Required for reactants (the player drops it in). " +
                     "Optional for products: leave empty and fill in Formula and Name instead.")]
            public RecipeData substance;

            [Tooltip("Formula used when Substance is empty, e.g. \"H2O\".")]
            public string formula;

            [Tooltip("Name used when Substance is empty, e.g. \"Water\".")]
            public string displayName;

            public Term()
            {
            }

            public Term(int coefficient, RecipeData substance)
            {
                this.coefficient = coefficient;
                this.substance = substance;
            }

            public Term(int coefficient, string formula, string displayName)
            {
                this.coefficient = coefficient;
                this.formula = formula;
                this.displayName = displayName;
            }

            /// <summary>The formula from <see cref="substance"/> if set, otherwise <see cref="formula"/>.</summary>
            public string Formula => substance != null ? substance.chemicalFormula : formula;

            /// <summary>The common name from <see cref="substance"/> if set, otherwise <see cref="displayName"/>.</summary>
            public string Name => substance != null ? substance.commonName : displayName;

            public int Coefficient => Mathf.Max(1, coefficient);
        }

        [Tooltip("Substances that react. Each must be an ingredient asset the player can drop into the cauldron.")]
        public List<Term> reactants = new List<Term>();

        [Tooltip("Substances that are made. Use an asset if the product is also an ingredient, otherwise type formula and name.")]
        public List<Term> products = new List<Term>();

        [Tooltip("Off: reactants can go in in any order. On: they must go in exactly in the order listed " +
                 "(each term's full coefficient before the next term).")]
        public bool reactantsInOrder;

        [Tooltip("One short sentence a child can read, shown under the equation. Say what the player sees and why.")]
        [TextArea(2, 4)]
        public string kidExplanation = "";

        /// <summary>Plain-text balanced equation, e.g. "3 NaHCO3 + C6H8O7 → Na3C6H5O7 + 3 CO2 + 3 H2O".</summary>
        public string Equation => BuildEquation(false);

        /// <summary>The equation with TMP subscripts for formula digits (coefficients stay full size).</summary>
        public string RichTextEquation => BuildEquation(true);

        /// <summary>
        /// The ingredients the player must drop in, with each reactant repeated by its coefficient, in listed order.
        /// Reactants without a substance are skipped. Never null.
        /// </summary>
        public List<RecipeData> GetIngredientSequence()
        {
            var sequence = new List<RecipeData>();
            if (reactants == null)
            {
                return sequence;
            }

            foreach (Term term in reactants)
            {
                if (term?.substance == null)
                {
                    continue;
                }

                for (int i = 0; i < term.Coefficient; i++)
                {
                    sequence.Add(term.substance);
                }
            }

            return sequence;
        }

        /// <summary>
        /// Checks that every element has the same number of atoms on both sides.
        /// <paramref name="problem"/> describes the first mismatch or unreadable formula, or is empty when balanced.
        /// </summary>
        public bool IsBalanced(out string problem)
        {
            if (!TryCountSide(reactants, "reactants", out Dictionary<string, int> left, out problem) ||
                !TryCountSide(products, "products", out Dictionary<string, int> right, out problem))
            {
                return false;
            }

            if (left.Count == 0 || right.Count == 0)
            {
                problem = "Add at least one reactant and one product.";
                return false;
            }

            var elements = new SortedSet<string>(left.Keys);
            elements.UnionWith(right.Keys);
            foreach (string element in elements)
            {
                left.TryGetValue(element, out int leftCount);
                right.TryGetValue(element, out int rightCount);
                if (leftCount != rightCount)
                {
                    problem = $"{element}: {leftCount} atoms on the left but {rightCount} on the right.";
                    return false;
                }
            }

            problem = string.Empty;
            return true;
        }

        private string BuildEquation(bool richText)
        {
            string left = BuildSide(reactants, richText);
            string right = BuildSide(products, richText);
            if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right))
            {
                return string.Empty;
            }

            return $"{left} {Arrow} {right}";
        }

        private static string BuildSide(List<Term> terms, bool richText)
        {
            var builder = new StringBuilder();
            if (terms == null)
            {
                return string.Empty;
            }

            foreach (Term term in terms)
            {
                string formula = term?.Formula;
                if (string.IsNullOrEmpty(formula))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" + ");
                }

                if (term.Coefficient > 1)
                {
                    builder.Append(term.Coefficient).Append(' ');
                }

                builder.Append(richText ? RecipeData.ToRichTextFormula(formula) : formula);
            }

            return builder.ToString();
        }

        private static bool TryCountSide(List<Term> terms, string sideName, out Dictionary<string, int> atoms, out string problem)
        {
            atoms = new Dictionary<string, int>();
            problem = string.Empty;
            if (terms == null)
            {
                return true;
            }

            foreach (Term term in terms)
            {
                if (term == null)
                {
                    continue;
                }

                string formula = term.Formula;
                if (string.IsNullOrEmpty(formula))
                {
                    problem = $"A term in {sideName} has no formula.";
                    return false;
                }

                if (!ChemicalFormula.TryCountAtoms(formula, out Dictionary<string, int> termAtoms, out string error))
                {
                    problem = $"Can't read \"{formula}\": {error}";
                    return false;
                }

                foreach (KeyValuePair<string, int> pair in termAtoms)
                {
                    atoms.TryGetValue(pair.Key, out int count);
                    atoms[pair.Key] = count + pair.Value * term.Coefficient;
                }
            }

            return true;
        }

        private void OnValidate()
        {
            kidExplanation = kidExplanation?.Trim();

            if (reactants != null && reactants.Exists(term => term != null && term.substance == null))
            {
                Debug.LogWarning($"Reaction '{name}': every reactant needs a Substance asset so the player can drop it in.", this);
            }

            bool hasTerms = reactants != null && reactants.Count > 0 && products != null && products.Count > 0;
            if (hasTerms && !IsBalanced(out string problem))
            {
                Debug.LogWarning($"Reaction '{name}' is not balanced. {problem}", this);
            }
        }
    }
}
