using UnityEngine;
using UnityEngine.UI;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// Swaps the pot's picture with what is inside it: an empty pot, a pot with liquid, and a fizzing pot for a few
    /// seconds after a successful reaction. Works next to <see cref="MixtureVisual"/>, which keeps tinting the liquid.
    /// </summary>
    /// <remarks>
    /// While fizzing, the pot image is made fully transparent instead of disabled so it keeps receiving drops.
    /// </remarks>
    public class PotSprites : MonoBehaviour
    {
        [SerializeField] private MixingVessel vessel;
        [SerializeField] private CauldronManager cauldron;

        [Tooltip("The pot's own image. Defaults to an Image on this GameObject.")]
        [SerializeField] private Image potImage;

        [SerializeField] private Sprite emptySprite;

        [Tooltip("Shown while the pot holds anything. Leave empty to keep the empty pot.")]
        [SerializeField] private Sprite filledSprite;

        [Tooltip("Tinted liquid surface (driven by MixtureVisual). Hidden while fizzing.")]
        [SerializeField] private Graphic liquid;

        [Tooltip("Fizzing pot shown over the pot after a successful reaction. Optional.")]
        [SerializeField] private GameObject fizzOverlay;

        [SerializeField, Min(0f)] private float fizzSeconds = 2.5f;

        private float fizzTimeLeft;

        private void Awake()
        {
            if (potImage == null)
            {
                potImage = GetComponent<Image>();
            }

            if (vessel == null)
            {
                vessel = GetComponent<MixingVessel>();
            }
        }

        private void OnEnable()
        {
            if (vessel != null)
            {
                vessel.MixtureChanged += HandleMixtureChanged;
            }

            if (cauldron != null)
            {
                cauldron.SuccessfulReaction += HandleSuccessfulReaction;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (vessel != null)
            {
                vessel.MixtureChanged -= HandleMixtureChanged;
            }

            if (cauldron != null)
            {
                cauldron.SuccessfulReaction -= HandleSuccessfulReaction;
            }
        }

        private void Update()
        {
            if (fizzTimeLeft <= 0f)
            {
                return;
            }

            fizzTimeLeft -= Time.deltaTime;
            if (fizzTimeLeft <= 0f)
            {
                Refresh();
            }
        }

        private void HandleMixtureChanged(MixingVessel changed) => Refresh();

        private void HandleSuccessfulReaction(RecipeData recipe)
        {
            if (fizzOverlay == null || fizzSeconds <= 0f)
            {
                return;
            }

            fizzTimeLeft = fizzSeconds;
            Refresh();
        }

        private void Refresh()
        {
            bool fizzing = fizzTimeLeft > 0f && fizzOverlay != null;
            if (fizzOverlay != null)
            {
                fizzOverlay.SetActive(fizzing);
            }

            if (liquid != null)
            {
                liquid.enabled = !fizzing;
            }

            if (potImage == null)
            {
                return;
            }

            potImage.color = fizzing ? Color.clear : Color.white;
            bool hasContents = vessel != null && !vessel.Mixture.IsEmpty;
            Sprite sprite = hasContents && filledSprite != null ? filledSprite : emptySprite;
            if (sprite != null)
            {
                potImage.sprite = sprite;
            }
        }
    }
}
