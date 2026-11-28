using Silence.Core.Battle;

namespace Silence.Core.Open
{
    /// <summary>The one encounter of the gridless prototype: a Concord flagship and escort against a cruiser and two escorts, in a breaker's yard.</summary>
    public static class OpenContent
    {
        public static OpenShip Flagship() => new OpenShip
        {
            Name = "Long Promise", Art = "con_frigate", Side = OpenSide.Player, Flagship = true,
            MaxSpeed = 3, Radius = 1200, Hull = 30, MaxHull = 30, Shields = 4, MaxShields = 4,
            Armor = new[] { 2, 2, 1, 2 }, HeatCap = 5, Vent = 1,
        };

        public static OpenShip Outrider() => new OpenShip
        {
            Name = "Outrider", Art = "con_skiff", Side = OpenSide.Player,
            MaxSpeed = 4, Radius = 600, Hull = 14, MaxHull = 14, Shields = 3, MaxShields = 3,
            Armor = new[] { 1, 1, 0, 1 }, HeatCap = 3, Vent = 1,
        };

        public static OpenShip Cruiser() => new OpenShip
        {
            Name = "Verdict cruiser", Art = "asc_cruiser", Side = OpenSide.Enemy,
            MaxSpeed = 2, Radius = 1100, Hull = 22, MaxHull = 22, Shields = 4, MaxShields = 4,
            Armor = new[] { 3, 2, 0, 2 }, Turn = 30, Broadsider = true,
            Weapons =
            {
                new OpenWeapon { Name = "Broadside", Sides = true, Range = 5000, Damage = 5 },
                new OpenWeapon { Name = "Prow lance", Fore = true, Range = 7000, Damage = 6 },
            },
        };

        public static OpenShip Escort(string name) => new OpenShip
        {
            Name = name, Art = "asc_escort", Side = OpenSide.Enemy,
            MaxSpeed = 3, Radius = 600, Hull = 9, MaxHull = 9, Shields = 2, MaxShields = 2,
            Armor = new[] { 1, 1, 0, 1 }, Turn = 45,
            Weapons = { new OpenWeapon { Name = "Fore guns", Fore = true, Range = 4000, Damage = 3 } },
        };

        public static OpenCard Burn() => new OpenCard
        {
            Art = "burn", Name = "Burn", Kind = OpenKind.Burn, Type = CardType.Maneuver, Cost = 0, Heat = 1, Amount = 1,
            Text = "Speed +1 and move 1 span straight ahead now. +1 Heat.",
        };

        public static OpenCard Retro() => new OpenCard
        {
            Art = "screening_maneuver", Name = "Retro Burn", Kind = OpenKind.Brake, Type = CardType.Maneuver, Cost = 0, Amount = 2,
            Text = "Speed -2. The ship drifts less at the end of the turn.",
        };

        public static OpenCard HardAbout() => new OpenCard
        {
            Art = "hard_about", Name = "Hard About", Kind = OpenKind.Turn, Type = CardType.Maneuver, Cost = 1, Amount = 60,
            Text = "Turn up to 60 deg. Only 30 deg at Speed 3 or more.",
        };

        public static OpenCard ComeAbout() => new OpenCard
        {
            Art = "come_about", Name = "Come About", Kind = OpenKind.Turn, Type = CardType.Maneuver, Cost = 0, Amount = 90,
            Text = "Turn up to 90 deg. Only 30 deg at Speed 3 or more.",
        };

        public static OpenCard DeckGuns() => new OpenCard
        {
            Art = "deck_guns", Name = "Deck Guns", Kind = OpenKind.Fire, Type = CardType.Weapon, Cost = 1, Heat = 1,
            Weapon = new OpenWeapon { Name = "Deck Guns", Sides = true, Range = 5000, Damage = 5 },
            Text = "5 dmg, port or starboard arc, range 5, +1 Heat.",
        };

        public static OpenCard MassDriver() => new OpenCard
        {
            Art = "mass_driver", Name = "Mass Driver", Kind = OpenKind.Fire, Type = CardType.Weapon, Cost = 2, Heat = 2,
            Weapon = new OpenWeapon { Name = "Mass Driver", Fore = true, Range = 7000, Damage = 7 },
            Text = "7 dmg, fore arc, range 7, +2 Heat.",
        };

        public static OpenCard OutriderGuns() => new OpenCard
        {
            Art = "outrider_guns", Name = "Outrider Guns", Kind = OpenKind.Fire, Type = CardType.Weapon, Cost = 1, Heat = 1,
            Weapon = new OpenWeapon { Name = "Outrider Guns", Fore = true, Sides = true, Range = 4000, Damage = 3 },
            Text = "3 dmg, fore or side arcs, range 4, +1 Heat.",
        };

        public static OpenCard RingShield() => new OpenCard
        {
            Art = "ring_shield", Name = "Ring Shield", Kind = OpenKind.Shields, Type = CardType.Defense, Cost = 1, Amount = 3,
            Text = "+3 shields until your next turn.",
        };

        public static OpenCard VentPlasma() => new OpenCard
        {
            Art = "vent_plasma", Name = "Vent Plasma", Kind = OpenKind.Vent, Type = CardType.Reactor, Cost = 0, Amount = 3,
            Text = "Remove 3 Heat.",
        };

        /// <summary>The prototype fight, not yet started.</summary>
        public static OpenBattle BreakersYard(ulong seed)
        {
            var b = new OpenBattle(seed);
            var flag = b.Add(Flagship(), -6500, 600, 0, 1);
            var outrider = b.Add(Outrider(), -6900, 3400, 0, 2);
            b.Add(Cruiser(), 5600, -600, 180, 1);
            b.Add(Escort("Blade escort"), 6600, -3600, 180, 2);
            b.Add(Escort("Blade escort"), 6300, 3700, 195, 2);

            b.Rocks.Add(new OpenRock { Name = "Wreck", Art = "iso_wreck", Pos = new Vec(-200, -1300), Radius = 1100 });
            b.Rocks.Add(new OpenRock { Name = "Rocks", Art = "iso_rock_a", Pos = new Vec(-1700, 3500), Radius = 800 });
            b.Rocks.Add(new OpenRock { Name = "Rocks", Art = "iso_rock_b", Pos = new Vec(2700, 2500), Radius = 800 });
            b.Rocks.Add(new OpenRock { Name = "Rocks", Art = "iso_rock_a", Pos = new Vec(-3400, -4000), Radius = 700 });
            b.Rocks.Add(new OpenRock { Name = "Rocks", Art = "iso_rock_b", Pos = new Vec(3300, -4300), Radius = 700 });

            foreach (var c in new[] { Burn(), Burn(), Retro(), HardAbout(), HardAbout(), DeckGuns(), DeckGuns(), MassDriver(), RingShield(), VentPlasma() })
                b.AddCard(c, flag);
            foreach (var c in new[] { Burn(), ComeAbout(), OutriderGuns(), OutriderGuns() })
                b.AddCard(c, outrider);
            return b;
        }
    }
}
