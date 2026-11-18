using System.Collections.Generic;
using NUnit.Framework;
using Witch.Data;

namespace Witch.Tests
{
    public class FormulaAndReactionTests
    {
        private TestObjects objects;

        [SetUp]
        public void SetUp() => objects = new TestObjects();

        [TearDown]
        public void TearDown() => objects.DestroyAll();

        [TestCase("H2O", "H", 2, "O", 1)]
        [TestCase("NaHCO3", "Na", 1, "C", 1)]
        [TestCase("Ca(OH)2", "O", 2, "H", 2)]
        [TestCase("CuSO4·5H2O", "H", 10, "O", 9)]
        public void CountsAtoms(string formula, string elementA, int countA, string elementB, int countB)
        {
            Assert.IsTrue(ChemicalFormula.TryCountAtoms(formula, out Dictionary<string, int> atoms, out string error), error);
            Assert.AreEqual(countA, atoms[elementA]);
            Assert.AreEqual(countB, atoms[elementB]);
        }

        [TestCase("")]
        [TestCase("H2O)")]
        [TestCase("(H2O")]
        [TestCase("H2-O")]
        public void RejectsBadFormulas(string formula)
        {
            Assert.IsFalse(ChemicalFormula.TryCountAtoms(formula, out _, out string error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void RichTextFormula_SubscriptsCounts()
        {
            Assert.AreEqual("H<sub>2</sub>O", RecipeData.ToRichTextFormula("H2O"));
            Assert.AreEqual("Ca(OH)<sub>2</sub>", RecipeData.ToRichTextFormula("Ca(OH)2"));
            Assert.AreEqual("2 H<sub>2</sub>", RecipeData.ToRichTextFormula("2 H2"), "Leading coefficients stay normal size.");
        }

        [Test]
        public void BakingSodaAndCitricAcid_IsBalanced()
        {
            RecipeData citric = objects.Recipe("CitricAcid", "C6H8O7");
            RecipeData soda = objects.Recipe("BakingSoda", "NaHCO3");
            ReactionData reaction = objects.Reaction("Fizz", false, (1, citric), (3, soda));
            reaction.products.Add(new ReactionData.Term(1, "Na3C6H5O7", "Sodium citrate"));
            reaction.products.Add(new ReactionData.Term(3, "H2O", "Water"));
            reaction.products.Add(new ReactionData.Term(3, "CO2", "Carbon dioxide"));

            Assert.IsTrue(reaction.IsBalanced(out string problem), problem);
            Assert.AreEqual("C6H8O7 + 3 NaHCO3 → Na3C6H5O7 + 3 H2O + 3 CO2", reaction.Equation);
            Assert.AreEqual(4, reaction.GetIngredientSequence().Count);
        }

        [Test]
        public void UnbalancedReaction_ExplainsWhichElement()
        {
            RecipeData soda = objects.Recipe("BakingSoda", "NaHCO3");
            ReactionData reaction = objects.Reaction("Heat", false, (1, soda));
            reaction.products.Add(new ReactionData.Term(1, "Na2CO3", "Washing soda"));

            Assert.IsFalse(reaction.IsBalanced(out string problem));
            StringAssert.StartsWith("H:", problem, "Elements are checked in alphabetical order; H is off first.");
        }
    }
}
