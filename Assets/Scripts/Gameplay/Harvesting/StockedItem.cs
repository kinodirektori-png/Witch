using TMPro;
using UnityEngine;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// Makes a kitchen item use up the witch's <see cref="HarvestStock"/>: each pour from the salt jar or each fresh
    /// lemon takes one, and an empty item asks the player to gather more in the garden. Shows how many are left.
    /// </summary>
    /// <remarks>
    /// <see cref="CauldronManager"/> asks it (as an <see cref="IPourGate"/>) before accepting a pour, and
    /// <see cref="SqueezeInteraction"/> asks it before putting a fresh lemon in the bowl.
    /// </remarks>
    [RequireComponent(typeof(ItemState))]
    public class StockedItem : MonoBehaviour, IPourGate
    {
        [SerializeField] private HarvestStock stock;

        [Tooltip("Which stock entry this uses. Defaults to the ItemState's start contents.")]
        [SerializeField] private KitchenItemData stockItem;

        [Tooltip("Optional label showing how many are left, e.g. \"x3\".")]
        [SerializeField] private TMP_Text countLabel;

        [SerializeField] private string countFormat = "x{0}";

        [Tooltip("On: each pour into the pot uses one (salt jar, baking soda box). Off: the stock is used some other " +
                 "way, e.g. a fresh lemon each time the bowl is refilled after squeezing.")]
        [SerializeField] private bool usedWhenPoured = true;

        [Tooltip("{0} = the item's name.")]
        [SerializeField] private string emptyHintFormat = "No {0} left! Gather more in the garden.";

        public KitchenItemData StockItem => stockItem;

        /// <summary>How many are left (int.MaxValue if the item doesn't run out).</summary>
        public int Count => stock != null ? stock.Get(stockItem) : int.MaxValue;

        public bool HasAny => Count > 0;

        public string EmptyHint => string.Format(emptyHintFormat, stockItem != null ? stockItem.displayName : "more");

        private void Awake()
        {
            if (stockItem == null)
            {
                var state = GetComponent<ItemState>();
                stockItem = state != null ? state.StartContents : null;
            }
        }

        private void OnEnable()
        {
            if (stock != null)
            {
                stock.Changed += HandleStockChanged;
            }

            RefreshLabel();
        }

        private void Start()
        {
            // The stock fills its counts in Awake; show them once everything has woken up.
            RefreshLabel();
        }

        private void OnDisable()
        {
            if (stock != null)
            {
                stock.Changed -= HandleStockChanged;
            }
        }

        /// <summary>Takes one from the stock. Returns false if there are none left.</summary>
        public bool TryUse() => stock == null || stock.TryTake(stockItem);

        bool IPourGate.CanPour(out string hint)
        {
            bool canPour = !usedWhenPoured || HasAny;
            hint = canPour ? string.Empty : EmptyHint;
            return canPour;
        }

        void IPourGate.OnPoured()
        {
            if (usedWhenPoured)
            {
                TryUse();
            }
        }

        private void HandleStockChanged(KitchenItemData item, int count)
        {
            if (item == stockItem)
            {
                RefreshLabel();
            }
        }

        private void RefreshLabel()
        {
            if (countLabel == null)
            {
                return;
            }

            bool tracked = stock != null && stock.IsTracked(stockItem);
            countLabel.gameObject.SetActive(tracked);
            if (tracked)
            {
                countLabel.text = string.Format(countFormat, stock.Get(stockItem));
            }
        }
    }
}
