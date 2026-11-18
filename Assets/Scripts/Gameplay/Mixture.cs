using System.Collections.Generic;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>One pour into a vessel: which substance went in and in what form.</summary>
    public readonly struct MixtureEntry
    {
        public readonly RecipeData Substance;
        public readonly PhysicalState State;
        public readonly float Amount;

        public MixtureEntry(RecipeData substance, PhysicalState state, float amount)
        {
            Substance = substance;
            State = state;
            Amount = amount;
        }
    }

    /// <summary>
    /// What is inside a pot or bowl: the substances poured in (with their form), how much water, and how hot it is.
    /// Plain C# (not a component) so reaction conditions can be unit-tested without a scene.
    /// </summary>
    public class Mixture
    {
        /// <summary>Kitchen room temperature in °C. Vessels cool back to this.</summary>
        public const float RoomTemperatureC = 20f;

        private readonly List<MixtureEntry> entries = new List<MixtureEntry>();

        /// <summary>Everything poured in, in order.</summary>
        public IReadOnlyList<MixtureEntry> Entries => entries;

        /// <summary>Parts of plain water added.</summary>
        public float WaterParts { get; private set; }

        public float TemperatureC { get; private set; } = RoomTemperatureC;

        /// <summary>Total parts of substances (not counting plain water).</summary>
        public float SubstanceParts
        {
            get
            {
                float total = 0f;
                foreach (MixtureEntry entry in entries)
                {
                    total += entry.Amount;
                }

                return total;
            }
        }

        public bool IsEmpty => entries.Count == 0 && WaterParts <= 0f;

        /// <summary>
        /// How strong the mixture is, from 0 (only water, or empty) to 1 (no water added).
        /// Substance parts / (substance parts + water parts).
        /// </summary>
        public float Concentration
        {
            get
            {
                float substance = SubstanceParts;
                float total = substance + WaterParts;
                return total > 0f ? substance / total : 0f;
            }
        }

        public void AddSubstance(RecipeData substance, PhysicalState state, float amount = 1f)
        {
            if (substance != null && amount > 0f)
            {
                entries.Add(new MixtureEntry(substance, state, amount));
            }
        }

        public void AddWater(float parts)
        {
            if (parts > 0f)
            {
                WaterParts += parts;
            }
        }

        public void SetTemperature(float temperatureC)
        {
            TemperatureC = temperatureC;
        }

        /// <summary>True if <paramref name="substance"/> was poured in at least once in <paramref name="state"/>.</summary>
        public bool Contains(RecipeData substance, PhysicalState state)
        {
            foreach (MixtureEntry entry in entries)
            {
                if (entry.Substance == substance && entry.State == state)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Every form <paramref name="substance"/> was poured in as. Empty if it isn't in the mixture.</summary>
        public List<PhysicalState> GetStatesOf(RecipeData substance)
        {
            var states = new List<PhysicalState>();
            foreach (MixtureEntry entry in entries)
            {
                if (entry.Substance == substance && !states.Contains(entry.State))
                {
                    states.Add(entry.State);
                }
            }

            return states;
        }

        /// <summary>Throws everything away. The temperature stays: an emptied hot pot is still hot.</summary>
        public void Clear()
        {
            entries.Clear();
            WaterParts = 0f;
        }
    }
}
