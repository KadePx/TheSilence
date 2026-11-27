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
                Tip.On(b.targetGraphic, relic.Name, () => relic.Text + "\n\n" + Gloss.Legend(relic.Text + " relic"), Gloss.Relic);
                Ui.Art(b.transform, ArtOr(relic.Icon, "icon_relic"), 187, 22, 2f).raycastTarget = false;
                Ui.Heading(b.transform, relic.Name, 16, 20, 134, 430, 20, TextAlignmentOptions.Center, Skin.Hex("ffc46a"));
                Ui.Body(b.transform, relic.Text, 16, 30, 170, 410, 80, TextAlignmentOptions.Top, Skin.Text);
            }
        }

        /// <summary>
        /// Between sectors: the gate is taken, the fleet has been through its yards, and there is one thing to carry
        /// off before the jump. What lies beyond is told here, like an event.
        /// </summary>
        void ShowSectorClear()
        {
            var next = content.Faction.SectorInfo(run.Sector + 1);
            const float w = 1500, h = 810;
            var d = OpenDialog(w, h, 0.86f);
            var well = Ui.Well(d, "ArtWell", 34, 34, 700, 396);
            var art = ArtLibrary.Instance.Sprite(next.MapArt) ?? ArtLibrary.Instance.Sprite("event_wrecks");
            if (art != null)
            {
                // The next sector's sky, cropped to the well at its own size.
                var mask = Ui.Box(well.transform, "Mask", 6, 6, 688, 384);
                mask.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
                Ui.Img(mask, "Art", art, 0, 0, art.rect.width, art.rect.height);
            }
            Ui.Stamp(d, "The gate is yours", 12, 760, 34, 700, 14, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Heading(d, next.Name, 32, 758, 56, 700, 36);
            var text = Ui.Label(d, next.Arrival, Skin.Body, 16, Skin.Text, 760, 116, 700, 240, TextAlignmentOptions.TopLeft);
            text.lineSpacing = 20f;
            // One colour: this is a single piece of good news, not six rules words.
            Ui.Label(d, $"In the gate's yards: every hull mended, a lost escort replaced, junk stripped, the wounded back on duty, {content.Faction.CraftName} and {content.Faction.CrewName.ToLowerInvariant()} made up.",
                Skin.Body, 16, Skin.Good, 760, 362, 700, 60, TextAlignmentOptions.TopLeft);

            Ui.Stamp(d, run.SectorRelics.Count > 0 ? "From the wreck of the gate fleet - take one" : "Nothing left worth taking", 12, 40, 458, 800, 14, TextAlignmentOptions.Left, Skin.Brass);
            for (int i = 0; i < run.SectorRelics.Count; i++)
            {
                var relic = content.Relic(run.SectorRelics[i]);
                int index = i;
                var b = Ui.Btn(d, "", 40 + i * 478, 484, 464, 250, () => NextSector(index));
                Tip.On(b.targetGraphic, relic.Name, () => relic.Text + "\n\n" + Gloss.Legend(relic.Text + " relic"), Gloss.Relic);
                Ui.Art(b.transform, ArtOr(relic.Icon, "icon_relic"), 184, 18, 2f).raycastTarget = false;
                Ui.Heading(b.transform, relic.Name, 16, 20, 126, 424, 20, TextAlignmentOptions.Center, Skin.Hex("ffc46a"));
                Ui.Body(b.transform, relic.Text, 16, 30, 160, 404, 80, TextAlignmentOptions.Top, Skin.Text);
            }
            if (run.SectorRelics.Count == 0) Ui.Btn(d, "Jump", (w - 320) / 2f, 560, 320, 70, () => NextSector(-1), 32);
            Ui.Body(d, "Choosing one takes the fleet through the gate. The race carries over: nobody starts more than a node and a half apart.", 16, 40, h - 56, w - 80, 16, TextAlignmentOptions.TopLeft, Skin.Dim);
        }

        public void NextSector(int relic)
        {
            string error = RunRules.EnterNextSector(run, content, relic);
            if (error == null) Sfx.Play("sfx_jump");
            Commit(error);
        }

        public void PickRelic(int index)
        {
            string error = RunRules.ChooseRelic(run, content, index);
            if (error == null) Sfx.Play("sfx_coin");
            Commit(error);
        }
    }
}
