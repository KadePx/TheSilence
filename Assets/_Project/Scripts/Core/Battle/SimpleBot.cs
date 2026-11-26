using System.Linq;
using Silence.Core.Hex;

namespace Silence.Core.Battle
{
    /// <summary>
    /// A greedy stand-in player for headless battles and balance sims. It is not smart: it turns toward the
    /// nearest enemy ship, fires whatever can hit, keeps heat in check and sends darts at the closest enemy.
    /// </summary>
    public static class SimpleBot
    {
        /// <summary>Play one full Command step, then end the turn.</summary>
        public static void PlayTurn(Battle b)
        {
            if (b.Phase != BattlePhase.Command) return;

            bool progress = true;
            int guard = 0;
            while (progress && guard++ < 50 && b.Phase == BattlePhase.Command)
            {
                progress = false;
                foreach (var card in b.Deck.Hand.OrderBy(Priority).ThenBy(c => c.Id).ToList())
                {
                    if (TryPlay(b, card))
                    {
                        progress = true;
                        break;
                    }
                }
            }

            if (b.Phase != BattlePhase.Command) return;
            MoveCraft(b);
            if (b.Phase != BattlePhase.Command) return;
            SetThrottles(b);
            b.EndTurn();
        }

        /// <summary>
        /// The helm, last thing each turn: stop rather than drift into something, hold position once an enemy
        /// ship is in gun range, and otherwise close at Speed 1 (2 when the enemy is far off).
        /// </summary>
        static void SetThrottles(Battle b)
        {
            foreach (var u in b.LiveUnits(Side.Player).Where(u => u.IsCapital).ToList())
            {
                var enemy = b.LiveUnits(Side.Enemy).Where(e => e.IsCapital).OrderBy(e => e.Pos.DistanceTo(u.Pos)).ThenBy(e => e.Id).FirstOrDefault();
                int d = enemy == null ? 99 : enemy.Pos.DistanceTo(u.Pos);
                // Hold only where a gun actually bears: the fore arc out to lance range, or a flank inside broadside range.
                int foreRange = u.IsFlagship ? 4 : 3;
                bool bears = b.LiveUnits(Side.Enemy).Any(e => e.IsCapital &&
                    ((Arcs.InAny(u.Pos, u.Facing, e.Pos, Arc.Fore) && e.Pos.DistanceTo(u.Pos) <= foreRange) ||
                     (u.IsFlagship && Arcs.InAny(u.Pos, u.Facing, e.Pos, Arc.Port | Arc.Starboard) && e.Pos.DistanceTo(u.Pos) <= 3)));
                // Close to knife range before holding, and stop holding in a fight that is dragging on:
                // a ship that sits at long range only ever fires the lance.
                bool hold = bears && d <= 2 && b.Turn <= 8 && Arcs.InAny(u.Pos, u.Facing, enemy.Pos, Arc.Fore);
                int want = hold ? 0 : d > 6 ? 2 : 1;
                // Never drift into rock, wreckage or another unit.
                var h = u.Pos;
                for (int step = 1; step <= want; step++)
                {
                    h = h.Neighbor(u.Facing);
                    if (!b.Board.InBounds(h)) break;
                    if (!b.IsFree(h)) { want = step - 1; break; }
                }
                want = System.Math.Min(want, u.MaxSpeed);
                if (want > u.Speed && u.Heat < u.HeatCap - 1) b.Throttle(u.Id, 1);
                else if (want < u.Speed) b.Throttle(u.Id, -1);
            }
        }

        static int Priority(CardInstance c)
        {
            switch (c.Def.Id)
            {
                case "thermal_purge": return 0;
                case "vent_plasma": case "cold_discipline": return 0;
                case "redline": return 1;
                case "decimation_order": case "waymind_targeting": case "precision_edict": case "overcharge_lances": return 1;
                case "hard_about": case "come_about": return 2;
                case "scry_the_void": return 1;
                case "lance_volley": return 3;
                case "launch_darts": case "dart_sortie": return 5;
                case "fire_on_deck": return 6;
                default: return 4;
            }
        }

        static bool TryPlay(Battle b, CardInstance card)
        {
            var def = card.Def;
            var source = card.SourceId >= 0 ? b.Get(card.SourceId) : null;
            var enemies = b.LiveUnits(Side.Enemy).OrderBy(e => e.IsCapital ? 0 : 1).ThenBy(e => e.Hull).ThenBy(e => e.Id).ToList();
            var flag = b.Flagship ?? b.LiveUnits(Side.Player).FirstOrDefault(u => u.IsCapital);
            if (flag == null) return false;

            switch (def.Id)
            {
                case "vent_plasma":
                    var hot = b.LiveUnits(Side.Player).Where(u => u.IsCapital && u.Heat > u.HeatCap - 3).OrderByDescending(u => u.Heat).FirstOrDefault();
                    return hot != null && b.PlayCard(card.Id, PlayTarget.At(hot.Id)) == null;
                case "cold_discipline":
                    return b.LiveUnits(Side.Player).Any(u => u.IsCapital && u.Heat > u.HeatCap - 3) && b.PlayCard(card.Id, PlayTarget.None) == null;
                case "brace":
                    var threatened = b.Intents.Where(i => !i.Cancelled).Select(i => b.Get(i.TargetId)).FirstOrDefault(u => u != null && u.Side == Side.Player);
                    return threatened != null && b.PlayCard(card.Id, PlayTarget.At(threatened.Id)) == null;
                case "burn":
                {
                    // Close one hex when the nearest enemy ship is just outside the lance's reach.
                    var far = b.LiveUnits(Side.Enemy).Where(e => e.IsCapital).OrderBy(e => e.Pos.DistanceTo(flag.Pos)).FirstOrDefault();
                    return far != null && far.Pos.DistanceTo(flag.Pos) == 5 && Arcs.InAny(flag.Pos, flag.Facing, far.Pos, Arc.Fore)
                           && b.PlayCard(card.Id, PlayTarget.At(flag.Id)) == null;
                }
                case "redline":
                    // One more command, if the reactor has room and there is a card that the extra command buys.
                    return source != null && source.Heat + 4 <= source.HeatCap
                           && b.Deck.Hand.Any(c => c != card && !c.Def.Unplayable && c.Def.Type == CardType.Weapon && b.CostOf(c) == b.Command + 1)
                           && b.PlayCard(card.Id, PlayTarget.None) == null;
                case "thermal_purge":
                {
                    if (source == null || source.Heat < 5) return false;
                    foreach (var e in enemies)
                        if (b.PlayCard(card.Id, PlayTarget.At(e.Id)) == null) return true;
                    return false;
                }
                case "hard_about":
                    return TryTurnToward(b, card, flag, false);
                case "come_about":
                    return source != null && TryTurnToward(b, card, source, true);
                case "hold_the_line":
                    return b.Intents.Any(i => !i.Cancelled && i.Damage > 0) && b.PlayCard(card.Id, PlayTarget.None) == null;
                case "seal_the_decks":
                    var hurt = b.LiveUnits(Side.Player).Where(u => u.IsCapital && u.MaxHull - u.Hull >= 5).OrderBy(u => u.Hull).FirstOrDefault();
                    return hurt != null && b.Ordinals > 8 && b.PlayCard(card.Id, PlayTarget.At(hurt.Id)) == null;
                case "overcharge_lances":
                case "waymind_targeting":
                    return HasPlayableWeapon(b, card) && b.PlayCard(card.Id, PlayTarget.None) == null;
                case "precision_edict":
                    var aimed = enemies.FirstOrDefault(e => e.IsCapital && CanHit(b, e));
                    return aimed != null && HasPlayableWeapon(b, card) && b.PlayCard(card.Id, PlayTarget.At(aimed.Id)) == null;
                case "screening_maneuver":
                    return b.PlayCard(card.Id, PlayTarget.Turning(-1, 1)) == null;
                case "fire_on_deck":
                    return b.Command >= b.CostOf(card) && b.PlayCard(card.Id, PlayTarget.None) == null;
            }

            switch (def.Target)
            {
                case TargetKind.Enemy:
                    foreach (var e in enemies)
                    {
                        // Spend crew freely early; in a fight that drags on, keep spending down to a small reserve to break it open.
                        bool expend = def.ExpendOptional && (b.Ordinals > 12 || (b.Turn > 8 && e.IsCapital && b.Ordinals >= 6));
                        if (b.PlayCard(card.Id, PlayTarget.At(e.Id, expend)) == null) return true;
                        if (expend && b.PlayCard(card.Id, PlayTarget.At(e.Id)) == null) return true;
                    }
                    return false;
                case TargetKind.AllyCapital:
                    return b.PlayCard(card.Id, PlayTarget.At(flag.Id)) == null;
                default:
                    return source != null && b.PlayCard(card.Id, PlayTarget.None) == null;
            }
        }

        /// <summary>
        /// Is there a weapon in hand that could really be fired after paying for <paramref name="except"/>: affordable,
        /// and with some enemy inside its arc and range right now? (A buff with nothing to buff is a wasted command.)
        /// </summary>
        static bool HasPlayableWeapon(Battle b, CardInstance except)
        {
            bool lanceOnly = except.Def.Id == "overcharge_lances";
            foreach (var c in b.Deck.Hand)
            {
                if (c == except || c.Def.Type != CardType.Weapon) continue;
                if (b.CostOf(c) + b.CostOf(except) > b.Command) continue;
                if (lanceOnly && !c.Def.Effects.OfType<AttackEffect>().Any(a => a.IsLance)) continue;
                if (b.LiveUnits(Side.Enemy).Any(e => b.CheckPlay(c.Id, PlayTarget.At(e.Id)) == null)) return true;
            }
            return false;
        }

        static bool CanHit(Battle b, Unit e)
        {
            foreach (var c in b.Deck.Hand.Where(c => c.Def.Type == CardType.Weapon))
            {
                var s = b.Get(c.SourceId);
                var atk = c.Def.Effects.OfType<AttackEffect>().FirstOrDefault();
                if (s == null || atk == null) continue;
                if (Arcs.InAny(s.Pos, s.Facing, e.Pos, atk.Arcs) && s.Pos.DistanceTo(e.Pos) <= atk.Range) return true;
            }
            return false;
        }

        /// <summary>Turn the flagship so the nearest enemy ship ends up closer to its fore arc.</summary>
        static bool TryTurnToward(Battle b, CardInstance card, Unit ship, bool onSource)
        {
            var target = b.LiveUnits(Side.Enemy).Where(e => e.IsCapital).OrderBy(e => e.Pos.DistanceTo(ship.Pos)).ThenBy(e => e.Id).FirstOrDefault();
            if (target == null || Arcs.InAny(ship.Pos, ship.Facing, target.Pos, Arc.Fore)) return false;

            int[] options = ship.Speed <= 1 ? new[] { 1, -1, 2, -2 } : new[] { 1, -1 };
            int best = 0, bestScore = Score(ship.Facing);
            foreach (int s in options)
            {
                int score = Score(ship.Facing.Rotate(s));
                if (score < bestScore) { bestScore = score; best = s; }
            }
            return best != 0 && b.PlayCard(card.Id, PlayTarget.Turning(onSource ? -1 : ship.Id, best)) == null;

            int Score(HexDirection f)
            {
                var arc = Arcs.Of(ship.Pos, f, target.Pos);
                if ((arc & Arc.Fore) != 0) return 0;
                if ((arc & Arc.Aft) != 0) return 3;
                return 1 + (b.Board.BlocksMovement(ship.Pos.Neighbor(f)) ? 1 : 0);
            }
        }

        static void MoveCraft(Battle b)
        {
            foreach (var d in b.LiveUnits(Side.Player).Where(u => u.Kind == UnitKind.Craft && !u.CraftActed).ToList())
            {
                var reach = b.Reachable(d.Pos, d.Move + d.CraftBonusMove);
                var targets = b.LiveUnits(Side.Enemy)
                    .OrderBy(e => e.Kind == UnitKind.Torpedo ? 0 : e.IsCapital ? 2 : 1)
                    .ThenBy(e => e.Pos.DistanceTo(d.Pos)).ThenBy(e => e.Id).ToList();
                bool acted = false;
                foreach (var t in targets)
                {
                    var spot = reach.Where(h => h.DistanceTo(t.Pos) == 1).Cast<HexCoord?>().FirstOrDefault();
                    if (spot == null) continue;
                    if (b.CommandCraft(d.Id, spot.Value, t.Id) == null) { acted = true; break; }
                }
                if (acted || targets.Count == 0) continue;
                var nearest = targets.OrderBy(t => t.Pos.DistanceTo(d.Pos)).First();
                var closer = reach.OrderBy(h => h.DistanceTo(nearest.Pos)).First();
                b.CommandCraft(d.Id, closer);
            }
        }
    }
}
