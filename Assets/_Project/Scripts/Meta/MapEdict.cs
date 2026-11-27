using Silence.Core.Run;
using Silence.Data;
using Silence.View;
using TMPro;
using UnityEngine;

namespace Silence.Meta
{
    /// <summary>The Prime's Edict for the sector: the two read out at its start, and the one in force under the fleet cards.</summary>
    public sealed partial class MapScreen
    {
        static readonly Color PrimeGold = new Color(1f, 0.94f, 0.72f);

        string EdictPenalty => $"Broken: an Inquisitor comes aboard (a junk card that costs 1 command whenever it is drawn) and {content.Config.EdictPenaltyOrdinals} Ordinals are taken for the audit.";

        /// <summary>Two orders, and no way to refuse both. Shown before the first jump of each sector.</summary>
        void ShowEdictChoice()
        {
            const float w = 1240, h = 640;
            var d = OpenDialog(w, h, 0.86f);
            Ui.Stamp(d, "From Lumen - by the hand of the Prime", 12, 40, 26, 700, 14, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Heading(d, "Edict of the Prime", 32, 38, 48, 800, 36);
            Ui.Body(d, "An Edict is obeyed or it is failed. It is not declined. Take one; it is judged when you reach the gate.", 16, 40, 100, w - 80, 16, TextAlignmentOptions.TopLeft, Skin.Dim);
            for (int i = 0; i < run.EdictChoices.Count; i++)
            {
                var e = content.Edict(run.EdictChoices[i]);
                int index = i;
                var b = Ui.Btn(d, "", 40 + i * 590, 140, 570, 400, () => TakeEdict(index));
                Ui.Heading(b.transform, e.Name, 16, 28, 26, 514, 20, TextAlignmentOptions.Left, PrimeGold);
                // The decree is the Prime's voice: plain label, not marked up as rules text.
                var decree = Ui.Label(b.transform, "\"" + e.Decree + "\"", Skin.BodyBold, 16, Skin.Hex("ffe0b0"), 28, 62, 514, 80, TextAlignmentOptions.TopLeft);
                decree.lineSpacing = 10f;
                Ui.Stamp(b.transform, "The order", 12, 28, 156, 300, 14, TextAlignmentOptions.Left, Skin.BrassDark);
                Ui.Body(b.transform, e.Text, 16, 28, 176, 514, 60, TextAlignmentOptions.TopLeft, Skin.Text);
                Ui.Stamp(b.transform, "Obeyed", 12, 28, 252, 300, 14, TextAlignmentOptions.Left, Skin.Good);
                Ui.Body(b.transform, Cap(RunRules.EdictReward(content, e)) + ".", 16, 28, 272, 514, 40, TextAlignmentOptions.TopLeft, Skin.Text);
                Ui.Stamp(b.transform, "Broken", 12, 28, 320, 300, 14, TextAlignmentOptions.Left, Skin.Bad);
                Ui.Body(b.transform, $"An Inquisitor comes aboard, and {content.Config.EdictPenaltyOrdinals} Ordinals are taken.", 16, 28, 340, 514, 40, TextAlignmentOptions.TopLeft, Skin.Text);
                Tip.On(b.targetGraphic, e.Name, () => e.Text + "\n\n" + EdictPenalty, "fff0b8");
            }
            Ui.Body(d, "Leaving without choosing takes the first.", 16, 40, h - 60, w - 80, 16, TextAlignmentOptions.TopLeft, Skin.Faint);
        }

        public void TakeEdict(int index)
        {
            string error = RunRules.AcceptEdict(run, content, index);
            if (error == null) Sfx.Play("sfx_click");
            Commit(error);
        }

        /// <summary>The Edict in force, under the fleet: its name and how it stands. Hover for the order and what rides on it.</summary>
        float EdictPanel(float y)
        {
            if (string.IsNullOrEmpty(run.Edict.Id)) return y;
            var e = content.Edict(run.Edict.Id);
            var card = Ui.Panel(fleetRoot, "Edict", 0, y, FleetW, 66);
            Ui.Stamp(card.transform, "Edict of the Prime", 12, 16, 12, 220, 14, TextAlignmentOptions.Left, Skin.BrassDark);
            var name = Ui.Label(card.transform, e.Name.ToUpperInvariant(), Skin.Stencil, 12, PrimeGold, 16, 30, FleetW - 32, 14);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            bool broken = run.Edict.Verdict == 2 || run.Edict.Failed;
            bool good = run.Edict.Verdict == 1 || (!broken && RunRules.EdictObeyed(run, content));
            var state = Ui.Label(card.transform, RunRules.EdictProgress(run, content).ToUpperInvariant(), Skin.Stencil, 12, broken ? Skin.Bad : good ? Skin.Good : Skin.Warn, 16, 46, FleetW - 32, 14);
            state.textWrappingMode = TextWrappingModes.NoWrap;
            Tip.On(card, e.Name, () => "\"" + e.Decree + "\"\n\n" + e.Text + "\n\nObeyed: " + RunRules.EdictReward(content, e) + ".\n" + EdictPenalty +
                                       (run.Edict.Verdict == 0 ? "\n\nJudged when you reach the gate." : ""), "fff0b8");
            return y + 74;
        }

        /// <summary>One line for the gate briefing: how the Edict was judged on arriving.</summary>
        string EdictVerdictLine()
        {
            if (string.IsNullOrEmpty(run.Edict.Id) || run.Edict.Verdict == 0) return "";
            var e = content.Edict(run.Edict.Id);
            return run.Edict.Verdict == 1
                ? $"<color=#8cc47a>The Edict was obeyed: {e.Name}.</color> {Cap(RunRules.EdictReward(content, e))}."
                : $"<color=#d9573f>The Edict was broken: {e.Name}.</color> An Inquisitor has come aboard, and {content.Config.EdictPenaltyOrdinals} Ordinals are gone.";
        }
    }
}
