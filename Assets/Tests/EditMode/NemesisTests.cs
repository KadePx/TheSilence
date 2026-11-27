using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Silence.Core.Battle;
using Silence.Core.Hex;
using Silence.Core.Run;
using Silence.Data;

namespace Silence.Tests
{
    /// <summary>M3: the race changes routing (jump lanes, mines, sabotage), the nemesis comes back changed, relics.</summary>
    public class NemesisTests
    {
        RunContent c;

        [SetUp]
        public void Load() => c = ContentLoader.Load();

        static void Win(Battle b)
        {
            if (b.Phase == BattlePhase.Setup) b.Start();
            foreach (var e in b.Units.Where(u => u.Side == Side.Enemy && u.IsCapital && u.Alive).ToList()) b.Destroy(e);
        }

        int Reply(string id) => c.HailReplies.FindIndex(x => x.Id == id);

        static bool Leads(RunState r, int from, int target)
        {
            if (from == target) return true;
            var n = r.Node(from);
            return n.Next.Concat(n.Jumps).Any(next => Leads(r, next, target));
        }

        /// <summary>Walk to a node, winning every fight on the way at once. Stops the moment the node is entered.</summary>
        bool WalkTo(RunState r, int target)
        {
            for (int guard = 0; guard < 80 && r.Phase != RunPhase.Over; guard++)
            {
                switch (r.Phase)
                {
                    case RunPhase.Map:
                    {
                        if (r.CurrentNode == target) return true;
                        var next = RunRules.Choices(r).OrderBy(n => RunRules.IsJump(r, n.Id) ? 1 : 0).FirstOrDefault(n => Leads(r, n.Id, target));
                        if (next == null) return false;
                        Assert.IsNull(RunRules.Travel(r, c, next.Id));
                        if (next.Id == target) return true;
                        break;
                    }
                    case RunPhase.Hail:
                        Assert.IsNull(RunRules.ChooseHail(r, c, Reply("silence")));
                        break;
                    case RunPhase.Battle:
                    {
                        var b = RunRules.BuildBattle(r, c);
                        Win(b);
                        RunRules.FinishBattle(r, c, b);
                        break;
                    }
                    case RunPhase.Reward:
                        if (r.Reward.RelicChoices.Count > 0) RunRules.ChooseRelic(r, c, 0);
                        RunRules.TakeReward(r, -1);
                        break;
                    case RunPhase.Event:
                    {
                        var ev = c.Event(r.EventId);
                        for (int i = ev.Choices.Count - 1; i >= 0 && r.Phase == RunPhase.Event; i--) RunRules.ChooseEvent(r, c, i);
                        break;
                    }
                    case RunPhase.EventResult:
                        RunRules.CloseEvent(r);
                        break;
                    case RunPhase.Shipyard:
                        RunRules.LeaveShipyard(r, c);
                        break;
                }
            }
            return false;
        }

        /// <summary>A run standing in front of its nemesis, hail open.</summary>
        RunState AtHail(long firstSeed = 1)
        {
            for (long seed = firstSeed; seed < firstSeed + 50; seed++)
            {
                var r = RunRules.NewRun(seed, c);
                if (r.Nemesis.Node < 0) continue;
                if (WalkTo(r, r.Nemesis.Node) && r.Phase == RunPhase.Hail) return r;
            }
            Assert.Fail("no run reached its nemesis");
            return null;
        }

        Battle Fight(RunState r, string reply = "silence")
        {
            Assert.IsNull(RunRules.ChooseHail(r, c, Reply(reply)));
            Assert.AreEqual(RunPhase.Battle, r.Phase);
            return RunRules.BuildBattle(r, c);
        }

        // ---------------------------------------------------------------- content

        [Test]
        public void CommandersRepliesAndRelicsLoadAndResolve()
        {
            Assert.AreEqual(3, c.Nemeses.Count);
            Assert.AreEqual(4, c.HailReplies.Count);
            foreach (var n in c.Nemeses)
            {
                Assert.AreEqual(UnitKind.Capital, UnitLibrary.Get(n.Ship).Kind, n.Id);
                Assert.IsTrue(c.Relic(n.Trophy).Trophy, n.Id);
                foreach (var rank in n.Escorts)
                {
                    Assert.IsTrue(rank.Any(u => UnitLibrary.Get(u.Unit).Kind == UnitKind.Capital), $"{n.Id}: every rank needs a capital escort so a commander who breaks off does not end the fight alone");
                    foreach (var u in rank) Assert.AreNotEqual(n.Ship, u.Unit);
                }
                foreach (string line in new[] { n.HailFirst, n.HailEscaped, n.HailStandoff, n.HailFinal }) Assert.IsNotEmpty(line, n.Id);
            }
            foreach (var reply in c.HailReplies) Assert.IsNotEmpty(RunRules.HailEffect(c, reply));
            Assert.IsTrue(c.Relics.Count(x => !x.Trophy) >= 5);
            Assert.AreEqual(c.Relics.Count, c.Relics.Select(x => x.Id).Distinct().Count());
        }

        // ---------------------------------------------------------------- the race

        [Test]
        public void JumpLanesSkipExactlyOneLayer()
        {
            for (long seed = 1; seed <= 40; seed++)
            {
                var r = RunRules.NewRun(seed, c);
                var jumps = r.Nodes.SelectMany(n => n.Jumps.Select(j => (from: n, to: r.Node(j)))).ToList();
                Assert.LessOrEqual(jumps.Count, c.Config.JumpLanes, $"seed {seed}");
                foreach (var (from, to) in jumps)
                {
                    Assert.AreEqual(from.Layer + 2, to.Layer, $"seed {seed}");
                    Assert.Less(to.Layer, r.BossLayer, $"seed {seed}: a jump lane never lands on the gate");
                }
            }
        }

        RunState AtJumpLane(out int target)
        {
            for (long seed = 1; seed < 80; seed++)
            {
                var r = RunRules.NewRun(seed, c);
                var from = r.Nodes.FirstOrDefault(n => n.Jumps.Count > 0);
                if (from == null || !WalkTo(r, from.Id)) continue;
                // Finish whatever the node itself opened.
                for (int guard = 0; guard < 10 && r.Phase != RunPhase.Map && r.Phase != RunPhase.Over; guard++) WalkTo(r, -1);
                if (r.Phase != RunPhase.Map || r.CurrentNode != from.Id) continue;
                target = from.Jumps[0];
                return r;
            }
            Assert.Fail("no run reached a jump lane");
            target = -1;
            return null;
        }

        [Test]
        public void AJumpLaneGainsANodeOnTheRivalsAndCostsHull()
        {
            var r = AtJumpLane(out int target);
            foreach (var s in r.Ships) s.Hull = s.MaxHull;
            Assert.IsTrue(RunRules.IsJump(r, target));
            Assert.IsTrue(RunRules.Choices(r).Any(n => n.Id == target));
            int progress = r.PlayerProgress, concord = r.ConcordProgress;
            Assert.IsNull(RunRules.Travel(r, c, target));
            Assert.AreEqual(progress + 20, r.PlayerProgress);
            Assert.LessOrEqual(r.ConcordProgress - concord, c.Config.ConcordRate + c.Config.RivalSpread + 2 * c.Config.NemesisDodgeDelay, "one jump of the rivals' time, plus at most a commander slipping past and falling back");
            foreach (var s in r.Ships) Assert.AreEqual(s.MaxHull - c.Config.JumpHullCost, s.Hull, s.Name);
            Assert.IsNotEmpty(r.TravelNote);
        }

        [Test]
        public void OldGateCodesMakeJumpLanesFree()
        {
            var r = AtJumpLane(out int target);
            foreach (var s in r.Ships) s.Hull = s.MaxHull;
            RunRules.GainRelic(r, c, "gate_codes");
            Assert.AreEqual(0, RunRules.JumpCost(r, c));
            Assert.IsNull(RunRules.Travel(r, c, target));
            foreach (var s in r.Ships) Assert.AreEqual(s.MaxHull, s.Hull, s.Name);
        }

        [Test]
        public void AJumpLaneNeverKillsAShip()
        {
            var r = AtJumpLane(out int target);
            r.Flagship.Hull = 2;
            Assert.IsNull(RunRules.Travel(r, c, target));
            Assert.AreEqual(1, r.Flagship.Hull);
            Assert.IsTrue(r.Flagship.Alive);
        }

        [Test]
        public void LanesAreMinedWhileTheConcordLeadsByANode()
        {
            var r = RunRules.NewRun(3, c);
            Assert.IsFalse(RunRules.LanesMined(r, c), "nothing is mined before the first jump");
            WalkTo(r, RunRules.Choices(r)[0].Id);
            WalkTo(r, -1);
            Assert.AreEqual(RunPhase.Map, r.Phase);
            foreach (var s in r.Ships) s.Hull = s.MaxHull;

            r.ConcordProgress = r.PlayerProgress + c.Config.MineLead - 1;
            Assert.IsFalse(RunRules.LanesMined(r, c));
            r.ConcordProgress = r.PlayerProgress + c.Config.MineLead;
            Assert.IsTrue(RunRules.LanesMined(r, c));

            var next = RunRules.Choices(r).First(n => !RunRules.IsJump(r, n.Id));
            Assert.IsNull(RunRules.Travel(r, c, next.Id));
            foreach (var s in r.Ships) Assert.AreEqual(s.MaxHull - c.Config.MineDamage, s.Hull, s.Name);
            StringAssert.Contains("mines", r.TravelNote);

            // The Lost Compass reads the minefield: the lanes are clear to this fleet.
            r.Phase = RunPhase.Map;
            r.ConcordProgress = r.PlayerProgress + c.Config.MineLead + 20;
            Assert.IsTrue(RunRules.LanesMined(r, c));
            RunRules.GainRelic(r, c, "lost_compass");
            Assert.IsFalse(RunRules.LanesMined(r, c));
        }

        [Test]
        public void FalseBeaconsSetTheRivalsBackOncePerYard()
        {
            var r = RunRules.NewRun(2, c);
            var yard = r.Nodes.First(n => n.Kind == NodeKind.Shipyard);
            Assert.IsTrue(WalkTo(r, yard.Id));
            Assert.AreEqual(RunPhase.Shipyard, r.Phase);
            r.Scrap = 100;
            r.ConcordProgress = 60;
            r.ChorusProgress = 5;
            Assert.IsNull(RunRules.Sabotage(r, c));
            Assert.AreEqual(60 - c.Config.SabotageAmount, r.ConcordProgress);
            Assert.AreEqual(0, r.ChorusProgress, "progress never goes below the start");
            Assert.AreEqual(100 - c.Config.SabotageCost, r.Scrap);
            Assert.AreEqual(0, r.Shop.Purchases, "seeding beacons takes no dock time");
            Assert.IsNotNull(RunRules.Sabotage(r, c));
            Assert.AreEqual(100 - c.Config.SabotageCost, r.Scrap);
        }

        // ---------------------------------------------------------------- the nemesis

        [Test]
        public void EveryRunHasACommanderWaitingOnABattleNodeAhead()
        {
            var seen = new HashSet<string>();
            for (long seed = 1; seed <= 60; seed++)
            {
                var r = RunRules.NewRun(seed, c);
                Assert.IsTrue(r.Nemesis.Hunting, $"seed {seed}");
                seen.Add(r.Nemesis.Id);
                if (r.Nemesis.FellBack) continue;
                var node = r.Node(r.Nemesis.Node);
                Assert.AreEqual(NodeKind.Nemesis, node.Kind, $"seed {seed}");
                Assert.GreaterOrEqual(node.Layer, 2, $"seed {seed}: the opening is left alone");
                Assert.LessOrEqual(node.Layer, r.BossLayer - 2, $"seed {seed}");
                Assert.AreEqual(1, r.Nodes.Count(n => n.Kind == NodeKind.Nemesis), $"seed {seed}");
            }
            Assert.AreEqual(3, seen.Count, "every commander turns up across sixty seeds");
        }

        [Test]
        public void ReachingTheCommanderOpensAHailAndTheReplyStartsTheFight()
        {
            var r = AtHail();
            Assert.AreEqual(RunPhase.Hail, r.Phase);
            Assert.IsNotNull(RunRules.ChooseHail(r, c, 99));
            var b = Fight(r);
            Assert.AreEqual(RunRules.NemesisEncounterId, r.PendingEncounter);
            var def = c.Nemesis(r.Nemesis.Id);
            var commanders = b.Units.Where(u => u.IsCommander).ToList();
            Assert.AreEqual(1, commanders.Count);
            Assert.AreEqual(UnitLibrary.Get(def.Ship).Name, commanders[0].Name);
            Assert.AreEqual(commanders[0].MaxHull * c.Config.NemesisFleePercent / 100, commanders[0].FleesAtHull);
            Assert.AreEqual(1 + def.Escorts[0].Count, b.Units.Count(u => u.Side == Side.Enemy));
        }

        [Test]
        public void ACommanderWhoBreaksOffComesBackStrongerAndRefittedAgainstWhatHurt()
        {
            var r = AtHail();
            var def = c.Nemesis(r.Nemesis.Id);
            int oldNode = r.Nemesis.Node, layer = r.Current.Layer;
            var b = Fight(r);
            b.Start();
            var ship = b.Units.Find(u => u.IsCommander);
            b.CommanderDamage["lance"] = 12;
            b.CommanderDamage["guns"] = 5;
            ship.Hull = ship.FleesAtHull;
            Assert.IsNull(b.EndTurn());
            Assert.IsTrue(ship.Escaped);
            Assert.IsFalse(ship.Alive);
            Assert.IsTrue(T.Logged(b, EventKind.Escaped));
            Assert.AreEqual(BattleOutcome.Ongoing, b.Outcome, "the escorts fight on");
            Win(b);
            Assert.AreEqual(BattleOutcome.Victory, b.Outcome);
            RunRules.FinishBattle(r, c, b);

            var n = r.Nemesis;
            Assert.IsTrue(n.Hunting);
            Assert.AreEqual(NemesisOutcome.Escaped, n.LastOutcome);
            Assert.AreEqual(2, n.Rank);
            Assert.AreEqual(1, n.Grudge);
            Assert.AreEqual(1, n.Meetings);
            Assert.AreEqual("lance", n.Adaptation);
            Assert.AreEqual(1, n.Scars.Count);
            Assert.AreNotEqual(oldNode, n.Node);
            Assert.IsTrue(n.FellBack || r.Node(n.Node).Layer >= layer + 2);
            Assert.AreEqual(RunPhase.Reward, r.Phase);
            Assert.IsEmpty(r.Reward.Relic, "no trophy from a commander who got away");
            Assert.IsFalse(r.Relics.Contains(def.Trophy));

            // The return match: bigger, escorted by the next rank, and baffled against lances.
            RunRules.TakeReward(r, -1);
            r.Phase = RunPhase.Hail;
            var again = Fight(r);
            var back = again.Units.Find(u => u.IsCommander);
            Assert.AreEqual(UnitLibrary.Get(def.Ship).Hull + c.Config.NemesisRankHull, back.MaxHull);
            Assert.AreEqual(3, back.LanceWard);
            Assert.AreEqual(1 + def.Escorts[1].Count, again.Units.Count(u => u.Side == Side.Enemy));
        }

        [Test]
        public void KillingTheCommanderOutrightTakesTheirTrophyAndBlindsTheConcord()
        {
            var r = AtHail();
            var def = c.Nemesis(r.Nemesis.Id);
            var b = Fight(r);
            r.ConcordProgress = 50;
            Win(b);
            RunRules.FinishBattle(r, c, b);
            Assert.IsFalse(r.Nemesis.Alive);
            Assert.IsFalse(r.Nemesis.Hunting);
            Assert.AreEqual(NemesisOutcome.Killed, r.Nemesis.LastOutcome);
            Assert.AreEqual(50 - c.Config.NemesisSlow, r.ConcordProgress);
            Assert.Contains(def.Trophy, r.Relics);
            Assert.AreEqual(def.Trophy, r.Reward.Relic);
            Assert.AreEqual(c.Config.RewardCards + 1, r.Reward.Cards.Count);
            Assert.AreEqual(0, r.Nodes.Count(n => n.Kind == NodeKind.Nemesis && !n.Visited));
        }

        [Test]
        public void AStandOffAlsoBringsTheCommanderBack()
        {
            var r = AtHail();
            var b = Fight(r);
            b.TurnLimit = 1;
            b.Start();
            b.EndTurn();
            Assert.AreEqual(BattleOutcome.Withdrawal, b.Outcome);
            int concord = r.ConcordProgress;
            RunRules.FinishBattle(r, c, b);
            Assert.AreEqual(NemesisOutcome.Standoff, r.Nemesis.LastOutcome);
            Assert.AreEqual(2, r.Nemesis.Rank);
            Assert.AreEqual(concord + c.Config.WithdrawalDelay, r.ConcordProgress);
            Assert.AreEqual(RunPhase.Map, r.Phase);
        }

        [Test]
        public void AtRankThreeTheCommanderFightsToTheEnd()
        {
            var r = AtHail();
            r.Nemesis.Rank = 3;
            var b = Fight(r);
            var ship = b.Units.Find(u => u.IsCommander);
            Assert.AreEqual(0, ship.FleesAtHull);
            b.Start();
            ship.Hull = 1;
            b.EndTurn();
            Assert.IsFalse(ship.Escaped);
        }

        [Test]
        public void OpenFireBuysACommandAndMaxesTheGrudge()
        {
            var r = AtHail();
            var b = Fight(r, "fire");
            Assert.AreEqual(3, r.Nemesis.Grudge);
            b.Start();
            Assert.AreEqual(b.CommandPerTurn + 1, b.Command);
            var ship = b.Units.Find(u => u.IsCommander);
            ship.Hull = ship.FleesAtHull;
            b.EndTurn();
            if (b.Outcome == BattleOutcome.Ongoing) Assert.AreEqual(b.CommandPerTurn, b.Command, "only on turn 1");
            Win(b);
            RunRules.FinishBattle(r, c, b);
            Assert.AreEqual(3, r.Nemesis.Rank, "a grudge of 3 skips straight to rank III");
        }

        [Test]
        public void TheEdictEnragesThemAndPaysMore()
        {
            var r = AtHail();
            var b = Fight(r, "edict");
            Assert.AreEqual(c.Config.HailEdictScrapPercent, r.PendingRewardPercent);
            Assert.AreEqual(c.Config.HailEdictDamage, b.EnemyDamageBonus);
            Assert.IsTrue(b.EnemyFocusFlagship);
            b.Start();
            foreach (var i in b.Intents.Where(i => i.Kind == IntentKind.Broadside || i.Kind == IntentKind.CraftAttack))
            {
                Assert.AreEqual(b.Flagship.Id, i.TargetId);
                Assert.AreEqual(c.Config.HailEdictDamage, i.Bonus);
            }
        }

        [Test]
        public void BoardingTheRelayCostsOrdinalsAndSilencesTheEscorts()
        {
            var r = AtHail();
            r.Ordinals = 3;
            Assert.IsNotNull(RunRules.ChooseHail(r, c, Reply("board")));
            Assert.AreEqual(RunPhase.Hail, r.Phase);
            r.Ordinals = 10;
            var b = Fight(r, "board");
            Assert.AreEqual(6, r.Ordinals);
            Assert.AreEqual(6, b.Ordinals);
            b.Start();
            var ship = b.Units.Find(u => u.IsCommander);
            foreach (var i in b.Intents) Assert.AreEqual(ship.Id, i.UnitId, "only the commander has orders on turn 1");
            for (int turn = 1; turn <= c.Config.HailBoardTurns && b.Outcome == BattleOutcome.Ongoing; turn++) b.EndTurn();
            if (b.Outcome == BattleOutcome.Ongoing) Assert.IsTrue(b.Intents.Any(i => i.UnitId != ship.Id), "the escorts wake up afterwards");
        }

        [Test]
        public void SlippingPastTheCommanderMovesThemAheadAndTellsTheConcord()
        {
            for (long seed = 1; seed < 80; seed++)
            {
                var r = RunRules.NewRun(seed, c);
                if (r.Nemesis.Node < 0) continue;
                var his = r.Node(r.Nemesis.Node);
                var other = r.Nodes.FirstOrDefault(n => n.Layer == his.Layer && n.Id != his.Id);
                if (other == null) continue;
                // Stop one node short, then step past him by hand so the cost of that one jump can be read.
                var before = r.Nodes.FirstOrDefault(n => n.Layer == his.Layer - 1 && n.Next.Contains(other.Id));
                if (before == null || !WalkTo(r, before.Id)) continue;
                for (int guard = 0; guard < 10 && r.Phase != RunPhase.Map && r.Phase != RunPhase.Over; guard++) WalkTo(r, -1);
                if (r.Phase != RunPhase.Map || !r.Nemesis.Hunting || r.Nemesis.Node != his.Id) continue;

                int concord = r.ConcordProgress;
                Assert.IsNull(RunRules.Travel(r, c, other.Id));
                Assert.AreEqual(NemesisOutcome.Dodged, r.Nemesis.LastOutcome);
                Assert.AreNotEqual(his.Id, r.Nemesis.Node);
                Assert.AreEqual(NodeKind.Battle, his.Kind, "the lane he left is an ordinary fight again");
                Assert.AreEqual(1, r.Nemesis.Rank, "dodging does not make him stronger");
                Assert.GreaterOrEqual(r.ConcordProgress - concord, c.Config.ConcordRate + c.Config.NemesisDodgeDelay);
                Assert.IsTrue(r.Nemesis.FellBack || r.Node(r.Nemesis.Node).Layer >= other.Layer + 2);
                return;
            }
            Assert.Fail("no run let the fleet slip past its nemesis");
        }

        [Test]
        public void ACommanderWithNoLaneLeftFallsBackAndTheConcordGainsGround()
        {
            var r = AtHail();
            // Every fight further on is taken away, so there is nowhere left to wait.
            foreach (var n in r.Nodes.Where(n => n.Layer > r.Current.Layer && (n.Kind == NodeKind.Battle || n.Kind == NodeKind.Elite))) n.Kind = NodeKind.Event;
            var b = Fight(r);
            b.Start();
            var ship = b.Units.Find(u => u.IsCommander);
            ship.Hull = ship.FleesAtHull;
            b.EndTurn();
            Win(b);
            int concord = r.ConcordProgress;
            RunRules.FinishBattle(r, c, b);
            Assert.IsTrue(r.Nemesis.FellBack);
            Assert.IsFalse(r.Nemesis.Hunting);
            Assert.IsTrue(r.Nemesis.Alive);
            Assert.AreEqual(-1, r.Nemesis.Node);
            Assert.AreEqual(concord + c.Config.NemesisDodgeDelay, r.ConcordProgress);
        }

        // ---------------------------------------------------------------- battle rules behind it

        [Test]
        public void LanceBafflesTakeThreeOffEveryLanceHit()
        {
            var b = T.Fleet(out var flag, out _);
            var target = b.AddUnit(T.Dummy(), Side.Enemy, 5, 3, HexDirection.W, 0);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            target.LanceWard = 3;
            b.Start();
            var lance = T.Give(b, PaperCards.LanceVolley, flag);
            Assert.IsNull(b.PlayCard(lance.Id, PlayTarget.At(target.Id)));
            Assert.AreEqual(20 - (9 - 3), target.Hull);
        }

        [Test]
        public void PointDefenceTakesOneOffEveryDartAttackAndDamageIsTalliedBySource()
        {
            var b = T.Fleet(out var flag, out _);
            var target = b.AddUnit(T.Dummy(), Side.Enemy, 5, 3, HexDirection.W, 0);
            b.AddUnit(T.Dummy(), Side.Enemy, 8, 0, HexDirection.W, 0);
            target.IsCommander = true;
            target.CraftWard = 1;
            var dart = b.AddUnit(PaperUnits.Dart, Side.Player, 4, 3, HexDirection.E, 0);
            b.Start();
            Assert.IsNull(b.CommandCraft(dart.Id, dart.Pos, target.Id));
            Assert.AreEqual(20 - (PaperUnits.Dart.AttackDamage - 1), target.Hull);
            var lance = T.Give(b, PaperCards.LanceVolley, flag);
            Assert.IsNull(b.PlayCard(lance.Id, PlayTarget.At(target.Id)));
            var guns = T.Give(b, PaperCards.BroadsideBattery, flag);
            b.TurnShip(flag, 1);
            if (b.CheckPlay(guns.Id, PlayTarget.At(target.Id)) == null) b.PlayCard(guns.Id, PlayTarget.At(target.Id));
            Assert.AreEqual(1, b.CommanderDamage["craft"]);
            Assert.AreEqual(9, b.CommanderDamage["lance"]);
        }

        [Test]
        public void ACommanderBreaksOffOnlyAfterTheEnemyHasActed()
        {
            var b = T.Fleet(out _, out _);
            var ship = b.AddUnit(T.Dummy(), Side.Enemy, 7, 3, HexDirection.W, 0);
            ship.FleesAtHull = 8;
            b.Start();
            ship.Hull = 9;
            b.EndTurn();
            Assert.IsTrue(ship.Alive, "above the threshold they stay");
            ship.Hull = 8;
            Assert.IsTrue(ship.Alive, "nothing happens in the player's own turn");
            b.EndTurn();
            Assert.IsTrue(ship.Escaped);
            Assert.AreEqual(BattleOutcome.Victory, b.Outcome, "with nobody else left the field is yours");
        }

        // ---------------------------------------------------------------- relics

        [Test]
        public void RelicsChangeTheNumbersTheySayTheyChange()
        {
            var r = RunRules.NewRun(6, c);
            int hull = r.Flagship.MaxHull, darts = r.MaxDarts, repair = RunRules.FieldRepairAmount(r, c);
            RunRules.GainRelic(r, c, "koyls_plate");
            RunRules.GainRelic(r, c, "rasks_beacon");
            RunRules.GainRelic(r, c, "quartermasters_writ");
            Assert.AreEqual(hull + 6, r.Flagship.MaxHull);
            Assert.AreEqual(hull + 6, r.Flagship.Hull);
            Assert.AreEqual(darts + 2, r.MaxDarts);
            Assert.AreEqual(darts + 2, r.Darts);
            Assert.AreEqual(repair + 3, RunRules.FieldRepairAmount(r, c));

            RunRules.Travel(r, c, RunRules.Choices(r)[0].Id);
            var plain = RunRules.BuildBattle(r, c);
            RunRules.GainRelic(r, c, "marrows_ledger");
            RunRules.GainRelic(r, c, "cold_core");
            RunRules.GainRelic(r, c, "aegis_lattice");
            var b = RunRules.BuildBattle(r, c);
            Assert.AreEqual(plain.Flagship.HeatCap + 2, b.Flagship.HeatCap);
            Assert.AreEqual(plain.Flagship.MaxShields + 1, b.Flagship.MaxShields);
            b.Start();
            Assert.AreEqual(b.CommandPerTurn + 1, b.Command);
        }

        [Test]
        public void ReefChartsSlowBothRivalsOnEveryJump()
        {
            var a = RunRules.NewRun(9, c);
            var b = RunRules.NewRun(9, c);
            RunRules.GainRelic(b, c, "reef_charts");
            int first = RunRules.Choices(a)[0].Id;
            RunRules.Travel(a, c, first);
            RunRules.Travel(b, c, first);
            Assert.AreEqual(a.ConcordProgress - 1, b.ConcordProgress);
            Assert.AreEqual(a.ChorusProgress - 1, b.ChorusProgress);
        }

        [Test]
        public void AnEliteOffersTwoRelicsAndOneIsTakenBeforeAnythingElse()
        {
            for (long seed = 1; seed < 60; seed++)
            {
                var r = RunRules.NewRun(seed, c);
                var elite = r.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Elite);
                if (elite == null || !WalkTo(r, elite.Id) || r.Phase != RunPhase.Battle) continue;
                int had = r.Relics.Count;
                var b = RunRules.BuildBattle(r, c);
                Win(b);
                RunRules.FinishBattle(r, c, b);
                Assert.AreEqual(had, r.Relics.Count, "nothing is taken until it is chosen");
                Assert.AreEqual(2, r.Reward.RelicChoices.Count);
                Assert.AreNotEqual(r.Reward.RelicChoices[0], r.Reward.RelicChoices[1]);
                foreach (string id in r.Reward.RelicChoices)
                {
                    Assert.IsFalse(c.Relic(id).Trophy);
                    Assert.IsFalse(r.Relics.Contains(id));
                }
                Assert.AreEqual(c.Config.RewardCards + 1, r.Reward.Cards.Count);
                Assert.IsNotNull(RunRules.TakeReward(r, 0), "the relic comes first");
                Assert.IsNotNull(RunRules.TakeFieldRepair(r, c));
                Assert.IsNotNull(RunRules.ChooseRelic(r, c, 2));
                string pick = r.Reward.RelicChoices[1];
                Assert.IsNull(RunRules.ChooseRelic(r, c, 1));
                Assert.Contains(pick, r.Relics);
                Assert.AreEqual(pick, r.Reward.Relic);
                Assert.AreEqual(0, r.Reward.RelicChoices.Count);
                Assert.IsNull(RunRules.TakeReward(r, 0));
                Assert.AreEqual(RunPhase.Map, r.Phase);
                return;
            }
            Assert.Fail("no run reached an elite");
        }

        // ---------------------------------------------------------------- hazards

        RunState AtHazard(string hazard)
        {
            var r = RunRules.NewRun(5, c);
            Assert.IsNull(RunRules.Travel(r, c, RunRules.Choices(r)[0].Id));
            if (r.Phase != RunPhase.Battle) { r.PendingEncounter = c.Encounters.First(e => e.Tier == 1).Id; r.Phase = RunPhase.Battle; }
            r.PendingHazard = hazard;
            return r;
        }

        [Test]
        public void SomeFightsPastTheOpeningAreInAHazardAndPayMore()
        {
            int hazards = 0, fights = 0;
            var kinds = new HashSet<string>();
            for (long seed = 1; seed <= 40; seed++)
            {
                var r = RunRules.NewRun(seed, c);
                foreach (var n in r.Nodes)
                {
                    if (n.Hazard == "") continue;
                    Assert.IsTrue(n.Kind == NodeKind.Battle || n.Kind == NodeKind.Elite || n.Kind == NodeKind.Nemesis, $"seed {seed}");
                    Assert.GreaterOrEqual(n.Layer, 2, $"seed {seed}: the opening is clear space");
                    Assert.IsNotEmpty(Hazards.Name(n.Hazard));
                    Assert.IsNotEmpty(Hazards.Text(n.Hazard));
                    kinds.Add(n.Hazard);
                    hazards++;
                }
                fights += r.Nodes.Count(n => (n.Kind == NodeKind.Battle || n.Kind == NodeKind.Elite) && n.Layer >= 2);
            }
            Assert.AreEqual(Hazards.All.Length, kinds.Count);
            Assert.That(hazards * 100 / fights, Is.InRange(12, 40));

            var at = RunRules.NewRun(3, c);
            var node = at.Nodes.First(n => n.Hazard != "" && n.Kind != NodeKind.Nemesis);
            Assert.IsTrue(WalkTo(at, node.Id));
            Assert.AreEqual(node.Hazard, at.PendingHazard);
            Assert.AreEqual(c.Config.HazardScrapPercent, at.PendingRewardPercent);
        }

        [Test]
        public void AnIonStormStopsEveryShieldFromComingBack()
        {
            var r = AtHazard("ion_storm");
            var b = RunRules.BuildBattle(r, c);
            Assert.AreEqual("Ion storm", b.Hazard);
            b.Start();
            var enemy = b.LiveUnits(Side.Enemy).First(u => u.IsCapital);
            enemy.Shields[0] = 0;
            b.Flagship.Shields[1] = 0;
            b.EndTurn();
            if (enemy.Alive) Assert.AreEqual(0, enemy.Shields[0]);
            Assert.AreEqual(0, b.Flagship.Shields[1]);

            var clear = RunRules.BuildBattle(AtHazard(""), c);
            clear.Start();
            clear.Flagship.Shields[2] = 0;
            clear.EndTurn();
            Assert.Greater(clear.Flagship.Shields[2], 0);
        }

        [Test]
        public void ANebulaShortensEveryWeaponOnBothSides()
        {
            var b = T.Fleet(out var flag, out _);
            b.RangePenalty = 1;
            var target = b.AddUnit(PaperUnits.Frigate, Side.Enemy, 6, 3, HexDirection.W, 0);
            b.Start();
            var lance = T.Give(b, PaperCards.LanceVolley, flag);
            StringAssert.Contains("range", b.CheckPlay(lance.Id, PlayTarget.At(target.Id)), "four hexes is now out of the lance's reach");
            target.Pos = T.At(5, 3);
            Assert.IsNull(b.CheckPlay(lance.Id, PlayTarget.At(target.Id)));
            Assert.AreEqual(1, b.Reach(1), "nothing drops below 1");

            target.CycleIndex = 1; // its broadside, range 3 on a clear day
            b.EndTurn();
            var fire = b.Intents.FirstOrDefault(i => i.Kind == IntentKind.Broadside);
            if (fire != null) Assert.AreEqual(2, fire.Range);

            var run = RunRules.BuildBattle(AtHazard("nebula"), c);
            Assert.AreEqual(1, run.RangePenalty);
        }

        [Test]
        public void ADenseFieldDoublesTheRockAndLeavesEveryShipClear()
        {
            int Rocks(Battle b)
            {
                int n = 0;
                for (int col = 0; col < b.Board.Width; col++)
                for (int row = 0; row < b.Board.Height; row++)
                    if (b.Board.BlocksMovement(T.At(col, row))) n++;
                return n;
            }
            var clear = RunRules.BuildBattle(AtHazard(""), c);
            var dense = RunRules.BuildBattle(AtHazard("asteroid_field"), c);
            Assert.GreaterOrEqual(Rocks(dense), Rocks(clear) + 4);
            foreach (var u in dense.Units) Assert.IsFalse(dense.Board.BlocksMovement(u.Pos), u.Name);
        }

        [Test]
        public void HazardsNeverReachTheGateOrANemesisFight()
        {
            var r = AtHail();
            r.PendingHazard = "nebula";
            Assert.IsNull(RunRules.ChooseHail(r, c, Reply("silence")));
            Assert.AreEqual(0, RunRules.BuildBattle(r, c).RangePenalty);
            r.PendingEncounter = c.Encounters.First(e => e.Tier == 3).Id;
            r.PendingHazard = "ion_storm";
            Assert.IsFalse(RunRules.BuildBattle(r, c).NoShieldRegen);
        }

        // ---------------------------------------------------------------- story

        [Test]
        public void TheBriefingIsShownOnceAndBeingOvertakenIsRemarkedOnOnce()
        {
            foreach (string id in ContentLoader.Factions)
            {
                var f = ContentLoader.Load(id);
                Assert.Greater(f.Faction.Briefing.Length, 200, id);
                Assert.IsNotEmpty(f.Faction.RivalPasses, id);
                Assert.IsNotEmpty(f.Faction.ThirdPasses, id);
                foreach (var n in f.Nemeses) Assert.IsNotEmpty(n.Epitaph, n.Id);

                var r = RunRules.NewRun(4, f);
                Assert.IsFalse(r.BriefingSeen);
                RunRules.DismissBriefing(r);
                Assert.IsTrue(RunJson.FromJson(RunJson.ToJson(r)).BriefingSeen);

                RunRules.Travel(r, f, RunRules.Choices(r)[0].Id);
                r.Phase = RunPhase.Map;
                r.ConcordProgress = r.PlayerProgress + 30;
                r.ChorusProgress = r.PlayerProgress + 30;
                RunRules.Travel(r, f, RunRules.Choices(r)[0].Id);
                Assert.IsTrue(r.RivalPassed && r.ThirdPassed);
                Assert.Contains(f.Faction.RivalPasses, r.Journal);
                Assert.Contains(f.Faction.ThirdPasses, r.Journal);
                int lines = r.Journal.Count(l => l == f.Faction.RivalPasses);
                r.Phase = RunPhase.Map;
                RunRules.Travel(r, f, RunRules.Choices(r)[0].Id);
                Assert.AreEqual(lines, r.Journal.Count(l => l == f.Faction.RivalPasses), "said once");
            }
        }

        [Test]
        public void AnEventCanHandOverARelic()
        {
            foreach (string id in ContentLoader.Factions)
            {
                var f = ContentLoader.Load(id);
                var ev = f.Event("black_market");
                int buy = ev.Choices.FindIndex(x => x.Outcomes.Any(o => o.Kind == OutcomeKind.GainRelic));
                var r = RunRules.NewRun(2, f);
                r.Phase = RunPhase.Event;
                r.EventId = ev.Id;
                r.Scrap = 10;
                Assert.IsNotNull(RunRules.ChooseEvent(r, f, buy), "it is not free");
                r.Scrap = 60;
                Assert.IsNull(RunRules.ChooseEvent(r, f, buy));
                Assert.AreEqual(1, r.Relics.Count);
                Assert.IsFalse(f.Relic(r.Relics[0]).Trophy);
                StringAssert.Contains(f.Relic(r.Relics[0]).Name, r.EventResult);
                Assert.GreaterOrEqual(f.Events.Count, 9, id);
            }
        }

        // ---------------------------------------------------------------- saves and whole runs

        [Test]
        public void TheNemesisRelicsAndJumpLanesSurviveASave()
        {
            var r = AtHail();
            var b = Fight(r);
            b.Start();
            var ship = b.Units.Find(u => u.IsCommander);
            b.CommanderDamage["craft"] = 4;
            ship.Hull = ship.FleesAtHull;
            b.EndTurn();
            Win(b);
            RunRules.FinishBattle(r, c, b);
            RunRules.GainRelic(r, c, "tithe_ledger");

            var loaded = RunJson.FromJson(RunJson.ToJson(r));
            Assert.AreEqual(r.Nemesis.Id, loaded.Nemesis.Id);
            Assert.AreEqual(r.Nemesis.Rank, loaded.Nemesis.Rank);
            Assert.AreEqual(r.Nemesis.Node, loaded.Nemesis.Node);
            Assert.AreEqual("craft", loaded.Nemesis.Adaptation);
            CollectionAssert.AreEqual(r.Nemesis.Scars, loaded.Nemesis.Scars);
            CollectionAssert.AreEqual(r.Relics, loaded.Relics);
            CollectionAssert.AreEqual(r.Nodes.SelectMany(n => n.Jumps).ToList(), loaded.Nodes.SelectMany(n => n.Jumps).ToList());
            Assert.AreEqual(RunJson.ToJson(r), RunJson.ToJson(loaded));
        }

        [Test]
        public void BotRunsMeetTheirNemesisAndStillEnd()
        {
            int met = 0, killed = 0, returned = 0;
            for (long seed = 1; seed <= 12; seed++)
            {
                var r = RunBot.Play(seed, c);
                Assert.AreEqual(RunPhase.Over, r.Phase, $"seed {seed}");
                if (r.Nemesis.Meetings > 0) met++;
                if (r.Nemesis.LastOutcome == NemesisOutcome.Killed) killed++;
                if (r.Nemesis.Rank > 1) returned++;
            }
            Assert.Greater(met, 3, "the bot should run into its nemesis in a fair share of runs");
            Assert.Greater(killed + returned, 0);
        }
    }
}
