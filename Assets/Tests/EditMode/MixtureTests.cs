using NUnit.Framework;
using UnityEngine;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.Tests
{
    public class MixtureTests
    {
        private TestObjects objects;
        private RecipeData salt;

        [SetUp]
        public void SetUp()
        {
            objects = new TestObjects();
            salt = objects.Recipe("TableSalt", "NaCl");
        }

        [TearDown]
        public void TearDown() => objects.DestroyAll();

        [Test]
        public void NewMixture_IsEmptyAtRoomTemperature()
        {
            var mixture = new Mixture();

            Assert.IsTrue(mixture.IsEmpty);
            Assert.AreEqual(0f, mixture.Concentration);
            Assert.AreEqual(Mixture.RoomTemperatureC, mixture.TemperatureC);
        }

        [Test]
        public void Concentration_IsSubstanceOverTotal()
        {
            var mixture = new Mixture();
            mixture.AddSubstance(salt, PhysicalState.Powder, 1f);
            Assert.AreEqual(1f, mixture.Concentration, 1e-5f, "No water: full strength.");

            mixture.AddWater(3f);
            Assert.AreEqual(0.25f, mixture.Concentration, 1e-5f);
            Assert.AreEqual(3f, mixture.WaterParts, 1e-5f);
        }

        [Test]
        public void OnlyWater_HasZeroConcentration()
        {
            var mixture = new Mixture();
            mixture.AddWater(2f);

            Assert.IsFalse(mixture.IsEmpty);
            Assert.AreEqual(0f, mixture.Concentration);
        }

        [Test]
        public void IgnoresNullSubstanceAndNonPositiveAmounts()
        {
            var mixture = new Mixture();
            mixture.AddSubstance(null, PhysicalState.Powder, 1f);
            mixture.AddSubstance(salt, PhysicalState.Powder, 0f);
            mixture.AddWater(-1f);

            Assert.IsTrue(mixture.IsEmpty);
        }

        [Test]
        public void RemembersTheFormEachSubstanceWentIn()
        {
            var mixture = new Mixture();
            mixture.AddSubstance(salt, PhysicalState.Powder, 1f);
            mixture.AddSubstance(salt, PhysicalState.Liquid, 1f);
            mixture.AddSubstance(salt, PhysicalState.Powder, 1f);

            Assert.IsTrue(mixture.Contains(salt, PhysicalState.Powder));
            Assert.IsFalse(mixture.Contains(salt, PhysicalState.Solid));
            CollectionAssert.AreEquivalent(new[] { PhysicalState.Powder, PhysicalState.Liquid }, mixture.GetStatesOf(salt));
        }

        [Test]
        public void Clear_KeepsTemperature()
        {
            var mixture = new Mixture();
            mixture.AddSubstance(salt, PhysicalState.Powder, 1f);
            mixture.AddWater(1f);
            mixture.SetTemperature(90f);

            mixture.Clear();

            Assert.IsTrue(mixture.IsEmpty);
            Assert.AreEqual(90f, mixture.TemperatureC);
        }

        [Test]
        public void AverageTint_IsWeightedByAmount()
        {
            var mixture = new Mixture();
            Assert.AreEqual(Color.white, mixture.GetAverageTint(), "Empty is white.");

            mixture.AddSubstance(salt, PhysicalState.Powder, 3f, Color.red);
            mixture.AddSubstance(salt, PhysicalState.Powder, 1f, Color.blue);

            Color tint = mixture.GetAverageTint();
            Assert.AreEqual(0.75f, tint.r, 1e-5f);
            Assert.AreEqual(0.25f, tint.b, 1e-5f);
        }
    }
}
