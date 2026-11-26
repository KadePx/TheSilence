using System.Collections.Generic;
using Silence.Core.Hex;

namespace Silence.Core.Battle
{
    /// <summary>Asteroids block movement and fire; debris blocks movement only.</summary>
    public enum Terrain { Open, Asteroid, Debris }

    /// <summary>The hex battlefield, stored as offset odd-r columns and rows (9 x 7 by default).</summary>
    public sealed class Board
    {
        public readonly int Width;
        public readonly int Height;
        readonly Terrain[] cells;

        public Board(int width = 9, int height = 7)
        {
            Width = width;
            Height = height;
            cells = new Terrain[width * height];
        }

        public bool InBounds(HexCoord h)
        {
            var (col, row) = h.ToOffset();
            return col >= 0 && col < Width && row >= 0 && row < Height;
        }

        public Terrain Get(HexCoord h)
        {
            var (col, row) = h.ToOffset();
            return InBounds(h) ? cells[row * Width + col] : Terrain.Open;
        }

        public void Set(HexCoord h, Terrain t)
        {
            var (col, row) = h.ToOffset();
            cells[row * Width + col] = t;
        }

        public void Set(int col, int row, Terrain t) => cells[row * Width + col] = t;

        /// <summary>Off the board, asteroid or debris.</summary>
        public bool BlocksMovement(HexCoord h) => !InBounds(h) || Get(h) != Terrain.Open;

        public bool BlocksFire(HexCoord h) => Get(h) == Terrain.Asteroid;

        /// <summary>True when no asteroid lies strictly between the two hexes.</summary>
        public bool HasLineOfFire(HexCoord from, HexCoord to)
        {
            var line = Line(from, to);
            for (int i = 1; i < line.Count - 1; i++)
                if (BlocksFire(line[i])) return false;
            return true;
        }

        public IEnumerable<HexCoord> All()
        {
            for (int row = 0; row < Height; row++)
            for (int col = 0; col < Width; col++)
                yield return HexCoord.FromOffset(col, row);
        }

        /// <summary>
        /// Hexes on the straight line from a to b, both ends included. Integer cube interpolation with a
        /// fixed nudge, so lines along hex edges always resolve to the same side on every platform.
        /// </summary>
        public static List<HexCoord> Line(HexCoord a, HexCoord b)
        {
            int n = HexCoord.Distance(a, b);
            var result = new List<HexCoord>(n + 1);
            if (n == 0)
            {
                result.Add(a);
                return result;
            }

            const long K = 1000;
            long aq = a.Q * K + 1, ar = a.R * K + 2, aS = a.S * K - 3;
            long bq = b.Q * K + 1, br = b.R * K + 2, bS = b.S * K - 3;
            long d = K * n;

            for (int i = 0; i <= n; i++)
            {
                long x = aq * (n - i) + bq * i;
                long y = ar * (n - i) + br * i;
                long z = aS * (n - i) + bS * i;
                long rq = RoundDiv(x, d), rr = RoundDiv(y, d), rs = RoundDiv(z, d);
                long dq = Abs(rq * d - x), dr = Abs(rr * d - y), ds = Abs(rs * d - z);
                if (dq > dr && dq > ds) rq = -rr - rs;
                else if (dr > ds) rr = -rq - rs;
                result.Add(new HexCoord((int)rq, (int)rr));
            }
            return result;
        }

        static long Abs(long v) => v < 0 ? -v : v;

        static long RoundDiv(long a, long b) => FloorDiv(2 * a + b, 2 * b);

        static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if (a % b != 0 && (a < 0) != (b < 0)) q--;
            return q;
        }
    }
}
