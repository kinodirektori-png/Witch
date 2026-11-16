using System.Collections;
using TMPro;
using UnityEngine;

namespace Witch.UI
{
    /// <summary>
    /// Temporary popup that floats up and fades out, used to show the chemical formula of a brewed potion.
    /// Does not block touches so the player can keep dragging ingredients while it is visible.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class FormulaPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text formulaLabel;

        [Tooltip("Optional second line, e.g. \"Sodium Chloride\".")]
        [SerializeField] private TMP_Text subtitleLabel;

        [Header("Animation")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.15f;
        [SerializeField, Min(0f)] private float holdDuration = 1.5f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;

        [Tooltip("How far the popup rises (in canvas units) over its lifetime.")]
        [SerializeField] private float riseDistance = 60f;

        [Tooltip("Scale at the start of the fade-in; it pops up to 1.")]
        [SerializeField, Min(0f)] private float startScale = 0.7f;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector2 restingPosition;
        private Coroutine showRoutine;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            canvasGroup = GetComponent<CanvasGroup>();
            restingPosition = rectTransform.anchoredPosition;

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        /// <summary>Shows the popup. <paramref name="formula"/> may contain TMP rich text such as &lt;sub&gt;.</summary>
        public void Show(string formula, string subtitle = null)
        {
            if (formulaLabel != null)
            {
                formulaLabel.text = formula;
            }

            if (subtitleLabel != null)
            {
                bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
                subtitleLabel.gameObject.SetActive(hasSubtitle);
                subtitleLabel.text = hasSubtitle ? subtitle : string.Empty;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
            }

            showRoutine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            float total = fadeInDuration + holdDuration + fadeOutDuration;
            float elapsed = 0f;

            while (elapsed < total)
            {
                float progress = total > 0f ? elapsed / total : 1f;
                rectTransform.anchoredPosition = restingPosition + Vector2.up * (riseDistance * progress);

                if (elapsed < fadeInDuration)
                {
                    float t = elapsed / fadeInDuration;
                    canvasGroup.alpha = t;
                    rectTransform.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, 1f - (1f - t) * (1f - t));
                }
                else if (elapsed < fadeInDuration + holdDuration)
                {
                    canvasGroup.alpha = 1f;
                    rectTransform.localScale = Vector3.one;
                }
                else
                {
                    float t = fadeOutDuration > 0f ? (elapsed - fadeInDuration - holdDuration) / fadeOutDuration : 1f;
                    canvasGroup.alpha = 1f - t;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            canvasGroup.alpha = 0f;
            rectTransform.anchoredPosition = restingPosition;
            rectTransform.localScale = Vector3.one;
            showRoutine = null;
        }
    }
}
