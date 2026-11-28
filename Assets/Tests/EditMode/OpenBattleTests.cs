using System.Linq;
using NUnit.Framework;
using Silence.Core.Open;

namespace Silence.Tests
{
    /// <summary>The gridless prototype: arcs, range, sight, drift, turning and replay from a seed.</summary>
    public class OpenBattleTests
    {
        static OpenShip Target(int hull = 20, int shields = 0) => new OpenShip
        {
            Name = "Target", Side = OpenSide.Enemy, Radius = 500, Hull = hull, MaxHull = hull, Shields = shields, MaxShields = shields,
            Armor = new[] { 3, 1, 0, 1 }, MaxSpeed = 2, Agility = 45,
        };

        static OpenBattle Duel(out OpenShip flag, out OpenShip target, int tx, int ty, int theading = 180)
        {
            var b = new OpenBattle(1);
            flag = b.Add(OpenContent.Flagship(), 0, 0, 0, 0);
            target = b.Add(Target(), tx, ty, theading, 0);
            return b;
        }

        static OpenCard Give(OpenBattle b, OpenCard card, OpenShip source)
        {
            b.AddCard(card, source);
            b.DrawPile.Remove(card);
            b.Hand.Add(card);
            return card;
        }

        [Test]
        public void SineTableIsSymmetric()
        {
            Assert.AreEqual(0, OpenMath.Sin(0));
            Assert.AreEqual(1000, OpenMath.Sin(90));
            Assert.AreEqual(-1000, OpenMath.Sin(270));
            Assert.AreEqual(500, OpenMath.Sin(150));
            Assert.AreEqual(-866, OpenMath.Cos(150));
            Assert.AreEqual(1000, OpenMath.Cos(360));
        }

        [Test]
        public void ArcsFollowTheHeading()
        {
            var at = new Vec(0, 0);
            Assert.AreEqual(OpenArc.Fore, OpenMath.ArcOf(at, 0, new Vec(3000, 500)));
            Assert.AreEqual(OpenArc.Aft, OpenMath.ArcOf(at, 0, new Vec(-3000, -500)));
            Assert.AreEqual(OpenArc.Starboard, OpenMath.ArcOf(at, 0, new Vec(0, 3000)), "Y grows toward the camera: the ship's right when heading 0");
            Assert.AreEqual(OpenArc.Port, OpenMath.ArcOf(at, 0, new Vec(0, -3000)));
            Assert.AreEqual(OpenArc.Fore, OpenMath.ArcOf(at, 90, new Vec(0, 3000)));
            Assert.AreEqual(OpenArc.Starboard, OpenMath.ArcOf(at, 90, new Vec(-3000, 0)));
            Assert.AreEqual(OpenArc.Starboard, OpenMath.ArcOf(at, 0, new Vec(3000, 2000)), "34 degrees off the bow is outside the fore arc");
            Assert.AreEqual(45, OpenMath.Bearing(at, new Vec(3000, 2500)), "bearings come in eight points");
        }

        [Test]
        public void GunsNeedArcRangeAndSight()
        {
            var b = Duel(out var flag, out var target, 0, 4000);
            var side = OpenContent.DeckGuns().Weapon;
            var fore = OpenContent.MassDriver().Weapon;
            Assert.AreEqual(Shot.Clear, b.Check(flag, side, target));
            Assert.AreEqual(Shot.OutOfArc, b.Check(flag, fore, target));
            target.Pos = new Vec(0, 5600);
            Assert.AreEqual(Shot.OutOfRange, b.Check(flag, side, target), "range is measured to the edge of the base: 5600 - 500 is over 5000");
            target.Pos = new Vec(0, 5500);
            Assert.AreEqual(Shot.Clear, b.Check(flag, side, target));
            b.Rocks.Add(new OpenRock { Name = "Wreck", Pos = new Vec(300, 2500), Radius = 600 });
            Assert.AreEqual(Shot.Blocked, b.Check(flag, side, target));
            target.Pos = new Vec(3000, 3000);
            Assert.AreEqual(Shot.Clear, b.Check(flag, side, target), "a step to the side clears the wreck");
        }

        [Test]
        public void ARefusedCardChangesNothing()
        {
            var b = Duel(out var flag, out var target, 6500, 0);
            b.Start();
            var guns = Give(b, OpenContent.DeckGuns(), flag);
            Assert.AreEqual("Not in arc.", b.Play(guns.Id, OpenChoice.At(target.Id)));
            Assert.AreEqual(4, b.Command);
            Assert.AreEqual(0, flag.Heat);
            Assert.IsTrue(b.Hand.Contains(guns));
        }

        [Test]
        public void ShieldsThenTheArmorOfTheArcHit()
        {
            var b = Duel(out var flag, out var target, 4000, 0, theading: 180);
            b.Start();
            target.Shields = 2;
            var driver = Give(b, OpenContent.MassDriver(), flag);
            Assert.IsNull(b.Play(driver.Id, OpenChoice.At(target.Id)));
            Assert.AreEqual(18, target.Hull, "7 - 2 shields - 3 fore armor");
            var e = b.Log.Last(x => x.Kind == OpenEventKind.Fired);
            Assert.AreEqual(OpenArc.Fore, e.Arc);
            Assert.AreEqual(2, e.Absorbed);
            Assert.AreEqual(3, e.Armored);

            target.Heading = 0; // now the same shot comes in from astern
            flag.Heat = 0;
            b.Command = 4;
            var again = Give(b, OpenContent.MassDriver(), flag);
            Assert.IsNull(b.Play(again.Id, OpenChoice.At(target.Id)));
            Assert.AreEqual(11, target.Hull, "no armor aft: all 7 go through");
        }

        [Test]
        public void ShipsDriftAtTheEndOfTheTurn()
        {
            var b = Duel(out var flag, out var target, 0, -5000);
            flag.Speed = 2;
            b.Start();
            b.EndTurn();
            Assert.AreEqual(new Vec(2000, 0), flag.Pos);
            Assert.AreEqual(2, flag.Speed, "speed is kept");
        }

        [Test]
        public void BurnAddsSpeedAndMovesNow()
        {
            var b = Duel(out var flag, out _, 0, -5000);
            b.Start();
            var burn = Give(b, OpenContent.Burn(), flag);
            Assert.IsNull(b.Play(burn.Id, OpenChoice.None));
            Assert.AreEqual(1, flag.Speed);
            Assert.AreEqual(new Vec(1000, 0), flag.Pos);
            Assert.AreEqual(1, flag.Heat);
        }

        [Test]
        public void RunningIntoAWreckStopsTheShipAndCostsHull()
        {
            var b = Duel(out var flag, out _, 0, -5000);
            b.Rocks.Add(new OpenRock { Name = "Wreck", Pos = new Vec(3000, 0), Radius = 800 });
            flag.Speed = 3;
            b.Start();
            b.EndTurn();
            Assert.AreEqual(new Vec(600, 0), flag.Pos, "stops where the bases touch: 3000 - 800 - 1600");
            Assert.AreEqual(0, flag.Speed);
            Assert.AreEqual(27, flag.Hull);
            Assert.IsTrue(b.Log.Any(e => e.Kind == OpenEventKind.Collision));
        }

        [Test]
        public void TheHelmTurnsWithinAgilityAndTurningBackIsFree()
        {
            var b = Duel(out var flag, out _, 0, -5000);
            b.Start(); // flagship: agility 90, Speed 0 of 3
            Assert.IsNull(b.Helm(flag.Id, 90, 1));
            Assert.AreEqual(90, flag.Heading);
            StringAssert.Contains("can turn 90", b.Helm(flag.Id, 135, 1));
            Assert.AreEqual(90, flag.Heading, "a refused helm changes nothing");
            Assert.IsNull(b.Helm(flag.Id, 270, 1), "measured from where the turn began, so turning back is free");
            Assert.AreEqual(4, b.Command, "the helm costs no command");
        }

        [Test]
        public void AStoppedShipPivotsToAnyHeading()
        {
            var b = Duel(out var flag, out _, 0, -5000);
            b.Start(); // Speed 0
            Assert.IsNull(b.Helm(flag.Id, 180, 0), "stopped: any heading");
            StringAssert.Contains("can turn 90", b.Helm(flag.Id, 180, 1), "getting under way: agility again");
        }

        [Test]
        public void TheHelmChangesSpeedByOneStep()
        {
            var b = Duel(out var flag, out _, 0, -5000);
            flag.Speed = 1;
            b.Start();
            Assert.IsNotNull(b.Helm(flag.Id, 0, 3));
            Assert.IsNull(b.Helm(flag.Id, 0, 2));
            Assert.IsNull(b.Helm(flag.Id, 0, 0));
            Assert.AreEqual(0, flag.Speed);
        }

        [Test]
        public void AtTopSpeedTheHelmTurnsOnePointLess()
        {
            var b = Duel(out var flag, out _, 0, -5000);
            flag.Speed = 2;
            b.Start();
            Assert.IsNotNull(b.Helm(flag.Id, 90, 3), "agility 90 is 45 at top speed");
            Assert.IsNull(b.Helm(flag.Id, 45, 3));
            var hard = Give(b, OpenContent.HardAbout(), flag);
            Assert.IsNull(b.Play(hard.Id, OpenChoice.None));
            Assert.IsNull(b.Helm(flag.Id, 135, 3), "Hard About adds 90 for this turn");
        }

        [Test]
        public void TheHelmResetsEachTurnAndIsOnlyForYourShips()
        {
            var b = Duel(out var flag, out var target, 0, -9000);
            b.Start();
            Assert.IsNull(b.Helm(flag.Id, 90, 1));
            b.EndTurn();
            Assert.IsNull(b.Helm(flag.Id, 180, 1), "next turn it is measured from 90");
            Assert.IsNotNull(b.Helm(target.Id, 0, 0));
        }

        [Test]
        public void BurnPushesPastTheHelmsSpeedStep()
        {
            var b = Duel(out var flag, out _, 0, -5000);
            b.Start();
            Assert.IsNull(b.Helm(flag.Id, 0, 1));
            var burn = Give(b, OpenContent.Burn(), flag);
            Assert.IsNull(b.Play(burn.Id, OpenChoice.None));
            Assert.AreEqual(2, flag.Speed);
            Assert.IsNull(b.Helm(flag.Id, 0, 2), "the band moved up with it");
        }

        [Test]
        public void TheFieldIsTheSameForASeedAndKeepsLanesOpen()
        {
            var a = OpenContent.BreakersYard(5);
            var again = OpenContent.BreakersYard(5);
            var other = OpenContent.BreakersYard(6);
            CollectionAssert.AreEqual(a.Rocks.Select(r => r.Pos).ToList(), again.Rocks.Select(r => r.Pos).ToList());
            CollectionAssert.AreNotEqual(a.Rocks.Select(r => r.Pos).ToList(), other.Rocks.Select(r => r.Pos).ToList());
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var b = OpenContent.BreakersYard(seed);
                Assert.GreaterOrEqual(b.Rocks.Count, 16, $"seed {seed}");
                foreach (var r in b.Rocks)
                {
                    Assert.LessOrEqual(System.Math.Abs(r.Pos.X) + r.Radius, b.HalfWidth);
                    Assert.LessOrEqual(System.Math.Abs(r.Pos.Y) + r.Radius, b.HalfHeight);
                    foreach (var s in b.Ships)
                        Assert.GreaterOrEqual(OpenMath.Dist(r.Pos, s.Pos), r.Radius + s.Radius + OpenContent.StartClearance, $"seed {seed}: rock on a start");
                    foreach (var o in b.Rocks)
                        if (o != r) Assert.GreaterOrEqual(OpenMath.Dist(r.Pos, o.Pos), r.Radius + o.Radius + OpenContent.Lane, $"seed {seed}: no lane");
                }
            }
        }

        [Test]
        public void HeatStopsAnotherShot()
        {
            var b = Duel(out var flag, out var target, 0, 4000);
            b.Start();
            flag.Heat = 5;
            var guns = Give(b, OpenContent.DeckGuns(), flag);
            StringAssert.Contains("too hot", b.Play(guns.Id, OpenChoice.At(target.Id)));
        }

        [Test]
        public void EnemyFiresFromWhereItSatThenMoves()
        {
            var b = new OpenBattle(1);
            var flag = b.Add(OpenContent.Flagship(), 0, 0, 0, 0);
            var escort = b.Add(OpenContent.Escort("Blade escort"), 3500, 0, 180, 1);
            b.Start();
            flag.Shields = 0;
            b.EndTurn();
            Assert.AreEqual(29, flag.Hull, "3 dmg - 2 fore armor");
            var first = b.Log.First(e => e.ShipId == escort.Id && e.Kind != OpenEventKind.TurnStarted);
            Assert.AreEqual(OpenEventKind.Fired, first.Kind, "it fires before it turns, changes speed or drifts");
            Assert.AreEqual(new Vec(3500, 0), first.From, "from where it sat");
            Assert.AreEqual(escort.Speed * 1000, OpenMath.Dist(new Vec(3500, 0), escort.Pos), 5, "then drifts its new Speed");
        }

        [Test]
        public void EnemyBehindAWreckCannotFire()
        {
            var b = new OpenBattle(1);
            var flag = b.Add(OpenContent.Flagship(), 0, 0, 0, 0);
            var escort = b.Add(OpenContent.Escort("Blade escort"), 3900, 0, 180, 0);
            b.Rocks.Add(new OpenRock { Name = "Wreck", Pos = new Vec(2000, 100), Radius = 500 });
            b.Start();
            Assert.IsFalse(OpenAi.Fire(b, escort));
            Assert.AreEqual(30, flag.Hull);
        }

        [Test]
        public void ADestroyedShipTakesItsCardsWithIt()
        {
            var b = OpenContent.BreakersYard(3);
            b.Start();
            var outrider = b.Ships.First(s => s.Name == "Outrider");
            var cruiser = b.Ships.First(s => s.Name == "Verdict cruiser");
            outrider.Hull = 1;
            outrider.Shields = 0;
            b.Hit(cruiser, cruiser.Weapons[0], outrider);
            Assert.IsFalse(outrider.Alive);
            Assert.IsFalse(b.DrawPile.Concat(b.Hand).Concat(b.Discard).Any(c => c.SourceId == outrider.Id));
            Assert.AreEqual(OpenOutcome.Ongoing, b.Outcome);
        }

        [Test]
        public void TheSameSeedReplaysTheSameBattle()
        {
            string Run(ulong seed)
            {
                var b = OpenContent.BreakersYard(seed);
                b.Start();
                for (int i = 0; i < 60 && b.Outcome == OpenOutcome.Ongoing; i++) OpenBot.PlayTurn(b);
                return b.Outcome + " " + b.Turn + " " + string.Join(";", b.Log.Select(e => $"{e.Kind},{e.ShipId},{e.TargetId},{e.Amount},{e.To}"));
            }
            Assert.AreEqual(Run(11), Run(11));
            Assert.AreNotEqual(Run(11), Run(12));
        }

        [Test]
        public void BotBattlesEndAndNoBasesEverOverlap()
        {
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var b = OpenContent.BreakersYard(seed);
                b.Start();
                for (int i = 0; i < 80 && b.Outcome == OpenOutcome.Ongoing; i++)
                {
                    OpenBot.PlayTurn(b);
                    var live = b.Ships.Where(s => s.Alive).ToList();
                    foreach (var s in live)
                    {
                        foreach (var o in live)
                            if (o != s) Assert.GreaterOrEqual(OpenMath.Dist(s.Pos, o.Pos), s.Radius + o.Radius, $"seed {seed}: {s.Name} overlaps {o.Name}");
                        foreach (var r in b.Rocks)
                            Assert.GreaterOrEqual(OpenMath.Dist(s.Pos, r.Pos), s.Radius + r.Radius, $"seed {seed}: {s.Name} inside {r.Name}");
                    }
                }
                Assert.AreNotEqual(OpenOutcome.Ongoing, b.Outcome, $"seed {seed} did not finish in 80 turns");
            }
        }
    }
}
