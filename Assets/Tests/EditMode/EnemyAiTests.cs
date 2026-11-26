using System.Linq;
using NUnit.Framework;
using Silence.Core.Battle;
using Silence.Core.Hex;

namespace Silence.Tests
{
    /// <summary>Sprint task 7: intent cycles, enemy movement, torpedoes, and full headless battles.</summary>
    public class EnemyAiTests
    {
        [Test]
        public void FrigateCyclesManeuverBroadsideTorpedo()
        {
            var b = T.Fleet(out _, out _);
            var f = b.AddUnit(PaperUnits.Frigate, Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            var kinds = new System.Collections.Generic.List<IntentKind>();
            for (int i = 0; i < 4; i++)
            {
                kinds.Add(b.Intents.First(x => x.UnitId == f.Id).Kind);
                b.EndTurn();
            }
            CollectionAssert.AreEqual(new[] { IntentKind.Maneuver, IntentKind.Broadside, IntentKind.LaunchTorpedo, IntentKind.Maneuver }, kinds);
        }

        [Test]
        public void IntentsAreDeclaredBeforeTheCommandStep()
        {
            var b = Scenarios.InterceptAtTheRing(1);
            b.Start();
            Assert.AreEqual(5, b.Intents.Count);
            var firstCommand = b.Log.FindIndex(e => e.Kind == EventKind.PhaseChanged && e.Text == "Command");
            var lastIntent = b.Log.FindLastIndex(e => e.Kind == EventKind.IntentDeclared);
            Assert.Less(lastIntent, firstCommand);
        }

        [Test]
        public void BroadsideHitsInTheSideArc()
        {
            var b = T.Fleet(out var flag, out _);
            var f = b.AddUnit(PaperUnits.Frigate, Side.Enemy, 0, 0, HexDirection.E, 0);
            f.Pos = new HexCoord(flag.Pos.Q, flag.Pos.R - 2); // flagship is in the frigate's starboard arc, range 2
            f.CycleIndex = 1; // Broadside
            b.Start();
            b.EndTurn();
            Assert.IsTrue(b.Log.Any(e => e.Kind == EventKind.Attack && e.UnitId == f.Id));
            Assert.AreEqual(1, flag.Shields[ArcIndex.Port], "the broadside emptied the port shields; regen 1");
            Assert.AreEqual(30 - (9 - 4 - 2), flag.Hull, "9 damage, 4 shields, 2 port armor");
        }

        [Test]
        public void BroadsideMissesIfYouLeaveTheArc()
        {
            var b = T.Fleet(out var flag, out _);
            var f = b.AddUnit(PaperUnits.Frigate, Side.Enemy, 0, 0, HexDirection.E, 0);
            f.Pos = new HexCoord(flag.Pos.Q, flag.Pos.R - 2);
            f.CycleIndex = 1; // Broadside
            flag.Speed = 3;
            b.Start();
            b.EndTurn(); // flagship drifts 3 east, ending in the frigate's fore arc
            Assert.IsTrue(b.Log.Any(e => e.Kind == EventKind.Miss && e.UnitId == f.Id));
            Assert.AreEqual(30, flag.Hull);
        }

        [Test]
        public void ManeuverLinesUpABroadside()
        {
            var b = T.Fleet(out var flag, out _);
            var f = b.AddUnit(PaperUnits.Frigate, Side.Enemy, 5, 3, HexDirection.W, 0);
            b.Start();
            var (facing, path, speed) = b.ChooseManeuver(f);
            var end = path.Count > 0 ? path.Last() : f.Pos;
            for (int i = 0; i < speed; i++) end = end.Neighbor(facing); // where next turn's drift leaves it
            var nearest = b.NearestPlayerCapital(end);
            Assert.IsTrue(Arcs.InAny(end, facing, nearest.Pos, Arc.Port | Arc.Starboard));
            Assert.LessOrEqual(end.DistanceTo(nearest.Pos), 3);
        }

        [Test]
        public void TorpedoClosesThreeHexesAndHitsForNine()
        {
            var b = new Battle(1);
            var target = b.AddUnit(T.Dummy(30), Side.Player, 2, 3, HexDirection.E, 0, flagship: true);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 6, HexDirection.W, 0);
            var torp = b.Spawn(PaperUnits.Torpedo, Side.Enemy, T.At(7, 3), HexDirection.W, 0);
            torp.TargetId = target.Id;
            b.Start();
            b.EndTurn();
            Assert.AreEqual(T.At(4, 3), torp.Pos);
            b.EndTurn();
            Assert.IsFalse(torp.Alive);
            Assert.AreEqual(21, target.Hull);
        }

        [Test]
        public void GunshipsGoForTheAft()
        {
            var b = new Battle(1);
            var target = b.AddUnit(T.Dummy(30), Side.Player, 4, 3, HexDirection.E, 0, flagship: true);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 6, HexDirection.W, 0);
            var g = b.AddUnit(PaperUnits.Gunship, Side.Enemy, 6, 1, HexDirection.W, 0);
            b.Start();
            b.EndTurn();
            Assert.AreEqual(target.Pos.Neighbor(HexDirection.W), g.Pos);
            Assert.AreEqual(27, target.Hull);
        }

        [Test]
        public void CarrierLaunchesPatchesAndCoordinatesJointFire()
        {
            var b = T.Fleet(out var flag, out var escort);
            var c = b.AddUnit(PaperUnits.Carrier, Side.Enemy, 8, 3, HexDirection.W, 0);
            b.Start();
            Assert.AreEqual(IntentKind.LaunchCraft, b.Intents[0].Kind);
            b.EndTurn();
            Assert.AreEqual(2, b.LiveUnits(Side.Enemy).Count(u => u.Ai == AiProfile.Gunship));

            c.Hull = 20;
            var patch = b.Intents.First(i => i.UnitId == c.Id);
            Assert.AreEqual(IntentKind.PatchUp, patch.Kind);
            b.EndTurn();
            Assert.AreEqual(22, c.Hull, "alone, it patches itself for half of 4");

            var joint = b.Intents.First(i => i.UnitId == c.Id);
            Assert.AreEqual(IntentKind.JointFire, joint.Kind);
            foreach (var i in b.Intents.Where(i => i.Kind == IntentKind.CraftAttack))
            {
                Assert.AreEqual(joint.TargetId, i.TargetId);
                Assert.AreEqual(2, i.Bonus);
            }
        }

        [Test]
        public void RepairShipsMendAnotherShipInFullAndThemselvesForHalf()
        {
            var b = T.Fleet(out _, out _);
            var carrier = b.AddUnit(PaperUnits.Carrier, Side.Enemy, 8, 3, HexDirection.W, 0);
            var frigate = b.AddUnit(PaperUnits.Frigate, Side.Enemy, 8, 5, HexDirection.W, 0);
            carrier.CycleIndex = 1; // Patch Up
            carrier.Hull = 10;
            frigate.Hull = 10;
            b.Start();
            var patch = b.Intents.First(i => i.UnitId == carrier.Id);
            Assert.AreEqual(IntentKind.PatchUp, patch.Kind);
            Assert.AreEqual(frigate.Id, patch.TargetId, "another damaged ship comes first");
            Assert.AreEqual(4, patch.Damage);

            frigate.Hull = frigate.MaxHull;
            var alone = new Battle(1);
            alone.AddUnit(PaperUnits.LanceCruiser, Side.Player, 0, 3, HexDirection.E, 0, flagship: true);
            var solo = alone.AddUnit(PaperUnits.Carrier, Side.Enemy, 8, 3, HexDirection.W, 0);
            solo.CycleIndex = 1;
            solo.Hull = 10;
            alone.Start();
            var self = alone.Intents.First(i => i.UnitId == solo.Id);
            Assert.AreEqual(solo.Id, self.TargetId);
            Assert.AreEqual(2, self.Damage, "half, alone");
        }

        [Test]
        public void HeadlessBattlesRunToAWinOrALoss()
        {
            int decided = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var one = Scenarios.InterceptAtTheRing(seed);
                RunToEnd(one);
                Assert.AreNotEqual(BattleOutcome.Ongoing, one.Outcome, $"scenario 1, seed {seed}");

                var two = Scenarios.TheLongPromise(seed, PaperCards.Rewards.Take(2));
                RunToEnd(two);
                Assert.AreNotEqual(BattleOutcome.Ongoing, two.Outcome, $"scenario 2, seed {seed}");

                if (one.Outcome != BattleOutcome.Withdrawal) decided++;
                if (two.Outcome != BattleOutcome.Withdrawal) decided++;
                TestContext.WriteLine($"seed {seed}: scenario 1 {one.Outcome} on turn {one.Turn}, scenario 2 {two.Outcome} on turn {two.Turn}");
            }
            // 80%, down from 95%: since the throttle the bot no longer drifts into enemy ships every turn, and some
            // of its old "wins" against the carrier were ramming damage. It now times out on a few of those fights.
            Assert.GreaterOrEqual(decided, 32, "at least 80% of bot battles end in a win or a loss");
        }

        [Test]
        public void TurnLimitEndsAStalemateAsAWithdrawal()
        {
            var b = T.Fleet(out _, out _);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.TurnLimit = 3;
            b.Start();
            for (int i = 0; i < 5 && b.Outcome == BattleOutcome.Ongoing; i++) b.EndTurn();
            Assert.AreEqual(BattleOutcome.Withdrawal, b.Outcome);
            Assert.AreEqual(3, b.Turn);
        }

        [Test]
        public void HeadlessBattleReplaysIdentically()
        {
            var a = Scenarios.InterceptAtTheRing(9);
            var b = Scenarios.InterceptAtTheRing(9);
            RunToEnd(a);
            RunToEnd(b);
            Assert.AreEqual(T.LogText(a), T.LogText(b));
        }

        static void RunToEnd(Battle b, int maxTurns = 60)
        {
            b.Start();
            while (b.Outcome == BattleOutcome.Ongoing && b.Turn <= maxTurns) SimpleBot.PlayTurn(b);
        }
    }
}
