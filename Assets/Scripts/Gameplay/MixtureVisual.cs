using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.Gameplay
{
    /// <summary>
    /// Shows what is going on inside a <see cref="MixingVessel"/> so kids can observe it: the liquid takes the colour
    /// of what was poured in and fades towards clear water as it is diluted, a label says how strong it is in kid
    /// words, the temperature is shown in °C, and steam appears when it gets hot.
    /// </summary>
    public class MixtureVisual : MonoBehaviour
    {
        [Tooltip("Vessel to show. Defaults to one on this GameObject or its parents.")]
        [SerializeField] private MixingVessel vessel;

        [Header("Liquid")]
        [Tooltip("Graphic tinted with the mixture colour (e.g. the liquid surface in the pot).")]
        [SerializeField] private Graphic liquid;

        [SerializeField] private Color emptyColor = new Color(0.25f, 0.22f, 0.2f);
        [SerializeField] private Color waterColor = new Color(0.65f, 0.85f, 1f);

        [Tooltip("How fast the colour changes, so kids can see it shift.")]
        [SerializeField, Min(0.1f)] private float colorChangeSpeed = 2.5f;

        [Header("Labels (optional)")]
        [SerializeField] private TMP_Text strengthLabel;
        [SerializeField] private TMP_Text temperatureLabel;

        [SerializeField] private string emptyText = "Empty";
        [SerializeField] private string waterOnlyText = "Just water";
        [SerializeField] private string strongText = "Strong";
        [SerializeField] private string mediumText = "Medium";
        [SerializeField] private string wateryText = "Watery";

        [Tooltip("At or above this concentration the mixture reads Strong.")]
        [SerializeField, Range(0f, 1f)] private float strongAbove = 0.66f;

        [Tooltip("At or above this concentration the mixture reads Medium; below it Watery.")]
        [SerializeField, Range(0f, 1f)] private float mediumAbove = 0.34f;

        [Header("Steam")]
        [Tooltip("Shown while the vessel is at or above Steam Temperature.")]
        [SerializeField] private GameObject steam;

        [SerializeField] private float steamTemperatureC = 70f;

        private Color targetColor;

        private void Awake()
        {
            if (vessel == null)
            {
                vessel = GetComponentInParent<MixingVessel>();
            }

            targetColor = emptyColor;
            if (liquid != null)
            {
                liquid.color = emptyColor;
            }
        }

        private void OnEnable()
        {
            if (vessel == null)
            {
                Debug.LogWarning($"{nameof(MixtureVisual)} on '{name}' has no {nameof(MixingVessel)} to show.", this);
                return;
            }

            vessel.MixtureChanged += HandleMixtureChanged;
            vessel.TemperatureChanged += HandleTemperatureChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (vessel != null)
            {
                vessel.MixtureChanged -= HandleMixtureChanged;
                vessel.TemperatureChanged -= HandleTemperatureChanged;
            }
        }

        private void Update()
        {
            if (liquid != null && liquid.color != targetColor)
            {
                float blend = 1f - Mathf.Exp(-colorChangeSpeed * Time.deltaTime);
                liquid.color = Color.Lerp(liquid.color, targetColor, blend);
            }
        }

        /// <summary>Kid word for a concentration, e.g. "Strong". Public so the HUD or book can reuse it.</summary>
        public string DescribeStrength(Mixture mixture)
        {
            if (mixture == null || mixture.IsEmpty)
            {
                return emptyText;
            }

            if (mixture.SubstanceParts <= 0f)
            {
                return waterOnlyText;
            }

            float concentration = mixture.Concentration;
            if (concentration >= strongAbove)
            {
                return strongText;
            }

            return concentration >= mediumAbove ? mediumText : wateryText;
        }

        private void HandleMixtureChanged(MixingVessel changed) => Refresh();

        private void HandleTemperatureChanged(float temperatureC) => RefreshTemperature();

        private void Refresh()
        {
            if (vessel == null)
            {
                return;
            }

            Mixture mixture = vessel.Mixture;
            if (mixture.IsEmpty)
            {
                targetColor = emptyColor;
            }
            else if (mixture.SubstanceParts <= 0f)
            {
                targetColor = waterColor;
            }
            else
            {
                Color tint = mixture.GetAverageTint();
                tint.a = 1f;
                // Strong mixtures show their own colour; adding water fades them towards clear water.
                targetColor = Color.Lerp(waterColor, tint, mixture.Concentration);
            }

            if (strengthLabel != null)
            {
                strengthLabel.text = DescribeStrength(mixture);
            }

            RefreshTemperature();
        }

        private void RefreshTemperature()
        {
            if (vessel == null)
            {
                return;
            }

            float temperature = vessel.TemperatureC;
            if (temperatureLabel != null)
            {
                temperatureLabel.text = $"{Mathf.RoundToInt(temperature)} °C";
            }

            if (steam != null)
            {
                bool showSteam = temperature >= steamTemperatureC;
                if (steam.activeSelf != showSteam)
                {
                    steam.SetActive(showSteam);
                }
            }
        }
    }
}
