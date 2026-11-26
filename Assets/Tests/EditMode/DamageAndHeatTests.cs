using System.Linq;
using NUnit.Framework;
using Silence.Core.Battle;
using Silence.Core.Hex;

namespace Silence.Tests
{
    /// <summary>Sprint task 6: damage order, arcs, crits, junk, heat thresholds, destruction.</summary>
    public class DamageAndHeatTests
    {
        static Battle Pair(out Unit attacker, out Unit target, UnitSpec targetSpec)
        {
            var b = new Battle(1);
            attacker = b.AddUnit(T.Dummy(), Side.Player, 2, 3, HexDirection.E, 0);
            target = b.AddUnit(targetSpec, Side.Enemy, 5, 3, HexDirection.W, 0);
            return b;
        }

        [Test]
        public void ShieldsThenArmorThenHull()
        {
            var b = Pair(out var a, out var t, T.Dummy(20, shields: 3, armor: 2));
            int hull = b.DealDamage(t, 9, a.Pos, new DamageFlags(), a);
            Assert.AreEqual(4, hull);
            Assert.AreEqual(16, t.Hull);
            Assert.AreEqual(0, t.Shields[ArcIndex.Fore]);
            Assert.AreEqual(3, t.Shields[ArcIndex.Aft], "only the hit arc loses shields");
        }

        [Test]
        public void ArmorOnlyReducesOncePerAttack()
        {
            var b = Pair(out var a, out var t, T.Dummy(20, shields: 0, armor: 2));
            b.DealDamage(t, 1, a.Pos, new DamageFlags(), a);
            b.DealDamage(t, 3, a.Pos, new DamageFlags(), a);
            Assert.AreEqual(19, t.Hull);
        }

        [Test]
        public void AttackFromBehindHitsAft()
        {
            var b = new Battle(1);
            var t = b.AddUnit(PaperUnits.LanceCruiser, Side.Enemy, 4, 3, HexDirection.E, 0);
            b.DealDamage(t, 6, T.At(2, 3), new DamageFlags(), null);
            Assert.AreEqual(0, t.Shields[ArcIndex.Aft]);
            Assert.AreEqual(4, t.Shields[ArcIndex.Fore]);
            Assert.AreEqual(29, t.Hull, "6 - 4 shields - 1 aft armor");
        }

        [Test]
        public void OnABoundaryTheDefenderPicksTheStrongerArc()
        {
            var b = new Battle(1);
            var t = b.AddUnit(PaperUnits.LanceCruiser, Side.Enemy, 4, 3, HexDirection.E, 0);
            // Two hexes NE + E of the ship lies exactly on the 30 degree line: Fore and Port both apply.
            var from = t.Pos.Neighbor(HexDirection.E).Neighbor(HexDirection.NE);
            Assert.AreEqual(Arc.Fore | Arc.Port, Arcs.Of(t.Pos, t.Facing, from));
            Assert.AreEqual(ArcIndex.Fore, Battle.HitArc(t, from), "fore armor 3 beats port armor 2");
            t.Shields[ArcIndex.Fore] = 0;
            Assert.AreEqual(ArcIndex.Port, Battle.HitArc(t, from));
        }

        [Test]
        public void IgnoreShieldsAndIgnoreArmor()
        {
            var b = Pair(out var a, out var t, T.Dummy(20, shields: 5, armor: 3));
            b.DealDamage(t, 4, a.Pos, new DamageFlags { IgnoreShields = true, IgnoreArmor = true }, a);
            Assert.AreEqual(16, t.Hull);
            Assert.AreEqual(5, t.Shields[ArcIndex.Fore]);
        }

        [Test]
        public void FiveHullDamageIsACritButFourIsNot()
        {
            var b = T.Fleet(out var flag, out _);
            flag.Shields[ArcIndex.Fore] = 0;
            b.DealDamage(flag, 7, T.At(5, 3), new DamageFlags(), null); // 7 - 3 armor = 4
            Assert.IsFalse(T.Logged(b, EventKind.Critical));
            b.DealDamage(flag, 8, T.At(5, 3), new DamageFlags(), null); // 8 - 3 = 5
            Assert.IsTrue(T.Logged(b, EventKind.Critical));
        }

        [Test]
        public void CritTableCoversAllSixResults()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (ulong seed = 1; seed < 200 && seen.Count < 4; seed++)
            {
                var b = T.Fleet(out var flag, out _, seed);
                flag.Shields[ArcIndex.Fore] = 0;
                b.DealDamage(flag, 9, T.At(5, 3), new DamageFlags(), null);
                if (b.Deck.Discard.Any()) seen.Add(b.Deck.Discard[0].Def.Id);
                else if (flag.Speed == 0 && flag.NoBurnNextTurn) seen.Add("engine");
            }
            CollectionAssert.AreEquivalent(new[] { "jammed_mount", "fire_on_deck", "hull_breach", "engine" }, seen);
        }

        [Test]
        public void EngineHitBlocksBurnNextTurn()
        {
            var b = T.Fleet(out var flag, out _);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            flag.NoBurnNextTurn = true;
            b.EndTurn();
            var burn = T.Give(b, PaperCards.Burn, flag);
            Assert.IsNotNull(b.PlayCard(burn.Id, PlayTarget.At(flag.Id)));
            b.EndTurn();
            burn = b.Deck.Hand.Contains(burn) ? burn : T.Give(b, PaperCards.Burn, flag);
            Assert.IsNull(b.PlayCard(burn.Id, PlayTarget.At(flag.Id)));
        }

        [Test]
        public void DestroyedShipTakesItsCardsWithIt()
        {
            var b = Scenarios.InterceptAtTheRing(5);
            b.Start();
            var escort = b.Units.First(u => u.Name == "Blade");
            b.DealDamage(escort, 100, escort.Pos.Neighbor(HexDirection.E), new DamageFlags(), null);
            Assert.IsFalse(escort.Alive);
            Assert.AreEqual(10, b.Deck.Count);
            Assert.IsFalse(b.Deck.AllPiles().SelectMany(p => p).Any(c => c.SourceId == escort.Id));
            Assert.AreEqual(BattleOutcome.Ongoing, b.Outcome, "losing a ship is not losing the battle");
        }

        [Test]
        public void HeatOverCapAddsFireOnDeck()
        {
            var b = T.Fleet(out var flag, out _);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            flag.Heat = 13; // still over cap 10 after venting
            b.EndTurn();
            Assert.AreEqual(1, b.Deck.AllPiles().SelectMany(p => p).Count(c => c.Def == PaperCards.FireOnDeck));
            Assert.AreEqual(30, flag.Hull);
            Assert.AreEqual(13 - flag.Vent, flag.Heat);
        }

        [Test]
        public void HeatAtCapPlusFourCausesReactorStrain()
        {
            var b = T.Fleet(out var flag, out _);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            flag.Heat = 16; // vents to 14 = cap + 4
            b.EndTurn();
            Assert.AreEqual(24, flag.Hull, "6 hull damage ignoring shields and armor");
            Assert.AreEqual(10, flag.Heat, "heat resets to cap");
            Assert.IsTrue(T.Logged(b, EventKind.ReactorStrain));
        }

        [Test]
        public void ZeroOrdinalsMakesEveryCardCostOneMore()
        {
            var b = T.Fleet(out var flag, out _);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            var burn = T.Give(b, PaperCards.Burn, flag);
            b.Ordinals = 0;
            Assert.AreEqual(1, b.CostOf(burn));
            b.Command = 0;
            Assert.IsNotNull(b.PlayCard(burn.Id, PlayTarget.At(flag.Id)));
        }

        [Test]
        public void VictoryWhenEveryEnemyCapitalShipIsDestroyed()
        {
            var b = T.Fleet(out _, out _);
            var e = b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.AddUnit(PaperUnits.Gunship, Side.Enemy, 8, 6, HexDirection.W, 0);
            b.Start();
            b.DealDamage(e, 100, e.Pos.Neighbor(HexDirection.W), new DamageFlags(), null);
            Assert.AreEqual(BattleOutcome.Victory, b.Outcome);
            Assert.AreEqual(BattlePhase.Over, b.Phase);
        }

        [Test]
        public void DefeatWhenEveryPlayerCapitalShipIsDestroyed()
        {
            var b = T.Fleet(out var flag, out var escort);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            b.DealDamage(flag, 100, T.At(5, 3), new DamageFlags { Direct = true }, null);
            Assert.AreEqual(BattleOutcome.Ongoing, b.Outcome, "the battle continues without the flagship");
            b.DealDamage(escort, 100, T.At(5, 5), new DamageFlags { Direct = true }, null);
            Assert.AreEqual(BattleOutcome.Defeat, b.Outcome);
        }
    }
}
