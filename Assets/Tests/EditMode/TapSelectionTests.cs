using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.Tests
{
    /// <summary>Tap an item, then tap the pot: the drag-free way to play (#21).</summary>
    public class TapSelectionTests
    {
        private TestObjects objects;

        [SetUp]
        public void SetUp()
        {
            objects = new TestObjects();
            TapSelection.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            TapSelection.Clear();
            objects.DestroyAll();
        }

        private static PointerEventData Tap() => new PointerEventData(null) { button = PointerEventData.InputButton.Left };

        private TapSelectable Item(string name, RecipeData recipe = null)
        {
            GameObject go = objects.NewGameObject(name);
            go.AddComponent<RectTransform>();
            if (recipe != null)
            {
                go.AddComponent<IngredientItem>().SetRecipe(recipe);
            }

            return go.AddComponent<TapSelectable>();
        }

        private ItemDropZone Zone(ItemDropZone.AcceptedItemBehaviour behaviour)
        {
            GameObject go = objects.NewGameObject("Zone");
            go.AddComponent<RectTransform>();
            var zone = go.AddComponent<ItemDropZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("acceptedItemBehaviour").enumValueIndex = (int)behaviour;
            so.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        [Test]
        public void TappingSelects_AndTappingAgainPutsDown()
        {
            TapSelectable jar = Item("SaltJar");

            jar.OnPointerClick(Tap());
            Assert.AreSame(jar, TapSelection.Current);
            Assert.Greater(jar.transform.localScale.x, 1f, "Selection is shown by size, not colour.");

            jar.OnPointerClick(Tap());
            Assert.IsNull(TapSelection.Current);
            Assert.AreEqual(1f, jar.transform.localScale.x, 1e-5f);
        }

        [Test]
        public void SelectingAnotherItem_ReplacesTheSelection()
        {
            TapSelectable jar = Item("SaltJar");
            TapSelectable jug = Item("WaterJug");

            jar.OnPointerClick(Tap());
            jug.OnPointerClick(Tap());

            Assert.AreSame(jug, TapSelection.Current);
            Assert.AreEqual(1f, jar.transform.localScale.x, 1e-5f);
        }

        [Test]
        public void ItemsThatAlreadyUseTaps_AreNotSelected()
        {
            TapSelectable lemon = Item("Lemon");
            lemon.gameObject.AddComponent<SqueezeInteraction>();

            lemon.OnPointerClick(Tap());

            Assert.IsNull(TapSelection.Current, "Tapping the lemon squeezes it instead.");
        }

        [Test]
        public void TappingTheZone_WithoutSelection_DoesNothing()
        {
            ItemDropZone zone = Zone(ItemDropZone.AcceptedItemBehaviour.ParentToZone);
            int accepted = 0;
            zone.onItemAccepted.AddListener(_ => accepted++);

            zone.OnPointerClick(Tap());

            Assert.AreEqual(0, accepted);
        }

        [Test]
        public void TappingTheZone_DeliversTheSelectedItem()
        {
            ItemDropZone zone = Zone(ItemDropZone.AcceptedItemBehaviour.ParentToZone);
            TapSelectable jar = Item("SaltJar");

            jar.OnPointerClick(Tap());
            zone.OnPointerClick(Tap());

            Assert.AreSame(zone.transform, jar.transform.parent);
            Assert.IsNull(TapSelection.Current, "The item is put down once delivered.");
        }

        [Test]
        public void TapIntoThePot_BrewsLikeADrop()
        {
            RecipeData salt = objects.Recipe("TableSalt", "NaCl");
            ItemDropZone zone = Zone(ItemDropZone.AcceptedItemBehaviour.ReturnToStart);
            var cauldron = zone.gameObject.AddComponent<CauldronManager>();
            var so = new SerializedObject(cauldron);
            SerializedProperty list = so.FindProperty("recipes");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = salt;
            so.ApplyModifiedPropertiesWithoutUndo();
            cauldron.RebuildDatabase();

            RecipeData brewed = null;
            cauldron.SuccessfulReaction += recipe => brewed = recipe;

            TapSelectable jar = Item("SaltJar", salt);
            jar.OnPointerClick(Tap());
            zone.OnPointerClick(Tap());

            Assert.AreSame(salt, brewed);
            Assert.IsNull(TapSelection.Current);
        }

        [Test]
        public void WrongIngredient_StaysSelected()
        {
            RecipeData salt = objects.Recipe("TableSalt", "NaCl");
            RecipeData flour = objects.Recipe("Flour", "C6H10O5");
            ItemDropZone zone = Zone(ItemDropZone.AcceptedItemBehaviour.ReturnToStart);
            var cauldron = zone.gameObject.AddComponent<CauldronManager>();
            var so = new SerializedObject(cauldron);
            SerializedProperty list = so.FindProperty("recipes");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = salt;
            so.ApplyModifiedPropertiesWithoutUndo();
            cauldron.RebuildDatabase();

            int rejected = 0;
            zone.onItemRejected.AddListener(_ => rejected++);

            TapSelectable bag = Item("FlourBag", flour);
            bag.OnPointerClick(Tap());
            zone.OnPointerClick(Tap());

            Assert.AreEqual(1, rejected);
            Assert.AreSame(bag, TapSelection.Current, "Still picked up, so the child can try another place.");
            Assert.IsTrue(bag.gameObject.activeSelf);
        }
    }
}
