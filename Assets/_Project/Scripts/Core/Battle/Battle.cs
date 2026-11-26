using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Silence.Core.Hex;
using Silence.Core.Random;

namespace Silence.Core.Battle
{
    public enum BattlePhase { Setup, Intent, Command, Drift, EnemyAction, End, Over }

    /// <summary>Withdrawal: neither side could finish the other before <see cref="Battle.TurnLimit"/> (placeholder rule).</summary>
    public enum BattleOutcome { Ongoing, Victory, Defeat, Withdrawal }

    public struct DamageFlags
    {
        public bool IgnoreShields;
        public bool IgnoreArmor;
        /// <summary>Any hull damage crits (Waymind Targeting), not just 5 or more.</summary>
        public bool CritOnAnyHull;
        /// <summary>Straight to hull: no arc, shields, armor or crit (Reactor Strain, rams).</summary>
        public bool Direct;
    }

    /// <summary>
    /// One battle: board, units, deck, intents and the rules that move it forward. Pure and deterministic:
    /// the same seed, setup and list of player actions always produce the same events.
    /// The view calls <see cref="PlayCard"/>, <see cref="CommandCraft"/> and <see cref="EndTurn"/> and
    /// animates <see cref="Log"/>; it never changes state directly.
    /// </summary>
    public sealed partial class Battle
    {
        public readonly Board Board;
        public readonly Pcg32 Rng;
        public readonly List<Unit> Units = new List<Unit>();
        public readonly Deck Deck = new Deck();
        public readonly List<Intent> Intents = new List<Intent>();
        public readonly List<BattleEvent> Log = new List<BattleEvent>();

        public int Turn { get; private set; }
        public BattlePhase Phase { get; private set; } = BattlePhase.Setup;
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;

        public int Command;
        public int CommandPerTurn = 3;
        public int HandSize = 5;
        /// <summary>Fleet-wide Ascendancy crew. At 0 nothing can be Expended and every card costs +1.</summary>
        public int Ordinals = 24;
        public bool WaymindPrimed;
        /// <summary>Darts left aboard to launch. Losses carry over between battles; -1 = unlimited.</summary>
        public int DartsInHangar = -1;
        /// <summary>Shown on the battle screen ("Intercept at the Ring").</summary>
        public string Title = "";
        /// <summary>False keeps the draw pile in the order the cards were added (the tutorial's fixed opening hands).</summary>
        public bool ShuffleAtStart = true;
        /// <summary>After this many turns the fleets disengage (Withdrawal). Placeholder until the spec decides; 0 = no limit.</summary>
        public int TurnLimit = 25;

        public UnitSpec DartSpec = PaperUnits.Dart;
        public UnitSpec GunshipSpec = PaperUnits.Gunship;
        public UnitSpec TorpedoSpec = PaperUnits.Torpedo;
        public CardDef FireOnDeck = PaperCards.FireOnDeck;
        public CardDef HullBreach = PaperCards.HullBreach;
        public CardDef JammedMount = PaperCards.JammedMount;

        public const int CollisionPerSpeed = 2;
        public const int CritThreshold = 5;
        public const int ReactorStrainMargin = 4;
        public const int ReactorStrainDamage = 6;

        int nextUnitId = 1;
        int nextCardId = 1;

        public Battle(ulong seed, Board board = null)
        {
            Rng = new Pcg32(seed);
            Board = board ?? new Board();
        }

        // ================================================================ setup

        public Unit AddUnit(UnitSpec spec, Side side, int col, int row, HexDirection facing, int speed, bool flagship = false)
        {
            var u = Unit.From(spec, nextUnitId++, side, HexCoord.FromOffset(col, row), facing, speed);
            u.IsFlagship = flagship;
            Units.Add(u);
            return u;
        }

        public CardInstance AddCard(CardDef def, int sourceId)
        {
            var c = new CardInstance { Id = nextCardId++, Def = def, SourceId = sourceId };
            Deck.DrawPile.Add(c);
            return c;
        }

        /// <summary>Shuffle the deck and run the first Intent step and draw.</summary>
        public void Start()
        {
            if (Phase != BattlePhase.Setup) throw new InvalidOperationException("battle already started");
            if (ShuffleAtStart) Rng.Shuffle(Deck.DrawPile);
            Emit(EventKind.BattleStarted, -1, -1, 0, $"Battle starts: {Units.Count} units, {Deck.Count} cards");
            BeginTurn();
        }

        // ================================================================ queries

        public Unit Get(int id) => Units.Find(u => u.Id == id);

        public IEnumerable<Unit> LiveUnits(Side side) => Units.Where(u => u.Alive && u.Side == side);

        public Unit Flagship => Units.Find(u => u.Alive && u.IsFlagship);

        public Unit UnitAt(HexCoord h) => Units.Find(u => u.Alive && u.Pos == h);

        public bool IsFree(HexCoord h) => !Board.BlocksMovement(h) && UnitAt(h) == null;

        public IEnumerable<HexCoord> FreeNeighbors(HexCoord h)
        {
            for (int d = 0; d < HexDirections.Count; d++)
            {
                var n = h.Neighbor((HexDirection)d);
                if (IsFree(n)) yield return n;
            }
        }

        public int PlayerCraftCount() => LiveUnits(Side.Player).Count(u => u.Kind == UnitKind.Craft);

        public int CostOf(CardInstance c) => c.Def.Cost + c.TempCost + (Ordinals <= 0 ? 1 : 0);

        /// <summary>Hexes reachable in up to <paramref name="steps"/> moves through free hexes, in a fixed order.</summary>
        public List<HexCoord> Reachable(HexCoord start, int steps)
        {
            var order = new List<HexCoord> { start };
            var dist = new Dictionary<HexCoord, int> { [start] = 0 };
            for (int i = 0; i < order.Count; i++)
            {
                var h = order[i];
                if (dist[h] >= steps) continue;
                for (int d = 0; d < HexDirections.Count; d++)
                {
                    var n = h.Neighbor((HexDirection)d);
                    if (dist.ContainsKey(n) || !IsFree(n)) continue;
                    dist[n] = dist[h] + 1;
                    order.Add(n);
                }
            }
            return order;
        }

        /// <summary>Where a capital ship will end its drift if nothing else moves (for drift ghosts).</summary>
        public HexCoord PredictDrift(Unit u)
        {
            var pos = u.Pos;
            for (int i = 0; i < u.Speed; i++)
            {
                var next = pos.Neighbor(u.Facing);
                if (Board.BlocksMovement(next) || UnitAt(next) != null) break;
                pos = next;
            }
            return pos;
        }

        // ================================================================ player actions

        /// <summary>Why a card can't be played with this choice, or null when it can. Changes nothing.</summary>
        public string CheckPlay(int cardId, PlayTarget choice) => Prepare(cardId, choice, out _, out _, out _);

        string Prepare(int cardId, PlayTarget choice, out PlayContext ctx, out int cost, out bool expend)
        {
            ctx = null;
            cost = 0;
            expend = false;
            if (Phase != BattlePhase.Command) return "cards can only be played in the Command step";
            var card = Deck.FindInHand(cardId);
            if (card == null) return "card is not in hand";
            var def = card.Def;
            if (def.Unplayable) return $"{def.Name} is unplayable";

            Unit source = null;
            if (card.SourceId >= 0)
            {
                source = Get(card.SourceId);
                if (source == null || !source.Alive) return "the card's ship is gone";
            }

            cost = CostOf(card);
            if (cost > Command) return $"needs {cost} command, have {Command}";

            expend = def.ExpendCost > 0 && (!def.ExpendOptional || choice.Expend);
            if (expend && Ordinals < def.ExpendCost) return $"needs {def.ExpendCost} Ordinals, have {Ordinals}";

            Unit target = null;
            switch (def.Target)
            {
                case TargetKind.Enemy:
                    target = Get(choice.UnitId);
                    if (target == null || !target.Alive || target.Side != Side.Enemy) return "choose an enemy";
                    break;
                case TargetKind.AllyCapital:
                    target = Get(choice.UnitId);
                    if (target == null || !target.Alive || target.Side != Side.Player || !target.IsCapital)
                        return "choose one of your capital ships";
                    break;
            }

            ctx = new PlayContext { Card = card, Source = source, Target = target, Choice = choice, Expended = expend };
            foreach (var e in def.Effects)
            {
                string err = e.Validate(this, ctx);
                if (err != null) return err;
            }
            return null;
        }

        /// <summary>Play a card from hand. Returns null on success, otherwise why it can't be played (nothing changes).</summary>
        public string PlayCard(int cardId, PlayTarget choice)
        {
            string error = Prepare(cardId, choice, out var ctx, out int cost, out bool expend);
            if (error != null) return error;
            var card = ctx.Card;
            var def = card.Def;
            var source = ctx.Source;
            var target = ctx.Target;

            Command -= cost;
            if (expend)
            {
                Ordinals -= def.ExpendCost;
                Emit(EventKind.Ordinals, -1, -1, -def.ExpendCost, $"Expend {def.ExpendCost} Ordinals ({Ordinals} left)");
            }
            Deck.Hand.Remove(card);
            (def.Exhaust ? Deck.Exhausted : Deck.Discard).Add(card);
            Emit(EventKind.CardPlayed, source?.Id ?? -1, target?.Id ?? -1, cost,
                $"Play {def.Name}" + (target != null ? $" on {target.Name}" : "") + (expend ? " (Expend)" : ""));

            foreach (var e in def.Effects)
            {
                e.Apply(this, ctx);
                if (Outcome != BattleOutcome.Ongoing) break;
            }
            return null;
        }

        /// <summary>
        /// Move one of the player's small craft up to its move, then optionally attack an adjacent enemy
        /// (craft, torpedo or ship). Each craft acts once per turn.
        /// </summary>
        public string CommandCraft(int craftId, HexCoord dest, int attackTargetId = -1)
        {
            if (Phase != BattlePhase.Command) return "craft can only act in the Command step";
            var c = Get(craftId);
            if (c == null || !c.Alive || c.Side != Side.Player || c.Kind != UnitKind.Craft) return "choose one of your craft";
            if (c.CraftActed) return $"{c.Name} has already acted this turn";
            if (!Reachable(c.Pos, c.Move + c.CraftBonusMove).Contains(dest)) return "destination is out of reach";

            Unit target = null;
            if (attackTargetId >= 0)
            {
                target = Get(attackTargetId);
                if (target == null || !target.Alive || target.Side != Side.Enemy) return "choose an enemy to attack";
                if (target.Pos.DistanceTo(dest) != 1) return "target must be next to the destination";
            }

            if (dest != c.Pos)
            {
                Emit(EventKind.Moved, c.Id, -1, c.Pos.DistanceTo(dest), $"{c.Name} moves", c.Pos, dest);
                c.Pos = dest;
            }
            c.CraftActed = true;

            if (target != null)
            {
                for (int i = 0; i < c.CraftAttacks && target.Alive; i++)
                {
                    Emit(EventKind.Attack, c.Id, target.Id, c.AttackDamage, $"{c.Name} attacks {target.Name}");
                    DealDamage(target, c.AttackDamage, c.Pos, new DamageFlags(), c);
                }
            }
            return null;
        }

        /// <summary>Heat for speeding up with the throttle.</summary>
        public const int ThrottleHeat = 1;

        /// <summary>Why this ship's throttle can't move by <paramref name="delta"/> (+1 or -1) right now, or null if it can.</summary>
        public string CheckThrottle(int unitId, int delta)
        {
            if (Phase != BattlePhase.Command) return "speed can only be changed in the Command step";
            var u = Get(unitId);
            if (u == null || !u.Alive || u.Side != Side.Player || !u.IsCapital) return "choose one of your capital ships";
            if (delta != 1 && delta != -1) return "the throttle moves one step at a time";
            if (u.ThrottleUsed) return $"{u.Name} has already changed speed this turn";
            if (delta > 0 && u.NoBurnThisTurn) return "engines are damaged: no speeding up this turn";
            if (delta > 0 && u.Speed >= u.MaxSpeed) return "already at max speed";
            if (delta < 0 && u.Speed <= 0) return "already stopped";
            return null;
        }

        /// <summary>
        /// The helm: each capital ship may change its Speed by one step once a turn, for free. Speeding up adds
        /// <see cref="ThrottleHeat"/> heat; slowing down costs nothing. Speed decides how far the ship drifts.
        /// </summary>
        public string Throttle(int unitId, int delta)
        {
            string error = CheckThrottle(unitId, delta);
            if (error != null) return error;
            var u = Get(unitId);
            int before = u.Speed;
            u.Speed += delta;
            u.ThrottleUsed = true;
            Emit(EventKind.SpeedChanged, u.Id, -1, u.Speed, $"{u.Name} speed {before} -> {u.Speed}");
            if (delta > 0) AddHeat(u, ThrottleHeat);
            return null;
        }

        /// <summary>Finish the Command step: Drift, Enemy action, End, then the next turn's Intent and draw.</summary>
        public string EndTurn()
        {
            if (Phase != BattlePhase.Command) return "not in the Command step";

            SetPhase(BattlePhase.Drift);
            DoDrift();
            if (Outcome != BattleOutcome.Ongoing) return null;

            SetPhase(BattlePhase.EnemyAction);
            ResolveIntents();
            if (Outcome != BattleOutcome.Ongoing) return null;

            SetPhase(BattlePhase.End);
            DoEndStep();
            if (Outcome != BattleOutcome.Ongoing) return null;

            if (TurnLimit > 0 && Turn >= TurnLimit)
            {
                Outcome = BattleOutcome.Withdrawal;
                Phase = BattlePhase.Over;
                Emit(EventKind.Withdrawal, -1, -1, Turn, $"Neither fleet can finish the other: Withdrawal after turn {Turn}");
                return null;
            }
            BeginTurn();
            return null;
        }

        // ================================================================ turn steps

        void BeginTurn()
        {
            Turn++;
            SetPhase(BattlePhase.Intent);
            Emit(EventKind.TurnStarted, -1, -1, Turn, $"Turn {Turn}");
            foreach (var u in Units)
            {
                u.NoBurnThisTurn = u.NoBurnNextTurn;
                u.NoBurnNextTurn = false;
                u.ThrottleUsed = false;
                u.CraftActed = false;
                u.CraftBonusMove = 0;
                u.CraftAttacks = 1;
            }
            PlanIntents();

            SetPhase(BattlePhase.Command);
            Command = CommandPerTurn;
            Emit(EventKind.Command, -1, -1, Command, $"{Command} command");
            DrawCards(HandSize - Deck.Hand.Count);
        }

        void DoDrift()
        {
            foreach (var u in Units.Where(u => u.IsCapital).ToList())
            {
                if (!u.Alive || u.Speed <= 0) continue;
                for (int step = 0; step < u.Speed && u.Alive; step++)
                {
                    var next = u.Pos.Neighbor(u.Facing);
                    if (!Board.InBounds(next))
                    {
                        Emit(EventKind.Moved, u.Id, -1, 0, $"{u.Name} holds at the edge of the field", u.Pos, u.Pos);
                        break;
                    }
                    int dmg = u.Speed * CollisionPerSpeed;
                    if (Board.Get(next) != Terrain.Open)
                    {
                        Emit(EventKind.Collision, u.Id, -1, dmg, $"{u.Name} hits {Board.Get(next)} for {dmg}", u.Pos, next);
                        DealDamage(u, dmg, next, new DamageFlags(), null);
                        break;
                    }
                    var other = UnitAt(next);
                    if (other != null)
                    {
                        Emit(EventKind.Collision, u.Id, other.Id, dmg, $"{u.Name} collides with {other.Name}: {dmg} each", u.Pos, next);
                        DealDamage(other, dmg, u.Pos, new DamageFlags(), u);
                        DealDamage(u, dmg, next, new DamageFlags(), other);
                        break;
                    }
                    Emit(EventKind.Moved, u.Id, -1, 1, $"{u.Name} drifts", u.Pos, next);
                    u.Pos = next;
                }
                if (Outcome != BattleOutcome.Ongoing) return;
            }
        }

        void DoEndStep()
        {
            foreach (var u in Units.Where(u => u.Alive && u.IsCapital))
            {
                for (int i = 0; i < ArcIndex.Count; i++)
                    u.Shields[i] = Math.Min(u.MaxShields, Math.Min(u.Shields[i], u.MaxShields) + u.ShieldRegen);
                Emit(EventKind.ShieldsRegen, u.Id, -1, u.ShieldRegen,
                    $"{u.Name} shields F{u.Shields[0]} S{u.Shields[1]} A{u.Shields[2]} P{u.Shields[3]}");
            }

            var hot = LiveUnits(Side.Player).Where(u => u.IsCapital && u.HeatCap > 0).ToList();
            foreach (var u in hot) AddHeat(u, -u.Vent);

            foreach (var u in hot)
            {
                if (!u.Alive) continue;
                if (u.Heat > u.HeatCap) AddJunk(FireOnDeck, $"{u.Name} is over its heat cap");
                if (u.Heat >= u.HeatCap + ReactorStrainMargin)
                {
                    Emit(EventKind.ReactorStrain, u.Id, -1, ReactorStrainDamage, $"{u.Name} suffers Reactor Strain");
                    DealDamage(u, ReactorStrainDamage, u.Pos, new DamageFlags { Direct = true }, null);
                    u.Heat = u.HeatCap;
                    if (Outcome != BattleOutcome.Ongoing) return;
                }
            }

            foreach (var c in Deck.Hand.ToList())
            {
                switch (c.Def.Junk)
                {
                    case JunkTrigger.EndOfTurnHeat:
                        var f = Flagship;
                        if (f != null) AddHeat(f, 2);
                        break;
                    case JunkTrigger.EndOfTurnLoseOrdinals:
                        Ordinals = Math.Max(0, Ordinals - 2);
                        Emit(EventKind.Ordinals, -1, -1, -2, $"{c.Def.Name}: lose 2 Ordinals ({Ordinals} left)");
                        break;
                }
            }

            foreach (var c in Deck.Hand) c.TempCost = 0;
            Deck.Discard.AddRange(Deck.Hand);
            Deck.Hand.Clear();
            Command = 0;
            foreach (var u in Units)
            {
                u.LanceBonus = 0;
                u.MarkBonus = 0;
                u.Guarding = false;
            }
        }

        // ================================================================ deck

        public void DrawCards(int n)
        {
            int jams = 0;
            for (int i = 0; i < n; i++)
            {
                if (Deck.DrawPile.Count == 0)
                {
                    if (Deck.Discard.Count == 0) break;
                    Deck.DrawPile.AddRange(Deck.Discard);
                    Deck.Discard.Clear();
                    Rng.Shuffle(Deck.DrawPile);
                    Emit(EventKind.Shuffled, -1, -1, Deck.DrawPile.Count, "Discard shuffled into the draw pile");
                }
                var c = Deck.DrawPile[0];
                Deck.DrawPile.RemoveAt(0);
                Deck.Hand.Add(c);
                Emit(EventKind.CardDrawn, c.SourceId, -1, c.Id, $"Draw {c.Def.Name}");
                if (c.Def.Junk == JunkTrigger.OnDrawJamWeapon) jams++;
            }

            for (int j = 0; j < jams; j++)
            {
                var weapons = Deck.Hand.Where(c => c.Def.Type == CardType.Weapon).ToList();
                if (weapons.Count == 0) break;
                var w = Rng.Pick(weapons);
                w.TempCost++;
                Emit(EventKind.Buff, w.SourceId, -1, 1, $"Jammed Mount: {w.Def.Name} costs +1 this turn");
            }
        }

        public void AddJunk(CardDef def, string reason)
        {
            var c = new CardInstance { Id = nextCardId++, Def = def, SourceId = -1 };
            Deck.Discard.Add(c);
            Emit(EventKind.CardAdded, -1, -1, c.Id, $"{reason}: {def.Name} added to the discard pile");
        }

        // ================================================================ units and damage

        public Unit Spawn(UnitSpec spec, Side side, HexCoord pos, HexDirection facing, int speed)
        {
            var u = Unit.From(spec, nextUnitId++, side, pos, facing, speed);
            Units.Add(u);
            var (col, row) = pos.ToOffset();
            Emit(EventKind.Spawned, u.Id, -1, 0, $"{u.Name} #{u.Id} appears at {col},{row}", pos, pos);
            return u;
        }

        public void AddHeat(Unit u, int delta)
        {
            if (delta == 0 || u.HeatCap <= 0) return;
            int before = u.Heat;
            u.Heat = Math.Max(0, u.Heat + delta);
            if (u.Heat != before) Emit(EventKind.Heat, u.Id, -1, u.Heat - before, $"{u.Name} heat {before} -> {u.Heat}");
        }

        public void TurnShip(Unit u, int steps)
        {
            var before = u.Facing;
            u.Facing = u.Facing.Rotate(steps);
            Emit(EventKind.Turned, u.Id, -1, steps, $"{u.Name} turns {before} -> {u.Facing}");
        }

        /// <summary>
        /// The arc of <paramref name="target"/> hit from <paramref name="from"/>. On a boundary the defender
        /// chooses: the arc with the most shields plus armor (first arc on a tie).
        /// </summary>
        public static int HitArc(Unit target, HexCoord from)
        {
            var arcs = Arcs.Of(target.Pos, target.Facing, from);
            if (arcs == Arc.None) return ArcIndex.Fore;
            int best = -1;
            for (int i = 0; i < ArcIndex.Count; i++)
            {
                if ((arcs & ArcIndex.ToArc(i)) == 0) continue;
                if (best < 0 || target.Shields[i] + target.Armor[i] > target.Shields[best] + target.Armor[best]) best = i;
            }
            return best;
        }

        /// <summary>What an attack would do right now, without doing it: (hit arc, shields absorbed, armor, hull damage).</summary>
        public (int arc, int shields, int armor, int hull) PreviewDamage(Unit target, int amount, HexCoord from, DamageFlags flags)
        {
            int remaining = amount, absorbed = 0, armored = 0, arc = -1;
            if (target.IsCapital && !flags.Direct)
            {
                arc = HitArc(target, from);
                if (!flags.IgnoreShields)
                {
                    absorbed = Math.Min(target.Shields[arc], remaining);
                    remaining -= absorbed;
                }
                if (!flags.IgnoreArmor)
                {
                    armored = Math.Min(target.Armor[arc], remaining);
                    remaining -= armored;
                }
            }
            return (arc, absorbed, armored, Math.Min(remaining, target.Hull));
        }

        /// <summary>
        /// Shields on the hit arc absorb first, that arc's armor reduces the rest once, the remainder hits hull.
        /// 5 or more hull damage is a critical hit. Returns the hull damage dealt.
        /// </summary>
        public int DealDamage(Unit target, int amount, HexCoord from, DamageFlags flags, Unit attacker)
        {
            if (target == null || !target.Alive || amount <= 0) return 0;

            int remaining = amount, absorbed = 0, armored = 0;
            string where = "";
            if (target.IsCapital && !flags.Direct)
            {
                int arc = HitArc(target, from);
                where = $" ({ArcIndex.ToArc(arc)})";
                if (!flags.IgnoreShields)
                {
                    absorbed = Math.Min(target.Shields[arc], remaining);
                    target.Shields[arc] -= absorbed;
                    remaining -= absorbed;
                }
                if (!flags.IgnoreArmor)
                {
                    armored = Math.Min(target.Armor[arc], remaining);
                    remaining -= armored;
                }
            }

            target.Hull = Math.Max(0, target.Hull - remaining);
            Emit(EventKind.Damage, target.Id, attacker?.Id ?? -1, remaining,
                $"{target.Name}{where} takes {remaining} hull damage of {amount} (shields {absorbed}, armor {armored}), hull {target.Hull}");

            if (target.Hull <= 0)
            {
                Destroy(target);
                return remaining;
            }

            bool crit = target.IsCapital && !flags.Direct && remaining > 0 &&
                        (remaining >= CritThreshold || flags.CritOnAnyHull);
            if (crit) Critical(target);
            return remaining;
        }

        /// <summary>Paper-prototype crit table, d6: 1-2 Jammed Mount, 3-4 Fire on Deck, 5 Hull Breach, 6 engine hit.</summary>
        void Critical(Unit u)
        {
            int roll = Rng.Range(1, 7);
            bool player = u.Side == Side.Player;
            if (roll == 6)
            {
                u.Speed = Math.Max(0, u.Speed - 1);
                u.NoBurnNextTurn = true;
                Emit(EventKind.Critical, u.Id, -1, roll, $"Critical on {u.Name} (6): engine hit, speed {u.Speed}, no Burn next turn");
                return;
            }
            if (!player)
            {
                Emit(EventKind.Critical, u.Id, -1, roll, $"Critical on {u.Name} ({roll}): no extra effect");
                return;
            }
            var junk = roll <= 2 ? JammedMount : roll <= 4 ? FireOnDeck : HullBreach;
            Emit(EventKind.Critical, u.Id, -1, roll, $"Critical on {u.Name} ({roll})");
            AddJunk(junk, $"Critical on {u.Name}");
        }

        /// <summary>Remove a unit. A destroyed ship's cards leave the draw pile, hand, discard and exhaust.</summary>
        public void Destroy(Unit u)
        {
            if (!u.Alive) return;
            u.Alive = false;
            u.Hull = 0;
            Emit(EventKind.Destroyed, u.Id, -1, 0, $"{u.Name} #{u.Id} is destroyed");

            int removed = 0;
            foreach (var pile in Deck.AllPiles()) removed += pile.RemoveAll(c => c.SourceId == u.Id);
            if (removed > 0) Emit(EventKind.CardsRemoved, u.Id, -1, removed, $"{removed} cards from {u.Name} leave the deck");

            CheckOutcome();
        }

        void CheckOutcome()
        {
            if (Outcome != BattleOutcome.Ongoing) return;
            if (!LiveUnits(Side.Player).Any(u => u.IsCapital)) Outcome = BattleOutcome.Defeat;
            else if (!LiveUnits(Side.Enemy).Any(u => u.IsCapital)) Outcome = BattleOutcome.Victory;
            else return;

            Phase = BattlePhase.Over;
            Emit(Outcome == BattleOutcome.Victory ? EventKind.Victory : EventKind.Defeat, -1, -1, Turn, $"{Outcome} on turn {Turn}");
        }

        // ================================================================ events and printing

        void SetPhase(BattlePhase p)
        {
            Phase = p;
            Emit(EventKind.PhaseChanged, -1, -1, 0, p.ToString());
        }

        public void Emit(EventKind kind, int unitId, int otherId, int amount, string text,
            HexCoord from = default, HexCoord to = default)
        {
            Log.Add(new BattleEvent
            {
                Turn = Turn, Phase = Phase, Kind = kind, UnitId = unitId, OtherId = otherId, Amount = amount,
                From = from, To = to, Text = text,
            });
        }

        /// <summary>A text picture of the battle: board, units, intents and piles.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Turn {Turn} | {Phase} | command {Command} | Ordinals {Ordinals} | {Outcome}");
            for (int row = 0; row < Board.Height; row++)
            {
                if ((row & 1) == 1) sb.Append(' ');
                for (int col = 0; col < Board.Width; col++)
                {
                    var h = HexCoord.FromOffset(col, row);
                    var u = UnitAt(h);
                    char ch = u != null ? Glyph(u) : Board.Get(h) == Terrain.Asteroid ? '#' : Board.Get(h) == Terrain.Debris ? '%' : '.';
                    sb.Append(ch).Append(' ');
                }
                sb.AppendLine();
            }
            foreach (var u in Units.Where(u => u.Alive)) sb.AppendLine(u.ToString());
            foreach (var i in Intents) sb.AppendLine("Intent: " + i.Describe(this));
            sb.AppendLine($"Hand: {string.Join(", ", Deck.Hand)}");
            sb.AppendLine($"Draw {Deck.DrawPile.Count} | Discard {Deck.Discard.Count} | Exhausted {Deck.Exhausted.Count}");
            return sb.ToString();
        }

        static char Glyph(Unit u)
        {
            if (u.Side == Side.Player) return u.IsFlagship ? 'F' : u.IsCapital ? 'P' : 'd';
            return u.IsCapital ? 'E' : u.Kind == UnitKind.Torpedo ? 't' : 'g';
        }
    }
}
