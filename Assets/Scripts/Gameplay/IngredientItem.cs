using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// Tells the game which <see cref="RecipeData"/> a draggable ingredient represents.
    /// Optionally shows the recipe's icon on an Image. If the item also has an <see cref="ItemState"/> (kitchen items),
    /// the ingredient is whatever that state currently holds, so an empty glass or a squeezed lemon holds nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public class IngredientItem : MonoBehaviour
    {
        [SerializeField] private RecipeData recipe;

        [Tooltip("Optional. If set, its sprite is replaced by the recipe's itemIcon.")]
        [SerializeField] private Image iconImage;

        [Tooltip("Off for kitchen items: their ItemState shows the everyday object (lemon, jar) instead of the chemical's icon.")]
        [SerializeField] private bool useRecipeIcon = true;

        private ItemState itemState;
        private bool itemStateLooked;

        private static readonly List<IngredientItem> ActiveItems = new List<IngredientItem>();

        /// <summary>
        /// The ingredient this item gives right now: the <see cref="ItemState"/>'s substance if there is one
        /// (null while empty or squeezed), otherwise the assigned recipe.
        /// </summary>
        public RecipeData Recipe
        {
            get
            {
                ItemState state = GetItemState();
                return state != null ? state.Substance : recipe;
            }
        }

        /// <summary>
        /// Returns an enabled ingredient in the scene that holds <paramref name="ingredientRecipe"/>, or null.
        /// Used by hints; avoids scene-wide Find calls.
        /// </summary>
        public static IngredientItem FindActive(RecipeData ingredientRecipe)
        {
            if (ingredientRecipe == null)
            {
                return null;
            }

            // Prefer an item that is ready to pour (the glass of juice) over one that still needs work (the whole lemon).
            IngredientItem fallback = null;
            foreach (IngredientItem item in ActiveItems)
            {
                if (item == null || item.Recipe != ingredientRecipe)
                {
                    continue;
                }

                ItemState state = item.GetItemState();
                if (state == null || state.CanPour)
                {
                    return item;
                }

                if (fallback == null)
                {
                    fallback = item;
                }
            }

            return fallback;
        }

        // Keeps the registry clean when domain reload is disabled in Enter Play Mode settings.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => ActiveItems.Clear();

        private void OnEnable() => ActiveItems.Add(this);

        private void OnDisable() => ActiveItems.Remove(this);

        /// <summary>Assigns a recipe at runtime (e.g. when spawning harvested ingredients).</summary>
        public void SetRecipe(RecipeData newRecipe)
        {
            recipe = newRecipe;
            RefreshIcon();
        }

        private void Awake()
        {
            RefreshIcon();
        }

        private void OnValidate()
        {
            if (iconImage == null)
            {
                iconImage = GetComponent<Image>();
            }

            RefreshIcon();
        }

        private ItemState GetItemState()
        {
            // Cached lazily so Recipe works before Awake (e.g. from editor code) and costs nothing afterwards.
            if (!itemStateLooked || (itemState == null && Application.isEditor && !Application.isPlaying))
            {
                itemState = GetComponent<ItemState>();
                itemStateLooked = true;
            }

            return itemState;
        }

        private void RefreshIcon()
        {
            if (useRecipeIcon && iconImage != null && recipe != null && recipe.itemIcon != null)
            {
                iconImage.sprite = recipe.itemIcon;
            }
        }
    }
}
