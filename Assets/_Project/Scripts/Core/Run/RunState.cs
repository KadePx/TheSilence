using System;
using System.Collections.Generic;

namespace Silence.Core.Run
{
    public enum RunPhase { Map = 0, Battle = 1, Reward = 2, Event = 3, EventResult = 4, Shipyard = 5, Over = 6 }

    public enum RunResult { Ongoing = 0, Victory = 1, Defeat = 2, Outpaced = 3 }

    public enum NodeKind { Battle = 0, Elite = 1, Event = 2, Shipyard = 3, Boss = 4 }

    /// <summary>A capital ship between battles. Index 0 in <see cref="RunState.Ships"/> is the flagship, 1 the escort.</summary>
    [Serializable]
    public sealed class ShipState
    {
        public string Unit = "";
        public string Name = "";
        public int Hull;
        public int MaxHull;
        public bool Alive = true;
    }

    /// <summary>A card in the run deck: the card id and the ship that provides it (-1 for junk).</summary>
    [Serializable]
    public struct DeckCard
    {
        public string Card;
        public int Ship;

        public DeckCard(string card, int ship)
        {
            Card = card;
            Ship = ship;
        }
    }

    [Serializable]
    public sealed class MapNode
    {
        public int Id;
        public int Layer;
        /// <summary>Vertical position on the map, 0 (top) to 3.</summary>
        public int Lane;
        public NodeKind Kind;
        /// <summary>Encounter id for battle, elite and boss nodes.</summary>
        public string Encounter = "";
        /// <summary>Event id for event nodes.</summary>
        public string Event = "";
        public List<int> Next = new List<int>();
        public bool Visited;
    }

    /// <summary>What a won battle offers: scrap (already banked) and one card out of a few.</summary>
    [Serializable]
    public sealed class RewardOffer
    {
        public bool Active;
        public int Scrap;
        public int Ordinals;
        public List<DeckCard> Cards = new List<DeckCard>();
        public string Note = "";
    }

    /// <summary>What a shipyard has on offer this visit.</summary>
    [Serializable]
    public sealed class ShopState
    {
        public bool Active;
        public List<DeckCard> Cards = new List<DeckCard>();
        public List<int> CardPrices = new List<int>();
        /// <summary>Module ids offered as a refit, at most one per slot.</summary>
        public List<string> Modules = new List<string>();
        public int Purchases;
    }

    /// <summary>
    /// Everything about a run that is saved at each map node: seed and RNG position, fleet, deck, map,
    /// race positions and whatever is pending (a reward to take, an event to answer, a shipyard visit).
    /// Plain serializable data; the rules that change it are in <see cref="RunRules"/>.
    /// </summary>
    [Serializable]
    public sealed class RunState
    {
        public int Version = 1;
        public long Seed;
        public long RngState;
        public long RngIncrement;

        public RunPhase Phase;
        public RunResult Result;

        public int Scrap;
        public int Ordinals;
        public int MaxOrdinals;
        /// <summary>Darts aboard the flagship. Losses carry over; shipyards sell replacements.</summary>
        public int Darts;
        public int MaxDarts;

        public List<ShipState> Ships = new List<ShipState>();
        /// <summary>The flagship's fitted module ids, one per slot in slot order.</summary>
        public List<string> Modules = new List<string>();
        public List<DeckCard> Deck = new List<DeckCard>();

        public List<MapNode> Nodes = new List<MapNode>();
        public int CurrentNode = -1;
        public int BossLayer;

        /// <summary>Race positions in tenths of a node. The player's is 10 per node travelled.</summary>
        public int PlayerProgress;
        public int ConcordProgress;
        public int ChorusProgress;
        /// <summary>Set by events: the sector boss brings an extra escort.</summary>
        public bool VennEnraged;

        public RewardOffer Reward = new RewardOffer();
        public ShopState Shop = new ShopState();
        public string EventId = "";
        public string EventResult = "";
        /// <summary>Encounter to fight when <see cref="Phase"/> is Battle.</summary>
        public string PendingEncounter = "";
        /// <summary>Scrap multiplier in percent for the pending battle (events can raise it).</summary>
        public int PendingRewardPercent = 100;
        public int BattleNumber;

        public int BattlesWon;
        public int TurnsFought;
        public int NodesVisited;
        public List<string> Journal = new List<string>();

        public ShipState Flagship => Ships.Count > 0 ? Ships[0] : null;
        public ShipState Escort => Ships.Count > 1 ? Ships[1] : null;
        public MapNode Node(int id) => id >= 0 && id < Nodes.Count ? Nodes[id] : null;
        public MapNode Current => Node(CurrentNode);

        public int JunkCount()
        {
            int n = 0;
            foreach (var c in Deck)
                if (c.Ship < 0) n++;
            return n;
        }
    }
}
