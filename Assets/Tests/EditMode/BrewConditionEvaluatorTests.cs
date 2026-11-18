using NUnit.Framework;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.Tests
{
    public class BrewConditionEvaluatorTests
    {
        private TestObjects objects;
        private RecipeData citricAcid;
        private RecipeData bakingSoda;
        private RecipeData recipe;

        [SetUp]
        public void SetUp()
        {
            objects = new TestObjects();
            citricAcid = objects.Recipe("CitricAcid", "C6H8O7");
            bakingSoda = objects.Recipe("BakingSoda", "NaHCO3");
            recipe = objects.Recipe("Fizz", "CO2", citricAcid, bakingSoda);
        }

        [TearDown]
        public void TearDown() => objects.DestroyAll();

        private void RequireJuice()
        {
            recipe.conditions.ingredientStates.Add(new IngredientStateRule
            {
                ingredient = citricAcid,
                requiredState = PhysicalState.Liquid
            });
        }

        private Mixture Mix(PhysicalState acidState = PhysicalState.Liquid)
        {
            var mixture = new Mixture();
            mixture.AddSubstance(citricAcid, acidState, 1f);
            mixture.AddSubstance(bakingSoda, PhysicalState.Powder, 1f);
            return mixture;
        }

        [Test]
        public void NoConditions_IsMet()
        {
            Assert.IsTrue(BrewConditionEvaluator.Evaluate(recipe, Mix()).IsMet);
            Assert.IsTrue(BrewConditionEvaluator.Evaluate(recipe, null).IsMet);
            Assert.IsTrue(BrewConditionEvaluator.Evaluate(null, Mix()).IsMet);
        }

        [Test]
        public void WrongState_InMixture()
        {
            RequireJuice();

            ConditionResult result = BrewConditionEvaluator.Evaluate(recipe, Mix(PhysicalState.Solid));

            Assert.AreEqual(ConditionProblem.WrongState, result.Problem);
            Assert.IsNotEmpty(result.Hint);
        }

        [Test]
        public void CheckIngredient_WholeLemon_SuggestsSqueezing()
        {
            RequireJuice();
            KitchenItemData juice = objects.KitchenItem("LemonJuice", citricAcid, PhysicalState.Liquid);
            KitchenItemData lemon = objects.KitchenItem("Lemon", citricAcid, PhysicalState.Solid);
            lemon.squeezeYield = juice;

            ConditionResult wrong = BrewConditionEvaluator.CheckIngredient(recipe, citricAcid, PhysicalState.Solid, lemon);
            ConditionResult right = BrewConditionEvaluator.CheckIngredient(recipe, citricAcid, PhysicalState.Liquid, juice);
            ConditionResult unruled = BrewConditionEvaluator.CheckIngredient(recipe, bakingSoda, PhysicalState.Solid);

            Assert.AreEqual(ConditionProblem.WrongState, wrong.Problem);
            Assert.AreEqual(BrewConditionEvaluator.SqueezeFirstHint, wrong.Hint);
            Assert.IsTrue(right.IsMet);
            Assert.IsTrue(unruled.IsMet, "Ingredients without a rule can be in any form.");
        }

        [Test]
        public void NeedsWater_UntilEnoughWaterIsAdded()
        {
            recipe.conditions.needsWater = true;
            recipe.conditions.minWaterParts = 2f;
            Mixture mixture = Mix();

            Assert.AreEqual(ConditionProblem.NeedsWater, BrewConditionEvaluator.Evaluate(recipe, mixture).Problem);
            Assert.AreEqual(ConditionProblem.NeedsWater, BrewConditionEvaluator.Evaluate(recipe, null).Problem);

            mixture.AddWater(1f);
            Assert.AreEqual(ConditionProblem.NeedsWater, BrewConditionEvaluator.Evaluate(recipe, mixture).Problem);

            mixture.AddWater(1f);
            Assert.IsTrue(BrewConditionEvaluator.Evaluate(recipe, mixture).IsMet);
        }

        [Test]
        public void NeedsHeat_UntilHotEnough()
        {
            recipe.conditions.needsHeat = true;
            recipe.conditions.minTemperatureC = 80f;
            Mixture mixture = Mix();

            ConditionResult cold = BrewConditionEvaluator.Evaluate(recipe, mixture);
            Assert.AreEqual(ConditionProblem.NeedsHeat, cold.Problem);
            Assert.AreEqual(BrewConditionEvaluator.NeedsHeatHint, cold.Hint);

            mixture.SetTemperature(79f);
            Assert.AreEqual(ConditionProblem.NeedsHeat, BrewConditionEvaluator.Evaluate(recipe, mixture).Problem);

            mixture.SetTemperature(80f);
            Assert.IsTrue(BrewConditionEvaluator.Evaluate(recipe, mixture).IsMet);
        }

        [Test]
        public void TooWatery_WhenBelowMinConcentration()
        {
            recipe.conditions.minConcentration = 0.5f;
            Mixture mixture = Mix(); // 2 parts substance

            Assert.IsTrue(BrewConditionEvaluator.Evaluate(recipe, mixture).IsMet);

            mixture.AddWater(3f); // 2 / 5 = 0.4
            ConditionResult result = BrewConditionEvaluator.Evaluate(recipe, mixture);
            Assert.AreEqual(ConditionProblem.TooWatery, result.Problem);
            Assert.AreEqual(BrewConditionEvaluator.TooWateryHint, result.Hint);
        }

        [Test]
        public void AllConditionsMet()
        {
            RequireJuice();
            recipe.conditions.needsWater = true;
            recipe.conditions.minWaterParts = 1f;
            recipe.conditions.needsHeat = true;
            recipe.conditions.minTemperatureC = 60f;
            recipe.conditions.minConcentration = 0.3f;

            Mixture mixture = Mix();
            mixture.AddWater(1f);
            mixture.SetTemperature(70f);

            Assert.IsTrue(recipe.conditions.HasConditions);
            Assert.IsTrue(BrewConditionEvaluator.Evaluate(recipe, mixture).IsMet);
        }

        [Test]
        public void WrongStateHint_NamesTheIngredient()
        {
            string hint = BrewConditionEvaluator.GetWrongStateHint(bakingSoda, PhysicalState.Powder, PhysicalState.Solid, null);

            StringAssert.Contains(bakingSoda.commonName, hint);
            StringAssert.Contains("powder", hint);
        }
    }
}
