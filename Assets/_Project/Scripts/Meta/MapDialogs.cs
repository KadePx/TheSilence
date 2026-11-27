using System.Collections.Generic;
using System.Linq;
using Silence.Core.Battle;
using Silence.Core.Run;
using Silence.Data;
using Silence.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Silence.Meta
{
    /// <summary>The dialogs the map opens: pre-battle briefing, reward, event, shipyard and the run-over screen.</summary>
    public sealed partial class MapScreen
    {
        // ---------------------------------------------------------------- pre-battle

        void ShowBattlePrompt()
        {
            var enc = RunRules.PendingEncounterDef(run, content);
            bool boss = enc.Tier == 3;
            var roster = RunRules.Roster(run, content, enc);
            int rows = roster.Select(u => u.Unit).Distinct().Count();
            float w = boss ? 1180 : 980;
            float h = 116 + (boss ? Mathf.Max((run.VennEnraged ? 150 : 110) + 36 + 64 + 22 + rows * 48 + (EdictVerdictLine() != "" ? 48 : 0), 340) : 64 + 22 + rows * 48 + (enc.Tier == 5 ? 44 : 0) + (run.PendingHazard != "" && enc.Tier <= 2 ? 48 : 0)) + 130;
            var d = OpenDialog(w, h);
            string tier = boss ? "Sector boss" : enc.Tier == 5 ? "Nemesis" : enc.Tier == 2 ? "Elite battle" : enc.Tier == 4 ? "Ambush" : "Battle";
            Ui.Stamp(d, tier, 14, 40, 26, 400, 20, TextAlignmentOptions.Left, enc.Tier >= 2 ? Skin.Bad : Skin.Brass);
            Ui.Heading(d, enc.Name, 32, 38, 50, w - 80, 40);

            float textX = 40, textW = w - 80, y = 116;
            if (boss)
            {
                var well = Ui.Well(d, "Comms", 40, 116, 272, 272);
                var fac = content.Faction.SectorInfo(run.Sector);
                var portrait = Ui.Art(well.transform, fac.BossPortrait, 8, 8, 2f);
                if (!portrait.enabled) Ui.Body(well.transform, "NO SIGNAL", 20, 0, 120, 272, 30, TextAlignmentOptions.Center, Skin.Faint);
                Ui.Stamp(d, fac.BossName, 14, 20, 394, 312, 18, TextAlignmentOptions.Center, Skin.Hex("ffc46a"));
                Ui.Stamp(d, fac.BossShip, 11, 20, 414, 312, 16, TextAlignmentOptions.Center, Skin.Dim);
                textX = 340;
                textW = w - 380;
                string hail = fac.BossHail;
                if (run.VennEnraged) hail += "\n\n" + fac.BossHailEnraged;
                // The same comms well the commanders speak from.
                float speechH = run.VennEnraged ? 142 : 102;
                var speech = Ui.Well(d, "Speech", textX, y, textW, speechH);
                Ui.Label(speech.transform, hail, Skin.BodyBold, 16, Skin.Hex("ffe0b0"), 20, 16, textW - 40, speechH - 24, TextAlignmentOptions.TopLeft);
                y += speechH + 12;
            }

            Ui.Body(d, enc.Intro, 20, textX, y, textW, 70, TextAlignmentOptions.TopLeft, Skin.Text);
            y += 64;

            var units = roster;
            if (run.PendingHazard != "" && enc.Tier <= 2)
            {
                Ui.Body(d, $"<color=#b48cf0>{Hazards.Name(run.PendingHazard)}:</color> {Hazards.Text(run.PendingHazard)} Salvage x{content.Config.HazardScrapPercent / 100f:0.0}.", 16, textX, y, textW, 40);
                y += 48;
            }
            if (enc.Tier == 5)
            {
                var reply = content.HailReplies.Find(x => x.Id == run.PendingHail);
                if (reply != null)
                {
                    Ui.Body(d, $"<color=#c9a24a>{reply.Tag}:</color> {RunRules.HailEffect(content, reply)}.", 16, textX, y, textW, 40);
                    y += 44;
                }
            }
            if (boss && EdictVerdictLine() != "")
            {
                Ui.Body(d, EdictVerdictLine(), 16, textX, y, textW, 40);
                y += 48;
            }
            if (boss)
            {
                string rivalName = content.Faction.RivalName;
                string race = RunRules.ConcordAhead(run)
                    ? $"<color=#d9573f>The {rivalName} fleet reached the gate before you. Its guard is reinforced.</color>"
                    : run.VennEnraged ? "<color=#d9573f>The gate has called in another ship.</color>"
                    : $"<color=#8cc47a>You are ahead of the {rivalName} fleet. The gate has only its standing guard.</color>";
                Ui.Body(d, race, 18, textX, y, textW, 26);
                y += 36;
            }

            Ui.Stamp(d, "Enemy fleet", 12, textX, y, 300, 16, TextAlignmentOptions.Left, Skin.Brass);
            y += 22;
            FleetRows(d, units, textX, y, textW);

            Ui.Btn(d, "Engage", (w - 300) / 2f, h - 104, 300, 74, Engage, 32);
        }

        static string StepName(IntentStep s)
        {
            switch (s.Kind)
            {
                case IntentKind.Broadside: return $"{s.Label ?? "Broadside"} {s.Amount}";
                case IntentKind.Maneuver: return "Maneuver";
                case IntentKind.LaunchTorpedo: return "Torpedo";
                case IntentKind.LaunchCraft: return $"Launch {s.Amount}";
                case IntentKind.PatchUp: return $"Repair {s.Amount}";
                case IntentKind.JointFire: return "Joint Fire";
                default: return s.Kind.ToString();
            }
        }

        // ---------------------------------------------------------------- reward

        void ShowReward()
        {
            var offer = run.Reward;
            if (offer.RelicChoices.Count > 0)
            {
                ShowRelicChoice();
                return;
            }
            var d = OpenDialog(1040, 660);
            Ui.Stamp(d, "After action", 14, 40, 26, 400, 20, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Heading(d, "Salvage", 32, 38, 50, 560, 40);
            Ui.Body(d, offer.Note, 16, 40, 88, 960, 36, TextAlignmentOptions.TopLeft, Skin.Dim);
            Ui.Art(d, "res_scrap", 640, 40, 1f);
            Ui.Number(d, $"+{offer.Scrap} scrap", 24, 700, 52, 200, 24, TextAlignmentOptions.Left, Skin.Brass);
            if (offer.Ordinals > 0)
            {
                Ui.Art(d, CrewIcon, 850, 40, 1f);
                Ui.Number(d, $"+{offer.Ordinals}", 24, 910, 52, 120, 24, TextAlignmentOptions.Left, Skin.Cyan);
            }
            if (!string.IsNullOrEmpty(offer.Relic))
            {
                var relic = content.Relic(offer.Relic);
                Ui.Art(d, ArtOr(relic.Icon, "icon_relic"), 40, 126, 1f);
                Ui.Body(d, $"<color=#ffc46a>{(relic.Trophy ? "Trophy" : "Relic")}: {relic.Name}.</color> {relic.Text}", 16, 96, 134, 900, 16);
            }
            Ui.Stamp(d, "Take one card for the command deck, or patch the fleet up", 12, string.IsNullOrEmpty(offer.Relic) ? 40 : 96, 160, 800, 14, TextAlignmentOptions.Left, Skin.BrassDark);

            int n = offer.Cards.Count;
            float step = CardView.W + 28, x0 = (1040 - (step * (n - 1) + CardView.W)) / 2f + CardView.W / 2f;
            for (int i = 0; i < n; i++)
            {
                int index = i;
                var card = MakeCard(d, offer.Cards[i], x0 + i * step, 180 + CardView.H);
                card.Clicked = _ => PickReward(index);
                card.Hovered = v => v.SetSelected(true);
                card.Unhovered = v => v.SetSelected(false);
            }
            bool hurt = run.Ships.Any(s => s.Alive && s.Hull < s.MaxHull);
            var repair = Ui.Btn(d, $"Field repairs instead: +{RunRules.FieldRepairAmount(run, content)} hull each", 130, 566, 500, 60, FieldRepair, 16);
            repair.interactable = hurt;
            Ui.Btn(d, "Take nothing", 660, 566, 250, 60, () => PickReward(-1), 16);
        }

        // ---------------------------------------------------------------- events

        void ShowEvent()
        {
            var ev = content.Event(run.EventId);
            var d = OpenDialog(1500, 470 + ev.Choices.Count * 116 + 34);
            EventArt(d, ev);
            Ui.Stamp(d, "Event", 14, 760, 34, 300, 20, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Heading(d, ev.Title, 32, 758, 60, 700, 40);
            Ui.Body(d, ev.Body, 21, 760, 130, 700, 300, TextAlignmentOptions.TopLeft);

            float y = 470;
            if (!string.IsNullOrEmpty(notice))
            {
                Ui.Body(d, $"<color=#f0a040>{notice}</color>", 18, 40, 436, 1400, 26);
            }
            for (int i = 0; i < ev.Choices.Count; i++)
            {
                var choice = ev.Choices[i];
                string blocked = RunRules.CanChoose(run, choice);
                int index = i;
                var b = Ui.Btn(d, "", 40, y, 1420, 104, () => Choose(index));
                b.interactable = blocked == null;
                Ui.Number(b.transform, (i + 1).ToString(), 24, 22, 40, 40, 24, TextAlignmentOptions.Center, blocked == null ? Skin.Brass : Skin.Faint);
                Ui.Label(b.transform, choice.Label, Skin.BodyBold, 16, blocked == null ? Skin.Text : Skin.Faint, 80, 26, 1300, 36);
                string tag = Summary(choice) + (blocked != null ? $"   <color=#d9573f>({blocked})</color>" : "");
                Ui.Stamp(b.transform, tag, 12, 82, 64, 1300, 14, TextAlignmentOptions.Left, blocked == null ? Skin.Brass : Skin.Faint);
                string effects = Summary(choice);
                Tip.On(b.targetGraphic, "What this does", () => effects == "No effect" ? "Nothing changes." : Gloss.Legend(effects));
                y += 116;
            }
        }

        /// <summary>What a choice does, in numbers, built from its outcomes so the text can never drift from the rules.</summary>
        string Summary(EventChoice choice)
        {
            var parts = new List<string>();
            foreach (var o in choice.Outcomes)
            {
                string sign = o.Amount >= 0 ? "+" + o.Amount : o.Amount.ToString();
                switch (o.Kind)
                {
                    case OutcomeKind.Scrap: parts.Add(o.Amount < 0 ? $"Pay {-o.Amount} scrap" : $"{sign} scrap"); break;
                    case OutcomeKind.Ordinals: parts.Add(o.Amount < 0 ? $"Expend {-o.Amount} {content.Faction.CrewName}" : $"{sign} {content.Faction.CrewName}"); break;
                    case OutcomeKind.Approval: parts.Add($"{sign} approval"); break;
                    case OutcomeKind.GainOfficer: parts.Add("An officer comes aboard"); break;
                    case OutcomeKind.HealOfficers: parts.Add("Wounded officers return to duty"); break;
                    case OutcomeKind.GainRelic: parts.Add("Gain a relic"); break;
                    case OutcomeKind.HullFlagship: parts.Add($"Flagship {sign} hull"); break;
                    case OutcomeKind.HullAll: parts.Add($"Every ship {sign} hull"); break;
                    case OutcomeKind.MaxHullFlagship: parts.Add($"Flagship {sign} max hull"); break;
                    case OutcomeKind.Darts: parts.Add($"{sign} {content.Faction.CraftName}"); break;
                    case OutcomeKind.AddCard: parts.Add($"Gain {PaperCards.Get(o.Id).Name}"); break;
                    case OutcomeKind.AddJunk: parts.Add($"Adds {PaperCards.Get(o.Id).Name}"); break;
                    case OutcomeKind.RemoveJunk: parts.Add(o.Amount > 1 ? $"Removes up to {o.Amount} junk cards" : "Removes a junk card"); break;
                    case OutcomeKind.RivalsAdvance:
                        parts.Add(o.Amount > 0 ? $"Costs time: rivals gain {o.Amount / 10f:0.0} node" : $"Rivals fall back {-o.Amount / 10f:0.0} node");
                        break;
                    case OutcomeKind.EnrageBoss: parts.Add("The gate guard is reinforced"); break;
                    case OutcomeKind.StartBattle:
                        parts.Add($"Battle: {content.Encounter(o.Id).Name}" + (o.Amount > 100 ? $", salvage x{o.Amount / 100f:0.0}" : ""));
                        break;
                }
            }
            return parts.Count == 0 ? "No effect" : string.Join("   /   ", parts);
        }

        void EventArt(RectTransform d, EventDef ev)
        {
            var well = Ui.Well(d, "ArtWell", 34, 34, 700, 396);
            var art = ArtLibrary.Instance.Sprite(ev.Art);
            if (art != null) Ui.Img(well.transform, "Art", art, 6, 6, art.rect.width * 2f, art.rect.height * 2f);
            else Ui.Art(well.transform, "icon_derelict", 302, 150, 2f);
        }

        void ShowEventResult()
        {
            var ev = content.Event(run.EventId);
            var d = OpenDialog(1500, 640);
            EventArt(d, ev);
            Ui.Stamp(d, "Event", 14, 760, 34, 300, 20, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Heading(d, ev.Title, 32, 758, 60, 700, 40);
            string text = run.EventResult;
            int split = text.LastIndexOf("\n\n", System.StringComparison.Ordinal);
            if (split > 0) text = text.Substring(0, split) + "\n\n<color=#c9a24a>" + text.Substring(split + 2) + "</color>";
            Ui.Body(d, text, 21, 760, 130, 700, 300, TextAlignmentOptions.TopLeft);
            Ui.Btn(d, "Continue", 600, 520, 300, 74, CloseEvent, 32);
        }

        // ---------------------------------------------------------------- shipyard

        void ShowShipyard()
        {
            var cfg = content.Config;
            var shop = run.Shop;
            var d = OpenDialog(1760, 960, 0.6f, "shipyard_bg");
            Ui.Stamp(d, "Shipyard", 14, 40, 24, 400, 20, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Heading(d, "Drydock", 32, 38, 48, 500, 40);
            Ui.Art(d, "res_scrap", 1080, 30, 1f);
            Ui.Number(d, $"{run.Scrap} scrap", 24, 1136, 42, 260, 24, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Body(d, $"Every service keeps you in dock: the rivals gain {cfg.ShipyardDelayPerPurchase / 10f:0.0} node per purchase. Services bought: {shop.Purchases}.",
                16, 560, 96, 800, 24, TextAlignmentOptions.TopLeft, Skin.Dim);
            if (!string.IsNullOrEmpty(notice)) Ui.Body(d, $"<color=#f0a040>{notice}</color>", 18, 40, 96, 500, 26);
            Ui.Btn(d, "Leave dock", 1460, 26, 260, 66, LeaveShipyard, 16);

            // ---- services
            float x = 40, y = 140;
            Ui.Stamp(d, "Services", 13, x, y, 300, 18, TextAlignmentOptions.Left, Skin.Brass);
            y += 26;
            for (int i = 0; i < run.Ships.Count; i++)
            {
                var ship = run.Ships[i];
                int index = i;
                if (!ship.Alive)
                {
                    Service(d, x, ref y, $"Rebuild {ship.Name}", "A new escort from the yard's own stock.", cfg.RebuildEscortCost,
                        true, () => Shop(() => RunRules.RebuildEscort(run, content)));
                    continue;
                }
                int missing = ship.MaxHull - ship.Hull;
                int cost = Mathf.Min(missing * cfg.RepairCostPerHull, run.Scrap / Mathf.Max(1, cfg.RepairCostPerHull) * cfg.RepairCostPerHull);
                Service(d, x, ref y, $"Repair {ship.Name}", missing == 0 ? "Hull is whole." : $"Hull {ship.Hull}/{ship.MaxHull}. {cfg.RepairCostPerHull} scrap a point, as far as the scrap goes.",
                    Mathf.Max(cost, missing > 0 ? cfg.RepairCostPerHull : 0), missing > 0, () => Shop(() => RunRules.Repair(run, content, index)));
            }
            Service(d, x, ref y, "Strip out junk", run.JunkCount() == 0 ? "No junk cards in the deck." : $"Removes one of {run.JunkCount()} junk cards for good.",
                cfg.RemoveJunkCost, run.JunkCount() > 0, () => Shop(() => RunRules.RemoveJunk(run, content)));
            int keep = run.Deck.Count(card => card.Ship >= 0);
            Service(d, x, ref y, "Strike a card", keep > cfg.MinDeckSize ? "Remove any one card for good. A thin deck draws its best cards more often." : $"The deck is at its floor of {cfg.MinDeckSize} cards.",
                cfg.RemoveCardCost, keep > cfg.MinDeckSize, ShowStrikePicker);
            var fct = content.Faction;
            string craft = fct.CraftName.TrimEnd('s');
            Service(d, x, ref y, $"Load a {craft}", $"{Cap(fct.CraftName)} aboard {run.Darts}/{run.MaxDarts}. Takes no dock time.", cfg.DartCost,
                run.Darts < run.MaxDarts, () => Shop(() => RunRules.BuyDart(run, content)));
            Service(d, x, ref y, fct.HasCouncil ? $"Sign on {cfg.OrdinalsPerPurchase} crew" : $"Decant {cfg.OrdinalsPerPurchase} Ordinals", $"{fct.CrewName} {run.Ordinals}/{run.MaxOrdinals}.", cfg.OrdinalCost * cfg.OrdinalsPerPurchase,
                run.Ordinals < run.MaxOrdinals, () => Shop(() => RunRules.BuyOrdinals(run, content)));
            if (run.Officers.Count > 0)
            {
                int wounded = run.Officers.Count(o => o.Wounded);
                Service(d, x, ref y, "Sickbay", wounded == 0 ? "No officer is wounded." : $"Returns one of {wounded} wounded officers to duty.", cfg.HealOfficerCost,
                    wounded > 0, () => Shop(() => RunRules.HealOfficer(run, content)));
            }

            Service(d, x, ref y, "Seed false beacons", shop.Sabotaged ? "Already seeded from this yard." : $"Both rival fleets lose {cfg.SabotageAmount / 10f:0.0} node. Takes no dock time.",
                cfg.SabotageCost, !shop.Sabotaged, () => Shop(() => RunRules.Sabotage(run, content)));

            // ---- cards
            float cx = 580;
            Ui.Stamp(d, "Cards for sale", 13, cx, 140, 300, 18, TextAlignmentOptions.Left, Skin.Brass);
            for (int i = 0; i < shop.Cards.Count; i++)
            {
                int index = i;
                float ccx = cx + CardView.W / 2f + i * (CardView.W + 8);
                var card = MakeCard(d, shop.Cards[i], ccx, 164 + CardView.H);
                card.Clicked = _ => Shop(() => RunRules.BuyCard(run, index));
                card.Hovered = v => v.SetSelected(true);
                card.Unhovered = v => v.SetSelected(false);
                var buy = Ui.Btn(d, $"{shop.CardPrices[i]} scrap", ccx - 90, 468, 180, 40, () => Shop(() => RunRules.BuyCard(run, index)), 16);
                buy.interactable = run.Scrap >= shop.CardPrices[i];
            }
            if (shop.Cards.Count == 0) Ui.Body(d, "Sold out.", 18, cx, 180, 300, 26, TextAlignmentOptions.TopLeft, Skin.Dim);

            // ---- the fleet as it stands
            float fx = 1290, fy = 140;
            Ui.Stamp(d, "In dock", 13, fx, fy, 300, 18, TextAlignmentOptions.Left, Skin.Brass);
            fy += 28;
            foreach (var ship in run.Ships)
            {
                Ui.Heading(d, ship.Name, 16, fx, fy + 6, 300, 20, TextAlignmentOptions.Left, ship.Alive ? Skin.Text : Skin.Faint);
                Ui.Number(d, ship.Alive ? $"{ship.Hull}/{ship.MaxHull}" : "LOST", 12, fx + 290, fy + 10, 140, 14, TextAlignmentOptions.Right, ship.Alive ? Skin.Text : Skin.Bad);
                float frac = ship.Alive ? Mathf.Clamp01(ship.Hull / (float)ship.MaxHull) : 0f;
                Ui.Bar(d, "Hull", fx, fy + 32, 430, 18, frac, frac > 0.34f ? Skin.Good : Skin.Bad);
                fy += 66;
            }
            Ui.Body(d, $"{content.Faction.CrewName} {run.Ordinals}/{run.MaxOrdinals}     {Cap(content.Faction.CraftName)} {run.Darts}/{run.MaxDarts}\nDeck {run.Deck.Count} cards, {run.JunkCount()} junk", 17, fx, fy, 440, 50, TextAlignmentOptions.TopLeft, Skin.Dim);
            fy += 58;
            for (int slot = 0; slot < run.Modules.Count; slot++)
            {
                var fitted = content.Module(run.Modules[slot]);
                Ui.Stamp(d, fitted.Slot, 11, fx, fy + 3, 110, 16);
                Ui.Body(d, fitted.Name, 16, fx + 120, fy, 320, 22);
                fy += 23;
            }

            // ---- refits
            float mx = 580, my = 530;
            Ui.Stamp(d, $"Refit a module - {cfg.ModuleCost} scrap each. The old module's cards leave the deck, the new one's join it.", 12, mx, my, 1140, 14, TextAlignmentOptions.Left, Skin.Brass);
            my += 22;
            for (int i = 0; i < shop.Modules.Count && i < 4; i++)
            {
                var m = content.Module(shop.Modules[i]);
                int slot = cfg.Slots.IndexOf(m.Slot);
                var old = slot >= 0 && slot < run.Modules.Count ? content.Module(run.Modules[slot]) : null;
                var row = Ui.Img(d, "Module", Skin.Slot, mx, my, 1140, 88, Color.white, true);
                Ui.Stamp(row.transform, m.Slot, 12, 16, 10, 140, 16, TextAlignmentOptions.Left, Skin.Brass);
                Ui.Heading(row.transform, m.Name, 16, 16, 30, 300, 20);
                Ui.Stamp(row.transform, m.Text, 12, 16, 60, 300, 14, TextAlignmentOptions.Left, Skin.Dim).textWrappingMode = TextWrappingModes.NoWrap;
                string adds = string.Join(", ", m.Cards.Select(c => PaperCards.Get(c).Name));
                string loses = old != null ? string.Join(", ", old.Cards.Select(c => PaperCards.Get(c).Name)) : "";
                Ui.Body(row.transform, $"<color=#8cc47a>Adds:</color> {adds}\n<color=#d9573f>Replaces {(old != null ? old.Name : "")}:</color> {loses}", 16, 330, 14, 600, 60, TextAlignmentOptions.TopLeft);
                string id = m.Id;
                var fit = Ui.Btn(row.transform, "Fit", 954, 20, 170, 48, () => Shop(() => RunRules.FitModule(run, content, id)), 16);
                fit.interactable = run.Scrap >= cfg.ModuleCost;
                my += 94;
            }
        }

        /// <summary>Pick the card to strike: the deck at full size, click one to remove it.</summary>
        void ShowStrikePicker()
        {
            var cfg = content.Config;
            var d = OpenDialog(1880, 1040, 0.85f);
            Ui.Heading(d, "Strike a card", 32, 36, 26, 900, 36);
            Ui.Body(d, $"Click a card to remove it from the deck for good. {cfg.RemoveCardCost} scrap; you have {run.Scrap}.", 16, 38, 70, 1200, 16, TextAlignmentOptions.TopLeft, Skin.Dim);
            Ui.Btn(d, "Cancel", 1690, 26, 150, 48, Refresh, 16);
            var entries = Enumerable.Range(0, run.Deck.Count).Where(i => run.Deck[i].Ship >= 0)
                .OrderBy(i => run.Deck[i].Ship).ThenBy(i => PaperCards.Get(run.Deck[i].Card).Type).ThenBy(i => run.Deck[i].Card).ToList();
            const int perRow = 8;
            int rows = Mathf.Max(1, (entries.Count + perRow - 1) / perRow);
            float cw = CardView.W + 6, top = 104, room = 1040 - top - 26 - CardView.H;
            float rowStep = rows <= 1 ? 0 : Mathf.Min(CardView.H + 10, Mathf.Floor(room / (rows - 1)));
            float x0 = Mathf.Round((1880 - perRow * cw) / 2f);
            for (int n = 0; n < entries.Count; n++)
            {
                int index = entries[n];
                var view = MakeCard(d, run.Deck[index], x0 + (n % perRow) * cw + CardView.W / 2f, top + (n / perRow) * rowStep + CardView.H);
                view.Hovered = v => { v.transform.SetAsLastSibling(); v.SetSelected(true); };
                view.Unhovered = v => v.SetSelected(false);
                view.Clicked = _ => Shop(() => RunRules.RemoveCard(run, content, index));
            }
        }

        void Service(RectTransform d, float x, ref float y, string title, string detail, int cost, bool available, System.Action onClick)
        {
            var row = Ui.Img(d, "Service", Skin.Slot, x, y, 520, 84, Color.white, true);
            Ui.Heading(row.transform, title, 16, 16, 14, 340, 20, TextAlignmentOptions.Left, available ? Skin.Text : Skin.Faint);
            Ui.Stamp(row.transform, detail, 12, 16, 42, 340, 30, TextAlignmentOptions.TopLeft, Skin.Dim);
            Tip.On(row, title, () => detail + "\n\n" + Gloss.Legend(title + ". " + detail + " scrap"));
            var b = Ui.Btn(row.transform, $"{cost} scrap", 364, 18, 140, 48, onClick, 16);
            b.interactable = available && run.Scrap >= cost && cost > 0;
            y += 92;
        }

        // ---------------------------------------------------------------- run over

        void ShowRunOver()
        {
            var d = OpenDialog(1100, 760, 0.82f);
            bool won = run.Result == RunResult.Victory;
            string title = won ? "The Gate Is Yours" : run.Result == RunResult.Outpaced ? "Outpaced" : "The Fleet Is Lost";
            var color = won ? Skin.Brass : run.Result == RunResult.Outpaced ? Skin.Warn : Skin.Bad;
            Ui.Label(d, title, Skin.Logo, 64, color, 0, 44, 1100, 80, TextAlignmentOptions.Center);

            var fx = content.Faction;
            string story;
            if (won)
            {
                var last = fx.SectorInfo(run.Sector);
                story = RunRules.ConcordAhead(run) ? (last.WinBehind != "" ? last.WinBehind : fx.WinBehind) : (last.WinAhead != "" ? last.WinAhead : fx.WinAhead);
                if (run.Nemesis.LastOutcome == NemesisOutcome.Killed && run.Nemesis.Id != "") story += $" {content.Nemesis(run.Nemesis.Id).Name} did not live to see it.";
                if (RunRules.ChorusAhead(run)) story += " " + fx.WinThirdAhead;
            }
            else if (run.Result == RunResult.Outpaced) story = fx.SectorInfo(run.Sector).Outpaced != "" ? fx.SectorInfo(run.Sector).Outpaced : fx.Outpaced;
            else story = RunRules.ConcordAhead(run) ? fx.LostAhead : fx.LostBehind;
            Ui.Body(d, story, 22, 80, 156, 940, 130, TextAlignmentOptions.Top);

            var stats = new[]
            {
                ("Sector reached", run.Sector.ToString()), ("Battles won", run.BattlesWon.ToString()), ("Turns fought", run.TurnsFought.ToString()),
                ("Scrap", run.Scrap.ToString()), (content.Faction.CrewName + " left", run.Ordinals.ToString()), ("Score", RunRules.Score(run).ToString()),
            };
            for (int i = 0; i < stats.Length; i++)
            {
                float x = 110 + (i % 3) * 300, y = 250 + (i / 3) * 86;
                Ui.Stamp(d, stats[i].Item1, 13, x, y, 280, 18);
                Ui.Number(d, stats[i].Item2, 36, x, y + 22, 280, 40, TextAlignmentOptions.Left, i == stats.Length - 1 ? Skin.Brass : Skin.Text);
            }
            Ui.Stamp(d, $"Seed {run.Seed}", 12, 110, 440, 400, 14);
            var lines = run.Journal.Skip(System.Math.Max(0, run.Journal.Count - 4));
            Ui.Body(d, string.Join("\n", lines), 16, 110, 464, 880, 90, TextAlignmentOptions.TopLeft, Skin.Dim);

            Ui.Btn(d, "New run", 250, 650, 280, 70, () => { long seed = Game.RandomSeed(); string faction = run.Faction; Game.AbandonRun(); Game.NewRun(seed, faction); }, 16);
            Ui.Btn(d, "Main menu", 570, 650, 280, 70, () => { Game.AbandonRun(); Game.ToMenu(); }, 16);
        }
    }

    static class UnitSpecExtensions
    {
        public static bool IsCapitalSpec(this UnitSpec s) => s.Kind == UnitKind.Capital;
    }
}
