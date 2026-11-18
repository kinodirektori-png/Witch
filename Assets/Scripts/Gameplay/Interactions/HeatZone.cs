using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Witch.Gameplay
{
    /// <summary>
    /// A stove or oven. While switched on it heats every <see cref="MixingVessel"/> on it towards a target temperature.
    /// Vessels can sit on it permanently (the pot on the stove) or be dragged in (a bowl into the oven); a vessel dragged
    /// back out stops heating and cools down by itself.
    /// </summary>
    /// <remarks>
    /// Put it next to an <see cref="ItemDropZone"/> (Accepted Item Behaviour: Parent To Zone) to accept draggable
    /// vessels. Hook a knob <see cref="Button"/> to <see cref="Toggle"/>. Doesn't know about recipes: reactions that
    /// need heat are triggered by <see cref="CauldronManager"/> when the vessel's temperature changes.
    /// </remarks>
    public class HeatZone : MonoBehaviour, IItemDropReceiver
    {
        [Tooltip("Vessels that always sit on this zone, e.g. the pot on the stove.")]
        [SerializeField] private List<MixingVessel> fixedVessels = new List<MixingVessel>();

        [Tooltip("Accept draggable vessels (e.g. a mixing bowl) dropped onto this zone.")]
        [SerializeField] private bool acceptDraggableVessels = true;

        [SerializeField] private bool startsOn;

        [Tooltip("Temperature the zone heats vessels to, in °C.")]
        [SerializeField] private float targetTemperatureC = 100f;

        [Tooltip("°C per second while heating.")]
        [SerializeField, Min(0f)] private float heatingPerSecond = 15f;

        [Header("Visuals (optional)")]
        [Tooltip("Tinted with On Color / Off Color, e.g. the burner ring or the oven window.")]
        [SerializeField] private Graphic glow;

        [SerializeField] private Color onColor = new Color(1f, 0.45f, 0.15f);
        [SerializeField] private Color offColor = new Color(0.35f, 0.35f, 0.38f);

        [Tooltip("Shows \"Heat: ON\" / \"Heat: OFF\" so the state isn't shown by colour alone.")]
        [SerializeField] private TMPro.TMP_Text stateLabel;

        [SerializeField] private string onText = "Heat: ON";
        [SerializeField] private string offText = "Heat: OFF";

        [Tooltip("Knob image whose sprite turns with the heat. Optional.")]
        [SerializeField] private Image knobImage;

        [SerializeField] private Sprite knobOnSprite;
        [SerializeField] private Sprite knobOffSprite;

        [Header("Events")]
        [Tooltip("Raised with true when switched on and false when switched off. Hook stove sounds here (#23).")]
        public UnityEvent<bool> onHeatChanged = new UnityEvent<bool>();

        /// <summary>Raised when switched on or off.</summary>
        public event Action<bool> HeatChanged;

        private readonly List<MixingVessel> droppedVessels = new List<MixingVessel>();

        public bool IsOn { get; private set; }

        public float TargetTemperatureC => targetTemperatureC;

        private void Awake()
        {
            IsOn = startsOn;
            RefreshVisuals();
        }

        /// <summary>Switches the heat on or off. Hook the stove knob's OnClick here.</summary>
        public void Toggle() => SetOn(!IsOn);

        public void SetOn(bool on)
        {
            if (IsOn == on)
            {
                return;
            }

            IsOn = on;
            RefreshVisuals();
            HeatChanged?.Invoke(IsOn);
            onHeatChanged.Invoke(IsOn);
        }

        /// <summary>Adds a vessel that stays on this zone (e.g. set up from code).</summary>
        public void AddFixedVessel(MixingVessel vessel)
        {
            if (vessel != null && !fixedVessels.Contains(vessel))
            {
                fixedVessels.Add(vessel);
            }
        }

        bool IItemDropReceiver.TryReceiveItem(GameObject droppedItem)
        {
            if (!acceptDraggableVessels || droppedItem == null)
            {
                return false;
            }

            var vessel = droppedItem.GetComponent<MixingVessel>();
            if (vessel == null)
            {
                return false;
            }

            if (!droppedVessels.Contains(vessel))
            {
                droppedVessels.Add(vessel);
            }

            return true;
        }

        private void Update()
        {
            // A dropped vessel that was dragged elsewhere (no longer under this zone) stops heating.
            for (int i = droppedVessels.Count - 1; i >= 0; i--)
            {
                MixingVessel vessel = droppedVessels[i];
                if (vessel == null || !vessel.isActiveAndEnabled || !vessel.transform.IsChildOf(transform))
                {
                    droppedVessels.RemoveAt(i);
                }
            }

            if (!IsOn)
            {
                return;
            }

            float degrees = heatingPerSecond * Time.deltaTime;
            HeatAll(fixedVessels, degrees);
            HeatAll(droppedVessels, degrees);
        }

        private void HeatAll(List<MixingVessel> vessels, float degrees)
        {
            foreach (MixingVessel vessel in vessels)
            {
                if (vessel != null && vessel.isActiveAndEnabled)
                {
                    vessel.ApplyHeat(degrees, targetTemperatureC);
                }
            }
        }

        private void RefreshVisuals()
        {
            if (glow != null)
            {
                glow.color = IsOn ? onColor : offColor;
            }

            if (stateLabel != null)
            {
                stateLabel.text = IsOn ? onText : offText;
            }

            Sprite knobSprite = IsOn ? knobOnSprite : knobOffSprite;
            if (knobImage != null && knobSprite != null)
            {
                knobImage.sprite = knobSprite;
            }
        }
    }
}
