using NUnit.Framework;
using Silence.Core.Hex;

namespace Silence.Tests
{
    public class HexTests
    {
        [Test]
        public void OffsetRoundTrip()
        {
            for (int row = 0; row < 7; row++)
            for (int col = 0; col < 9; col++)
            {
                var h = HexCoord.FromOffset(col, row);
                Assert.AreEqual((col, row), h.ToOffset());
            }
        }

        [Test]
        public void EveryNeighborIsDistanceOne()
        {
            var c = new HexCoord(3, -2);
            for (int i = 0; i < HexDirections.Count; i++)
                Assert.AreEqual(1, c.DistanceTo(c.Neighbor((HexDirection)i)));
        }

        [Test]
        public void DistanceMatchesKnownValues()
        {
            Assert.AreEqual(0, HexCoord.Distance(HexCoord.Zero, HexCoord.Zero));
            Assert.AreEqual(3, HexCoord.Distance(HexCoord.Zero, new HexCoord(3, -3)));
            Assert.AreEqual(3, HexCoord.Distance(new HexCoord(-1, 2), new HexCoord(2, 1)));
            Assert.AreEqual(4, HexCoord.Distance(new HexCoord(-2, 2), new HexCoord(2, 0)));
        }

        [Test]
        public void RotateWrapsBothWays()
        {
            Assert.AreEqual(HexDirection.NE, HexDirection.E.Rotate(1));
            Assert.AreEqual(HexDirection.SE, HexDirection.E.Rotate(-1));
            Assert.AreEqual(HexDirection.W, HexDirection.E.Opposite());
            Assert.AreEqual(HexDirection.E, HexDirection.SE.Rotate(7));
            Assert.AreEqual(3, HexDirections.TurnDistance(HexDirection.NE, HexDirection.SW));
            Assert.AreEqual(1, HexDirections.TurnDistance(HexDirection.SE, HexDirection.E));
        }

        [Test]
        public void ScreenAnglesMatchFacings()
        {
            int[] expected = { 0, -60, -120, 180, 120, 60 };
            for (int i = 0; i < 6; i++)
            {
                var d = (HexDirection)i;
                Assert.AreEqual(expected[i], d.ScreenAngle());
                var (x, y) = d.Offset().ToScreen(1);
                double deg = System.Math.Atan2(y, x) * 180 / System.Math.PI;
                Assert.AreEqual(expected[i] == 180 ? 180 : expected[i], deg, 1e-9);
            }
        }

        [Test]
        public void SpritesOnlyUseDrawnFacings()
        {
            Assert.AreEqual((HexDirection.E, true), HexDirection.W.SpriteFor());
            Assert.AreEqual((HexDirection.NE, true), HexDirection.SW.SpriteFor());
            Assert.AreEqual((HexDirection.SE, true), HexDirection.NW.SpriteFor());
            Assert.AreEqual((HexDirection.NE, false), HexDirection.NE.SpriteFor());
        }
    }

    public class ArcTests
    {
        static readonly HexCoord O = HexCoord.Zero;

        [Test]
        public void AdjacentHexesForEveryFacing()
        {
            // For each facing: the hex ahead is Fore, behind is Aft, the two to the left are Port,
            // the two to the right are Starboard.
            for (int f = 0; f < 6; f++)
            {
                var facing = (HexDirection)f;
                Assert.AreEqual(Arc.Fore, Arcs.Of(O, facing, O.Neighbor(facing)), $"fore {facing}");
                Assert.AreEqual(Arc.Aft, Arcs.Of(O, facing, O.Neighbor(facing.Rotate(3))), $"aft {facing}");
                Assert.AreEqual(Arc.Port, Arcs.Of(O, facing, O.Neighbor(facing.Rotate(1))), $"port {facing}");
                Assert.AreEqual(Arc.Port, Arcs.Of(O, facing, O.Neighbor(facing.Rotate(2))), $"port2 {facing}");
                Assert.AreEqual(Arc.Starboard, Arcs.Of(O, facing, O.Neighbor(facing.Rotate(-1))), $"stbd {facing}");
                Assert.AreEqual(Arc.Starboard, Arcs.Of(O, facing, O.Neighbor(facing.Rotate(-2))), $"stbd2 {facing}");
            }
        }

        [Test]
        public void BoundaryHexesBelongToBothArcs()
        {
            // (1,-1)+(1,0) is at exactly -30 degrees from an E-facing ship: Fore and Port.
            var facing = HexDirection.E;
            var at30Port = new HexCoord(2, -1);
            var at30Stbd = new HexCoord(1, 1);
            var at150Port = new HexCoord(-1, -1);
            var at150Stbd = new HexCoord(-2, 1);
            Assert.AreEqual(Arc.Fore | Arc.Port, Arcs.Of(O, facing, at30Port));
            Assert.AreEqual(Arc.Fore | Arc.Starboard, Arcs.Of(O, facing, at30Stbd));
            Assert.AreEqual(Arc.Aft | Arc.Port, Arcs.Of(O, facing, at150Port));
            Assert.AreEqual(Arc.Aft | Arc.Starboard, Arcs.Of(O, facing, at150Stbd));
        }

        [Test]
        public void BoundariesRotateWithFacing()
        {
            var at30Port = new HexCoord(2, -1);
            for (int f = 0; f < 6; f++)
            {
                var rotated = RotateCoord(at30Port, f);
                Assert.AreEqual(Arc.Fore | Arc.Port, Arcs.Of(O, (HexDirection)f, rotated), $"facing {(HexDirection)f}");
            }
        }

        [Test]
        public void DistantTargetsAreClassified()
        {
            Assert.AreEqual(Arc.Fore, Arcs.Of(O, HexDirection.E, new HexCoord(5, 0)));
            Assert.AreEqual(Arc.Fore, Arcs.Of(O, HexDirection.E, new HexCoord(5, -1)));
            Assert.AreEqual(Arc.Port, Arcs.Of(O, HexDirection.E, new HexCoord(1, -4)));
            Assert.AreEqual(Arc.Starboard, Arcs.Of(O, HexDirection.E, new HexCoord(-1, 4)));
            Assert.AreEqual(Arc.Aft, Arcs.Of(O, HexDirection.E, new HexCoord(-5, 1)));
            Assert.AreEqual(Arc.None, Arcs.Of(O, HexDirection.E, O));
        }

        [Test]
        public void ArcsAreTranslationInvariant()
        {
            var origin = HexCoord.FromOffset(4, 3);
            var target = new HexCoord(2, -1);
            Assert.AreEqual(Arcs.Of(O, HexDirection.NE, target), Arcs.Of(origin, HexDirection.NE, origin + target));
        }

        // Rotate an axial coordinate by 60 degree steps counter-clockwise on screen.
        static HexCoord RotateCoord(HexCoord h, int steps)
        {
            int q = h.Q, r = h.R, s = h.S;
            for (int i = 0; i < ((steps % 6) + 6) % 6; i++)
            {
                // CCW on screen (y down), E to NE: (q, r, s) -> (-s, -q, -r)
                int nq = -s, nr = -q, ns = -r;
                q = nq; r = nr; s = ns;
            }
            return new HexCoord(q, r);
        }
    }
}
