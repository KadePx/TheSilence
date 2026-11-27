using Silence.Core.Run;
using Silence.Data;
using Silence.View;
using TMPro;
using UnityEngine;

namespace Silence.Meta
{
    /// <summary>The opening briefing (story, then how the map works) and the choice of relic from an elite's hold.</summary>
    public sealed partial class MapScreen
    {
        /// <summary>Shown once, before the first jump: why you are here, then the four things the map asks of you.</summary>
        void ShowBriefing()
        {
            var f = content.Faction;
            const float w = 1500, h = 820;
            var d = OpenDialog(w, h, 0.86f);
            // Laid out like an event: the picture on the left, the words beside it.
            var well = Ui.Well(d, "ArtWell", 34, 34, 700, 396);
            var art = ArtLibrary.Instance.Sprite("event_marker");
            if (art != null) Ui.Img(well.transform, "Art", art, 6, 6, art.rect.width * 2f, art.rect.height * 2f);
            Ui.Stamp(d, f.Name + " - " + f.SectorName, 12, 760, 34, 700, 14, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Label(d, "The Silence", Skin.Logo, 64, Skin.Hex("eef1f6"), 756, 50, 700, 76, TextAlignmentOptions.Left);
            var text = Ui.Label(d, f.Briefing, Skin.Body, 16, Skin.Text, 760, 140, 700, 300, TextAlignmentOptions.TopLeft);
            text.lineSpacing = 20f;

            Ui.Stamp(d, "How the Reef works", 12, 48, 462, 600, 14, TextAlignmentOptions.Left, Skin.Brass);
            var tips = new[]
            {
                ("Jump", "Click a ringed node to go there. The map only goes forward."),
                ("Race", $"Every jump moves the {f.RivalName} and the {f.ThirdName} along the track at the top. Long fights and time in dock move them further."),
                ("Fall behind", $"If the {f.RivalName} gets a node ahead it mines the lanes, and it reinforces the gate if it arrives first. Dotted jump lanes and false beacons win ground back."),
                ("Hunted", "The red crosshair is a commander waiting for you. Their ship breaks off when it is hurt, and comes back knowing how you fight."),
                ("Hazards", "A violet label under a fight is a storm, a nebula or a dense field. It changes the rules of that fight and pays more salvage."),
            };
            float y = 490;
            foreach (var (head, body) in tips)
            {
                Ui.Label(d, head, Skin.Heading, 16, Skin.BrassLight, 48, y, 200, 20);
                var line = Ui.Body(d, body, 16, 250, y + 1, w - 300, 40, TextAlignmentOptions.TopLeft, Skin.Dim);
                line.ForceMeshUpdate();
                y += Mathf.Max(20f, Mathf.Ceil(line.preferredHeight)) + 14;
            }
            Ui.Btn(d, "Set course", (w - 320) / 2f, h - 104, 320, 70, CloseBriefing, 32);
        }

        public void CloseBriefing()
        {
            RunRules.DismissBriefing(run);
            Sfx.Play("sfx_click");
            Commit(null);
        }

        /// <summary>An elite's hold: two relics, take one. It comes before the rest of the salvage.</summary>
        void ShowRelicChoice()
        {
            var offer = run.Reward;
            const float w = 1040, h = 470;
            var d = OpenDialog(w, h);
            Ui.Stamp(d, "After action", 12, 40, 26, 400, 14, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Heading(d, "From their hold", 32, 38, 48, 700, 36);
            Ui.Body(d, "Two things worth carrying, and room for one. A relic changes a rule for the rest of the run.", 16, 40, 100, w - 80, 16, TextAlignmentOptions.TopLeft, Skin.Dim);
            for (int i = 0; i < offer.RelicChoices.Count; i++)
            {
                var relic = content.Relic(offer.RelicChoices[i]);
                int index = i;
                var b = Ui.Btn(d, "", 40 + i * 490, 150, 470, 270, () => PickRelic(index));
                Ui.Art(b.transform, ArtOr(relic.Icon, "icon_relic"), 187, 22, 2f).raycastTarget = false;
                Ui.Heading(b.transform, relic.Name, 16, 20, 134, 430, 20, TextAlignmentOptions.Center, Skin.Hex("ffc46a"));
                Ui.Body(b.transform, relic.Text, 16, 30, 170, 410, 80, TextAlignmentOptions.Top, Skin.Text);
            }
        }

        public void PickRelic(int index)
        {
            string error = RunRules.ChooseRelic(run, content, index);
            if (error == null) Sfx.Play("sfx_coin");
            Commit(error);
        }
    }
}
