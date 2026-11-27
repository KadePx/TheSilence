using System;
using System.Collections.Generic;
using System.Linq;
using Silence.Core.Hex;

namespace Silence.Core.Battle
{
    // ================================================================ effects the Concord cards need

    /// <summary>Every enemy within range of the source takes damage, whatever arc it is in (Ring Barrage).</summary>
    public sealed class BurstEffect : Effect
    {
        public int Damage;
        public int Range;
        public int Heat;

        List<Unit> Hits(Battle b, Unit s)
            => b.LiveUnits(Side.Enemy).Where(e => s.Pos.DistanceTo(e.Pos) <= b.Reach(Range) && b.Board.HasLineOfFire(s.Pos, e.Pos)).ToList();

        public override string Validate(Battle b, PlayContext ctx)
            => Hits(b, ctx.Source).Count == 0 ? $"no enemy within {Range} hexes" : null;

        public override void Apply(Battle b, PlayContext ctx)
        {
            var s = ctx.Source;
            b.AddHeat(s, Heat);
            foreach (var e in Hits(b, s))
            {
                if (b.Outcome != BattleOutcome.Ongoing) break;
                b.Emit(EventKind.Attack, s.Id, e.Id, Damage, $"{s.Name} fires the ring at {e.Name}");
                b.DealDamage(e, Damage + e.MarkBonus + b.WeaponBonusFor(s), s.Pos, new DamageFlags(), s);
            }
        }
    }

    /// <summary>
    /// Several small hits on one enemy within range, any arc (Ripple Salvo). Each missile is its own attack, so
    /// shields and armor take their bite out of every one. If the target dies the rest go to the nearest enemy left.
    /// </summary>
    public sealed class SalvoEffect : Effect
    {
        public int Hits;
        public int Damage;
        public int Range;
        public int Heat;

        public override string Validate(Battle b, PlayContext ctx)
        {
            var s = ctx.Source;
            var t = ctx.Target;
            if (t == null || !t.Alive || t.Side != Side.Enemy) return "needs an enemy target";
            if (s.Pos.DistanceTo(t.Pos) > b.Reach(Range)) return "target is out of range";
            if (!b.Board.HasLineOfFire(s.Pos, t.Pos)) return "an asteroid blocks the line of fire";
            return null;
        }

        public override void Apply(Battle b, PlayContext ctx)
        {
            var s = ctx.Source;
            var t = ctx.Target;
            b.AddHeat(s, Heat);
            for (int i = 0; i < Hits && b.Outcome == BattleOutcome.Ongoing; i++)
            {
                if (!t.Alive)
                {
                    t = b.LiveUnits(Side.Enemy).Where(e => s.Pos.DistanceTo(e.Pos) <= b.Reach(Range) && b.Board.HasLineOfFire(s.Pos, e.Pos))
                        .OrderBy(e => e.Pos.DistanceTo(s.Pos)).ThenBy(e => e.Id).FirstOrDefault();
                    if (t == null) break;
                }
                b.Emit(EventKind.Attack, s.Id, t.Id, Damage, $"{s.Name} missile at {t.Name}");
                b.DealDamage(t, Damage + t.MarkBonus, s.Pos, new DamageFlags(), s);
            }
        }
    }

    /// <summary>Repair hull on every player capital ship (Patch Crews).</summary>
    public sealed class RepairFleetEffect : Effect
    {
        public int Amount;

        public override string Validate(Battle b, PlayContext ctx)
            => b.LiveUnits(Side.Player).Any(u => u.IsCapital && u.Hull < u.MaxHull) ? null : "no ship is damaged";

        public override void Apply(Battle b, PlayContext ctx)
        {
            foreach (var u in b.LiveUnits(Side.Player).Where(u => u.IsCapital && u.Hull < u.MaxHull).ToList())
            {
                int before = u.Hull;
                u.Hull = Math.Min(u.MaxHull, u.Hull + Amount + b.RepairBonus);
                b.Emit(EventKind.Repaired, u.Id, -1, u.Hull - before, $"{u.Name} repairs {u.Hull - before} hull ({u.Hull})");
            }
        }
    }

    /// <summary>The next weapon card this turn hits harder and reaches further (Spotter Data).</summary>
    public sealed class SpotterEffect : Effect
    {
        public int Bonus;
        public int Range;

        public override void Apply(Battle b, PlayContext ctx)
        {
            b.SpotterBonus += Bonus;
            b.SpotterRange += Range;
            b.Emit(EventKind.Buff, ctx.Source.Id, -1, Bonus, $"Spotter data: next weapon +{Bonus} damage, +{Range} range");
        }
    }

    /// <summary>More command at the start of the next turn (Council Vote).</summary>
    public sealed class NextTurnCommandEffect : Effect
    {
        public int Amount;

        public override void Apply(Battle b, PlayContext ctx)
        {
            b.NextTurnCommand += Amount;
            b.Emit(EventKind.Buff, -1, -1, Amount, $"The vote carries: +{Amount} command next turn");
        }
    }

    /// <summary>Your craft fly cover: the next enemy attacks on a ship with a craft beside it hit the craft instead (Combat Patrol).</summary>
    public sealed class PatrolEffect : Effect
    {
        public int Charges;

        public override string Validate(Battle b, PlayContext ctx) => b.PlayerCraftCount() == 0 ? "no craft are out" : null;

        public override void Apply(Battle b, PlayContext ctx)
        {
            b.PatrolCharges += Charges;
            b.Emit(EventKind.Buff, ctx.Source.Id, -1, Charges, $"Combat patrol: the next {Charges} attack{(Charges == 1 ? "" : "s")} on a covered ship hit a craft instead");
        }
    }

    // ================================================================ units

    /// <summary>The Concord as a playable fleet, and the Ascendancy as the enemy it meets.</summary>
    public static class ConcordUnits
    {
        const Arc Sides = Arc.Port | Arc.Starboard;

        static UnitSpec Named(string id, UnitSpec s, params IntentStep[] cycle)
        {
            s.Id = id;
            s.Cycle = cycle;
            return s;
        }

        // ---- the player's Concord fleet ----

        /// <summary>Trimaran carrier: tougher and slower than the lance-cruiser, with a hotter reactor. Spec: Hull 36, armor 2, shields 3, Speed 2.</summary>
        public static UnitSpec LongPromise => Named("con_trimaran", UnitSpec.Capital("Long Promise", 36, 2, 2, 2, 1, 3, 1, 8, 1, 2));

        public static UnitSpec Outrider => Named("con_outrider", UnitSpec.Capital("Outrider", 16, 1, 1, 1, 1, 3, 1, 6, 2, 2));

        /// <summary>A wing gunship flown by hand: sturdier and harder-hitting than a dart, and there are fewer of them.</summary>
        public static UnitSpec WingGunship => Named("con_gunship", UnitSpec.Craft("Wing gunship", 3, 4, 3));

        // ---- the Ascendancy as an enemy ----

        public static UnitSpec EnemyBlade => Named("asc_blade",
            UnitSpec.Capital("Ascendancy Blade", 12, 1, 1, 1, 0, 2, 1, 0, 0, 3, AiProfile.Frigate),
            IntentStep.Of(IntentKind.Maneuver), IntentStep.Fire("Fore guns", 7, Arc.Fore, 3));

        public static UnitSpec EnemyCruiser => Named("asc_cruiser",
            UnitSpec.Capital("Lance-cruiser", 24, 3, 2, 2, 1, 3, 1, 0, 0, 2, AiProfile.Frigate),
            IntentStep.Of(IntentKind.Maneuver), IntentStep.Fire("Lance Volley", 11, Arc.Fore, 4), IntentStep.Fire("Broadside", 7, Sides, 3));

        /// <summary>A vat-ship: it fights through its darts and spends them freely.</summary>
        public static UnitSpec EnemyWarden => Named("asc_cruiser",
            UnitSpec.Capital("Vat-warden", 24, 2, 2, 2, 1, 3, 1, 0, 0, 1, AiProfile.Carrier),
            IntentStep.Of(IntentKind.LaunchCraft, 3), IntentStep.Fire("Broadside", 8, Sides, 3), IntentStep.Of(IntentKind.JointFire));

        public static UnitSpec EnemyDart => Named("asc_dart", UnitSpec.Craft("Ordinal dart", 2, 4, 3, AiProfile.Gunship));

        /// <summary>Sector boss on the Concord's route: the High Inquisitor's ship. Leave the lance's fore arc before it fires.</summary>
        public static UnitSpec NinthSentence => Named("asc_ninth",
            UnitSpec.Capital("Ninth Sentence", 44, 3, 2, 2, 1, 4, 1, 0, 0, 1, AiProfile.Carrier),
            IntentStep.Of(IntentKind.LaunchCraft, 2), IntentStep.Fire("Grand Lance", 14, Arc.Fore, 5),
            IntentStep.Of(IntentKind.Maneuver), IntentStep.Of(IntentKind.JointFire), IntentStep.Fire("Broadside", 8, Sides, 3));

        // ---- Ascendancy commanders hunting a Concord run ----

        /// <summary>Inquisitor-Adjunct Vael's cruiser: the longest lance in the sector.</summary>
        public static UnitSpec FinalTally => Named("asc_finaltally",
            UnitSpec.Capital("Final Tally", 22, 3, 2, 2, 1, 3, 1, 0, 0, 2, AiProfile.Frigate),
            IntentStep.Of(IntentKind.Maneuver), IntentStep.Fire("Audit Lance", 12, Arc.Fore, 5), IntentStep.Fire("Broadside", 7, Sides, 3));

        /// <summary>Lictor-Captain Achter's Blade: fast, and it never stops shooting.</summary>
        public static UnitSpec CleanHand => Named("asc_cleanhand",
            UnitSpec.Capital("Clean Hand", 18, 1, 1, 1, 0, 3, 1, 0, 0, 3, AiProfile.Frigate),
            IntentStep.Fire("Fore guns", 8, Arc.Fore, 3), IntentStep.Of(IntentKind.Maneuver), IntentStep.Fire("Fore guns", 8, Arc.Fore, 3));

        /// <summary>Vat-Master Rell's ship: darts, and more darts.</summary>
        public static UnitSpec TitheOfNine => Named("asc_titheofnine",
            UnitSpec.Capital("Tithe of Nine", 24, 2, 2, 2, 1, 3, 1, 0, 0, 1, AiProfile.Carrier),
            IntentStep.Of(IntentKind.LaunchCraft, 3), IntentStep.Of(IntentKind.JointFire), IntentStep.Fire("Broadside", 7, Sides, 3));

        public static UnitSpec Get(string id)
        {
            switch (id)
            {
                case "con_flagship": return LongPromise;
                case "con_escort": return Outrider;
                case "wing_gunship": return WingGunship;
                case "asc_blade": return EnemyBlade;
                case "asc_cruiser": return EnemyCruiser;
                case "asc_warden": return EnemyWarden;
                case "asc_dart": return EnemyDart;
                case "ninth_sentence": return NinthSentence;
                case "final_tally": return FinalTally;
                case "clean_hand": return CleanHand;
                case "tithe_of_nine": return TitheOfNine;
                default: return null;
            }
        }
    }

    // ================================================================ cards

    /// <summary>The Concord's cards: the Long Promise's hull and modules, the Outrider, and the Council.</summary>
    public static class ConcordCards
    {
        const Arc Sides = Arc.Port | Arc.Starboard;
        const Arc AllArcs = Arc.Fore | Arc.Aft | Arc.Port | Arc.Starboard;

        public static readonly CardDef MassDriver = new CardDef
        {
            Id = "mass_driver", Name = "Mass Driver", Cost = 2, Type = CardType.Weapon, Tags = "Weapon · Driver", Module = "Bow driver",
            Text = "10 dmg, fore arc, range 5, +3 Heat. Pushes the target 1 hex away.", Target = TargetKind.Enemy,
            Effects = new Effect[] { new AttackEffect { Damage = 10, Arcs = Arc.Fore, Range = 5, Heat = 3, Heavy = true, Push = 1 } },
        };

        public static readonly CardDef DeckGuns = new CardDef
        {
            Id = "deck_guns", Name = "Deck Guns", Cost = 1, Type = CardType.Weapon, Tags = "Weapon", Module = "Outrigger guns",
            Text = "5 dmg, port or starboard arc, range 3, +1 Heat.", Target = TargetKind.Enemy,
            Effects = new Effect[] { new AttackEffect { Damage = 5, Arcs = Sides, Range = 3, Heat = 1 } },
        };

        public static readonly CardDef PatchJob = new CardDef
        {
            Id = "patch_job", Name = "Patch Job", Cost = 1, Type = CardType.Defense, Tags = "Repair", Module = "Bridge",
            Text = "Repair 4 hull on one ship.", Target = TargetKind.AllyCapital,
            Effects = new Effect[] { new RepairEffect { Amount = 4 } },
        };

        public static readonly CardDef CouncilVote = new CardDef
        {
            Id = "council_vote", Name = "Council Vote", Cost = 0, Type = CardType.Order, Tags = "Order · Council", Module = "Bridge",
            Text = "Draw 1. +1 command next turn.",
            Effects = new Effect[] { new DrawEffect { Count = 1 }, new NextTurnCommandEffect { Amount = 1 } },
        };

        public static readonly CardDef RingShield = new CardDef
        {
            Id = "ring_shield", Name = "Ring Shield", Cost = 1, Type = CardType.Defense, Module = "Shield projector",
            Text = "One ship: +6 shields on every arc this turn.", Target = TargetKind.AllyCapital,
            Effects = new Effect[] { new ShieldAllArcsEffect { Amount = 6 } },
        };

        public static readonly CardDef RingBarrage = new CardDef
        {
            Id = "ring_barrage", Name = "Ring Barrage", Cost = 2, Type = CardType.Weapon, Tags = "Weapon", Module = "Gun ring",
            Text = "3 dmg to every enemy within 2 hexes, any arc. +2 Heat.",
            Effects = new Effect[] { new BurstEffect { Damage = 3, Range = 2, Heat = 2 } },
        };

        public static readonly CardDef LaunchWing = new CardDef
        {
            Id = "launch_wing", Name = "Launch Wing", Cost = 1, Type = CardType.Hangar, Tags = "Hangar · Swarm", Module = "Flight decks",
            Text = "Launch 2 gunships next to the flagship (max 4 out); they act this turn.",
            Effects = new Effect[] { new LaunchDartsEffect { Count = 2, MaxOut = 4 } },
        };

        public static readonly CardDef CombatPatrol = new CardDef
        {
            Id = "combat_patrol", Name = "Combat Patrol", Cost = 1, Type = CardType.Hangar, Tags = "Hangar · Swarm", Module = "Flight decks",
            Text = "The next enemy attack on a ship with one of your craft beside it hits the craft instead.",
            Effects = new Effect[] { new PatrolEffect { Charges = 1 } },
        };

        public static readonly CardDef RippleSalvo = new CardDef
        {
            Id = "ripple_salvo", Name = "Ripple Salvo", Cost = 2, Type = CardType.Weapon, Tags = "Weapon · Ordnance", Module = "Missile racks",
            Text = "4 missiles of 3 dmg at one enemy within 4, any arc. If it dies the rest find the next. +2 Heat.", Target = TargetKind.Enemy,
            Effects = new Effect[] { new SalvoEffect { Hits = 4, Damage = 3, Range = 4, Heat = 2 } },
        };

        public static readonly CardDef PatchCrews = new CardDef
        {
            Id = "patch_crews", Name = "Patch Crews", Cost = 1, Type = CardType.Defense, Tags = "Repair", Module = "Drone bay",
            Text = "Repair 2 hull on every ship.",
            Effects = new Effect[] { new RepairFleetEffect { Amount = 2 } },
        };

        public static readonly CardDef CuttingBeam = new CardDef
        {
            Id = "cutting_beam", Name = "Cutting Beam", Cost = 1, Type = CardType.Weapon, Tags = "Weapon", Module = "Cutting beam",
            Text = "7 dmg to an adjacent enemy, any arc, ignoring armor. +2 Heat.", Target = TargetKind.Enemy,
            Effects = new Effect[] { new AttackEffect { Damage = 7, Arcs = AllArcs, Range = 1, Heat = 2, IgnoreArmor = true } },
        };

        public static readonly CardDef LongScan = new CardDef
        {
            Id = "long_scan", Name = "Long Scan", Cost = 0, Type = CardType.Sensor, Module = "Survey prow",
            Text = "Draw 2. Exhaust.", Exhaust = true,
            Effects = new Effect[] { new DrawEffect { Count = 2 } },
        };

        public static readonly CardDef SpotterData = new CardDef
        {
            Id = "spotter_data", Name = "Spotter Data", Cost = 1, Type = CardType.Sensor, Module = "Survey prow",
            Text = "The next weapon this turn deals +3 and reaches 1 hex further.",
            Effects = new Effect[] { new SpotterEffect { Bonus = 3, Range = 1 } },
        };

        public static readonly CardDef OutriderGuns = new CardDef
        {
            Id = "outrider_guns", Name = "Outrider Guns", Cost = 1, Type = CardType.Weapon, Tags = "Weapon", Module = "Broadside",
            Text = "5 dmg, escort's port or starboard arc, range 3, +2 Heat.", Target = TargetKind.Enemy,
            Effects = new Effect[] { new AttackEffect { Damage = 5, Arcs = Sides, Range = 3, Heat = 2 } },
        };

        public static readonly CardDef OutriderTorpedo = new CardDef
        {
            Id = "outrider_torpedo", Name = "Torpedo Run", Cost = 2, Type = CardType.Weapon, Tags = "Weapon · Ordnance", Module = "Torpedo tube",
            Text = "8 dmg, escort's fore arc, range 3, +2 Heat. Exhaust.", Target = TargetKind.Enemy, Exhaust = true,
            Effects = new Effect[] { new AttackEffect { Damage = 8, Arcs = Arc.Fore, Range = 3, Heat = 2 } },
        };

        public static readonly CardDef[] Everything =
        {
            MassDriver, DeckGuns, PatchJob, CouncilVote, RingShield, RingBarrage, LaunchWing, CombatPatrol, RippleSalvo,
            PatchCrews, CuttingBeam, LongScan, SpotterData, OutriderGuns, OutriderTorpedo,
        };
    }
}
