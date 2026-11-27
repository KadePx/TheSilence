using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Silence.Core.Battle;
using Silence.Core.Hex;
using Silence.Core.Run;
using Silence.Data;

namespace Silence.Tests
{
    /// <summary>M3: the Concord as a playable side. Its content, its cards, its officers and its Council.</summary>
    public class ConcordTests
    {
        RunContent c;

        [SetUp]
        public void Load() => c = ContentLoader.Load(ContentLoader.Concord);

        static void Win(Battle b)
        {
            if (b.Phase == BattlePhase.Setup) b.Start();
            foreach (var e in b.Units.Where(u => u.Side == Side.Enemy && u.IsCapital && u.Alive).ToList()) b.Destroy(e);
        }

        /// <summary>Long Promise at (2,3) facing E and the Outrider at (2,5), Speed 0. Not started.</summary>
        static Battle Fleet(out Unit flag, out Unit escort)
        {
            var b = new Battle(1);
            flag = b.AddUnit(ConcordUnits.LongPromise, Side.Player, 2, 3, HexDirection.E, 0, flagship: true);
            escort = b.AddUnit(ConcordUnits.Outrider, Side.Player, 2, 5, HexDirection.E, 0);
            b.DartSpec = ConcordUnits.WingGunship;
            return b;
        }

        RunState AtFirstBattle(long seed = 5)
        {
            var r = RunRules.NewRun(seed, c);
            Assert.IsNull(RunRules.Travel(r, c, RunRules.Choices(r)[0].Id));
            Assert.AreEqual(RunPhase.Battle, r.Phase);
            return r;
        }

        // ---------------------------------------------------------------- content

        [Test]
        public void EveryFactionLoadsAndEveryReferenceResolves()
        {
            foreach (string id in ContentLoader.Factions)
            {
                var f = ContentLoader.Load(id);
                Assert.AreEqual(id, f.Faction.Id);
                Assert.AreEqual(UnitKind.Capital, UnitLibrary.Get(f.Faction.FlagshipUnit).Kind, id);
                Assert.AreEqual(UnitKind.Capital, UnitLibrary.Get(f.Faction.EscortUnit).Kind, id);
                Assert.AreEqual(UnitKind.Craft, UnitLibrary.Get(f.Faction.CraftUnit).Kind, id);
                Assert.AreEqual(UnitKind.Craft, UnitLibrary.Get(f.Faction.EnemyCraftUnit).Kind, id);
                foreach (string text in new[] { f.Faction.Name, f.Faction.Blurb, f.Faction.SectorName, f.Faction.OpeningJournal, f.Faction.BossName, f.Faction.BossHail,
                             f.Faction.WinAhead, f.Faction.WinBehind, f.Faction.Outpaced, f.Faction.LostAhead, f.Faction.LostBehind })
                    Assert.IsNotEmpty(text, id);

                Assert.AreEqual(f.Config.Slots.Count, f.Config.StarterModules.Count, id);
                foreach (var m in f.Modules)
                {
                    Assert.Contains(m.Slot, f.Config.Slots, $"{id}: {m.Id}");
                    foreach (string card in m.Cards) Assert.IsNotNull(PaperCards.Get(card));
                }
                foreach (string slot in f.Config.Slots) Assert.GreaterOrEqual(f.Modules.Count(m => m.Slot == slot), 2, $"{id}: {slot} needs something to refit to");
                foreach (var card in f.Config.StarterCards) Assert.IsNotNull(PaperCards.Get(card.Card));
                foreach (var x in f.Rewards)
                {
                    Assert.IsNotNull(PaperCards.Get(x.Card));
                    if (x.RequiresModule != "") Assert.IsNotNull(f.Module(x.RequiresModule));
                }
                foreach (var e in f.Encounters)
                    foreach (var u in e.Units.Concat(e.Reinforcements)) Assert.IsNotNull(UnitLibrary.Get(u.Unit), $"{id}: {e.Id}");
                for (int tier = 0; tier <= 3; tier++) Assert.IsTrue(f.Encounters.Any(e => e.Tier == tier), $"{id}: tier {tier}");
                Assert.GreaterOrEqual(f.Events.Count, 5, id);
                foreach (var ev in f.Events)
                    foreach (var ch in ev.Choices)
                    foreach (var o in ch.Outcomes)
                    {
                        if (o.Kind == OutcomeKind.AddCard || o.Kind == OutcomeKind.AddJunk) Assert.IsNotNull(PaperCards.Get(o.Id), $"{id}: {ev.Id}");
                        if (o.Kind == OutcomeKind.StartBattle) Assert.IsNotNull(f.Encounter(o.Id), $"{id}: {ev.Id}");
                        if (o.Kind == OutcomeKind.Approval || o.Kind == OutcomeKind.GainOfficer || o.Kind == OutcomeKind.HealOfficers)
                            Assert.IsTrue(f.Faction.HasCouncil, $"{id}: {ev.Id} uses a Council outcome");
                    }
                Assert.AreEqual(3, f.Nemeses.Count, id);
                foreach (var n in f.Nemeses)
                {
                    Assert.IsNotNull(UnitLibrary.Get(n.Ship), n.Id);
                    Assert.IsTrue(f.Relic(n.Trophy).Trophy, n.Id);
                    foreach (var rank in n.Escorts)
                        foreach (var u in rank) Assert.IsNotNull(UnitLibrary.Get(u.Unit), n.Id);
                }
                CollectionAssert.AreEquivalent(new[] { "edict", "fire", "board", "silence" }, f.HailReplies.Select(h => h.Effect).ToList(), id);
                foreach (string officer in f.Faction.StarterOfficers) Assert.IsNotNull(f.Officer(officer));
                if (f.Faction.HasCouncil)
                {
                    Assert.Greater(f.Motions.Count, 3, id);
                    Assert.Greater(f.Officers.Count, f.Faction.MaxOfficers, $"{id}: more officers than berths, so there is a choice");
                }
            }
        }

        [Test]
        public void AConcordRunStartsWithItsOwnFleetDeckOfficersAndApproval()
        {
            var r = RunRules.NewRun(11, c);
            Assert.AreEqual("concord", r.Faction);
            Assert.AreEqual("Long Promise", r.Flagship.Name);
            Assert.AreEqual("Outrider", r.Escort.Name);
            Assert.AreEqual(c.Faction.StartApproval, r.Approval);
            CollectionAssert.AreEqual(c.Faction.StarterOfficers, r.Officers.Select(o => o.Id).ToList());
            Assert.IsTrue(r.Deck.Any(d => d.Card == "mass_driver"));
            Assert.IsFalse(r.Deck.Any(d => d.Card == "lance_volley"));
            Assert.IsTrue(r.Deck.All(d => PaperCards.Get(d.Card).ExpendCost == 0), "the Concord does not spend its crew");
            Assert.IsTrue(r.Nemesis.Hunting);
            Assert.IsNotNull(c.Nemesis(r.Nemesis.Id));

            var asc = RunRules.NewRun(11, ContentLoader.Load());
            Assert.AreEqual("ascendancy", asc.Faction);
            Assert.AreEqual(0, asc.Officers.Count);
            Assert.AreEqual(0, asc.Approval);
        }

        [Test]
        public void ConcordBattlesOpenInReachAgainstTheAscendancy()
        {
            foreach (var enc in c.Encounters)
            {
                var r = AtFirstBattle();
                r.PendingEncounter = enc.Id;
                var b = RunRules.BuildBattle(r, c);
                Assert.AreEqual("Crew", b.CrewName);
                Assert.AreEqual("Wing gunship", b.DartSpec.Name);
                Assert.AreEqual("Ordinal dart", b.GunshipSpec.Name, "enemy carriers on this route launch darts");
                Assert.AreEqual(b.Units.Count, b.Units.Select(u => u.Pos).Distinct().Count(), enc.Id);
                foreach (var u in b.Units) Assert.IsFalse(b.Board.BlocksMovement(u.Pos), $"{enc.Id}: {u.Name} starts in terrain");
                int nearest = b.LiveUnits(Side.Enemy).Min(e => e.Pos.DistanceTo(b.Flagship.Pos));
                Assert.That(nearest, Is.InRange(2, 5), enc.Id);
                b.Start();
                Assert.AreEqual(5 + 1, b.Deck.Hand.Count, "the navigator draws one more on turn 1");
            }
        }

        // ---------------------------------------------------------------- cards

        [Test]
        public void TheMassDriverShovesItsTargetBackOrSlamsItIntoWhatIsBehind()
        {
            var b = Fleet(out var flag, out _);
            var target = b.AddUnit(T.Dummy(30), Side.Enemy, 5, 3, HexDirection.W, 0);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            var driver = T.Give(b, ConcordCards.MassDriver, flag);
            Assert.IsNull(b.PlayCard(driver.Id, PlayTarget.At(target.Id)));
            Assert.AreEqual(20, target.Hull);
            Assert.AreEqual(T.At(6, 3), target.Pos, "shoved one hex directly away");
            Assert.AreEqual(3, flag.Heat);

            b.Board.Set(7, 3, Terrain.Asteroid);
            b.Command = 3;
            var again = T.Give(b, ConcordCards.MassDriver, flag);
            Assert.IsNull(b.PlayCard(again.Id, PlayTarget.At(target.Id)));
            Assert.AreEqual(T.At(6, 3), target.Pos, "rock behind: it does not move");
            Assert.AreEqual(20 - 10 - AttackEffect.SlamDamage, target.Hull);
        }

        [Test]
        public void RingBarrageHitsEverythingCloseAndNothingFurther()
        {
            var b = Fleet(out var flag, out _);
            var near = b.AddUnit(T.Dummy(), Side.Enemy, 3, 3, HexDirection.W, 0);
            var behind = b.AddUnit(T.Dummy(), Side.Enemy, 0, 3, HexDirection.W, 0);
            var far = b.AddUnit(T.Dummy(), Side.Enemy, 6, 3, HexDirection.W, 0);
            b.Start();
            var ring = T.Give(b, ConcordCards.RingBarrage, flag);
            Assert.IsNull(b.PlayCard(ring.Id, PlayTarget.None));
            Assert.AreEqual(17, near.Hull);
            Assert.AreEqual(17, behind.Hull, "any arc");
            Assert.AreEqual(20, far.Hull);
        }

        [Test]
        public void ARippleSalvoIsFourHitsAndTheSpareMissilesFindTheNextTarget()
        {
            var b = Fleet(out var flag, out _);
            var armored = b.AddUnit(T.Dummy(20, 0, 1), Side.Enemy, 5, 3, HexDirection.W, 0);
            b.Start();
            var salvo = T.Give(b, ConcordCards.RippleSalvo, flag);
            Assert.IsNull(b.PlayCard(salvo.Id, PlayTarget.At(armored.Id)));
            Assert.AreEqual(20 - 4 * (3 - 1), armored.Hull, "armor bites every missile");

            var frail = b.AddUnit(T.Dummy(5), Side.Enemy, 4, 2, HexDirection.W, 0);
            b.Command = 3;
            var second = T.Give(b, ConcordCards.RippleSalvo, flag);
            Assert.IsNull(b.PlayCard(second.Id, PlayTarget.At(frail.Id)));
            Assert.IsFalse(frail.Alive);
            Assert.AreEqual(12 - 2 * 2, armored.Hull, "two missiles killed the first target, two went on");
        }

        [Test]
        public void SpotterDataBuffsOneWeaponAndLetsItReachAHexFurther()
        {
            var b = Fleet(out var flag, out _);
            var target = b.AddUnit(T.Dummy(40), Side.Enemy, 8, 3, HexDirection.W, 0);
            b.Start();
            var driver = T.Give(b, ConcordCards.MassDriver, flag);
            Assert.IsNotNull(b.CheckPlay(driver.Id, PlayTarget.At(target.Id)), "six hexes is out of the driver's range");
            b.Command = 5;
            var spot = T.Give(b, ConcordCards.SpotterData, flag);
            Assert.IsNull(b.PlayCard(spot.Id, PlayTarget.None));
            Assert.IsNull(b.PlayCard(driver.Id, PlayTarget.At(target.Id)));
            Assert.AreEqual(40 - 13, target.Hull);
            Assert.AreEqual(0, b.SpotterBonus, "used up by the first weapon");
            Assert.AreEqual(0, b.SpotterRange);
        }

        [Test]
        public void ACouncilVoteBuysCommandNextTurnOnly()
        {
            var b = Fleet(out var flag, out _);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            int hand = b.Deck.Hand.Count;
            var vote = T.Give(b, ConcordCards.CouncilVote, flag);
            b.AddCard(ConcordCards.DeckGuns, flag.Id);
            Assert.IsNull(b.PlayCard(vote.Id, PlayTarget.None));
            Assert.AreEqual(hand + 1, b.Deck.Hand.Count, "it replaces itself");
            Assert.AreEqual(3, b.Command);
            b.EndTurn();
            Assert.AreEqual(4, b.Command);
            b.EndTurn();
            Assert.AreEqual(3, b.Command);
        }

        [Test]
        public void PatchCrewsMendEveryShipAndAnEngineerMendsMore()
        {
            var b = Fleet(out var flag, out var escort);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            flag.Hull -= 10;
            escort.Hull -= 10;
            var crews = T.Give(b, ConcordCards.PatchCrews, flag);
            Assert.IsNull(b.PlayCard(crews.Id, PlayTarget.None));
            Assert.AreEqual(flag.MaxHull - 8, flag.Hull);
            Assert.AreEqual(escort.MaxHull - 8, escort.Hull);

            b.RepairBonus = 2;
            var job = T.Give(b, ConcordCards.PatchJob, flag);
            Assert.IsNull(b.PlayCard(job.Id, PlayTarget.At(escort.Id)));
            Assert.AreEqual(escort.MaxHull - 8 + 4 + 2, escort.Hull);
        }

        [Test]
        public void TheWingLaunchesGunshipsAndACombatPatrolTakesTheHit()
        {
            var b = Fleet(out var flag, out _);
            var frigate = b.AddUnit(PaperUnits.Picket, Side.Enemy, 4, 3, HexDirection.NW, 0);
            b.DartsInHangar = 4;
            b.Start();
            var patrol = T.Give(b, ConcordCards.CombatPatrol, flag);
            Assert.IsNotNull(b.CheckPlay(patrol.Id, PlayTarget.None), "no craft out yet");
            var wing = T.Give(b, ConcordCards.LaunchWing, flag);
            Assert.IsNull(b.PlayCard(wing.Id, PlayTarget.None));
            var craft = b.LiveUnits(Side.Player).Where(u => u.Kind == UnitKind.Craft).ToList();
            Assert.AreEqual(2, craft.Count);
            Assert.AreEqual("Wing gunship", craft[0].Name);
            Assert.AreEqual(2, b.DartsInHangar);
            Assert.IsNull(b.PlayCard(patrol.Id, PlayTarget.None));

            // The picket's broadside is aimed at the flagship; a gunship beside her takes it.
            var intent = b.Intents.First(i => i.UnitId == frigate.Id);
            Assert.AreEqual(flag.Id, intent.TargetId);
            int hull = flag.Hull;
            int[] shields = flag.Shields.ToArray();
            b.EndTurn();
            if (T.Logged(b, EventKind.Attack) && b.Log.Any(e => e.Kind == EventKind.Attack && e.UnitId == frigate.Id))
            {
                Assert.AreEqual(hull, flag.Hull, "the flagship was not touched");
                Assert.AreEqual(1, b.LiveUnits(Side.Player).Count(u => u.Kind == UnitKind.Craft), "one gunship flew into the shot");
            }
            Assert.AreEqual(0, b.PatrolCharges, "cover lasts one turn");
        }

        [Test]
        public void TheCuttingBeamIgnoresArmorAndWorksOnAnyArc()
        {
            var b = Fleet(out var flag, out _);
            var plated = b.AddUnit(T.Dummy(20, 0, 4), Side.Enemy, 1, 3, HexDirection.W, 0);
            b.Start();
            var beam = T.Give(b, ConcordCards.CuttingBeam, flag);
            Assert.IsNull(b.PlayCard(beam.Id, PlayTarget.At(plated.Id)), "dead astern is fine");
            Assert.AreEqual(13, plated.Hull);
        }

        [Test]
        public void TheMassDriverCountsAsAHeavyGunForACommandersBaffles()
        {
            var b = Fleet(out var flag, out _);
            var target = b.AddUnit(T.Dummy(30), Side.Enemy, 5, 3, HexDirection.W, 0);
            target.IsCommander = true;
            target.LanceWard = 3;
            b.Start();
            var driver = T.Give(b, ConcordCards.MassDriver, flag);
            Assert.IsNull(b.PlayCard(driver.Id, PlayTarget.At(target.Id)));
            Assert.AreEqual(30 - 7, target.Hull);
            Assert.AreEqual(7, b.CommanderDamage["lance"]);
        }

        // ---------------------------------------------------------------- officers

        [Test]
        public void OfficersGiveTheirBonusWhileFitAndNothingWhileWounded()
        {
            var r = AtFirstBattle();
            r.Officers.Clear();
            foreach (var o in c.Officers) r.Officers.Add(new OfficerState { Id = o.Id });
            var plain = RunRules.NewRun(5, c);
            RunRules.Travel(plain, c, RunRules.Choices(plain)[0].Id);
            plain.Officers.Clear();
            var bare = RunRules.BuildBattle(plain, c);

            var b = RunRules.BuildBattle(r, c);
            Assert.AreEqual(2, b.RepairBonus);
            Assert.AreEqual(1, b.FlagshipWeaponBonus);
            Assert.AreEqual(1, b.CraftDamageBonus);
            Assert.AreEqual(1, b.FirstTurnDraw);
            Assert.AreEqual(bare.Flagship.MaxShields + 1, b.Flagship.MaxShields);

            foreach (var o in r.Officers) o.Wounded = true;
            var hurt = RunRules.BuildBattle(r, c);
            Assert.AreEqual(0, hurt.RepairBonus + hurt.FlagshipWeaponBonus + hurt.CraftDamageBonus + hurt.FirstTurnDraw);
            Assert.AreEqual(bare.Flagship.MaxShields, hurt.Flagship.MaxShields);
        }

        [Test]
        public void TheGunneryOfficerAddsToTheFlagshipsGunsOnlyAndTheFlightBossToCraft()
        {
            var b = Fleet(out var flag, out var escort);
            var target = b.AddUnit(T.Dummy(40), Side.Enemy, 2, 4, HexDirection.W, 0);
            b.FlagshipWeaponBonus = 1;
            b.CraftDamageBonus = 1;
            var gunship = b.AddUnit(ConcordUnits.WingGunship, Side.Player, 3, 4, HexDirection.E, 0);
            b.Start();
            b.Command = 9;
            var deck = T.Give(b, ConcordCards.DeckGuns, flag);
            Assert.IsNull(b.PlayCard(deck.Id, PlayTarget.At(target.Id)));
            Assert.AreEqual(40 - 6, target.Hull);
            var guns = T.Give(b, ConcordCards.OutriderGuns, escort);
            Assert.IsNull(b.PlayCard(guns.Id, PlayTarget.At(target.Id)));
            Assert.AreEqual(40 - 6 - 5, target.Hull, "the escort's guns are her own");
            Assert.IsNull(b.CommandCraft(gunship.Id, gunship.Pos, target.Id));
            Assert.AreEqual(40 - 6 - 5 - 4, target.Hull);
        }

        [Test]
        public void ACriticalHitOnTheFlagshipWoundsAnOfficerAndAYardReturnsThemToDuty()
        {
            var r = AtFirstBattle();
            var b = RunRules.BuildBattle(r, c);
            b.Start();
            b.DealDamage(b.Flagship, 30, b.Flagship.Pos.Neighbor(HexDirection.E), new DamageFlags { IgnoreShields = true, IgnoreArmor = true }, null);
            Assert.IsTrue(T.Logged(b, EventKind.Critical));
            Win(b);
            RunRules.FinishBattle(r, c, b);
            Assert.AreEqual(1, r.Officers.Count(o => o.Wounded));

            RunRules.TakeReward(r, -1);
            r.Phase = RunPhase.Shipyard;
            r.Shop = new ShopState { Active = true };
            r.Scrap = 10;
            Assert.IsNotNull(RunRules.HealOfficer(r, c));
            r.Scrap = 40;
            Assert.IsNull(RunRules.HealOfficer(r, c));
            Assert.AreEqual(40 - c.Config.HealOfficerCost, r.Scrap);
            Assert.AreEqual(0, r.Officers.Count(o => o.Wounded));
            Assert.IsNotNull(RunRules.HealOfficer(r, c), "nobody left to treat");
        }

        // ---------------------------------------------------------------- the Council

        [Test]
        public void TheCouncilRewardsCleanWinsAndPunishesStandOffs()
        {
            var r = AtFirstBattle();
            int approval = r.Approval;
            var b = RunRules.BuildBattle(r, c);
            Win(b);
            RunRules.FinishBattle(r, c, b);
            Assert.AreEqual(approval + 1, r.Approval);

            var costly = AtFirstBattle(6);
            var b2 = RunRules.BuildBattle(costly, c);
            b2.Start();
            b2.Destroy(b2.Units.First(u => u.Side == Side.Player && u.IsCapital && !u.IsFlagship));
            Win(b2);
            RunRules.FinishBattle(costly, c, b2);
            Assert.AreEqual(approval, costly.Approval, "a win that cost a ship earns nothing");

            var stalled = AtFirstBattle(7);
            var b3 = RunRules.BuildBattle(stalled, c);
            b3.TurnLimit = 1;
            b3.Start();
            b3.EndTurn();
            Assert.AreEqual(BattleOutcome.Withdrawal, b3.Outcome);
            RunRules.FinishBattle(stalled, c, b3);
            Assert.AreEqual(approval - 1, stalled.Approval);
        }

        [Test]
        public void MotionsSpendApprovalAndDoWhatTheySay()
        {
            var r = RunRules.NewRun(8, c);
            int Motion(string id) => c.Motions.FindIndex(m => m.Id == id);
            r.Approval = 0;
            Assert.IsNotNull(RunRules.CallVote(r, c, Motion("requisition")), "no approval, no vote");

            r.Approval = c.Faction.MaxApproval;
            int scrap = r.Scrap;
            Assert.IsNull(RunRules.CallVote(r, c, Motion("requisition")));
            Assert.AreEqual(scrap + c.Motions[Motion("requisition")].Scrap, r.Scrap);
            Assert.AreEqual(c.Faction.MaxApproval - 1, r.Approval);

            Assert.IsNotNull(RunRules.CallVote(r, c, Motion("emergency_repairs")), "nothing to mend");
            r.Flagship.Hull -= 10;
            Assert.IsNull(RunRules.CallVote(r, c, Motion("emergency_repairs")));
            Assert.AreEqual(r.Flagship.MaxHull - 4, r.Flagship.Hull);

            Assert.IsNotNull(RunRules.CallVote(r, c, Motion("sickbay")), "nobody is wounded");
            r.Officers[0].Wounded = true;
            Assert.IsNull(RunRules.CallVote(r, c, Motion("sickbay")));
            Assert.IsFalse(r.Officers[0].Wounded);

            r.Approval = c.Faction.MaxApproval;
            r.ConcordProgress = 30;
            Assert.IsNull(RunRules.CallVote(r, c, Motion("run_dark")));
            Assert.AreEqual(22, r.ConcordProgress);

            int officers = r.Officers.Count;
            Assert.IsNull(RunRules.CallVote(r, c, Motion("commission")));
            Assert.AreEqual(officers + 1, r.Officers.Count);
            Assert.AreEqual(r.Officers.Count, r.Officers.Select(o => o.Id).Distinct().Count());
            while (r.Officers.Count < c.Faction.MaxOfficers)
            {
                r.Approval = c.Faction.MaxApproval;
                Assert.IsNull(RunRules.CallVote(r, c, Motion("commission")));
            }
            r.Approval = c.Faction.MaxApproval;
            Assert.IsNotNull(RunRules.CallVote(r, c, Motion("commission")), "every berth is taken");

            r.Phase = RunPhase.Reward;
            Assert.IsNotNull(RunRules.CallVote(r, c, Motion("requisition")), "the Council sits only between jumps");
            Assert.IsNotNull(RunRules.CallVote(RunRules.NewRun(1, ContentLoader.Load()), ContentLoader.Load(), 0), "the Ascendancy does not vote");
        }

        [Test]
        public void JammingTheRelayIsPaidForInApproval()
        {
            var r = RunRules.NewRun(3, c);
            r.Phase = RunPhase.Hail;
            int jam = c.HailReplies.FindIndex(h => h.Effect == "board");
            r.Approval = 0;
            Assert.IsNotNull(RunRules.ChooseHail(r, c, jam));
            r.Approval = 2;
            int crew = r.Ordinals;
            Assert.IsNull(RunRules.ChooseHail(r, c, jam));
            Assert.AreEqual(1, r.Approval);
            Assert.AreEqual(crew, r.Ordinals, "the Concord does not spend crew on it");
            var b = RunRules.BuildBattle(r, c);
            Assert.IsTrue(b.Units.Where(u => u.Side == Side.Enemy && !u.IsCommander).All(u => u.StunnedThroughTurn == c.Config.HailBoardTurns));
            StringAssert.Contains("approval", RunRules.HailEffect(c, c.HailReplies[jam]));
        }

        [Test]
        public void ConcordEventsCanAllBeAnsweredAndCouncilOutcomesApply()
        {
            foreach (var ev in c.Events)
            {
                for (int i = 0; i < ev.Choices.Count; i++)
                {
                    var r = RunRules.NewRun(2, c);
                    r.Scrap = 100;
                    r.Approval = 3;
                    r.Officers[0].Wounded = true;
                    r.Phase = RunPhase.Event;
                    r.EventId = ev.Id;
                    Assert.IsNull(RunRules.ChooseEvent(r, c, i), $"{ev.Id} choice {i}");
                    Assert.IsTrue(r.Phase == RunPhase.EventResult || r.Phase == RunPhase.Battle, ev.Id);
                    int delta = ev.Choices[i].Outcomes.Where(o => o.Kind == OutcomeKind.Approval).Sum(o => o.Amount);
                    Assert.AreEqual(3 + delta, r.Approval, $"{ev.Id} choice {i}");
                    if (ev.Choices[i].Outcomes.Any(o => o.Kind == OutcomeKind.HealOfficers)) Assert.IsFalse(r.Officers[0].Wounded);
                }
            }
        }

        // ---------------------------------------------------------------- saves and whole runs

        [Test]
        public void AConcordSaveKeepsItsFactionOfficersAndApproval()
        {
            var r = AtFirstBattle();
            r.Officers[1].Wounded = true;
            r.Approval = 4;
            var loaded = RunJson.FromJson(RunJson.ToJson(r));
            Assert.AreEqual("concord", loaded.Faction);
            Assert.AreEqual(4, loaded.Approval);
            Assert.IsTrue(loaded.Officers[1].Wounded);
            var content = ContentLoader.Load(loaded.Faction);
            Assert.AreSame(c, content);
            var b = RunRules.BuildBattle(loaded, content);
            Assert.AreEqual("Long Promise", b.Flagship.Name);
        }

        [Test]
        public void ConcordBotRunsAlwaysEndAndAreNeitherHopelessNorFree()
        {
            int won = 0, lost = 0;
            const int runs = 24;
            for (long seed = 1; seed <= runs; seed++)
            {
                var r = RunBot.Play(seed, c);
                Assert.AreEqual(RunPhase.Over, r.Phase, $"seed {seed}");
                Assert.AreEqual("concord", r.Faction);
                if (r.Result == RunResult.Victory) won++;
                if (r.Result == RunResult.Defeat) lost++;
            }
            Assert.That(won, Is.InRange(runs / 5, runs * 4 / 5), "Concord bot wins");
            Assert.Greater(lost, 0, "the Ascendancy should be able to kill a careless fleet");
        }
    }
}
