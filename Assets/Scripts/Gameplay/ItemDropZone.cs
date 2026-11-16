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
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ItemDropZone : MonoBehaviour, IDropHandler
    {
        [Tooltip("Parent accepted items to this zone and centre them. Turn off if a receiver moves or consumes them.")]
        [SerializeField] private bool parentAcceptedItems = true;

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

            if (!IsAccepted(droppedObject))
            {
                onItemRejected.Invoke(droppedObject);
                return;
            }

            // The receiver may already have consumed (disabled/destroyed) the item.
            bool itemStillActive = droppedObject != null && droppedObject.activeInHierarchy;
            draggable.AcceptDrop(parentAcceptedItems && itemStillActive ? itemContainer : null);
            onItemAccepted.Invoke(droppedObject);
        }

        private bool IsAccepted(GameObject droppedObject)
        {
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
