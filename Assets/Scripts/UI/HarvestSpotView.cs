using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.UI
{
    /// <summary>
    /// The visible, touchable part of a <see cref="HarvestSpot"/>: press and hold to gather (a bar fills up), then
    /// "+1 Lemon" floats up. Shows the source's common and scientific name and where it is found; locked spots are a
    /// silhouette that says to read about it in the book first, and a harvested spot shows it growing back.
    /// </summary>
    public class HarvestSpotView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private HarvestSpot spot;

        [Header("Visuals")]
        [SerializeField] private Image picture;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text scientificLabel;
        [SerializeField] private TMP_Text whereLabel;
        [SerializeField] private TMP_Text statusLabel;

        [Tooltip("Image Type Filled. Shows hold progress, then regrow progress.")]
        [SerializeField] private Image progressFill;

        [Tooltip("Floats up with \"+1 Lemon\" after a harvest.")]
        [SerializeField] private TMP_Text feedbackLabel;

        [SerializeField] private Sprite fallbackPicture;
        [SerializeField] private Color pictureTint = new Color(0.55f, 0.8f, 0.45f);
        [SerializeField] private Color silhouetteColor = new Color(0.15f, 0.12f, 0.2f, 0.85f);

        [Header("Input")]
        [Tooltip("Seconds to hold before gathering.")]
        [SerializeField, Min(0.1f)] private float holdSeconds = 0.8f;

        [SerializeField] private string readyText = "Hold to gather";
        [SerializeField] private string regrowingText = "Growing back...";

        private bool holding;
        private float heldFor;
        private Vector3 restScale;
        private Coroutine feedbackRoutine;
        private Coroutine highlightRoutine;

        public HarvestSpot Spot => spot;

        private void Awake()
        {
            restScale = transform.localScale;
            if (feedbackLabel != null)
            {
                feedbackLabel.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (spot != null)
            {
                spot.StateChanged += HandleStateChanged;
            }

            Refresh();
        }

        private void Start() => Refresh(); // the lore book unlocks its start pages in Awake

        private void OnDisable()
        {
            if (spot != null)
            {
                spot.StateChanged -= HandleStateChanged;
            }

            holding = false;
            heldFor = 0f;
            transform.localScale = restScale;
            feedbackRoutine = null;
            highlightRoutine = null;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (spot != null && spot.CanHarvest)
            {
                holding = true;
                heldFor = 0f;
            }
        }

        public void OnPointerUp(PointerEventData eventData) => CancelHold();

        public void OnPointerExit(PointerEventData eventData) => CancelHold();

        /// <summary>Pulses the spot to draw the eye, e.g. after "Gather" in the book.</summary>
        public void Highlight()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (highlightRoutine != null)
            {
                StopCoroutine(highlightRoutine);
            }

            highlightRoutine = StartCoroutine(HighlightRoutine());
        }

        /// <summary>Re-reads the spot's state. Called automatically when it changes.</summary>
        public void Refresh()
        {
            if (spot == null)
            {
                return;
            }

            LoreEntryData entry = spot.Entry;
            bool unlocked = spot.IsUnlocked;

            if (picture != null)
            {
                Sprite sprite = entry != null && entry.illustration != null ? entry.illustration : fallbackPicture;
                picture.sprite = sprite;
                picture.preserveAspect = true;
                bool hasArt = entry != null && entry.illustration != null;
                picture.color = !unlocked ? silhouetteColor : hasArt ? Color.white : pictureTint;
            }

            SetText(nameLabel, unlocked && entry != null ? entry.commonName : "???");
            SetText(scientificLabel, unlocked && entry != null && !string.IsNullOrEmpty(entry.scientificName)
                ? $"<i>{entry.scientificName}</i>"
                : string.Empty);
            SetText(whereLabel, unlocked && entry != null ? entry.whereFound : string.Empty);

            if (!unlocked)
            {
                SetText(statusLabel, spot.LockedHint);
            }
            else if (spot.IsRegrowing)
            {
                SetText(statusLabel, regrowingText);
            }
            else
            {
                KitchenItemData item = spot.KitchenItem;
                SetText(statusLabel, item != null ? $"{readyText}: {item.displayName}" : readyText);
            }

            SetFill(unlocked && !spot.IsRegrowing ? 0f : unlocked ? spot.RegrowProgress : 0f);
        }

        private void Update()
        {
            if (spot == null)
            {
                return;
            }

            if (spot.IsRegrowing)
            {
                SetFill(spot.RegrowProgress);
                return;
            }

            if (!holding)
            {
                return;
            }

            heldFor += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(heldFor / holdSeconds);
            SetFill(progress);
            transform.localScale = restScale * Mathf.Lerp(1f, 0.94f, progress); // squish while pulling

            if (progress >= 1f)
            {
                CancelHold();
                spot.TryHarvest(); // StateChanged refreshes the labels
            }
        }

        private void CancelHold()
        {
            holding = false;
            heldFor = 0f;
            transform.localScale = restScale;
            if (spot != null && !spot.IsRegrowing)
            {
                SetFill(0f);
            }
        }

        private void HandleStateChanged(HarvestSpot changed) => Refresh();

        /// <summary>Hook to the spot's onHarvested UnityEvent, or call from code, to float "+1 Lemon".</summary>
        public void ShowFeedback(string text)
        {
            if (feedbackLabel == null || !isActiveAndEnabled)
            {
                return;
            }

            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            feedbackRoutine = StartCoroutine(FeedbackRoutine(text));
        }

        private IEnumerator FeedbackRoutine(string text)
        {
            feedbackLabel.text = text;
            feedbackLabel.gameObject.SetActive(true);
            var rect = feedbackLabel.rectTransform;
            Vector2 start = rect.anchoredPosition;
            Color color = feedbackLabel.color;
            const float duration = 1.2f;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                rect.anchoredPosition = start + new Vector2(0f, 80f * k);
                feedbackLabel.color = new Color(color.r, color.g, color.b, 1f - k * k);
                yield return null;
            }

            rect.anchoredPosition = start;
            feedbackLabel.color = color;
            feedbackLabel.gameObject.SetActive(false);
            feedbackRoutine = null;
        }

        private IEnumerator HighlightRoutine()
        {
            const float duration = 1.2f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (!holding)
                {
                    transform.localScale = restScale * (1f + 0.08f * Mathf.Sin(t / duration * Mathf.PI * 3f));
                }

                yield return null;
            }

            if (!holding)
            {
                transform.localScale = restScale;
            }

            highlightRoutine = null;
        }

        private void SetFill(float amount)
        {
            if (progressFill != null)
            {
                progressFill.fillAmount = amount;
            }
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }
    }
}
