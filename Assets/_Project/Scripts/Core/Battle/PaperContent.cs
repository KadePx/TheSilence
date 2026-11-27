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
        const Arc Sides = Arc.Port | Arc.Starboard;

        public static UnitSpec LanceCruiser => Named("asc_cruiser", UnitSpec.Capital("Sixth Verdict", 30, 3, 2, 2, 1, 4, 1, 10, 1, 3));
        public static UnitSpec Blade => Named("asc_blade", UnitSpec.Capital("Blade", 14, 1, 1, 1, 0, 2, 1, 6, 2, 3));
        public static UnitSpec Dart => Named("asc_dart", UnitSpec.Craft("Ordinal dart", 2, 4, 2));

        public static UnitSpec Frigate => Cycled("con_outrider",
            UnitSpec.Capital("Concord frigate", 18, 1, 1, 1, 1, 3, 1, 0, 0, 2, AiProfile.Frigate),
            IntentStep.Of(IntentKind.Maneuver), IntentStep.Fire("Broadside", 9, Sides, 3), IntentStep.Of(IntentKind.LaunchTorpedo));

        /// <summary>Tutorial target: a moored Concord picket that never moves and fires a light broadside every turn.</summary>
        public static UnitSpec Picket => Cycled("con_outrider",
            UnitSpec.Capital("Concord picket", 12, 1, 1, 1, 1, 2, 1, 0, 0, 0, AiProfile.Frigate),
            IntentStep.Fire("Broadside", 4, Sides, 3));

        public static UnitSpec Gunship => Named("con_gunship", UnitSpec.Craft("Gunship", 3, 4, 3, AiProfile.Gunship));

        public static UnitSpec Carrier => Cycled("con_trimaran",
            UnitSpec.Capital("Long Promise", 32, 2, 2, 2, 1, 4, 1, 0, 0, 1, AiProfile.Carrier),
            IntentStep.Of(IntentKind.LaunchCraft, 2), IntentStep.Of(IntentKind.PatchUp, 4), IntentStep.Of(IntentKind.JointFire));

        /// <summary>Concord escort: fast, fragile, fights with its fore guns.</summary>
        public static UnitSpec Skiff => Cycled("con_skiff",
            UnitSpec.Capital("Concord skiff", 9, 0, 0, 0, 0, 2, 1, 0, 0, 3, AiProfile.Frigate),
            IntentStep.Of(IntentKind.Maneuver), IntentStep.Fire("Fore guns", 5, Arc.Fore, 3));

        /// <summary>Concord support ship: repairs the line and adds its own light broadside.</summary>
        public static UnitSpec Tender => Cycled("con_tender",
            UnitSpec.Capital("Concord tender", 22, 1, 1, 1, 1, 4, 1, 0, 0, 1, AiProfile.Carrier),
            IntentStep.Of(IntentKind.PatchUp, 5), IntentStep.Fire("Broadside", 5, Sides, 3), IntentStep.Of(IntentKind.LaunchCraft, 1));

        /// <summary>Sector boss: Marshal Oru Venn's flagship. The Mass Driver is dodged by leaving its fore arc.</summary>
        public static UnitSpec VennFlagship => Cycled("con_trimaran",
            UnitSpec.Capital("Long Promise", 40, 2, 2, 2, 1, 4, 1, 0, 0, 1, AiProfile.Carrier),
            IntentStep.Of(IntentKind.LaunchCraft, 2), IntentStep.Fire("Mass Driver", 12, Arc.Fore, 6),
            IntentStep.Of(IntentKind.Maneuver), IntentStep.Of(IntentKind.JointFire), IntentStep.Of(IntentKind.PatchUp, 4));

        // ---- nemesis commanders (Concord). Each is hunted across a run and comes back changed. ----

        /// <summary>Captain Ilse Marrow's survey frigate: a long fore gun, and quick enough to keep you in front of it.</summary>
        public static UnitSpec QuietLedger => Cycled("con_quietledger",
            UnitSpec.Capital("Quiet Ledger", 20, 2, 1, 1, 0, 3, 1, 0, 0, 2, AiProfile.Frigate),
            IntentStep.Of(IntentKind.Maneuver), IntentStep.Fire("Survey Driver", 9, Arc.Fore, 5), IntentStep.Fire("Broadside", 6, Sides, 3));

        /// <summary>Wing-Speaker Tobiah Rask's flight tender: it fights through its gunships.</summary>
        public static UnitSpec OpenHand => Cycled("con_openhand",
            UnitSpec.Capital("Open Hand", 22, 1, 1, 1, 1, 3, 1, 0, 0, 1, AiProfile.Carrier),
            IntentStep.Of(IntentKind.LaunchCraft, 2), IntentStep.Of(IntentKind.JointFire), IntentStep.Fire("Broadside", 5, Sides, 3));

        /// <summary>Engineer-Prime Dessa Koyl's yard ship: thick plate, a short cutting beam, and it mends its escorts.</summary>
        public static UnitSpec MendersOath => Cycled("con_mendersoath",
            UnitSpec.Capital("Mender's Oath", 24, 3, 2, 2, 1, 3, 1, 0, 0, 1, AiProfile.Carrier),
            IntentStep.Fire("Cutting Beam", 8, Arc.Fore | Sides, 2), IntentStep.Of(IntentKind.PatchUp, 4), IntentStep.Fire("Broadside", 6, Sides, 3));

        /// <summary>Gate of the second sector on the Ascendancy's route: Fleet-Speaker Dray's dreadnought, with everything the Concord has left.</summary>
        public static UnitSpec CommonCause => Cycled("con_commoncause",
            UnitSpec.Capital("Common Cause", 38, 2, 2, 2, 1, 4, 1, 0, 0, 1, AiProfile.Carrier),
            IntentStep.Fire("Mass Driver", 12, Arc.Fore, 6), IntentStep.Of(IntentKind.LaunchCraft, 2), IntentStep.Fire("Broadside", 8, Sides, 3),
            IntentStep.Of(IntentKind.JointFire), IntentStep.Of(IntentKind.PatchUp, 4), IntentStep.Of(IntentKind.Maneuver));

        // ---- the Kessane Drift: what is left of the Concord's line, and it has had eleven years to think ----

        /// <summary>A line frigate: no torpedoes, two broadsides in three turns.</summary>
        public static UnitSpec LineFrigate => Cycled("con_linefrigate",
            UnitSpec.Capital("Line frigate", 20, 2, 1, 1, 1, 3, 1, 0, 0, 2, AiProfile.Frigate),
            IntentStep.Of(IntentKind.Maneuver), IntentStep.Fire("Broadside", 9, Sides, 3), IntentStep.Fire("Broadside", 9, Sides, 3));

        /// <summary>A torpedo skiff: it launches, runs, and comes back to launch again.</summary>
        public static UnitSpec TorpedoSkiff => Cycled("con_torpskiff",
            UnitSpec.Capital("Torpedo skiff", 9, 0, 0, 0, 0, 2, 1, 0, 0, 3, AiProfile.Frigate),
            IntentStep.Of(IntentKind.LaunchTorpedo), IntentStep.Of(IntentKind.Maneuver), IntentStep.Fire("Fore guns", 5, Arc.Fore, 3));

        public static UnitSpec Torpedo
        {
            get
            {
                var s = UnitSpec.Craft("Torpedo", 1, 3, 9, AiProfile.Torpedo);
                s.Kind = UnitKind.Torpedo;
                s.Id = "con_torpedo";
                return s;
            }
        }

        static UnitSpec Named(string id, UnitSpec s)
        {
            s.Id = id;
            return s;
        }

        static UnitSpec Cycled(string id, UnitSpec s, params IntentStep[] cycle)
        {
            s.Id = id;
            s.Cycle = cycle;
            return s;
        }
    }

    /// <summary>Unit templates by id, for encounters defined in data.</summary>
    public static class UnitLibrary
    {
        public static UnitSpec Get(string id)
        {
            switch (id)
            {
                case "flagship": return PaperUnits.LanceCruiser;
                case "escort": return PaperUnits.Blade;
                case "dart": return PaperUnits.Dart;
                case "frigate": return PaperUnits.Frigate;
                case "gunship": return PaperUnits.Gunship;
                case "carrier": return PaperUnits.Carrier;
                case "skiff": return PaperUnits.Skiff;
                case "tender": return PaperUnits.Tender;
                case "venn": return PaperUnits.VennFlagship;
                case "torpedo": return PaperUnits.Torpedo;
                case "common_cause": return PaperUnits.CommonCause;
                case "line_frigate": return PaperUnits.LineFrigate;
                case "torpedo_skiff": return PaperUnits.TorpedoSkiff;
                case "quiet_ledger": return PaperUnits.QuietLedger;
                case "open_hand": return PaperUnits.OpenHand;
                case "menders_oath": return PaperUnits.MendersOath;
                default: return ConcordUnits.Get(id) ?? throw new System.ArgumentException($"unknown unit '{id}'");
            }
        }
    }

    /// <summary>The paper-prototype cards (starter and reward), the alpha additions and the three junk cards, as effect blocks.</summary>
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
            Text = "One capital ship moves 1 hex straight ahead now. +1 Heat.", Target = TargetKind.AllyCapital,
            Effects = new Effect[] { new AdvanceEffect { Heat = 1 } },
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
            Text = "Launch 2 darts from the hangar next to the flagship (max 4 out); they act this turn.",
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
            Text = "Escort rams an enemy straight ahead within Speed + 2: twice its remaining hull as damage, ignoring shields and armor. The escort is lost. Exhaust.",
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
            Text = "Destroy up to 3 small craft or torpedoes adjacent to this ship.",
            Effects = new Effect[] { new FlakEffect { Count = 3 } },
        };

        // ---- alpha additions (escort helm, module and reward cards) ----

        public static readonly CardDef ComeAbout = new CardDef
        {
            Id = "come_about", Name = "Come About", Cost = 0, Type = CardType.Maneuver, Module = "Helm",
            Text = "This ship turns 60°; twice at Speed 0-1.",
            Effects = new Effect[] { new TurnEffect { OnSource = true } },
        };

        public static readonly CardDef ShortLance = new CardDef
        {
            Id = "short_lance", Name = "Short Lance", Cost = 1, Type = CardType.Weapon, Tags = "Weapon · Lance", Module = "Fore lance",
            Text = "5 dmg, fore arc, range 3, +2 Heat.", Target = TargetKind.Enemy,
            Effects = new Effect[] { new AttackEffect { Damage = 5, Arcs = Arc.Fore, Range = 3, Heat = 2, IsLance = true } },
        };

        public static readonly CardDef RakingBeam = new CardDef
        {
            Id = "raking_beam", Name = "Raking Beam", Cost = 2, Type = CardType.Weapon, Tags = "Weapon · Heat", Module = "Broadside",
            Text = "4 dmg to every enemy in the port or starboard arc within 4, +3 Heat.",
            Effects = new Effect[] { new SweepEffect { Damage = 4, Arcs = Arc.Port | Arc.Starboard, Range = 4, Heat = 3 } },
        };

        public static readonly CardDef PurgeTheirDecks = new CardDef
        {
            Id = "purge_their_decks", Name = "Purge Their Decks", Cost = 1, Type = CardType.Boarding, Module = "Boarding pods",
            Text = "Expend 2 Ordinals: 6 hull damage to an enemy within 2, ignoring shields and armor.",
            Target = TargetKind.Enemy, ExpendCost = 2,
            Effects = new Effect[] { new BoardingStrikeEffect { Damage = 6, Range = 2 } },
        };

        public static readonly CardDef HoldTheLine = new CardDef
        {
            Id = "hold_the_line", Name = "Hold the Line", Cost = 1, Type = CardType.Defense, Module = "Plating",
            Text = "Every ship: +3 shields on every arc this turn.",
            Effects = new Effect[] { new ShieldFleetEffect { Amount = 3 } },
        };

        public static readonly CardDef ScryTheVoid = new CardDef
        {
            Id = "scry_the_void", Name = "Scry the Void", Cost = 0, Type = CardType.Sensor, Module = "Sensors",
            Text = "Draw 2. Exhaust.", Exhaust = true,
            Effects = new Effect[] { new DrawEffect { Count = 2 } },
        };

        public static readonly CardDef SealTheDecks = new CardDef
        {
            Id = "seal_the_decks", Name = "Seal the Decks", Cost = 1, Type = CardType.Order, Tags = "Order · Repair", Module = "Bridge",
            Text = "Expend 2 Ordinals: repair 5 hull on one ship. Exhaust.", Target = TargetKind.AllyCapital, ExpendCost = 2, Exhaust = true,
            Effects = new Effect[] { new RepairEffect { Amount = 5 } },
        };

        // ---- junk ----

        // ---- heat as a resource (core review) ----

        public static readonly CardDef Redline = new CardDef
        {
            Id = "redline", Name = "Redline", Cost = 0, Type = CardType.Reactor, Tags = "Reactor · Heat", Module = "Reactor",
            Text = "+1 command. +4 Heat.",
            Effects = new Effect[] { new GainCommandEffect { Amount = 1 }, new HeatSourceEffect { Amount = 4 } },
        };

        public static readonly CardDef ThermalPurge = new CardDef
        {
            Id = "thermal_purge", Name = "Thermal Purge", Cost = 1, Type = CardType.Reactor, Tags = "Reactor · Heat", Module = "Reactor",
            Text = "Dump all Heat. An enemy within 2 takes that much damage (max 8), ignoring shields.",
            Target = TargetKind.Enemy,
            Effects = new Effect[] { new ThermalPurgeEffect { Range = 2, Max = 8 } },
        };

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

        public static readonly CardDef InquisitorAboard = new CardDef
        {
            Id = "inquisitor_aboard", Name = "Inquisitor Aboard", Cost = 0, Type = CardType.Junk, Unplayable = true,
            Text = "Unplayable. When drawn, lose 1 command this turn: every order is read twice.", Junk = JunkTrigger.OnDrawLoseCommand,
        };

        /// <summary>The starter deck: (card, source) with source 0 = flagship, 1 = escort.</summary>
        public static readonly (CardDef card, int source)[] Starter =
        {
            (LanceVolley, 0), (LanceVolley, 0), (BroadsideBattery, 0), (BroadsideBattery, 0), (Burn, 0),
            (HardAbout, 0), (HardAbout, 0), (Brace, 0), (EscortGuns, 1), (LaunchDarts, 0), (VentPlasma, 0), (ComeAbout, 1),
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

        /// <summary>Every card in the game, including junk.</summary>
        public static readonly CardDef[] Everything =
        {
            LanceVolley, BroadsideBattery, Burn, HardAbout, Brace, EscortGuns, LaunchDarts, VentPlasma,
            OverchargeLances, DecimationOrder, SacrificialRam, BoardingAction, WaymindTargeting, PrecisionEdict,
            ColdDiscipline, ScreeningManeuver, DartSortie, FlakLattice,
            ComeAbout, ShortLance, RakingBeam, PurgeTheirDecks, HoldTheLine, ScryTheVoid, SealTheDecks,
            Redline, ThermalPurge,
            HullBreach, FireOnDeck, JammedMount, InquisitorAboard,
        };

        public static CardDef Get(string id)
        {
            foreach (var c in Everything)
                if (c.Id == id) return c;
            foreach (var c in ConcordCards.Everything)
                if (c.Id == id) return c;
            throw new System.ArgumentException($"unknown card '{id}'");
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
            var b = new Battle(seed) { Title = "Intercept at the Ring" };
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

        /// <summary>
        /// The tutorial: the flagship alone against one stationary Concord picket whose only order is a light
        /// broadside. The deck is not shuffled, so the first two hands are fixed: turn 1 teaches orders, drift,
        /// the lance and Brace; turn 2 teaches turning, side arcs and darts; after that the discard reshuffles
        /// and the player finishes the picket unaided. Units: 1 flagship, 2 picket.
        /// </summary>
        public static Battle Tutorial(ulong seed = 1)
        {
            var b = new Battle(seed) { Title = "Tutorial: first contact", ShuffleAtStart = false, DartsInHangar = 4 };
            var flag = b.AddUnit(PaperUnits.LanceCruiser, Side.Player, 1, 3, HexDirection.E, 1, flagship: true);
            b.AddUnit(PaperUnits.Picket, Side.Enemy, 5, 3, HexDirection.NW, 0);
            foreach (var card in TutorialDeck) b.AddCard(card, flag.Id);
            return b;
        }

        /// <summary>Draw order of the tutorial deck: the first five cards are turn 1's hand, the next five turn 2's.</summary>
        public static readonly CardDef[] TutorialDeck =
        {
            PaperCards.LanceVolley, PaperCards.Brace, PaperCards.Burn, PaperCards.BroadsideBattery, PaperCards.HardAbout,
            PaperCards.HardAbout, PaperCards.BroadsideBattery, PaperCards.LaunchDarts, PaperCards.LanceVolley, PaperCards.VentPlasma,
        };

        /// <summary>Scenario 2, The Long Promise: the carrier and one frigate.</summary>
        public static Battle TheLongPromise(ulong seed, IEnumerable<(CardDef card, int source)> extraCards = null)
        {
            var b = new Battle(seed) { Title = "The Long Promise" };
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

        /// <summary>
        /// Carry damage, junk, Ordinals and dart losses from a finished battle into one that has not started:
        /// hull by ship name, junk cards into the deck, a lost ship stays lost (with its cards).
        /// </summary>
        public static void CarryOver(Battle from, Battle to)
        {
            to.Ordinals = from.Ordinals;
            if (from.DartsInHangar >= 0) to.DartsInHangar = from.DartsInHangar + from.PlayerCraftCount();
            foreach (var u in to.Units.FindAll(x => x.Side == Side.Player && x.IsCapital))
            {
                var old = from.Units.Find(x => x.Side == Side.Player && x.IsCapital && x.Name == u.Name);
                if (old == null) continue;
                if (!old.Alive)
                {
                    u.Alive = false;
                    u.Hull = 0;
                    foreach (var pile in to.Deck.AllPiles()) pile.RemoveAll(c => c.SourceId == u.Id);
                }
                else u.Hull = System.Math.Min(u.MaxHull, old.Hull);
            }
            foreach (var pile in from.Deck.AllPiles())
                foreach (var c in pile)
                    if (c.Def.IsJunk) to.AddCard(c.Def, -1);
        }

        static void AddDeck(Battle b, IEnumerable<(CardDef card, int source)> cards, Unit flag, Unit escort)
        {
            foreach (var (card, source) in cards) b.AddCard(card, source == 1 ? escort.Id : flag.Id);
        }
    }
}
