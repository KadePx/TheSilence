using System.Linq;
using NUnit.Framework;
using Silence.Core.Battle;
using Silence.Core.Hex;

namespace Silence.Tests
{
    /// <summary>Sprint task 4: Intent, Command, Drift, Enemy action, End; collisions, shields, venting.</summary>
    public class TurnLoopTests
    {
        static string ScriptedTurn(ulong seed)
        {
            var b = Scenarios.InterceptAtTheRing(seed);
            b.Start();
            var flag = b.Flagship;
            var hard = b.Deck.Hand.FirstOrDefault(c => c.Def == PaperCards.HardAbout);
            if (hard != null) b.PlayCard(hard.Id, PlayTarget.Turning(flag.Id, 1));
            b.EndTurn();
            SimpleBot.PlayTurn(b);
            return T.LogText(b);
        }

        [Test]
        public void ScriptedTurnReplaysExactly()
        {
            Assert.AreEqual(ScriptedTurn(42), ScriptedTurn(42));
        }

        [Test]
        public void DifferentSeedsShuffleDifferently()
        {
            Assert.AreNotEqual(ScriptedTurn(1), ScriptedTurn(2));
        }

        [Test]
        public void PhasesRunInOrder()
        {
            var b = Scenarios.InterceptAtTheRing(3);
            b.Start();
            b.EndTurn();
            var phases = b.Log.Where(e => e.Kind == EventKind.PhaseChanged).Select(e => e.Text).ToList();
            CollectionAssert.AreEqual(new[] { "Intent", "Command", "Drift", "EnemyAction", "End", "Intent", "Command" }, phases);
            Assert.AreEqual(2, b.Turn);
        }

        [Test]
        public void EachTurnDrawsFiveAndResetsCommand()
        {
            var b = Scenarios.InterceptAtTheRing(4);
            b.Start();
            b.Command = 1;
            b.EndTurn();
            Assert.AreEqual(3, b.Command, "unspent command is lost and refilled to 3");
            Assert.AreEqual(5, b.Deck.Hand.Count);
            Assert.AreEqual(10, b.Deck.Count);
        }

        [Test]
        public void DriftMovesStraightAheadBySpeed()
        {
            var b = T.Fleet(out var flag, out _);
            flag.Speed = 2;
            b.Start();
            b.EndTurn();
            Assert.AreEqual(T.At(4, 3), flag.Pos);
            Assert.AreEqual(HexDirection.E, flag.Facing);
        }

        [Test]
        public void DriftHoldsAtTheEdge()
        {
            var b = new Battle(1);
            var u = b.AddUnit(T.Dummy(), Side.Player, 7, 3, HexDirection.E, 3);
            b.AddUnit(T.Dummy(), Side.Enemy, 0, 0, HexDirection.E, 0);
            b.Start();
            b.EndTurn();
            Assert.AreEqual(T.At(8, 3), u.Pos);
        }

        [Test]
        public void CollisionHitsBothForSpeedTimesTwoAndStopsTheMover()
        {
            var b = new Battle(1);
            var mover = b.AddUnit(T.Dummy(), Side.Player, 2, 3, HexDirection.E, 2);
            var other = b.AddUnit(T.Dummy(), Side.Enemy, 3, 3, HexDirection.W, 0);
            b.Start();
            b.EndTurn();
            Assert.AreEqual(T.At(2, 3), mover.Pos);
            Assert.AreEqual(16, mover.Hull);
            Assert.AreEqual(16, other.Hull);
        }

        [Test]
        public void AsteroidStopsDriftAndHurtsTheMover()
        {
            var b = new Battle(1);
            b.Board.Set(4, 3, Terrain.Asteroid);
            var mover = b.AddUnit(T.Dummy(), Side.Player, 2, 3, HexDirection.E, 3);
            b.AddUnit(T.Dummy(), Side.Enemy, 0, 0, HexDirection.E, 0);
            b.Start();
            b.EndTurn();
            Assert.AreEqual(T.At(3, 3), mover.Pos);
            Assert.AreEqual(14, mover.Hull);
        }

        [Test]
        public void ShieldsRegeneratePerArcUpToMax()
        {
            var b = T.Fleet(out var flag, out _);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            flag.Shields[ArcIndex.Fore] = 0;
            flag.Shields[ArcIndex.Aft] = 3;
            b.EndTurn();
            Assert.AreEqual(2, flag.Shields[ArcIndex.Fore]);
            Assert.AreEqual(4, flag.Shields[ArcIndex.Aft]);
            Assert.AreEqual(4, flag.Shields[ArcIndex.Port]);
        }

        [Test]
        public void ShipsVentHeatAtEnd()
        {
            var b = T.Fleet(out var flag, out var escort);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            flag.Heat = 7;
            escort.Heat = 1;
            b.EndTurn();
            Assert.AreEqual(5, flag.Heat);
            Assert.AreEqual(0, escort.Heat);
        }

        [Test]
        public void PredictDriftMatchesDrift()
        {
            var b = T.Fleet(out var flag, out _);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            flag.Speed = 3;
            b.Start();
            var predicted = b.PredictDrift(flag);
            b.EndTurn();
            Assert.AreEqual(predicted, flag.Pos);
        }
    }
}
