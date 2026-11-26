using System;
using System.Collections.Generic;
using Silence.Core.Hex;
using Silence.Core.Run;
using UnityEngine;

namespace Silence.Data
{
    /// <summary>
    /// Loads the run content (encounters, events, modules, reward pool, tuning numbers) from the JSON files
    /// in Data/Resources/Content and turns it into the plain definitions Core works with.
    /// Cards and unit templates are still code in Core (their effects and AI are code); everything a
    /// designer tunes between runs is in these files.
    /// </summary>
    public static class ContentLoader
    {
        static RunContent cached;

        public static RunContent Load(bool reload = false)
        {
            if (cached != null && !reload) return cached;
            var content = new RunContent();
            ReadRun(Text("Content/run"), content);
            ReadEncounters(Text("Content/encounters"), content);
            ReadEvents(Text("Content/events"), content);
            cached = content;
            return content;
        }

        /// <summary>Forget the loaded content so the next <see cref="Load"/> reads the files again.</summary>
        public static void Invalidate() => cached = null;

        static string Text(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException($"missing content file Resources/{path}.json");
            return asset.text;
        }

        // ---------------------------------------------------------------- run.json

        [Serializable] class CardDto { public string card; public int ship; }

        [Serializable]
        class ConfigDto
        {
            public int startScrap, startOrdinals, maxOrdinals, startDarts, maxDarts, flagshipBaseHeatCap, layers;
            public int removeCardCost, minDeckSize;
            public int repairCostPerHull, removeJunkCost, moduleCost, rebuildEscortCost, dartCost, ordinalCost, ordinalsPerPurchase;
            public int rewardCards, ordinalsPerVictory, fieldRepair, concordRate, chorusRate, rivalSpread, concordStart, chorusStart;
            public int withdrawalDelay, longBattleTurns, longBattleDelay, shipyardDelayPerPurchase;
            public int flagshipRebuildOrdinals, flagshipRebuildMaxHullLoss;
            public string[] slots;
            public string[] starterModules;
            public CardDto[] starterCards;
        }

        [Serializable]
        class ModuleDto
        {
            public string id, slot, name, text;
            public string[] cards;
            public int hull, heatCap, maxSpeed, shields, armorFore;
        }

        [Serializable] class RewardDto { public string card; public int ship; public string requiresModule; public int weight; public int price; }

        [Serializable] class RunDto { public ConfigDto config; public ModuleDto[] modules; public RewardDto[] rewards; }

        static void ReadRun(string json, RunContent content)
        {
            var dto = JsonUtility.FromJson<RunDto>(json);
            var c = dto.config;
            content.Config = new RunConfig
            {
                StartScrap = c.startScrap, StartOrdinals = c.startOrdinals, MaxOrdinals = c.maxOrdinals, StartDarts = c.startDarts, MaxDarts = c.maxDarts,
                FlagshipBaseHeatCap = c.flagshipBaseHeatCap, Layers = c.layers,
                RemoveCardCost = c.removeCardCost > 0 ? c.removeCardCost : 30, MinDeckSize = c.minDeckSize > 0 ? c.minDeckSize : 8,
                RepairCostPerHull = c.repairCostPerHull, RemoveJunkCost = c.removeJunkCost, ModuleCost = c.moduleCost, RebuildEscortCost = c.rebuildEscortCost,
                DartCost = c.dartCost, OrdinalCost = c.ordinalCost, OrdinalsPerPurchase = c.ordinalsPerPurchase,
                RewardCards = c.rewardCards, OrdinalsPerVictory = c.ordinalsPerVictory, FieldRepair = c.fieldRepair,
                ConcordRate = c.concordRate, ChorusRate = c.chorusRate, RivalSpread = c.rivalSpread, ConcordStart = c.concordStart, ChorusStart = c.chorusStart,
                WithdrawalDelay = c.withdrawalDelay, LongBattleTurns = c.longBattleTurns, LongBattleDelay = c.longBattleDelay,
                ShipyardDelayPerPurchase = c.shipyardDelayPerPurchase,
                FlagshipRebuildOrdinals = c.flagshipRebuildOrdinals, FlagshipRebuildMaxHullLoss = c.flagshipRebuildMaxHullLoss,
                Slots = new List<string>(c.slots), StarterModules = new List<string>(c.starterModules),
            };
            foreach (var s in c.starterCards) content.Config.StarterCards.Add(new DeckCard(s.card, s.ship));
            foreach (var m in dto.modules)
                content.Modules.Add(new ModuleDef
                {
                    Id = m.id, Slot = m.slot, Name = m.name, Text = m.text, Cards = new List<string>(m.cards),
                    Hull = m.hull, HeatCap = m.heatCap, MaxSpeed = m.maxSpeed, Shields = m.shields, ArmorFore = m.armorFore,
                });
            foreach (var r in dto.rewards)
                content.Rewards.Add(new RewardCard { Card = r.card, Ship = r.ship, RequiresModule = r.requiresModule ?? "", Weight = r.weight, Price = r.price });
        }

        // ---------------------------------------------------------------- encounters.json

        [Serializable] class UnitDto { public string unit; public int col, row, speed; public string facing; }
        [Serializable] class TerrainDto { public int col, row; public bool asteroid; }

        [Serializable]
        class EncounterDto
        {
            public string id, name, intro;
            public int tier, scrap, turnLimit, scatterAsteroids, scatterDebris;
            public UnitDto[] units;
            public TerrainDto[] terrain;
            public UnitDto[] reinforcements;
        }

        [Serializable] class EncountersDto { public EncounterDto[] encounters; }

        static EncounterUnit Unit(UnitDto u) => new EncounterUnit
        {
            Unit = u.unit, Col = u.col, Row = u.row, Speed = u.speed,
            Facing = string.IsNullOrEmpty(u.facing) ? HexDirection.W : (HexDirection)Enum.Parse(typeof(HexDirection), u.facing),
        };

        static void ReadEncounters(string json, RunContent content)
        {
            foreach (var e in JsonUtility.FromJson<EncountersDto>(json).encounters)
            {
                var def = new EncounterDef
                {
                    Id = e.id, Name = e.name, Intro = e.intro ?? "", Tier = e.tier, Scrap = e.scrap, TurnLimit = e.turnLimit,
                    ScatterAsteroids = e.scatterAsteroids, ScatterDebris = e.scatterDebris,
                };
                foreach (var u in e.units ?? new UnitDto[0]) def.Units.Add(Unit(u));
                foreach (var u in e.reinforcements ?? new UnitDto[0]) def.Reinforcements.Add(Unit(u));
                foreach (var t in e.terrain ?? new TerrainDto[0]) def.Terrain.Add((t.col, t.row, t.asteroid));
                content.Encounters.Add(def);
            }
        }

        // ---------------------------------------------------------------- events.json

        [Serializable] class OutcomeDto { public string kind; public int amount; public string id; public int ship; }
        [Serializable] class ChoiceDto { public string label, tag, result; public OutcomeDto[] outcomes; }
        [Serializable] class EventDto { public string id, title, art, body; public ChoiceDto[] choices; }
        [Serializable] class EventsDto { public EventDto[] events; }

        static void ReadEvents(string json, RunContent content)
        {
            foreach (var e in JsonUtility.FromJson<EventsDto>(json).events)
            {
                var def = new EventDef { Id = e.id, Title = e.title, Art = e.art ?? "", Body = e.body ?? "" };
                foreach (var ch in e.choices)
                {
                    var choice = new EventChoice { Label = ch.label, Tag = ch.tag ?? "", Result = ch.result ?? "" };
                    foreach (var o in ch.outcomes ?? new OutcomeDto[0])
                        choice.Outcomes.Add(new EventOutcome
                        {
                            Kind = (OutcomeKind)Enum.Parse(typeof(OutcomeKind), o.kind), Amount = o.amount, Id = o.id ?? "", Ship = o.ship,
                        });
                    def.Choices.Add(choice);
                }
                content.Events.Add(def);
            }
        }
    }

    /// <summary>The run save format: the whole <see cref="RunState"/> as JSON.</summary>
    public static class RunJson
    {
        public static string ToJson(RunState run) => JsonUtility.ToJson(run, true);

        public static RunState FromJson(string json)
        {
            var run = JsonUtility.FromJson<RunState>(json);
            if (run == null || run.Ships == null || run.Ships.Count == 0 || run.Nodes == null || run.Nodes.Count == 0)
                throw new FormatException("not a run save");
            return run;
        }
    }
}
