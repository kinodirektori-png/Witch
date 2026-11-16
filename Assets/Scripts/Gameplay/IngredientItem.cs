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

        public RecipeData Recipe => recipe;

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
