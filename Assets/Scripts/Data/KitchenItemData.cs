using UnityEngine;

namespace Witch.Data
{
    /// <summary>
    /// An everyday object kids recognise in a home kitchen (a lemon in a bowl, a box of baking soda, a jug of water)
    /// and the chemistry inside it. The <see cref="substance"/> is the real chemical (e.g. Citric Acid); this asset is
    /// the thing the player picks up, with the form it starts in and what can be done to it.
    /// Create assets via: Assets > Create > Witch > Kitchen Item Data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewKitchenItem", menuName = "Witch/Kitchen Item Data", order = 2)]
    public class KitchenItemData : ScriptableObject
    {
        [Tooltip("What kids call it, e.g. \"Lemon\" or \"Baking Soda\".")]
        public string displayName = "Lemon";

        [Tooltip("Where it sits in the kitchen, e.g. \"in the fruit bowl\". Shown as a small caption.")]
        public string placeDescription = "in the fruit bowl";

        [Tooltip("Sprite shown on the shelf or counter. Leave empty to use the placeholder colour.")]
        public Sprite shelfSprite;

        [Tooltip("Placeholder colour used until the art is ready, and the tint it gives a mixture in the pot.")]
        public Color placeholderColor = new Color(1f, 0.86f, 0.25f);

        [Header("Chemistry")]
        [Tooltip("The real substance inside, e.g. Citric Acid for a lemon. Leave empty for water.")]
        public RecipeData substance;

        [Tooltip("The form the item starts in: a lemon is Solid, baking soda is Powder, juice is Liquid.")]
        public PhysicalState startState = PhysicalState.Solid;

        [Tooltip("Tick for plain water. Water dilutes mixtures and never counts as a recipe ingredient.")]
        public bool isWater;

        [Header("Pouring")]
        [Tooltip("How many parts go into the pot per pour. Water uses this as parts of water.")]
        [Min(0.1f)] public float pourAmount = 1f;

        [Tooltip("On: the container is empty after one pour (a glass of juice). Off: it can pour again (a salt jar).")]
        public bool emptiesWhenPoured;

        [Header("Squeezing")]
        [Tooltip("What squeezing produces, e.g. Lemon Juice. Leave empty if it can't be squeezed.")]
        public KitchenItemData squeezeYield;

        [Tooltip("Sprite after squeezing (e.g. lemon halves). Leave empty to darken the placeholder instead.")]
        public Sprite squeezedSprite;

        [Tooltip("Taps needed to squeeze it completely.")]
        [Min(1)] public int squeezeTaps = 3;

        /// <summary>True if this item can be squeezed into something else.</summary>
        public bool CanBeSqueezed => squeezeYield != null;

        private void OnValidate()
        {
            displayName = displayName?.Trim();
            placeDescription = placeDescription?.Trim();

            if (isWater && substance != null)
            {
                Debug.LogWarning($"Kitchen item '{name}' is water but also has a substance. Water ignores the substance.", this);
            }

            if (squeezeYield == this)
            {
                Debug.LogWarning($"Kitchen item '{name}' squeezes into itself. Pick a different Squeeze Yield.", this);
                squeezeYield = null;
            }

            if (squeezeYield != null && squeezeYield.startState != PhysicalState.Liquid)
            {
                Debug.LogWarning($"Kitchen item '{name}' squeezes into '{squeezeYield.name}', which doesn't start as a Liquid.", this);
            }
        }
    }
}
