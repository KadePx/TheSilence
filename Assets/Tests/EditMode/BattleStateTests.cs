using System.Linq;
using NUnit.Framework;
using Silence.Core.Battle;
using Silence.Core.Hex;

namespace Silence.Tests
{
    /// <summary>Sprint task 3: battle state built from data, terrain, line of fire, printing.</summary>
    public class BattleStateTests
    {
        [Test]
        public void ScenarioOneBuildsAndPrints()
        {
            var b = Scenarios.InterceptAtTheRing(1);
            b.Start();

            Assert.AreEqual(7, b.Units.Count);
            Assert.AreEqual(2, b.Units.Count(u => u.Side == Side.Enemy && u.IsCapital));
            Assert.AreEqual(12, b.Deck.Count);
            Assert.AreEqual(5, b.Deck.Hand.Count);
            Assert.AreEqual(3, b.Command);
            Assert.AreEqual(BattlePhase.Command, b.Phase);

            string text = b.Describe();
            TestContext.WriteLine(text);
            StringAssert.Contains("Sixth Verdict", text);
            StringAssert.Contains("Concord frigate", text);
            var boardRows = text.Split('\n').Skip(1).Take(7);
            Assert.AreEqual(2, boardRows.Sum(r => r.Count(ch => ch == '#')), "two asteroids on the board");
        }

        [Test]
        public void UnitCopiesItsSpec()
        {
            var u = Unit.From(PaperUnits.LanceCruiser, 1, Side.Player, HexCoord.Zero, HexDirection.E, 1);
            Assert.AreEqual(30, u.Hull);
            Assert.AreEqual(3, u.Armor[ArcIndex.Fore]);
            Assert.AreEqual(2, u.Armor[ArcIndex.Port]);
            Assert.AreEqual(1, u.Armor[ArcIndex.Aft]);
            Assert.AreEqual(4, u.Shields[ArcIndex.Starboard]);
            Assert.AreEqual(10, u.HeatCap);
        }

        [Test]
        public void LineIncludesBothEndsAndIsContiguous()
        {
            var a = T.At(0, 3);
            var c = T.At(8, 0);
            var line = Board.Line(a, c);
            Assert.AreEqual(a, line.First());
            Assert.AreEqual(c, line.Last());
            Assert.AreEqual(a.DistanceTo(c) + 1, line.Count);
            for (int i = 1; i < line.Count; i++) Assert.AreEqual(1, line[i - 1].DistanceTo(line[i]));
        }

        [Test]
        public void AsteroidBlocksFireButDebrisDoesNot()
        {
            var board = new Board();
            board.Set(3, 3, Terrain.Asteroid);
            board.Set(3, 1, Terrain.Debris);
            Assert.IsFalse(board.HasLineOfFire(T.At(1, 3), T.At(5, 3)));
            Assert.IsTrue(board.HasLineOfFire(T.At(1, 1), T.At(5, 1)));
            Assert.IsTrue(board.BlocksMovement(T.At(3, 1)));
            Assert.IsTrue(board.HasLineOfFire(T.At(2, 3), T.At(3, 3)), "adjacent hexes always see each other");
        }

        [Test]
        public void OutOfBoundsBlocksMovement()
        {
            var board = new Board();
            Assert.IsTrue(board.BlocksMovement(T.At(-1, 0)));
            Assert.IsTrue(board.BlocksMovement(T.At(9, 0)));
            Assert.IsTrue(board.BlocksMovement(T.At(0, 7)));
            Assert.IsFalse(board.BlocksMovement(T.At(8, 6)));
        }
    }
}
