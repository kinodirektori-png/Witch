using System;
using UnityEngine;
using UnityEngine.Events;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// A place in nature where the witch can gather something, tied to a <see cref="LoreEntryData"/> page: the lemon
    /// tree gives lemons, a rock salt outcrop gives salt. It can only be used once the page is unlocked in the
    /// <see cref="LoreBook"/>, and needs a short time to regrow after each harvest.
    /// </summary>
    /// <remarks>
    /// Logic only. Input and visuals live in <c>Witch.UI.HarvestSpotView</c>, which calls <see cref="TryHarvest"/>.
    /// </remarks>
    public class HarvestSpot : MonoBehaviour
    {
        [Tooltip("The lore page this spot belongs to. Its Gather Yield and Restocks Kitchen Item say what you get.")]
        [SerializeField] private LoreEntryData entry;

        [SerializeField] private LoreBook loreBook;
        [SerializeField] private HarvestStock stock;

        [Tooltip("How many of the kitchen item one harvest gives.")]
        [SerializeField, Min(1)] private int amountPerHarvest = 1;

        [Tooltip("Seconds before the spot can be harvested again.")]
        [SerializeField, Min(0f)] private float regrowSeconds = 3f;

        [Header("Events")]
        [Tooltip("Raised after a harvest with a message like \"+1 Lemon\". Hook sounds here (#23).")]
        public UnityEvent<string> onHarvested = new UnityEvent<string>();

        /// <summary>Raised after a harvest with the kitchen item and the amount gathered.</summary>
        public event Action<HarvestSpot, KitchenItemData, int> Harvested;

        /// <summary>Raised when the spot unlocks, is harvested or finishes regrowing.</summary>
        public event Action<HarvestSpot> StateChanged;

        private float readyAt;
        private bool wasReady = true;

        public LoreEntryData Entry => entry;

        public int AmountPerHarvest => amountPerHarvest;

        /// <summary>The kitchen item this spot restocks, or null.</summary>
        public KitchenItemData KitchenItem => entry != null ? entry.restocksKitchenItem : null;

        /// <summary>True once the lore page is unlocked (or if there is no book to ask).</summary>
        public bool IsUnlocked => entry != null && (loreBook == null || loreBook.IsUnlocked(entry));

        /// <summary>True while the spot is regrowing after a harvest.</summary>
        public bool IsRegrowing => Time.time < readyAt;

        /// <summary>0..1 how far the spot has regrown (1 = ready).</summary>
        public float RegrowProgress => regrowSeconds <= 0f || !IsRegrowing ? 1f : 1f - (readyAt - Time.time) / regrowSeconds;

        public bool CanHarvest => IsUnlocked && !IsRegrowing && KitchenItem != null;

        /// <summary>Hint shown on a spot that can't be used yet.</summary>
        public string LockedHint => "Read about it in the book first!";

        private void OnEnable()
        {
            if (loreBook != null)
            {
                loreBook.EntryUnlocked += HandleEntryUnlocked;
            }
        }

        private void OnDisable()
        {
            if (loreBook != null)
            {
                loreBook.EntryUnlocked -= HandleEntryUnlocked;
            }
        }

        private void Update()
        {
            bool ready = !IsRegrowing;
            if (ready != wasReady)
            {
                wasReady = ready;
                StateChanged?.Invoke(this);
            }
        }

        /// <summary>Gathers from the spot. Returns false if it is locked, regrowing or has nothing to give.</summary>
        public bool TryHarvest()
        {
            if (!CanHarvest)
            {
                return false;
            }

            KitchenItemData item = KitchenItem;
            if (stock != null)
            {
                stock.Add(item, amountPerHarvest);
            }

            readyAt = Time.time + regrowSeconds;
            wasReady = regrowSeconds <= 0f;

            Harvested?.Invoke(this, item, amountPerHarvest);
            onHarvested.Invoke($"+{amountPerHarvest} {item.displayName}");
            StateChanged?.Invoke(this);
            return true;
        }

        private void HandleEntryUnlocked(LoreEntryData unlocked)
        {
            if (unlocked == entry)
            {
                StateChanged?.Invoke(this);
            }
        }
    }
}
