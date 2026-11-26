using System;
using System.Collections.Generic;
using System.Linq;
using Silence.Core.Hex;

namespace Silence.Core.Battle
{
    public enum IntentKind { Broadside, Maneuver, LaunchTorpedo, TorpedoRun, CraftAttack, LaunchCraft, PatchUp, JointFire }

    /// <summary>An enemy's announced action for this turn, shown on the board before the player acts.</summary>
    public sealed class Intent
    {
        public int UnitId;
        public IntentKind Kind;
        public int TargetId = -1;
        public int Damage;
        public int Range;
        public Arc Arcs;
        /// <summary>Extra damage from a carrier's Joint Fire.</summary>
        public int Bonus;
        public bool Cancelled;
        /// <summary>Name of the attack ("Broadside", "Mass Driver").</summary>
        public string Label;

        public string Describe(Battle b)
        {
            var u = b.Get(UnitId);
            var t = b.Get(TargetId);
            string who = $"{u?.Name} #{UnitId}";
            string target = t != null ? $"{t.Name} #{t.Id}" : "nobody";
            string text;
            switch (Kind)
            {
                case IntentKind.Broadside: text = $"{Label ?? "Broadside"} {Damage + Bonus} at {target} ({Arcs}, range {Range})"; break;
                case IntentKind.Maneuver: text = $"Maneuver toward {target}"; break;
                case IntentKind.LaunchTorpedo: text = $"Launch torpedo at {target}"; break;
                case IntentKind.TorpedoRun: text = $"Torpedo run at {target}, {Damage + Bonus} on impact"; break;
                case IntentKind.CraftAttack: text = $"Attack {target}'s aft for {Damage + Bonus}"; break;
                case IntentKind.LaunchCraft: text = $"Launch {Damage} gunships"; break;
                case IntentKind.PatchUp: text = $"Patch Up {target}: repair {Damage}, +{EnemyRules.PatchUpShields} shields"; break;
                case IntentKind.JointFire: text = $"Joint Fire: every attack at {target}, +2"; break;
                default: text = Kind.ToString(); break;
            }
            return $"{who}: {text}" + (Cancelled ? " (cancelled)" : "");
        }
    }

    /// <summary>Enemy numbers shared by every cycle (Docs/PaperPrototype.md).</summary>
    public static class EnemyRules
    {
        public const int ManeuverMove = 2;
        public const int PatchUpShields = 3, JointFireBonus = 2;
    }

    public sealed partial class Battle
    {
        // ================================================================ intent step

        void PlanIntents()
        {
            Intents.Clear();
            foreach (var u in Units.Where(u => u.Alive && u.Side == Side.Enemy).ToList())
            {
                var i = PlanFor(u);
                if (i != null) Intents.Add(i);
            }

            var joint = Intents.FirstOrDefault(i => i.Kind == IntentKind.JointFire && i.TargetId >= 0);
            if (joint != null)
            {
                foreach (var i in Intents)
                {
                    if (i.Kind != IntentKind.Broadside && i.Kind != IntentKind.CraftAttack && i.Kind != IntentKind.LaunchTorpedo) continue;
                    i.TargetId = joint.TargetId;
                    i.Bonus += EnemyRules.JointFireBonus;
                }
            }

            foreach (var i in Intents) Emit(EventKind.IntentDeclared, i.UnitId, i.TargetId, i.Damage + i.Bonus, i.Describe(this));
        }

        Intent PlanFor(Unit u)
        {
            var nearest = NearestPlayerCapital(u.Pos);
            switch (u.Ai)
            {
                case AiProfile.Gunship:
                    if (nearest == null) return null;
                    return new Intent { UnitId = u.Id, Kind = IntentKind.CraftAttack, TargetId = nearest.Id, Damage = u.AttackDamage };
                case AiProfile.Torpedo:
                {
                    var t = Get(u.TargetId);
                    if (t == null || !t.Alive) t = nearest;
                    if (t == null) return null;
                    u.TargetId = t.Id;
                    return new Intent { UnitId = u.Id, Kind = IntentKind.TorpedoRun, TargetId = t.Id, Damage = u.AttackDamage };
                }
                case AiProfile.Frigate:
                case AiProfile.Carrier:
                {
                    if (u.Cycle.Length == 0) return null;
                    var step = u.Cycle[u.CycleIndex % u.Cycle.Length];
                    var i = new Intent { UnitId = u.Id, Kind = step.Kind, Label = step.Label, Damage = step.Amount, Range = step.Range, Arcs = step.Arcs };
                    switch (step.Kind)
                    {
                        case IntentKind.PatchUp:
                            i.TargetId = LiveUnits(Side.Enemy).Where(e => e.IsCapital)
                                .OrderByDescending(e => e.MaxHull - e.Hull).ThenBy(e => e.Id).First().Id;
                            break;
                        case IntentKind.JointFire:
                        {
                            var weakest = LiveUnits(Side.Player).Where(p => p.IsCapital).OrderBy(p => p.Hull).ThenBy(p => p.Id).FirstOrDefault();
                            if (weakest == null) return null;
                            i.TargetId = weakest.Id;
                            break;
                        }
                        case IntentKind.LaunchCraft:
                            break;
                        case IntentKind.LaunchTorpedo:
                            if (nearest == null) return null;
                            i.TargetId = nearest.Id;
                            i.Damage = TorpedoSpec.AttackDamage;
                            break;
                        default:
                            if (nearest == null) return null;
                            i.TargetId = nearest.Id;
                            break;
                    }
                    return i;
                }
                default:
                    return null;
            }
        }

        /// <summary>The player capital ship an enemy at this hex goes for: the nearest, but the flagship wins when it is at most one hex further.</summary>
        public Unit NearestPlayerCapital(HexCoord from)
            => LiveUnits(Side.Player).Where(p => p.IsCapital)
                .OrderBy(p => p.Pos.DistanceTo(from) - (p.IsFlagship ? 1 : 0)).ThenBy(p => p.IsFlagship ? 0 : 1).ThenBy(p => p.Id).FirstOrDefault();

        /// <summary>Boarding Action and similar: the unit skips its intent this turn.</summary>
        public void CancelIntent(int unitId)
        {
            foreach (var i in Intents.Where(i => i.UnitId == unitId && !i.Cancelled))
            {
                i.Cancelled = true;
                if (i.Kind == IntentKind.JointFire)
                    foreach (var other in Intents) other.Bonus = 0;
                Emit(EventKind.IntentCancelled, unitId, -1, 0, i.Describe(this));
            }
        }

        // ================================================================ enemy action step

        void ResolveIntents()
        {
            foreach (var i in Intents.ToList())
            {
                var u = Get(i.UnitId);
                if (u == null || !u.Alive) continue;
                u.CycleIndex++;
                if (i.Cancelled) continue;

                switch (i.Kind)
                {
                    case IntentKind.Broadside: ResolveBroadside(u, i); break;
                    case IntentKind.Maneuver: ResolveManeuver(u); break;
                    case IntentKind.LaunchTorpedo: ResolveLaunchTorpedo(u, i); break;
                    case IntentKind.TorpedoRun: ResolveTorpedo(u, i); break;
                    case IntentKind.CraftAttack: ResolveCraftAttack(u, i); break;
                    case IntentKind.LaunchCraft: ResolveLaunchCraft(u, i); break;
                    case IntentKind.PatchUp: ResolvePatchUp(u, i); break;
                    case IntentKind.JointFire:
                        Emit(EventKind.Buff, u.Id, i.TargetId, EnemyRules.JointFireBonus, $"{u.Name} coordinates Joint Fire");
                        break;
                }
                if (Outcome != BattleOutcome.Ongoing) return;
                if (u.Alive && u.IsCapital && i.Kind != IntentKind.Maneuver) Trim(u);
            }
        }

        /// <summary>A guarding escort next to the target takes the attack instead (Screening Maneuver).</summary>
        Unit Screen(Unit target)
        {
            var guard = LiveUnits(Side.Player).FirstOrDefault(g => g.Guarding && g != target && g.Pos.DistanceTo(target.Pos) == 1);
            if (guard == null) return target;
            guard.Guarding = false;
            Emit(EventKind.Buff, guard.Id, target.Id, 0, $"{guard.Name} screens {target.Name}");
            return guard;
        }

        void ResolveBroadside(Unit u, Intent i)
        {
            var t = Get(i.TargetId);
            string miss = null;
            if (t == null || !t.Alive) miss = "target is gone";
            else if (!Arcs.InAny(u.Pos, u.Facing, t.Pos, i.Arcs)) miss = "target left the arc";
            else if (u.Pos.DistanceTo(t.Pos) > i.Range) miss = "target out of range";
            else if (!Board.HasLineOfFire(u.Pos, t.Pos)) miss = "line of fire blocked";
            if (miss != null)
            {
                Emit(EventKind.Miss, u.Id, i.TargetId, 0, $"{u.Name} {i.Label ?? "Broadside"} misses: {miss}");
                return;
            }
            var victim = Screen(t);
            Emit(EventKind.Attack, u.Id, victim.Id, i.Damage + i.Bonus, $"{u.Name} fires {i.Label ?? "Broadside"} at {victim.Name}");
            DealDamage(victim, i.Damage + i.Bonus, u.Pos, new DamageFlags(), u);
        }

        void ResolveManeuver(Unit u)
        {
            var (facing, path, speed) = ChooseManeuver(u);
            if (facing != u.Facing) TurnShip(u, TurnSteps(u.Facing, facing));
            foreach (var h in path)
            {
                Emit(EventKind.Moved, u.Id, -1, 1, $"{u.Name} maneuvers", u.Pos, h);
                u.Pos = h;
            }
            if (speed != u.Speed)
            {
                Emit(EventKind.SpeedChanged, u.Id, -1, speed, $"{u.Name} speed {u.Speed} -> {speed}");
                u.Speed = speed;
            }
        }

        /// <summary>
        /// After acting, an enemy capital ship trims its course: up to one 60 degree turn and one step of
        /// speed, whichever keeps its guns on the nearest player ship after the next drift.
        /// </summary>
        void Trim(Unit u)
        {
            if (!u.Cycle.Any(s => s.Kind == IntentKind.Broadside)) return;
            int bestScore = int.MinValue, bestTurn = 0, bestSpeed = u.Speed;
            int[] turns = { 0, 1, -1 };
            int[] speeds = { 0, -1, 1 };
            foreach (int k in turns)
            foreach (int ds in speeds)
            {
                int speed = u.Speed + ds;
                if (speed < 0 || speed > u.MaxSpeed) continue;
                int score = ScoreFiringPosition(u, u.Pos, u.Facing.Rotate(k), speed) - Math.Abs(k) - Math.Abs(ds);
                if (score > bestScore) { bestScore = score; bestTurn = k; bestSpeed = speed; }
            }
            if (bestTurn != 0) TurnShip(u, bestTurn);
            if (bestSpeed != u.Speed)
            {
                Emit(EventKind.SpeedChanged, u.Id, -1, bestSpeed, $"{u.Name} speed {u.Speed} -> {bestSpeed}");
                u.Speed = bestSpeed;
            }
        }

        static int TurnSteps(HexDirection from, HexDirection to)
        {
            int diff = ((int)to - (int)from + HexDirections.Count) % HexDirections.Count;
            return diff > 3 ? diff - HexDirections.Count : diff;
        }

        /// <summary>
        /// Enemy Maneuver: turn to any facing, move up to 2 straight ahead and set a new speed, picking the
        /// combination that leaves its guns bearing on the nearest player ship after next turn's drift
        /// (both its own and the target's, as currently predicted). The player dodges by changing course.
        /// </summary>
        public (HexDirection facing, List<HexCoord> path, int speed) ChooseManeuver(Unit u)
        {
            int bestScore = int.MinValue;
            var bestFacing = u.Facing;
            var bestPath = new List<HexCoord>();
            int bestSpeed = u.Speed;
            int[] order = { 0, 1, -1, 2, -2, 3 };
            foreach (int k in order)
            {
                var f = u.Facing.Rotate(k);
                var path = new List<HexCoord>();
                var pos = u.Pos;
                for (int steps = 0; steps <= EnemyRules.ManeuverMove; steps++)
                {
                    if (steps > 0)
                    {
                        var next = pos.Neighbor(f);
                        if (!IsFree(next)) break;
                        pos = next;
                        path.Add(next);
                    }
                    for (int speed = 0; speed <= u.MaxSpeed; speed++)
                    {
                        int score = ScoreFiringPosition(u, pos, f, speed) - Math.Abs(k);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestFacing = f;
                            bestPath = new List<HexCoord>(path);
                            bestSpeed = speed;
                        }
                    }
                }
            }
            return (bestFacing, bestPath, bestSpeed);
        }

        int ScoreFiringPosition(Unit u, HexCoord pos, HexDirection f, int speed)
        {
            var end = pos;
            int score = 0;
            for (int s = 0; s < speed; s++)
            {
                var next = end.Neighbor(f);
                var occ = UnitAt(next);
                if (Board.BlocksMovement(next) || (occ != null && occ != u)) { score -= 40; break; }
                end = next;
            }
            var t = NearestPlayerCapital(end);
            if (t == null) return score;
            var tp = PredictDrift(t);
            int d = end.DistanceTo(tp);
            if (d == 0) return int.MinValue / 2;
            var gun = u.Cycle.FirstOrDefault(s => s.Kind == IntentKind.Broadside);
            var arcs = gun != null ? gun.Arcs : Arc.Port | Arc.Starboard;
            int range = gun != null ? gun.Range : 3;
            score -= 3 * Math.Abs(d - Math.Min(2, range));
            if (d <= range && Arcs.InAny(end, f, tp, arcs) && Board.HasLineOfFire(end, tp)) score += 100;
            return score;
        }

        /// <summary>
        /// Whether an attack intent would land if every ship simply drifted as predicted from here.
        /// For the board's intent lines; the real check happens when the intent resolves.
        /// </summary>
        public bool ForecastHit(Intent i)
        {
            var u = Get(i.UnitId);
            var t = Get(i.TargetId);
            if (u == null || t == null || !u.Alive || !t.Alive || i.Cancelled) return false;
            if (i.Kind != IntentKind.Broadside) return i.Kind == IntentKind.CraftAttack || i.Kind == IntentKind.TorpedoRun;
            var from = u.IsCapital ? PredictDrift(u) : u.Pos;
            var to = t.IsCapital ? PredictDrift(t) : t.Pos;
            return Arcs.InAny(from, u.Facing, to, i.Arcs) && from.DistanceTo(to) <= i.Range && Board.HasLineOfFire(from, to);
        }

        void ResolveLaunchTorpedo(Unit u, Intent i)
        {
            var t = Get(i.TargetId);
            if (t == null || !t.Alive) t = NearestPlayerCapital(u.Pos);
            var spot = t == null ? (HexCoord?)null : FreeNeighbors(u.Pos).OrderBy(h => h.DistanceTo(t.Pos)).Cast<HexCoord?>().FirstOrDefault();
            if (spot == null)
            {
                Emit(EventKind.Miss, u.Id, i.TargetId, 0, $"{u.Name} can't launch: no room");
                return;
            }
            var torp = Spawn(TorpedoSpec, Side.Enemy, spot.Value, u.Facing, 0);
            torp.TargetId = t.Id;
            torp.AttackDamage += i.Bonus;
        }

        /// <summary>A torpedo moves up to 3 hexes toward its target and hits on entering its hex.</summary>
        void ResolveTorpedo(Unit u, Intent i)
        {
            var t = Get(u.TargetId);
            for (int step = 0; step < u.Move && t != null && t.Alive; step++)
            {
                HexCoord? best = null;
                int bestD = u.Pos.DistanceTo(t.Pos);
                for (int d = 0; d < HexDirections.Count; d++)
                {
                    var n = u.Pos.Neighbor((HexDirection)d);
                    if (Board.BlocksMovement(n)) continue;
                    var occ = UnitAt(n);
                    if (occ != null && occ != t) continue;
                    int dist = n.DistanceTo(t.Pos);
                    if (dist < bestD)
                    {
                        bestD = dist;
                        best = n;
                    }
                }
                if (best == null) break;
                if (best.Value == t.Pos)
                {
                    var victim = Screen(t);
                    Emit(EventKind.Attack, u.Id, victim.Id, u.AttackDamage, $"Torpedo #{u.Id} hits {victim.Name}");
                    var from = u.Pos;
                    Destroy(u);
                    DealDamage(victim, u.AttackDamage, from, new DamageFlags(), u);
                    return;
                }
                Emit(EventKind.Moved, u.Id, -1, 1, $"Torpedo #{u.Id} closes in", u.Pos, best.Value);
                u.Pos = best.Value;
            }
        }

        /// <summary>Gunships move toward the target's aft, then hit an adjacent capital ship.</summary>
        void ResolveCraftAttack(Unit u, Intent i)
        {
            var t = Get(i.TargetId);
            if (t == null || !t.Alive) t = NearestPlayerCapital(u.Pos);
            if (t == null) return;

            var aft = t.Pos.Neighbor(t.Facing.Opposite());
            var dest = Reachable(u.Pos, u.Move)
                .OrderBy(h => h.DistanceTo(aft))
                .ThenBy(h => h.DistanceTo(t.Pos))
                .First();
            if (dest != u.Pos)
            {
                Emit(EventKind.Moved, u.Id, -1, u.Pos.DistanceTo(dest), $"{u.Name} #{u.Id} swoops in", u.Pos, dest);
                u.Pos = dest;
            }

            var victim = u.Pos.DistanceTo(t.Pos) == 1 ? t
                : LiveUnits(Side.Player).FirstOrDefault(p => p.IsCapital && p.Pos.DistanceTo(u.Pos) == 1);
            if (victim == null)
            {
                Emit(EventKind.Miss, u.Id, t.Id, 0, $"{u.Name} #{u.Id} can't reach a target");
                return;
            }
            victim = Screen(victim);
            Emit(EventKind.Attack, u.Id, victim.Id, i.Damage + i.Bonus, $"{u.Name} #{u.Id} strafes {victim.Name}");
            DealDamage(victim, i.Damage + i.Bonus, u.Pos, new DamageFlags(), u);
        }

        void ResolveLaunchCraft(Unit u, Intent i)
        {
            var near = NearestPlayerCapital(u.Pos);
            var spots = FreeNeighbors(u.Pos)
                .OrderBy(h => near == null ? 0 : h.DistanceTo(near.Pos))
                .Take(i.Damage).ToList();
            if (spots.Count == 0) Emit(EventKind.Miss, u.Id, -1, 0, $"{u.Name} can't launch: no room");
            foreach (var h in spots) Spawn(GunshipSpec, Side.Enemy, h, u.Facing, 0);
        }

        void ResolvePatchUp(Unit u, Intent i)
        {
            var t = Get(i.TargetId);
            if (t == null || !t.Alive) return;
            int before = t.Hull;
            t.Hull = Math.Min(t.MaxHull, t.Hull + i.Damage);
            for (int a = 0; a < ArcIndex.Count; a++) t.Shields[a] += EnemyRules.PatchUpShields;
            Emit(EventKind.Repaired, t.Id, u.Id, t.Hull - before,
                $"{u.Name} patches up {t.Name}: hull {before} -> {t.Hull}, +{EnemyRules.PatchUpShields} shields");
        }
    }
}
