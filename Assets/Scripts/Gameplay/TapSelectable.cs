using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Witch.Gameplay
{
    /// <summary>
    /// Tap-to-select alternative to dragging (#21), for children who find dragging hard: tap this item to pick it up,
    /// then tap the pot (or any <see cref="ItemDropZone"/>) to put it in. Tap it again to put it down.
    /// The selected item grows, wobbles and gets a thick outline, so selection never relies on colour alone.
    /// </summary>
    /// <remarks>
    /// Items whose taps already mean something else (the lemon: tap to squeeze) are left alone: if another component
    /// on this GameObject handles taps, this one doesn't select.
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public class TapSelectable : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Scale while selected. Size, not colour, shows the selection.")]
        [SerializeField, Min(1f)] private float selectedScale = 1.12f;

        [Tooltip("Gentle side-to-side wobble in degrees while selected. 0 turns it off (e.g. for reduced motion).")]
        [SerializeField, Min(0f)] private float wobbleDegrees = 4f;

        [SerializeField, Min(0f)] private float wobbleSpeed = 6f;

        [Tooltip("Optional marker shown only while selected (e.g. a hand icon or ring). An outline is added if empty.")]
        [SerializeField] private GameObject selectedMarker;

        [SerializeField] private Color outlineColor = new Color(0.15f, 0.1f, 0.2f, 1f);
        [SerializeField] private Vector2 outlineDistance = new Vector2(8f, -8f);

        private Vector3 restScale = Vector3.one;
        private Quaternion restRotation = Quaternion.identity;
        private Outline outline;
        private DraggableItem draggable;

        public bool IsSelected => TapSelection.Current == this;

        private void Awake()
        {
            restScale = transform.localScale;
            restRotation = transform.localRotation;
            draggable = GetComponent<DraggableItem>();

            if (selectedMarker == null)
            {
                var graphic = GetComponent<Graphic>();
                if (graphic != null)
                {
                    outline = gameObject.AddComponent<Outline>();
                    outline.effectColor = outlineColor;
                    outline.effectDistance = outlineDistance;
                    outline.enabled = false;
                }
            }
            else
            {
                selectedMarker.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (draggable != null)
            {
                draggable.DragStarted += HandleDragStarted;
            }
        }

        private void OnDisable()
        {
            if (draggable != null)
            {
                draggable.DragStarted -= HandleDragStarted;
            }

            // Used up by the pot, or hidden: it can't stay picked up.
            TapSelection.Release(this);
            SetSelectedVisual(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // A drag never ends in a click, so this is a real tap.
            if (eventData.button != PointerEventData.InputButton.Left || HasOtherTapHandler())
            {
                return;
            }

            TapSelection.Toggle(this);
        }

        /// <summary>Called by <see cref="TapSelection"/>; shows or hides the selected look.</summary>
        public void SetSelectedVisual(bool selected)
        {
            if (outline != null)
            {
                outline.enabled = selected;
            }

            if (selectedMarker != null)
            {
                selectedMarker.SetActive(selected);
            }

            transform.localScale = selected ? restScale * selectedScale : restScale;
            transform.localRotation = restRotation;
        }

        private void Update()
        {
            if (!IsSelected || wobbleDegrees <= 0f)
            {
                return;
            }

            float angle = Mathf.Sin(Time.unscaledTime * wobbleSpeed) * wobbleDegrees;
            transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, angle);
        }

        private void HandleDragStarted(DraggableItem item)
        {
            // Dragging works as before; picking it up by drag drops any tap selection.
            TapSelection.Clear();
        }

        private bool HasOtherTapHandler()
        {
            foreach (IPointerClickHandler handler in GetComponents<IPointerClickHandler>())
            {
                if (!ReferenceEquals(handler, this) && handler is Behaviour behaviour && behaviour.enabled)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
