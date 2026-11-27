using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Silence.Core.Battle;
using Silence.Core.Run;
using Silence.Data;

namespace Silence.Tests
{
    /// <summary>M2: the run loop. Content from data, map, travel, battles in and out, rewards, events, shipyard, saves.</summary>
    public class RunRulesTests
    {
        RunContent c;

        [SetUp]
        public void Load() => c = ContentLoader.Load();

        static void Win(Battle b)
        {
            b.Start();
            foreach (var e in b.Units.Where(u => u.Side == Side.Enemy && u.IsCapital).ToList()) b.Destroy(e);
        }

        static void Lose(Battle b)
        {
            b.Start();
            foreach (var p in b.Units.Where(u => u.Side == Side.Player && u.IsCapital).ToList()) b.Destroy(p);
        }

        RunState AtFirstBattle(long seed = 5)
        {
            var r = RunRules.NewRun(seed, c);
            Assert.IsNull(RunRules.Travel(r, c, RunRules.Choices(r)[0].Id));
            Assert.AreEqual(RunPhase.Battle, r.Phase);
            return r;
        }

        /// <summary>Walk the map, winning fights instantly, until a node of the wanted kind is entered.</summary>
        RunState Reach(NodeKind kind, long firstSeed = 1)
        {
            for (long seed = firstSeed; seed < firstSeed + 200; seed++)
            {
                var r = RunRules.NewRun(seed, c);
                for (int guard = 0; guard < 60 && r.Phase != RunPhase.Over; guard++)
                {
                    switch (r.Phase)
                    {
                        case RunPhase.Map:
                        {
                            var options = RunRules.Choices(r);
                            var next = options.FirstOrDefault(n => n.Kind == kind) ?? options[0];
                            RunRules.Travel(r, c, next.Id);
                            if (next.Kind == kind) return r;
                            break;
                        }
                        case RunPhase.Battle:
                        {
                            var b = RunRules.BuildBattle(r, c);
                            Win(b);
                            RunRules.FinishBattle(r, c, b);
                            break;
                        }
                        case RunPhase.Reward: RunRules.TakeReward(r, -1); break;
                        case RunPhase.Event: RunRules.ChooseEvent(r, c, c.Event(r.EventId).Choices.Count - 1); break;
                        case RunPhase.EventResult: RunRules.CloseEvent(r); break;
                        case RunPhase.Shipyard: RunRules.LeaveShipyard(r, c); break;
                    }
                }
            }
            Assert.Fail($"no run reached a {kind} node");
            return null;
        }

        // ---------------------------------------------------------------- content

        [Test]
        public void ContentLoadsAndEveryReferenceResolves()
        {
            Assert.GreaterOrEqual(c.Events.Count, 5, "M2 asks for 5 written events");
            Assert.AreEqual(1, c.Encounters.Count(e => e.Tier == 3), "one sector boss");
            foreach (int tier in new[] { 0, 1, 2 }) Assert.GreaterOrEqual(c.Encounters.Count(e => e.Tier == tier), 2, $"tier {tier}");

            foreach (var e in c.Encounters)
            {
                Assert.IsNotEmpty(e.Units, e.Id);
                foreach (var u in e.Units.Concat(e.Reinforcements))
                {
                    Assert.DoesNotThrow(() => UnitLibrary.Get(u.Unit), $"{e.Id}: {u.Unit}");
                    Assert.IsTrue(u.Col >= 2 && u.Col <= 8 && u.Row >= 0 && u.Row <= 6, $"{e.Id}: {u.Unit} on the board, clear of the player's edge");
                }
                Assert.AreEqual(e.Units.Count + e.Reinforcements.Count, e.Units.Concat(e.Reinforcements).Select(u => (u.Col, u.Row)).Distinct().Count(), $"{e.Id}: no shared hexes");
            }
            foreach (var m in c.Modules)
            {
                Assert.Contains(m.Slot, c.Config.Slots);
                foreach (string card in m.Cards) Assert.DoesNotThrow(() => PaperCards.Get(card), $"{m.Id}: {card}");
            }
            foreach (string slot in c.Config.Slots) Assert.GreaterOrEqual(c.Modules.Count(m => m.Slot == slot), 2, $"{slot} has a refit option");
            foreach (var x in c.Rewards) Assert.DoesNotThrow(() => PaperCards.Get(x.Card));
            foreach (var x in c.Config.StarterCards) Assert.DoesNotThrow(() => PaperCards.Get(x.Card));
            foreach (var ev in c.Events)
            {
                Assert.GreaterOrEqual(ev.Choices.Count, 2, ev.Id);
                Assert.IsNotEmpty(ev.Body, ev.Id);
                foreach (var o in ev.Choices.SelectMany(ch => ch.Outcomes))
                {
                    if (o.Kind == OutcomeKind.AddCard || o.Kind == OutcomeKind.AddJunk) Assert.DoesNotThrow(() => PaperCards.Get(o.Id), ev.Id);
                    if (o.Kind == OutcomeKind.StartBattle) Assert.DoesNotThrow(() => c.Encounter(o.Id), ev.Id);
                }
                Assert.IsTrue(ev.Choices.Any(ch => RunRules.CanChoose(new RunState(), ch) == null), $"{ev.Id}: a broke fleet can still answer");
            }
        }

        [Test]
        public void NewRunStartsWithTheStarterFleetAndDeck()
        {
            var r = RunRules.NewRun(1, c);
            Assert.AreEqual(2, r.Ships.Count);
            Assert.AreEqual(30, r.Flagship.Hull);
            Assert.AreEqual(14, r.Escort.Hull);
            Assert.AreEqual(4, r.Modules.Count);
            Assert.AreEqual(12, r.Deck.Count);
            Assert.AreEqual(24, r.Ordinals);
            Assert.AreEqual(6, r.Darts);
            Assert.AreEqual(RunPhase.Map, r.Phase);
            Assert.AreEqual(-1, r.CurrentNode);
        }

        // ---------------------------------------------------------------- map

        [Test]
        public void SameSeedSameMap()
        {
            string Shape(RunState r) => string.Join(";", r.Nodes.Select(n => $"{n.Layer}/{n.Lane}/{n.Kind}/{n.Encounter}{n.Event}>{string.Join(",", n.Next)}"));
            Assert.AreEqual(Shape(RunRules.NewRun(42, c)), Shape(RunRules.NewRun(42, c)));
            Assert.AreNotEqual(Shape(RunRules.NewRun(42, c)), Shape(RunRules.NewRun(43, c)));
        }

        [Test]
        public void EveryMapIsABranchingPathToTheBoss()
        {
            for (long seed = 1; seed <= 60; seed++)
            {
                var r = RunRules.NewRun(seed, c);
                int last = r.BossLayer;
                var boss = r.Nodes.Where(n => n.Kind == NodeKind.Boss).ToList();
                Assert.AreEqual(1, boss.Count, $"seed {seed}");
                Assert.AreEqual(last, boss[0].Layer);
                Assert.IsTrue(r.Nodes.Where(n => n.Layer == last - 1).All(n => n.Kind == NodeKind.Shipyard), "a yard before the boss");
                Assert.IsTrue(r.Nodes.Where(n => n.Layer == 0).All(n => n.Kind == NodeKind.Battle), "the run opens with a fight");
                Assert.IsTrue(r.Nodes.Any(n => n.Layer == last / 2 && n.Kind == NodeKind.Shipyard), "a yard halfway");
                Assert.GreaterOrEqual(r.Nodes.Count(n => n.Kind == NodeKind.Event), 1);

                var reached = new HashSet<int>(r.Nodes.Where(n => n.Layer == 0).Select(n => n.Id));
                foreach (var n in r.Nodes.OrderBy(n => n.Layer))
                {
                    Assert.IsTrue(reached.Contains(n.Id), $"seed {seed}: node {n.Id} can be reached");
                    if (n.Kind != NodeKind.Boss) Assert.IsNotEmpty(n.Next, $"seed {seed}: node {n.Id} leads on");
                    foreach (int id in n.Next)
                    {
                        Assert.AreEqual(n.Layer + 1, r.Node(id).Layer);
                        reached.Add(id);
                    }
                }
                for (int layer = 1; layer < last - 1; layer++)
                {
                    var kinds = r.Nodes.Where(n => n.Layer == layer).Select(n => n.Kind).Distinct().Count();
                    Assert.GreaterOrEqual(kinds, 2, $"seed {seed}: layer {layer} offers a real choice");
                }
            }
        }

        [Test]
        public void TheMapOnlyEverGoesForward()
        {
            for (long seed = 1; seed <= 30; seed++)
            {
                var r = RunRules.NewRun(seed, c);
                foreach (var n in r.Nodes)
                    foreach (int id in n.Next) Assert.Greater(r.Node(id).Layer, n.Layer, $"seed {seed}: a link never leads back or sideways");

                // Walk a few nodes: nothing behind or beside the fleet is ever offered or accepted.
                for (int step = 0; step < 4; step++)
                {
                    var options = RunRules.Choices(r);
                    int layer = r.CurrentNode < 0 ? -1 : r.Current.Layer;
                    Assert.IsTrue(options.All(o => o.Layer == layer + (RunRules.IsJump(r, o.Id) ? 2 : 1)), $"seed {seed}: only the next layer is offered, or the one after it by jump lane");
                    foreach (var behind in r.Nodes.Where(n => n.Layer <= layer))
                        Assert.IsNotNull(RunRules.Travel(r, c, behind.Id), $"seed {seed}: node {behind.Id} is behind the fleet");
                    Assert.IsNull(RunRules.Travel(r, c, options[0].Id));
                    r.Phase = RunPhase.Map; // skip whatever the node holds
                }
            }
        }

        [Test]
        public void TravelFollowsLinksAndMovesTheRace()
        {
            var r = RunRules.NewRun(3, c);
            var first = RunRules.Choices(r);
            Assert.AreEqual(3, first.Count);
            Assert.IsNotNull(RunRules.Travel(r, c, r.Nodes.First(n => n.Layer == 2).Id), "can't skip ahead");
            Assert.IsNull(RunRules.Travel(r, c, first[1].Id));
            Assert.AreEqual(10, r.PlayerProgress);
            Assert.That(r.ConcordProgress, Is.InRange(c.Config.ConcordStart + c.Config.ConcordRate, c.Config.ConcordStart + c.Config.ConcordRate + c.Config.RivalSpread));
            Assert.IsTrue(first[1].Visited);
            Assert.IsNotNull(RunRules.Travel(r, c, first[1].Next[0]), "the battle comes first");
        }

        // ---------------------------------------------------------------- battles

        [Test]
        public void BattleIsBuiltFromTheFleetAsItStands()
        {
            var r = AtFirstBattle();
            r.Flagship.Hull = 17;
            r.Ordinals = 9;
            r.Darts = 2;
            r.Deck.Add(new DeckCard("hull_breach", -1));
            var b = RunRules.BuildBattle(r, c);
            Assert.AreEqual(17, b.Flagship.Hull);
            Assert.AreEqual(30, b.Flagship.MaxHull);
            Assert.AreEqual(8, b.Flagship.HeatCap, "base 6 plus the radiator stack");
            Assert.AreEqual(9, b.Ordinals);
            Assert.AreEqual(2, b.DartsInHangar);
            Assert.AreEqual(13, b.Deck.Count);
            Assert.AreEqual(1, b.Deck.DrawPile.Count(x => x.Def.IsJunk));
            Assert.IsTrue(b.LiveUnits(Side.Enemy).Any(u => u.IsCapital));
            Assert.AreEqual(c.Encounter(r.PendingEncounter).Name, b.Title);
        }

        [Test]
        public void BattlesOpenWithTheFleetsAlreadyInReach()
        {
            Assert.AreEqual(2, c.Config.PlayerStartCol);
            foreach (var enc in c.Encounters)
            {
                var r = RunRules.NewRun(3, c);
                r.Phase = RunPhase.Battle;
                r.PendingEncounter = enc.Id;
                r.CurrentNode = 0;
                var b = RunRules.BuildBattle(r, c);
                var flag = b.Flagship;
                Assert.AreEqual((c.Config.PlayerStartCol, 2), flag.Pos.ToOffset(), enc.Id);
                int nearest = b.LiveUnits(Side.Enemy).Min(e => e.Pos.DistanceTo(flag.Pos));
                Assert.LessOrEqual(nearest, 4, $"{enc.Id}: something is inside lance range on turn 1");
                Assert.GreaterOrEqual(nearest, 2, $"{enc.Id}: nothing starts on top of the flagship");
                Assert.AreEqual(b.Units.Count, b.Units.Select(u => u.Pos).Distinct().Count(), $"{enc.Id}: no two units share a hex");
                foreach (var u in b.Units) Assert.IsFalse(b.Board.BlocksMovement(u.Pos), $"{enc.Id}: {u.Name} does not start inside terrain");
            }
        }

        [Test]
        public void SlowHeaviesHoldTheBackLineWhileTheirEscortsComeForward()
        {
            var r = RunRules.NewRun(3, c);
            r.Phase = RunPhase.Battle;
            r.PendingEncounter = "carrier_group";
            r.CurrentNode = 0;
            var b = RunRules.BuildBattle(r, c);
            var enc = c.Encounter("carrier_group");
            var carrier = b.Units.First(u => u.Side == Side.Enemy && u.MaxSpeed <= 1);
            var frigate = b.Units.First(u => u.Side == Side.Enemy && u.IsCapital && u.MaxSpeed > 1);
            Assert.AreEqual(enc.Units.First(u => u.Unit == "carrier").Col, carrier.Pos.ToOffset().col);
            Assert.AreEqual(enc.Units.First(u => u.Unit == "frigate").Col - c.Config.EnemyAdvance, frigate.Pos.ToOffset().col);
        }

        [Test]
        public void TheSameNodeAlwaysBuildsTheSameBattle()
        {
            var r = AtFirstBattle(11);
            var a = RunRules.BuildBattle(r, c);
            var b = RunRules.BuildBattle(r, c);
            a.Start();
            b.Start();
            Assert.AreEqual(a.Describe(), b.Describe());
        }

        [Test]
        public void ALostEscortTakesItsCardsOutOfTheNextBattle()
        {
            var r = AtFirstBattle();
            r.Escort.Alive = false;
            var b = RunRules.BuildBattle(r, c);
            Assert.AreEqual(1, b.LiveUnits(Side.Player).Count(u => u.IsCapital));
            Assert.AreEqual(10, b.Deck.Count, "Escort Guns and Come About stay behind");
        }

        [Test]
        public void VictoryCarriesDamageJunkAndLossesAndOffersAReward()
        {
            var r = AtFirstBattle();
            var b = RunRules.BuildBattle(r, c);
            b.Start();
            b.Flagship.Hull = 21;
            b.Ordinals = 15;
            b.DartsInHangar = 4; // two launched and lost
            b.AddJunk(PaperCards.FireOnDeck, "test");
            b.AddJunk(PaperCards.HullBreach, "test");
            b.Deck.Exhausted.Add(new CardInstance { Id = 999, Def = PaperCards.FireOnDeck, SourceId = -1 }); // a fire put out
            foreach (var e in b.Units.Where(u => u.Side == Side.Enemy && u.IsCapital).ToList()) b.Destroy(e);
            int scrapBefore = r.Scrap;

            RunRules.FinishBattle(r, c, b);
            Assert.AreEqual(RunPhase.Reward, r.Phase);
            Assert.AreEqual(21, r.Flagship.Hull);
            Assert.AreEqual(15 + c.Config.OrdinalsPerVictory, r.Ordinals);
            Assert.AreEqual(4, r.Darts);
            Assert.AreEqual(2, r.JunkCount(), "the exhausted fire is gone, the rest stays");
            Assert.Greater(r.Scrap, scrapBefore);
            Assert.AreEqual(c.Config.RewardCards, r.Reward.Cards.Count);
            Assert.AreEqual(r.Reward.Cards.Count, r.Reward.Cards.Select(x => x.Card).Distinct().Count());
            Assert.AreEqual(1, r.BattlesWon);

            int deck = r.Deck.Count;
            Assert.IsNotNull(RunRules.TakeReward(r, 9));
            Assert.IsNull(RunRules.TakeReward(r, 0));
            Assert.AreEqual(deck + 1, r.Deck.Count);
            Assert.AreEqual(RunPhase.Map, r.Phase);
            Assert.IsNotNull(RunRules.TakeReward(r, 0), "only once");
        }

        [Test]
        public void SkippingARewardAddsNothing()
        {
            var r = AtFirstBattle();
            var b = RunRules.BuildBattle(r, c);
            Win(b);
            RunRules.FinishBattle(r, c, b);
            int deck = r.Deck.Count;
            Assert.IsNull(RunRules.TakeReward(r, -1));
            Assert.AreEqual(deck, r.Deck.Count);
        }

        [Test]
        public void FieldRepairsCanBeTakenInsteadOfACard()
        {
            var r = AtFirstBattle();
            var b = RunRules.BuildBattle(r, c);
            b.Start();
            b.Flagship.Hull = 12;
            foreach (var e in b.Units.Where(u => u.Side == Side.Enemy && u.IsCapital).ToList()) b.Destroy(e);
            RunRules.FinishBattle(r, c, b);
            int deck = r.Deck.Count;
            Assert.IsNull(RunRules.TakeFieldRepair(r, c));
            Assert.AreEqual(12 + c.Config.FieldRepair, r.Flagship.Hull);
            Assert.AreEqual(14, r.Escort.Hull, "never above maximum");
            Assert.AreEqual(deck, r.Deck.Count);
            Assert.AreEqual(RunPhase.Map, r.Phase);
            Assert.IsNotNull(RunRules.TakeFieldRepair(r, c), "only once, and not as well as a card");
        }

        [Test]
        public void EachEventAppearsAtMostOncePerSector()
        {
            for (long seed = 1; seed <= 40; seed++)
            {
                var r = RunRules.NewRun(seed, c);
                var ids = r.Nodes.Where(n => n.Kind == NodeKind.Event).Select(n => n.Event).ToList();
                Assert.AreEqual(ids.Count, ids.Distinct().Count(), $"seed {seed}");
                Assert.IsTrue(r.Nodes.Where(n => n.Kind == NodeKind.Battle).All(n => !string.IsNullOrEmpty(n.Encounter)));
            }
        }

        [Test]
        public void DefeatEndsTheRun()
        {
            var r = AtFirstBattle();
            var b = RunRules.BuildBattle(r, c);
            Lose(b);
            RunRules.FinishBattle(r, c, b);
            Assert.AreEqual(RunPhase.Over, r.Phase);
            Assert.AreEqual(RunResult.Defeat, r.Result);
            Assert.IsEmpty(RunRules.Choices(r));
        }

        [Test]
        public void WithdrawalCostsTimeAndPaysNothing()
        {
            var r = AtFirstBattle();
            var b = RunRules.BuildBattle(r, c);
            b.TurnLimit = 1;
            b.Start();
            b.EndTurn();
            Assert.AreEqual(BattleOutcome.Withdrawal, b.Outcome);
            int concord = r.ConcordProgress, scrap = r.Scrap;
            RunRules.FinishBattle(r, c, b);
            Assert.AreEqual(RunPhase.Map, r.Phase);
            Assert.AreEqual(concord + c.Config.WithdrawalDelay, r.ConcordProgress);
            Assert.AreEqual(scrap, r.Scrap);
            Assert.IsFalse(r.Reward.Active);
        }

        [Test]
        public void AFlagshipLostInAWonBattleIsRebuiltAtACost()
        {
            var r = AtFirstBattle();
            var b = RunRules.BuildBattle(r, c);
            b.Start();
            b.Destroy(b.Flagship);
            foreach (var e in b.Units.Where(u => u.Side == Side.Enemy && u.IsCapital).ToList()) b.Destroy(e);
            Assert.AreEqual(BattleOutcome.Victory, b.Outcome);
            RunRules.FinishBattle(r, c, b);
            Assert.IsTrue(r.Flagship.Alive);
            Assert.AreEqual(30 - c.Config.FlagshipRebuildMaxHullLoss, r.Flagship.MaxHull);
            Assert.AreEqual(r.Flagship.MaxHull / 2, r.Flagship.Hull);
            Assert.AreEqual(24 - c.Config.FlagshipRebuildOrdinals + c.Config.OrdinalsPerVictory, r.Ordinals);
        }

        [Test]
        public void BeatingTheBossWinsTheRunAndStallingThereLosesTheRace()
        {
            var r = Reach(NodeKind.Boss);
            Assert.AreEqual(3, c.Encounter(r.PendingEncounter).Tier);
            var b = RunRules.BuildBattle(r, c);
            Win(b);
            RunRules.FinishBattle(r, c, b);
            Assert.AreEqual(RunResult.Victory, r.Result);
            Assert.Greater(RunRules.Score(r), 500);

            var r2 = Reach(NodeKind.Boss);
            var b2 = RunRules.BuildBattle(r2, c);
            b2.TurnLimit = 1;
            b2.Start();
            b2.EndTurn();
            RunRules.FinishBattle(r2, c, b2);
            Assert.AreEqual(RunResult.Outpaced, r2.Result);
        }

        [Test]
        public void TheGateFleetIsReinforcedWhenTheConcordGetsThereFirst()
        {
            var r = Reach(NodeKind.Boss);
            r.ConcordProgress = 0;
            r.VennEnraged = false;
            int normal = RunRules.BuildBattle(r, c).Units.Count(u => u.Side == Side.Enemy);
            r.ConcordProgress = r.PlayerProgress;
            Assert.IsTrue(RunRules.ConcordAhead(r));
            Assert.Greater(RunRules.BuildBattle(r, c).Units.Count(u => u.Side == Side.Enemy), normal);
            r.ConcordProgress = 0;
            r.VennEnraged = true;
            Assert.Greater(RunRules.BuildBattle(r, c).Units.Count(u => u.Side == Side.Enemy), normal, "an enraged Venn brings the same help");
        }

        // ---------------------------------------------------------------- events

        [Test]
        public void EveryEventChoiceCanBeTakenByAFreshFleet()
        {
            foreach (var ev in c.Events)
            {
                for (int i = 0; i < ev.Choices.Count; i++)
                {
                    var r = RunRules.NewRun(1, c);
                    r.Scrap = 100;
                    r.Deck.Add(new DeckCard("fire_on_deck", -1));
                    r.Phase = RunPhase.Event;
                    r.EventId = ev.Id;
                    Assert.IsNull(RunRules.ChooseEvent(r, c, i), $"{ev.Id} choice {i}");
                    Assert.That(r.Phase, Is.EqualTo(RunPhase.EventResult).Or.EqualTo(RunPhase.Battle), $"{ev.Id} choice {i}");
                    Assert.IsNotEmpty(r.EventResult);
                    if (r.Phase == RunPhase.Battle) Assert.DoesNotThrow(() => RunRules.BuildBattle(r, c));
                    else Assert.IsNull(RunRules.CloseEvent(r));
                }
            }
        }

        [Test]
        public void EventOutcomesChangeTheFleet()
        {
            var r = RunRules.NewRun(1, c);
            r.Phase = RunPhase.Event;
            r.EventId = "derelict";
            Assert.IsNull(RunRules.ChooseEvent(r, c, 0));
            Assert.AreEqual(20, r.Ordinals);
            Assert.IsTrue(r.Deck.Any(d => d.Card == "waymind_targeting"));

            r = RunRules.NewRun(1, c);
            r.Phase = RunPhase.Event;
            r.EventId = "derelict";
            Assert.IsNull(RunRules.ChooseEvent(r, c, 1));
            Assert.AreEqual(25, r.Flagship.Hull);
            Assert.AreEqual(c.Config.StartScrap + 34, r.Scrap);

            r = RunRules.NewRun(1, c);
            r.Phase = RunPhase.Event;
            r.EventId = "convoy";
            int concord = r.ConcordProgress;
            Assert.IsNull(RunRules.ChooseEvent(r, c, 1));
            Assert.Less(r.ConcordProgress, concord);

            r = RunRules.NewRun(1, c);
            r.Phase = RunPhase.Event;
            r.EventId = "convoy";
            Assert.IsNull(RunRules.ChooseEvent(r, c, 0));
            Assert.IsTrue(r.VennEnraged);
        }

        [Test]
        public void AChoiceTheFleetCannotPayForIsRefused()
        {
            var r = RunRules.NewRun(1, c);
            r.Phase = RunPhase.Event;
            r.EventId = "derelict";
            r.Ordinals = 3;
            Assert.IsNotNull(RunRules.ChooseEvent(r, c, 0));
            Assert.AreEqual(RunPhase.Event, r.Phase);
            Assert.AreEqual(3, r.Ordinals);

            r.EventId = "edict";
            r.Scrap = 10;
            Assert.IsNotNull(RunRules.ChooseEvent(r, c, 2));
        }

        // ---------------------------------------------------------------- shipyard

        [Test]
        public void ShipyardRepairsRemovesJunkAndSellsCards()
        {
            var r = Reach(NodeKind.Shipyard);
            Assert.AreEqual(RunPhase.Shipyard, r.Phase);
            r.Scrap = 200;
            r.Flagship.Hull = 20;
            r.Deck.Add(new DeckCard("jammed_mount", -1));
            Assert.AreEqual(20, RunRules.RepairCost(r, c, 0));
            Assert.IsNull(RunRules.Repair(r, c, 0));
            Assert.AreEqual(30, r.Flagship.Hull);
            Assert.AreEqual(180, r.Scrap);
            Assert.IsNotNull(RunRules.Repair(r, c, 0), "nothing left to repair");

            Assert.IsNull(RunRules.RemoveJunk(r, c));
            Assert.AreEqual(0, r.JunkCount());
            Assert.IsNotNull(RunRules.RemoveJunk(r, c));

            Assert.AreEqual(3, r.Shop.Cards.Count);
            int deck = r.Deck.Count, price = r.Shop.CardPrices[0];
            int scrap = r.Scrap;
            Assert.IsNull(RunRules.BuyCard(r, 0));
            Assert.AreEqual(deck + 1, r.Deck.Count);
            Assert.AreEqual(scrap - price, r.Scrap);
            Assert.AreEqual(2, r.Shop.Cards.Count);

            int concord = r.ConcordProgress;
            Assert.IsNull(RunRules.LeaveShipyard(r, c));
            Assert.AreEqual(concord + 3 * c.Config.ShipyardDelayPerPurchase, r.ConcordProgress, "three services, three delays");
            Assert.AreEqual(RunPhase.Map, r.Phase);
        }

        [Test]
        public void PartialRepairSpendsWhatThereIs()
        {
            var r = Reach(NodeKind.Shipyard);
            r.Scrap = 7;
            r.Flagship.Hull = 10;
            Assert.IsNull(RunRules.Repair(r, c, 0));
            Assert.AreEqual(13, r.Flagship.Hull);
            Assert.AreEqual(1, r.Scrap);
        }

        [Test]
        public void RefittingAModuleSwapsItsCardsAndStats()
        {
            var r = Reach(NodeKind.Shipyard);
            r.Scrap = 500;
            r.Shop.Modules = new List<string> { "armored_plating", "lance_battery" };

            Assert.IsNull(RunRules.FitModule(r, c, "armored_plating"));
            Assert.AreEqual("armored_plating", r.Modules[3]);
            Assert.AreEqual(38, r.Flagship.MaxHull);
            Assert.IsFalse(r.Deck.Any(d => d.Card == "launch_darts"));
            Assert.IsTrue(r.Deck.Any(d => d.Card == "hold_the_line"));
            Assert.Contains("dart_hangar", r.Shop.Modules, "the old module can be put back");

            Assert.IsNull(RunRules.FitModule(r, c, "lance_battery"));
            Assert.AreEqual(0, r.Deck.Count(d => d.Card == "lance_volley"));
            Assert.AreEqual(3, r.Deck.Count(d => d.Card == "short_lance"));
            Assert.IsNotNull(RunRules.FitModule(r, c, "boarding_pods"), "not on offer");

            RunRules.LeaveShipyard(r, c);
            r.Phase = RunPhase.Battle;
            r.PendingEncounter = "picket";
            var b = RunRules.BuildBattle(r, c);
            Assert.AreEqual(2, b.Flagship.MaxSpeed, "plating slows her");
            Assert.AreEqual(38, b.Flagship.MaxHull);
        }

        [Test]
        public void ALostEscortCanBeRebuiltAndDartsRestocked()
        {
            var r = Reach(NodeKind.Shipyard);
            r.Scrap = 100;
            Assert.IsNotNull(RunRules.RebuildEscort(r, c));
            r.Escort.Alive = false;
            r.Escort.Hull = 0;
            Assert.IsNull(RunRules.RebuildEscort(r, c));
            Assert.IsTrue(r.Escort.Alive);
            Assert.AreEqual(14, r.Escort.Hull);

            r.Darts = 4;
            Assert.IsNull(RunRules.BuyDart(r, c));
            Assert.IsNull(RunRules.BuyDart(r, c));
            Assert.IsNotNull(RunRules.BuyDart(r, c), "hangar full");
            r.Ordinals = 10;
            Assert.IsNull(RunRules.BuyOrdinals(r, c));
            Assert.AreEqual(10 + c.Config.OrdinalsPerPurchase, r.Ordinals);

            r.Scrap = 0;
            r.Darts = 0;
            Assert.IsNotNull(RunRules.BuyDart(r, c), "no scrap");
        }

        // ---------------------------------------------------------------- save and whole runs

        [Test]
        public void SaveRoundTripsAndTheRunContinuesIdentically()
        {
            var a = RunRules.NewRun(77, c);
            for (int i = 0; i < 7; i++) RunBot.Step(a, c);
            string json = RunJson.ToJson(a);
            var b = RunJson.FromJson(json);
            Assert.AreEqual(json, RunJson.ToJson(b), "nothing lost in the save");

            for (int i = 0; i < 300 && a.Phase != RunPhase.Over; i++) RunBot.Step(a, c);
            for (int i = 0; i < 300 && b.Phase != RunPhase.Over; i++) RunBot.Step(b, c);
            Assert.AreEqual(RunJson.ToJson(a), RunJson.ToJson(b), "the loaded run plays out exactly like the original");
        }

        [Test]
        public void GarbageIsNotASave()
        {
            Assert.Throws<System.FormatException>(() => RunJson.FromJson("{}"));
        }

        [Test]
        public void BotRunsAlwaysEndAndAreNeitherHopelessNorFree()
        {
            int wins = 0, defeats = 0, outpaced = 0;
            const int runs = 60;
            for (long seed = 1; seed <= runs; seed++)
            {
                var r = RunBot.Play(seed, c);
                Assert.AreEqual(RunPhase.Over, r.Phase, $"seed {seed} did not finish");
                if (r.Result == RunResult.Victory) wins++;
                else if (r.Result == RunResult.Defeat) defeats++;
                else outpaced++;
            }
            TestContext.WriteLine($"bot runs: {wins} won, {defeats} lost, {outpaced} outpaced of {runs}");
            Assert.That(wins, Is.InRange(runs * 10 / 100, runs * 75 / 100), "the stand-in bot should win some runs, not most");
        }

        // ---------------------------------------------------------------- core review: striking a card

        [Test]
        public void ShipyardStrikesAChosenCardFromTheDeck()
        {
            var r = Reach(NodeKind.Shipyard);
            r.Scrap = 100;
            int index = r.Deck.FindIndex(d => d.Card == "burn");
            int before = r.Deck.Count, purchases = r.Shop.Purchases;
            Assert.IsNull(RunRules.RemoveCard(r, c, index));
            Assert.AreEqual(before - 1, r.Deck.Count);
            Assert.IsFalse(r.Deck.Any(d => d.Card == "burn"));
            Assert.AreEqual(100 - c.Config.RemoveCardCost, r.Scrap);
            Assert.AreEqual(purchases + 1, r.Shop.Purchases, "striking a card takes dock time like any service");
        }

        [Test]
        public void StrikingACardHasLimits()
        {
            var r = Reach(NodeKind.Shipyard);
            r.Scrap = 0;
            Assert.IsNotNull(RunRules.RemoveCard(r, c, 0), "needs scrap");
            r.Scrap = 1000;
            r.Deck.Add(new DeckCard("hull_breach", -1));
            Assert.IsNotNull(RunRules.RemoveCard(r, c, r.Deck.Count - 1), "junk has its own service");
            Assert.IsNotNull(RunRules.RemoveCard(r, c, 99), "no such card");
            while (r.Deck.Count(d => d.Ship >= 0) > c.Config.MinDeckSize) Assert.IsNull(RunRules.RemoveCard(r, c, 0));
            Assert.IsNotNull(RunRules.RemoveCard(r, c, 0), "the deck has a floor");
        }

        [Test]
        public void TheNewHeatCardsAreInTheRewardPool()
        {
            Assert.IsTrue(c.Rewards.Any(x => x.Card == "redline"));
            Assert.IsTrue(c.Rewards.Any(x => x.Card == "thermal_purge"));
        }
    }
}
