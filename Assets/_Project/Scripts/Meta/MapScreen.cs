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
        const float LeftW = 330f, TopH = 52f, FleetW = 300f;

        RunState run;
        RunContent content;
        RectTransform root, mapRoot, fleetRoot, trackRoot, topRoot, overlay;
        TextMeshProUGUI infoText;
        readonly List<Image> pulsing = new List<Image>();
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
            var top = Ui.Img(root, "TopBar", Skin.Header, 0, 0, Ui.RefW, TopH, Color.white, true);
            top.raycastTarget = true;
            topRoot = Ui.Box(top.transform, "Content", 0, 0, Ui.RefW, TopH);
            // The fleet is a few compact cards floating over the map, not a wall down the left side.
            fleetRoot = Ui.Box(root, "Fleet", 12, TopH + 12, FleetW, Ui.RefH - TopH - 24);
            var track = Ui.Panel(root, "Race", LeftW + 10, TopH + 12, Ui.RefW - LeftW - 22, 100);
            trackRoot = Ui.Box(track.transform, "Content", 0, 0, Ui.RefW - LeftW - 22, 100);
            var info = Ui.Img(root, "Info", Skin.Slot, LeftW + 10, Ui.RefH - 50, Ui.RefW - LeftW - 22, 38, Color.white, true);
            infoText = Ui.Body(info.transform, "", 16, 16, 11, Ui.RefW - LeftW - 60, 16, TextAlignmentOptions.Left);
            infoText.textWrappingMode = TextWrappingModes.NoWrap;
            infoText.overflowMode = TextOverflowModes.Ellipsis;

            overlay = Ui.Box(root, "Overlay", 0, 0, Ui.RefW, Ui.RefH);
            overlay.gameObject.SetActive(false);
            Refresh();
        }

        void Update()
        {
            // Reachable nodes blink rather than swell: scaling pixel art by a fraction makes it shimmer.
            float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
            foreach (var img in pulsing)
                if (img != null) img.color = Skin.Alpha(img.color, a);
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
                case RunPhase.Hail: ShowHail(); break;
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
            if (run.CurrentNode < 0) return "Choose where to enter the Ion Reef. Brass rings mark the nodes you can reach.";
            if (RunRules.LanesMined(run, content))
                return $"<color=#d9573f>The Concord is ahead and has mined the lanes: every jump costs each ship {content.Config.MineDamage} hull until you catch up.</color> Jump lanes are clear.";
            return "Choose the next node. Every jump moves the rival fleets along the track above.";
        }

        void SetInfo(string text)
        {
            if (infoText != null) infoText.text = text;
        }

        void RefreshTop()
        {
            Ui.Clear(topRoot);
            Ui.Heading(topRoot, "Sector I - The Ion Reef", 16, 28, 18, 520, 20);
            float x = 560;
            x = Resource(x, "res_scrap", "Scrap", run.Scrap.ToString(), Skin.Brass);
            x = Resource(x, "res_ordinals", "Ordinals", $"{run.Ordinals}/{run.MaxOrdinals}", Skin.Cyan);
            x = Resource(x, "res_darts", "Darts", $"{run.Darts}/{run.MaxDarts}", Skin.Text);
            x = Resource(x, "icon_junk", "Junk", run.JunkCount().ToString(), run.JunkCount() > 0 ? Skin.Warn : Skin.Dim);
            Ui.Stamp(topRoot, $"Seed {run.Seed}", 12, 1440, 20, 170, 14, TextAlignmentOptions.Right, Skin.Faint);
            Ui.Btn(topRoot, "Deck", 1630, 8, 130, 37, ShowDeck, 16);
            Ui.Btn(topRoot, "Menu", 1768, 8, 134, 37, () => { Game.Save(); Game.ToMenu(); }, 16);
        }

        float Resource(float x, string icon, string label, string value, Color color)
        {
            var art = Ui.Art(topRoot, icon, x, 2, 1f);
            float w = art.rectTransform.sizeDelta.x;
            Ui.Stamp(topRoot, label, 12, x + w + 8, 8, 120, 14);
            Ui.Number(topRoot, value, 24, x + w + 8, 22, 120, 24, TextAlignmentOptions.Left, color);
            return x + 210;
        }

        void RefreshFleet()
        {
            Ui.Clear(fleetRoot);
            float y = 0;
            for (int i = 0; i < run.Ships.Count; i++)
            {
                var ship = run.Ships[i];
                bool flag = i == 0;
                float h = !ship.Alive ? 66 : flag ? 78 + run.Modules.Count * 18 + 8 : 70;
                var card = Ui.Panel(fleetRoot, "Ship " + ship.Name, 0, y, FleetW, h);
                var t = card.transform;
                Ui.Heading(t, ship.Name, 16, 16, 12, 200, 20, TextAlignmentOptions.Left, ship.Alive ? Skin.Text : Skin.Faint);
                Ui.Stamp(t, flag ? "Flagship" : "Escort", 12, 190, 16, 94, 14, TextAlignmentOptions.Right, Skin.BrassDark);
                y += h + 8;
                if (!ship.Alive)
                {
                    Ui.Stamp(t, "Lost - rebuild at a shipyard", 12, 16, 40, 270, 14, TextAlignmentOptions.Left, Skin.Bad);
                    continue;
                }
                float frac = Mathf.Clamp01(ship.Hull / (float)ship.MaxHull);
                Ui.Stamp(t, "Hull", 12, 16, 43, 36, 14, TextAlignmentOptions.Left, Skin.Dim);
                Ui.Bar(t, "Hull", 54, 40, 168, 18, frac, frac > 0.34f ? Skin.Good : Skin.Bad);
                Ui.Number(t, $"{ship.Hull}/{ship.MaxHull}", 12, 224, 43, 62, 14, TextAlignmentOptions.Right);
                if (!flag) continue;
                for (int slot = 0; slot < run.Modules.Count; slot++)
                {
                    var m = content.Module(run.Modules[slot]);
                    Ui.Stamp(t, m.Slot, 12, 16, 70 + slot * 18, 96, 14, TextAlignmentOptions.Left, Skin.BrassDark);
                    Ui.Stamp(t, m.Name, 12, 112, 70 + slot * 18, 176, 14, TextAlignmentOptions.Left, Skin.Text).textWrappingMode = TextWrappingModes.NoWrap;
                }
            }

            y = RelicShelf(y);
            Ui.Btn(fleetRoot, $"Deck - {run.Deck.Count} cards", 0, y, FleetW, 40, ShowDeck, 16);
            y += 50;

            // The journal: only the last few lines, and only once there is something to say.
            var lines = run.Journal.Skip(Math.Max(0, run.Journal.Count - 4)).ToList();
            if (lines.Count == 0) return;
            var log = Ui.Panel(fleetRoot, "Journal", 0, y, FleetW, 200);
            var text = Ui.Label(log.transform, string.Join("\n", lines), Skin.Stencil, 12, Skin.Dim, 16, 14, FleetW - 32, 172, TextAlignmentOptions.TopLeft);
            text.lineSpacing = 30f;
            text.overflowMode = TextOverflowModes.Truncate;
            text.ForceMeshUpdate();
            log.rectTransform.sizeDelta = new Vector2(FleetW, Mathf.Min(200f, Mathf.Ceil(text.preferredHeight) + 28f));
        }

        // ---------------------------------------------------------------- race track

        void RefreshTrack()
        {
            Ui.Clear(trackRoot);
            var t = trackRoot;
            float w = Ui.RefW - LeftW - 22;
            Ui.Stamp(t, "The race to the gate", 12, 18, 14, 300, 14, TextAlignmentOptions.Left, Skin.BrassDark);
            float x0 = 320, x1 = w - 130, yLine = 46;
            int length = RunRules.TrackLength(run);
            Ui.Fill(t, "Line", x0, yLine - 1, x1 - x0, 3, Skin.Faint);
            for (int i = 0; i <= run.BossLayer + 1; i++)
            {
                float x = x0 + (x1 - x0) * i / (run.BossLayer + 1);
                Ui.Fill(t, "Tick", x - 1, yLine - 6, 2, 13, Skin.Faint);
            }
            Ui.Art(t, "node_boss", x1 + 14, yLine - 24, 1f);
            Ui.Stamp(t, "Gate", 12, x1 + 66, yLine - 6, 60, 14);

            Marker(t, x0, x1, yLine, run.ConcordProgress, length, "Concord", Skin.Hex("ffc46a"), false, yLine + 24, 12);
            Marker(t, x0, x1, yLine, run.ChorusProgress, length, "Chorus", Skin.Hex("5fffc8"), false, yLine + 36, 12);
            Marker(t, x0, x1, yLine, run.PlayerProgress, length, "You", Color.white, true, yLine - 40, 12);

            Ui.Body(t, Lead("Concord", run.ConcordProgress), 16, 18, 34, 290, 16, TextAlignmentOptions.TopLeft, Skin.Dim);
            Ui.Body(t, Lead("Chorus", run.ChorusProgress), 16, 18, 54, 290, 16, TextAlignmentOptions.TopLeft, Skin.Dim);
            if (run.Nemesis.Hunting)
                Ui.Stamp(t, $"{content.Nemesis(run.Nemesis.Id).Name.Split(' ').Last()} hunts you - rank {Roman(run.Nemesis.Rank)}", 12, 18, 78, 230, 14, TextAlignmentOptions.Left, Skin.Bad);
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
            Ui.Stamp(t, label, size, x - 70, labelY, 140, 14, TextAlignmentOptions.Center, color);
        }

        // ---------------------------------------------------------------- map

        Vector2 NodePos(MapNode n)
        {
            float x0 = LeftW + 130, x1 = Ui.RefW - 120;
            float y0 = 270, y1 = Ui.RefH - 170;
            return new Vector2(x0 + (x1 - x0) * n.Layer / run.BossLayer, y0 + (y1 - y0) * n.Lane / 30f);
        }

        static string KindName(NodeKind k) => k == NodeKind.Shipyard ? "Shipyard" : k == NodeKind.Boss ? "Sector boss" : k.ToString();

        /// <summary>The hunting commander only shows on the map where they actually wait.</summary>
        bool NemesisHere(MapNode n) => n.Kind == NodeKind.Nemesis && run.Nemesis.Hunting && run.Nemesis.Node == n.Id;

        static string KindIcon(NodeKind k)
        {
            switch (k)
            {
                case NodeKind.Battle: return "node_battle";
                case NodeKind.Elite: return "node_elite";
                case NodeKind.Nemesis: return "node_nemesis";
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
                case NodeKind.Nemesis:
                    return NemesisHere(n) ? "<color=#d9573f>NEMESIS</color>  " + NemesisSummary() : "<color=#8a90a0>The commander who waited here has moved on.</color>";
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
            bool mined = RunRules.LanesMined(run, content);
            var visited = new HashSet<int>(run.Nodes.Where(n => n.Visited).Select(n => n.Id));

            foreach (var n in run.Nodes)
            {
                foreach (int id in n.Next)
                {
                    var m = run.Node(id);
                    bool walked = visited.Contains(n.Id) && visited.Contains(m.Id);
                    bool open = n.Id == run.CurrentNode && choices.Contains(m);
                    Line(mapRoot, NodePos(n), NodePos(m), walked ? Skin.Brass : open ? Skin.Alpha(mined ? Skin.Bad : Skin.Brass, 0.65f) : new Color(0.55f, 0.6f, 0.75f, 0.28f), walked || open ? 4f : 2f);
                }
            }

            // Jump lanes: a long dotted arc over the layer they skip.
            foreach (var n in run.Nodes)
            {
                foreach (int id in n.Jumps)
                {
                    var m = run.Node(id);
                    bool open = n.Id == run.CurrentNode && choices.Contains(m);
                    bool walked = visited.Contains(n.Id) && visited.Contains(m.Id) && !n.Next.Contains(id);
                    Dotted(mapRoot, NodePos(n), NodePos(m), open ? Skin.Cyan : walked ? Skin.Alpha(Skin.Cyan, 0.7f) : Skin.Alpha(Skin.Cyan, 0.35f), open ? 6f : 4f);
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
                    // A pixel hexagon around the hexagonal node icon, drawn 1:1.
                    var hex = Skin.HexOutline(Mathf.RoundToInt(size * 0.62f), 4);
                    var ring = Ui.Img(mapRoot, "Ring", hex, 0, 0, hex.rect.width, hex.rect.height, here ? Color.white : Skin.BrassLight);
                    ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    ring.rectTransform.anchoredPosition = new Vector2(Mathf.Round(p.x), -Mathf.Round(p.y));
                    if (can) pulsing.Add(ring);
                }
                bool jumpTo = can && RunRules.IsJump(run, n.Id);
                var sprite = ArtLibrary.Instance.Sprite(KindIcon(n.Kind == NodeKind.Nemesis && !NemesisHere(n) ? NodeKind.Battle : n.Kind)) ?? ArtLibrary.Instance.Sprite("node_elite");
                var tint = here ? Color.white : n.Visited ? new Color(0.4f, 0.4f, 0.45f) : can ? Color.white : new Color(0.72f, 0.72f, 0.78f);
                var img = Ui.Img(mapRoot, $"Node {n.Id}", sprite ?? Skin.Pip, p.x - size / 2f, p.y - size / 2f, size, size, tint);
                img.raycastTarget = true;
                var relay = img.gameObject.AddComponent<PointerRelay>();
                var node = n;
                string jumpNote = jumpTo ? $"   <color=#59d0e0>JUMP LANE: skips a node, every ship loses {RunRules.JumpCost(run, content)} hull, the rivals move once.</color>" : "";
                relay.Enter = () => SetInfo(Describe(node) + jumpNote + (can ? "" : here ? "   <color=#8a90a0>(you are here)</color>" : node.Visited ? "   <color=#8a90a0>(visited)</color>" : ""));
                relay.Exit = () => SetInfo(DefaultInfo());
                relay.Click = _ => ClickNode(node.Id);
                string label = NemesisHere(n) ? "Nemesis" : n.Kind == NodeKind.Nemesis ? "Battle" : KindName(n.Kind);
                Ui.Stamp(mapRoot, jumpTo ? label + " - jump" : label, 12, p.x - 80, p.y + size / 2f + 8, 160, 14, TextAlignmentOptions.Center,
                    jumpTo ? Skin.Cyan : NemesisHere(n) ? Skin.Bad : can ? Skin.BrassLight : Skin.Dim);
            }

            if (run.CurrentNode < 0)
                Ui.Stamp(mapRoot, "Entry", 12, NodePos(run.Nodes[0]).x - 80, 186, 160, 14, TextAlignmentOptions.Center, Skin.Brass);
        }

        static void Dotted(Transform parent, Vector2 a, Vector2 b, Color color, float size)
        {
            // Bowed upward so the lane reads as leaving the map plane, and never hides an ordinary lane.
            var mid = (a + b) / 2f + new Vector2(0f, -70f);
            const int dots = 26;
            for (int i = 1; i < dots; i++)
            {
                float t = i / (float)dots;
                var p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * mid + t * t * b;
                Ui.Fill(parent, "Jump", Mathf.Round(p.x - size / 2f), Mathf.Round(p.y - size / 2f), size, size, color);
            }
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
            string error = RunRules.Travel(run, content, id);
            Commit(error);
            if (error == null && !string.IsNullOrEmpty(run.TravelNote))
            {
                notice = run.TravelNote + ".";
                SetInfo(DefaultInfo());
            }
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
            view.Rect.anchoredPosition = new Vector2(Mathf.Round(centreX), -Mathf.Round(bottomY));
            return view;
        }

        /// <summary>The whole deck at full size (cards are never scaled). Rows close up when the deck outgrows three of them.</summary>
        void ShowDeck()
        {
            var d = OpenDialog(1880, 1040, 0.8f);
            Ui.Heading(d, $"Command deck - {run.Deck.Count} cards", 32, 36, 26, 1200, 36);
            Ui.Body(d, "Every card comes from a ship. Lose the ship and its cards go with it.", 16, 38, 70, 1200, 16, TextAlignmentOptions.TopLeft, Skin.Dim);
            Ui.Btn(d, "Close", 1690, 26, 150, 48, Refresh, 16);
            var cards = run.Deck.OrderBy(c => c.Ship < 0 ? 2 : c.Ship).ThenBy(c => PaperCards.Get(c.Card).Type).ThenBy(c => c.Card).ToList();
            const int perRow = 8;
            int rows = Mathf.Max(1, (cards.Count + perRow - 1) / perRow);
            float cw = CardView.W + 6, top = 104, room = 1040 - top - 26 - CardView.H;
            float rowStep = rows <= 1 ? 0 : Mathf.Min(CardView.H + 10, Mathf.Floor(room / (rows - 1)));
            float x0 = Mathf.Round((1880 - perRow * cw) / 2f);
            for (int i = 0; i < cards.Count; i++)
            {
                float x = x0 + (i % perRow) * cw + CardView.W / 2f;
                float y = top + (i / perRow) * rowStep + CardView.H;
                var view = MakeCard(d, cards[i], x, y);
                view.Hovered = v => v.transform.SetAsLastSibling();
            }
        }
    }
}
