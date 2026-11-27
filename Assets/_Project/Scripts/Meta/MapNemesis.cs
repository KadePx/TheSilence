using System.Collections.Generic;
using System.Linq;
using Silence.Core.Battle;
using Silence.Core.Run;
using Silence.Data;
using Silence.View;
using TMPro;
using UnityEngine;

namespace Silence.Meta
{
    /// <summary>The nemesis on the map: the hail before a fight with the hunting commander, their dossier, and the relic shelf.</summary>
    public sealed partial class MapScreen
    {
        static readonly Color ConcordGold = new Color(1f, 0.77f, 0.42f);

        static string Roman(int n) => n <= 1 ? "I" : n == 2 ? "II" : "III";

        /// <summary>What the commander says, chosen by what happened last time.</summary>
        string HailLine(NemesisDef def)
        {
            var n = run.Nemesis;
            string line = n.Meetings == 0 ? def.HailFirst
                : n.Rank >= 3 ? def.HailFinal
                : n.LastOutcome == NemesisOutcome.Standoff ? def.HailStandoff
                : def.HailEscaped;
            string scar = n.Scars.Count > 0 ? n.Scars[n.Scars.Count - 1] : "a week in dock";
            return "\"" + line.Replace("{scar}", scar) + "\"";
        }

        /// <summary>One line for the map's info bar and the node tooltip.</summary>
        string NemesisSummary()
        {
            var n = run.Nemesis;
            var def = content.Nemesis(n.Id);
            string s = $"{def.Name}, rank {Roman(n.Rank)}, grudge {n.Grudge}/3. {def.Trait}";
            if (n.Adaptation != "") s += " " + RunRules.AdaptationText(n.Adaptation) + ".";
            return s;
        }

        /// <summary>History, refits and scars: everything known about the commander, as short lines.</summary>
        List<string> Dossier(NemesisDef def)
        {
            var n = run.Nemesis;
            var lines = new List<string> { def.Trait };
            if (n.Meetings == 0) lines.Add("No history. This is the first contact.");
            else
            {
                string last = n.LastOutcome == NemesisOutcome.Standoff ? "held you to a stand-off"
                    : n.LastOutcome == NemesisOutcome.Dodged ? "you slipped past them" : "broke off and jumped clear";
                lines.Add($"Met {n.Meetings} time{(n.Meetings == 1 ? "" : "s")}. Last time: {last}.");
            }
            foreach (string scar in n.Scars) lines.Add("Scar: " + scar + ".");
            if (n.Adaptation != "") lines.Add("Refit: " + RunRules.AdaptationText(n.Adaptation) + ".");
            lines.Add(n.Rank >= 3
                ? "Rank III: this time the ship fights to the end."
                : $"Breaks off at {content.Config.NemesisFleePercent} percent hull, after the enemy has acted. Take the ship from above that to nothing in one turn to finish it.");
            return lines;
        }

        void CommanderPlate(RectTransform d, NemesisDef def, float x, float y)
        {
            var n = run.Nemesis;
            var well = Ui.Well(d, "Comms", x, y, 272, 272);
            // Once they have a scar from you, they wear it.
            string face = n.Scars.Count > 0 && ArtLibrary.Instance.Sprite(def.Portrait + "_scarred") != null ? def.Portrait + "_scarred" : def.Portrait;
            var portrait = Ui.Art(well.transform, face, 8, 8, 2f);
            if (!portrait.enabled) Ui.Body(well.transform, "NO SIGNAL", 16, 0, 124, 272, 20, TextAlignmentOptions.Center, Skin.Faint);
            Ui.Heading(d, def.Name, 16, x - 20, y + 282, 312, 20, TextAlignmentOptions.Center, ConcordGold);
            Ui.Stamp(d, $"{def.Title} - {UnitLibrary.Get(def.Ship).Name}", 12, x - 20, y + 306, 312, 14, TextAlignmentOptions.Center, Skin.Dim);
            Ui.Stamp(d, $"Rank {Roman(n.Rank)}", 12, x, y + 330, 130, 14, TextAlignmentOptions.Left, Skin.Bad);
            Ui.Stamp(d, $"Grudge {n.Grudge}/3", 12, x + 130, y + 330, 142, 14, TextAlignmentOptions.Right, n.Grudge >= 3 ? Skin.Bad : Skin.Warn);
            for (int i = 0; i < 3; i++)
                Ui.Fill(d, "Grudge", x + i * 92, y + 350, 88, 6, i < n.Grudge ? Skin.Bad : Skin.Faint);
        }

        void ShowHail()
        {
            var def = content.Nemesis(run.Nemesis.Id);
            var enc = RunRules.NemesisEncounter(run, content);
            var units = RunRules.Roster(run, content, enc);
            int rows = units.Select(u => u.Unit).Distinct().Count();
            var dossier = Dossier(def);
            const float w = 1500;
            float h = 580 + rows * 48 + content.HailReplies.Count * 68;
            var d = OpenDialog(w, h, 0.8f);
            Ui.Stamp(d, "Nemesis - incoming hail", 12, 40, 26, 500, 14, TextAlignmentOptions.Left, Skin.Bad);
            Ui.Heading(d, def.Name, 32, 38, 48, w - 80, 36);
            CommanderPlate(d, def, 60, 110);

            float tx = 390, tw = w - tx - 40, y = 112;
            // What they say, set apart in a comms well sized to the line.
            var speech = Ui.Well(d, "Speech", tx, y, tw, 80);
            var said = Ui.Label(speech.transform, HailLine(def), Skin.BodyBold, 16, Skin.Hex("ffe0b0"), 20, 18, tw - 40, 120, TextAlignmentOptions.TopLeft);
            said.ForceMeshUpdate();
            float speechH = Mathf.Ceil(said.preferredHeight) + 36;
            speech.rectTransform.sizeDelta = new Vector2(tw, speechH);
            y += speechH + 22;
            // Their ship, beside what is known about it.
            var hull = ArtLibrary.Instance.Sprite(UnitLibrary.Get(def.Ship).Id + "_E");
            float shipW = 0;
            if (hull != null)
            {
                shipW = 250;
                var dock = Ui.Well(d, "Hull", w - 40 - 240, y, 240, 230);
                Ui.Img(dock.transform, "Ship", hull, Mathf.Round((240 - hull.rect.width * 2f) / 2f), Mathf.Round((206 - hull.rect.height * 2f) / 2f), hull.rect.width * 2f, hull.rect.height * 2f);
                Ui.Stamp(dock.transform, UnitLibrary.Get(def.Ship).Name, 12, 0, 206, 240, 14, TextAlignmentOptions.Center, Skin.Dim);
            }
            Ui.Stamp(d, "Dossier", 12, tx, y, 300, 14, TextAlignmentOptions.Left, Skin.Brass);
            y += 22;
            foreach (string line in dossier)
            {
                var text = Ui.Body(d, line, 16, tx, y, tw - shipW, 40, TextAlignmentOptions.TopLeft, line.StartsWith("Scar") || line.StartsWith("Refit") ? Skin.Warn : Skin.Dim);
                text.ForceMeshUpdate();
                y += Mathf.Max(20f, Mathf.Ceil(text.preferredHeight)) + 6;
            }

            y = Mathf.Max(y + 10, 490);
            Ui.Stamp(d, "Enemy fleet", 12, 40, y, 300, 14, TextAlignmentOptions.Left, Skin.Brass);
            y += 22;
            y = FleetRows(d, units, 40, y, w - 80);

            y += 14;
            if (!string.IsNullOrEmpty(notice)) Ui.Body(d, $"<color=#f0a040>{notice}</color>", 16, 400, y - 16, 1000, 16);
            Ui.Stamp(d, "Your reply", 12, 40, y, 300, 14, TextAlignmentOptions.Left, Skin.Brass);
            y += 22;
            for (int i = 0; i < content.HailReplies.Count; i++)
            {
                var reply = content.HailReplies[i];
                string blocked = RunRules.CanReply(run, reply);
                int index = i;
                var b = Ui.Btn(d, "", 40, y, w - 80, 62, () => Reply(index));
                b.interactable = blocked == null;
                Ui.Number(b.transform, (i + 1).ToString(), 24, 22, 19, 40, 24, TextAlignmentOptions.Center, blocked == null ? Skin.Brass : Skin.Faint);
                Ui.Label(b.transform, reply.Label, Skin.BodyBold, 16, blocked == null ? Skin.Text : Skin.Faint, 80, 10, w - 200, 20);
                string tag = reply.Tag.ToUpperInvariant() + "   " + RunRules.HailEffect(content, reply) + (blocked != null ? $"   <color=#d9573f>({blocked})</color>" : "");
                Ui.Stamp(b.transform, tag, 12, 82, 36, w - 200, 14, TextAlignmentOptions.Left, blocked == null ? Skin.Brass : Skin.Faint);
                y += 68;
            }
        }

        /// <summary>Answer the hail and go straight into the fight.</summary>
        public void Reply(int index)
        {
            string error = RunRules.ChooseHail(run, content, index);
            if (error != null)
            {
                Commit(error);
                return;
            }
            Sfx.Play("sfx_click");
            Game.Save();
            Game.StartBattle();
        }

        /// <summary>One row per kind of enemy ship: icon, count, hull and what it does. Returns the y below the last row.</summary>
        float FleetRows(RectTransform d, List<EncounterUnit> units, float x, float y, float w)
        {
            foreach (var group in units.GroupBy(u => u.Unit))
            {
                var spec = UnitLibrary.Get(group.Key);
                var row = Ui.Img(d, "Unit", Skin.Slot, x, y, w, 44, Color.white, true);
                var icon = ArtLibrary.Instance.Sprite(spec.Id + "_E") ?? ArtLibrary.Instance.Sprite("con_outrider_E");
                if (icon != null)
                {
                    float s = icon.rect.height <= 40 ? 1f : 40f / Mathf.Max(icon.rect.width, icon.rect.height);
                    Ui.Img(row.transform, "Icon", icon, 8 + (40 - icon.rect.width * s) / 2f, 2 + (40 - icon.rect.height * s) / 2f, icon.rect.width * s, icon.rect.height * s);
                }
                string cycle = spec.Cycle.Length > 0
                    ? string.Join(" > ", spec.Cycle.Select(StepName))
                    : spec.Kind == UnitKind.Craft ? $"goes for the aft, {spec.AttackDamage} damage" : "";
                Ui.Label(row.transform, $"{group.Count()} x {spec.Name.ToUpperInvariant()}", Skin.Heading, 16, Skin.Text, 60, 13, 300, 20);
                Ui.Body(row.transform, $"Hull {spec.Hull}   " + cycle, 16, 330, 14, w - 340, 16, TextAlignmentOptions.TopLeft, Skin.Dim)
                    .textWrappingMode = TextWrappingModes.NoWrap;
                y += 48;
            }
            return y;
        }

        /// <summary>Relics held, under the fleet cards. Hovering one shows what it does in the info bar.</summary>
        float RelicShelf(float y)
        {
            if (run.Relics.Count == 0) return y;
            const int perRow = 5;
            int rows = (run.Relics.Count + perRow - 1) / perRow;
            float h = 34 + rows * 54 + 8;
            var card = Ui.Panel(fleetRoot, "Relics", 0, y, FleetW, h);
            Ui.Stamp(card.transform, "Relics", 12, 16, 12, 200, 14, TextAlignmentOptions.Left, Skin.BrassDark);
            for (int i = 0; i < run.Relics.Count; i++)
            {
                var relic = content.Relic(run.Relics[i]);
                var icon = Ui.Art(card.transform, ArtOr(relic.Icon, "icon_relic"), 18 + (i % perRow) * 54, 32 + (i / perRow) * 54, 1f);
                icon.raycastTarget = true;
                var relay = icon.gameObject.AddComponent<PointerRelay>();
                relay.Enter = () => SetInfo($"<color=#c9a24a>{(relic.Trophy ? "TROPHY" : "RELIC")}</color>  {relic.Name}: {relic.Text}");
                relay.Exit = () => SetInfo(DefaultInfo());
            }
            return y + h + 8;
        }
    }
}
