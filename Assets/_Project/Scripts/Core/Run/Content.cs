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

    /// <summary>Numbers for the run loop. Everything here is a first guess, tuned by the balance sims.</summary>
    public sealed class RunConfig
    {
        public int StartScrap = 30, StartOrdinals = 24, MaxOrdinals = 24, StartDarts = 6, MaxDarts = 6;
        public int FlagshipBaseHeatCap = 8;
        public int Layers = 8;
        public int RepairCostPerHull = 2, RemoveJunkCost = 20, ModuleCost = 50, RebuildEscortCost = 55;
        public int DartCost = 5, OrdinalCost = 3, OrdinalsPerPurchase = 4;
        public int RewardCards = 3;
        public int OrdinalsPerVictory = 2;
        /// <summary>Race: tenths of a node each rival advances per node you travel, plus 0..spread.</summary>
        public int ConcordRate = 9, ChorusRate = 8, RivalSpread = 3;
        public int ConcordStart = 6, ChorusStart = 2;
        public int WithdrawalDelay = 10, LongBattleTurns = 10, LongBattleDelay = 4, ShipyardDelayPerPurchase = 2;
        public int FlagshipRebuildOrdinals = 8, FlagshipRebuildMaxHullLoss = 4;
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

        public EncounterDef Encounter(string id) => Encounters.Find(e => e.Id == id) ?? throw new ArgumentException($"unknown encounter '{id}'");
        public EventDef Event(string id) => Events.Find(e => e.Id == id) ?? throw new ArgumentException($"unknown event '{id}'");
        public ModuleDef Module(string id) => Modules.Find(m => m.Id == id) ?? throw new ArgumentException($"unknown module '{id}'");
    }
}
