using System.Collections.Generic;
using Silence.Core.Hex;

namespace Silence.Core.Battle
{
    /// <summary>
    /// Units from Docs/PaperPrototype.md. Placeholder content in code until the Data layer turns it into
    /// ScriptableObjects. Dart damage (2) is not in the prototype and is a first guess.
    /// </summary>
    public static class PaperUnits
    {
        public static UnitSpec LanceCruiser => UnitSpec.Capital("Sixth Verdict", 30, 3, 2, 2, 1, 4, 2, 10, 2, 3);
        public static UnitSpec Blade => UnitSpec.Capital("Blade", 14, 1, 1, 1, 0, 2, 1, 6, 2, 3);
        public static UnitSpec Dart => UnitSpec.Craft("Ordinal dart", 2, 4, 2);

        public static UnitSpec Frigate => UnitSpec.Capital("Concord frigate", 16, 1, 1, 1, 1, 3, 1, 0, 0, 2, AiProfile.Frigate);
        public static UnitSpec Gunship => UnitSpec.Craft("Gunship", 2, 4, 2, AiProfile.Gunship);
        public static UnitSpec Carrier => UnitSpec.Capital("Long Promise", 40, 2, 2, 2, 1, 5, 2, 0, 0, 1, AiProfile.Carrier);

        public static UnitSpec Torpedo
        {
            get
            {
                var s = UnitSpec.Craft("Torpedo", 1, 3, 6, AiProfile.Torpedo);
                s.Kind = UnitKind.Torpedo;
                return s;
            }
        }
    }

    /// <summary>The 20 paper-prototype cards (10 starter, 10 reward) and the three junk cards, as effect blocks.</summary>
    public static class PaperCards
    {
        // ---- starter ----

        public static readonly CardDef LanceVolley = new CardDef
        {
            Id = "lance_volley", Name = "Lance Volley", Cost = 2, Type = CardType.Weapon, Tags = "Weapon · Lance", Module = "Fore lance",
            Text = "9 dmg, fore arc, range 4, +3 Heat. Expend 3 Ordinals: +6 and ignore shields.",
            Target = TargetKind.Enemy, ExpendCost = 3, ExpendOptional = true,
            Effects = new Effect[] { new AttackEffect { Damage = 9, Arcs = Arc.Fore, Range = 4, Heat = 3, IsLance = true, ExpendBonus = 6, ExpendIgnoresShields = true } },
        };

        public static readonly CardDef BroadsideBattery = new CardDef
        {
            Id = "broadside_battery", Name = "Broadside Battery", Cost = 1, Type = CardType.Weapon, Tags = "Weapon", Module = "Broadside",
            Text = "4 dmg, port or starboard arc, range 3, +1 Heat.", Target = TargetKind.Enemy,
            Effects = new Effect[] { new AttackEffect { Damage = 4, Arcs = Arc.Port | Arc.Starboard, Range = 3, Heat = 1 } },
        };

        public static readonly CardDef Burn = new CardDef
        {
            Id = "burn", Name = "Burn", Cost = 0, Type = CardType.Maneuver, Module = "Bridge",
            Text = "One capital ship: Speed +1 (max 3), +1 Heat.", Target = TargetKind.AllyCapital,
            Effects = new Effect[] { new SpeedEffect { Delta = 1, Heat = 1 } },
        };

        public static readonly CardDef HardAbout = new CardDef
        {
            Id = "hard_about", Name = "Hard About", Cost = 1, Type = CardType.Maneuver, Module = "Bridge",
            Text = "Turn one capital ship 60°; twice at Speed 0-1.", Target = TargetKind.AllyCapital,
            Effects = new Effect[] { new TurnEffect() },
        };

        public static readonly CardDef Brace = new CardDef
        {
            Id = "brace", Name = "Brace", Cost = 1, Type = CardType.Defense, Module = "Bridge",
            Text = "One ship: +4 shields on every arc this turn.", Target = TargetKind.AllyCapital,
            Effects = new Effect[] { new ShieldAllArcsEffect { Amount = 4 } },
        };

        public static readonly CardDef EscortGuns = new CardDef
        {
            Id = "escort_guns", Name = "Escort Guns", Cost = 1, Type = CardType.Weapon, Tags = "Weapon", Module = "Fore guns",
            Text = "5 dmg, escort's fore arc, range 3, +2 Heat.", Target = TargetKind.Enemy,
            Effects = new Effect[] { new AttackEffect { Damage = 5, Arcs = Arc.Fore, Range = 3, Heat = 2 } },
        };

        public static readonly CardDef LaunchDarts = new CardDef
        {
            Id = "launch_darts", Name = "Launch Darts", Cost = 1, Type = CardType.Hangar, Tags = "Hangar · Swarm", Module = "Hangar",
            Text = "Launch 2 darts next to the flagship (max 4 out); they act this turn.",
            Effects = new Effect[] { new LaunchDartsEffect { Count = 2, MaxOut = 4 } },
        };

        public static readonly CardDef VentPlasma = new CardDef
        {
            Id = "vent_plasma", Name = "Vent Plasma", Cost = 0, Type = CardType.Reactor, Tags = "Reactor · Heat", Module = "Reactor",
            Text = "Remove 4 Heat from one ship. Exhaust.", Target = TargetKind.AllyCapital, Exhaust = true,
            Effects = new Effect[] { new RemoveHeatEffect { Amount = 4 } },
        };

        // ---- reward pool ----

        public static readonly CardDef OverchargeLances = new CardDef
        {
            Id = "overcharge_lances", Name = "Overcharge Lances", Cost = 1, Type = CardType.Reactor, Tags = "Reactor · Lance", Module = "Lance",
            Text = "Next Lance Volley this turn +5, +3 Heat.",
            Effects = new Effect[] { new LanceBonusEffect { Bonus = 5, Heat = 3 } },
        };

        public static readonly CardDef DecimationOrder = new CardDef
        {
            Id = "decimation_order", Name = "Decimation Order", Cost = 0, Type = CardType.Order, Tags = "Order · Edict", Module = "Bridge",
            Text = "Expend 5 Ordinals: +2 command, draw 3. Exhaust.", ExpendCost = 5, Exhaust = true,
            Effects = new Effect[] { new GainCommandEffect { Amount = 2 }, new DrawEffect { Count = 3 } },
        };

        public static readonly CardDef SacrificialRam = new CardDef
        {
            Id = "sacrificial_ram", Name = "Sacrificial Ram", Cost = 2, Type = CardType.Maneuver, Module = "Engines",
            Text = "Escort rams an enemy straight ahead within Speed + 2: the enemy is destroyed, and so is the escort. Exhaust.",
            Target = TargetKind.Enemy, Exhaust = true,
            Effects = new Effect[] { new RamEffect { ExtraRange = 2 } },
        };

        public static readonly CardDef BoardingAction = new CardDef
        {
            Id = "boarding_action", Name = "Boarding Action", Cost = 2, Type = CardType.Boarding, Module = "Hangar",
            Text = "Expend 4 Ordinals: an enemy within 2 hexes loses its intent.", Target = TargetKind.Enemy, ExpendCost = 4,
            Effects = new Effect[] { new RemoveIntentEffect { Range = 2 } },
        };

        public static readonly CardDef WaymindTargeting = new CardDef
        {
            Id = "waymind_targeting", Name = "Waymind Targeting", Cost = 1, Type = CardType.Sensor, Module = "Sensors",
            Text = "Next weapon ignores armor and crits on any hull damage.",
            Effects = new Effect[] { new WaymindEffect() },
        };

        public static readonly CardDef PrecisionEdict = new CardDef
        {
            Id = "precision_edict", Name = "Precision Edict", Cost = 1, Type = CardType.Order, Tags = "Order · Edict", Module = "Bridge",
            Text = "Mark an enemy; each weapon that hits it this turn deals +3.", Target = TargetKind.Enemy,
            Effects = new Effect[] { new MarkEffect { Bonus = 3 } },
        };

        public static readonly CardDef ColdDiscipline = new CardDef
        {
            Id = "cold_discipline", Name = "Cold Discipline", Cost = 1, Type = CardType.Reactor, Tags = "Reactor · Heat", Module = "Reactor",
            Text = "Remove 3 Heat from every ship, draw 1.",
            Effects = new Effect[] { new RemoveHeatEffect { Amount = 3, AllShips = true }, new DrawEffect { Count = 1 } },
        };

        public static readonly CardDef ScreeningManeuver = new CardDef
        {
            Id = "screening_maneuver", Name = "Screening Maneuver", Cost = 1, Type = CardType.Maneuver, Module = "Engines",
            Text = "Escort turns 60° and moves 1; takes the next attack aimed at an adjacent ally.",
            Effects = new Effect[] { new ScreenEffect() },
        };

        public static readonly CardDef DartSortie = new CardDef
        {
            Id = "dart_sortie", Name = "Dart Sortie", Cost = 1, Type = CardType.Hangar, Tags = "Hangar · Swarm", Module = "Hangar",
            Text = "Every dart moves +2 and attacks twice.",
            Effects = new Effect[] { new SortieEffect { BonusMove = 2, Attacks = 2 } },
        };

        public static readonly CardDef FlakLattice = new CardDef
        {
            Id = "flak_lattice", Name = "Flak Lattice", Cost = 1, Type = CardType.Defense, Module = "Point defense",
            Text = "Destroy up to 3 small craft or torpedoes adjacent to the escort.",
            Effects = new Effect[] { new FlakEffect { Count = 3 } },
        };

        // ---- junk ----

        public static readonly CardDef HullBreach = new CardDef
        {
            Id = "hull_breach", Name = "Hull Breach", Cost = 0, Type = CardType.Junk, Unplayable = true,
            Text = "Unplayable. In hand at end of turn: lose 2 Ordinals.", Junk = JunkTrigger.EndOfTurnLoseOrdinals,
        };

        public static readonly CardDef FireOnDeck = new CardDef
        {
            Id = "fire_on_deck", Name = "Fire on Deck", Cost = 1, Type = CardType.Junk, Exhaust = true,
            Text = "In hand at end of turn: +2 Heat to the flagship. Pay 1 to put it out (Exhaust).", Junk = JunkTrigger.EndOfTurnHeat,
        };

        public static readonly CardDef JammedMount = new CardDef
        {
            Id = "jammed_mount", Name = "Jammed Mount", Cost = 0, Type = CardType.Junk, Unplayable = true,
            Text = "When drawn, a weapon card in hand costs +1 this turn.", Junk = JunkTrigger.OnDrawJamWeapon,
        };

        /// <summary>The starter deck: (card, source) with source 0 = flagship, 1 = escort.</summary>
        public static readonly (CardDef card, int source)[] Starter =
        {
            (LanceVolley, 0), (LanceVolley, 0), (BroadsideBattery, 0), (BroadsideBattery, 0), (Burn, 0),
            (HardAbout, 0), (Brace, 0), (EscortGuns, 1), (LaunchDarts, 0), (VentPlasma, 0),
        };

        public static readonly (CardDef card, int source)[] Rewards =
        {
            (OverchargeLances, 0), (DecimationOrder, 0), (SacrificialRam, 1), (BoardingAction, 0), (WaymindTargeting, 0),
            (PrecisionEdict, 0), (ColdDiscipline, 0), (ScreeningManeuver, 1), (DartSortie, 0), (FlakLattice, 1),
        };

        public static IEnumerable<CardDef> All()
        {
            foreach (var (c, _) in Starter) yield return c;
            foreach (var (c, _) in Rewards) yield return c;
        }
    }

    /// <summary>The paper-prototype scenarios, built in code.</summary>
    public static class Scenarios
    {
        /// <summary>
        /// Scenario 1, Intercept at the Ring: flagship and escort on the left edge facing E at Speed 1;
        /// 2 frigates and a gunship wing of 3 on the right edge; two asteroids in the centre.
        /// Units: 1 flagship, 2 escort, 3-4 frigates, 5-7 gunships.
        /// </summary>
        public static Battle InterceptAtTheRing(ulong seed, IEnumerable<(CardDef card, int source)> extraCards = null)
        {
            var b = new Battle(seed);
            b.Board.Set(4, 2, Terrain.Asteroid);
            b.Board.Set(4, 4, Terrain.Asteroid);

            var flag = b.AddUnit(PaperUnits.LanceCruiser, Side.Player, 0, 2, HexDirection.E, 1, flagship: true);
            var escort = b.AddUnit(PaperUnits.Blade, Side.Player, 0, 4, HexDirection.E, 1);
            b.AddUnit(PaperUnits.Frigate, Side.Enemy, 8, 2, HexDirection.W, 2);
            b.AddUnit(PaperUnits.Frigate, Side.Enemy, 8, 4, HexDirection.W, 2);
            b.AddUnit(PaperUnits.Gunship, Side.Enemy, 8, 1, HexDirection.W, 0);
            b.AddUnit(PaperUnits.Gunship, Side.Enemy, 8, 3, HexDirection.W, 0);
            b.AddUnit(PaperUnits.Gunship, Side.Enemy, 8, 5, HexDirection.W, 0);

            AddDeck(b, PaperCards.Starter, flag, escort);
            if (extraCards != null) AddDeck(b, extraCards, flag, escort);
            return b;
        }

        /// <summary>Scenario 2, The Long Promise: the carrier and one frigate.</summary>
        public static Battle TheLongPromise(ulong seed, IEnumerable<(CardDef card, int source)> extraCards = null)
        {
            var b = new Battle(seed);
            b.Board.Set(4, 3, Terrain.Asteroid);
            b.Board.Set(5, 1, Terrain.Debris);

            var flag = b.AddUnit(PaperUnits.LanceCruiser, Side.Player, 0, 2, HexDirection.E, 1, flagship: true);
            var escort = b.AddUnit(PaperUnits.Blade, Side.Player, 0, 4, HexDirection.E, 1);
            b.AddUnit(PaperUnits.Carrier, Side.Enemy, 8, 3, HexDirection.W, 1);
            b.AddUnit(PaperUnits.Frigate, Side.Enemy, 7, 1, HexDirection.W, 2);

            AddDeck(b, PaperCards.Starter, flag, escort);
            if (extraCards != null) AddDeck(b, extraCards, flag, escort);
            return b;
        }

        static void AddDeck(Battle b, IEnumerable<(CardDef card, int source)> cards, Unit flag, Unit escort)
        {
            foreach (var (card, source) in cards) b.AddCard(card, source == 1 ? escort.Id : flag.Id);
        }
    }
}
