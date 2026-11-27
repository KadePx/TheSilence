using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Silence.Core.Battle;
using Silence.Core.Hex;
using Silence.Core.Run;
using Silence.Data;

namespace Silence.Tests
{
    /// <summary>Edicts of the Prime (the Ascendancy's order for each sector) and the Kessane Drift's own fights.</summary>
    public class EdictTests
    {
        RunContent c;

        [SetUp]
        public void Load() => c = ContentLoader.Load();

        static void Win(Battle b)
        {
            if (b.Phase == BattlePhase.Setup) b.Start();
            foreach (var e in b.Units.Where(u => u.Side == Side.Enemy && u.IsCapital && u.Alive).ToList()) b.Destroy(e);
        }

        /// <summary>A fresh run under the named Edict, standing at a first-layer fight.</summary>
        RunState Under(string kind, long seed = 4)
        {
            var r = RunRules.NewRun(seed, c);
            r.EdictChoices.Clear();
            r.Edict = new EdictState { Id = c.Edicts.First(e => e.Kind == kind).Id };
            Assert.IsNull(RunRules.Travel(r, c, RunRules.Choices(r)[0].Id));
            return r;
        }

        /// <summary>Put the fleet at the gate, which is where an Edict is judged.</summary>
        void ArriveAtGate(RunState r)
        {
            r.Phase = RunPhase.Map;
            var gate = r.Nodes.First(n => n.Kind == NodeKind.Boss);
            r.CurrentNode = r.Nodes.First(n => n.Next.Contains(gate.Id)).Id;
            Assert.IsNull(RunRules.Travel(r, c, gate.Id));
        }

        // ---------------------------------------------------------------- the offer

        [Test]
        public void TheAscendancyIsReadTwoEdictsAndTheConcordNone()
        {
            Assert.GreaterOrEqual(c.Edicts.Count, 6);
            foreach (var e in c.Edicts)
            {
                Assert.IsNotEmpty(e.Decree, e.Id);
                Assert.IsNotEmpty(e.Text, e.Id);
                Assert.IsNotEmpty(RunRules.EdictReward(c, e), $"{e.Id} pays nothing");
                Assert.Contains(e.Kind, new[] { "expend", "no_standoff", "kill_commander", "arrive_first", "austerity", "win_elite", "keep_escort" });
            }
            for (long seed = 1; seed <= 20; seed++)
            {
                var r = RunRules.NewRun(seed, c);
                Assert.AreEqual(2, r.EdictChoices.Count, $"seed {seed}");
                Assert.AreNotEqual(r.EdictChoices[0], r.EdictChoices[1]);
                Assert.IsEmpty(r.Edict.Id);
                if (!r.Nemesis.Hunting) Assert.IsFalse(r.EdictChoices.Any(id => c.Edict(id).Kind == "kill_commander"), "nobody to hunt");
            }
            var concord = RunRules.NewRun(3, ContentLoader.Load(ContentLoader.Concord));
            Assert.AreEqual(0, concord.EdictChoices.Count);
            Assert.IsEmpty(concord.Edict.Id);
        }

        [Test]
        public void AnEdictIsTakenOrItIsChosenForYou()
        {
            var r = RunRules.NewRun(2, c);
            string second = r.EdictChoices[1];
            Assert.IsNotNull(RunRules.AcceptEdict(r, c, 2));
            Assert.IsNull(RunRules.AcceptEdict(r, c, 1));
            Assert.AreEqual(second, r.Edict.Id);
            Assert.AreEqual(0, r.EdictChoices.Count);
            Assert.IsTrue(r.Edict.InForce);

            var hasty = RunRules.NewRun(2, c);
            string first = hasty.EdictChoices[0];
            Assert.IsNull(RunRules.Travel(hasty, c, RunRules.Choices(hasty)[0].Id));
            Assert.AreEqual(first, hasty.Edict.Id, "jumping without choosing takes the first");
        }

        // ---------------------------------------------------------------- each kind

        [Test]
        public void TheTitheOfBloodCountsOrdinalsExpendedInBattle()
        {
            var r = Under("expend");
            var e = c.Edict(r.Edict.Id);
            var b = RunRules.BuildBattle(r, c);
            b.Start();
            var target = b.LiveUnits(Side.Enemy).First(u => u.IsCapital);
            b.Flagship.Pos = target.Pos.Neighbor(HexDirection.W).Neighbor(HexDirection.W);
            b.Flagship.Facing = HexDirection.E;
            var lance = T.Give(b, PaperCards.LanceVolley, b.Flagship);
            if (b.CheckPlay(lance.Id, PlayTarget.At(target.Id, true)) == null)
            {
                Assert.IsNull(b.PlayCard(lance.Id, PlayTarget.At(target.Id, true)));
                Assert.AreEqual(3, b.OrdinalsExpended);
            }
            else b.OrdinalsExpended = 3;
            Win(b);
            RunRules.FinishBattle(r, c, b);
            Assert.AreEqual(3, r.Edict.Progress);
            Assert.IsFalse(RunRules.EdictObeyed(r, c));
            StringAssert.Contains("3 of " + e.Amount, RunRules.EdictProgress(r, c));

            r.Edict.Progress = e.Amount;
            int vats = r.MaxOrdinals;
            RunRules.TakeReward(r, -1);
            r.Ordinals = 5;
            ArriveAtGate(r);
            Assert.AreEqual(1, r.Edict.Verdict);
            Assert.AreEqual(vats + e.RewardMaxOrdinals, r.MaxOrdinals);
            Assert.AreEqual(r.MaxOrdinals, r.Ordinals, "the vats are refilled");
            Assert.AreEqual(0, r.Deck.Count(d => d.Card == "inquisitor_aboard"));
        }

        [Test]
        public void ABrokenEdictBringsAnInquisitorAboard()
        {
            var r = Under("no_standoff");
            var b = RunRules.BuildBattle(r, c);
            b.TurnLimit = 1;
            b.Start();
            b.EndTurn();
            Assert.AreEqual(BattleOutcome.Withdrawal, b.Outcome);
            RunRules.FinishBattle(r, c, b);
            Assert.IsTrue(r.Edict.Failed);
            Assert.AreEqual("Broken", RunRules.EdictProgress(r, c));
            Assert.AreEqual(1, r.Journal.Count(l => l.StartsWith("The Edict cannot now be obeyed")), "the journal says so at once, and once");

            r.Ordinals = 10;
            int scrap = r.Scrap;
            ArriveAtGate(r);
            Assert.AreEqual(2, r.Edict.Verdict);
            Assert.AreEqual(10 - c.Config.EdictPenaltyOrdinals, r.Ordinals);
            Assert.AreEqual(scrap, r.Scrap, "no reward");
            Assert.AreEqual(1, r.Deck.Count(d => d.Card == "inquisitor_aboard"));
            Assert.AreEqual(1, r.JunkCount(), "it is junk, so a yard can strip it out");

            // Judged once: nothing more happens to it.
            var gate = RunRules.BuildBattle(r, c);
            Assert.IsTrue(gate.Deck.DrawPile.Any(card => card.Def.Id == "inquisitor_aboard"));
        }

        [Test]
        public void TheInquisitorCostsACommandEveryTimeTheCardIsDrawn()
        {
            var b = T.Fleet(out var flag, out _);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            b.Start();
            Assert.AreEqual(3, b.Command);
            T.Give(b, PaperCards.InquisitorAboard, null);
            Assert.AreEqual(2, b.Command);
            Assert.IsNotNull(b.PlayCard(b.Deck.Hand.Last().Id, PlayTarget.None), "unplayable");
            b.Command = 0;
            T.Give(b, PaperCards.InquisitorAboard, null);
            Assert.AreEqual(0, b.Command, "never below nothing");
        }

        [Test]
        public void TheOtherOrdersAreJudgedOnWhatTheySay()
        {
            // Win an elite.
            var elite = Under("win_elite");
            Assert.IsFalse(RunRules.EdictObeyed(elite, c));
            elite.PendingEncounter = c.Encounters.First(e => e.Tier == 2 && e.Sector <= 1).Id;
            var b = RunRules.BuildBattle(elite, c);
            Win(b);
            RunRules.FinishBattle(elite, c, b);
            Assert.IsTrue(RunRules.EdictObeyed(elite, c));
            int hull = elite.Flagship.MaxHull;
            RunRules.ChooseRelic(elite, c, 0);
            RunRules.TakeReward(elite, -1);
            ArriveAtGate(elite);
            Assert.AreEqual(hull + c.Edict(elite.Edict.Id).RewardHull, elite.Flagship.MaxHull);

            // Keep the escort.
            var escort = Under("keep_escort");
            var b2 = RunRules.BuildBattle(escort, c);
            b2.Start();
            b2.Destroy(b2.Units.First(u => u.Side == Side.Player && u.IsCapital && !u.IsFlagship));
            Win(b2);
            RunRules.FinishBattle(escort, c, b2);
            Assert.IsTrue(escort.Edict.Failed);

            // Arrive first.
            var first = Under("arrive_first");
            first.ConcordProgress = 0;
            Assert.IsTrue(RunRules.EdictObeyed(first, c));
            first.ConcordProgress = first.PlayerProgress + 5;
            Assert.IsFalse(RunRules.EdictObeyed(first, c));
            StringAssert.Contains("ahead", RunRules.EdictProgress(first, c));

            // The commander.
            var hunt = Under("kill_commander");
            Assert.IsFalse(RunRules.EdictObeyed(hunt, c));
            hunt.Nemesis.Alive = false;
            hunt.Nemesis.LastOutcome = NemesisOutcome.Killed;
            Assert.IsTrue(RunRules.EdictObeyed(hunt, c));
            int relics = hunt.Relics.Count;
            ArriveAtGate(hunt);
            Assert.AreEqual(relics + 1, hunt.Relics.Count);
        }

        [Test]
        public void AnAuditOfStoresCountsYardServicesButNotDartsOrBeacons()
        {
            var r = Under("austerity");
            var e = c.Edict(r.Edict.Id);
            r.Phase = RunPhase.Shipyard;
            r.Shop = new ShopState { Active = true };
            r.Scrap = 500;
            r.Flagship.Hull -= 5;
            r.Darts = 0;
            r.Deck.Add(new DeckCard("fire_on_deck", -1));
            Assert.IsNull(RunRules.Repair(r, c, 0));
            Assert.IsNull(RunRules.RemoveJunk(r, c));
            Assert.IsNull(RunRules.BuyDart(r, c));
            Assert.IsNull(RunRules.Sabotage(r, c));
            Assert.IsNull(RunRules.LeaveShipyard(r, c));
            Assert.AreEqual(2, r.Edict.Progress);
            Assert.IsTrue(RunRules.EdictObeyed(r, c), $"{e.Amount} are allowed");

            r.Phase = RunPhase.Shipyard;
            r.Shop = new ShopState { Active = true };
            r.Deck.Add(new DeckCard("fire_on_deck", -1));
            Assert.IsNull(RunRules.RemoveJunk(r, c));
            RunRules.LeaveShipyard(r, c);
            Assert.IsFalse(RunRules.EdictObeyed(r, c));
        }

        [Test]
        public void EachSectorBringsANewEdictAndTheStateSurvivesASave()
        {
            var r = Under("no_standoff");
            r.Edict.Progress = 3;
            var loaded = RunJson.FromJson(RunJson.ToJson(r));
            Assert.AreEqual(r.Edict.Id, loaded.Edict.Id);
            Assert.AreEqual(3, loaded.Edict.Progress);

            r.Phase = RunPhase.Battle;
            r.CurrentNode = r.Nodes.First(n => n.Kind == NodeKind.Boss).Id;
            r.PendingEncounter = r.Current.Encounter;
            var b = RunRules.BuildBattle(r, c);
            Win(b);
            RunRules.FinishBattle(r, c, b);
            Assert.IsNull(RunRules.EnterNextSector(r, c, 0));
            Assert.AreEqual(2, r.EdictChoices.Count, "a new sector, a new order");
            Assert.IsEmpty(r.Edict.Id);
        }

        // ---------------------------------------------------------------- the Drift's own fights

        [Test]
        public void TheDriftFightsItsOwnBattlesAndTheReefNeverMeetsThem()
        {
            foreach (string id in ContentLoader.Factions)
            {
                var f = ContentLoader.Load(id);
                for (int tier = 0; tier <= 2; tier++)
                    Assert.GreaterOrEqual(f.Encounters.Count(e => e.Tier == tier && e.Sector == 2), 2, $"{id}: the Drift needs fights of its own at tier {tier}");

                for (long seed = 1; seed <= 8; seed++)
                {
                    var r = RunRules.NewRun(seed, f);
                    foreach (var n in r.Nodes.Where(n => n.Kind == NodeKind.Battle || n.Kind == NodeKind.Elite || n.Kind == NodeKind.Nemesis))
                        Assert.LessOrEqual(f.Encounter(n.Encounter).Sector, 1, $"{id} seed {seed}: a Drift fight in the Reef");

                    r.Phase = RunPhase.Battle;
                    r.CurrentNode = r.Nodes.First(n => n.Kind == NodeKind.Boss).Id;
                    r.PendingEncounter = r.Current.Encounter;
                    var b = RunRules.BuildBattle(r, f);
                    Win(b);
                    RunRules.FinishBattle(r, f, b);
                    Assert.IsNull(RunRules.EnterNextSector(r, f, 0));
                    foreach (var n in r.Nodes.Where(n => n.Kind == NodeKind.Battle || n.Kind == NodeKind.Elite || n.Kind == NodeKind.Nemesis))
                        Assert.AreEqual(2, f.Encounter(n.Encounter).Sector, $"{id} seed {seed}: a Reef fight in the Drift");
                }
            }
        }

        [Test]
        public void EveryDriftFightOpensInReachWithNobodyInTheRock()
        {
            foreach (string id in ContentLoader.Factions)
            {
                var f = ContentLoader.Load(id);
                foreach (var enc in f.Encounters.Where(e => e.Sector == 2))
                {
                    Assert.IsNotEmpty(enc.Intro, enc.Id);
                    if (enc.Tier < 3) Assert.IsNotEmpty(enc.Aftermath, enc.Id);
                    var r = RunRules.NewRun(5, f);
                    RunRules.Travel(r, f, RunRules.Choices(r)[0].Id);
                    r.Sector = 2;
                    r.Phase = RunPhase.Battle;
                    r.PendingEncounter = enc.Id;
                    r.PendingHazard = "";
                    var b = RunRules.BuildBattle(r, f);
                    Assert.AreEqual(b.Units.Count, b.Units.Select(u => u.Pos).Distinct().Count(), enc.Id);
                    foreach (var u in b.Units) Assert.IsFalse(b.Board.BlocksMovement(u.Pos), $"{enc.Id}: {u.Name} starts in terrain");
                    Assert.That(b.LiveUnits(Side.Enemy).Min(e => e.Pos.DistanceTo(b.Flagship.Pos)), Is.InRange(2, 5), enc.Id);
                    b.Start();
                    while (b.Outcome == BattleOutcome.Ongoing && b.Turn <= 60) SimpleBot.PlayTurn(b);
                    Assert.AreNotEqual(BattleOutcome.Ongoing, b.Outcome, $"{enc.Id} never ends");
                }
            }
        }
    }
}
