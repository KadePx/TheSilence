using System;
using System.Collections.Generic;
using Silence.Core.Battle;
using Silence.Core.Random;

namespace Silence.Core.Open
{
    public enum OpenSide { Player, Enemy }
    public enum OpenOutcome { Ongoing, Victory, Defeat }

    /// <summary>Why a shot can or cannot be taken.</summary>
    public enum Shot { Clear, OutOfArc, OutOfRange, Blocked }

    public enum OpenKind { Burn, Brake, Turn, Fire, Shields, Vent }

    /// <summary>What stopped a ship that was moving.</summary>
    public enum Stop { None, Edge, Wreck, Ship }

    /// <summary>A gun: which arcs it bears on, how far it reaches (edge of the target's base), what it does.</summary>
    public sealed class OpenWeapon
    {
        public string Name;
        public bool Fore, Sides, Aft;
        public int Range;
        public int Damage;

        public bool Bears(OpenArc arc)
            => arc == OpenArc.Fore ? Fore : arc == OpenArc.Aft ? Aft : Sides;

        public string ArcText => Fore && Sides ? "fore or side arcs" : Fore ? "fore arc" : Sides ? "port or starboard arc" : "aft arc";
    }

    public sealed class OpenShip
    {
        public int Id;
        public string Name;
        /// <summary>Sprite key: the view draws "iso_[Art]_[direction]".</summary>
        public string Art;
        public OpenSide Side;
        public bool Flagship;
        public Vec Pos;
        public int Heading;
        public int Speed, MaxSpeed;
        /// <summary>Size of the base, in thousandths of a span. Two bases never overlap.</summary>
        public int Radius;
        public int Hull, MaxHull;
        public int Shields, MaxShields;
        /// <summary>Armor by the arc the shot comes in on: fore, starboard, aft, port.</summary>
        public int[] Armor = new int[4];
        public int Heat, HeatCap, Vent;
        /// <summary>How far the helm can turn the ship in one turn, in degrees (a multiple of 45). One point (45) less at top speed.</summary>
        public int Agility;
        /// <summary>Player ships: heading and speed when the turn began. The helm is measured from these, so turning back is free.</summary>
        public int StartHeading, StartSpeed;
        /// <summary>Extra turn this turn, from Hard About or Come About.</summary>
        public int TurnBonus;
        /// <summary>Enemy ships only: what they shoot with, and whether they want the target on their beam.</summary>
        public List<OpenWeapon> Weapons = new List<OpenWeapon>();
        public bool Broadsider;
        public bool Alive => Hull > 0;
    }

    /// <summary>Something that drifts on the field, blocks fire and wrecks whatever runs into it.</summary>
    public sealed class OpenRock
    {
        public string Name;
        public string Art;
        public Vec Pos;
        public int Radius;
    }

    public sealed class OpenCard
    {
        public int Id;
        /// <summary>Id of the card in the main game whose picture this borrows.</summary>
        public string Art;
        public string Name, Text;
        public int Cost, Heat, Amount;
        public OpenKind Kind;
        public CardType Type;
        public OpenWeapon Weapon;
        public int SourceId;
    }

    public enum OpenEventKind { TurnStarted, Moved, Turned, Speed, Fired, Missed, Destroyed, Collision, Shields, Vented, Boost, Over }

    public struct OpenEvent
    {
        public OpenEventKind Kind;
        public int ShipId, TargetId;
        public Vec From, To;
        public int Amount, Absorbed, Armored;
        public OpenArc Arc;
        public Shot Shot;
        public string Text;
    }

    /// <summary>What the player picks when playing a card: a target ship, or nothing.</summary>
    public struct OpenChoice
    {
        public int TargetId;
        public static OpenChoice None => new OpenChoice { TargetId = -1 };
        public static OpenChoice At(int targetId) => new OpenChoice { TargetId = targetId };
    }

    /// <summary>
    /// Prototype of battle without a grid. Ships stand on round bases anywhere on the plane, face one of 8
    /// headings and keep their speed: at the end of its side's turn every ship drifts Speed spans straight
    /// ahead. Guns need the target in arc, in range and in sight; wrecks and rocks block sight.
    /// Round: the player sets each ship's helm (free, within its limits) and plays cards, then the player's
    /// ships drift; each enemy fires from where it sits (the position the player saw all turn), then turns,
    /// changes speed and drifts.
    /// </summary>
    public sealed class OpenBattle
    {
        /// <summary>Half the size of the field. Nothing may leave it.</summary>
        public int HalfWidth = 20000, HalfHeight = 12000;
        public const int HandSize = 5;
        const int StepSize = 100;

        public readonly Pcg32 Rng;
        public readonly List<OpenShip> Ships = new List<OpenShip>();
        public readonly List<OpenRock> Rocks = new List<OpenRock>();
        public readonly List<OpenCard> DrawPile = new List<OpenCard>(), Hand = new List<OpenCard>(), Discard = new List<OpenCard>();
        public readonly List<OpenEvent> Log = new List<OpenEvent>();
        public int Turn, Command, MaxCommand = 4;
        public OpenOutcome Outcome = OpenOutcome.Ongoing;
        int nextCard;

        public OpenBattle(ulong seed)
        {
            Rng = new Pcg32(seed);
        }

        // ------------------------------------------------------------ setup

        public OpenShip Add(OpenShip ship, int x, int y, int heading, int speed)
        {
            ship.Id = Ships.Count;
            ship.Pos = new Vec(x, y);
            ship.Heading = OpenMath.Norm(heading);
            ship.Speed = speed;
            Ships.Add(ship);
            return ship;
        }

        public OpenCard AddCard(OpenCard card, OpenShip source)
        {
            card.Id = nextCard++;
            card.SourceId = source.Id;
            DrawPile.Add(card);
            return card;
        }

        public void Start()
        {
            Rng.Shuffle(DrawPile);
            BeginPlayerTurn();
        }

        public OpenShip Get(int id) => id >= 0 && id < Ships.Count ? Ships[id] : null;

        public IEnumerable<OpenShip> Live(OpenSide side)
        {
            foreach (var s in Ships)
                if (s.Alive && s.Side == side) yield return s;
        }

        // ------------------------------------------------------------ questions the view and the AI ask

        /// <summary>Can this ship hit that one with this gun, and if not, why not.</summary>
        public Shot Check(OpenShip from, OpenWeapon weapon, OpenShip target) => Check(from.Pos, from.Heading, weapon, target);

        public Shot Check(Vec pos, int heading, OpenWeapon weapon, OpenShip target)
        {
            if (!weapon.Bears(OpenMath.ArcOf(pos, heading, target.Pos))) return Shot.OutOfArc;
            if (OpenMath.Dist(pos, target.Pos) - target.Radius > weapon.Range) return Shot.OutOfRange;
            return Sight(pos, target.Pos) == null ? Shot.Clear : Shot.Blocked;
        }

        /// <summary>The rock between two points, or null when the line is clear.</summary>
        public OpenRock Sight(Vec from, Vec to)
        {
            foreach (var r in Rocks)
                if (OpenMath.SegmentHits(from, to, r.Pos, r.Radius)) return r;
            return null;
        }

        /// <summary>Where a ship ends up moving a distance along a heading: it stops short of anything in the way.</summary>
        public Vec Trace(OpenShip ship, int heading, int distance, out Stop stop)
        {
            stop = Stop.None;
            var at = ship.Pos;
            for (int d = StepSize; d <= distance; d += StepSize)
            {
                var next = OpenMath.Step(ship.Pos, heading, d);
                stop = Blocked(ship, next);
                if (stop != Stop.None) return at;
                at = next;
            }
            return at;
        }

        Stop Blocked(OpenShip ship, Vec at)
        {
            if (Math.Abs(at.X) + ship.Radius > HalfWidth || Math.Abs(at.Y) + ship.Radius > HalfHeight) return Stop.Edge;
            foreach (var r in Rocks)
                if (OpenMath.Dist(at, r.Pos) < ship.Radius + r.Radius) return Stop.Wreck;
            foreach (var o in Ships)
                if (o != ship && o.Alive && OpenMath.Dist(at, o.Pos) < ship.Radius + o.Radius) return Stop.Ship;
            return Stop.None;
        }

        /// <summary>Where the ship will be after it drifts at the end of its side's turn.</summary>
        public Vec DriftEnd(OpenShip ship, out Stop stop) => Trace(ship, ship.Heading, ship.Speed * OpenMath.Span, out stop);

        /// <summary>
        /// How far from its starting heading the helm may turn a ship at a given speed: one point less at top
        /// speed. A ship that began the turn stopped (and stays stopped) pivots in place to any heading.
        /// </summary>
        public int HelmTurn(OpenShip ship, int speed)
            => ship.StartSpeed == 0 && speed == 0 ? 180
            : Math.Max(0, ship.Agility - (speed >= ship.MaxSpeed ? OpenMath.HeadingStep : 0)) + ship.TurnBonus;

        /// <summary>Slowest and fastest the helm may set this turn: one step either way from the starting speed.</summary>
        public int HelmMin(OpenShip ship) => Math.Max(0, ship.StartSpeed - 1);
        public int HelmMax(OpenShip ship) => Math.Min(ship.MaxSpeed, ship.StartSpeed + 1);

        /// <summary>
        /// Set a player ship's heading and speed for this turn. Free, any number of times before the turn ends,
        /// within <see cref="HelmTurn"/> of the heading it began the turn on and one speed step of its starting speed.
        /// Returns null on success, otherwise why not (nothing changes).
        /// </summary>
        public string Helm(int shipId, int heading, int speed)
        {
            var ship = Get(shipId);
            if (Outcome != OpenOutcome.Ongoing) return "The battle is over.";
            if (ship == null || !ship.Alive || ship.Side != OpenSide.Player) return "Not your ship.";
            heading = OpenMath.Norm(heading);
            if (heading % OpenMath.HeadingStep != 0) return "Ships face one of eight headings.";
            if (speed < HelmMin(ship) || speed > HelmMax(ship))
                return $"{ship.Name} can only go from Speed {HelmMin(ship)} to {HelmMax(ship)} this turn.";
            int limit = HelmTurn(ship, speed);
            if (Math.Abs(OpenMath.Diff(ship.StartHeading, heading)) > limit)
                return limit == 0 ? $"{ship.Name} cannot turn at top speed." : $"{ship.Name} can turn {limit} deg this turn.";
            SetHeading(ship, heading);
            SetSpeed(ship, speed);
            return null;
        }

        /// <summary>Null when the card can be played, otherwise the reason in plain words.</summary>
        public string WhyNot(OpenCard card)
        {
            if (Outcome != OpenOutcome.Ongoing) return "The battle is over.";
            if (!Hand.Contains(card)) return "Not in hand.";
            if (card.Cost > Command) return "Not enough command.";
            var src = Get(card.SourceId);
            if (src == null || !src.Alive) return "That ship is gone.";
            if (card.Heat > 0 && src.Heat + card.Heat > src.HeatCap) return src.Name + " is too hot. Vent first.";
            if (card.Kind == OpenKind.Burn && src.NoRoomAhead(this)) return "No room ahead.";
            return null;
        }

        // ------------------------------------------------------------ actions

        /// <summary>Play a card from the hand. Returns null on success, otherwise why it was refused (nothing changes).</summary>
        public string Play(int cardId, OpenChoice choice)
        {
            var card = Hand.Find(c => c.Id == cardId);
            if (card == null) return "Not in hand.";
            string why = WhyNot(card);
            if (why != null) return why;
            var src = Get(card.SourceId);

            switch (card.Kind)
            {
                case OpenKind.Fire:
                    var target = Get(choice.TargetId);
                    if (target == null || !target.Alive || target.Side == src.Side) return "Pick an enemy ship.";
                    var shot = Check(src, card.Weapon, target);
                    if (shot == Shot.OutOfArc) return "Not in arc.";
                    if (shot == Shot.OutOfRange) return "Out of range.";
                    if (shot == Shot.Blocked) return "No line of sight.";
                    Pay(card, src);
                    Hit(src, card.Weapon, target);
                    break;
                case OpenKind.Turn:
                    Pay(card, src);
                    src.TurnBonus += card.Amount;
                    Log.Add(new OpenEvent { Kind = OpenEventKind.Boost, ShipId = src.Id, TargetId = -1, Amount = card.Amount, Text = $"+{card.Amount} DEG HELM" });
                    break;
                case OpenKind.Burn:
                    // Past the helm's step: the band of speeds this turn moves up with it.
                    Pay(card, src);
                    src.StartSpeed = Math.Min(src.MaxSpeed, src.StartSpeed + 1);
                    SetSpeed(src, Math.Min(src.MaxSpeed, src.Speed + 1));
                    Move(src, card.Amount * OpenMath.Span);
                    break;
                case OpenKind.Brake:
                    Pay(card, src);
                    src.StartSpeed = Math.Max(0, src.StartSpeed - card.Amount);
                    SetSpeed(src, Math.Max(0, src.Speed - card.Amount));
                    break;
                case OpenKind.Shields:
                    Pay(card, src);
                    src.Shields += card.Amount;
                    Log.Add(new OpenEvent { Kind = OpenEventKind.Shields, ShipId = src.Id, Amount = card.Amount });
                    break;
                case OpenKind.Vent:
                    Pay(card, src);
                    int vented = Math.Min(src.Heat, card.Amount);
                    src.Heat -= vented;
                    Log.Add(new OpenEvent { Kind = OpenEventKind.Vented, ShipId = src.Id, Amount = vented });
                    break;
            }
            CheckOver();
            return null;
        }

        void Pay(OpenCard card, OpenShip src)
        {
            Command -= card.Cost;
            src.Heat += card.Heat;
            Hand.Remove(card);
            Discard.Add(card);
        }

        /// <summary>End the player's turn: their ships drift, the enemy fires, manoeuvres and drifts, and a new hand is drawn.</summary>
        public void EndTurn()
        {
            if (Outcome != OpenOutcome.Ongoing) return;
            Discard.AddRange(Hand);
            Hand.Clear();
            foreach (var s in Ships)
                if (s.Alive && s.Side == OpenSide.Player) Move(s, s.Speed * OpenMath.Span);
            if (CheckOver()) return;

            foreach (var s in Ships)
            {
                if (!s.Alive || s.Side != OpenSide.Enemy) continue;
                if (s.Shields < s.MaxShields) s.Shields++;
                OpenAi.Fire(this, s);
                if (CheckOver()) return;
            }
            foreach (var s in Ships)
            {
                if (!s.Alive || s.Side != OpenSide.Enemy) continue;
                s.StartHeading = s.Heading;
                s.StartSpeed = s.Speed;
                OpenAi.Manoeuvre(this, s);
                Move(s, s.Speed * OpenMath.Span);
            }
            if (CheckOver()) return;
            BeginPlayerTurn();
        }

        void BeginPlayerTurn()
        {
            Turn++;
            Command = MaxCommand;
            foreach (var s in Live(OpenSide.Player))
            {
                // Shields come back one a turn; a Ring Shield's extra does not last.
                if (s.Shields > s.MaxShields) s.Shields = s.MaxShields;
                else if (s.Shields < s.MaxShields) s.Shields++;
                s.Heat = Math.Max(0, s.Heat - s.Vent);
                s.StartHeading = s.Heading;
                s.StartSpeed = s.Speed;
                s.TurnBonus = 0;
            }
            Log.Add(new OpenEvent { Kind = OpenEventKind.TurnStarted, Amount = Turn, ShipId = -1, TargetId = -1 });
            while (Hand.Count < HandSize)
            {
                if (DrawPile.Count == 0)
                {
                    if (Discard.Count == 0) break;
                    DrawPile.AddRange(Discard);
                    Discard.Clear();
                    Rng.Shuffle(DrawPile);
                }
                Hand.Add(DrawPile[DrawPile.Count - 1]);
                DrawPile.RemoveAt(DrawPile.Count - 1);
            }
        }

        // ------------------------------------------------------------ what actions are made of (the AI uses these too)

        public void SetHeading(OpenShip ship, int heading)
        {
            heading = OpenMath.Norm(heading);
            if (heading == ship.Heading) return;
            Log.Add(new OpenEvent { Kind = OpenEventKind.Turned, ShipId = ship.Id, TargetId = -1, Amount = ship.Heading, Absorbed = heading });
            ship.Heading = heading;
        }

        public void SetSpeed(OpenShip ship, int speed)
        {
            if (speed == ship.Speed) return;
            ship.Speed = speed;
            Log.Add(new OpenEvent { Kind = OpenEventKind.Speed, ShipId = ship.Id, TargetId = -1, Amount = speed });
        }

        /// <summary>Move straight ahead. Running into a wreck costs hull; anything in the way kills the ship's speed.</summary>
        public void Move(OpenShip ship, int distance)
        {
            if (distance <= 0 || !ship.Alive) return;
            var from = ship.Pos;
            var to = Trace(ship, ship.Heading, distance, out var stop);
            ship.Pos = to;
            if (to != from) Log.Add(new OpenEvent { Kind = OpenEventKind.Moved, ShipId = ship.Id, TargetId = -1, From = from, To = to });
            if (stop == Stop.None) return;
            ship.Speed = 0;
            int damage = stop == Stop.Wreck ? 3 : stop == Stop.Ship ? 1 : 0;
            ship.Hull = Math.Max(0, ship.Hull - damage);
            Log.Add(new OpenEvent { Kind = OpenEventKind.Collision, ShipId = ship.Id, TargetId = -1, Amount = damage, Text = stop == Stop.Edge ? "edge of the field" : stop == Stop.Wreck ? "wreckage" : "another ship" });
            if (!ship.Alive) Destroy(ship);
        }

        /// <summary>Resolve a hit: shields first, then the armor of the arc it comes in on, then hull.</summary>
        public int Hit(OpenShip from, OpenWeapon weapon, OpenShip target)
        {
            var arc = OpenMath.ArcOf(target.Pos, target.Heading, from.Pos);
            int absorbed = Math.Min(target.Shields, weapon.Damage);
            target.Shields -= absorbed;
            int rest = weapon.Damage - absorbed;
            int armored = Math.Min(rest, rest > 0 ? target.Armor[(int)arc] : 0);
            int hull = rest - armored;
            target.Hull = Math.Max(0, target.Hull - hull);
            Log.Add(new OpenEvent
            {
                Kind = OpenEventKind.Fired, ShipId = from.Id, TargetId = target.Id, From = from.Pos, To = target.Pos,
                Amount = hull, Absorbed = absorbed, Armored = armored, Arc = arc, Text = weapon.Name,
            });
            if (!target.Alive) Destroy(target);
            return hull;
        }

        void Destroy(OpenShip ship)
        {
            Log.Add(new OpenEvent { Kind = OpenEventKind.Destroyed, ShipId = ship.Id, TargetId = -1 });
            // A ship takes its cards with it.
            DrawPile.RemoveAll(c => c.SourceId == ship.Id);
            Hand.RemoveAll(c => c.SourceId == ship.Id);
            Discard.RemoveAll(c => c.SourceId == ship.Id);
        }

        bool CheckOver()
        {
            if (Outcome != OpenOutcome.Ongoing) return true;
            bool player = false, enemy = false;
            foreach (var s in Ships)
            {
                if (!s.Alive) continue;
                if (s.Side == OpenSide.Player) player = true;
                else enemy = true;
            }
            if (player && enemy) return false;
            Outcome = player ? OpenOutcome.Victory : OpenOutcome.Defeat;
            Log.Add(new OpenEvent { Kind = OpenEventKind.Over, ShipId = -1, TargetId = -1 });
            return true;
        }
    }

    static class OpenShipQuestions
    {
        public static bool NoRoomAhead(this OpenShip ship, OpenBattle battle)
        {
            var end = battle.Trace(ship, ship.Heading, OpenMath.Span, out _);
            return end == ship.Pos;
        }
    }
}
