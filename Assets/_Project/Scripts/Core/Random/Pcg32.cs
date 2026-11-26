using System;
using System.Collections.Generic;

namespace Silence.Core.Random
{
    /// <summary>
    /// Deterministic PCG32 random generator (O'Neill, pcg-random.org). Same seed, same sequence,
    /// on every platform. The whole state is two ulongs, so it can be saved and restored mid-run.
    /// Never use UnityEngine.Random or System.Random in the rules.
    /// </summary>
    [Serializable]
    public sealed class Pcg32
    {
        const ulong Multiplier = 6364136223846793005UL;

        public ulong State { get; private set; }
        public ulong Increment { get; private set; }

        public Pcg32(ulong seed, ulong stream = 54UL)
        {
            State = 0UL;
            Increment = (stream << 1) | 1UL;
            NextUInt();
            State += seed;
            NextUInt();
        }

        Pcg32(ulong state, ulong increment, bool _)
        {
            State = state;
            Increment = increment;
        }

        /// <summary>Restore a generator saved with <see cref="State"/> and <see cref="Increment"/>.</summary>
        public static Pcg32 FromState(ulong state, ulong increment) => new Pcg32(state, increment, true);

        public Pcg32 Clone() => FromState(State, Increment);

        /// <summary>Split off an independent generator (for a sub-system such as the map or loot).</summary>
        public Pcg32 Fork(ulong stream) => new Pcg32(((ulong)NextUInt() << 32) | NextUInt(), stream);

        public uint NextUInt()
        {
            ulong old = State;
            State = unchecked(old * Multiplier + Increment);
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        /// <summary>Uniform integer in [0, bound) without modulo bias.</summary>
        public int NextInt(int bound)
        {
            if (bound <= 0) throw new ArgumentOutOfRangeException(nameof(bound), "bound must be positive");
            uint b = (uint)bound;
            uint threshold = unchecked((uint)(-(int)b)) % b;
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return (int)(r % b);
            }
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentException("max must be greater than min");
            return minInclusive + NextInt(maxExclusive - minInclusive);
        }

        /// <summary>True with the given chance in percent (0 to 100).</summary>
        public bool Chance(int percent) => NextInt(100) < percent;

        /// <summary>Fisher-Yates shuffle in place.</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public T Pick<T>(IReadOnlyList<T> list)
        {
            if (list.Count == 0) throw new ArgumentException("list is empty");
            return list[NextInt(list.Count)];
        }
    }
}
