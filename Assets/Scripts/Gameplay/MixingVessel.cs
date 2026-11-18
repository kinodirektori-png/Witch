using System;
using UnityEngine;
using UnityEngine.Events;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// A pot, bowl or cauldron that things can be poured into. Holds a <see cref="Mixture"/>, takes water (dilution)
    /// and heat (from a <see cref="HeatZone"/>), and cools back to room temperature on its own.
    /// </summary>
    /// <remarks>
    /// Only stores and reports physical contents. What a mixture turns into is decided by <see cref="CauldronManager"/>,
    /// and what it looks like by <see cref="MixtureVisual"/>.
    /// </remarks>
    [DisallowMultipleComponent]
    public class MixingVessel : MonoBehaviour
    {
        [Tooltip("°C per second the vessel cools towards room temperature when nothing heats it.")]
        [SerializeField, Min(0f)] private float coolingPerSecond = 6f;

        [Tooltip("Highest temperature the vessel reaches (water boils at 100 °C).")]
        [SerializeField] private float maxTemperatureC = 120f;

        [Tooltip("Only raise TemperatureChanged when the temperature moved at least this much, to avoid per-frame spam.")]
        [SerializeField, Min(0.01f)] private float temperatureEventStep = 1f;

        [Header("Events")]
        public UnityEvent<float> onDiluted = new UnityEvent<float>();
        public UnityEvent<float> onTemperatureChanged = new UnityEvent<float>();

        /// <summary>Raised when something is poured in, water is added or the vessel is emptied.</summary>
        public event Action<MixingVessel> MixtureChanged;

        /// <summary>Raised when the temperature changes by at least the event step. Passes the new °C.</summary>
        public event Action<float> TemperatureChanged;

        private readonly Mixture mixture = new Mixture();
        private int lastHeatedFrame = -1;
        private float lastReportedTemperature = Mixture.RoomTemperatureC;

        public Mixture Mixture => mixture;

        public float TemperatureC => mixture.TemperatureC;

        public float Concentration => mixture.Concentration;

        /// <summary>True while a heat zone is heating this vessel (this frame or the last).</summary>
        public bool IsBeingHeated => Time.frameCount - lastHeatedFrame <= 1;

        /// <summary>Pours a substance in.</summary>
        public void AddSubstance(RecipeData substance, PhysicalState state, float amount = 1f)
        {
            if (substance == null)
            {
                return;
            }

            mixture.AddSubstance(substance, state, amount);
            MixtureChanged?.Invoke(this);
        }

        /// <summary>Adds plain water, making the mixture weaker.</summary>
        public void AddWater(float parts)
        {
            if (parts <= 0f)
            {
                return;
            }

            mixture.AddWater(parts);
            onDiluted.Invoke(mixture.Concentration);
            MixtureChanged?.Invoke(this);
        }

        /// <summary>
        /// Heats the vessel towards <paramref name="targetC"/> by up to <paramref name="degrees"/>. Call every frame
        /// while heating (a <see cref="HeatZone"/> does); the vessel cools again once calls stop.
        /// </summary>
        public void ApplyHeat(float degrees, float targetC)
        {
            lastHeatedFrame = Time.frameCount;
            float target = Mathf.Min(targetC, maxTemperatureC);
            if (mixture.TemperatureC < target)
            {
                float next = Mathf.Min(target, mixture.TemperatureC + Mathf.Max(0f, degrees));
                ChangeTemperature(next, forceEvent: Mathf.Approximately(next, target));
            }
        }

        /// <summary>Sets the temperature directly (e.g. tests or a save game).</summary>
        public void SetTemperature(float temperatureC)
        {
            ChangeTemperature(temperatureC, forceEvent: true);
        }

        private void ChangeTemperature(float temperatureC, bool forceEvent)
        {
            mixture.SetTemperature(Mathf.Clamp(temperatureC, -20f, maxTemperatureC));

            bool moved = !Mathf.Approximately(mixture.TemperatureC, lastReportedTemperature);
            if (moved && (forceEvent || Mathf.Abs(mixture.TemperatureC - lastReportedTemperature) >= temperatureEventStep))
            {
                lastReportedTemperature = mixture.TemperatureC;
                TemperatureChanged?.Invoke(mixture.TemperatureC);
                onTemperatureChanged.Invoke(mixture.TemperatureC);
            }
        }

        /// <summary>Throws the contents away (after a brew, or a "start again" button).</summary>
        public void Empty()
        {
            if (mixture.IsEmpty)
            {
                return;
            }

            mixture.Clear();
            MixtureChanged?.Invoke(this);
        }

        private void Update()
        {
            if (IsBeingHeated || coolingPerSecond <= 0f)
            {
                return;
            }

            float current = mixture.TemperatureC;
            if (!Mathf.Approximately(current, Mixture.RoomTemperatureC))
            {
                float next = Mathf.MoveTowards(current, Mixture.RoomTemperatureC, coolingPerSecond * Time.deltaTime);
                ChangeTemperature(next, forceEvent: Mathf.Approximately(next, Mixture.RoomTemperatureC));
            }
        }
    }
}
