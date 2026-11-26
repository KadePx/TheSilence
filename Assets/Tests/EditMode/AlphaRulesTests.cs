using System.Linq;
using NUnit.Framework;
using Silence.Core.Battle;
using Silence.Core.Hex;

namespace Silence.Tests
{
    /// <summary>Rules added for the alpha: escort helm, hangar stock, module cards, enemy course trim.</summary>
    public class AlphaRulesTests
    {
        [Test]
        public void ComeAboutTurnsTheEscortEvenWithTheFlagshipGone()
        {
            var b = T.Fleet(out var flag, out var escort);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            b.Destroy(flag);
            var c = T.Give(b, PaperCards.ComeAbout, escort);
            Assert.AreEqual(0, b.CostOf(c));
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.Turning(-1, 2)), "Speed 0: may turn twice");
            Assert.AreEqual(HexDirection.NW, escort.Facing);
        }

        [Test]
        public void ComeAboutTurnsOnlyOnceAtSpeed()
        {
            var b = T.Fleet(out _, out var escort);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            escort.Speed = 2;
            b.Start();
            var c = T.Give(b, PaperCards.ComeAbout, escort);
            Assert.IsNotNull(b.PlayCard(c.Id, PlayTarget.Turning(-1, 2)));
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.Turning(-1, -1)));
            Assert.AreEqual(HexDirection.SE, escort.Facing);
        }

        [Test]
        public void StarterDeckLetsTheEscortSteer()
        {
            Assert.IsTrue(PaperCards.Starter.Any(s => s.card == PaperCards.ComeAbout && s.source == 1));
        }

        [Test]
        public void LaunchDartsDrawsOnTheHangar()
        {
            var b = T.Fleet(out var flag, out _);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.DartsInHangar = 3;
            b.Start();
            Assert.IsNull(b.PlayCard(T.Give(b, PaperCards.LaunchDarts, flag).Id, PlayTarget.None));
            Assert.AreEqual(1, b.DartsInHangar);
            Assert.AreEqual(2, b.PlayerCraftCount());
            b.Command = 3;
            Assert.IsNull(b.PlayCard(T.Give(b, PaperCards.LaunchDarts, flag).Id, PlayTarget.None));
            Assert.AreEqual(0, b.DartsInHangar);
            Assert.AreEqual(3, b.PlayerCraftCount(), "only one dart was left aboard");
            b.Command = 3;
            Assert.IsNotNull(b.PlayCard(T.Give(b, PaperCards.LaunchDarts, flag).Id, PlayTarget.None), "empty hangar");
        }

        [Test]
        public void ShortLanceTakesTheOvercharge()
        {
            var b = T.Fleet(out var flag, out _);
            var e = b.AddUnit(T.Dummy(30), Side.Enemy, 4, 3, HexDirection.W, 0);
            b.Start();
            b.Command = 5;
            Assert.IsNull(b.PlayCard(T.Give(b, PaperCards.OverchargeLances, flag).Id, PlayTarget.None));
            Assert.IsNull(b.PlayCard(T.Give(b, PaperCards.ShortLance, flag).Id, PlayTarget.At(e.Id)));
            Assert.AreEqual(30 - 10, e.Hull);
            Assert.AreEqual(5, flag.Heat);
        }

        [Test]
        public void RakingBeamHitsEveryEnemyAbeam()
        {
            var b = T.Fleet(out var flag, out _);
            var port = b.AddUnit(T.Dummy(), Side.Enemy, 0, 0, HexDirection.W, 0);
            port.Pos = new HexCoord(flag.Pos.Q, flag.Pos.R - 2);
            var starboard = b.AddUnit(T.Dummy(), Side.Enemy, 0, 0, HexDirection.W, 0);
            starboard.Pos = new HexCoord(flag.Pos.Q, flag.Pos.R + 1);
            var ahead = b.AddUnit(T.Dummy(), Side.Enemy, 5, 3, HexDirection.W, 0);
            b.Start();
            Assert.IsNull(b.PlayCard(T.Give(b, PaperCards.RakingBeam, flag).Id, PlayTarget.None));
            Assert.AreEqual(16, port.Hull);
            Assert.AreEqual(16, starboard.Hull);
            Assert.AreEqual(20, ahead.Hull, "the fore arc is not swept");
            Assert.AreEqual(3, flag.Heat);
        }

        [Test]
        public void RakingBeamNeedsSomethingToHit()
        {
            var b = T.Fleet(out var flag, out _);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 3, HexDirection.W, 0);
            b.Start();
            Assert.IsNotNull(b.PlayCard(T.Give(b, PaperCards.RakingBeam, flag).Id, PlayTarget.None));
            Assert.AreEqual(3, b.Command);
        }

        [Test]
        public void PurgeTheirDecksIgnoresShieldsAndArmor()
        {
            var b = T.Fleet(out var flag, out _);
            var e = b.AddUnit(T.Dummy(20, 5, 3), Side.Enemy, 4, 3, HexDirection.W, 0);
            var far = b.AddUnit(T.Dummy(), Side.Enemy, 8, 3, HexDirection.W, 0);
            b.Start();
            var c = T.Give(b, PaperCards.PurgeTheirDecks, flag);
            Assert.IsNotNull(b.PlayCard(c.Id, PlayTarget.At(far.Id)), "out of reach");
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(e.Id)));
            Assert.AreEqual(14, e.Hull);
            Assert.AreEqual(22, b.Ordinals);
        }

        [Test]
        public void HoldTheLineShieldsTheWholeFleet()
        {
            var b = T.Fleet(out var flag, out var escort);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            Assert.IsNull(b.PlayCard(T.Give(b, PaperCards.HoldTheLine, flag).Id, PlayTarget.None));
            Assert.AreEqual(7, flag.Shields[ArcIndex.Aft]);
            Assert.AreEqual(5, escort.Shields[ArcIndex.Fore]);
        }

        [Test]
        public void ScryTheVoidDrawsTwoAndExhausts()
        {
            var b = Scenarios.InterceptAtTheRing(3);
            b.Start();
            var c = T.Give(b, PaperCards.ScryTheVoid, b.Flagship);
            int hand = b.Deck.Hand.Count;
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.None));
            Assert.AreEqual(hand + 1, b.Deck.Hand.Count);
            Assert.Contains(c, b.Deck.Exhausted);
        }

        [Test]
        public void SealTheDecksRepairsForOrdinals()
        {
            var b = T.Fleet(out var flag, out _);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            var c = T.Give(b, PaperCards.SealTheDecks, flag);
            Assert.IsNotNull(b.PlayCard(c.Id, PlayTarget.At(flag.Id)), "nothing to repair");
            flag.Hull = 20;
            Assert.IsNull(b.PlayCard(c.Id, PlayTarget.At(flag.Id)));
            Assert.AreEqual(25, flag.Hull);
            Assert.AreEqual(22, b.Ordinals);
        }

        [Test]
        public void EnemiesPreferTheFlagshipWhenItIsNearlyAsClose()
        {
            var b = T.Fleet(out var flag, out var escort);
            b.Start();
            var from = escort.Pos.Neighbor(HexDirection.SE);
            Assert.AreEqual(1, from.DistanceTo(escort.Pos));
            Assert.AreEqual(3, from.DistanceTo(flag.Pos));
            Assert.AreEqual(escort, b.NearestPlayerCapital(from), "two hexes further is too far");
            Assert.AreEqual(flag, b.NearestPlayerCapital(T.At(2, 4)), "equidistant: the flagship");
        }

        [Test]
        public void EnemyTrimsItsCourseToKeepItsGunsBearing()
        {
            var b = T.Fleet(out var flag, out _);
            var f = b.AddUnit(PaperUnits.Frigate, Side.Enemy, 0, 0, HexDirection.E, 2);
            f.Pos = new HexCoord(flag.Pos.Q, flag.Pos.R - 2);
            f.CycleIndex = 1; // Broadside, then trim
            b.Start();
            b.EndTurn();
            Assert.IsTrue(b.Log.Any(e => e.Kind == EventKind.SpeedChanged && e.UnitId == f.Id) ||
                          b.Log.Any(e => e.Kind == EventKind.Turned && e.UnitId == f.Id), "it adjusts rather than sailing off");
            Assert.LessOrEqual(f.Speed, 2);
        }

        [Test]
        public void ForecastSaysWhetherABroadsideWouldLand()
        {
            var b = T.Fleet(out var flag, out _);
            var f = b.AddUnit(PaperUnits.Frigate, Side.Enemy, 0, 0, HexDirection.E, 0);
            f.Pos = new HexCoord(flag.Pos.Q, flag.Pos.R - 2);
            f.CycleIndex = 1;
            b.Start();
            var intent = b.Intents.First(i => i.UnitId == f.Id);
            Assert.IsTrue(b.ForecastHit(intent));
            flag.Speed = 3; // will drift out of the side arc
            Assert.IsFalse(b.ForecastHit(intent));
        }

        [Test]
        public void SkiffFightsWithItsForeGuns()
        {
            var b = T.Fleet(out var flag, out _);
            var s = b.AddUnit(PaperUnits.Skiff, Side.Enemy, 5, 3, HexDirection.W, 0);
            s.CycleIndex = 1;
            b.Start();
            var intent = b.Intents.First(i => i.UnitId == s.Id);
            Assert.AreEqual(IntentKind.Broadside, intent.Kind);
            Assert.AreEqual("Fore guns", intent.Label);
            Assert.AreEqual(Arc.Fore, intent.Arcs);
            b.EndTurn();
            Assert.AreEqual(30, flag.Hull, "5 damage: 4 fore shields and 3 armor soak it");
            Assert.AreEqual(1, flag.Shields[ArcIndex.Fore], "shields stripped, regen 1");
        }

        [Test]
        public void LibrariesKnowEveryUnitAndCard()
        {
            foreach (var id in new[] { "flagship", "escort", "dart", "frigate", "gunship", "carrier", "skiff", "tender", "venn", "torpedo" })
                Assert.IsNotEmpty(UnitLibrary.Get(id).Id, id);
            foreach (var c in PaperCards.Everything) Assert.AreSame(c, PaperCards.Get(c.Id));
            Assert.Throws<System.ArgumentException>(() => PaperCards.Get("nope"));
        }

        [Test]
        public void BossCycleIncludesADodgeableMassDriver()
        {
            var b = T.Fleet(out var flag, out _);
            var v = b.AddUnit(PaperUnits.VennFlagship, Side.Enemy, 7, 3, HexDirection.W, 0);
            v.CycleIndex = 1;
            b.Start();
            var intent = b.Intents.First(i => i.UnitId == v.Id);
            Assert.AreEqual("Mass Driver", intent.Label);
            Assert.IsTrue(b.ForecastHit(intent));
            b.EndTurn();
            Assert.AreEqual(30 - (12 - 4 - 3), flag.Hull);
        }
    }
}
