using UnityEngine;

namespace Witch.Gameplay
{
    /// <summary>
    /// Implement on a component that sits next to an <see cref="ItemDropZone"/> to decide whether a dropped
    /// item is accepted. Keeps "what happens on drop" (e.g. reaction checking) out of the drag code.
    /// </summary>
    public interface IItemDropReceiver
    {
        /// <summary>Return true to accept the item; false makes it snap back to where it was picked up.</summary>
        bool TryReceiveItem(GameObject droppedItem);
    }
}
