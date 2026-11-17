using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// An order-independent bag of ingredients, used as the key of the cauldron's recipe dictionary.
    /// "Lemon + Baking Soda" and "Baking Soda + Lemon" are the same combination; repeats count, so
    /// "3 x Baking Soda + Lemon" is different from "Baking Soda + Lemon". Null entries are ignored.
    /// </summary>
    public readonly struct IngredientCombination : IEquatable<IngredientCombination>
    {
        // Session-local key per ingredient asset, sorted so that order never matters.
        // Keys come from KeyRegistry (reference identity) because Object.GetInstanceID() is a compile error in Unity 6.
        private readonly int[] sortedIds;
        private readonly string[] names;
        private readonly int hash;

        public IngredientCombination(IEnumerable<RecipeData> ingredients)
        {
            var entries = new List<KeyValuePair<int, string>>();
            if (ingredients != null)
            {
                foreach (RecipeData ingredient in ingredients)
                {
                    if (ingredient != null)
                    {
                        entries.Add(new KeyValuePair<int, string>(KeyRegistry.GetKey(ingredient), ingredient.name));
                    }
                }
            }

            entries.Sort((a, b) => a.Key.CompareTo(b.Key));
            sortedIds = new int[entries.Count];
            names = new string[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                sortedIds[i] = entries[i].Key;
                names[i] = entries[i].Value;
            }

            // Hash is computed once because the struct is immutable and used as a dictionary key.
            unchecked
            {
                int h = 17;
                foreach (int id in sortedIds)
                {
                    h = h * 31 + id;
                }

                hash = h;
            }
        }

        /// <summary>Number of ingredients in the combination, counting repeats.</summary>
        public int Count => sortedIds?.Length ?? 0;

        public bool IsEmpty => Count == 0;

        public bool Equals(IngredientCombination other)
        {
            if (Count != other.Count || hash != other.hash)
            {
                return false;
            }

            for (int i = 0; i < Count; i++)
            {
                if (sortedIds[i] != other.sortedIds[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => obj is IngredientCombination other && Equals(other);

        public override int GetHashCode() => hash;

        public static bool operator ==(IngredientCombination left, IngredientCombination right) => left.Equals(right);

        public static bool operator !=(IngredientCombination left, IngredientCombination right) => !left.Equals(right);

        public override string ToString()
        {
            if (IsEmpty)
            {
                return "(empty)";
            }

            var builder = new StringBuilder();
            foreach (string ingredientName in names)
            {
                if (builder.Length > 0)
                {
                    builder.Append(" + ");
                }

                builder.Append(ingredientName);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Hands out a stable int per ingredient asset (compared by reference) for the current session.
        /// The weak table does not keep assets alive, so unloaded assets are not leaked.
        /// </summary>
        private static class KeyRegistry
        {
            private sealed class Key
            {
                public readonly int Value;

                public Key(int value)
                {
                    Value = value;
                }
            }

            private static readonly ConditionalWeakTable<RecipeData, Key> Keys =
                new ConditionalWeakTable<RecipeData, Key>();

            private static int nextKey;

            public static int GetKey(RecipeData ingredient)
            {
                return Keys.GetValue(ingredient, _ => new Key(++nextKey)).Value;
            }
        }
    }
}
