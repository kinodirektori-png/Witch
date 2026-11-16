using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Witch.Gameplay
{
    /// <summary>
    /// Makes a UI element draggable with mouse or touch. While dragging it turns semi-transparent and stops
    /// blocking raycasts so the drop target underneath can receive OnDrop. If it is not dropped on an
    /// <see cref="ItemDropZone"/> that accepts it, it smoothly snaps back to where it was picked up.
    /// This component only handles movement; what a drop means is decided by the drop zone and its receivers.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Drag")]
        [Tooltip("Alpha applied to the CanvasGroup while dragging.")]
        [SerializeField, Range(0f, 1f)] private float dragAlpha = 0.6f;

        [Tooltip("How quickly the item catches up with the finger. Higher is snappier; 0 follows instantly.")]
        [SerializeField, Min(0f)] private float followSharpness = 25f;

        [Tooltip("Reparent to the root Canvas while dragging so the item renders above everything else.")]
        [SerializeField] private bool renderOnTopWhileDragging = true;

        [Header("Snap Back")]
        [Tooltip("Seconds to return to the start position after an invalid drop. 0 snaps instantly.")]
        [SerializeField, Min(0f)] private float snapBackDuration = 0.2f;

        /// <summary>Raised when the player picks the item up.</summary>
        public event Action<DraggableItem> DragStarted;

        /// <summary>Raised when the player lets go. The bool is true if a drop zone accepted the item.</summary>
        public event Action<DraggableItem, bool> DragEnded;

        public bool IsDragging { get; private set; }

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Canvas rootCanvas;

        private Transform initialParent;
        private int initialSiblingIndex;
        private Vector2 initialAnchoredPosition;

        private float originalAlpha;
        private bool originalBlocksRaycasts;

        private Vector2 pointerOffset;
        private Vector2 targetAnchoredPosition;
        private int activePointerId;
        private bool wasDroppedOnValidTarget;
        private Coroutine snapBackRoutine;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnDisable()
        {
            // Being consumed (e.g. by the cauldron) mid-drag must not leave the item invisible to raycasts.
            if (IsDragging)
            {
                RestoreCanvasGroup();
                IsDragging = false;
            }

            if (snapBackRoutine != null)
            {
                StopCoroutine(snapBackRoutine);
                snapBackRoutine = null;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (IsDragging || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (snapBackRoutine != null)
            {
                StopCoroutine(snapBackRoutine);
                snapBackRoutine = null;
            }

            rootCanvas = FindRootCanvas();
            if (rootCanvas == null)
            {
                Debug.LogWarning($"{nameof(DraggableItem)} on '{name}' is not under a Canvas and cannot be dragged.", this);
                return;
            }

            IsDragging = true;
            activePointerId = eventData.pointerId;
            wasDroppedOnValidTarget = false;

            initialParent = rectTransform.parent;
            initialSiblingIndex = rectTransform.GetSiblingIndex();
            initialAnchoredPosition = rectTransform.anchoredPosition;

            originalAlpha = canvasGroup.alpha;
            originalBlocksRaycasts = canvasGroup.blocksRaycasts;
            canvasGroup.alpha = dragAlpha;
            canvasGroup.blocksRaycasts = false;

            if (renderOnTopWhileDragging)
            {
                rectTransform.SetParent(rootCanvas.transform, worldPositionStays: true);
                rectTransform.SetAsLastSibling();
            }

            // Keep the grab point under the finger instead of jumping the item's pivot to it.
            if (TryGetLocalPointerPosition(eventData, out Vector2 localPointer))
            {
                pointerOffset = rectTransform.anchoredPosition - localPointer;
            }
            else
            {
                pointerOffset = Vector2.zero;
            }

            targetAnchoredPosition = rectTransform.anchoredPosition;
            DragStarted?.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging || eventData.pointerId != activePointerId)
            {
                return;
            }

            if (TryGetLocalPointerPosition(eventData, out Vector2 localPointer))
            {
                targetAnchoredPosition = localPointer + pointerOffset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging || eventData.pointerId != activePointerId)
            {
                return;
            }

            // Unity calls OnDrop on the target before OnEndDrag, so wasDroppedOnValidTarget is final here.
            IsDragging = false;
            RestoreCanvasGroup();

            if (!wasDroppedOnValidTarget)
            {
                SnapBack();
            }

            DragEnded?.Invoke(this, wasDroppedOnValidTarget);
        }

        /// <summary>
        /// Called by <see cref="ItemDropZone"/> when it accepts this item. Optionally parents the item to the zone.
        /// </summary>
        public void AcceptDrop(RectTransform newParent = null)
        {
            wasDroppedOnValidTarget = true;

            if (newParent != null)
            {
                rectTransform.SetParent(newParent, worldPositionStays: false);
                rectTransform.anchoredPosition = Vector2.zero;
                targetAnchoredPosition = Vector2.zero;
            }
        }

        /// <summary>Sends the item back to where it was last picked up.</summary>
        public void SnapBack()
        {
            if (initialParent == null)
            {
                return;
            }

            // Reparent first (keeping the on-screen position) so we can animate in the original parent's space.
            rectTransform.SetParent(initialParent, worldPositionStays: true);
            rectTransform.SetSiblingIndex(initialSiblingIndex);

            if (snapBackDuration <= 0f || !isActiveAndEnabled)
            {
                rectTransform.anchoredPosition = initialAnchoredPosition;
                return;
            }

            snapBackRoutine = StartCoroutine(SnapBackRoutine());
        }

        private void Update()
        {
            if (!IsDragging)
            {
                return;
            }

            if (followSharpness <= 0f)
            {
                rectTransform.anchoredPosition = targetAnchoredPosition;
                return;
            }

            // Frame-rate independent exponential smoothing.
            float blend = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetAnchoredPosition, blend);
        }

        private IEnumerator SnapBackRoutine()
        {
            Vector2 start = rectTransform.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < snapBackDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / snapBackDuration);
                float eased = 1f - (1f - t) * (1f - t); // ease-out quad
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, initialAnchoredPosition, eased);
                yield return null;
            }

            rectTransform.anchoredPosition = initialAnchoredPosition;
            snapBackRoutine = null;
        }

        private bool TryGetLocalPointerPosition(PointerEventData eventData, out Vector2 localPoint)
        {
            var parentRect = rectTransform.parent as RectTransform;
            if (parentRect == null)
            {
                localPoint = Vector2.zero;
                return false;
            }

            // Screen Space - Overlay canvases must pass a null camera.
            Camera eventCamera = rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : eventData.pressEventCamera;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventCamera, out localPoint);
        }

        private Canvas FindRootCanvas()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas : null;
        }

        private void RestoreCanvasGroup()
        {
            canvasGroup.alpha = originalAlpha;
            canvasGroup.blocksRaycasts = originalBlocksRaycasts;
        }
    }
}
