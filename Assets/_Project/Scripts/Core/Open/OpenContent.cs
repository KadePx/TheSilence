using Silence.Core.Battle;
using Silence.Core.Random;

namespace Silence.Core.Open
{
    /// <summary>The one encounter of the gridless prototype: a Concord flagship and escort against a cruiser and two escorts, in a breaker's yard.</summary>
    public static class OpenContent
    {
        public static OpenShip Flagship() => new OpenShip
        {
            Name = "Long Promise", Art = "con_frigate", Side = OpenSide.Player, Flagship = true,
            MaxSpeed = 3, Radius = 1300, Hull = 30, MaxHull = 30, Shields = 4, MaxShields = 4,
            Armor = new[] { 2, 2, 1, 2 }, HeatCap = 5, Vent = 1, Agility = 90,
        };

        public static OpenShip Outrider() => new OpenShip
        {
            Name = "Outrider", Art = "con_skiff", Side = OpenSide.Player,
            MaxSpeed = 4, Radius = 800, Hull = 14, MaxHull = 14, Shields = 3, MaxShields = 3,
            Armor = new[] { 1, 1, 0, 1 }, HeatCap = 3, Vent = 1, Agility = 135,
        };

        public static OpenShip Cruiser() => new OpenShip
        {
            Name = "Verdict cruiser", Art = "asc_cruiser", Side = OpenSide.Enemy,
            MaxSpeed = 2, Radius = 1100, Hull = 22, MaxHull = 22, Shields = 4, MaxShields = 4,
            Armor = new[] { 3, 2, 0, 2 }, Agility = 90, Broadsider = true,
            Weapons =
            {
                new OpenWeapon { Name = "Broadside", Sides = true, Range = 5000, Damage = 5 },
                new OpenWeapon { Name = "Prow lance", Fore = true, Range = 7000, Damage = 6 },
            },
        };

        public static OpenShip Escort(string name) => new OpenShip
        {
            Name = name, Art = "asc_escort", Side = OpenSide.Enemy,
            MaxSpeed = 3, Radius = 650, Hull = 9, MaxHull = 9, Shields = 2, MaxShields = 2,
            Armor = new[] { 1, 1, 0, 1 }, Agility = 135,
            Weapons = { new OpenWeapon { Name = "Fore guns", Fore = true, Range = 4000, Damage = 3 } },
        };

        public static OpenCard Burn() => new OpenCard
        {
            Art = "burn", Name = "Burn", Kind = OpenKind.Burn, Type = CardType.Maneuver, Cost = 0, Heat = 1, Amount = 1,
            Text = "Speed +1 past the helm's limit, and move 1 span straight ahead now. +1 Heat.",
        };

        public static OpenCard Retro() => new OpenCard
        {
            Art = "screening_maneuver", Name = "Retro Burn", Kind = OpenKind.Brake, Type = CardType.Maneuver, Cost = 0, Amount = 2,
            Text = "Speed -2 past the helm's limit.",
        };

        public static OpenCard HardAbout() => new OpenCard
        {
            Art = "hard_about", Name = "Hard About", Kind = OpenKind.Turn, Type = CardType.Maneuver, Cost = 1, Amount = 90,
            Text = "The helm can turn this ship 90 deg further this turn.",
        };

        public static OpenCard ComeAbout() => new OpenCard
        {
            Art = "come_about", Name = "Come About", Kind = OpenKind.Turn, Type = CardType.Maneuver, Cost = 0, Amount = 45,
            Text = "The helm can turn this ship 45 deg further this turn.",
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

        /// <summary>The prototype fight, not yet started. The debris is scattered from the seed.</summary>
        public static OpenBattle BreakersYard(ulong seed)
        {
            var b = new OpenBattle(seed);
            var flag = b.Add(Flagship(), -11000, 500, 0, 1);
            var outrider = b.Add(Outrider(), -11500, 4500, 0, 2);
            b.Add(Cruiser(), 10500, -500, 180, 1);
            b.Add(Escort("Blade escort"), 11500, -4500, 180, 2);
            b.Add(Escort("Blade escort"), 11000, 5000, 180, 2);
            Scatter(b, seed);

            foreach (var c in new[] { Burn(), HardAbout(), DeckGuns(), DeckGuns(), DeckGuns(), MassDriver(), MassDriver(), RingShield(), RingShield(), VentPlasma() })
                b.AddCard(c, flag);
            foreach (var c in new[] { Burn(), ComeAbout(), OutriderGuns(), OutriderGuns(), OutriderGuns() })
                b.AddCard(c, outrider);
            return b;
        }

        /// <summary>
        /// Wrecks and rock across the field, from the seed on a stream of its own (so the deck shuffle is not
        /// disturbed). Each keeps clear of the ships' starting places and leaves a lane between itself and the next.
        /// </summary>
        public static void Scatter(OpenBattle b, ulong seed, int wrecks = 4, int rocks = 10)
        {
            var rng = new Pcg32(seed, 77);
            for (int i = 0; i < wrecks + rocks; i++)
            {
                bool wreck = i < wrecks;
                string art = wreck ? "iso_wreck" : i % 2 == 0 ? "iso_rock_a" : "iso_rock_b";
                int radius = wreck ? 1100 : art == "iso_rock_a" ? 800 : 850;
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    var p = new Vec(rng.Range(-b.HalfWidth + Lane + radius, b.HalfWidth - Lane - radius), rng.Range(-b.HalfHeight + Lane + radius, b.HalfHeight - Lane - radius));
                    if (!Clear(b, p, radius)) continue;
                    b.Rocks.Add(new OpenRock { Name = wreck ? "Wreck" : "Rocks", Art = art, Pos = p, Radius = radius });
                    break;
                }
            }
        }

        /// <summary>
        /// Room kept clear around the starts, and the narrowest gap left between two obstacles (or an obstacle and
        /// the edge): wider than the biggest base, with room to turn, so no ship can be caught in a pocket.
        /// </summary>
        public const int StartClearance = 2500, Lane = 4200;

        static bool Clear(OpenBattle b, Vec p, int radius)
        {
            foreach (var s in b.Ships)
                if (OpenMath.Dist(p, s.Pos) < s.Radius + radius + StartClearance) return false;
            foreach (var r in b.Rocks)
                if (OpenMath.Dist(p, r.Pos) < r.Radius + radius + Lane) return false;
            return true;
        }
    }
}
