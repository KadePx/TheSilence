using System;
using Silence.Core.Hex;

namespace Silence.Core.Battle
{
    public enum Side { Player, Enemy }

    public enum UnitKind { Capital, Craft, Torpedo }

    /// <summary>Which intent cycle an enemy runs (Docs/PaperPrototype.md, Enemy).</summary>
    public enum AiProfile { None, Frigate, Gunship, Carrier, Torpedo }

    /// <summary>One step of an enemy capital ship's repeating intent cycle.</summary>
    public sealed class IntentStep
    {
        public IntentKind Kind;
        /// <summary>Damage of an attack, hull repaired by Patch Up, or number of craft launched.</summary>
        public int Amount;
        public int Range;
        public Arc Arcs;
        /// <summary>Name shown to the player ("Broadside", "Mass Driver").</summary>
        public string Label;

        public static IntentStep Fire(string label, int damage, Arc arcs, int range)
            => new IntentStep { Kind = IntentKind.Broadside, Label = label, Amount = damage, Arcs = arcs, Range = range };
        public static IntentStep Of(IntentKind kind, int amount = 0) => new IntentStep { Kind = kind, Amount = amount };
    }

    /// <summary>Index of an arc in per-arc arrays (shields, armor). Matches the bit order of <see cref="Arc"/>.</summary>
    public static class ArcIndex
    {
        public const int Fore = 0, Starboard = 1, Aft = 2, Port = 3, Count = 4;

        public static int Of(Arc single)
        {
            switch (single)
            {
                case Arc.Fore: return Fore;
                case Arc.Starboard: return Starboard;
                case Arc.Aft: return Aft;
                case Arc.Port: return Port;
                default: throw new ArgumentException($"not a single arc: {single}");
            }
        }

        public static Arc ToArc(int index) => (Arc)(1 << index);
    }

    /// <summary>
    /// Template for a unit: what a hull or craft starts a battle with. Pure data; the Data layer
    /// builds these from ScriptableObjects later.
    /// </summary>
    public sealed class UnitSpec
    {
        /// <summary>Key in <see cref="UnitLibrary"/> and the name of its sprite set.</summary>
        public string Id = "";
        public string Name;
        public UnitKind Kind;
        public AiProfile Ai;
        /// <summary>Intent cycle for enemy capital ships (Frigate and Carrier profiles).</summary>
        public IntentStep[] Cycle = new IntentStep[0];
        public int Hull;
        /// <summary>Armor per arc, indexed by <see cref="ArcIndex"/>.</summary>
        public int[] Armor = new int[ArcIndex.Count];
        public int Shields;
        public int ShieldRegen;
        public int HeatCap;
        public int Vent;
        public int MaxSpeed;
        /// <summary>Free move per turn for craft and torpedoes.</summary>
        public int Move;
        /// <summary>Damage of a craft's automatic attack or a torpedo's hit.</summary>
        public int AttackDamage;

        public static UnitSpec Capital(string name, int hull, int fore, int port, int starboard, int aft,
            int shields, int regen, int heatCap, int vent, int maxSpeed, AiProfile ai = AiProfile.None)
        {
            var s = new UnitSpec
            {
                Name = name, Kind = UnitKind.Capital, Ai = ai, Hull = hull, Shields = shields, ShieldRegen = regen,
                HeatCap = heatCap, Vent = vent, MaxSpeed = maxSpeed,
            };
            s.Armor[ArcIndex.Fore] = fore;
            s.Armor[ArcIndex.Port] = port;
            s.Armor[ArcIndex.Starboard] = starboard;
            s.Armor[ArcIndex.Aft] = aft;
            return s;
        }

        public static UnitSpec Craft(string name, int hull, int move, int damage, AiProfile ai = AiProfile.None)
            => new UnitSpec { Name = name, Kind = UnitKind.Craft, Ai = ai, Hull = hull, Move = move, AttackDamage = damage };
    }

    /// <summary>A capital ship, small craft or torpedo on the board.</summary>
    public sealed class Unit
    {
        public int Id;
        public string SpecId = "";
        public string Name;
        public IntentStep[] Cycle = new IntentStep[0];
        public Side Side;
        public UnitKind Kind;
        public AiProfile Ai;
        public bool IsFlagship;
        public bool Alive = true;

        public HexCoord Pos;
        public HexDirection Facing;

        public int Hull, MaxHull;
        public readonly int[] Armor = new int[ArcIndex.Count];
        public readonly int[] Shields = new int[ArcIndex.Count];
        public int MaxShields, ShieldRegen;
        public int Heat, HeatCap, Vent;
        public int Speed, MaxSpeed;
        public int Move, AttackDamage;

        /// <summary>Torpedo target.</summary>
        public int TargetId = -1;
        /// <summary>Position in the enemy intent cycle.</summary>
        public int CycleIndex;

        // ---- per-turn state, cleared in the End step ----
        public int LanceBonus;
        public int MarkBonus;
        public bool Guarding;
        public bool NoBurnThisTurn;
        public bool NoBurnNextTurn;

        // ---- craft, reset at the start of each turn ----
        /// <summary>This capital ship has changed speed with the throttle this turn.</summary>
        public bool ThrottleUsed;
        public bool CraftActed;
        public int CraftBonusMove;
        public int CraftAttacks = 1;

        public bool IsCapital => Kind == UnitKind.Capital;

        public static Unit From(UnitSpec spec, int id, Side side, HexCoord pos, HexDirection facing, int speed)
        {
            var u = new Unit
            {
                Id = id, SpecId = spec.Id, Name = spec.Name, Cycle = spec.Cycle, Side = side, Kind = spec.Kind, Ai = spec.Ai, Pos = pos, Facing = facing,
                Hull = spec.Hull, MaxHull = spec.Hull, MaxShields = spec.Shields, ShieldRegen = spec.ShieldRegen,
                HeatCap = spec.HeatCap, Vent = spec.Vent, MaxSpeed = spec.MaxSpeed, Speed = speed,
                Move = spec.Move, AttackDamage = spec.AttackDamage,
            };
            for (int i = 0; i < ArcIndex.Count; i++)
            {
                u.Armor[i] = spec.Armor[i];
                u.Shields[i] = spec.Shields;
            }
            return u;
        }

        public override string ToString()
        {
            var (col, row) = Pos.ToOffset();
            if (!IsCapital) return $"#{Id} {Name} [{Side}] at {col},{row} hull {Hull}/{MaxHull}";
            return $"#{Id} {Name} [{Side}{(IsFlagship ? ", flagship" : "")}] at {col},{row} facing {Facing} speed {Speed} " +
                   $"hull {Hull}/{MaxHull} shields F{Shields[0]} S{Shields[1]} A{Shields[2]} P{Shields[3]} " +
                   $"armor F{Armor[0]} S{Armor[1]} A{Armor[2]} P{Armor[3]}" + (HeatCap > 0 ? $" heat {Heat}/{HeatCap}" : "");
        }
    }
}
