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

        private ParticleSystem particlesInstance;

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
            PlayParticles();

            if (formulaPopup == null || recipe == null)
            {
                return;
            }

            string subtitle = showTechnicalName ? recipe.technicalName : null;
            if (showReactionEquation && recipe.HasReaction)
            {
                ReactionData reaction = recipe.reaction;
                formulaPopup.Show(reaction.RichTextEquation, subtitle, reaction.kidExplanation);
            }
            else
            {
                formulaPopup.Show(recipe.RichTextFormula, subtitle);
            }
        }

        private void HandleSuccessfulReaction(RecipeData recipe)
        {
            PlayReactionEffect(recipe);
        }

        private void PlayParticles()
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

            particlesInstance.transform.SetPositionAndRotation(particleSpawnPoint.position, particleSpawnPoint.rotation);
            particlesInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particlesInstance.Play(true);
        }
    }
}
