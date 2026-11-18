using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Witch.Gameplay
{
    /// <summary>
    /// Pouring feedback for a draggable kitchen item (juice glass, baking soda box, water jug): when a vessel accepts
    /// it, the item tips over as if pouring, then rights itself while it returns to the shelf.
    /// </summary>
    /// <remarks>
    /// Presentation only. What gets poured is decided by <see cref="CauldronManager"/> from the item's
    /// <see cref="ItemState"/>; it calls <see cref="PlayPour"/>. Rotation is animated separately from position,
    /// so it never fights <see cref="DraggableItem"/>'s snap-back.
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public class PourSource : MonoBehaviour
    {
        [Tooltip("How far the item tips while pouring, in degrees. Negative tips to the right.")]
        [SerializeField] private float tiltAngle = -70f;

        [Tooltip("Seconds to tip over, hold and come back.")]
        [SerializeField, Min(0.05f)] private float duration = 0.6f;

        [Tooltip("Optional particles (drops or grains) played while pouring.")]
        [SerializeField] private ParticleSystem pourParticles;

        [Header("Events")]
        [Tooltip("Hook pour sounds here (#23).")]
        public UnityEvent onPour = new UnityEvent();

        private RectTransform rectTransform;
        private Quaternion restRotation;
        private Coroutine pourRoutine;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            restRotation = rectTransform.localRotation;
        }

        private void OnDisable()
        {
            if (pourRoutine != null)
            {
                StopCoroutine(pourRoutine);
                pourRoutine = null;
            }

            if (rectTransform != null)
            {
                rectTransform.localRotation = restRotation;
            }
        }

        /// <summary>Plays the tip-and-pour animation.</summary>
        public void PlayPour()
        {
            onPour.Invoke();

            if (pourParticles != null)
            {
                pourParticles.Play(true);
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

            if (pourRoutine != null)
            {
                StopCoroutine(pourRoutine);
            }

            pourRoutine = StartCoroutine(PourRoutine());
        }

        private IEnumerator PourRoutine()
        {
            Quaternion tipped = restRotation * Quaternion.Euler(0f, 0f, tiltAngle);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 0 -> 1 -> 0 with a short hold at the top: sin curve flattened at the peak.
                float tip = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI) * 1.4f);
                rectTransform.localRotation = Quaternion.SlerpUnclamped(restRotation, tipped, tip);
                yield return null;
            }

            rectTransform.localRotation = restRotation;
            pourRoutine = null;
        }
    }
}
