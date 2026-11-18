using System.Collections.Generic;
using NUnit.Framework;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.Tests
{
    public class RecipeDatabaseTests
    {
        private TestObjects objects;
        private RecipeData salt;
        private RecipeData citricAcid;
        private RecipeData bakingSoda;

        [SetUp]
        public void SetUp()
        {
            objects = new TestObjects();
            salt = objects.Recipe("TableSalt", "NaCl");
            citricAcid = objects.Recipe("CitricAcid", "C6H8O7");
            bakingSoda = objects.Recipe("BakingSoda", "NaHCO3");
        }

        [TearDown]
        public void TearDown() => objects.DestroyAll();

        [Test]
        public void BasicIngredient_IsBrewedByItself()
        {
            var db = new RecipeDatabase(new[] { salt });

            Assert.IsTrue(db.TryGetRecipe(new[] { salt }, out RecipeData found));
            Assert.AreSame(salt, found);
        }

        [Test]
        public void IngredientList_MatchesInAnyOrder()
        {
            RecipeData fizz = objects.Recipe("Fizz", "CO2", citricAcid, bakingSoda);
            var db = new RecipeDatabase(new[] { fizz });

            Assert.IsTrue(db.TryGetRecipe(new[] { bakingSoda, citricAcid }, out RecipeData found));
            Assert.AreSame(fizz, found);
        }

        [Test]
        public void Reaction_UsesReactantsRepeatedByCoefficient()
        {
            RecipeData fizz = objects.Recipe("Fizz", "CO2");
            fizz.reaction = objects.Reaction("FizzReaction", false, (3, bakingSoda), (1, citricAcid));

            List<RecipeData> required = RecipeDatabase.GetRequiredIngredients(fizz);

            Assert.AreEqual(4, required.Count);
            Assert.AreEqual(3, required.FindAll(r => r == bakingSoda).Count);
            Assert.IsFalse(RecipeDatabase.IsComplete(new[] { bakingSoda, citricAcid }, fizz));
            Assert.IsTrue(RecipeDatabase.IsComplete(new[] { bakingSoda, citricAcid, bakingSoda, bakingSoda }, fizz));
        }

        [Test]
        public void OrderedReaction_RejectsWrongOrder()
        {
            RecipeData layered = objects.Recipe("Layered", "X");
            layered.reaction = objects.Reaction("Ordered", true, (1, salt), (1, citricAcid));
            var db = new RecipeDatabase(new[] { layered });

            Assert.IsTrue(RecipeDatabase.CanStillBecome(new[] { salt }, layered));
            Assert.IsFalse(RecipeDatabase.CanStillBecome(new[] { citricAcid }, layered));
            Assert.IsTrue(db.TryGetRecipe(new[] { salt, citricAcid }, out _));
            Assert.IsFalse(db.TryGetRecipe(new[] { citricAcid, salt }, out _));
        }

        [Test]
        public void Missing_ListsWhatIsStillNeeded()
        {
            RecipeData fizz = objects.Recipe("Fizz", "CO2", citricAcid, bakingSoda);

            List<RecipeData> missing = RecipeDatabase.GetMissingIngredients(new[] { citricAcid }, fizz);

            CollectionAssert.AreEqual(new[] { bakingSoda }, missing);
        }

        [Test]
        public void WrongIngredient_CannotBecomeRecipe()
        {
            RecipeData fizz = objects.Recipe("Fizz", "CO2", citricAcid, bakingSoda);
            var db = new RecipeDatabase(new[] { fizz });

            Assert.IsFalse(RecipeDatabase.CanStillBecome(new[] { salt }, fizz));
            Assert.IsFalse(db.IsPartOfAnyRecipe(new[] { salt }));
            Assert.IsTrue(db.IsPartOfAnyRecipe(new[] { bakingSoda }));
        }

        [Test]
        public void DuplicateCombination_KeepsFirstAndWarns()
        {
            RecipeData first = objects.Recipe("First", "A", salt, citricAcid);
            RecipeData second = objects.Recipe("Second", "B", citricAcid, salt);

            var db = new RecipeDatabase(new[] { first, second, null });

            Assert.AreEqual(1, db.Count);
            Assert.IsTrue(db.Contains(first));
            Assert.IsFalse(db.Contains(second));
            Assert.AreEqual(1, db.Warnings.Count);
        }
    }
}
