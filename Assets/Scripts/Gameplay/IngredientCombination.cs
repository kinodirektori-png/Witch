using System;
using System.Collections.Generic;
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
        // Instance IDs of the ingredients, sorted so that order never matters.
        private readonly int[] sortedIds;
        private readonly int hash;

        public IngredientCombination(IEnumerable<RecipeData> ingredients)
        {
            var ids = new List<int>();
            if (ingredients != null)
            {
                foreach (RecipeData ingredient in ingredients)
                {
                    if (ingredient != null)
                    {
                        ids.Add(ingredient.GetInstanceID());
                    }
                }
            }

            ids.Sort();
            sortedIds = ids.ToArray();

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
            foreach (int id in sortedIds)
            {
                if (builder.Length > 0)
                {
                    builder.Append(" + ");
                }

                builder.Append('#').Append(id);
            }

            return builder.ToString();
        }
    }
}
