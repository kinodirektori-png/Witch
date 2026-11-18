using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Witch.Gameplay
{
    /// <summary>
    /// A valid target for <see cref="DraggableItem"/>s. Needs a raycast-target Graphic (e.g. an Image) on the same
    /// GameObject to receive drops. Every <see cref="IItemDropReceiver"/> on this GameObject is asked whether it
    /// accepts the item; with no receivers the zone accepts everything (e.g. a plain shelf or desk slot).
    /// Items that are rejected snap back to where they were picked up.
    /// Also takes the item picked up by tapping (<see cref="TapSelection"/>, #21): tap the item, then tap the zone.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ItemDropZone : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        /// <summary>What happens to an item after this zone accepts it.</summary>
        public enum AcceptedItemBehaviour
        {
            /// <summary>Parent the item to the zone (or Item Container) and centre it, e.g. a desk slot.</summary>
            ParentToZone,
            /// <summary>Leave the item where it was dropped. Use when a receiver moves or consumes it.</summary>
            LeaveWhereDropped,
            /// <summary>Send the item back to where it was picked up, e.g. a reusable ingredient on a shelf.</summary>
            ReturnToStart
        }

        [Tooltip("ParentToZone: snap into this zone. LeaveWhereDropped: a receiver handles it (e.g. consumes it). ReturnToStart: go back to the shelf.")]
        [SerializeField] private AcceptedItemBehaviour acceptedItemBehaviour = AcceptedItemBehaviour.ParentToZone;

        [Tooltip("Optional container for accepted items. Defaults to this zone's RectTransform.")]
        [SerializeField] private RectTransform itemContainer;

        [Header("Events")]
        public UnityEvent<GameObject> onItemAccepted = new UnityEvent<GameObject>();
        public UnityEvent<GameObject> onItemRejected = new UnityEvent<GameObject>();

        private IItemDropReceiver[] receivers;

        private void Awake()
        {
            receivers = GetComponents<IItemDropReceiver>();
            if (itemContainer == null)
            {
                itemContainer = (RectTransform)transform;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            GameObject droppedObject = eventData.pointerDrag;
            if (droppedObject == null)
            {
                return;
            }

            var draggable = droppedObject.GetComponent<DraggableItem>();
            if (draggable == null || !draggable.IsDragging)
            {
                return;
            }

            Deliver(droppedObject, draggable);
        }

        /// <summary>Tap-to-select: a tap on the zone delivers the selected item, as if it had been dropped here.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !TapSelection.HasSelection)
            {
                return;
            }

            TapSelectable selected = TapSelection.Current;
            if (TryDeliverSelected(selected))
            {
                TapSelection.Release(selected);
            }
        }

        /// <summary>
        /// Delivers a tap-selected item. Returns true if the zone accepted it. A refused item stays selected and
        /// where it is, so the child can try somewhere else.
        /// </summary>
        public bool TryDeliverSelected(TapSelectable selected)
        {
            if (selected == null || !selected.isActiveAndEnabled)
            {
                return false;
            }

            return Deliver(selected.gameObject, selected.GetComponent<DraggableItem>());
        }

        private bool Deliver(GameObject item, DraggableItem draggable)
        {
            if (!IsAccepted(item))
            {
                onItemRejected.Invoke(item);
                return false;
            }

            // The receiver may already have consumed (disabled/destroyed) the item.
            bool itemStillActive = item != null && item.activeInHierarchy;
            bool parentToZone = acceptedItemBehaviour == AcceptedItemBehaviour.ParentToZone && itemStillActive;
            if (itemContainer == null)
            {
                itemContainer = (RectTransform)transform;
            }

            if (draggable != null && draggable.IsDragging)
            {
                draggable.AcceptDrop(parentToZone ? itemContainer : null);

                if (acceptedItemBehaviour == AcceptedItemBehaviour.ReturnToStart && itemStillActive)
                {
                    draggable.SnapBack();
                }
            }
            else if (parentToZone)
            {
                // Tapped, not dragged: the item is still in its original place, so move it into the zone directly.
                var itemRect = (RectTransform)item.transform;
                itemRect.SetParent(itemContainer, worldPositionStays: false);
                itemRect.anchoredPosition = Vector2.zero;
            }

            onItemAccepted.Invoke(item);
            return true;
        }

        private bool IsAccepted(GameObject droppedObject)
        {
            receivers ??= GetComponents<IItemDropReceiver>();
            if (receivers == null || receivers.Length == 0)
            {
                return true;
            }

            foreach (IItemDropReceiver receiver in receivers)
            {
                if (receiver != null && receiver.TryReceiveItem(droppedObject))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
