using System;

namespace Witch.UI
{
    /// <summary>
    /// A question an adult can answer but a young child can't easily guess: multiply two numbers written as words
    /// ("What is seven times eight?"). Used by <see cref="ParentalGate"/> before anything leaves the game.
    /// Plain C# so it can be unit-tested.
    /// </summary>
    public sealed class ParentalGateChallenge
    {
        private static readonly string[] NumberWords =
        {
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"
        };

        /// <summary>Smallest factor. 6 to 9 keeps the products beyond what young players know by heart.</summary>
        public const int MinFactor = 6;
        public const int MaxFactor = 9;

        public int FactorA { get; }
        public int FactorB { get; }
        public int Answer => FactorA * FactorB;

        /// <summary>The question to show, e.g. "What is seven times eight?"</summary>
        public string Question => $"What is {NumberWords[FactorA]} times {NumberWords[FactorB]}?";

        public ParentalGateChallenge(int factorA, int factorB)
        {
            if (factorA < MinFactor || factorA > MaxFactor || factorB < MinFactor || factorB > MaxFactor)
            {
                throw new ArgumentOutOfRangeException(nameof(factorA), $"Factors must be {MinFactor}-{MaxFactor}.");
            }

            FactorA = factorA;
            FactorB = factorB;
        }

        /// <summary>A new random challenge.</summary>
        public static ParentalGateChallenge CreateRandom(Random random)
        {
            random ??= new Random();
            return new ParentalGateChallenge(random.Next(MinFactor, MaxFactor + 1), random.Next(MinFactor, MaxFactor + 1));
        }

        /// <summary>True if <paramref name="input"/> is the right number (spaces are ignored).</summary>
        public bool IsCorrect(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            return int.TryParse(input.Trim(), out int value) && value == Answer;
        }
    }
}
