using System;

namespace Silence.Core.Open
{
    /// <summary>
    /// A point on the open (gridless) battle plane, in thousandths of a span. X grows to the right of the
    /// screen and Y toward the camera, so headings turn clockwise: 0 is right, 90 is toward the camera.
    /// Whole numbers only, like the rest of Core, so a battle replays exactly from its seed and actions.
    /// </summary>
    public struct Vec : IEquatable<Vec>
    {
        public int X, Y;

        public Vec(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Vec operator +(Vec a, Vec b) => new Vec(a.X + b.X, a.Y + b.Y);
        public static Vec operator -(Vec a, Vec b) => new Vec(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Vec a, Vec b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Vec a, Vec b) => !(a == b);
        public bool Equals(Vec other) => this == other;
        public override bool Equals(object obj) => obj is Vec v && this == v;
        public override int GetHashCode() => X * 397 ^ Y;
        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>Which side of a ship something lies on. One arc only: on a boundary, fore and aft win.</summary>
    public enum OpenArc { Fore = 0, Starboard = 1, Aft = 2, Port = 3 }

    /// <summary>Whole-number geometry: a sine table in steps of 15 degrees, distances, arcs and lines of fire.</summary>
    public static class OpenMath
    {
        /// <summary>One span, the unit ranges and speeds are quoted in.</summary>
        public const int Span = 1000;
        /// <summary>Ships face one of 8 headings, one for each drawn facing of their sprite.</summary>
        public const int HeadingStep = 45;

        static readonly int[] Table = { 0, 259, 500, 707, 866, 966, 1000 };

        public static int Norm(int degrees) => ((degrees % 360) + 360) % 360;

        /// <summary>Sine times 1000 for a multiple of 15 degrees.</summary>
        public static int Sin(int degrees)
        {
            degrees = Norm(degrees);
            int i = degrees % 90 / 15; // the table is in 15 degree steps, whatever the heading step
            switch (degrees / 90)
            {
                case 0: return Table[i];
                case 1: return Table[6 - i];
                case 2: return -Table[i];
                default: return -Table[6 - i];
            }
        }

        public static int Cos(int degrees) => Sin(degrees + 90);

        /// <summary>Signed turn from one heading to another, from -179 to 180. Positive is clockwise.</summary>
        public static int Diff(int from, int to)
        {
            int d = Norm(to - from);
            return d > 180 ? d - 360 : d;
        }

        public static long ISqrt(long n)
        {
            if (n <= 0) return 0;
            // Newton's method in whole numbers: no floating point anywhere in the rules.
            long x = n, y = (x + 1) / 2;
            while (y < x)
            {
                x = y;
                y = (x + n / x) / 2;
            }
            return x;
        }

        public static int Dist(Vec a, Vec b)
        {
            long dx = a.X - b.X, dy = a.Y - b.Y;
            return (int)ISqrt(dx * dx + dy * dy);
        }

        public static Vec Step(Vec from, int heading, int distance)
            => new Vec(from.X + Cos(heading) * distance / 1000, from.Y + Sin(heading) * distance / 1000);

        /// <summary>The heading (one of 8) that points most nearly from one point at another.</summary>
        public static int Bearing(Vec from, Vec to)
        {
            long dx = to.X - from.X, dy = to.Y - from.Y;
            int best = 0;
            long bestDot = long.MinValue;
            for (int h = 0; h < 360; h += HeadingStep)
            {
                long dot = dx * Cos(h) + dy * Sin(h);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = h;
                }
            }
            return best;
        }

        /// <summary>
        /// The arc of a ship that a point lies in. Fore is within 30 degrees of the heading, aft within 30 of
        /// the reverse; the rest is starboard (the ship's right) or port.
        /// </summary>
        public static OpenArc ArcOf(Vec pos, int heading, Vec point)
        {
            long dx = point.X - pos.X, dy = point.Y - pos.Y;
            long cos = Cos(heading), sin = Sin(heading);
            long dot = dx * cos + dy * sin, cross = cos * dy - sin * dx;
            long len2 = dx * dx + dy * dy;
            // cos(30)^2 = 3/4, and the heading vector has length 1000.
            bool inCone = dot * dot * 4 >= 3 * len2 * 1000000;
            if (inCone) return dot >= 0 ? OpenArc.Fore : OpenArc.Aft;
            return cross > 0 ? OpenArc.Starboard : OpenArc.Port;
        }

        /// <summary>True if the straight line from a to b passes within radius of c.</summary>
        public static bool SegmentHits(Vec a, Vec b, Vec c, int radius)
        {
            long dx = b.X - a.X, dy = b.Y - a.Y;
            long len2 = dx * dx + dy * dy;
            long t = (c.X - a.X) * dx + (c.Y - a.Y) * dy;
            long r2 = (long)radius * radius;
            if (t <= 0) return Dist2(a, c) < r2;
            if (t >= len2) return Dist2(b, c) < r2;
            long cross = dx * (c.Y - a.Y) - dy * (c.X - a.X);
            return cross * cross < r2 * len2;
        }

        static long Dist2(Vec a, Vec b)
        {
            long dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}
