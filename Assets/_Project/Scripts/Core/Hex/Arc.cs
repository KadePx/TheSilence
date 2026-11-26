using System;

namespace Silence.Core.Hex
{
    /// <summary>
    /// Firing / shield arcs of a ship. Fore is within 30 degrees of the facing, Aft within 30 degrees
    /// of the opposite direction, Port and Starboard are the sides. A hex exactly on a 30 or 150 degree
    /// line belongs to both arcs it touches, and the defender chooses which one is hit.
    /// </summary>
    [Flags]
    public enum Arc
    {
        None = 0,
        Fore = 1,
        Starboard = 2,
        Aft = 4,
        Port = 8,
    }

    public static class Arcs
    {
        /// <summary>
        /// Arc(s) of <paramref name="target"/> as seen from a ship at <paramref name="origin"/> facing
        /// <paramref name="facing"/>. Returns two flags on a boundary, <see cref="Arc.None"/> for the same hex.
        /// Port is the ship's left on screen (for an E-facing ship, the hexes above it).
        /// </summary>
        public static Arc Of(HexCoord origin, HexDirection facing, HexCoord target)
        {
            if (origin == target) return Arc.None;

            var (va, vb) = (target - origin).ScreenVector;
            var (da, db) = facing.Offset().ScreenVector;

            // With v = (A*sqrt3, B): dot = 3*A1*A2 + B1*B2, |v|^2 = 3*A^2 + B^2, cross sign = A1*B2 - B1*A2.
            long dot = 3 * va * da + vb * db;
            long vLen2 = 3 * va * va + vb * vb;
            long dLen2 = 3 * da * da + db * db;
            long cross = da * vb - db * va; // > 0 is clockwise on screen (starboard)

            // |cos| compared with cos(30) = sqrt(3)/2  <=>  4*dot^2 vs 3*|v|^2*|d|^2
            long lhs = 4 * dot * dot;
            long rhs = 3 * vLen2 * dLen2;

            Arc side = cross > 0 ? Arc.Starboard : cross < 0 ? Arc.Port : Arc.None;

            if (lhs > rhs) return dot > 0 ? Arc.Fore : Arc.Aft;
            if (lhs == rhs) return (dot > 0 ? Arc.Fore : Arc.Aft) | side;
            return side;
        }

        /// <summary>True when the target lies in (or on the edge of) any of the given arcs.</summary>
        public static bool InAny(HexCoord origin, HexDirection facing, HexCoord target, Arc arcs)
            => (Of(origin, facing, target) & arcs) != 0;
    }
}
