using System.Linq;
using Silence.Core.Run;
using Silence.Data;
using Silence.View;
using TMPro;
using UnityEngine;

namespace Silence.Meta
{
    /// <summary>For a fleet that votes: the officers aboard, under the fleet cards, and the Council's motions.</summary>
    public sealed partial class MapScreen
    {
        /// <summary>A sprite name if the library has it, otherwise the stand-in.</summary>
        static string ArtOr(string name, string fallback) => ArtLibrary.Instance.Sprite(name) != null ? name : fallback;

        string CrewIcon => content.Faction.Id == ContentLoader.Ascendancy ? "res_ordinals" : ArtOr("res_crew", "res_ordinals");
        string CraftIcon => content.Faction.Id == ContentLoader.Ascendancy ? "res_darts" : ArtOr("res_gunships", "res_darts");
        static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>Officers aboard and the door to the Council. Hovering an officer says what they do.</summary>
        float OfficerPanel(float y)
        {
            if (!content.Faction.HasCouncil) return y;
            float h = 34 + Mathf.Max(1, run.Officers.Count) * 18 + 10;
            var card = Ui.Panel(fleetRoot, "Officers", 0, y, FleetW, h);
            Ui.Stamp(card.transform, $"Officers {run.Officers.Count}/{content.Faction.MaxOfficers}", 12, 16, 12, 200, 14, TextAlignmentOptions.Left, Skin.BrassDark);
            if (run.Officers.Count == 0) Ui.Stamp(card.transform, "Nobody aboard", 12, 16, 34, 260, 14, TextAlignmentOptions.Left, Skin.Faint);
            for (int i = 0; i < run.Officers.Count; i++)
            {
                var state = run.Officers[i];
                var def = content.Officer(state.Id);
                var line = Ui.Stamp(card.transform, def.Name, 12, 16, 34 + i * 18, 150, 14, TextAlignmentOptions.Left, state.Wounded ? Skin.Faint : Skin.Text);
                Ui.Stamp(card.transform, state.Wounded ? "Wounded" : def.Role, 12, 150, 34 + i * 18, 134, 14, TextAlignmentOptions.Right, state.Wounded ? Skin.Bad : Skin.BrassDark)
                    .textWrappingMode = TextWrappingModes.NoWrap;
                line.raycastTarget = true;
                var relay = line.gameObject.AddComponent<PointerRelay>();
                relay.Enter = () => SetInfo($"<color=#c9a24a>{def.Role.ToUpperInvariant()}</color>  {def.Name}: {def.Text}" +
                                            (state.Wounded ? "   <color=#d9573f>Wounded: no effect until a yard or the Council sees to it.</color>" : ""));
                relay.Exit = () => SetInfo(DefaultInfo());
            }
            y += h + 8;
            var council = Ui.Btn(fleetRoot, $"Council - approval {run.Approval}/{content.Faction.MaxApproval}", 0, y, FleetW, 40, ShowCouncil, 16);
            council.interactable = run.Phase == RunPhase.Map;
            return y + 50;
        }

        /// <summary>The order paper: every motion, what it costs in approval and why it can't be moved right now.</summary>
        void ShowCouncil()
        {
            float h = 190 + content.Motions.Count * 92 + 40;
            const float w = 1100;
            var d = OpenDialog(w, h, 0.8f);
            Ui.Stamp(d, "The Council", 12, 40, 26, 400, 14, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Heading(d, "Put a motion", 32, 38, 48, 600, 36);
            Ui.Number(d, $"Approval {run.Approval}/{content.Faction.MaxApproval}", 24, 620, 54, 280, 24, TextAlignmentOptions.Right, Skin.Brass);
            Ui.Btn(d, "Close", w - 190, 36, 150, 48, Refresh, 16);
            Ui.Body(d, "A clean win earns 1 approval, an elite 2. A win that costs a ship earns none; a stand-off loses 1. Votes are held between jumps and take no time.",
                16, 40, 104, w - 80, 40, TextAlignmentOptions.TopLeft, Skin.Dim);
            if (!string.IsNullOrEmpty(notice)) Ui.Body(d, $"<color=#f0a040>{notice}</color>", 16, 40, 150, w - 80, 16);

            float y = 176;
            for (int i = 0; i < content.Motions.Count; i++)
            {
                var m = content.Motions[i];
                string blocked = RunRules.CanCallVote(run, content, m);
                int index = i;
                var row = Ui.Img(d, "Motion", Skin.Slot, 40, y, w - 80, 84, Color.white, true);
                Ui.Heading(row.transform, m.Name, 16, 16, 14, 600, 20, TextAlignmentOptions.Left, blocked == null ? Skin.Text : Skin.Faint);
                Ui.Stamp(row.transform, m.Text + (blocked != null ? $"   <color=#d9573f>({blocked})</color>" : ""), 12, 16, 46, 760, 14, TextAlignmentOptions.Left, Skin.Dim);
                var vote = Ui.Btn(row.transform, $"{m.Cost} approval", w - 80 - 196, 18, 180, 48, () => Vote(index), 16);
                vote.interactable = blocked == null;
                y += 92;
            }
        }

        /// <summary>Call a vote, then show the order paper again with what is left.</summary>
        public void Vote(int index)
        {
            string error = RunRules.CallVote(run, content, index);
            if (error == null) Sfx.Play("sfx_coin");
            Commit(error);
            if (run.Phase == RunPhase.Map) ShowCouncil();
        }
    }
}
