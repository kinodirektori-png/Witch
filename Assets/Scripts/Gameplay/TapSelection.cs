using System;
using UnityEngine;

namespace Witch.Gameplay
{
    /// <summary>
    /// The item picked up by tapping instead of dragging (#21): tap a jar, then tap the pot. At most one item is
    /// selected at a time. <see cref="TapSelectable"/> selects; <see cref="ItemDropZone"/> delivers the selection.
    /// </summary>
    public static class TapSelection
    {
        /// <summary>Raised with the new selection (null when cleared).</summary>
        public static event Action<TapSelectable> Changed;

        public static TapSelectable Current { get; private set; }

        public static bool HasSelection => Current != null;

        /// <summary>Selects <paramref name="item"/>, or clears the selection if it is already selected.</summary>
        public static void Toggle(TapSelectable item)
        {
            Select(Current == item ? null : item);
        }

        public static void Select(TapSelectable item)
        {
            if (Current == item)
            {
                return;
            }

            TapSelectable previous = Current;
            Current = item;

            if (previous != null)
            {
                previous.SetSelectedVisual(false);
            }

            if (item != null)
            {
                item.SetSelectedVisual(true);
            }

            Changed?.Invoke(item);
        }

        public static void Clear() => Select(null);

        /// <summary>Clears the selection if it is <paramref name="item"/> (e.g. the item was used up or disabled).</summary>
        public static void Release(TapSelectable item)
        {
            if (Current == item)
            {
                Clear();
            }
        }

        // Domain reload may be off in Enter Play Mode settings, so static state is reset explicitly.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Current = null;
            Changed = null;
        }
    }
}
