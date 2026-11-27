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
        /// <summary>One line of what was found afterwards, shown with the salvage.</summary>
        public string Aftermath = "";
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
        Approval, GainOfficer, HealOfficers, GainRelic,
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

    /// <summary>Hazards a battle can be fought in. What each does is code in <see cref="RunRules.BuildBattle"/>.</summary>
    public static class Hazards
    {
        public static readonly string[] All = { "ion_storm", "asteroid_field", "nebula" };

        public static string Name(string id)
        {
            switch (id)
            {
                case "ion_storm": return "Ion storm";
                case "asteroid_field": return "Dense field";
                case "nebula": return "Nebula";
                default: return "";
            }
        }

        public static string Text(string id)
        {
            switch (id)
            {
                case "ion_storm": return "Nobody's shields regenerate. What you strip stays stripped, and so does what they strip from you.";
                case "asteroid_field": return "Twice the rock. Drift carefully and use it to break their lines of fire.";
                case "nebula": return "Every weapon on both sides reaches 1 hex less.";
                default: return "";
            }
        }
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
        /// <summary>Mined lanes cost this fleet nothing.</summary>
        public bool MineProof;
        /// <summary>Sprite name of its icon.</summary>
        public string Icon = "";
    }

    /// <summary>One way to answer a commander's hail. The effect is code, keyed by <see cref="Id"/>.</summary>
    public sealed class HailReply
    {
        public string Id;
        /// <summary>What it does, by code key: "edict" (enrage them), "fire" (first strike), "board" (silence the escorts) or "silence".</summary>
        public string Effect = "silence";
        public string Label = "";
        public string Tag = "";
        /// <summary>Crew expended to give this reply, or Council approval spent.</summary>
        public int Ordinals, Approval;
        /// <summary>Only on offer once you and the commander have met before.</summary>
        public bool RequiresHistory;
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
        /// <summary>What they say back to each kind of reply ("edict", "fire", "board", "silence"), as the fight opens.</summary>
        public Dictionary<string, string> Retorts = new Dictionary<string, string>();
        /// <summary>What is said over the wreck.</summary>
        public string Epitaph = "";
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
        /// <summary>A taunted commander does not break off, and their fleet hits this much harder.</summary>
        public int HailTauntDamage = 1;
        /// <summary>Shipyard service: false beacons set both rivals back.</summary>
        public int SabotageCost = 25, SabotageAmount = 8;
        /// <summary>Percent chance that a fight from the third layer on is in a hazard, and what its salvage is multiplied by.</summary>
        public int HazardChance = 25, HazardScrapPercent = 130;
        /// <summary>How many of the written events one sector draws on (0 = all of them).</summary>
        public int EventsPerSector = 5;
        /// <summary>Shipyard service: the sickbay returns one wounded officer to duty.</summary>
        public int HealOfficerCost = 15;
        public List<string> StarterModules = new List<string>();
        public List<DeckCard> StarterCards = new List<DeckCard>();
        public List<string> Slots = new List<string>();
    }

    /// <summary>A named officer aboard the flagship: a passive bonus while fit, nothing while wounded.</summary>
    public sealed class OfficerDef
    {
        public string Id;
        public string Name;
        public string Role = "";
        public string Text = "";
        /// <summary>Added to every repair card; to the flagship's weapon cards; to each craft attack.</summary>
        public int RepairBonus, WeaponBonus, CraftBonus;
        public int FirstTurnDraw, CrewPerVictory, ShieldBonus;
    }

    /// <summary>Something the Council can be asked to vote through, paid for in approval.</summary>
    public sealed class MotionDef
    {
        public string Id;
        public string Name;
        public string Text = "";
        public int Cost = 1;
        /// <summary>Hull to every ship, scrap, craft restocked, crew taken on, tenths of a node the rivals lose.</summary>
        public int Hull, Scrap, Craft, Crew, RivalsBack;
        /// <summary>Commission a new officer from those not aboard.</summary>
        public bool Officer;
        public bool HealOfficers;
    }

    /// <summary>Who the player is: names, voice and the lines the map and the end screens use. All from data.</summary>
    public sealed class FactionDef
    {
        public string Id = "ascendancy";
        public string Name = "The Ascendancy";
        public string Blurb = "";
        /// <summary>Unit ids of the two starting ships, the craft the hangar launches and the craft enemy carriers launch.</summary>
        public string FlagshipUnit = "flagship", EscortUnit = "escort", CraftUnit = "dart", EnemyCraftUnit = "gunship";
        /// <summary>What the crew pool and the hangar craft are called ("Ordinals", "darts").</summary>
        public string CrewName = "Ordinals", CraftName = "darts";
        /// <summary>The rival on the top race marker (its fleet guards the gate) and the other one.</summary>
        public string RivalName = "Concord", ThirdName = "Chorus";
        public string SectorName = "", OpeningJournal = "";
        /// <summary>The opening briefing, shown once before the first jump.</summary>
        public string Briefing = "";
        /// <summary>One line each for the first time the rival's fleet, and the third fleet, gets ahead of you.</summary>
        public string RivalPasses = "", ThirdPasses = "";
        public string BossName = "", BossShip = "", BossPortrait = "", BossHail = "", BossHailEnraged = "";
        public string WinAhead = "", WinBehind = "", WinThirdAhead = "", Outpaced = "", LostAhead = "", LostBehind = "";
        /// <summary>True for a faction that votes: approval is earned in battle and spent on Council motions.</summary>
        public bool HasCouncil;
        public int StartApproval, MaxApproval = 6;
        public List<string> StarterOfficers = new List<string>();
        public int MaxOfficers = 4;
    }

    /// <summary>All run content, loaded from data by the Data layer and handed to <see cref="RunRules"/>.</summary>
    public sealed class RunContent
    {
        public FactionDef Faction = new FactionDef();
        public RunConfig Config = new RunConfig();
        public List<EncounterDef> Encounters = new List<EncounterDef>();
        public List<EventDef> Events = new List<EventDef>();
        public List<ModuleDef> Modules = new List<ModuleDef>();
        public List<RewardCard> Rewards = new List<RewardCard>();
        public List<RelicDef> Relics = new List<RelicDef>();
        public List<NemesisDef> Nemeses = new List<NemesisDef>();
        public List<HailReply> HailReplies = new List<HailReply>();
        public List<OfficerDef> Officers = new List<OfficerDef>();
        public List<MotionDef> Motions = new List<MotionDef>();

        public EncounterDef Encounter(string id) => Encounters.Find(e => e.Id == id) ?? throw new ArgumentException($"unknown encounter '{id}'");
        public EventDef Event(string id) => Events.Find(e => e.Id == id) ?? throw new ArgumentException($"unknown event '{id}'");
        public OfficerDef Officer(string id) => Officers.Find(x => x.Id == id) ?? throw new ArgumentException($"unknown officer '{id}'");
        public RelicDef Relic(string id) => Relics.Find(x => x.Id == id) ?? throw new ArgumentException($"unknown relic '{id}'");
        public NemesisDef Nemesis(string id) => Nemeses.Find(x => x.Id == id) ?? throw new ArgumentException($"unknown nemesis '{id}'");
        public ModuleDef Module(string id) => Modules.Find(m => m.Id == id) ?? throw new ArgumentException($"unknown module '{id}'");
    }
}
