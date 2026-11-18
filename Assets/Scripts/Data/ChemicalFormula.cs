using System.Collections.Generic;

namespace Witch.Data
{
    /// <summary>
    /// Small helpers for plain-text chemical formulas such as "NaHCO3", "Ca(OH)2" or "CuSO4·5H2O".
    /// Pure C# so it can be unit-tested without a scene.
    /// </summary>
    public static class ChemicalFormula
    {
        /// <summary>
        /// Counts atoms per element, e.g. "Na3C6H5O7" gives Na 3, C 6, H 5, O 7.
        /// Supports nested () and [] groups and hydrate dots ("·" or ".") with a leading multiplier.
        /// Returns false with a short <paramref name="error"/> if the formula can't be read.
        /// </summary>
        public static bool TryCountAtoms(string formula, out Dictionary<string, int> atoms, out string error)
        {
            atoms = new Dictionary<string, int>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(formula))
            {
                error = "empty formula";
                return false;
            }

            string[] parts = formula.Replace('·', '.').Replace('•', '.').Split('.');
            foreach (string rawPart in parts)
            {
                string part = rawPart.Trim();
                if (part.Length == 0)
                {
                    error = "empty part next to a dot";
                    return false;
                }

                int index = 0;
                int multiplier = ReadNumber(part, ref index, 1);
                var stack = new Stack<Dictionary<string, int>>();
                stack.Push(new Dictionary<string, int>());

                while (index < part.Length)
                {
                    char c = part[index];
                    if (c == '(' || c == '[')
                    {
                        stack.Push(new Dictionary<string, int>());
                        index++;
                    }
                    else if (c == ')' || c == ']')
                    {
                        if (stack.Count < 2)
                        {
                            error = $"unmatched '{c}'";
                            return false;
                        }

                        index++;
                        int groupCount = ReadNumber(part, ref index, 1);
                        Dictionary<string, int> group = stack.Pop();
                        foreach (KeyValuePair<string, int> pair in group)
                        {
                            Add(stack.Peek(), pair.Key, pair.Value * groupCount);
                        }
                    }
                    else if (char.IsUpper(c))
                    {
                        int start = index++;
                        while (index < part.Length && char.IsLower(part[index]))
                        {
                            index++;
                        }

                        string element = part.Substring(start, index - start);
                        Add(stack.Peek(), element, ReadNumber(part, ref index, 1));
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        index++;
                    }
                    else
                    {
                        error = $"unexpected '{c}'";
                        return false;
                    }
                }

                if (stack.Count != 1)
                {
                    error = "missing closing bracket";
                    return false;
                }

                foreach (KeyValuePair<string, int> pair in stack.Pop())
                {
                    Add(atoms, pair.Key, pair.Value * multiplier);
                }
            }

            return true;
        }

        private static int ReadNumber(string text, ref int index, int fallback)
        {
            int start = index;
            int value = 0;
            while (index < text.Length && char.IsDigit(text[index]))
            {
                value = value * 10 + (text[index] - '0');
                index++;
            }

            return index > start ? value : fallback;
        }

        private static void Add(Dictionary<string, int> atoms, string element, int count)
        {
            atoms.TryGetValue(element, out int existing);
            atoms[element] = existing + count;
        }
    }
}
