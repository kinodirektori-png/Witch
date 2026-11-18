using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.Tests
{
    /// <summary>
    /// Drives <see cref="CauldronManager.CheckReaction"/> with plain GameObjects. No MixingVessel, so these cover the
    /// ingredient logic; water and heat are covered by <see cref="BrewConditionEvaluatorTests"/>.
    /// </summary>
    public class CauldronManagerTests
    {
        private TestObjects objects;
        private CauldronManager cauldron;
        private RecipeData salt;
        private RecipeData citricAcid;
        private RecipeData bakingSoda;
        private RecipeData fizz;
        private readonly List<RecipeData> brewed = new List<RecipeData>();
        private readonly List<GameObject> failed = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            objects = new TestObjects();
            salt = objects.Recipe("TableSalt", "NaCl");
            citricAcid = objects.Recipe("CitricAcid", "C6H8O7");
            bakingSoda = objects.Recipe("BakingSoda", "NaHCO3");
            fizz = objects.Recipe("FizzyBubbles", "CO2", citricAcid, bakingSoda);

            cauldron = objects.NewGameObject("Pot").AddComponent<CauldronManager>();
            brewed.Clear();
            failed.Clear();
            cauldron.SuccessfulReaction += brewed.Add;
            cauldron.onFailedReaction.AddListener(failed.Add);
        }

        [TearDown]
        public void TearDown() => objects.DestroyAll();

        private void Configure(CauldronManager.BrewingMode mode, params RecipeData[] recipes)
        {
            var so = new SerializedObject(cauldron);
            SerializedProperty list = so.FindProperty("recipes");
            list.arraySize = recipes.Length;
            for (int i = 0; i < recipes.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
            }

            so.FindProperty("brewingMode").enumValueIndex = (int)mode;
            so.FindProperty("activeRecipeIndex").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            cauldron.RebuildDatabase();
        }

        private GameObject Item(RecipeData recipe)
        {
            GameObject go = objects.NewGameObject(recipe.name);
            go.AddComponent<IngredientItem>().SetRecipe(recipe);
            return go;
        }

        [Test]
        public void CorrectIngredient_RaisesSuccessfulReaction()
        {
            Configure(CauldronManager.BrewingMode.ActiveRecipe, salt);

            bool accepted = cauldron.CheckReaction(Item(salt));

            Assert.IsTrue(accepted);
            CollectionAssert.AreEqual(new[] { salt }, brewed);
            Assert.AreSame(salt, cauldron.LastBrewedRecipe);
            Assert.AreEqual(0, cauldron.Contents.Count);
        }

        [Test]
        public void WrongIngredient_IsRejected()
        {
            Configure(CauldronManager.BrewingMode.ActiveRecipe, fizz, salt);
            GameObject wrong = Item(salt);

            bool accepted = cauldron.CheckReaction(wrong);

            Assert.IsFalse(accepted);
            Assert.IsEmpty(brewed);
            CollectionAssert.AreEqual(new[] { wrong }, failed);
            Assert.AreEqual(0, cauldron.Contents.Count);
            Assert.IsTrue(wrong.activeSelf, "A refused item should stay on the counter.");
        }

        [Test]
        public void MultiIngredientRecipe_WaitsForTheLastIngredient()
        {
            Configure(CauldronManager.BrewingMode.ActiveRecipe, fizz);

            Assert.IsTrue(cauldron.CheckReaction(Item(citricAcid)));
            Assert.IsEmpty(brewed);
            CollectionAssert.AreEqual(new[] { citricAcid }, cauldron.Contents);
            CollectionAssert.AreEqual(new[] { bakingSoda }, cauldron.GetMissingIngredients());

            Assert.IsTrue(cauldron.CheckReaction(Item(bakingSoda)));
            CollectionAssert.AreEqual(new[] { fizz }, brewed);
        }

        [Test]
        public void RecipeSequence_AdvancesAfterEachBrew_AndLoops()
        {
            Configure(CauldronManager.BrewingMode.ActiveRecipe, salt, citricAcid);
            var changes = new List<RecipeData>();
            cauldron.ActiveRecipeChanged += changes.Add;

            Assert.AreSame(salt, cauldron.ActiveRecipe);
            cauldron.CheckReaction(Item(salt));
            Assert.AreSame(citricAcid, cauldron.ActiveRecipe);

            // Salt is no longer the goal, so it is refused now.
            Assert.IsFalse(cauldron.CheckReaction(Item(salt)));

            cauldron.CheckReaction(Item(citricAcid));
            Assert.AreSame(salt, cauldron.ActiveRecipe, "Loops back to the first recipe.");
            CollectionAssert.AreEqual(new[] { citricAcid, salt }, changes);
        }

        [Test]
        public void FreeBrewing_AcceptsAnyKnownRecipe()
        {
            Configure(CauldronManager.BrewingMode.AnyKnownRecipe, fizz, salt);

            Assert.IsTrue(cauldron.CheckReaction(Item(salt)));
            CollectionAssert.AreEqual(new[] { salt }, brewed);

            Assert.IsTrue(cauldron.CheckReaction(Item(bakingSoda)));
            Assert.IsTrue(cauldron.CheckReaction(Item(citricAcid)));
            CollectionAssert.AreEqual(new[] { salt, fizz }, brewed);
        }

        [Test]
        public void FreeBrewing_RejectsIngredientNoRecipeUses()
        {
            Configure(CauldronManager.BrewingMode.AnyKnownRecipe, fizz);

            Assert.IsFalse(cauldron.CheckReaction(Item(salt)));
            Assert.IsEmpty(brewed);
        }

        [Test]
        public void ItemWithoutIngredient_IsRejected()
        {
            Configure(CauldronManager.BrewingMode.ActiveRecipe, salt);

            Assert.IsFalse(cauldron.CheckReaction(objects.NewGameObject("Spoon")));
            Assert.IsFalse(cauldron.CheckReaction(null));
            Assert.IsEmpty(brewed);
        }

        [Test]
        public void WholeLemon_IsRefused_WhenJuiceIsNeeded()
        {
            fizz.conditions.ingredientStates.Add(new IngredientStateRule
            {
                ingredient = citricAcid,
                requiredState = PhysicalState.Liquid
            });
            Configure(CauldronManager.BrewingMode.ActiveRecipe, fizz);

            KitchenItemData juice = objects.KitchenItem("LemonJuice", citricAcid, PhysicalState.Liquid);
            KitchenItemData lemon = objects.KitchenItem("Lemon", citricAcid, PhysicalState.Solid);
            lemon.squeezeYield = juice;

            GameObject lemonGo = objects.NewGameObject("Lemon");
            lemonGo.AddComponent<ItemState>().Fill(lemon);
            lemonGo.AddComponent<IngredientItem>();

            Assert.IsFalse(cauldron.CheckReaction(lemonGo));
            Assert.AreEqual(BrewConditionEvaluator.SqueezeFirstHint, cauldron.LastRejectHint);

            GameObject glass = objects.NewGameObject("JuiceGlass");
            glass.AddComponent<ItemState>().Fill(juice);
            glass.AddComponent<IngredientItem>();

            Assert.IsTrue(cauldron.CheckReaction(glass));
            CollectionAssert.AreEqual(new[] { citricAcid }, cauldron.Contents);
        }

        [Test]
        public void EmptyCauldron_ClearsContents()
        {
            Configure(CauldronManager.BrewingMode.ActiveRecipe, fizz);
            cauldron.CheckReaction(Item(citricAcid));

            cauldron.EmptyCauldron();

            Assert.AreEqual(0, cauldron.Contents.Count);
        }
    }
}
