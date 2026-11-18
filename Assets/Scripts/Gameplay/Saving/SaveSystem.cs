using System.Collections.Generic;
using UnityEngine;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// Keeps the player's progress between sessions: discovered recipes, unlocked lore pages, how many lemons and
    /// scoops are left, and the recipe they were working on. Loads once on start and saves (debounced) whenever
    /// something changes, plus right away when the app is paused or closed.
    /// </summary>
    /// <remarks>
    /// Runs after the other kitchen components so their own Start can't overwrite what was loaded.
    /// The save is local JSON only (see <see cref="SaveFile"/>).
    /// </remarks>
    [DefaultExecutionOrder(100)]
    public class SaveSystem : MonoBehaviour
    {
        [SerializeField] private CauldronManager cauldron;
        [SerializeField] private RecipeDiscovery discovery;
        [SerializeField] private LoreBook loreBook;
        [SerializeField] private HarvestStock stock;

        [Tooltip("Wait this long after the last change before writing, so a burst of changes is one write.")]
        [SerializeField, Min(0f)] private float saveDelaySeconds = 1f;

        [Tooltip("Turn off to test without touching the save file.")]
        [SerializeField] private bool loadOnStart = true;

        private bool dirty;
        private bool suppressDirty;
        private bool loaded;
        private float saveAt;

        /// <summary>True if progress was restored from a save this session.</summary>
        public bool LoadedFromSave { get; private set; }

        private void Awake()
        {
            if (cauldron == null) cauldron = GetComponentInParent<CauldronManager>();
            if (discovery == null) discovery = GetComponentInParent<RecipeDiscovery>();
            if (loreBook == null) loreBook = GetComponentInParent<LoreBook>();
            if (stock == null) stock = GetComponentInParent<HarvestStock>();
        }

        private void OnEnable()
        {
            if (discovery != null) discovery.ProgressChanged += MarkDirty;
            if (loreBook != null) loreBook.EntryUnlocked += OnLoreUnlocked;
            if (stock != null) stock.Changed += OnStockChanged;
            if (cauldron != null) cauldron.ActiveRecipeChanged += OnActiveRecipeChanged;
        }

        private void OnDisable()
        {
            if (discovery != null) discovery.ProgressChanged -= MarkDirty;
            if (loreBook != null) loreBook.EntryUnlocked -= OnLoreUnlocked;
            if (stock != null) stock.Changed -= OnStockChanged;
            if (cauldron != null) cauldron.ActiveRecipeChanged -= OnActiveRecipeChanged;
        }

        private void Start()
        {
            if (loadOnStart)
            {
                Load();
            }

            loaded = true;
            dirty = false;
        }

        private void Update()
        {
            if (dirty && Time.unscaledTime >= saveAt)
            {
                Save();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && loaded)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            if (loaded)
            {
                Save();
            }
        }

        /// <summary>Writes the current progress now.</summary>
        public void Save()
        {
            dirty = false;
            SaveFile.Write(Capture());
        }

        /// <summary>Reads the save and applies it. Returns false (fresh game) if there is none or it is unreadable.</summary>
        public bool Load()
        {
            if (!SaveFile.TryLoad(out SaveData data))
            {
                return false;
            }

            suppressDirty = true;
            try
            {
                Apply(data);
            }
            finally
            {
                suppressDirty = false;
            }

            LoadedFromSave = true;
            return true;
        }

        /// <summary>Starts a new game: forgets discoveries and pages, refills the stock and deletes the save.</summary>
        public void ResetProgress()
        {
            suppressDirty = true;
            try
            {
                if (discovery != null) discovery.ResetProgress();
                if (loreBook != null) loreBook.ResetProgress();
                if (stock != null) stock.ResetToStart();
                if (cauldron != null && cauldron.Recipes.Count > 0) cauldron.SetActiveRecipe(0);
            }
            finally
            {
                suppressDirty = false;
            }

            dirty = false;
            LoadedFromSave = false;
            SaveFile.Delete();
        }

        /// <summary>Builds a snapshot of the current progress.</summary>
        public SaveData Capture()
        {
            var data = new SaveData();

            if (discovery != null)
            {
                foreach (RecipeData recipe in discovery.Discovered)
                {
                    if (recipe != null) data.discoveredRecipes.Add(recipe.name);
                }
            }

            if (loreBook != null)
            {
                foreach (LoreEntryData entry in loreBook.GetUnlockedEntries())
                {
                    if (entry != null) data.unlockedLore.Add(entry.name);
                }
            }

            if (stock != null)
            {
                foreach (KeyValuePair<KitchenItemData, int> pair in stock.Counts)
                {
                    if (pair.Key != null)
                    {
                        data.stock.Add(new SaveData.StockEntry { item = pair.Key.name, count = pair.Value });
                    }
                }
            }

            if (cauldron != null && cauldron.ActiveRecipe != null)
            {
                data.activeRecipe = cauldron.ActiveRecipe.name;
            }

            return data;
        }

        private void Apply(SaveData data)
        {
            if (cauldron != null && discovery != null)
            {
                var found = new List<RecipeData>();
                foreach (string recipeName in data.discoveredRecipes)
                {
                    RecipeData recipe = FindRecipe(recipeName);
                    if (recipe != null) found.Add(recipe);
                }

                discovery.RestoreDiscovered(found);
            }

            if (loreBook != null)
            {
                var pages = new List<LoreEntryData>();
                foreach (string entryName in data.unlockedLore)
                {
                    LoreEntryData entry = FindByName(loreBook.Entries, entryName);
                    if (entry != null) pages.Add(entry);
                }

                loreBook.RestoreUnlocked(pages);
            }

            if (stock != null)
            {
                var items = new List<KitchenItemData>(stock.Counts.Keys);
                foreach (SaveData.StockEntry entry in data.stock)
                {
                    // Only items that still run out in this build; anything removed from the game is ignored.
                    KitchenItemData item = entry != null ? FindByName(items, entry.item) : null;
                    if (item != null) stock.Set(item, entry.count);
                }
            }

            if (cauldron != null && !string.IsNullOrEmpty(data.activeRecipe))
            {
                RecipeData active = FindRecipe(data.activeRecipe);
                if (active != null) cauldron.SetActiveRecipe(active);
            }
        }

        private RecipeData FindRecipe(string recipeName) =>
            cauldron != null ? FindByName(cauldron.Recipes, recipeName) : null;

        private static T FindByName<T>(IEnumerable<T> assets, string assetName) where T : Object
        {
            if (assets == null || string.IsNullOrEmpty(assetName))
            {
                return null;
            }

            foreach (T asset in assets)
            {
                if (asset != null && asset.name == assetName)
                {
                    return asset;
                }
            }

            return null;
        }

        private void MarkDirty()
        {
            if (suppressDirty || !loaded)
            {
                return;
            }

            dirty = true;
            saveAt = Time.unscaledTime + saveDelaySeconds;
        }

        private void OnLoreUnlocked(LoreEntryData entry) => MarkDirty();

        private void OnStockChanged(KitchenItemData item, int count) => MarkDirty();

        private void OnActiveRecipeChanged(RecipeData recipe) => MarkDirty();
    }
}
