using System.Linq;
using NUnit.Framework;
using Silence.Core.Battle;
using Silence.Core.Hex;

namespace Silence.Tests
{
    /// <summary>Sprint task 5: the deck, and one test per paper-prototype card (20 cards, 18 distinct, plus 3 junk).</summary>
    public class CardTests
    {
        Battle b;
        Unit flag, escort, foe;

        /// <summary>Flagship (2,3) facing E; escort (2,5) facing E; a plain enemy 3 hexes ahead of the flagship at (5,3).</summary>
        [SetUp]
        public void SetUp()
        {
            b = T.Fleet(out flag, out escort);
            foe = b.AddUnit(T.Dummy(), Side.Enemy, 5, 3, HexDirection.W, 0);
            b.Start();
            b.Command = 10;
        }

        // ---------------------------------------------------------------- deck

        [Test]
        public void DiscardIsShuffledBackWhenTheDrawPileRunsOut()
        {
            for (int i = 0; i < 3; i++) b.AddCard(PaperCards.Burn, flag.Id);
            b.DrawCards(3);
            b.Deck.Discard.AddRange(b.Deck.Hand);
            b.Deck.Hand.Clear();
            b.DrawCards(2);
            Assert.AreEqual(2, b.Deck.Hand.Count);
            Assert.AreEqual(1, b.Deck.DrawPile.Count);
            Assert.IsTrue(T.Logged(b, EventKind.Shuffled));
        }

        [Test]
        public void AFailedPlayChangesNothing()
        {
            var lance = T.Give(b, PaperCards.LanceVolley, flag);
            b.Command = 1;
            Assert.IsNotNull(b.PlayCard(lance.Id, PlayTarget.At(foe.Id)));
            Assert.AreEqual(1, b.Command);
            Assert.AreEqual(20, foe.Hull);
            Assert.Contains(lance, b.Deck.Hand);
        }

        [Test]
        public void CardsOfADestroyedShipCannotBePlayed()
        {
            var guns = T.Give(b, PaperCards.EscortGuns, escort);
            escort.Alive = false;
            Assert.IsNotNull(b.PlayCard(guns.Id, PlayTarget.At(foe.Id)));
        }

        [Test]
        public void EveryPaperCardIsDefined()
        {
            Assert.AreEqual(10, PaperCards.Starter.Length);
            Assert.AreEqual(10, PaperCards.Rewards.Length);
            Assert.AreEqual(18, PaperCards.All().Distinct().Count());
        }

        // ---------------------------------------------------------------- starter

        [Test]
        public void LanceVolley()
        {
            var c = T.Give(b, PaperCards.LanceVolley, flag);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(foe.Id)));
            Assert.AreEqual(11, foe.Hull);
            Assert.AreEqual(3, flag.Heat);
            Assert.AreEqual(8, b.Command);
            Assert.Contains(c, b.Deck.Discard);
        }

        [Test]
        public void LanceVolleyExpendAddsSixAndIgnoresShields()
        {
            var shielded = b.AddUnit(T.Dummy(30, shields: 5), Side.Enemy, 6, 3, HexDirection.W, 0);
            foe.Alive = false;
            var c = T.Give(b, PaperCards.LanceVolley, flag);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(shielded.Id, expend: true)));
            Assert.AreEqual(15, shielded.Hull);
            Assert.AreEqual(21, b.Ordinals);
        }

        [Test]
        public void LanceVolleyNeedsFireArcAndRange()
        {
            var behind = b.AddUnit(T.Dummy(), Side.Enemy, 0, 3, HexDirection.E, 0);
            var far = b.AddUnit(T.Dummy(), Side.Enemy, 7, 3, HexDirection.W, 0);
            var c = T.Give(b, PaperCards.LanceVolley, flag);
            Assert.IsNotNull(b.PlayCard(c.Id, PlayTarget.At(behind.Id)), "aft is not the fore arc");
            Assert.IsNotNull(b.PlayCard(c.Id, PlayTarget.At(far.Id)), "range 4");
        }

        [Test]
        public void BroadsideBatteryHitsTheSideArcs()
        {
            var side = b.AddUnit(T.Dummy(), Side.Enemy, 0, 0, HexDirection.W, 0);
            side.Pos = flag.Pos.Neighbor(HexDirection.NE).Neighbor(HexDirection.NW);
            var c = T.Give(b, PaperCards.BroadsideBattery, flag);
            Assert.IsNotNull(b.PlayCard(c.Id, PlayTarget.At(foe.Id)), "fore is not a side arc");
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(side.Id)));
            Assert.AreEqual(16, side.Hull);
            Assert.AreEqual(1, flag.Heat);
        }

        [Test]
        public void Burn()
        {
            var c = T.Give(b, PaperCards.Burn, flag);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(flag.Id)));
            Assert.AreEqual(1, flag.Speed);
            Assert.AreEqual(1, flag.Heat);
            flag.Speed = 3;
            var c2 = T.Give(b, PaperCards.Burn, flag);
            Assert.IsNotNull(b.PlayCard(c2.Id, PlayTarget.At(flag.Id)), "max speed 3");
        }

        [Test]
        public void HardAbout()
        {
            var c = T.Give(b, PaperCards.HardAbout, flag);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.Turning(flag.Id, 2)), "twice at speed 0");
            Assert.AreEqual(HexDirection.NW, flag.Facing);
            flag.Speed = 2;
            var c2 = T.Give(b, PaperCards.HardAbout, flag);
            Assert.IsNotNull(b.PlayCard(c2.Id, PlayTarget.Turning(flag.Id, -2)), "only once at speed 2");
            Assert.IsNull(b.PlayCard(c2.Id, PlayTarget.Turning(flag.Id, -1)));
            Assert.AreEqual(HexDirection.NE, flag.Facing);
        }

        [Test]
        public void BraceLastsUntilTheEndStep()
        {
            var c = T.Give(b, PaperCards.Brace, flag);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(flag.Id)));
            Assert.IsTrue(flag.Shields.All(s => s == 8));
            b.EndTurn();
            Assert.IsTrue(flag.Shields.All(s => s == 4));
        }

        [Test]
        public void EscortGunsFireFromTheEscort()
        {
            var ahead = b.AddUnit(T.Dummy(), Side.Enemy, 4, 5, HexDirection.W, 0);
            var c = T.Give(b, PaperCards.EscortGuns, escort);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(ahead.Id)));
            Assert.AreEqual(15, ahead.Hull);
            Assert.AreEqual(2, escort.Heat);
            Assert.AreEqual(0, flag.Heat);
        }

        [Test]
        public void LaunchDartsUpToFourAndTheyActThisTurn()
        {
            var c1 = T.Give(b, PaperCards.LaunchDarts, flag);
            var c2 = T.Give(b, PaperCards.LaunchDarts, flag);
            var c3 = T.Give(b, PaperCards.LaunchDarts, flag);
            Assert.IsNull(b.PlayCard(c1.Id, PlayTarget.None));
            Assert.IsNull(b.PlayCard(c2.Id, PlayTarget.None));
            Assert.AreEqual(4, b.PlayerCraftCount());
            Assert.IsNotNull(b.PlayCard(c3.Id, PlayTarget.None), "max 4 out");

            var dart = b.LiveUnits(Side.Player).First(u => u.Kind == UnitKind.Craft);
            var spot = foe.Pos.Neighbor(HexDirection.W);
            Assert.IsNull(b.CommandCraft(dart.Id, spot, foe.Id));
            Assert.AreEqual(18, foe.Hull);
            Assert.IsNotNull(b.CommandCraft(dart.Id, spot), "one action per turn");
        }

        [Test]
        public void VentPlasmaExhausts()
        {
            flag.Heat = 6;
            var c = T.Give(b, PaperCards.VentPlasma, flag);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(flag.Id)));
            Assert.AreEqual(2, flag.Heat);
            Assert.Contains(c, b.Deck.Exhausted);
        }

        // ---------------------------------------------------------------- rewards

        [Test]
        public void OverchargeLances()
        {
            var o = T.Give(b, PaperCards.OverchargeLances, flag);
            var l = T.Give(b, PaperCards.LanceVolley, flag);
            Assert.IsNull(b.PlayCard(o.Id, PlayTarget.None));
            Assert.IsNull(b.PlayCard(l.Id, PlayTarget.At(foe.Id)));
            Assert.AreEqual(6, foe.Hull, "9 + 5");
            Assert.AreEqual(6, flag.Heat, "3 + 3");
            Assert.AreEqual(0, flag.LanceBonus, "only the next volley");
        }

        [Test]
        public void DecimationOrder()
        {
            for (int i = 0; i < 3; i++) b.AddCard(PaperCards.Burn, flag.Id);
            var c = T.Give(b, PaperCards.DecimationOrder, flag);
            int hand = b.Deck.Hand.Count;
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.None));
            Assert.AreEqual(19, b.Ordinals);
            Assert.AreEqual(12, b.Command);
            Assert.AreEqual(hand - 1 + 3, b.Deck.Hand.Count);
            Assert.Contains(c, b.Deck.Exhausted);
            b.Ordinals = 4;
            var c2 = T.Give(b, PaperCards.DecimationOrder, flag);
            Assert.IsNotNull(b.PlayCard(c2.Id, PlayTarget.None), "needs 5 Ordinals");
        }

        [Test]
        public void SacrificialRam()
        {
            escort.Speed = 1;
            var target = b.AddUnit(T.Dummy(), Side.Enemy, 5, 5, HexDirection.W, 0);
            var guns = b.AddCard(PaperCards.EscortGuns, escort.Id);
            var c = T.Give(b, PaperCards.SacrificialRam, escort);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(target.Id)));
            Assert.IsFalse(target.Alive);
            Assert.IsFalse(escort.Alive);
            Assert.IsFalse(b.Deck.AllPiles().Any(p => p.Contains(guns)), "the escort's cards leave with it");
        }

        [Test]
        public void SacrificialRamNeedsAClearStraightPath()
        {
            escort.Speed = 1;
            var tooFar = b.AddUnit(T.Dummy(), Side.Enemy, 6, 5, HexDirection.W, 0);
            var c = T.Give(b, PaperCards.SacrificialRam, escort);
            Assert.IsNotNull(b.PlayCard(c.Id, PlayTarget.At(tooFar.Id)), "Speed 1 + 2 = 3 hexes");
        }

        [Test]
        public void BoardingAction()
        {
            var frigate = b.AddUnit(PaperUnits.Frigate, Side.Enemy, 4, 3, HexDirection.W, 0);
            b.EndTurn();
            b.Command = 10;
            Assert.IsTrue(b.Intents.Any(i => i.UnitId == frigate.Id));
            var c = T.Give(b, PaperCards.BoardingAction, flag);
            Assert.IsNotNull(b.PlayCard(c.Id, PlayTarget.At(foe.Id)), "foe is 3 hexes away");
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(frigate.Id)));
            Assert.IsTrue(b.Intents.First(i => i.UnitId == frigate.Id).Cancelled);
            Assert.AreEqual(20, b.Ordinals);
        }

        [Test]
        public void WaymindTargeting()
        {
            var armored = b.AddUnit(T.Dummy(20, armor: 3), Side.Enemy, 6, 3, HexDirection.W, 0);
            foe.Alive = false;
            var w = T.Give(b, PaperCards.WaymindTargeting, flag);
            var l = T.Give(b, PaperCards.LanceVolley, flag);
            Assert.IsNull(b.PlayCard(w.Id, PlayTarget.None));
            Assert.IsTrue(b.WaymindPrimed);
            Assert.IsNull(b.PlayCard(l.Id, PlayTarget.At(armored.Id)));
            Assert.AreEqual(11, armored.Hull, "armor ignored");
            Assert.IsFalse(b.WaymindPrimed);
        }

        [Test]
        public void WaymindCritsOnAnyHullDamage()
        {
            var side = b.AddUnit(T.Dummy(), Side.Enemy, 0, 0, HexDirection.W, 0);
            side.Pos = flag.Pos.Neighbor(HexDirection.NE).Neighbor(HexDirection.NW);
            T.Give(b, PaperCards.WaymindTargeting, flag);
            var bb = T.Give(b, PaperCards.BroadsideBattery, flag);
            b.PlayCard(b.Deck.Hand.First(c => c.Def == PaperCards.WaymindTargeting).Id, PlayTarget.None);
            Assert.IsNull(b.PlayCard(bb.Id, PlayTarget.At(side.Id)));
            Assert.IsTrue(T.Logged(b, EventKind.Critical), "4 hull damage crits");
        }

        [Test]
        public void PrecisionEdict()
        {
            var e = T.Give(b, PaperCards.PrecisionEdict, flag);
            var l = T.Give(b, PaperCards.LanceVolley, flag);
            Assert.IsNull(b.PlayCard(e.Id, PlayTarget.At(foe.Id)));
            Assert.IsNull(b.PlayCard(l.Id, PlayTarget.At(foe.Id)));
            Assert.AreEqual(8, foe.Hull, "9 + 3");
            b.EndTurn();
            Assert.AreEqual(0, foe.MarkBonus, "the mark lasts one turn");
        }

        [Test]
        public void ColdDiscipline()
        {
            flag.Heat = 5;
            escort.Heat = 2;
            b.AddCard(PaperCards.Burn, flag.Id);
            var c = T.Give(b, PaperCards.ColdDiscipline, flag);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.None));
            Assert.AreEqual(2, flag.Heat);
            Assert.AreEqual(0, escort.Heat);
            Assert.AreEqual(1, b.Deck.Hand.Count, "drew 1");
        }

        [Test]
        public void ScreeningManeuverTakesTheNextAttack()
        {
            var bb = new Battle(1);
            var f = bb.AddUnit(PaperUnits.LanceCruiser, Side.Player, 2, 3, HexDirection.E, 0, flagship: true);
            var esc = bb.AddUnit(PaperUnits.Blade, Side.Player, 1, 3, HexDirection.NE, 0);
            var frigate = bb.AddUnit(PaperUnits.Frigate, Side.Enemy, 0, 0, HexDirection.E, 0);
            frigate.Pos = new HexCoord(f.Pos.Q, f.Pos.R - 2);
            bb.Start();
            Assert.AreEqual(f.Id, bb.Intents[0].TargetId);
            var c = T.Give(bb, PaperCards.ScreeningManeuver, esc);
            Assert.IsNull(bb.PlayCard(c.Id, PlayTarget.Turning(-1, -1)));
            Assert.AreEqual(HexDirection.E, esc.Facing);
            Assert.IsTrue(esc.Guarding);
            bb.EndTurn();
            Assert.AreEqual(30, f.Hull);
            Assert.AreEqual(4, f.Shields.Sum() / 4, "flagship untouched");
            Assert.IsTrue(bb.Log.Any(e => e.Kind == EventKind.Attack && e.OtherId == esc.Id));
        }

        [Test]
        public void DartSortie()
        {
            var launch = T.Give(b, PaperCards.LaunchDarts, flag);
            var sortie = T.Give(b, PaperCards.DartSortie, flag);
            Assert.IsNull(b.PlayCard(launch.Id, PlayTarget.None));
            Assert.IsNull(b.PlayCard(sortie.Id, PlayTarget.None));
            var dart = b.LiveUnits(Side.Player).First(u => u.Kind == UnitKind.Craft);
            Assert.AreEqual(6, dart.Move + dart.CraftBonusMove);
            Assert.IsNull(b.CommandCraft(dart.Id, foe.Pos.Neighbor(HexDirection.W), foe.Id));
            Assert.AreEqual(16, foe.Hull, "two attacks of 2");
        }

        [Test]
        public void FlakLattice()
        {
            var g1 = b.AddUnit(PaperUnits.Gunship, Side.Enemy, 0, 0, HexDirection.W, 0);
            var g2 = b.AddUnit(PaperUnits.Gunship, Side.Enemy, 0, 0, HexDirection.W, 0);
            var g3 = b.AddUnit(PaperUnits.Gunship, Side.Enemy, 0, 0, HexDirection.W, 0);
            var g4 = b.AddUnit(PaperUnits.Gunship, Side.Enemy, 0, 0, HexDirection.W, 0);
            g1.Pos = escort.Pos.Neighbor(HexDirection.E);
            g2.Pos = escort.Pos.Neighbor(HexDirection.NE);
            g3.Pos = escort.Pos.Neighbor(HexDirection.SE);
            g4.Pos = escort.Pos.Neighbor(HexDirection.W);
            var c = T.Give(b, PaperCards.FlakLattice, escort);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.None));
            Assert.AreEqual(1, new[] { g1, g2, g3, g4 }.Count(g => g.Alive), "up to 3");
            Assert.IsTrue(foe.Alive, "capital ships are not flak targets");
        }

        // ---------------------------------------------------------------- junk

        [Test]
        public void FireOnDeckHeatsTheFlagshipOrCostsOneToPutOut()
        {
            T.Give(b, PaperCards.FireOnDeck, null);
            b.EndTurn();
            Assert.AreEqual(2, flag.Heat);

            var c = b.Deck.Hand.First(x => x.Def == PaperCards.FireOnDeck);
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.None));
            Assert.AreEqual(2, b.Command);
            Assert.Contains(c, b.Deck.Exhausted);
        }

        [Test]
        public void HullBreachIsUnplayableAndCostsOrdinals()
        {
            var c = T.Give(b, PaperCards.HullBreach, null);
            Assert.IsNotNull(b.PlayCard(c.Id, PlayTarget.None));
            b.EndTurn();
            Assert.AreEqual(22, b.Ordinals);
        }

        [Test]
        public void JammedMountRaisesAWeaponsCost()
        {
            var lance = T.Give(b, PaperCards.LanceVolley, flag);
            var jam = T.Give(b, PaperCards.JammedMount, null);
            Assert.AreEqual(3, b.CostOf(lance));
            Assert.IsNotNull(b.PlayCard(jam.Id, PlayTarget.None), "unplayable");
            b.Deck.Hand.Remove(jam); // keep it from being drawn again next turn
            b.EndTurn();
            Assert.AreEqual(0, lance.TempCost, "only this turn");
        }
    }
}
