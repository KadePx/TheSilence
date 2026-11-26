using System;

namespace Silence.Core.Hex
{
    /// <summary>
    /// Axial hex coordinate (q, r) on a pointy-top grid. Cube s = -q - r.
    /// The board is stored as offset "odd-r" columns/rows; convert with <see cref="FromOffset"/>.
    /// </summary>
    [Serializable]
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int Q;
        public readonly int R;
        public int S => -Q - R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public static readonly HexCoord Zero = new HexCoord(0, 0);

        // ---- offset (odd-r) conversion: odd rows are shoved right by half a hex ----

        public static HexCoord FromOffset(int col, int row)
        {
            int q = col - (row - (row & 1)) / 2;
            return new HexCoord(q, row);
        }

        public (int col, int row) ToOffset()
        {
            int col = Q + (R - (R & 1)) / 2;
            return (col, R);
        }

        // ---- arithmetic ----

        public static HexCoord operator +(HexCoord a, HexCoord b) => new HexCoord(a.Q + b.Q, a.R + b.R);
        public static HexCoord operator -(HexCoord a, HexCoord b) => new HexCoord(a.Q - b.Q, a.R - b.R);
        public static HexCoord operator *(HexCoord a, int k) => new HexCoord(a.Q * k, a.R * k);
        public static bool operator ==(HexCoord a, HexCoord b) => a.Q == b.Q && a.R == b.R;
        public static bool operator !=(HexCoord a, HexCoord b) => !(a == b);

        public HexCoord Neighbor(HexDirection d) => this + d.Offset();

        /// <summary>Hex distance in steps.</summary>
        public static int Distance(HexCoord a, HexCoord b)
        {
            HexCoord d = a - b;
            return (Math.Abs(d.Q) + Math.Abs(d.R) + Math.Abs(d.S)) / 2;
        }

        public int DistanceTo(HexCoord other) => Distance(this, other);

        /// <summary>
        /// Screen-space vector from the origin, scaled so both parts are integers:
        /// x = A * sqrt(3) / 2 and y = B / 2 hex sizes, with y pointing down.
        /// Exact integer maths keeps arc boundaries (30 and 150 degrees) exact.
        /// </summary>
        public (long A, long B) ScreenVector => (2L * Q + R, 3L * R);

        /// <summary>Screen position in hex-size units (pointy-top, y down). For the view only.</summary>
        public (double x, double y) ToScreen(double size)
        {
            double x = size * Math.Sqrt(3) * (Q + R / 2.0);
            double y = size * 1.5 * R;
            return (x, y);
        }

        public bool Equals(HexCoord other) => this == other;
        public override bool Equals(object obj) => obj is HexCoord h && this == h;
        public override int GetHashCode() => unchecked((Q * 397) ^ R);
        public override string ToString() => $"({Q},{R})";
    }
}
