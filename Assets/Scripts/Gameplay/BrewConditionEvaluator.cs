using System.Collections.Generic;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>Why a recipe can't happen yet.</summary>
    public enum ConditionProblem
    {
        None,
        /// <summary>An ingredient is in the wrong form, e.g. a whole lemon where juice is needed.</summary>
        WrongState,
        NeedsWater,
        NeedsHeat,
        /// <summary>Too much water: the mixture is too weak to react.</summary>
        TooWatery
    }

    /// <summary>The outcome of checking a recipe's <see cref="BrewConditions"/>, with a hint a child can act on.</summary>
    public readonly struct ConditionResult
    {
        public static readonly ConditionResult Met = new ConditionResult(ConditionProblem.None, string.Empty);

        public readonly ConditionProblem Problem;
        public readonly string Hint;

        public ConditionResult(ConditionProblem problem, string hint)
        {
            Problem = problem;
            Hint = hint ?? string.Empty;
        }

        public bool IsMet => Problem == ConditionProblem.None;
    }

    /// <summary>
    /// Decides whether a recipe's conditions are met: ingredient forms, water, heat and strength.
    /// Plain C# with no scene dependencies, so it can be unit-tested (#13).
    /// </summary>
    public static class BrewConditionEvaluator
    {
        public const string SqueezeFirstHint = "Try squeezing it first!";
        public const string NeedsWaterHint = "Pour in some water!";
        public const string NeedsHeatHint = "Turn on the heat!";
        public const string TooWateryHint = "Too watery to react. Empty the pot and try with less water.";

        /// <summary>
        /// Checks one ingredient about to be poured in for <paramref name="recipe"/>. Only the form matters here;
        /// water and heat can still be added later. <paramref name="item"/> (optional) improves the hint.
        /// </summary>
        public static ConditionResult CheckIngredient(RecipeData recipe, RecipeData ingredient, PhysicalState state, KitchenItemData item = null)
        {
            BrewConditions conditions = recipe != null ? recipe.conditions : null;
            if (conditions == null || !conditions.TryGetRequiredState(ingredient, out PhysicalState required) || required == state)
            {
                return ConditionResult.Met;
            }

            return new ConditionResult(ConditionProblem.WrongState, GetWrongStateHint(ingredient, required, state, item));
        }

        /// <summary>
        /// Checks the whole mixture once every ingredient of <paramref name="recipe"/> is in. A null mixture (a pot
        /// without a <see cref="MixingVessel"/>) passes form checks but can never have water or heat.
        /// </summary>
        public static ConditionResult Evaluate(RecipeData recipe, Mixture mixture)
        {
            BrewConditions conditions = recipe != null ? recipe.conditions : null;
            if (conditions == null)
            {
                return ConditionResult.Met;
            }

            if (mixture != null && conditions.ingredientStates != null)
            {
                foreach (IngredientStateRule rule in conditions.ingredientStates)
                {
                    if (rule == null || rule.ingredient == null)
                    {
                        continue;
                    }

                    List<PhysicalState> states = mixture.GetStatesOf(rule.ingredient);
                    foreach (PhysicalState state in states)
                    {
                        if (state != rule.requiredState)
                        {
                            return new ConditionResult(ConditionProblem.WrongState,
                                GetWrongStateHint(rule.ingredient, rule.requiredState, state, null));
                        }
                    }
                }
            }

            if (conditions.needsWater && (mixture == null || mixture.WaterParts < conditions.minWaterParts))
            {
                return new ConditionResult(ConditionProblem.NeedsWater, NeedsWaterHint);
            }

            if (conditions.minConcentration > 0f && mixture != null && mixture.Concentration < conditions.minConcentration)
            {
                return new ConditionResult(ConditionProblem.TooWatery, TooWateryHint);
            }

            if (conditions.needsHeat && (mixture == null || mixture.TemperatureC < conditions.minTemperatureC))
            {
                return new ConditionResult(ConditionProblem.NeedsHeat, NeedsHeatHint);
            }

            return ConditionResult.Met;
        }

        /// <summary>A hint like "Try squeezing it first!" or "Baking Soda needs to be a powder."</summary>
        public static string GetWrongStateHint(RecipeData ingredient, PhysicalState required, PhysicalState actual, KitchenItemData item)
        {
            if (required == PhysicalState.Liquid && item != null && item.CanBeSqueezed && actual == PhysicalState.Solid)
            {
                return SqueezeFirstHint;
            }

            string name = ingredient != null ? ingredient.commonName : "This";
            switch (required)
            {
                case PhysicalState.Liquid:
                    return $"{name} needs to be a liquid here.";
                case PhysicalState.Powder:
                    return $"{name} needs to be a powder here.";
                default:
                    return $"{name} needs to be {PhysicalStates.KidName(required)} here.";
            }
        }
    }
}
