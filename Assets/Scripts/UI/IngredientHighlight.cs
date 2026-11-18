using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Witch.Gameplay;

namespace Witch.UI
{
    /// <summary>
    /// Pulses an ingredient (and an optional glowing outline around it) when <see cref="HintTracker"/> picks it as the hint.
    /// Stops when the player starts dragging it or when the time runs out. Never changes raycast settings, so
    /// drag-and-drop works normally while it pulses.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class IngredientHighlight : MonoBehaviour, IHintHighlightable
    {
        [Tooltip("Optional UI Outline (or Shadow) effect on the ingredient's Image, used as a glow. " +
                 "Enabled only while highlighted; its alpha pulses.")]
        [SerializeField] private Shadow glow;

        [Tooltip("Scale at the top of each pulse.")]
        [SerializeField, Min(1f)] private float pulseScale = 1.15f;

        [Tooltip("Pulses per second.")]
        [SerializeField, Min(0.1f)] private float pulseSpeed = 1.5f;

        private RectTransform rectTransform;
        private DraggableItem draggable;
        private Vector3 baseScale;
        private Coroutine pulseRoutine;

        public bool IsHighlighted => pulseRoutine != null;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            draggable = GetComponent<DraggableItem>();
            baseScale = rectTransform.localScale;
            SetGlowVisible(false);
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

            HideHint();
        }

        public void ShowHint(float duration)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            HideHint();
            pulseRoutine = StartCoroutine(PulseRoutine(duration));
        }

        public void HideHint()
        {
            if (pulseRoutine != null)
            {
                StopCoroutine(pulseRoutine);
                pulseRoutine = null;
            }

            if (rectTransform != null)
            {
                rectTransform.localScale = baseScale;
            }

            SetGlowVisible(false);
        }

        private IEnumerator PulseRoutine(float duration)
        {
            SetGlowVisible(true);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // 0 -> 1 -> 0 once per pulse.
                float wave = 0.5f - 0.5f * Mathf.Cos(elapsed * pulseSpeed * 2f * Mathf.PI);
                rectTransform.localScale = baseScale * Mathf.Lerp(1f, pulseScale, wave);
                if (glow != null)
                {
                    Color color = glow.effectColor;
                    color.a = Mathf.Lerp(0.35f, 1f, wave);
                    glow.effectColor = color;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            pulseRoutine = null;
            HideHint();
        }

        private void HandleDragStarted(DraggableItem item) => HideHint();

        private void SetGlowVisible(bool visible)
        {
            if (glow != null)
            {
                glow.enabled = visible;
            }
        }
    }
}
