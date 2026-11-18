using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.Tests
{
    /// <summary>Discovery, lore pages, stock and the save layout. The save file on disk is never touched.</summary>
    public class ProgressAndSaveTests
    {
        private TestObjects objects;

        [SetUp]
        public void SetUp() => objects = new TestObjects();

        [TearDown]
        public void TearDown() => objects.DestroyAll();

        [Test]
        public void Discovery_CountsEachRecipeOnce()
        {
            RecipeData salt = objects.Recipe("TableSalt", "NaCl");
            var discovery = objects.NewGameObject("Pot").AddComponent<RecipeDiscovery>();
            int newCount = 0;
            discovery.RecipeDiscovered += _ => newCount++;

            discovery.MarkDiscovered(salt);
            discovery.MarkDiscovered(salt);
            discovery.MarkDiscovered(null);

            Assert.AreEqual(1, discovery.DiscoveredCount);
            Assert.AreEqual(1, newCount);
            Assert.IsTrue(discovery.IsDiscovered(salt));
        }

        [Test]
        public void Discovery_RestoreIsSilent()
        {
            RecipeData salt = objects.Recipe("TableSalt", "NaCl");
            RecipeData acid = objects.Recipe("CitricAcid", "C6H8O7");
            var discovery = objects.NewGameObject("Pot").AddComponent<RecipeDiscovery>();
            int newCount = 0;
            int progressCount = 0;
            discovery.RecipeDiscovered += _ => newCount++;
            discovery.ProgressChanged += () => progressCount++;

            discovery.RestoreDiscovered(new[] { acid, salt, acid });

            CollectionAssert.AreEqual(new[] { acid, salt }, discovery.Discovered);
            Assert.AreEqual(0, newCount, "Restored recipes must not show as new.");
            Assert.AreEqual(1, progressCount);

            discovery.ResetProgress();
            Assert.AreEqual(0, discovery.DiscoveredCount);
        }

        [Test]
        public void Stock_TracksOnlyListedItems()
        {
            KitchenItemData lemon = objects.KitchenItem("Lemon", null, PhysicalState.Solid);
            KitchenItemData flour = objects.KitchenItem("Flour", null, PhysicalState.Powder);
            var stock = objects.NewGameObject("Pot").AddComponent<HarvestStock>();
            stock.Set(lemon, 1);

            Assert.IsTrue(stock.TryTake(lemon));
            Assert.AreEqual(0, stock.Get(lemon));
            Assert.IsFalse(stock.TryTake(lemon), "Out of lemons.");

            stock.Add(lemon, 2);
            Assert.AreEqual(2, stock.Get(lemon));

            Assert.IsFalse(stock.IsTracked(flour));
            Assert.IsTrue(stock.TryTake(flour), "Unlisted items never run out.");
            Assert.AreEqual(int.MaxValue, stock.Get(flour));

            stock.Set(lemon, -5);
            Assert.AreEqual(0, stock.Get(lemon), "Counts never go below zero.");
        }

        [Test]
        public void Stock_ResetToStart_RefillsStartingCounts()
        {
            KitchenItemData salt = objects.KitchenItem("SaltJar", null, PhysicalState.Powder);
            var stock = objects.NewGameObject("Pot").AddComponent<HarvestStock>();
            var so = new SerializedObject(stock);
            SerializedProperty list = so.FindProperty("startingStock");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = salt;
            list.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = 3;
            so.ApplyModifiedPropertiesWithoutUndo();

            stock.Set(salt, 0);
            stock.ResetToStart();

            Assert.AreEqual(3, stock.Get(salt));
        }

        [Test]
        public void LoreBook_RestoreIsSilent_AndResetKeepsStartPages()
        {
            var startPage = objects.Track(ScriptableObject.CreateInstance<LoreEntryData>());
            startPage.unlockedAtStart = true;
            var laterPage = objects.Track(ScriptableObject.CreateInstance<LoreEntryData>());

            var book = objects.NewGameObject("Pot").AddComponent<LoreBook>();
            var so = new SerializedObject(book);
            SerializedProperty entries = so.FindProperty("entries");
            entries.arraySize = 2;
            entries.GetArrayElementAtIndex(0).objectReferenceValue = startPage;
            entries.GetArrayElementAtIndex(1).objectReferenceValue = laterPage;
            so.ApplyModifiedPropertiesWithoutUndo();

            int unlockEvents = 0;
            book.EntryUnlocked += _ => unlockEvents++;

            book.RestoreUnlocked(new[] { laterPage });
            Assert.IsTrue(book.IsUnlocked(laterPage));
            Assert.AreEqual(0, unlockEvents, "Restored pages must not show as new.");

            book.ResetProgress();
            Assert.IsTrue(book.IsUnlocked(startPage));
            Assert.IsFalse(book.IsUnlocked(laterPage));
        }

        [Test]
        public void SaveData_RoundTripsThroughJson()
        {
            var data = new SaveData { activeRecipe = "FizzyBubbles" };
            data.discoveredRecipes.Add("TableSalt");
            data.unlockedLore.Add("Halite");
            data.stock.Add(new SaveData.StockEntry { item = "Lemon", count = 2 });

            SaveData loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            Assert.AreEqual(SaveData.CurrentVersion, loaded.version);
            Assert.AreEqual("FizzyBubbles", loaded.activeRecipe);
            CollectionAssert.AreEqual(new[] { "TableSalt" }, loaded.discoveredRecipes);
            CollectionAssert.AreEqual(new[] { "Halite" }, loaded.unlockedLore);
            Assert.AreEqual(1, loaded.stock.Count);
            Assert.AreEqual("Lemon", loaded.stock[0].item);
            Assert.AreEqual(2, loaded.stock[0].count);
        }

        [Test]
        public void SaveData_HasNoPersonalFields()
        {
            // The save is local and child-safe: only progress, never names, emails or device ids.
            var allowed = new HashSet<string> { "version", "discoveredRecipes", "stock", "unlockedLore", "activeRecipe" };
            foreach (System.Reflection.FieldInfo field in typeof(SaveData).GetFields())
            {
                if (field.IsLiteral)
                {
                    continue;
                }

                Assert.IsTrue(allowed.Contains(field.Name),
                    $"New save field '{field.Name}': check it holds no personal data, then add it to this list.");
            }
        }
    }
}
