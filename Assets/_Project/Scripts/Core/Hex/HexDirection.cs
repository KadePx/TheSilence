namespace Silence.Core.Hex
{
    /// <summary>
    /// The six facings of a pointy-top hex, in counter-clockwise order as seen on screen.
    /// Screen angles (y axis down): E 0, NE -60, NW -120, W 180, SW 120, SE 60.
    /// </summary>
    public enum HexDirection
    {
        E = 0,
        NE = 1,
        NW = 2,
        W = 3,
        SW = 4,
        SE = 5,
    }

    public static class HexDirections
    {
        public const int Count = 6;

        static readonly HexCoord[] Offsets =
        {
            new HexCoord(1, 0),   // E
            new HexCoord(1, -1),  // NE
            new HexCoord(0, -1),  // NW
            new HexCoord(-1, 0),  // W
            new HexCoord(-1, 1),  // SW
            new HexCoord(0, 1),   // SE
        };

        static readonly int[] ScreenAngles = { 0, -60, -120, 180, 120, 60 };

        /// <summary>Axial step one hex in this direction.</summary>
        public static HexCoord Offset(this HexDirection d) => Offsets[(int)d];

        /// <summary>Screen angle in degrees with the y axis pointing down (E = 0, NE = -60).</summary>
        public static int ScreenAngle(this HexDirection d) => ScreenAngles[(int)d];

        /// <summary>Turn by a number of 60 degree steps; positive is counter-clockwise on screen (E to NE).</summary>
        public static HexDirection Rotate(this HexDirection d, int steps)
        {
            int i = ((int)d + steps) % Count;
            if (i < 0) i += Count;
            return (HexDirection)i;
        }

        public static HexDirection Opposite(this HexDirection d) => d.Rotate(3);

        /// <summary>Smallest number of 60 degree turns between two facings (0 to 3).</summary>
        public static int TurnDistance(HexDirection a, HexDirection b)
        {
            int diff = ((int)b - (int)a + Count) % Count;
            return diff > 3 ? Count - diff : diff;
        }

        /// <summary>
        /// Sprite to draw for a facing. Only E, NE and SE are drawn; W, SW and NW are the
        /// E, NE and SE sprites turned 180 degrees (see Docs/UnitySetupPlan.md).
        /// </summary>
        public static (HexDirection sprite, bool rotate180) SpriteFor(this HexDirection d)
        {
            switch (d)
            {
                case HexDirection.W: return (HexDirection.E, true);
                case HexDirection.SW: return (HexDirection.NE, true);
                case HexDirection.NW: return (HexDirection.SE, true);
                default: return (d, false);
            }
        }
    }
}
