using System;
using System.Collections.Generic;
using UnityEngine;

namespace Witch.Data
{
    /// <summary>What kids see when a recipe is made. Drives effects and is shown in the recipe book.</summary>
    public enum ObservableEffect
    {
        /// <summary>Generic bubbling (the default for older assets).</summary>
        Bubbles = 0,
        /// <summary>Gas fizzing out, e.g. baking soda and lemon juice.</summary>
        Fizz = 1,
        /// <summary>A solid disappearing into water, e.g. salt in water.</summary>
        Dissolve = 2,
        ColorChange = 3,
        Steam = 4,
        Crystals = 5,
        /// <summary>Nothing visible happens: no particles.</summary>
        None = 6
    }

    /// <summary>One ingredient that must go into the pot in a particular form, e.g. Citric Acid as a Liquid (juice).</summary>
    [Serializable]
    public class IngredientStateRule
    {
        [Tooltip("The ingredient this rule is about. Must be one of the recipe's ingredients or reactants.")]
        public RecipeData ingredient;

        [Tooltip("The form it must be poured in. A whole lemon (Solid) is refused when Liquid is required.")]
        public PhysicalState requiredState = PhysicalState.Liquid;
    }

    /// <summary>
    /// The conditions a recipe needs on top of the right ingredients: the form each ingredient is in, water, heat and
    /// strength. Leave everything off for a recipe that happens as soon as the ingredients are in.
    /// </summary>
    [Serializable]
    public class BrewConditions
    {
        [Tooltip("Ingredients that must be in a certain form, e.g. Citric Acid as Liquid (squeeze the lemon first).")]
        public List<IngredientStateRule> ingredientStates = new List<IngredientStateRule>();

        [Tooltip("The recipe only happens once water has been poured in (e.g. salt dissolving).")]
        public bool needsWater;

        [Tooltip("Parts of water needed when Needs Water is on.")]
        [Min(0.1f)] public float minWaterParts = 1f;

        [Tooltip("The recipe only happens when the pot is hot enough (turn on the stove).")]
        public bool needsHeat;

        [Tooltip("Lowest temperature in °C when Needs Heat is on. Baking soda starts to break down around 80 °C.")]
        public float minTemperatureC = 80f;

        [Tooltip("0 = any strength. Above 0, a mixture with too much water won't react.")]
        [Range(0f, 1f)] public float minConcentration;

        [Header("What you see")]
        [Tooltip("The change kids observe. Fizz and Bubbles play particles; None plays nothing.")]
        public ObservableEffect effect = ObservableEffect.Bubbles;

        [Tooltip("One short sentence about what kids see and why. Shown when the recipe has no reaction explanation.")]
        [TextArea(2, 4)] public string whatYouSee = "";

        /// <summary>True if any condition beyond the ingredients is set.</summary>
        public bool HasConditions =>
            needsWater || needsHeat || minConcentration > 0f ||
            (ingredientStates != null && ingredientStates.Exists(rule => rule != null && rule.ingredient != null));

        /// <summary>The form <paramref name="ingredient"/> must be in, if a rule says so.</summary>
        public bool TryGetRequiredState(RecipeData ingredient, out PhysicalState state)
        {
            state = PhysicalState.Solid;
            if (ingredient == null || ingredientStates == null)
            {
                return false;
            }

            foreach (IngredientStateRule rule in ingredientStates)
            {
                if (rule != null && rule.ingredient == ingredient)
                {
                    state = rule.requiredState;
                    return true;
                }
            }

            return false;
        }
    }
}
