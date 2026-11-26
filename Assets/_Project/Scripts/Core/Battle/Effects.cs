using System.Linq;
using Silence.Core.Hex;

namespace Silence.Core.Battle
{
    /// <summary>
    /// A composable effect block. A card is a list of these; <see cref="Validate"/> runs for every block
    /// before anything is paid or applied, so a card either resolves fully or not at all.
    /// </summary>
    public abstract class Effect
    {
        /// <summary>Null when the play is legal, otherwise the reason it is not.</summary>
        public virtual string Validate(Battle b, PlayContext ctx) => null;

        public abstract void Apply(Battle b, PlayContext ctx);
    }

    /// <summary>Weapon attack from the source ship on the chosen enemy, within arcs, range and line of fire.</summary>
    public sealed class AttackEffect : Effect
    {
        public int Damage;
        public Arc Arcs;
        public int Range;
        public int Heat;
        public bool IsLance;
        public int ExpendBonus;
        public bool ExpendIgnoresShields;

        public override string Validate(Battle b, PlayContext ctx)
        {
            var s = ctx.Source;
            var t = ctx.Target;
            if (s == null || !s.Alive) return "source ship is gone";
            if (t == null || !t.Alive || t.Side == s.Side) return "needs an enemy target";
            if (!Hex.Arcs.InAny(s.Pos, s.Facing, t.Pos, Arcs)) return "target is outside the weapon's arc";
            if (s.Pos.DistanceTo(t.Pos) > Range) return "target is out of range";
            if (!b.Board.HasLineOfFire(s.Pos, t.Pos)) return "an asteroid blocks the line of fire";
            return null;
        }

        public override void Apply(Battle b, PlayContext ctx)
        {
            var s = ctx.Source;
            var t = ctx.Target;
            int dmg = Damage + t.MarkBonus;
            var flags = new DamageFlags();
            if (ctx.Expended)
            {
                dmg += ExpendBonus;
                flags.IgnoreShields = ExpendIgnoresShields;
            }
            if (IsLance)
            {
                dmg += s.LanceBonus;
                s.LanceBonus = 0;
            }
            if (b.WaymindPrimed)
            {
                flags.IgnoreArmor = true;
                flags.CritOnAnyHull = true;
                b.WaymindPrimed = false;
            }
            b.AddHeat(s, Heat);
            b.DealDamage(t, dmg, s.Pos, flags, s);
        }
    }

    /// <summary>Burn: Speed +1 (up to max) and heat on the chosen capital ship.</summary>
    public sealed class SpeedEffect : Effect
    {
        public int Delta;
        public int Heat;

        public override string Validate(Battle b, PlayContext ctx)
        {
            var t = ctx.Target;
            if (Delta > 0 && t.NoBurnThisTurn) return "engines are damaged: no Burn this turn";
            if (Delta > 0 && t.Speed >= t.MaxSpeed) return "already at max speed";
            if (Delta < 0 && t.Speed <= 0) return "already stopped";
            return null;
        }

        public override void Apply(Battle b, PlayContext ctx)
        {
            var t = ctx.Target;
            int before = t.Speed;
            t.Speed = System.Math.Max(0, System.Math.Min(t.MaxSpeed, t.Speed + Delta));
            b.Emit(EventKind.SpeedChanged, t.Id, -1, t.Speed, $"{t.Name} speed {before} -> {t.Speed}");
            b.AddHeat(t, Heat);
        }
    }

    /// <summary>Hard About: turn 60 degrees, or up to 120 at Speed 0-1. The choice is in <see cref="PlayTarget.Turn"/>.</summary>
    public sealed class TurnEffect : Effect
    {
        public int SlowSpeed = 1;

        public override string Validate(Battle b, PlayContext ctx)
        {
            int steps = System.Math.Abs(ctx.Choice.Turn);
            if (steps == 0) return "choose a turn direction";
            if (steps > 2) return "can turn at most twice";
            if (steps == 2 && ctx.Target.Speed > SlowSpeed) return "can only turn twice at Speed 0-1";
            return null;
        }

        public override void Apply(Battle b, PlayContext ctx) => b.TurnShip(ctx.Target, ctx.Choice.Turn);
    }

    /// <summary>Brace: extra shields on every arc until the End step.</summary>
    public sealed class ShieldAllArcsEffect : Effect
    {
        public int Amount;

        public override void Apply(Battle b, PlayContext ctx)
        {
            for (int i = 0; i < ArcIndex.Count; i++) ctx.Target.Shields[i] += Amount;
            b.Emit(EventKind.ShieldsRaised, ctx.Target.Id, -1, Amount, $"{ctx.Target.Name} +{Amount} shields on every arc");
        }
    }

    /// <summary>Remove heat from the chosen ship, or from every player capital ship.</summary>
    public sealed class RemoveHeatEffect : Effect
    {
        public int Amount;
        public bool AllShips;

        public override void Apply(Battle b, PlayContext ctx)
        {
            if (!AllShips)
            {
                b.AddHeat(ctx.Target, -Amount);
                return;
            }
            foreach (var u in b.LiveUnits(Side.Player).Where(u => u.IsCapital).ToList()) b.AddHeat(u, -Amount);
        }
    }

    /// <summary>Launch darts next to the source ship, up to a cap on darts in space. They can act this turn.</summary>
    public sealed class LaunchDartsEffect : Effect
    {
        public int Count;
        public int MaxOut;

        public override string Validate(Battle b, PlayContext ctx)
        {
            if (b.PlayerCraftCount() >= MaxOut) return $"already {MaxOut} darts out";
            if (!b.FreeNeighbors(ctx.Source.Pos).Any()) return "no free hex next to the ship";
            return null;
        }

        public override void Apply(Battle b, PlayContext ctx)
        {
            for (int i = 0; i < Count && b.PlayerCraftCount() < MaxOut; i++)
            {
                var free = b.FreeNeighbors(ctx.Source.Pos).ToList();
                if (free.Count == 0) break;
                b.Spawn(b.DartSpec, Side.Player, free[0], ctx.Source.Facing, 0);
            }
        }
    }

    /// <summary>Overcharge Lances: the source's next Lance Volley this turn deals more, for heat now.</summary>
    public sealed class LanceBonusEffect : Effect
    {
        public int Bonus;
        public int Heat;

        public override void Apply(Battle b, PlayContext ctx)
        {
            ctx.Source.LanceBonus += Bonus;
            b.Emit(EventKind.Buff, ctx.Source.Id, -1, Bonus, $"{ctx.Source.Name}: next Lance Volley +{Bonus}");
            b.AddHeat(ctx.Source, Heat);
        }
    }

    public sealed class GainCommandEffect : Effect
    {
        public int Amount;

        public override void Apply(Battle b, PlayContext ctx)
        {
            b.Command += Amount;
            b.Emit(EventKind.Command, -1, -1, Amount, $"+{Amount} command");
        }
    }

    public sealed class DrawEffect : Effect
    {
        public int Count;
        public override void Apply(Battle b, PlayContext ctx) => b.DrawCards(Count);
    }

    /// <summary>
    /// Sacrificial Ram: the source escort rams an enemy straight ahead within Speed + extra hexes, with a
    /// clear path. The enemy takes damage equal to its remaining hull (destroyed) and the escort is lost.
    /// </summary>
    public sealed class RamEffect : Effect
    {
        public int ExtraRange;

        public override string Validate(Battle b, PlayContext ctx)
        {
            var s = ctx.Source;
            var t = ctx.Target;
            if (t == null || !t.Alive || t.Side == s.Side) return "needs an enemy target";
            int reach = s.Speed + ExtraRange;
            var h = s.Pos;
            for (int i = 1; i <= reach; i++)
            {
                h = h.Neighbor(s.Facing);
                if (h == t.Pos) return null;
                if (b.Board.BlocksMovement(h) || b.UnitAt(h) != null) return "path to the target is blocked";
            }
            return "target is not straight ahead within reach";
        }

        public override void Apply(Battle b, PlayContext ctx)
        {
            var s = ctx.Source;
            var t = ctx.Target;
            var from = t.Pos.Neighbor(s.Facing.Opposite());
            b.Emit(EventKind.Moved, s.Id, -1, 0, $"{s.Name} rams {t.Name}", s.Pos, t.Pos);
            b.DealDamage(t, t.Hull, from, new DamageFlags { Direct = true }, s);
            b.Destroy(s);
        }
    }

    /// <summary>Boarding Action: an enemy within range of the source loses its intent this turn.</summary>
    public sealed class RemoveIntentEffect : Effect
    {
        public int Range;

        public override string Validate(Battle b, PlayContext ctx)
        {
            var t = ctx.Target;
            if (t == null || !t.Alive || t.Side != Side.Enemy) return "needs an enemy target";
            if (ctx.Source.Pos.DistanceTo(t.Pos) > Range) return $"target must be within {Range} hexes";
            return null;
        }

        public override void Apply(Battle b, PlayContext ctx) => b.CancelIntent(ctx.Target.Id);
    }

    /// <summary>Waymind Targeting: the next weapon ignores armor and crits on any hull damage.</summary>
    public sealed class WaymindEffect : Effect
    {
        public override void Apply(Battle b, PlayContext ctx)
        {
            b.WaymindPrimed = true;
            b.Emit(EventKind.Buff, ctx.Source.Id, -1, 0, "Waymind Targeting: next weapon ignores armor and crits");
        }
    }

    /// <summary>Precision Edict: each weapon that hits the marked enemy this turn deals more.</summary>
    public sealed class MarkEffect : Effect
    {
        public int Bonus;

        public override string Validate(Battle b, PlayContext ctx)
            => ctx.Target == null || !ctx.Target.Alive || ctx.Target.Side != Side.Enemy ? "needs an enemy target" : null;

        public override void Apply(Battle b, PlayContext ctx)
        {
            ctx.Target.MarkBonus += Bonus;
            b.Emit(EventKind.Buff, ctx.Target.Id, -1, Bonus, $"{ctx.Target.Name} is marked: hits deal +{Bonus}");
        }
    }

    /// <summary>Screening Maneuver: the source turns 60 degrees, moves 1 and guards adjacent allies.</summary>
    public sealed class ScreenEffect : Effect
    {
        public override string Validate(Battle b, PlayContext ctx)
            => System.Math.Abs(ctx.Choice.Turn) != 1 ? "choose a 60 degree turn (+1 or -1)" : null;

        public override void Apply(Battle b, PlayContext ctx)
        {
            var s = ctx.Source;
            b.TurnShip(s, ctx.Choice.Turn);
            var next = s.Pos.Neighbor(s.Facing);
            if (!b.Board.BlocksMovement(next) && b.UnitAt(next) == null)
            {
                b.Emit(EventKind.Moved, s.Id, -1, 1, $"{s.Name} moves 1", s.Pos, next);
                s.Pos = next;
            }
            s.Guarding = true;
            b.Emit(EventKind.Buff, s.Id, -1, 0, $"{s.Name} screens adjacent allies");
        }
    }

    /// <summary>Dart Sortie: darts that have not acted this turn move further and attack twice.</summary>
    public sealed class SortieEffect : Effect
    {
        public int BonusMove;
        public int Attacks;

        public override void Apply(Battle b, PlayContext ctx)
        {
            foreach (var d in b.LiveUnits(Side.Player).Where(u => u.Kind == UnitKind.Craft && !u.CraftActed))
            {
                d.CraftBonusMove = BonusMove;
                d.CraftAttacks = Attacks;
            }
            b.Emit(EventKind.Buff, ctx.Source.Id, -1, Attacks, $"Dart Sortie: +{BonusMove} move, {Attacks} attacks");
        }
    }

    /// <summary>Flak Lattice: destroy enemy craft and torpedoes next to the source ship.</summary>
    public sealed class FlakEffect : Effect
    {
        public int Count;

        public override void Apply(Battle b, PlayContext ctx)
        {
            var s = ctx.Source;
            var hits = b.LiveUnits(Side.Enemy)
                .Where(u => !u.IsCapital && u.Pos.DistanceTo(s.Pos) == 1)
                .Take(Count).ToList();
            foreach (var u in hits)
            {
                b.Emit(EventKind.Damage, u.Id, s.Id, u.Hull, $"{s.Name} flak destroys {u.Name}");
                u.Hull = 0;
                b.Destroy(u);
            }
        }
    }
}
