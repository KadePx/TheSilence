using System.Linq;
using NUnit.Framework;
using Silence.Core.Battle;
using Silence.Core.Hex;

namespace Silence.Tests
{
    /// <summary>Rules added in the core review: the throttle, and heat as a resource (Redline, Thermal Purge).</summary>
    public class CoreReviewTests
    {
        Battle b;
        Unit flag, escort, foe;

        [SetUp]
        public void SetUp()
        {
            b = T.Fleet(out flag, out escort);
            foe = b.AddUnit(T.Dummy(), Side.Enemy, 4, 3, HexDirection.W, 0);
            b.Start();
            b.Command = 10;
        }

        // ---------------------------------------------------------------- throttle

        [Test]
        public void ThrottleUpAddsSpeedAndOneHeat()
        {
            Assert.IsNull(b.Throttle(flag.Id, 1));
            Assert.AreEqual(1, flag.Speed);
            Assert.AreEqual(Battle.ThrottleHeat, flag.Heat);
        }

        [Test]
        public void ThrottleDownIsFree()
        {
            flag.Speed = 2;
            Assert.IsNull(b.Throttle(flag.Id, -1));
            Assert.AreEqual(1, flag.Speed);
            Assert.AreEqual(0, flag.Heat);
            Assert.AreEqual(10, b.Command, "the throttle costs no command");
        }

        [Test]
        public void OneThrottleStepPerShipPerTurn()
        {
            Assert.IsNull(b.Throttle(flag.Id, 1));
            Assert.IsNotNull(b.Throttle(flag.Id, 1), "already used this turn");
            Assert.IsNull(b.Throttle(escort.Id, 1), "each ship has its own helm");
            b.EndTurn();
            Assert.IsNull(b.CheckThrottle(flag.Id, 1), "available again next turn");
        }

        [Test]
        public void ThrottleStaysWithinZeroAndMaxSpeed()
        {
            Assert.IsNotNull(b.Throttle(flag.Id, -1), "already stopped");
            flag.Speed = flag.MaxSpeed;
            Assert.IsNotNull(b.Throttle(flag.Id, 1), "already at max");
            Assert.IsNotNull(b.Throttle(flag.Id, 2), "one step at a time");
        }

        [Test]
        public void ThrottleOnlyWorksOnYourOwnCapitalShips()
        {
            Assert.IsNotNull(b.Throttle(foe.Id, 1));
            Assert.IsNotNull(b.Throttle(999, 1));
        }

        [Test]
        public void AnEngineHitBlocksSpeedingUpButNotSlowingDown()
        {
            flag.Speed = 1;
            flag.NoBurnThisTurn = true;
            Assert.IsNotNull(b.CheckThrottle(flag.Id, 1));
            Assert.IsNull(b.CheckThrottle(flag.Id, -1));
        }

        [Test]
        public void AStoppedShipDoesNotDrift()
        {
            flag.Speed = 1;
            var start = flag.Pos;
            Assert.IsNull(b.Throttle(flag.Id, -1));
            b.EndTurn();
            Assert.AreEqual(start, flag.Pos);
        }

        [Test]
        public void TheBotStopsRatherThanDriftIntoAnAsteroid()
        {
            var bb = new Battle(1);
            bb.Board.Set(3, 3, Terrain.Asteroid);
            var f = bb.AddUnit(PaperUnits.LanceCruiser, Side.Player, 2, 3, HexDirection.E, 1, flagship: true);
            bb.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            bb.Start();
            SimpleBot.PlayTurn(bb);
            Assert.AreEqual(0, f.Speed);
            Assert.IsFalse(bb.Log.Any(e => e.Kind == EventKind.Collision));
        }

        // ---------------------------------------------------------------- heat as a resource

        [Test]
        public void RedlineTradesHeatForCommand()
        {
            var c = T.Give(b, PaperCards.Redline, flag);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.None));
            Assert.AreEqual(11, b.Command);
            Assert.AreEqual(4, flag.Heat);
        }

        [Test]
        public void ThermalPurgeTurnsHeatIntoDamageThatIgnoresShields()
        {
            var shielded = b.AddUnit(T.Dummy(20, shields: 5), Side.Enemy, 3, 3, HexDirection.W, 0);
            flag.Heat = 6;
            var c = T.Give(b, PaperCards.ThermalPurge, flag);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(shielded.Id)));
            Assert.AreEqual(14, shielded.Hull);
            Assert.AreEqual(0, flag.Heat);
            Assert.AreEqual(5, shielded.Shields[ArcIndex.Fore]);
        }

        [Test]
        public void ThermalPurgeIsCappedAndNeedsHeatAndRange()
        {
            var near = b.AddUnit(T.Dummy(20), Side.Enemy, 3, 3, HexDirection.W, 0);
            var far = b.AddUnit(T.Dummy(20), Side.Enemy, 7, 3, HexDirection.W, 0);
            var c = T.Give(b, PaperCards.ThermalPurge, flag);
            Assert.IsNotNull(b.PlayCard(c.Id, PlayTarget.At(near.Id)), "no heat to purge");
            flag.Heat = 12;
            Assert.IsNotNull(b.PlayCard(c.Id, PlayTarget.At(far.Id)), "out of range");
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(near.Id)));
            Assert.AreEqual(12, near.Hull, "at most 8 damage");
        }
    }
}
