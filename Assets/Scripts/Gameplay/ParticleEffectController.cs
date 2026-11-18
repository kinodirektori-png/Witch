using UnityEngine;
using Witch.Data;
using Witch.UI;

namespace Witch.Gameplay
{
    /// <summary>
    /// Plays the cauldron's bubbling/fizzing particles and shows the brewed chemical formula whenever the
    /// <see cref="CauldronManager"/> reports a successful reaction. Kept separate from reaction logic so the
    /// cauldron works with or without visuals.
    /// </summary>
    public class ParticleEffectController : MonoBehaviour
    {
        [Tooltip("Cauldron to listen to. Defaults to one on this GameObject or its parents.")]
        [SerializeField] private CauldronManager cauldron;

        [Header("Particles")]
        [Tooltip("Particle System prefab configured for bubbling or fizzing.")]
        [SerializeField] private ParticleSystem reactionParticlesPrefab;

        [Tooltip("Where the particles spawn (e.g. the cauldron's rim). Defaults to this transform.")]
        [SerializeField] private Transform particleSpawnPoint;

        [Header("Formula Popup")]
        [Tooltip("Popup that shows the formula, e.g. \"NaCl\". Optional.")]
        [SerializeField] private FormulaPopup formulaPopup;

        [Tooltip("Also show the scientific name under the formula.")]
        [SerializeField] private bool showTechnicalName = true;

        [Tooltip("For recipes made by a reaction, show the balanced equation and the kid-friendly explanation " +
                 "instead of only the product's formula.")]
        [SerializeField] private bool showReactionEquation = true;

        [Header("Strength")]
        [Tooltip("Scale the particle burst by how strong the mixture was, so a watery mixture fizzes less.")]
        [SerializeField] private bool scaleWithConcentration = true;

        [Tooltip("Smallest burst, as a fraction of the prefab's burst, for a very watery mixture.")]
        [SerializeField, Range(0.05f, 1f)] private float minimumIntensity = 0.25f;

        private ParticleSystem particlesInstance;
        private float baseBurstCount = -1f;

        private void Awake()
        {
            if (cauldron == null)
            {
                cauldron = GetComponentInParent<CauldronManager>();
            }

            if (particleSpawnPoint == null)
            {
                particleSpawnPoint = transform;
            }
        }

        private void OnEnable()
        {
            if (cauldron != null)
            {
                cauldron.SuccessfulReaction += HandleSuccessfulReaction;
            }
            else
            {
                Debug.LogWarning($"{nameof(ParticleEffectController)} on '{name}' has no {nameof(CauldronManager)} to listen to.", this);
            }
        }

        private void OnDisable()
        {
            if (cauldron != null)
            {
                cauldron.SuccessfulReaction -= HandleSuccessfulReaction;
            }
        }

        /// <summary>Plays the effect for a recipe. Public so it can also be triggered from UnityEvents or tests.</summary>
        public void PlayReactionEffect(RecipeData recipe)
        {
            bool visibleEffect = recipe == null || recipe.conditions == null || recipe.conditions.effect != ObservableEffect.None;
            if (visibleEffect)
            {
                float intensity = scaleWithConcentration && cauldron != null
                    ? Mathf.Lerp(minimumIntensity, 1f, cauldron.LastBrewConcentration)
                    : 1f;
                PlayParticles(intensity);
            }

            if (formulaPopup == null || recipe == null)
            {
                return;
            }

            string subtitle = showTechnicalName ? recipe.technicalName : null;
            string explanation = recipe.KidExplanationWithSafety;
            if (showReactionEquation && recipe.HasReaction)
            {
                formulaPopup.Show(recipe.reaction.RichTextEquation, subtitle, explanation);
            }
            else
            {
                formulaPopup.Show(recipe.RichTextFormula, subtitle, explanation);
            }
        }

        private void HandleSuccessfulReaction(RecipeData recipe)
        {
            PlayReactionEffect(recipe);
        }

        private void PlayParticles(float intensity)
        {
            if (reactionParticlesPrefab == null)
            {
                return;
            }

            // Reuse a single instance so rapid brewing doesn't pile up GameObjects.
            if (particlesInstance == null)
            {
                particlesInstance = Instantiate(reactionParticlesPrefab, particleSpawnPoint.position, particleSpawnPoint.rotation, particleSpawnPoint);
                ParticleSystem.MainModule main = particlesInstance.main;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
            }

            ScaleBurst(intensity);
            particlesInstance.transform.SetPositionAndRotation(particleSpawnPoint.position, particleSpawnPoint.rotation);
            particlesInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particlesInstance.Play(true);
        }

        /// <summary>Sets the first burst to <paramref name="intensity"/> x the prefab's burst count.</summary>
        private void ScaleBurst(float intensity)
        {
            ParticleSystem.EmissionModule emission = particlesInstance.emission;
            if (emission.burstCount == 0)
            {
                return;
            }

            ParticleSystem.Burst burst = emission.GetBurst(0);
            if (baseBurstCount < 0f)
            {
                baseBurstCount = burst.count.constant;
            }

            burst.count = new ParticleSystem.MinMaxCurve(Mathf.Max(1f, Mathf.Round(baseBurstCount * Mathf.Clamp01(intensity))));
            emission.SetBurst(0, burst);
        }
    }
}
