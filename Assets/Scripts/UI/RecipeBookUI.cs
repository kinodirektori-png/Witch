using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.UI
{
    /// <summary>
    /// Medea's recipe and lore book (Stage 2). Opens and closes from a HUD button and has two tabs:
    /// <b>Recipes</b> (everything the pot can brew; brewed ones show names, formula, equation and what you saw, the
    /// rest are silhouettes with a hint) and <b>Lore</b> (plants, crystals, herbs and minerals from the
    /// <see cref="LoreBook"/>, grouped by category, with a "Gather" action for #18). A "new" badge marks pages that
    /// appeared since the book was last read.
    /// </summary>
    /// <remarks>
    /// Only displays data: recipes come from <see cref="RecipeDiscovery"/> and its cauldron, lore from
    /// <see cref="LoreBook"/>. The panel is shown and hidden with its <see cref="CanvasGroup"/> so it blocks raycasts
    /// (drags and drops in the kitchen) only while it is open.
    /// </remarks>
    public class RecipeBookUI : MonoBehaviour
    {
        /// <summary>The two book tabs.</summary>
        public enum Tab
        {
            Recipes,
            Lore
        }

        [Header("Sources")]
        [SerializeField] private RecipeDiscovery discovery;
        [SerializeField] private LoreBook loreBook;

        [Header("Panel")]
        [Tooltip("CanvasGroup on the full-screen book panel. Hidden (alpha 0, no raycasts) while closed.")]
        [SerializeField] private CanvasGroup panel;

        [Tooltip("HUD button that opens and closes the book.")]
        [SerializeField] private Button openButton;

        [Tooltip("Large close button on the book page.")]
        [SerializeField] private Button closeButton;

        [Tooltip("Dot on the open button while something new is in the book.")]
        [SerializeField] private GameObject openButtonBadge;

        [SerializeField] private bool startOpen;

        [Header("Tabs")]
        [SerializeField] private Button recipesTabButton;
        [SerializeField] private Button loreTabButton;

        [Tooltip("Optional dots on the tabs while they have something new.")]
        [SerializeField] private GameObject recipesTabBadge;
        [SerializeField] private GameObject loreTabBadge;

        [SerializeField] private Color activeTabColor = new Color(1f, 0.85f, 0.45f);
        [SerializeField] private Color inactiveTabColor = new Color(0.75f, 0.68f, 0.6f);

        [Header("Content")]
        [SerializeField] private ScrollRect scrollRect;

        [Tooltip("Parent of the entries (with a VerticalLayoutGroup and ContentSizeFitter).")]
        [SerializeField] private RectTransform content;

        [Tooltip("Inactive template cloned for every entry. Keep it outside Content.")]
        [SerializeField] private BookEntryView entryTemplate;

        [Tooltip("Inactive template cloned for lore category headings. Keep it outside Content.")]
        [SerializeField] private TMP_Text headerTemplate;

        [Tooltip("Shown when a tab has nothing to list yet.")]
        [SerializeField] private string emptyText = "Nothing here yet. Keep brewing!";

        [Header("Events")]
        [Tooltip("Raised when \"Gather\" is pressed on an unlocked lore page. Harvesting (#18) listens to this.")]
        public UnityEvent<LoreEntryData> onGatherRequested = new UnityEvent<LoreEntryData>();

        /// <summary>Raised when "Gather" is pressed on an unlocked lore page.</summary>
        public event Action<LoreEntryData> GatherRequested;

        /// <summary>Raised with true when the book opens and false when it closes.</summary>
        public event Action<bool> OpenChanged;

        private static readonly LoreCategory[] CategoryOrder =
            { LoreCategory.Plant, LoreCategory.Herb, LoreCategory.Crystal, LoreCategory.Mineral };

        private static readonly Dictionary<LoreCategory, string> CategoryHeadings = new Dictionary<LoreCategory, string>
        {
            { LoreCategory.Plant, "Plants" },
            { LoreCategory.Herb, "Herbs" },
            { LoreCategory.Crystal, "Crystals" },
            { LoreCategory.Mineral, "Minerals" },
        };

        private readonly HashSet<ScriptableObject> unseen = new HashSet<ScriptableObject>();
        private readonly List<GameObject> spawned = new List<GameObject>();

        public bool IsOpen { get; private set; }

        public Tab CurrentTab { get; private set; } = Tab.Recipes;

        /// <summary>
        /// True once harvesting (#18) exists and calls <see cref="SetGatherAvailable"/>. Until then "Gather" is shown as
        /// "Gathering opens soon" and can't be tapped.
        /// </summary>
        public bool GatherAvailable { get; private set; }

        /// <summary>Lets harvesting (#18) switch the "Gather" buttons on.</summary>
        public void SetGatherAvailable(bool available)
        {
            GatherAvailable = available;
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void Awake()
        {
            if (entryTemplate != null)
            {
                entryTemplate.gameObject.SetActive(false);
            }

            if (headerTemplate != null)
            {
                headerTemplate.gameObject.SetActive(false);
            }

            SetPanelVisible(startOpen);
            IsOpen = startOpen;
        }

        private void OnEnable()
        {
            AddListener(openButton, Toggle);
            AddListener(closeButton, Close);
            AddListener(recipesTabButton, ShowRecipes);
            AddListener(loreTabButton, ShowLore);

            if (discovery != null)
            {
                discovery.RecipeDiscovered += HandleRecipeDiscovered;
                discovery.ProgressChanged += HandleProgressChanged;
            }

            if (loreBook != null)
            {
                loreBook.EntryUnlocked += HandleEntryUnlocked;
            }

            RefreshBadges();
        }

        private void OnDisable()
        {
            RemoveListener(openButton, Toggle);
            RemoveListener(closeButton, Close);
            RemoveListener(recipesTabButton, ShowRecipes);
            RemoveListener(loreTabButton, ShowLore);

            if (discovery != null)
            {
                discovery.RecipeDiscovered -= HandleRecipeDiscovered;
                discovery.ProgressChanged -= HandleProgressChanged;
            }

            if (loreBook != null)
            {
                loreBook.EntryUnlocked -= HandleEntryUnlocked;
            }
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            IsOpen = true;
            SetPanelVisible(true);
            Refresh();
            ScrollToTop();
            OpenChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            // Pages on the tab just read are no longer new.
            MarkTabSeen(CurrentTab);
            IsOpen = false;
            SetPanelVisible(false);
            ClearEntries();
            RefreshBadges();
            OpenChanged?.Invoke(false);
        }

        public void ShowRecipes() => ShowTab(Tab.Recipes);

        public void ShowLore() => ShowTab(Tab.Lore);

        public void ShowTab(Tab tab)
        {
            if (IsOpen && tab != CurrentTab)
            {
                MarkTabSeen(CurrentTab);
            }

            CurrentTab = tab;
            if (IsOpen)
            {
                Refresh();
                ScrollToTop();
            }
            else
            {
                Open();
            }
        }

        /// <summary>Rebuilds the visible tab. Called automatically when the book opens or data changes while open.</summary>
        public void Refresh()
        {
            ClearEntries();
            RefreshTabColors();

            if (CurrentTab == Tab.Recipes)
            {
                BuildRecipes();
            }
            else
            {
                BuildLore();
            }

            RefreshBadges();
        }

        private void BuildRecipes()
        {
            CauldronManager cauldron = discovery != null ? discovery.Cauldron : null;
            IReadOnlyList<RecipeData> recipes = cauldron != null ? cauldron.Recipes : null;
            if (recipes == null || recipes.Count == 0)
            {
                AddHeader(emptyText);
                return;
            }

            // Brewed recipes first (in the order they were found), then the ones still to discover.
            var shown = new HashSet<RecipeData>();
            foreach (RecipeData recipe in discovery.Discovered)
            {
                if (recipe != null && shown.Add(recipe))
                {
                    AddEntry().ShowRecipe(recipe, true, unseen.Contains(recipe));
                }
            }

            foreach (RecipeData recipe in recipes)
            {
                if (recipe != null && shown.Add(recipe))
                {
                    AddEntry().ShowRecipe(recipe, false, false);
                }
            }
        }

        private void BuildLore()
        {
            if (loreBook == null || loreBook.Entries.Count == 0)
            {
                AddHeader(emptyText);
                return;
            }

            foreach (LoreCategory category in CategoryOrder)
            {
                List<LoreEntryData> entries = loreBook.GetEntries(category);
                if (entries.Count == 0)
                {
                    continue;
                }

                AddHeader(CategoryHeadings[category]);

                // Unlocked pages first, then locked teasers.
                entries.Sort((a, b) => loreBook.IsUnlocked(b).CompareTo(loreBook.IsUnlocked(a)));
                foreach (LoreEntryData entry in entries)
                {
                    LoreEntryData captured = entry;
                    AddEntry().ShowLore(entry, loreBook.IsUnlocked(entry), unseen.Contains(entry),
                        () => RequestGather(captured), GatherAvailable);
                }
            }
        }

        private void RequestGather(LoreEntryData entry)
        {
            if (!GatherAvailable || entry == null || !loreBook.IsUnlocked(entry))
            {
                return;
            }

            GatherRequested?.Invoke(entry);
            onGatherRequested.Invoke(entry);
        }

        private BookEntryView AddEntry()
        {
            BookEntryView view = Instantiate(entryTemplate, content);
            view.gameObject.SetActive(true);
            spawned.Add(view.gameObject);
            return view;
        }

        private void AddHeader(string text)
        {
            if (headerTemplate == null || content == null)
            {
                return;
            }

            TMP_Text header = Instantiate(headerTemplate, content);
            header.text = text;
            header.gameObject.SetActive(true);
            spawned.Add(header.gameObject);
        }

        private void ClearEntries()
        {
            foreach (GameObject item in spawned)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }

            spawned.Clear();
        }

        private void MarkTabSeen(Tab tab)
        {
            unseen.RemoveWhere(item => tab == Tab.Recipes ? item is RecipeData : item is LoreEntryData);
        }

        private void HandleRecipeDiscovered(RecipeData recipe)
        {
            if (recipe != null)
            {
                unseen.Add(recipe);
            }

            RefreshIfOpen();
        }

        private void HandleEntryUnlocked(LoreEntryData entry)
        {
            if (entry != null)
            {
                unseen.Add(entry);
            }

            RefreshIfOpen();
        }

        private void HandleProgressChanged() => RefreshIfOpen();

        private void RefreshIfOpen()
        {
            if (IsOpen)
            {
                Refresh();
            }
            else
            {
                RefreshBadges();
            }
        }

        private void RefreshBadges()
        {
            bool newRecipes = false;
            bool newLore = false;
            foreach (ScriptableObject item in unseen)
            {
                newRecipes |= item is RecipeData;
                newLore |= item is LoreEntryData;
            }

            SetActive(openButtonBadge, newRecipes || newLore);
            SetActive(recipesTabBadge, newRecipes);
            SetActive(loreTabBadge, newLore);
        }

        private void RefreshTabColors()
        {
            SetTabColor(recipesTabButton, CurrentTab == Tab.Recipes);
            SetTabColor(loreTabButton, CurrentTab == Tab.Lore);
        }

        private void SetTabColor(Button tab, bool active)
        {
            if (tab != null && tab.targetGraphic != null)
            {
                tab.targetGraphic.color = active ? activeTabColor : inactiveTabColor;
            }
        }

        private void SetPanelVisible(bool visible)
        {
            if (panel == null)
            {
                return;
            }

            // CanvasGroup instead of SetActive: the closed book can't swallow taps or drops meant for the kitchen.
            panel.alpha = visible ? 1f : 0f;
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
        }

        private void ScrollToTop()
        {
            if (scrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void AddListener(Button button, UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveListener(Button button, UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }
    }
}
