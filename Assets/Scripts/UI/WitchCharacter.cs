using UnityEngine;
using UnityEngine.UI;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.UI
{
    /// <summary>
    /// Medea in the kitchen: cheers (happy pose with a little bounce) when a reaction works, and puts on her thinking
    /// pose after a wrong try or when a hint is shown. Goes back to her idle pose after a few seconds.
    /// </summary>
    public class WitchCharacter : MonoBehaviour
    {
        [SerializeField] private Image image;

        [Tooltip("Pose while nothing is happening. Defaults to the happy pose.")]
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite happySprite;
        [SerializeField] private Sprite thinkingSprite;

        [SerializeField] private CauldronManager cauldron;
        [SerializeField] private HintTracker hintTracker;

        [Tooltip("How long a pose lasts before going back to idle.")]
        [SerializeField, Min(0.1f)] private float poseSeconds = 3f;

        [Tooltip("How much she grows at the top of the cheer bounce.")]
        [SerializeField, Min(1f)] private float bounceScale = 1.12f;

        [SerializeField, Min(0.05f)] private float bounceSeconds = 0.5f;

        private float poseTimeLeft;
        private float bounceTimeLeft;

        private void Awake()
        {
            if (image == null)
            {
                image = GetComponent<Image>();
            }

            ShowPose(IdleSprite);
        }

        private Sprite IdleSprite => idleSprite != null ? idleSprite : happySprite;

        private void OnEnable()
        {
            if (cauldron != null)
            {
                cauldron.SuccessfulReaction += HandleSuccess;
                cauldron.onFailedReaction.AddListener(HandleFailedReaction);
            }

            if (hintTracker != null)
            {
                hintTracker.HintShown += HandleHintShown;
            }
        }

        private void OnDisable()
        {
            if (cauldron != null)
            {
                cauldron.SuccessfulReaction -= HandleSuccess;
                cauldron.onFailedReaction.RemoveListener(HandleFailedReaction);
            }

            if (hintTracker != null)
            {
                hintTracker.HintShown -= HandleHintShown;
            }

            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (poseTimeLeft > 0f)
            {
                poseTimeLeft -= Time.deltaTime;
                if (poseTimeLeft <= 0f)
                {
                    ShowPose(IdleSprite);
                }
            }

            if (bounceTimeLeft > 0f)
            {
                bounceTimeLeft = Mathf.Max(0f, bounceTimeLeft - Time.deltaTime);
                float t = 1f - bounceTimeLeft / bounceSeconds;
                float scale = 1f + (bounceScale - 1f) * Mathf.Sin(t * Mathf.PI);
                transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>Plays the happy pose with a bounce. Public so other moments (e.g. a lore unlock) can use it.</summary>
        public void Cheer()
        {
            ShowPose(happySprite, poseSeconds);
            bounceTimeLeft = bounceSeconds;
        }

        /// <summary>Plays the thinking pose for a few seconds.</summary>
        public void Think() => ShowPose(thinkingSprite, poseSeconds);

        private void HandleSuccess(RecipeData recipe) => Cheer();

        private void HandleFailedReaction(GameObject droppedItem) => Think();

        private void HandleHintShown(IngredientItem item) => Think();

        private void ShowPose(Sprite sprite, float seconds = 0f)
        {
            poseTimeLeft = seconds;
            if (image != null && sprite != null)
            {
                image.sprite = sprite;
            }
        }
    }
}
