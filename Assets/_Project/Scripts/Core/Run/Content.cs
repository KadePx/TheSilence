using System;
using System.Collections.Generic;
using Silence.Core.Hex;

namespace Silence.Core.Run
{
    /// <summary>One enemy placed on the board at the start of an encounter.</summary>
    public sealed class EncounterUnit
    {
        public string Unit;
        public int Col, Row;
        public HexDirection Facing = HexDirection.W;
        public int Speed;
    }

    /// <summary>A fight: who is there, the terrain, and what it pays.</summary>
    public sealed class EncounterDef
    {
        public string Id;
        public string Name;
        /// <summary>0 opening, 1 standard, 2 elite, 3 boss, 4 only from events.</summary>
        public int Tier;
        public string Intro = "";
        public List<EncounterUnit> Units = new List<EncounterUnit>();
        /// <summary>Fixed terrain as (col, row, asteroid?) triples.</summary>
        public List<(int col, int row, bool asteroid)> Terrain = new List<(int, int, bool)>();
        /// <summary>Extra asteroids and debris scattered in the middle columns from the battle seed.</summary>
        public int ScatterAsteroids, ScatterDebris;
        public int Scrap;
        public int TurnLimit;
        /// <summary>Units added when the Concord fleet reached the gate first or Venn is enraged.</summary>
        public List<EncounterUnit> Reinforcements = new List<EncounterUnit>();
    }

    public enum OutcomeKind
    {
        Scrap, Ordinals, HullFlagship, HullAll, MaxHullFlagship, Darts,
        AddCard, AddJunk, RemoveJunk, RivalsAdvance, EnrageBoss, StartBattle,
    }

    public sealed class EventOutcome
    {
        public OutcomeKind Kind;
        public int Amount;
        /// <summary>Card, junk or encounter id.</summary>
        public string Id = "";
        /// <summary>Ship a gained card belongs to: 0 flagship, 1 escort.</summary>
        public int Ship;
    }

    public sealed class EventChoice
    {
        public string Label = "";
        /// <summary>Short tag shown beside the label ("Expend 4 Ordinals", "Risk").</summary>
        public string Tag = "";
        public string Result = "";
        public List<EventOutcome> Outcomes = new List<EventOutcome>();
    }

    public sealed class EventDef
    {
        public string Id;
        public string Title;
        public string Art = "";
        public string Body = "";
        public List<EventChoice> Choices = new List<EventChoice>();
    }

    /// <summary>A flagship module: the cards it adds to the deck and what it changes on the hull.</summary>
    public sealed class ModuleDef
    {
        public string Id;
        public string Slot;
        public string Name;
        public string Text = "";
        public List<string> Cards = new List<string>();
        public int Hull, HeatCap, MaxSpeed, Shields, ArmorFore;
    }

    public sealed class RewardCard
    {
        public string Card;
        public int Ship;
        /// <summary>Only offered while the flagship has this module fitted. Empty: always.</summary>
        public string RequiresModule = "";
        public int Weight = 10;
        public int Price = 45;
    }

    /// <summary>A passive rule change carried for the rest of the run. All effects are plain numbers.</summary>
    public sealed class RelicDef
    {
        public string Id;
        public string Name;
        public string Text = "";
        /// <summary>Taken from a named commander; never in the elite pool.</summary>
        public bool Trophy;
        public int FirstTurnCommand, MaxDarts, FlagshipHull, HeatCap, Shields;
        /// <summary>Tenths of a node per jump added to both rivals' pace (negative slows them).</summary>
        public int RivalRate;
        /// <summary>Percent added to battle scrap.</summary>
        public int ScrapPercent;
        public int OrdinalsPerVictory, FieldRepair;
        public bool FreeJumps;
    }

    /// <summary>One way to answer a commander's hail. The effect is code, keyed by <see cref="Id"/>.</summary>
    public sealed class HailReply
    {
        /// <summary>"edict", "fire", "board" or "silence".</summary>
        public string Id;
        public string Label = "";
        public string Tag = "";
        public string Effect = "";
        /// <summary>Ordinals expended to give this reply.</summary>
        public int Ordinals;
    }

    /// <summary>A named rival commander: ship, escorts by rank, and what they say.</summary>
    public sealed class NemesisDef
    {
        public string Id;
        public string Name;
        public string Title = "";
        /// <summary>Unit id of the commander's own ship.</summary>
        public string Ship;
        public string Portrait = "";
        public string Trait = "";
        public string Tactic = "";
        /// <summary>Escorts at rank 1, 2 and 3.</summary>
        public List<EncounterUnit>[] Escorts = { new List<EncounterUnit>(), new List<EncounterUnit>(), new List<EncounterUnit>() };
        /// <summary>Hail lines: first meeting, after escaping you, after a stand-off, and at rank 3.</summary>
        public string HailFirst = "", HailEscaped = "", HailStandoff = "", HailFinal = "";
        /// <summary>Relic id taken from the wreck.</summary>
        public string Trophy = "";
        public int Scrap;
    }

    /// <summary>Numbers for the run loop. Everything here is a first guess, tuned by the balance sims.</summary>
    public sealed class RunConfig
    {
        public int StartScrap = 30, StartOrdinals = 24, MaxOrdinals = 24, StartDarts = 6, MaxDarts = 6;
        public int FlagshipBaseHeatCap = 8;
        public int Layers = 8;
        public int RemoveCardCost = 30, MinDeckSize = 8;
        public int RepairCostPerHull = 2, RemoveJunkCost = 20, ModuleCost = 50, RebuildEscortCost = 55;
        public int DartCost = 5, OrdinalCost = 3, OrdinalsPerPurchase = 4;
        public int RewardCards = 3;
        public int OrdinalsPerVictory = 2;
        /// <summary>Hull each ship regains when the player takes field repairs instead of a reward card.</summary>
        public int FieldRepair = 5;
        /// <summary>
        /// Battles open with the fleets already closing: the player's ships start in this column and every
        /// enemy starts this many columns nearer than its encounter lists, so turn 1 has shots to take.
        /// </summary>
        public int PlayerStartCol = 2, EnemyAdvance = 2;
        /// <summary>Race: tenths of a node each rival advances per node you travel, plus 0..spread.</summary>
        public int ConcordRate = 9, ChorusRate = 8, RivalSpread = 3;
        public int ConcordStart = 6, ChorusStart = 2;
        public int WithdrawalDelay = 10, LongBattleTurns = 10, LongBattleDelay = 4, ShipyardDelayPerPurchase = 2;
        public int FlagshipRebuildOrdinals = 8, FlagshipRebuildMaxHullLoss = 4;
        /// <summary>Jump lanes per sector, and the hull each ship pays to take one.</summary>
        public int JumpLanes = 2, JumpHullCost = 4;
        /// <summary>When the Concord leads by this much (tenths of a node), ordinary lanes are mined: each ship loses MineDamage hull per jump.</summary>
        public int MineLead = 10, MineDamage = 2;
        /// <summary>Killing the commander sets the Concord back this far; slipping past them costs NemesisDodgeDelay.</summary>
        public int NemesisSlow = 15, NemesisDodgeDelay = 3;
        /// <summary>A commander below rank 3 breaks off at this percent of hull; each rank adds NemesisRankHull.</summary>
        public int NemesisFleePercent = 40, NemesisRankHull = 6;
        public int HailEdictDamage = 2, HailEdictScrapPercent = 150, HailBoardTurns = 2;
        /// <summary>Shipyard service: false beacons set both rivals back.</summary>
        public int SabotageCost = 25, SabotageAmount = 8;
        public List<string> StarterModules = new List<string>();
        public List<DeckCard> StarterCards = new List<DeckCard>();
        public List<string> Slots = new List<string>();
    }

    /// <summary>All run content, loaded from data by the Data layer and handed to <see cref="RunRules"/>.</summary>
    public sealed class RunContent
    {
        public RunConfig Config = new RunConfig();
        public List<EncounterDef> Encounters = new List<EncounterDef>();
        public List<EventDef> Events = new List<EventDef>();
        public List<ModuleDef> Modules = new List<ModuleDef>();
        public List<RewardCard> Rewards = new List<RewardCard>();
        public List<RelicDef> Relics = new List<RelicDef>();
        public List<NemesisDef> Nemeses = new List<NemesisDef>();
        public List<HailReply> HailReplies = new List<HailReply>();

        public EncounterDef Encounter(string id) => Encounters.Find(e => e.Id == id) ?? throw new ArgumentException($"unknown encounter '{id}'");
        public EventDef Event(string id) => Events.Find(e => e.Id == id) ?? throw new ArgumentException($"unknown event '{id}'");
        public RelicDef Relic(string id) => Relics.Find(x => x.Id == id) ?? throw new ArgumentException($"unknown relic '{id}'");
        public NemesisDef Nemesis(string id) => Nemeses.Find(x => x.Id == id) ?? throw new ArgumentException($"unknown nemesis '{id}'");
        public ModuleDef Module(string id) => Modules.Find(m => m.Id == id) ?? throw new ArgumentException($"unknown module '{id}'");
    }
}
