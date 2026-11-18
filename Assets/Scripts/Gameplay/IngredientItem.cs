using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// Tells the game which <see cref="RecipeData"/> a draggable ingredient represents.
    /// Optionally shows the recipe's icon on an Image.
    /// </summary>
    [DisallowMultipleComponent]
    public class IngredientItem : MonoBehaviour
    {
        [SerializeField] private RecipeData recipe;

        [Tooltip("Optional. If set, its sprite is replaced by the recipe's itemIcon.")]
        [SerializeField] private Image iconImage;

        private static readonly List<IngredientItem> ActiveItems = new List<IngredientItem>();

        public RecipeData Recipe => recipe;

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

            foreach (IngredientItem item in ActiveItems)
            {
                if (item != null && item.recipe == ingredientRecipe)
                {
                    return item;
                }
            }

            return null;
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

        private void RefreshIcon()
        {
            if (iconImage != null && recipe != null && recipe.itemIcon != null)
            {
                iconImage.sprite = recipe.itemIcon;
            }
        }
    }
}
