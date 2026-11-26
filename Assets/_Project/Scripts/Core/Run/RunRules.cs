using System;
using System.Collections.Generic;
using System.Linq;
using Silence.Core.Battle;
using Silence.Core.Hex;
using Silence.Core.Random;

namespace Silence.Core.Run
{
    /// <summary>
    /// The run loop: travel between map nodes, fight, take rewards, answer events, refit at shipyards.
    /// Pure and deterministic like the battle rules: every random choice comes from the RNG position
    /// stored in the <see cref="RunState"/>, so a seed and a list of choices replay the same run.
    /// Each method returns null on success or the reason nothing changed.
    /// </summary>
    public static class RunRules
    {
        public const int FlagshipIndex = 0, EscortIndex = 1;

        // ================================================================ rng

        static Pcg32 Rng(RunState r) => Pcg32.FromState(unchecked((ulong)r.RngState), unchecked((ulong)r.RngIncrement));

        static void Keep(RunState r, Pcg32 rng)
        {
            r.RngState = unchecked((long)rng.State);
            r.RngIncrement = unchecked((long)rng.Increment);
        }

        // ================================================================ new run

        public static RunState NewRun(long seed, RunContent c)
        {
            var cfg = c.Config;
            var r = new RunState
            {
                Seed = seed, Scrap = cfg.StartScrap, Ordinals = cfg.StartOrdinals, MaxOrdinals = cfg.MaxOrdinals,
                Darts = cfg.StartDarts, MaxDarts = cfg.MaxDarts, BossLayer = cfg.Layers,
                ConcordProgress = cfg.ConcordStart, ChorusProgress = cfg.ChorusStart,
            };
            var rng = new Pcg32(unchecked((ulong)seed), 77UL);

            var flag = UnitLibrary.Get("flagship");
            var escort = UnitLibrary.Get("escort");
            r.Ships.Add(new ShipState { Unit = "flagship", Name = flag.Name, Hull = flag.Hull, MaxHull = flag.Hull });
            r.Ships.Add(new ShipState { Unit = "escort", Name = escort.Name, Hull = escort.Hull, MaxHull = escort.Hull });

            foreach (string id in cfg.StarterModules)
            {
                var m = c.Module(id);
                r.Modules.Add(id);
                foreach (string card in m.Cards) r.Deck.Add(new DeckCard(card, FlagshipIndex));
                r.Flagship.MaxHull += m.Hull;
                r.Flagship.Hull += m.Hull;
            }
            r.Deck.AddRange(cfg.StarterCards);

            GenerateMap(r, c, rng);
            Keep(r, rng);
            r.Journal.Add("The broadcast reached everyone at once. The Sixth Verdict makes for the Ion Reef gate.");
            return r;
        }

        // ================================================================ map

        static void GenerateMap(RunState r, RunContent c, Pcg32 rng)
        {
            int last = r.BossLayer;
            var layers = new List<List<MapNode>>();
            var events = c.Events.Select(e => e.Id).ToList();
            rng.Shuffle(events);
            int nextEvent = 0;

            for (int layer = 0; layer <= last; layer++)
            {
                int count = layer == last ? 1 : layer == last - 1 ? 2 : layer == 0 ? 3 : rng.Range(2, 5);
                var nodes = new List<MapNode>();
                bool yardHere = false;
                for (int i = 0; i < count; i++)
                {
                    var n = new MapNode { Id = r.Nodes.Count, Layer = layer, Lane = count == 1 ? 15 : i * 30 / (count - 1) };
                    if (layer == last) n.Kind = NodeKind.Boss;
                    else if (layer == last - 1) n.Kind = NodeKind.Shipyard;
                    else if (layer == 0) n.Kind = NodeKind.Battle;
                    else
                    {
                        int roll = rng.NextInt(100);
                        if (roll < 46) n.Kind = NodeKind.Battle;
                        else if (roll < 76) n.Kind = NodeKind.Event;
                        else if (roll < 90) n.Kind = layer >= 3 ? NodeKind.Elite : NodeKind.Battle;
                        else n.Kind = layer >= 3 && layer <= last - 3 && !yardHere ? NodeKind.Shipyard : NodeKind.Event;
                        if (n.Kind == NodeKind.Shipyard) yardHere = true;
                    }
                    r.Nodes.Add(n);
                    nodes.Add(n);
                }
                if (layer > 0 && layer < last - 1)
                {
                    // A yard is always within reach halfway across, so a mauled fleet has somewhere to go.
                    if (layer == last / 2 && !nodes.Any(n => n.Kind == NodeKind.Shipyard)) nodes[rng.NextInt(nodes.Count)].Kind = NodeKind.Shipyard;
                    // Every written event appears at most once per sector; further unknowns are fights.
                    foreach (var n in nodes)
                    {
                        if (n.Kind != NodeKind.Event) continue;
                        if (nextEvent < events.Count) n.Event = events[nextEvent++];
                        else n.Kind = NodeKind.Battle;
                    }
                    // A layer is never all one thing: it has to be a choice.
                    if (nodes.All(n => n.Kind == nodes[0].Kind))
                    {
                        var pick = nodes[rng.NextInt(nodes.Count)];
                        if (pick.Kind != NodeKind.Battle)
                        {
                            if (pick.Kind == NodeKind.Event)
                            {
                                // Hand the most recently dealt event back: whichever node holds it takes this node's instead.
                                nextEvent--;
                                var holder = nodes.Find(n => n != pick && n.Event == events[nextEvent]);
                                if (holder != null) holder.Event = pick.Event;
                            }
                            pick.Kind = NodeKind.Battle;
                            pick.Event = "";
                        }
                        else if (nextEvent < events.Count)
                        {
                            pick.Kind = NodeKind.Event;
                            pick.Event = events[nextEvent++];
                        }
                        else pick.Kind = layer >= 3 ? NodeKind.Elite : NodeKind.Shipyard;
                    }
                }
                layers.Add(nodes);
            }

            foreach (var n in r.Nodes)
            {
                switch (n.Kind)
                {
                    case NodeKind.Battle:
                        n.Encounter = PickEncounter(c, rng, n.Layer <= 1 ? 0 : 1);
                        break;
                    case NodeKind.Elite:
                        n.Encounter = PickEncounter(c, rng, 2);
                        break;
                    case NodeKind.Boss:
                        n.Encounter = PickEncounter(c, rng, 3);
                        break;
                }
            }

            for (int layer = 0; layer < last; layer++)
            {
                var a = layers[layer];
                var b = layers[layer + 1];
                var primary = new int[a.Count];
                for (int i = 0; i < a.Count; i++)
                {
                    primary[i] = a.Count == 1 ? 0 : (i * (b.Count - 1) * 2 + (a.Count - 1)) / ((a.Count - 1) * 2);
                    a[i].Next.Add(b[primary[i]].Id);
                }
                for (int j = 0; j < b.Count; j++)
                {
                    if (a.Any(n => n.Next.Contains(b[j].Id))) continue;
                    int i = b.Count == 1 ? 0 : (j * (a.Count - 1) * 2 + (b.Count - 1)) / ((b.Count - 1) * 2);
                    a[i].Next.Add(b[j].Id);
                }
                for (int i = 0; i < a.Count; i++)
                {
                    int extra = primary[i] + 1;
                    bool crosses = i + 1 < a.Count && primary[i + 1] < extra;
                    if (extra < b.Count && !crosses && !a[i].Next.Contains(b[extra].Id) && rng.Chance(50)) a[i].Next.Add(b[extra].Id);
                }
                foreach (var n in a) n.Next.Sort();
            }
        }

        static string PickEncounter(RunContent c, Pcg32 rng, int tier)
        {
            var pool = c.Encounters.Where(e => e.Tier == tier).ToList();
            if (pool.Count == 0) pool = c.Encounters.Where(e => e.Tier <= 1).ToList();
            return rng.Pick(pool).Id;
        }

        /// <summary>Nodes the player may travel to now: the first layer at the start, otherwise the current node's links.</summary>
        public static List<MapNode> Choices(RunState r)
        {
            if (r.Phase != RunPhase.Map) return new List<MapNode>();
            if (r.CurrentNode < 0) return r.Nodes.Where(n => n.Layer == 0).ToList();
            return r.Current.Next.Select(r.Node).ToList();
        }

        public static string Travel(RunState r, RunContent c, int nodeId)
        {
            if (r.Phase != RunPhase.Map) return "finish what is in front of you first";
            var node = Choices(r).Find(n => n.Id == nodeId);
            if (node == null) return "that node is not connected to your position";

            var rng = Rng(r);
            var cfg = c.Config;
            r.CurrentNode = node.Id;
            node.Visited = true;
            r.NodesVisited++;
            r.PlayerProgress += 10;
            r.ConcordProgress += cfg.ConcordRate + rng.NextInt(cfg.RivalSpread + 1);
            r.ChorusProgress += cfg.ChorusRate + rng.NextInt(cfg.RivalSpread + 1);

            switch (node.Kind)
            {
                case NodeKind.Battle:
                case NodeKind.Elite:
                case NodeKind.Boss:
                    r.PendingEncounter = node.Encounter;
                    r.PendingRewardPercent = 100;
                    r.Phase = RunPhase.Battle;
                    break;
                case NodeKind.Event:
                    r.EventId = node.Event;
                    r.EventResult = "";
                    r.Phase = RunPhase.Event;
                    break;
                case NodeKind.Shipyard:
                    OpenShop(r, c, rng);
                    r.Phase = RunPhase.Shipyard;
                    break;
            }
            Keep(r, rng);
            return null;
        }

        /// <summary>True when the Concord fleet is at or past the player's position (it reaches the gate first).</summary>
        public static bool ConcordAhead(RunState r) => r.ConcordProgress >= r.PlayerProgress;

        public static bool ChorusAhead(RunState r) => r.ChorusProgress >= r.PlayerProgress;

        /// <summary>Length of the race track in tenths of a node: the boss node's position.</summary>
        public static int TrackLength(RunState r) => (r.BossLayer + 1) * 10;

        // ================================================================ battles

        public static ulong BattleSeed(RunState r) => unchecked((ulong)r.Seed * 6364136223846793005UL + (ulong)(r.CurrentNode + 1) * 7919UL + (ulong)r.BattleNumber * 104729UL);

        /// <summary>Build the pending battle from the fleet's current state. Not started; nothing in the run changes.</summary>
        public static Battle.Battle BuildBattle(RunState r, RunContent c)
        {
            if (r.Phase != RunPhase.Battle) throw new InvalidOperationException("no battle is pending");
            var enc = c.Encounter(r.PendingEncounter);
            var cfg = c.Config;
            ulong seed = BattleSeed(r);
            var b = new Battle.Battle(seed) { Title = enc.Name, Ordinals = r.Ordinals, DartsInHangar = r.Darts };
            if (enc.TurnLimit > 0) b.TurnLimit = enc.TurnLimit;

            var units = new List<EncounterUnit>(enc.Units);
            bool isBoss = enc.Tier == 3;
            if (isBoss && (ConcordAhead(r) || r.VennEnraged)) units.AddRange(enc.Reinforcements);

            var taken = new HashSet<HexCoord> { HexCoord.FromOffset(0, 2), HexCoord.FromOffset(0, 4) };
            foreach (var u in units) taken.Add(HexCoord.FromOffset(u.Col, u.Row));
            foreach (var (col, row, asteroid) in enc.Terrain)
            {
                b.Board.Set(col, row, asteroid ? Terrain.Asteroid : Terrain.Debris);
                taken.Add(HexCoord.FromOffset(col, row));
            }
            var terrainRng = new Pcg32(seed, 91UL);
            Scatter(b, terrainRng, taken, enc.ScatterAsteroids, Terrain.Asteroid);
            Scatter(b, terrainRng, taken, enc.ScatterDebris, Terrain.Debris);

            var ids = new int[r.Ships.Count];
            for (int i = 0; i < r.Ships.Count; i++)
            {
                ids[i] = -1;
                var ship = r.Ships[i];
                if (!ship.Alive) continue;
                var spec = UnitLibrary.Get(ship.Unit);
                spec.Hull = ship.MaxHull;
                if (i == FlagshipIndex)
                {
                    spec.HeatCap = cfg.FlagshipBaseHeatCap;
                    foreach (string id in r.Modules)
                    {
                        var m = c.Module(id);
                        spec.HeatCap += m.HeatCap;
                        spec.MaxSpeed += m.MaxSpeed;
                        spec.Shields += m.Shields;
                        spec.Armor[ArcIndex.Fore] += m.ArmorFore;
                    }
                    spec.MaxSpeed = Math.Max(1, spec.MaxSpeed);
                }
                var unit = b.AddUnit(spec, Side.Player, 0, i == FlagshipIndex ? 2 : 4, HexDirection.E, 1, i == FlagshipIndex);
                unit.Hull = Math.Max(1, Math.Min(ship.Hull, ship.MaxHull));
                ids[i] = unit.Id;
            }

            foreach (var u in units)
                b.AddUnit(UnitLibrary.Get(u.Unit), Side.Enemy, u.Col, u.Row, u.Facing, u.Speed);

            foreach (var card in r.Deck)
            {
                if (card.Ship < 0) b.AddCard(PaperCards.Get(card.Card), -1);
                else if (card.Ship < ids.Length && ids[card.Ship] >= 0) b.AddCard(PaperCards.Get(card.Card), ids[card.Ship]);
            }
            return b;
        }

        static void Scatter(Battle.Battle b, Pcg32 rng, HashSet<HexCoord> taken, int count, Terrain terrain)
        {
            for (int n = 0, tries = 0; n < count && tries < 60; tries++)
            {
                var h = HexCoord.FromOffset(rng.Range(2, 7), rng.Range(0, b.Board.Height));
                if (taken.Contains(h)) continue;
                b.Board.Set(h, terrain);
                taken.Add(h);
                n++;
            }
        }

        /// <summary>Carry a finished battle back into the run: damage, losses, junk, Ordinals, darts, then the reward or the end.</summary>
        public static void FinishBattle(RunState r, RunContent c, Battle.Battle b)
        {
            if (r.Phase != RunPhase.Battle) throw new InvalidOperationException("no battle is pending");
            if (b.Outcome == BattleOutcome.Ongoing) throw new InvalidOperationException("the battle is not over");
            var cfg = c.Config;
            var enc = c.Encounter(r.PendingEncounter);
            var rng = Rng(r);
            r.BattleNumber++;
            r.TurnsFought += b.Turn;
            r.Ordinals = Math.Max(0, b.Ordinals);
            if (b.DartsInHangar >= 0) r.Darts = Math.Min(r.MaxDarts, b.DartsInHangar + b.PlayerCraftCount());

            var players = b.Units.Where(u => u.Side == Side.Player && u.IsCapital).ToList();
            for (int i = 0; i < r.Ships.Count; i++)
            {
                var ship = r.Ships[i];
                if (!ship.Alive) continue;
                var unit = players.Find(u => u.IsFlagship == (i == FlagshipIndex));
                if (unit == null) continue;
                ship.Hull = unit.Alive ? unit.Hull : 0;
                if (!unit.Alive) ship.Alive = false;
            }

            // Junk left in the deck stays with the fleet; junk that was exhausted (a fire put out) is gone.
            r.Deck.RemoveAll(d => d.Ship < 0);
            foreach (var pile in new[] { b.Deck.DrawPile, b.Deck.Hand, b.Deck.Discard })
                foreach (var card in pile)
                    if (card.Def.IsJunk) r.Deck.Add(new DeckCard(card.Def.Id, -1));

            if (b.Turn > cfg.LongBattleTurns)
            {
                r.ConcordProgress += cfg.LongBattleDelay;
                r.ChorusProgress += cfg.LongBattleDelay;
            }

            if (b.Outcome == BattleOutcome.Defeat)
            {
                r.Journal.Add($"{enc.Name}: the fleet was destroyed on turn {b.Turn}.");
                End(r, RunResult.Defeat);
                Keep(r, rng);
                return;
            }

            if (b.Outcome == BattleOutcome.Withdrawal)
            {
                if (enc.Tier == 3)
                {
                    r.Journal.Add($"{enc.Name}: the gate held. The rivals passed while you fought.");
                    End(r, RunResult.Outpaced);
                }
                else
                {
                    r.ConcordProgress += cfg.WithdrawalDelay;
                    r.ChorusProgress += cfg.WithdrawalDelay;
                    r.Journal.Add($"{enc.Name}: both fleets disengaged after {b.Turn} turns. Time lost, nothing gained.");
                    RecoverFlagship(r, cfg);
                    r.Phase = RunPhase.Map;
                }
                Keep(r, rng);
                return;
            }

            r.BattlesWon++;
            if (!r.Escort.Alive && r.Escort.Hull == 0 && players.Any(u => !u.IsFlagship && !u.Alive)) r.Journal.Add($"{enc.Name}: the Blade was lost.");
            RecoverFlagship(r, cfg);

            if (enc.Tier == 3)
            {
                r.Journal.Add($"{enc.Name}: the gate is open after {b.Turn} turns.");
                End(r, RunResult.Victory);
                Keep(r, rng);
                return;
            }

            int scrap = (enc.Scrap + rng.NextInt(9)) * r.PendingRewardPercent / 100;
            int ordinals = Math.Min(cfg.OrdinalsPerVictory, r.MaxOrdinals - r.Ordinals);
            r.Scrap += scrap;
            r.Ordinals += Math.Max(0, ordinals);
            r.Reward = new RewardOffer
            {
                Active = true, Scrap = scrap, Ordinals = Math.Max(0, ordinals),
                Cards = PickCards(r, c, rng, cfg.RewardCards + (enc.Tier == 2 ? 1 : 0)).Select(x => new DeckCard(x.Card, x.Ship)).ToList(),
                Note = $"{enc.Name} won in {b.Turn} turns.",
            };
            r.Journal.Add($"{enc.Name}: won in {b.Turn} turns.");
            r.Phase = RunPhase.Reward;
            Keep(r, rng);
        }

        /// <summary>
        /// The flagship rule: if it was lost but the battle was survived, it is rebuilt at a high cost
        /// (Ordinals and permanent hull) and comes back at half strength.
        /// </summary>
        static void RecoverFlagship(RunState r, RunConfig cfg)
        {
            var flag = r.Flagship;
            if (flag.Alive) return;
            flag.Alive = true;
            flag.MaxHull = Math.Max(10, flag.MaxHull - cfg.FlagshipRebuildMaxHullLoss);
            flag.Hull = flag.MaxHull / 2;
            int paid = Math.Min(r.Ordinals, cfg.FlagshipRebuildOrdinals);
            r.Ordinals -= paid;
            r.Journal.Add($"The Sixth Verdict was rebuilt from her wreck: {paid} Ordinals spent, {cfg.FlagshipRebuildMaxHullLoss} hull lost for good.");
        }

        static void End(RunState r, RunResult result)
        {
            r.Result = result;
            r.Phase = RunPhase.Over;
            r.Reward.Active = false;
            r.Shop.Active = false;
        }

        static List<RewardCard> PickCards(RunState r, RunContent c, Pcg32 rng, int count)
        {
            var pool = c.Rewards.Where(x => Available(r, x)).ToList();
            var picked = new List<RewardCard>();
            while (picked.Count < count && pool.Count > 0)
            {
                int total = pool.Sum(x => Math.Max(1, x.Weight));
                int roll = rng.NextInt(total);
                foreach (var x in pool)
                {
                    roll -= Math.Max(1, x.Weight);
                    if (roll >= 0) continue;
                    picked.Add(x);
                    pool.Remove(x);
                    break;
                }
            }
            return picked;
        }

        static bool Available(RunState r, RewardCard x)
        {
            if (x.Ship >= 0 && x.Ship < r.Ships.Count && !r.Ships[x.Ship].Alive) return false;
            return string.IsNullOrEmpty(x.RequiresModule) || r.Modules.Contains(x.RequiresModule);
        }

        /// <summary>Take one of the offered cards, or -1 to take none.</summary>
        public static string TakeReward(RunState r, int cardIndex)
        {
            if (r.Phase != RunPhase.Reward || !r.Reward.Active) return "no reward is waiting";
            if (cardIndex >= r.Reward.Cards.Count) return "no such card";
            if (cardIndex >= 0) r.Deck.Add(r.Reward.Cards[cardIndex]);
            r.Reward.Active = false;
            r.Phase = RunPhase.Map;
            return null;
        }

        /// <summary>Instead of a card: spend the lull on field repairs to every surviving ship.</summary>
        public static string TakeFieldRepair(RunState r, RunContent c)
        {
            if (r.Phase != RunPhase.Reward || !r.Reward.Active) return "no reward is waiting";
            foreach (var s in r.Ships)
                if (s.Alive) s.Hull = Math.Min(s.MaxHull, s.Hull + c.Config.FieldRepair);
            r.Reward.Active = false;
            r.Phase = RunPhase.Map;
            return null;
        }

        // ================================================================ events

        /// <summary>Why a choice can't be taken (it costs more than the fleet has), or null.</summary>
        public static string CanChoose(RunState r, EventChoice choice)
        {
            foreach (var o in choice.Outcomes)
            {
                if (o.Kind == OutcomeKind.Scrap && r.Scrap + o.Amount < 0) return $"needs {-o.Amount} scrap";
                if (o.Kind == OutcomeKind.Ordinals && r.Ordinals + o.Amount < 0) return $"needs {-o.Amount} Ordinals";
            }
            return null;
        }

        public static string ChooseEvent(RunState r, RunContent c, int choiceIndex)
        {
            if (r.Phase != RunPhase.Event) return "no event is waiting";
            var ev = c.Event(r.EventId);
            if (choiceIndex < 0 || choiceIndex >= ev.Choices.Count) return "no such choice";
            var choice = ev.Choices[choiceIndex];
            string blocked = CanChoose(r, choice);
            if (blocked != null) return blocked;

            var effects = new List<string>();
            string battle = null;
            foreach (var o in choice.Outcomes)
            {
                switch (o.Kind)
                {
                    case OutcomeKind.Scrap:
                        r.Scrap += o.Amount;
                        effects.Add($"{Signed(o.Amount)} scrap");
                        break;
                    case OutcomeKind.Ordinals:
                    {
                        int before = r.Ordinals;
                        r.Ordinals = Math.Max(0, Math.Min(r.MaxOrdinals, r.Ordinals + o.Amount));
                        effects.Add($"{Signed(r.Ordinals - before)} Ordinals");
                        break;
                    }
                    case OutcomeKind.HullFlagship:
                        effects.Add($"{r.Flagship.Name} {Signed(ChangeHull(r.Flagship, o.Amount))} hull");
                        break;
                    case OutcomeKind.HullAll:
                        foreach (var s in r.Ships.Where(s => s.Alive)) effects.Add($"{s.Name} {Signed(ChangeHull(s, o.Amount))} hull");
                        break;
                    case OutcomeKind.MaxHullFlagship:
                        r.Flagship.MaxHull = Math.Max(10, r.Flagship.MaxHull + o.Amount);
                        r.Flagship.Hull = Math.Min(r.Flagship.MaxHull, r.Flagship.Hull + Math.Max(0, o.Amount));
                        effects.Add($"{r.Flagship.Name} {Signed(o.Amount)} maximum hull");
                        break;
                    case OutcomeKind.Darts:
                    {
                        int before = r.Darts;
                        r.Darts = Math.Max(0, Math.Min(r.MaxDarts, r.Darts + o.Amount));
                        effects.Add($"{Signed(r.Darts - before)} darts");
                        break;
                    }
                    case OutcomeKind.AddCard:
                        if (o.Ship < r.Ships.Count && r.Ships[o.Ship].Alive)
                        {
                            r.Deck.Add(new DeckCard(o.Id, o.Ship));
                            effects.Add($"gained {PaperCards.Get(o.Id).Name}");
                        }
                        break;
                    case OutcomeKind.AddJunk:
                        for (int i = 0; i < Math.Max(1, o.Amount); i++) r.Deck.Add(new DeckCard(o.Id, -1));
                        effects.Add($"{PaperCards.Get(o.Id).Name} added to the deck");
                        break;
                    case OutcomeKind.RemoveJunk:
                    {
                        int removed = 0;
                        for (int i = 0; i < Math.Max(1, o.Amount); i++)
                        {
                            int at = r.Deck.FindIndex(d => d.Ship < 0);
                            if (at < 0) break;
                            r.Deck.RemoveAt(at);
                            removed++;
                        }
                        effects.Add($"{removed} junk card{(removed == 1 ? "" : "s")} removed");
                        break;
                    }
                    case OutcomeKind.RivalsAdvance:
                        r.ConcordProgress += o.Amount;
                        r.ChorusProgress += o.Amount;
                        effects.Add(o.Amount > 0 ? "the rivals gain ground" : "the rivals lose ground");
                        break;
                    case OutcomeKind.EnrageBoss:
                        r.VennEnraged = true;
                        effects.Add("the gate fleet will be reinforced");
                        break;
                    case OutcomeKind.StartBattle:
                        battle = o.Id;
                        r.PendingRewardPercent = o.Amount > 0 ? o.Amount : 100;
                        break;
                }
            }

            r.EventResult = choice.Result + (effects.Count > 0 ? "\n\n" + Capital(string.Join(", ", effects)) + "." : "");
            r.Journal.Add($"{ev.Title}: {choice.Label}");
            if (battle != null)
            {
                r.PendingEncounter = battle;
                r.Phase = RunPhase.Battle;
            }
            else r.Phase = RunPhase.EventResult;
            return null;
        }

        public static string CloseEvent(RunState r)
        {
            if (r.Phase != RunPhase.EventResult) return "no event result is showing";
            r.Phase = RunPhase.Map;
            return null;
        }

        static int ChangeHull(ShipState s, int amount)
        {
            int before = s.Hull;
            s.Hull = Math.Max(1, Math.Min(s.MaxHull, s.Hull + amount));
            return s.Hull - before;
        }

        static string Signed(int v) => v >= 0 ? "+" + v : v.ToString();

        static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ================================================================ shipyard

        static void OpenShop(RunState r, RunContent c, Pcg32 rng)
        {
            var shop = new ShopState { Active = true };
            foreach (var x in PickCards(r, c, rng, 3))
            {
                shop.Cards.Add(new DeckCard(x.Card, x.Ship));
                shop.CardPrices.Add(x.Price);
            }
            for (int slot = 0; slot < c.Config.Slots.Count && slot < r.Modules.Count; slot++)
            {
                var options = c.Modules.Where(m => m.Slot == c.Config.Slots[slot] && m.Id != r.Modules[slot]).ToList();
                if (options.Count > 0) shop.Modules.Add(rng.Pick(options).Id);
            }
            r.Shop = shop;
        }

        static string Pay(RunState r, int cost)
        {
            if (r.Phase != RunPhase.Shipyard || !r.Shop.Active) return "not at a shipyard";
            if (r.Scrap < cost) return $"needs {cost} scrap";
            r.Scrap -= cost;
            r.Shop.Purchases++;
            return null;
        }

        public static int RepairCost(RunState r, RunContent c, int ship)
            => ship < r.Ships.Count && r.Ships[ship].Alive ? (r.Ships[ship].MaxHull - r.Ships[ship].Hull) * c.Config.RepairCostPerHull : 0;

        /// <summary>Repair a ship as far as the scrap goes.</summary>
        public static string Repair(RunState r, RunContent c, int ship)
        {
            if (r.Phase != RunPhase.Shipyard) return "not at a shipyard";
            if (ship < 0 || ship >= r.Ships.Count || !r.Ships[ship].Alive) return "no such ship";
            var s = r.Ships[ship];
            int per = c.Config.RepairCostPerHull;
            int hull = Math.Min(s.MaxHull - s.Hull, per > 0 ? r.Scrap / per : s.MaxHull);
            if (s.MaxHull == s.Hull) return $"{s.Name} is not damaged";
            if (hull <= 0) return $"needs {per} scrap per hull point";
            string err = Pay(r, hull * per);
            if (err != null) return err;
            s.Hull += hull;
            return null;
        }

        public static string RemoveJunk(RunState r, RunContent c)
        {
            if (r.JunkCount() == 0) return "no junk in the deck";
            string err = Pay(r, c.Config.RemoveJunkCost);
            if (err != null) return err;
            r.Deck.RemoveAt(r.Deck.FindIndex(d => d.Ship < 0));
            return null;
        }

        /// <summary>
        /// Strike one chosen card from the deck for good: a thinner deck draws its best cards more often.
        /// Junk has its own cheaper service; the deck can't go below <see cref="RunConfig.MinDeckSize"/>.
        /// </summary>
        public static string CheckRemoveCard(RunState r, RunContent c, int deckIndex)
        {
            if (r.Phase != RunPhase.Shipyard || !r.Shop.Active) return "not at a shipyard";
            if (deckIndex < 0 || deckIndex >= r.Deck.Count) return "no such card";
            if (r.Deck[deckIndex].Ship < 0) return "junk is stripped out by its own service";
            if (r.Deck.Count(d => d.Ship >= 0) <= c.Config.MinDeckSize) return $"the deck can't go below {c.Config.MinDeckSize} cards";
            if (r.Scrap < c.Config.RemoveCardCost) return $"needs {c.Config.RemoveCardCost} scrap";
            return null;
        }

        public static string RemoveCard(RunState r, RunContent c, int deckIndex)
        {
            string err = CheckRemoveCard(r, c, deckIndex) ?? Pay(r, c.Config.RemoveCardCost);
            if (err != null) return err;
            r.Journal.Add($"Struck {PaperCards.Get(r.Deck[deckIndex].Card).Name} from the deck.");
            r.Deck.RemoveAt(deckIndex);
            return null;
        }

        public static string BuyCard(RunState r, int index)
        {
            if (r.Phase != RunPhase.Shipyard) return "not at a shipyard";
            if (index < 0 || index >= r.Shop.Cards.Count) return "no such card";
            string err = Pay(r, r.Shop.CardPrices[index]);
            if (err != null) return err;
            r.Deck.Add(r.Shop.Cards[index]);
            r.Shop.Cards.RemoveAt(index);
            r.Shop.CardPrices.RemoveAt(index);
            return null;
        }

        /// <summary>Refit a flagship slot with an offered module: its cards replace the old module's cards.</summary>
        public static string FitModule(RunState r, RunContent c, string moduleId)
        {
            if (r.Phase != RunPhase.Shipyard) return "not at a shipyard";
            if (!r.Shop.Modules.Contains(moduleId)) return "that module is not on offer";
            var m = c.Module(moduleId);
            int slot = c.Config.Slots.IndexOf(m.Slot);
            if (slot < 0 || slot >= r.Modules.Count) return "no such slot";
            string err = Pay(r, c.Config.ModuleCost);
            if (err != null) return err;

            var old = c.Module(r.Modules[slot]);
            foreach (string card in old.Cards)
            {
                int at = r.Deck.FindIndex(d => d.Ship == FlagshipIndex && d.Card == card);
                if (at >= 0) r.Deck.RemoveAt(at);
            }
            foreach (string card in m.Cards) r.Deck.Add(new DeckCard(card, FlagshipIndex));
            var flag = r.Flagship;
            flag.MaxHull += m.Hull - old.Hull;
            flag.Hull = Math.Max(1, Math.Min(flag.MaxHull, flag.Hull + Math.Max(0, m.Hull - old.Hull)));
            r.Modules[slot] = moduleId;
            r.Shop.Modules.Remove(moduleId);
            r.Shop.Modules.Add(old.Id);
            r.Journal.Add($"Refit: {old.Name} replaced by {m.Name}.");
            return null;
        }

        public static string RebuildEscort(RunState r, RunContent c)
        {
            if (r.Escort == null || r.Escort.Alive) return "the escort is not lost";
            string err = Pay(r, c.Config.RebuildEscortCost);
            if (err != null) return err;
            r.Escort.Alive = true;
            r.Escort.Hull = r.Escort.MaxHull;
            r.Journal.Add("A new Blade joined the fleet.");
            return null;
        }

        public static string BuyDart(RunState r, RunContent c)
        {
            if (r.Darts >= r.MaxDarts) return "the hangar is full";
            string err = Pay(r, c.Config.DartCost);
            if (err != null) return err;
            r.Shop.Purchases--; // loading a dart takes no yard time
            r.Darts++;
            return null;
        }

        public static string BuyOrdinals(RunState r, RunContent c)
        {
            if (r.Ordinals >= r.MaxOrdinals) return "the vats are full";
            string err = Pay(r, c.Config.OrdinalCost * c.Config.OrdinalsPerPurchase);
            if (err != null) return err;
            r.Ordinals = Math.Min(r.MaxOrdinals, r.Ordinals + c.Config.OrdinalsPerPurchase);
            return null;
        }

        /// <summary>Leave the yard. Every service bought cost time: the rivals advance for each.</summary>
        public static string LeaveShipyard(RunState r, RunContent c)
        {
            if (r.Phase != RunPhase.Shipyard) return "not at a shipyard";
            int delay = r.Shop.Purchases * c.Config.ShipyardDelayPerPurchase;
            r.ConcordProgress += delay;
            r.ChorusProgress += delay;
            r.Shop.Active = false;
            r.Phase = RunPhase.Map;
            return null;
        }

        // ================================================================ score

        public static int Score(RunState r)
        {
            int score = r.BattlesWon * 50 + r.NodesVisited * 10 + r.Scrap;
            if (r.Result == RunResult.Victory)
            {
                score += 500 + r.Ships.Where(s => s.Alive).Sum(s => s.Hull) * 5 + r.Ordinals * 3;
                if (!ConcordAhead(r)) score += 150;
                if (!ChorusAhead(r)) score += 100;
            }
            return score;
        }
    }
}
