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
        /// <summary>Id of the encounter built from the run's nemesis instead of read from data.</summary>
        public const string NemesisEncounterId = "nemesis";

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
                Seed = seed, Faction = c.Faction.Id, Scrap = cfg.StartScrap, Ordinals = cfg.StartOrdinals, MaxOrdinals = cfg.MaxOrdinals,
                Darts = cfg.StartDarts, MaxDarts = cfg.MaxDarts, BossLayer = cfg.Layers,
                ConcordProgress = cfg.ConcordStart, ChorusProgress = cfg.ChorusStart,
            };
            var rng = new Pcg32(unchecked((ulong)seed), 77UL);

            var flag = UnitLibrary.Get(c.Faction.FlagshipUnit);
            var escort = UnitLibrary.Get(c.Faction.EscortUnit);
            r.Ships.Add(new ShipState { Unit = c.Faction.FlagshipUnit, Name = flag.Name, Hull = flag.Hull, MaxHull = flag.Hull });
            r.Ships.Add(new ShipState { Unit = c.Faction.EscortUnit, Name = escort.Name, Hull = escort.Hull, MaxHull = escort.Hull });

            foreach (string id in cfg.StarterModules)
            {
                var m = c.Module(id);
                r.Modules.Add(id);
                foreach (string card in m.Cards) r.Deck.Add(new DeckCard(card, FlagshipIndex));
                r.Flagship.MaxHull += m.Hull;
                r.Flagship.Hull += m.Hull;
            }
            r.Deck.AddRange(cfg.StarterCards);

            r.Approval = c.Faction.StartApproval;
            foreach (string id in c.Faction.StarterOfficers) r.Officers.Add(new OfficerState { Id = c.Officer(id).Id });

            GenerateMap(r, c, rng);
            AddJumpLanes(r, cfg, rng);
            if (c.Nemeses.Count > 0)
            {
                r.Nemesis = new NemesisState { Id = rng.Pick(c.Nemeses).Id, Alive = true };
                PlaceNemesis(r, c, rng, 2);
                if (r.Nemesis.FellBack) r.ConcordProgress = cfg.ConcordStart; // a map with no lane for them: no head start for it
            }
            // Hazards: some of the fights past the opening are in a storm, a nebula or a dense field, and pay more for it.
            foreach (var n in r.Nodes)
                if ((n.Kind == NodeKind.Battle || n.Kind == NodeKind.Elite) && n.Layer >= 2 && rng.Chance(cfg.HazardChance)) n.Hazard = rng.Pick(Hazards.All);
            Keep(r, rng);
            r.Journal.Add(c.Faction.OpeningJournal);
            return r;
        }

        // ================================================================ map

        static void GenerateMap(RunState r, RunContent c, Pcg32 rng)
        {
            int last = r.BossLayer;
            var layers = new List<List<MapNode>>();
            var events = c.Events.Select(e => e.Id).ToList();
            rng.Shuffle(events);
            // Only some of the written events turn up in any one sector: the rest are for another run.
            if (c.Config.EventsPerSector > 0 && events.Count > c.Config.EventsPerSector) events.RemoveRange(c.Config.EventsPerSector, events.Count - c.Config.EventsPerSector);
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

        /// <summary>
        /// A few risky jump lanes that skip a layer: two nodes of progress for one jump's worth of the rivals'
        /// time, paid for in hull (and in the node you never visit).
        /// </summary>
        static void AddJumpLanes(RunState r, RunConfig cfg, Pcg32 rng)
        {
            var sources = r.Nodes.Where(n => n.Layer >= 1 && n.Layer <= r.BossLayer - 4).ToList();
            rng.Shuffle(sources);
            int made = 0;
            var usedLayers = new HashSet<int>();
            foreach (var from in sources)
            {
                if (made >= cfg.JumpLanes) break;
                if (usedLayers.Contains(from.Layer)) continue;
                var targets = r.Nodes.Where(n => n.Layer == from.Layer + 2 && Math.Abs(n.Lane - from.Lane) <= 15).ToList();
                if (targets.Count == 0) continue;
                from.Jumps.Add(rng.Pick(targets).Id);
                usedLayers.Add(from.Layer);
                made++;
            }
        }

        /// <summary>Nodes that can still be reached from where the fleet is (everything, before the first jump).</summary>
        static HashSet<int> Ahead(RunState r)
        {
            var seen = new HashSet<int>();
            var open = new Queue<int>();
            if (r.CurrentNode < 0)
                foreach (var n in r.Nodes.Where(n => n.Layer == 0)) open.Enqueue(n.Id);
            else open.Enqueue(r.CurrentNode);
            while (open.Count > 0)
            {
                int id = open.Dequeue();
                if (!seen.Add(id)) continue;
                foreach (int next in r.Nodes[id].Next) open.Enqueue(next);
                foreach (int next in r.Nodes[id].Jumps) open.Enqueue(next);
            }
            return seen;
        }

        /// <summary>
        /// Put the hunting commander on a battle node at <paramref name="minLayer"/> or the nearest layer after it
        /// that the fleet can still reach. With no lane left before the yard at the gate they fall back to the Concord
        /// main fleet with what they have learned, and the Concord gains ground for it.
        /// </summary>
        static void PlaceNemesis(RunState r, RunContent c, Pcg32 rng, int minLayer)
        {
            var n = r.Nemesis;
            var old = r.Node(n.Node);
            if (old != null && !old.Visited && old.Kind == NodeKind.Nemesis) old.Kind = n.NodeWas;
            n.Node = -1;
            var ahead = Ahead(r);
            // Battle nodes two layers on or later first; failing that the very next layer, then an elite's lane.
            int first = Math.Max(1, minLayer), last = r.BossLayer - 2;
            var layers = Enumerable.Range(first, Math.Max(0, last - first + 1)).ToList();
            if (first - 1 >= 1 && first - 1 <= last && (r.Current == null || first - 1 > r.Current.Layer)) layers.Add(first - 1);
            foreach (var kind in new[] { NodeKind.Battle, NodeKind.Elite })
            foreach (int layer in layers)
            {
                var spots = r.Nodes.Where(x => x.Layer == layer && x.Kind == kind && !x.Visited && x.Id != r.CurrentNode && ahead.Contains(x.Id)).ToList();
                if (spots.Count == 0) continue;
                var spot = rng.Pick(spots);
                n.NodeWas = spot.Kind;
                spot.Kind = NodeKind.Nemesis;
                n.Node = spot.Id;
                return;
            }
            n.FellBack = true;
            r.ConcordProgress += c.Config.NemesisDodgeDelay;
            r.Journal.Add($"{c.Nemesis(n.Id).Name} has gone back to the Concord main fleet, and told it where you are.");
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
            return r.Current.Next.Concat(r.Current.Jumps.Where(j => !r.Current.Next.Contains(j))).Select(r.Node).ToList();
        }

        /// <summary>True when going to this node from here means taking a jump lane.</summary>
        public static bool IsJump(RunState r, int nodeId) => r.Current != null && r.Current.Jumps.Contains(nodeId) && !r.Current.Next.Contains(nodeId);

        /// <summary>Hull each ship pays to take a jump lane.</summary>
        public static int JumpCost(RunState r, RunContent c) => r.Relics.Any(id => c.Relic(id).FreeJumps) ? 0 : c.Config.JumpHullCost;

        /// <summary>
        /// The Concord is far enough ahead to have mined the lanes behind it: every ordinary jump costs each
        /// ship hull until the fleet catches up. Jump lanes are not mined.
        /// </summary>
        public static bool LanesMined(RunState r, RunContent c)
            => r.CurrentNode >= 0 && c.Config.MineDamage > 0 && r.ConcordProgress - r.PlayerProgress >= c.Config.MineLead
               && !r.Relics.Any(id => c.Relic(id).MineProof);

        static int RelicSum(RunState r, RunContent c, Func<RelicDef, int> value) => r.Relics.Sum(id => value(c.Relic(id)));

        /// <summary>Add a relic and apply what it changes at once (hull, hangar space).</summary>
        public static void GainRelic(RunState r, RunContent c, string id)
        {
            var relic = c.Relic(id);
            r.Relics.Add(id);
            if (relic.FlagshipHull != 0)
            {
                r.Flagship.MaxHull += relic.FlagshipHull;
                r.Flagship.Hull = Math.Max(1, r.Flagship.Hull + relic.FlagshipHull);
            }
            if (relic.MaxDarts != 0)
            {
                r.MaxDarts += relic.MaxDarts;
                r.Darts = Math.Max(0, r.Darts + relic.MaxDarts);
            }
            r.Journal.Add($"Relic: {relic.Name}.");
        }

        /// <summary>The fight that is pending: one from data, or the nemesis as they are now.</summary>
        public static EncounterDef PendingEncounterDef(RunState r, RunContent c)
            => r.PendingEncounter == NemesisEncounterId ? NemesisEncounter(r, c) : c.Encounter(r.PendingEncounter);

        public static string Travel(RunState r, RunContent c, int nodeId)
        {
            if (r.Phase != RunPhase.Map) return "finish what is in front of you first";
            var node = Choices(r).Find(n => n.Id == nodeId);
            if (node == null) return "that node is not connected to your position";

            var rng = Rng(r);
            var cfg = c.Config;
            bool jump = IsJump(r, nodeId);
            bool mined = !jump && LanesMined(r, c);
            int layers = r.Current == null ? 1 : Math.Max(1, node.Layer - r.Current.Layer);
            r.TravelNote = "";
            if (jump)
            {
                int cost = JumpCost(r, c);
                foreach (var s in r.Ships.Where(s => s.Alive)) ChangeHull(s, -cost);
                r.TravelNote = cost > 0 ? $"The jump lane shook the fleet: every ship lost {cost} hull, and a node was skipped" : "The old codes held: the jump lane cost nothing";
                r.Journal.Add("Took a jump lane.");
            }
            else if (mined)
            {
                foreach (var s in r.Ships.Where(s => s.Alive)) ChangeHull(s, -cfg.MineDamage);
                r.TravelNote = $"Concord mines in the lane: every ship lost {cfg.MineDamage} hull";
            }
            r.CurrentNode = node.Id;
            node.Visited = true;
            r.NodesVisited++;
            r.PlayerProgress += 10 * layers;
            int pace = RelicSum(r, c, x => x.RivalRate);
            r.ConcordProgress += Math.Max(0, cfg.ConcordRate + pace + rng.NextInt(cfg.RivalSpread + 1));
            r.ChorusProgress += Math.Max(0, cfg.ChorusRate + pace + rng.NextInt(cfg.RivalSpread + 1));

            // Story beats, once each: the rival's fleet, then the third fleet, getting ahead of you.
            if (!r.RivalPassed && r.ConcordProgress > r.PlayerProgress && r.NodesVisited > 1)
            {
                r.RivalPassed = true;
                if (c.Faction.RivalPasses != "") r.Journal.Add(c.Faction.RivalPasses);
            }
            if (!r.ThirdPassed && r.ChorusProgress > r.PlayerProgress && r.NodesVisited > 1)
            {
                r.ThirdPassed = true;
                if (c.Faction.ThirdPasses != "") r.Journal.Add(c.Faction.ThirdPasses);
            }

            // Slipping past the commander: they move to a lane further on, and their scouts report your course.
            var nem = r.Nemesis;
            if (nem.Hunting && nem.Node >= 0 && nem.Node != node.Id && r.Nodes[nem.Node].Layer <= node.Layer)
            {
                nem.LastOutcome = NemesisOutcome.Dodged;
                r.ConcordProgress += cfg.NemesisDodgeDelay;
                PlaceNemesis(r, c, rng, node.Layer + 2);
                r.Journal.Add($"{c.Nemesis(nem.Id).Name} was left behind, and has moved to cut you off.");
            }

            switch (node.Kind)
            {
                case NodeKind.Battle:
                case NodeKind.Elite:
                case NodeKind.Boss:
                    r.PendingEncounter = node.Encounter;
                    r.PendingHazard = node.Hazard ?? "";
                    r.PendingRewardPercent = r.PendingHazard != "" ? cfg.HazardScrapPercent : 100;
                    r.Phase = RunPhase.Battle;
                    break;
                case NodeKind.Nemesis:
                    r.PendingHazard = "";
                    r.PendingHail = "";
                    r.Phase = RunPhase.Hail;
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
            var enc = PendingEncounterDef(r, c);
            var cfg = c.Config;
            ulong seed = BattleSeed(r);
            var b = new Battle.Battle(seed) { Title = enc.Name, Ordinals = r.Ordinals, DartsInHangar = r.Darts };
            if (enc.TurnLimit > 0) b.TurnLimit = enc.TurnLimit;
            b.CrewName = c.Faction.CrewName;
            string hazard = enc.Tier <= 2 ? r.PendingHazard ?? "" : "";
            b.Hazard = Hazards.Name(hazard);
            b.HazardText = Hazards.Text(hazard);
            if (b.Hazard != "") b.Title = $"{enc.Name} - {b.Hazard}";
            b.NoShieldRegen = hazard == "ion_storm";
            b.RangePenalty = hazard == "nebula" ? 1 : 0;
            b.DartSpec = UnitLibrary.Get(c.Faction.CraftUnit);
            b.GunshipSpec = UnitLibrary.Get(c.Faction.EnemyCraftUnit);
            b.FirstTurnCommand = RelicSum(r, c, x => x.FirstTurnCommand);
            var fit = r.Flagship.Alive ? r.Officers.Where(o => !o.Wounded).Select(o => c.Officer(o.Id)).ToList() : new List<OfficerDef>();
            b.RepairBonus = fit.Sum(o => o.RepairBonus);
            b.FlagshipWeaponBonus = fit.Sum(o => o.WeaponBonus);
            b.CraftDamageBonus = fit.Sum(o => o.CraftBonus);
            b.FirstTurnDraw = fit.Sum(o => o.FirstTurnDraw);

            var units = Roster(r, c, enc);
            var nem = r.Nemesis;
            bool nemesisFight = enc.Id == NemesisEncounterId;
            string commanderShip = nemesisFight ? c.Nemesis(nem.Id).Ship : null;

            // The fleets start already closing, so the first turn is a fight and not an approach.
            int playerCol = Math.Max(0, Math.Min(b.Board.Width - 5, cfg.PlayerStartCol));
            // Slow heavies (carriers, tenders, the boss) hold the back line; their escorts and craft come forward.
            int EnemyCol(EncounterUnit u)
            {
                var spec = UnitLibrary.Get(u.Unit);
                bool holds = spec.Kind == UnitKind.Capital && spec.MaxSpeed <= 1;
                return holds ? u.Col : Math.Max(playerCol + 3, u.Col - cfg.EnemyAdvance);
            }

            var taken = new HashSet<HexCoord> { HexCoord.FromOffset(playerCol, 2), HexCoord.FromOffset(playerCol, 4) };
            foreach (var u in units) taken.Add(HexCoord.FromOffset(EnemyCol(u), u.Row));
            foreach (var (col, row, asteroid) in enc.Terrain)
            {
                if (taken.Contains(HexCoord.FromOffset(col, row))) continue; // a ship is there: ships win
                b.Board.Set(col, row, asteroid ? Terrain.Asteroid : Terrain.Debris);
                taken.Add(HexCoord.FromOffset(col, row));
            }
            var terrainRng = new Pcg32(seed, 91UL);
            Scatter(b, terrainRng, taken, enc.ScatterAsteroids + (hazard == "asteroid_field" ? 4 : 0), Terrain.Asteroid);
            Scatter(b, terrainRng, taken, enc.ScatterDebris + (hazard == "asteroid_field" ? 2 : 0), Terrain.Debris);

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
                    spec.HeatCap += RelicSum(r, c, x => x.HeatCap);
                    spec.Shields += RelicSum(r, c, x => x.Shields) + fit.Sum(o => o.ShieldBonus);
                    spec.MaxSpeed = Math.Max(1, spec.MaxSpeed);
                }
                var unit = b.AddUnit(spec, Side.Player, playerCol, i == FlagshipIndex ? 2 : 4, HexDirection.E, 1, i == FlagshipIndex);
                unit.Hull = Math.Max(1, Math.Min(ship.Hull, ship.MaxHull));
                ids[i] = unit.Id;
            }

            foreach (var u in units)
            {
                var spec = UnitLibrary.Get(u.Unit);
                bool commander = u.Unit == commanderShip;
                if (commander)
                {
                    spec.Hull += (nem.Rank - 1) * cfg.NemesisRankHull;
                    if (nem.Adaptation == "guns")
                    {
                        spec.Armor[ArcIndex.Port] += 2;
                        spec.Armor[ArcIndex.Starboard] += 2;
                    }
                    else if (nem.Adaptation == "other") spec.Hull += 6;
                }
                var unit = b.AddUnit(spec, Side.Enemy, EnemyCol(u), u.Row, u.Facing, u.Speed);
                if (commander)
                {
                    unit.IsCommander = true;
                    unit.LanceWard = nem.Adaptation == "lance" ? 3 : 0;
                    unit.CraftWard = nem.Adaptation == "craft" ? 1 : 0;
                    if (nemesisFight && nem.Rank < 3) unit.FleesAtHull = Math.Max(1, unit.MaxHull * cfg.NemesisFleePercent / 100);
                }
                else if (nemesisFight && r.PendingHail == "board") unit.StunnedThroughTurn = cfg.HailBoardTurns;
            }
            if (nemesisFight)
            {
                if (r.PendingHail == "edict")
                {
                    b.EnemyDamageBonus = cfg.HailEdictDamage;
                    b.EnemyFocusFlagship = true;
                }
                else if (r.PendingHail == "fire") b.FirstTurnCommand++;
            }

            foreach (var card in r.Deck)
            {
                if (card.Ship < 0) b.AddCard(PaperCards.Get(card.Card), -1);
                else if (card.Ship < ids.Length && ids[card.Ship] >= 0) b.AddCard(PaperCards.Get(card.Card), ids[card.Ship]);
            }
            return b;
        }

        /// <summary>Every enemy in the pending fight as it stands now: the listed units and, at the gate, any reinforcements.</summary>
        public static List<EncounterUnit> Roster(RunState r, RunContent c, EncounterDef enc)
        {
            var units = new List<EncounterUnit>(enc.Units);
            if (enc.Tier == 3 && (ConcordAhead(r) || r.VennEnraged)) units.AddRange(enc.Reinforcements);
            return units;
        }

        /// <summary>The nemesis fight as it would be now: the commander's ship and the escorts their rank brings.</summary>
        public static EncounterDef NemesisEncounter(RunState r, RunContent c)
        {
            var def = c.Nemesis(r.Nemesis.Id);
            int rank = Math.Max(1, Math.Min(3, r.Nemesis.Rank));
            var enc = new EncounterDef
            {
                Id = NemesisEncounterId, Name = def.Name, Tier = 5, Intro = def.Tactic, Scrap = def.Scrap + 8 * (rank - 1),
                TurnLimit = 25, ScatterAsteroids = 2, ScatterDebris = 1,
            };
            enc.Units.Add(new EncounterUnit { Unit = def.Ship, Col = 8, Row = 3, Facing = HexDirection.W, Speed = 1 });
            enc.Units.AddRange(def.Escorts[rank - 1]);
            return enc;
        }

        /// <summary>Why a hail reply can't be given (it costs Ordinals the fleet does not have), or null.</summary>
        public static string CanReply(RunState r, HailReply reply)
            => r.Ordinals < reply.Ordinals ? $"needs {reply.Ordinals} crew" : r.Approval < reply.Approval ? $"needs {reply.Approval} approval" : null;

        /// <summary>Answer the commander's hail. The reply shapes the battle that follows at once.</summary>
        public static string ChooseHail(RunState r, RunContent c, int replyIndex)
        {
            if (r.Phase != RunPhase.Hail) return "nobody is hailing";
            if (replyIndex < 0 || replyIndex >= c.HailReplies.Count) return "no such reply";
            var reply = c.HailReplies[replyIndex];
            string blocked = CanReply(r, reply);
            if (blocked != null) return blocked;
            r.Ordinals -= reply.Ordinals;
            r.Approval -= reply.Approval;
            r.PendingHail = reply.Effect;
            if (reply.Effect == "fire") r.Nemesis.Grudge = 3;
            r.PendingEncounter = NemesisEncounterId;
            r.PendingRewardPercent = reply.Effect == "edict" ? c.Config.HailEdictScrapPercent : 100;
            r.Journal.Add($"{c.Nemesis(r.Nemesis.Id).Name} hailed. Reply: {reply.Tag}.");
            r.Phase = RunPhase.Battle;
            return null;
        }

        /// <summary>What a reply does, in numbers taken from the config so the text can't drift from the rules.</summary>
        public static string HailEffect(RunContent c, HailReply reply)
        {
            var cfg = c.Config;
            string price = reply.Approval > 0 ? $"Spend {reply.Approval} approval" : $"Expend {reply.Ordinals} {c.Faction.CrewName}";
            switch (reply.Effect)
            {
                case "edict": return $"Enraged: every enemy attack deals +{cfg.HailEdictDamage} and all of it is aimed at the flagship. Salvage x{cfg.HailEdictScrapPercent / 100}.{cfg.HailEdictScrapPercent % 100 / 10}";
                case "fire": return "+1 command on turn 1. Their grudge goes to 3: if they get away they return at rank III";
                case "board": return $"{price}: their escorts get no orders for the first {cfg.HailBoardTurns} turns";
                default: return "No effect";
            }
        }

        static readonly string[] DamageSources = { "lance", "guns", "craft", "other" };

        /// <summary>The mark left by whatever hurt the commander most.</summary>
        public static string ScarText(string source)
        {
            switch (source)
            {
                case "lance": return "a burn the length of the prow";
                case "guns": return "one flank's batteries slagged by broadsides";
                case "craft": return "a hangar deck gutted by darts";
                case "other": return "three decks sealed after the boarding";
                default: return "";
            }
        }

        /// <summary>What the refit against that source does, for the dossier.</summary>
        public static string AdaptationText(string source)
        {
            switch (source)
            {
                case "lance": return "Baffles: every lance or mass driver hit deals 3 less";
                case "guns": return "Flank plating: +2 armor port and starboard";
                case "craft": return "Point defence: darts deal 1 less";
                case "other": return "Sealed decks: +6 hull";
                default: return "";
            }
        }

        /// <summary>After a fight the commander was in: dead and looted, or away and changed by it.</summary>
        static void ResolveNemesis(RunState r, RunContent c, Battle.Battle b, Pcg32 rng)
        {
            var n = r.Nemesis;
            var def = c.Nemesis(n.Id);
            var ship = b.Units.Find(u => u.IsCommander);
            if (ship == null) return;
            n.Meetings++;
            if (!ship.Alive && !ship.Escaped)
            {
                n.Alive = false;
                n.Node = -1;
                n.LastOutcome = NemesisOutcome.Killed;
                r.ConcordProgress = Math.Max(0, r.ConcordProgress - c.Config.NemesisSlow);
                r.Journal.Add($"{def.Name} is dead. {def.Epitaph}".Trim());
                if (!string.IsNullOrEmpty(def.Trophy) && !r.Relics.Contains(def.Trophy))
                {
                    GainRelic(r, c, def.Trophy);
                    r.Reward.Relic = def.Trophy;
                }
                return;
            }
            n.LastOutcome = ship.Escaped ? NemesisOutcome.Escaped : NemesisOutcome.Standoff;
            n.Grudge = Math.Min(3, n.Grudge + 1);
            string worst = "";
            int most = 0;
            foreach (string source in DamageSources)
                if (b.CommanderDamage.TryGetValue(source, out int dealt) && dealt > most) { most = dealt; worst = source; }
            if (worst != "")
            {
                n.Adaptation = worst;
                n.Scars.Add(ScarText(worst));
            }
            n.Rank = n.Grudge >= 3 ? 3 : Math.Min(3, n.Rank + 1);
            PlaceNemesis(r, c, rng, r.Current.Layer + 2);
            r.Journal.Add(ship.Escaped
                ? $"{def.Name} broke off with {ship.Hull} hull left. They will be back, refitted."
                : $"{def.Name} held you to a stand-off. They will be back.");
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
            var enc = PendingEncounterDef(r, c);
            bool nemesisFight = enc.Id == NemesisEncounterId;
            var rng = Rng(r);
            r.Reward = new RewardOffer();
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

            // Officers: every critical hit on the flagship puts one in the sickbay; losing the flagship puts them all there.
            var flagUnit = players.Find(u => u.IsFlagship);
            if (flagUnit != null && r.Officers.Count > 0)
            {
                int hurt = flagUnit.Alive ? b.Log.Count(e => e.Kind == EventKind.Critical && e.UnitId == flagUnit.Id) : r.Officers.Count;
                for (int i = 0; i < hurt; i++)
                {
                    var well = r.Officers.Where(o => !o.Wounded).ToList();
                    if (well.Count == 0) break;
                    var hit = rng.Pick(well);
                    hit.Wounded = true;
                    r.Journal.Add($"{c.Officer(hit.Id).Name} was wounded.");
                }
            }
            bool lostShip = players.Any(u => !u.Alive);

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
                    if (c.Faction.HasCouncil) r.Approval = Math.Max(0, r.Approval - 1);
                    r.Journal.Add($"{enc.Name}: both fleets disengaged after {b.Turn} turns. Time lost, nothing gained.");
                    if (nemesisFight) ResolveNemesis(r, c, b, rng);
                    RecoverFlagship(r, c);
                    r.Phase = RunPhase.Map;
                }
                Keep(r, rng);
                return;
            }

            r.BattlesWon++;
            if (!r.Escort.Alive && r.Escort.Hull == 0 && players.Any(u => !u.IsFlagship && !u.Alive)) r.Journal.Add($"{enc.Name}: the {r.Escort.Name} was lost.");
            RecoverFlagship(r, c);

            if (enc.Tier == 3)
            {
                r.Journal.Add($"{enc.Name}: the gate is open after {b.Turn} turns.");
                End(r, RunResult.Victory);
                Keep(r, rng);
                return;
            }

            int scrap = (enc.Scrap + rng.NextInt(9)) * (r.PendingRewardPercent + RelicSum(r, c, x => x.ScrapPercent)) / 100;
            int perVictory = cfg.OrdinalsPerVictory + RelicSum(r, c, x => x.OrdinalsPerVictory) + r.Officers.Where(o => !o.Wounded).Sum(o => c.Officer(o.Id).CrewPerVictory);
            int ordinals = Math.Min(perVictory, r.MaxOrdinals - r.Ordinals);
            // The Council watches results: a clean win earns approval, a win that cost a ship earns none.
            if (c.Faction.HasCouncil && !lostShip) r.Approval = Math.Min(c.Faction.MaxApproval, r.Approval + (enc.Tier == 2 ? 2 : 1));
            r.Scrap += scrap;
            r.Ordinals += Math.Max(0, ordinals);
            if (nemesisFight) ResolveNemesis(r, c, b, rng);
            bool killed = nemesisFight && !r.Nemesis.Alive;
            if (enc.Tier == 2)
            {
                // An elite carries something worth taking: one relic from the pool, never a commander's trophy.
                // Two are offered and one is taken, so two runs through the same elite need not come out the same.
                var pool = c.Relics.Where(x => !x.Trophy && !r.Relics.Contains(x.Id)).ToList();
                rng.Shuffle(pool);
                r.Reward.RelicChoices = pool.Take(2).Select(x => x.Id).ToList();
            }
            r.Reward.Active = true;
            r.Reward.Scrap = scrap;
            r.Reward.Ordinals = Math.Max(0, ordinals);
            r.Reward.Cards = PickCards(r, c, rng, cfg.RewardCards + (enc.Tier == 2 || killed ? 1 : 0)).Select(x => new DeckCard(x.Card, x.Ship)).ToList();
            r.Reward.Note = nemesisFight && !killed ? $"{enc.Name} got away. Their escorts did not."
                : killed ? $"{enc.Name} is dead. {c.Nemesis(r.Nemesis.Id).Epitaph}".Trim() : $"{enc.Name} won in {b.Turn} turns. {enc.Aftermath}".Trim();
            r.Journal.Add($"{enc.Name}: won in {b.Turn} turns.");
            r.Phase = RunPhase.Reward;
            Keep(r, rng);
        }

        /// <summary>
        /// The flagship rule: if it was lost but the battle was survived, it is rebuilt at a high cost
        /// (Ordinals and permanent hull) and comes back at half strength.
        /// </summary>
        static void RecoverFlagship(RunState r, RunContent c)
        {
            var cfg = c.Config;
            var flag = r.Flagship;
            if (flag.Alive) return;
            flag.Alive = true;
            flag.MaxHull = Math.Max(10, flag.MaxHull - cfg.FlagshipRebuildMaxHullLoss);
            flag.Hull = flag.MaxHull / 2;
            int paid = Math.Min(r.Ordinals, cfg.FlagshipRebuildOrdinals);
            r.Ordinals -= paid;
            r.Journal.Add($"The {flag.Name} was rebuilt from her wreck: {paid} {c.Faction.CrewName} spent, {cfg.FlagshipRebuildMaxHullLoss} hull lost for good.");
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

        /// <summary>Take one of the relics an elite was carrying. It has to be done before the rest of the reward.</summary>
        public static string ChooseRelic(RunState r, RunContent c, int index)
        {
            if (r.Phase != RunPhase.Reward || !r.Reward.Active) return "no reward is waiting";
            if (index < 0 || index >= r.Reward.RelicChoices.Count) return "no such relic";
            string id = r.Reward.RelicChoices[index];
            GainRelic(r, c, id);
            r.Reward.Relic = id;
            r.Reward.RelicChoices.Clear();
            return null;
        }

        /// <summary>The briefing has been read. Changes nothing else.</summary>
        public static void DismissBriefing(RunState r) => r.BriefingSeen = true;

        /// <summary>Take one of the offered cards, or -1 to take none.</summary>
        public static string TakeReward(RunState r, int cardIndex)
        {
            if (r.Phase != RunPhase.Reward || !r.Reward.Active) return "no reward is waiting";
            if (r.Reward.RelicChoices.Count > 0) return "choose a relic first";
            if (cardIndex >= r.Reward.Cards.Count) return "no such card";
            if (cardIndex >= 0) r.Deck.Add(r.Reward.Cards[cardIndex]);
            r.Reward.Active = false;
            r.Phase = RunPhase.Map;
            return null;
        }

        public static int FieldRepairAmount(RunState r, RunContent c) => c.Config.FieldRepair + RelicSum(r, c, x => x.FieldRepair);

        /// <summary>Instead of a card: spend the lull on field repairs to every surviving ship.</summary>
        public static string TakeFieldRepair(RunState r, RunContent c)
        {
            if (r.Phase != RunPhase.Reward || !r.Reward.Active) return "no reward is waiting";
            if (r.Reward.RelicChoices.Count > 0) return "choose a relic first";
            int hull = FieldRepairAmount(r, c);
            foreach (var s in r.Ships)
                if (s.Alive) s.Hull = Math.Min(s.MaxHull, s.Hull + hull);
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
                if (o.Kind == OutcomeKind.Ordinals && r.Ordinals + o.Amount < 0) return $"needs {-o.Amount} crew";
                if (o.Kind == OutcomeKind.Approval && r.Approval + o.Amount < 0) return $"needs {-o.Amount} approval";
                if (o.Kind == OutcomeKind.Darts && r.Darts + o.Amount < 0) return $"needs {-o.Amount} craft aboard";
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
                        effects.Add($"{Signed(r.Ordinals - before)} {c.Faction.CrewName}");
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
                    case OutcomeKind.Approval:
                    {
                        int before = r.Approval;
                        r.Approval = Math.Max(0, Math.Min(c.Faction.MaxApproval, r.Approval + o.Amount));
                        effects.Add($"{Signed(r.Approval - before)} approval");
                        break;
                    }
                    case OutcomeKind.GainOfficer:
                    {
                        string name = CommissionOfficer(r, c);
                        if (name != null) effects.Add($"{name} came aboard");
                        break;
                    }
                    case OutcomeKind.HealOfficers:
                        foreach (var officer in r.Officers) officer.Wounded = false;
                        effects.Add("every officer is back on duty");
                        break;
                    case OutcomeKind.EnrageBoss:
                        r.VennEnraged = true;
                        effects.Add("the gate fleet will be reinforced");
                        break;
                    case OutcomeKind.GainRelic:
                    {
                        var pool = c.Relics.Where(x => !x.Trophy && !r.Relics.Contains(x.Id)).ToList();
                        if (pool.Count == 0) break;
                        var relic = pool[(int)(((ulong)r.Seed + (ulong)r.NodesVisited * 13UL + (ulong)r.Relics.Count * 5UL) % (ulong)pool.Count)];
                        GainRelic(r, c, relic.Id);
                        effects.Add($"relic: {relic.Name} ({relic.Text.TrimEnd('.')})");
                        break;
                    }
                    case OutcomeKind.StartBattle:
                        r.PendingHazard = "";
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
            r.Journal.Add($"A new {r.Escort.Name} joined the fleet.");
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

        // ================================================================ officers and the Council

        /// <summary>Bring aboard an officer the fleet does not have yet, if there is a berth. Returns their name, or null.</summary>
        static string CommissionOfficer(RunState r, RunContent c)
        {
            if (r.Officers.Count >= c.Faction.MaxOfficers) return null;
            var pool = c.Officers.Where(o => r.Officers.All(have => have.Id != o.Id)).ToList();
            if (pool.Count == 0) return null;
            // Chosen from the run seed and the roster so far, so a vote and an event on the same node do not conflict over the run RNG.
            var pick = pool[(int)(((ulong)r.Seed + (ulong)r.Officers.Count * 31UL + (ulong)r.NodesVisited * 7UL) % (ulong)pool.Count)];
            r.Officers.Add(new OfficerState { Id = pick.Id });
            r.Journal.Add($"{pick.Name}, {pick.Role}, came aboard.");
            return pick.Name;
        }

        /// <summary>Why the Council can't be asked for this now, or null.</summary>
        public static string CanCallVote(RunState r, RunContent c, MotionDef m)
        {
            if (!c.Faction.HasCouncil) return "this fleet answers to nobody's vote";
            if (r.Phase != RunPhase.Map) return "the Council sits between jumps";
            if (r.Approval < m.Cost) return $"needs {m.Cost} approval";
            if (m.Officer && r.Officers.Count >= c.Faction.MaxOfficers) return "every officer's berth is taken";
            if (m.Officer && c.Officers.All(o => r.Officers.Any(have => have.Id == o.Id))) return "nobody is left to commission";
            if (m.HealOfficers && r.Officers.All(o => !o.Wounded)) return "no officer is wounded";
            if (m.Hull > 0 && r.Ships.All(s => !s.Alive || s.Hull >= s.MaxHull)) return "no ship is damaged";
            if (m.Craft > 0 && r.Darts >= r.MaxDarts) return "the hangar is full";
            if (m.Crew > 0 && r.Ordinals >= r.MaxOrdinals) return "the crew is at strength";
            return null;
        }

        /// <summary>Put a motion to the Council: approval is spent and the fleet gets what it voted for at once.</summary>
        public static string CallVote(RunState r, RunContent c, int motionIndex)
        {
            if (motionIndex < 0 || motionIndex >= c.Motions.Count) return "no such motion";
            var m = c.Motions[motionIndex];
            string blocked = CanCallVote(r, c, m);
            if (blocked != null) return blocked;
            r.Approval -= m.Cost;
            if (m.Hull > 0)
                foreach (var s in r.Ships.Where(s => s.Alive)) s.Hull = Math.Min(s.MaxHull, s.Hull + m.Hull);
            r.Scrap += m.Scrap;
            if (m.Craft > 0) r.Darts = Math.Min(r.MaxDarts, r.Darts + m.Craft);
            if (m.Crew > 0) r.Ordinals = Math.Min(r.MaxOrdinals, r.Ordinals + m.Crew);
            if (m.RivalsBack > 0)
            {
                r.ConcordProgress = Math.Max(0, r.ConcordProgress - m.RivalsBack);
                r.ChorusProgress = Math.Max(0, r.ChorusProgress - m.RivalsBack);
            }
            if (m.HealOfficers)
                foreach (var officer in r.Officers) officer.Wounded = false;
            if (m.Officer) CommissionOfficer(r, c);
            r.Journal.Add($"The Council voted: {m.Name}.");
            return null;
        }

        /// <summary>Shipyard sickbay: one wounded officer back on duty.</summary>
        public static string HealOfficer(RunState r, RunContent c)
        {
            var patient = r.Officers.Find(x => x.Wounded);
            if (patient == null) return "no officer is wounded";
            string err = Pay(r, c.Config.HealOfficerCost);
            if (err != null) return err;
            patient.Wounded = false;
            return null;
        }

        /// <summary>Seed false beacons from the yard's relays: both rivals lose ground. Once per visit, no dock time.</summary>
        public static string Sabotage(RunState r, RunContent c)
        {
            if (r.Phase != RunPhase.Shipyard || !r.Shop.Active) return "not at a shipyard";
            if (r.Shop.Sabotaged) return "the beacons are already seeded";
            string err = Pay(r, c.Config.SabotageCost);
            if (err != null) return err;
            r.Shop.Purchases--;
            r.Shop.Sabotaged = true;
            r.ConcordProgress = Math.Max(0, r.ConcordProgress - c.Config.SabotageAmount);
            r.ChorusProgress = Math.Max(0, r.ChorusProgress - c.Config.SabotageAmount);
            r.Journal.Add("False beacons seeded. The rivals are chasing echoes.");
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
            int score = r.BattlesWon * 50 + r.NodesVisited * 10 + r.Scrap + r.Relics.Count * 25;
            if (r.Nemesis.LastOutcome == NemesisOutcome.Killed) score += 100;
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
