using System;
using System.Collections.Generic;
using UnityEngine;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// How many of each kitchen item the witch has in reserve: spare lemons in the bowl, scoops left in the salt jar.
    /// Harvesting adds to it and using an item takes from it. Items that aren't listed are unlimited (e.g. flour).
    /// </summary>
    /// <remarks>
    /// In memory only. The inventory and save system (#19) can restore counts with <see cref="Set"/>.
    /// </remarks>
    public class HarvestStock : MonoBehaviour
    {
        [Serializable]
        public class StartingStock
        {
            public KitchenItemData item;
            [Min(0)] public int count = 3;
        }

        [Tooltip("Items that run out, and how many the player starts with. Unlisted items never run out.")]
        [SerializeField] private List<StartingStock> startingStock = new List<StartingStock>();

        /// <summary>Raised when a count changes, with the item and its new count.</summary>
        public event Action<KitchenItemData, int> Changed;

        private readonly Dictionary<KitchenItemData, int> counts = new Dictionary<KitchenItemData, int>();

        private void Awake()
        {
            foreach (StartingStock entry in startingStock)
            {
                if (entry != null && entry.item != null)
                {
                    counts[entry.item] = Mathf.Max(0, entry.count);
                }
            }
        }

        /// <summary>True if this item runs out (it is listed in the stock).</summary>
        public bool IsTracked(KitchenItemData item) => item != null && counts.ContainsKey(item);

        /// <summary>How many are left. Untracked items report int.MaxValue (unlimited).</summary>
        public int Get(KitchenItemData item)
        {
            if (item == null)
            {
                return 0;
            }

            return counts.TryGetValue(item, out int count) ? count : int.MaxValue;
        }

        /// <summary>Adds <paramref name="amount"/> (e.g. after harvesting). Starts tracking the item if needed.</summary>
        public void Add(KitchenItemData item, int amount)
        {
            if (item == null || amount <= 0)
            {
                return;
            }

            counts.TryGetValue(item, out int count);
            Set(item, count + amount);
        }

        /// <summary>Takes one if there is one. Untracked items always succeed.</summary>
        public bool TryTake(KitchenItemData item)
        {
            if (item == null)
            {
                return false;
            }

            if (!counts.TryGetValue(item, out int count))
            {
                return true;
            }

            if (count <= 0)
            {
                return false;
            }

            Set(item, count - 1);
            return true;
        }

        /// <summary>Sets a count directly (e.g. loading a save).</summary>
        public void Set(KitchenItemData item, int count)
        {
            if (item == null)
            {
                return;
            }

            counts[item] = Mathf.Max(0, count);
            Changed?.Invoke(item, counts[item]);
        }
    }
}
