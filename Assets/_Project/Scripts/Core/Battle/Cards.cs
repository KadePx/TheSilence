using System.Collections.Generic;
using Silence.Core.Hex;

namespace Silence.Core.Battle
{
    public enum CardType { Weapon, Maneuver, Defense, Hangar, Reactor, Boarding, Order, Sensor, Junk }

    /// <summary>What the player has to pick when playing a card.</summary>
    public enum TargetKind
    {
        /// <summary>Nothing to pick.</summary>
        None,
        /// <summary>An enemy unit (weapons also check arc, range and line of fire).</summary>
        Enemy,
        /// <summary>One of the player's capital ships.</summary>
        AllyCapital,
    }

    /// <summary>What a junk card does on its own.</summary>
    public enum JunkTrigger
    {
        None,
        /// <summary>Fire on Deck: in hand at end of turn, +2 Heat to the flagship.</summary>
        EndOfTurnHeat,
        /// <summary>Hull Breach: in hand at end of turn, lose 2 Ordinals.</summary>
        EndOfTurnLoseOrdinals,
        /// <summary>Jammed Mount: when drawn, a weapon card in hand costs +1 this turn.</summary>
        OnDrawJamWeapon,
        /// <summary>Inquisitor Aboard: when drawn, lose 1 command this turn.</summary>
        OnDrawLoseCommand,
    }

    /// <summary>Static definition of a card: cost, targeting and a list of composable effect blocks.</summary>
    public sealed class CardDef
    {
        public string Id;
        public string Name;
        public int Cost;
        public CardType Type;
        public string Tags = "";
        /// <summary>Source module, for display ("Fore lance", "Hangar").</summary>
        public string Module = "";
        public string Text = "";
        public bool Exhaust;
        public bool Unplayable;
        public TargetKind Target;
        /// <summary>Expend X Ordinals (Ascendancy). Required unless <see cref="ExpendOptional"/>.</summary>
        public int ExpendCost;
        public bool ExpendOptional;
        public JunkTrigger Junk;
        public Effect[] Effects = new Effect[0];

        public bool IsJunk => Type == CardType.Junk;
        public override string ToString() => $"{Name} ({Cost})";
    }

    /// <summary>A card in this battle's deck, tied to the ship that provides it (or -1 for junk).</summary>
    public sealed class CardInstance
    {
        public int Id;
        public CardDef Def;
        public int SourceId;
        /// <summary>Temporary cost change for this turn (Jammed Mount).</summary>
        public int TempCost;

        public override string ToString() => $"[{Id}] {Def.Name}";
    }

    /// <summary>The player's choices when playing a card.</summary>
    public struct PlayTarget
    {
        public int UnitId;
        /// <summary>60 degree turn steps, positive is counter-clockwise (E to NE).</summary>
        public int Turn;
        /// <summary>Pay the optional Expend cost.</summary>
        public bool Expend;

        public static PlayTarget None => new PlayTarget { UnitId = -1 };
        public static PlayTarget At(int unitId, bool expend = false) => new PlayTarget { UnitId = unitId, Expend = expend };
        public static PlayTarget Turning(int unitId, int steps) => new PlayTarget { UnitId = unitId, Turn = steps };
    }

    /// <summary>Everything an effect needs while a card resolves.</summary>
    public sealed class PlayContext
    {
        public CardInstance Card;
        public Unit Source;
        public Unit Target;
        public PlayTarget Choice;
        public bool Expended;
    }

    public enum Pile { Draw, Hand, Discard, Exhausted }

    /// <summary>The shared command deck: draw pile, hand, discard and exhaust.</summary>
    public sealed class Deck
    {
        public readonly List<CardInstance> DrawPile = new List<CardInstance>();
        public readonly List<CardInstance> Hand = new List<CardInstance>();
        public readonly List<CardInstance> Discard = new List<CardInstance>();
        public readonly List<CardInstance> Exhausted = new List<CardInstance>();

        public IEnumerable<List<CardInstance>> AllPiles()
        {
            yield return DrawPile;
            yield return Hand;
            yield return Discard;
            yield return Exhausted;
        }

        public int Count
        {
            get
            {
                int n = 0;
                foreach (var p in AllPiles()) n += p.Count;
                return n;
            }
        }

        public CardInstance FindInHand(int id) => Hand.Find(c => c.Id == id);
    }
}
