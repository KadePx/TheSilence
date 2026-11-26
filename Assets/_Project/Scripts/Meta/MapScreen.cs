using System;
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
    /// <summary>
    /// The sector map: branching nodes, the race track along the top, the fleet on the left. Whatever the run
    /// is waiting for (a fight to start, a reward, an event, a shipyard visit, the end) opens as a dialog
    /// over it. Every click goes through <see cref="RunRules"/> and is saved straight away.
    /// </summary>
    public sealed partial class MapScreen : MonoBehaviour
    {
        const float LeftW = 330f, TopH = 58f;

        RunState run;
        RunContent content;
        RectTransform root, mapRoot, fleetRoot, trackRoot, topRoot, overlay;
        TextMeshProUGUI infoText;
        readonly List<RectTransform> pulsing = new List<RectTransform>();
        string notice = "";

        public RunState Run => run;

        void Awake()
        {
            content = Game.Content;
            run = Game.Run ?? Game.EnsureRun();

            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = Skin.Ink;
            var canvas = Ui.Canvas("Map");
            canvas.transform.SetParent(transform, false);
            root = Ui.Root(canvas);
            Ui.Backdrop(root, "starmap_bg_sector1", new Color(0.72f, 0.72f, 0.8f));

            mapRoot = Ui.Box(root, "Map", 0, 0, Ui.RefW, Ui.RefH);
            var top = Ui.Plate(root, "TopBar", 0, 0, Ui.RefW, TopH);
            topRoot = Ui.Box(top.transform, "Content", 0, 0, Ui.RefW, TopH);
            var left = Ui.Plate(root, "Fleet", 0, TopH, LeftW, Ui.RefH - TopH);
            fleetRoot = Ui.Box(left.transform, "Content", 0, 0, LeftW, Ui.RefH - TopH);
            var track = Ui.Plate(root, "Race", LeftW + 10, TopH + 8, Ui.RefW - LeftW - 20, 112);
            trackRoot = Ui.Box(track.transform, "Content", 0, 0, Ui.RefW - LeftW - 20, 112);
            var info = Ui.Well(root, "Info", LeftW + 10, Ui.RefH - 58, Ui.RefW - LeftW - 20, 48);
            infoText = Ui.Body(info.transform, "", 19, 16, 9, Ui.RefW - LeftW - 60, 30, TextAlignmentOptions.Left);
            infoText.textWrappingMode = TextWrappingModes.NoWrap;
            infoText.overflowMode = TextOverflowModes.Ellipsis;

            overlay = Ui.Box(root, "Overlay", 0, 0, Ui.RefW, Ui.RefH);
            overlay.gameObject.SetActive(false);
            Refresh();
        }

        void Update()
        {
            float s = 1f + 0.07f * Mathf.Sin(Time.unscaledTime * 4f);
            foreach (var rt in pulsing)
                if (rt != null) rt.localScale = Vector3.one * s;
        }

        // ---------------------------------------------------------------- refresh

        void Commit(string error)
        {
            if (error != null)
            {
                notice = char.ToUpperInvariant(error[0]) + error.Substring(1) + ".";
                Sfx.Play("sfx_deny");
            }
            else
            {
                notice = "";
                Game.Save();
            }
            Refresh();
        }

        public void Refresh()
        {
            RefreshTop();
            RefreshFleet();
            RefreshTrack();
            RefreshMap();
            SetInfo(DefaultInfo());
            Ui.Clear(overlay);
            overlay.gameObject.SetActive(false);
            switch (run.Phase)
            {
                case RunPhase.Battle: ShowBattlePrompt(); break;
                case RunPhase.Reward: ShowReward(); break;
                case RunPhase.Event: ShowEvent(); break;
                case RunPhase.EventResult: ShowEventResult(); break;
                case RunPhase.Shipyard: ShowShipyard(); break;
                case RunPhase.Over: ShowRunOver(); break;
            }
        }

        string DefaultInfo()
        {
            if (!string.IsNullOrEmpty(notice)) return $"<color=#f0a040>{notice}</color>";
            if (run.Phase != RunPhase.Map) return "";
            return run.CurrentNode < 0
                ? "Choose where to enter the Ion Reef. Brass rings mark the nodes you can reach."
                : "Choose the next node. Every jump moves the rival fleets along the track above.";
        }

        void SetInfo(string text)
        {
            if (infoText != null) infoText.text = text;
        }

        void RefreshTop()
        {
            Ui.Clear(topRoot);
            Ui.Heading(topRoot, "Sector I - The Ion Reef", 32, 22, 6, 520, 46);
            float x = 620;
            x = Resource(x, "res_scrap", "Scrap", run.Scrap.ToString(), Skin.Brass);
            x = Resource(x, "res_ordinals", "Ordinals", $"{run.Ordinals}/{run.MaxOrdinals}", Skin.Cyan);
            x = Resource(x, "res_darts", "Darts", $"{run.Darts}/{run.MaxDarts}", Skin.Text);
            x = Resource(x, "icon_junk", "Junk", run.JunkCount().ToString(), run.JunkCount() > 0 ? Skin.Warn : Skin.Dim);
            Ui.Stamp(topRoot, "Seed", 12, 1500, 10, 60, 16);
            Ui.Number(topRoot, run.Seed.ToString(), 20, 1500, 26, 150, 26, TextAlignmentOptions.Left, Skin.Dim);
            Ui.Btn(topRoot, "Deck", 1640, 9, 110, 40, ShowDeck, 22);
            Ui.Btn(topRoot, "Menu", 1764, 9, 140, 40, () => { Game.Save(); Game.ToMenu(); }, 22);
        }

        float Resource(float x, string icon, string label, string value, Color color)
        {
            Ui.Art(topRoot, icon, x, 5, 1f);
            Ui.Stamp(topRoot, label, 12, x + 54, 9, 120, 16);
            Ui.Number(topRoot, value, 24, x + 54, 24, 120, 30, TextAlignmentOptions.Left, color);
            return x + 200;
        }

        void RefreshFleet()
        {
            Ui.Clear(fleetRoot);
            var t = fleetRoot;
            Ui.Heading(t, "Fleet", 30, 18, 12, 200, 40);
            float y = 58;
            for (int i = 0; i < run.Ships.Count; i++)
            {
                var ship = run.Ships[i];
                Ui.Stamp(t, i == 0 ? "Flagship" : "Escort", 12, 20, y, 200, 16, TextAlignmentOptions.Left, Skin.Brass);
                Ui.Heading(t, ship.Name, 28, 18, y + 14, 290, 36, TextAlignmentOptions.Left, ship.Alive ? Skin.Text : Skin.Faint);
                var well = Ui.Well(t, "Art", 16, y + 54, 298, i == 0 ? 120 : 84);
                well.gameObject.AddComponent<RectMask2D>();
                string spriteName = i == 0 ? "asc_cruiser_E128" : "asc_blade_E";
                var sprite = ArtLibrary.Instance.Sprite(spriteName);
                if (sprite != null)
                {
                    float sc = i == 0 ? 1f : 1f;
                    float w = sprite.rect.width * sc, h = sprite.rect.height * sc;
                    Ui.Img(well.transform, "Ship", sprite, (298 - w) / 2f, ((i == 0 ? 120 : 84) - h) / 2f, w, h, ship.Alive ? Color.white : new Color(0.25f, 0.25f, 0.28f));
                }
                y += i == 0 ? 182 : 146;
                if (!ship.Alive)
                {
                    Ui.Stamp(t, "Lost - rebuild at a shipyard", 13, 20, y, 290, 18, TextAlignmentOptions.Left, Skin.Bad);
                    y += 34;
                    continue;
                }
                Ui.Stamp(t, "Hull", 12, 20, y, 100, 16);
                Ui.Number(t, $"{ship.Hull}/{ship.MaxHull}", 19, 200, y - 4, 112, 22, TextAlignmentOptions.Right);
                Ui.Well(t, "HullWell", 16, y + 20, 298, 20);
                float frac = Mathf.Clamp01(ship.Hull / (float)ship.MaxHull);
                Ui.Fill(t, "HullFill", 19, y + 23, 292 * frac, 14, frac > 0.34f ? Skin.Good : Skin.Bad);
                y += 52;
                if (i != 0) continue;
                for (int slot = 0; slot < run.Modules.Count; slot++)
                {
                    var m = content.Module(run.Modules[slot]);
                    Ui.Stamp(t, m.Slot, 11, 22, y + 3, 96, 16);
                    Ui.Body(t, m.Name, 17, 124, y, 190, 22, TextAlignmentOptions.TopLeft);
                    y += 24;
                }
                y += 16;
            }

            y += 6;
            Ui.Stamp(t, $"Command deck - {run.Deck.Count} cards", 12, 20, y, 290, 16, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Btn(t, "View deck", 16, y + 22, 298, 44, ShowDeck, 24);
            y += 84;

            Ui.Stamp(t, "Log", 12, 20, y, 200, 16);
            float logH = Ui.RefH - TopH - y - 36;
            Ui.Well(t, "LogWell", 12, y + 20, LeftW - 24, logH);
            var lines = run.Journal.Skip(Math.Max(0, run.Journal.Count - 7));
            var log = Ui.Body(t, string.Join("\n", lines), 14, 22, y + 28, LeftW - 44, logH - 16, TextAlignmentOptions.BottomLeft, Skin.Dim);
            log.overflowMode = TextOverflowModes.Truncate;
            log.lineSpacing = -8f;
        }

        // ---------------------------------------------------------------- race track

        void RefreshTrack()
        {
            Ui.Clear(trackRoot);
            var t = trackRoot;
            float w = Ui.RefW - LeftW - 20;
            Ui.Stamp(t, "The race to the gate", 12, 20, 10, 300, 16, TextAlignmentOptions.Left, Skin.Brass);
            float x0 = 300, x1 = w - 120, yLine = 54;
            int length = RunRules.TrackLength(run);
            Ui.Fill(t, "Line", x0, yLine - 1, x1 - x0, 3, Skin.Faint);
            for (int i = 0; i <= run.BossLayer + 1; i++)
            {
                float x = x0 + (x1 - x0) * i / (run.BossLayer + 1);
                Ui.Fill(t, "Tick", x - 1, yLine - 6, 2, 13, Skin.Faint);
            }
            Ui.Art(t, "node_boss", x1 + 14, yLine - 24, 1f);
            Ui.Stamp(t, "Gate", 11, x1 + 64, yLine - 8, 60, 16);

            Marker(t, x0, x1, yLine, run.ConcordProgress, length, "Concord", Skin.Hex("ffc46a"), false, yLine + 28, 11);
            Marker(t, x0, x1, yLine, run.ChorusProgress, length, "Chorus", Skin.Hex("5fffc8"), false, yLine + 42, 11);
            Marker(t, x0, x1, yLine, run.PlayerProgress, length, "You", Color.white, true, yLine - 48, 14);

            Ui.Body(t, Lead("Concord", run.ConcordProgress) + "\n" + Lead("Chorus", run.ChorusProgress), 15, 20, 34, 270, 44, TextAlignmentOptions.TopLeft, Skin.Dim);
            Ui.Body(t, "Concord ahead at the gate: its guard is reinforced.", 13, 20, 84, 270, 20, TextAlignmentOptions.TopLeft, Skin.Faint);
        }

        string Lead(string name, int progress)
        {
            int d = progress - run.PlayerProgress;
            if (d == 0) return $"<color=#f0a040>{name} level with you</color>";
            string amount = (Math.Abs(d) / 10f).ToString("0.0");
            return d > 0
                ? $"<color=#d9573f>{name} {amount} node{(Math.Abs(d) == 10 ? "" : "s")} ahead</color>"
                : $"<color=#8cc47a>{name} {amount} behind</color>";
        }

        static void Marker(Transform t, float x0, float x1, float yLine, int progress, int length, string label, Color color, bool above, float labelY, float size)
        {
            float x = x0 + (x1 - x0) * Mathf.Clamp01(progress / (float)length);
            var arrow = Ui.Img(t, label, Skin.Arrow, 0, 0, 24, 24, color);
            arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            arrow.rectTransform.anchoredPosition = new Vector2(x, -(above ? yLine - 14 : yLine + 15));
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, above ? -90f : 90f);
            Ui.Stamp(t, label, size, x - 70, labelY, 140, 18, TextAlignmentOptions.Center, color).characterSpacing = 4f;
        }

        // ---------------------------------------------------------------- map

        Vector2 NodePos(MapNode n)
        {
            float x0 = LeftW + 130, x1 = Ui.RefW - 120;
            float y0 = 270, y1 = Ui.RefH - 170;
            return new Vector2(x0 + (x1 - x0) * n.Layer / run.BossLayer, y0 + (y1 - y0) * n.Lane / 30f);
        }

        static string KindName(NodeKind k) => k == NodeKind.Shipyard ? "Shipyard" : k == NodeKind.Boss ? "Sector boss" : k.ToString();

        static string KindIcon(NodeKind k)
        {
            switch (k)
            {
                case NodeKind.Battle: return "node_battle";
                case NodeKind.Elite: return "node_elite";
                case NodeKind.Event: return "node_event";
                case NodeKind.Shipyard: return "node_shipyard";
                default: return "node_boss";
            }
        }

        string Describe(MapNode n)
        {
            switch (n.Kind)
            {
                case NodeKind.Event: return "<color=#c9a24a>EVENT</color>  Something in the dark. A choice, not a fight - usually.";
                case NodeKind.Shipyard: return "<color=#c9a24a>SHIPYARD</color>  Repair, clear junk, buy cards, refit a module. Every service costs time.";
                default:
                {
                    var enc = content.Encounter(n.Encounter);
                    string tag = n.Kind == NodeKind.Boss ? "SECTOR BOSS" : n.Kind == NodeKind.Elite ? "ELITE" : "BATTLE";
                    string color = n.Kind == NodeKind.Battle ? "c9a24a" : "d9573f";
                    return $"<color=#{color}>{tag}</color>  {enc.Name}: {enc.Intro}";
                }
            }
        }

        void RefreshMap()
        {
            Ui.Clear(mapRoot);
            pulsing.Clear();
            var choices = RunRules.Choices(run);
            var visited = new HashSet<int>(run.Nodes.Where(n => n.Visited).Select(n => n.Id));

            foreach (var n in run.Nodes)
            {
                foreach (int id in n.Next)
                {
                    var m = run.Node(id);
                    bool walked = visited.Contains(n.Id) && visited.Contains(m.Id);
                    bool open = n.Id == run.CurrentNode && choices.Contains(m);
                    Line(mapRoot, NodePos(n), NodePos(m), walked ? Skin.Brass : open ? Skin.Alpha(Skin.Brass, 0.65f) : new Color(0.55f, 0.6f, 0.75f, 0.28f), walked || open ? 4f : 2f);
                }
            }

            foreach (var n in run.Nodes)
            {
                var p = NodePos(n);
                bool can = choices.Contains(n);
                bool here = n.Id == run.CurrentNode;
                float size = n.Kind == NodeKind.Boss ? 144f : 96f;
                if (can || here)
                {
                    var ring = Ui.Img(mapRoot, "Ring", Skin.Ring, p.x - size * 0.68f, p.y - size * 0.68f, size * 1.36f, size * 1.36f, here ? Color.white : Skin.Brass);
                    ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    ring.rectTransform.anchoredPosition = new Vector2(p.x, -p.y);
                    if (can) pulsing.Add(ring.rectTransform);
                }
                var sprite = ArtLibrary.Instance.Sprite(KindIcon(n.Kind));
                var tint = here ? Color.white : n.Visited ? new Color(0.4f, 0.4f, 0.45f) : can ? Color.white : new Color(0.72f, 0.72f, 0.78f);
                var img = Ui.Img(mapRoot, $"Node {n.Id}", sprite ?? Skin.Pip, p.x - size / 2f, p.y - size / 2f, size, size, tint);
                img.raycastTarget = true;
                var relay = img.gameObject.AddComponent<PointerRelay>();
                var node = n;
                relay.Enter = () => SetInfo(Describe(node) + (can ? "" : here ? "   <color=#8a90a0>(you are here)</color>" : node.Visited ? "   <color=#8a90a0>(visited)</color>" : ""));
                relay.Exit = () => SetInfo(DefaultInfo());
                relay.Click = _ => ClickNode(node.Id);
                var label = Ui.Stamp(mapRoot, KindName(n.Kind), 11, p.x - 80, p.y + size / 2f - 2, 160, 16, TextAlignmentOptions.Center, can ? Skin.Brass : Skin.Dim);
                label.characterSpacing = 4f;
            }

            if (run.CurrentNode < 0)
                Ui.Stamp(mapRoot, "Entry", 12, NodePos(run.Nodes[0]).x - 80, 196, 160, 18, TextAlignmentOptions.Center, Skin.Brass);
        }

        static void Line(Transform parent, Vector2 a, Vector2 b, Color color, float thickness)
        {
            var d = b - a;
            var img = Ui.Fill(parent, "Lane", a.x, a.y, d.magnitude, thickness, color);
            var rt = img.rectTransform;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(a.x, -a.y);
            rt.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        // ---------------------------------------------------------------- actions (also used by play-test scripts)

        public void ClickNode(int id)
        {
            if (run.Phase != RunPhase.Map) return;
            var node = run.Node(id);
            if (node == null) return;
            if (!RunRules.Choices(run).Contains(node))
            {
                if (!node.Visited && node.Id != run.CurrentNode) { notice = "That node is not connected to where you are."; SetInfo(DefaultInfo()); }
                return;
            }
            Sfx.Play("sfx_jump");
            Commit(RunRules.Travel(run, content, id));
        }

        public void Engage() => Game.StartBattle();

        public void PickReward(int index)
        {
            Sfx.Play(index >= 0 ? "sfx_card" : "sfx_click");
            Commit(RunRules.TakeReward(run, index));
        }

        public void FieldRepair()
        {
            Sfx.Play("sfx_click");
            Commit(RunRules.TakeFieldRepair(run, content));
        }

        public void Choose(int index) => Commit(RunRules.ChooseEvent(run, content, index));

        public void CloseEvent() => Commit(RunRules.CloseEvent(run));

        public void Shop(Func<string> action)
        {
            string error = action();
            if (error == null) Sfx.Play("sfx_coin");
            Commit(error);
        }

        public void LeaveShipyard() => Commit(RunRules.LeaveShipyard(run, content));

        // ---------------------------------------------------------------- shared dialog helpers

        RectTransform OpenDialog(float w, float h, float dim = 0.74f, string backdrop = null)
        {
            Ui.Clear(overlay);
            overlay.gameObject.SetActive(true);
            var shade = Ui.Fill(overlay, "Dim", 0, 0, Ui.RefW, Ui.RefH, new Color(0f, 0f, 0f, dim));
            shade.raycastTarget = true;
            if (backdrop != null && ArtLibrary.Instance.Sprite(backdrop) != null)
            {
                Ui.Backdrop(overlay, backdrop, new Color(0.55f, 0.55f, 0.6f));
                Ui.Fill(overlay, "Dim2", 0, 0, Ui.RefW, Ui.RefH, new Color(0f, 0f, 0f, 0.35f));
            }
            return Ui.Plate(overlay, "Dialog", (Ui.RefW - w) / 2f, (Ui.RefH - h) / 2f, w, h).rectTransform;
        }

        CardView MakeCard(Transform parent, DeckCard card, float centreX, float bottomY, float scale = 1f)
        {
            var def = PaperCards.Get(card.Card);
            string source = card.Ship >= 0 && card.Ship < run.Ships.Count ? run.Ships[card.Ship].Name : "";
            string art = def.IsJunk ? (def.Id == "fire_on_deck" ? "status_fire" : "status_warning")
                : def.Type == CardType.Hangar ? "asc_dart_E" : card.Ship == 1 ? "asc_blade_E" : "asc_cruiser_E128";
            var view = CardView.Create(parent, def, source, art, def.Cost);
            view.Rect.anchoredPosition = new Vector2(centreX, -bottomY);
            view.Rect.localScale = Vector3.one * scale;
            return view;
        }

        void ShowDeck()
        {
            var d = OpenDialog(1700, 980, 0.8f);
            Ui.Heading(d, $"Command deck - {run.Deck.Count} cards", 34, 34, 20, 900, 46);
            Ui.Stamp(d, "Every card comes from a ship. Lose the ship and its cards go with it.", 12, 36, 64, 900, 16);
            Ui.Btn(d, "Close", 1520, 22, 150, 48, Refresh, 26);
            var cards = run.Deck.OrderBy(c => c.Ship < 0 ? 2 : c.Ship).ThenBy(c => PaperCards.Get(c.Card).Type).ThenBy(c => c.Card).ToList();
            const float scale = 0.78f;
            float cw = CardView.W * scale + 12, ch = CardView.H * scale + 12;
            int perRow = 10;
            for (int i = 0; i < cards.Count && i < 40; i++)
            {
                float x = 40 + (i % perRow) * cw + CardView.W * scale / 2f;
                float y = 100 + (i / perRow) * ch + CardView.H * scale;
                MakeCard(d, cards[i], x, y, scale);
            }
        }
    }
}
