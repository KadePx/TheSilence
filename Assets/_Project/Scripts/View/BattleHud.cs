using System.Collections.Generic;
using System.Linq;
using Silence.Core.Battle;
using Silence.Core.Hex;
using Silence.Data;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Silence.View
{
    /// <summary>The battle HUD: top bar, selected-ship panel, intents and log, hand, prompt line and overlays.</summary>
    public sealed partial class BattleScreen
    {
        // Layout in 1920x1080 reference pixels. The board has the full width; the fleet cards (left) and the
        // enemy orders (right) float over the edges of the backdrop instead of walling the board in.
        const float TopH = 52f, LeftW = 0f, RightW = 0f, BottomY = 770f, PromptH = 34f;
        const float FleetW = 300f, FleetX = 12f, OrdersW = 292f, OrdersX = Ui.RefW - 292f - 12f;
        const float PromptX = 400f, PromptW = 1120f;
        static readonly Vector2 BoardCentrePx = new Vector2((LeftW + (Ui.RefW - RightW)) / 2f, (TopH + (BottomY - PromptH)) / 2f);

        static Vector2 BoardCentre => new Vector2((BoardCentrePx.x - Ui.RefW / 2f) / 32f, (Ui.RefH / 2f - BoardCentrePx.y) / 32f);

        RectTransform root, chipRoot, floatRoot, handRoot, unitPanel, intentRoot, overlay, logPanel;
        TextMeshProUGUI turnText, titleText, ordinalsText, dartsText, phaseText, promptText, logText, pilesText, actionsText, endHint;
        Image endGlow;
        readonly List<Image> pips = new List<Image>();
        readonly List<Image> pipFills = new List<Image>();
        Button endTurnButton, expendButton;
        readonly List<CardView> handViews = new List<CardView>();
        CardView hoveredCard;
        readonly List<string> logLines = new List<string>();
        bool logOpen;

        void BuildHud()
        {
            var canvas = Ui.Canvas("Battle HUD");
            canvas.transform.SetParent(transform, false);
            root = Ui.Root(canvas);

            var catcher = Ui.Catcher(root, "BoardInput", 0, TopH, Ui.RefW, BottomY - TopH);
            catcher.Click = OnBoardClick;
            catcher.Move = OnBoardMove;
            catcher.Exit = () =>
            {
                hoverUnitId = -1;
                hoverHex = new HexCoord(-99, -99);
                if (Battle != null && board != null && mode != Mode.Busy) { board.RefreshOverlay(); RefreshChips(); RefreshUnitPanel(); RefreshPrompt(); }
            };

            chipRoot = Ui.Box(root, "Chips", 0, 0, Ui.RefW, Ui.RefH);
            floatRoot = Ui.Box(root, "Floaters", 0, 0, Ui.RefW, Ui.RefH);

            // ---- top bar: turn, title, the two fleet resources, and three small buttons
            var top = Ui.Img(root, "TopBar", Skin.Header, 0, 0, Ui.RefW, TopH, Color.white, true);
            top.raycastTarget = true;
            turnText = Ui.Heading(top.transform, "TURN 1", 16, 28, 18, 140, 20);
            phaseText = Ui.Heading(top.transform, "", 16, 160, 18, 260, 20, TextAlignmentOptions.Left, Skin.BrassLight);
            titleText = Ui.Heading(top.transform, "", 16, 460, 18, 1000, 20, TextAlignmentOptions.Center, Skin.Dim);
            crewLabel = Ui.Stamp(top.transform, "Ordinals", 12, 1300, 20, 100, 14, TextAlignmentOptions.Right, Skin.Dim);
            ordinalsText = Ui.Number(top.transform, "24", 24, 1408, 14, 50, 24, TextAlignmentOptions.Left, Skin.Cyan);
            craftLabel = Ui.Stamp(top.transform, "Darts", 12, 1440, 20, 90, 14, TextAlignmentOptions.Right, Skin.Dim);
            dartsText = Ui.Number(top.transform, "6", 24, 1538, 14, 50, 24, TextAlignmentOptions.Left, Skin.Text);
            Ui.Btn(top.transform, "Log", 1630, 8, 84, 37, ToggleLog, 16);
            Ui.Btn(top.transform, "Help", 1720, 8, 88, 37, ShowHelp, 16);
            Ui.Btn(top.transform, "Quit", 1814, 8, 88, 37, QuitBattle, 16);

            // ---- left: one compact card per capital ship, then whatever the pointer is on
            unitPanel = Ui.Box(root, "Fleet", FleetX, TopH + 12, FleetW, BottomY - TopH - 24);

            // ---- right: enemy orders in the order they resolve; the log opens beneath on request
            intentRoot = Ui.Box(root, "Orders", OrdersX, TopH + 12, OrdersW, 460);
            var log = Ui.Panel(root, "Log", OrdersX, BottomY - PromptH - 262, OrdersW, 254);
            logPanel = log.rectTransform;
            Ui.Stamp(log.transform, "Log", 12, 14, 10, 200, 14, TextAlignmentOptions.Left, Skin.BrassDark);
            logText = Ui.Label(log.transform, "", Skin.Stencil, 12, Skin.Dim, 14, 28, OrdersW - 28, 214, TextAlignmentOptions.BottomLeft);
            logText.overflowMode = TextOverflowModes.Truncate;
            logPanel.gameObject.SetActive(false);

            // ---- prompt line: says exactly what the current selection would do
            var prompt = Ui.Img(root, "Prompt", Skin.Slot, PromptX, BottomY - PromptH, PromptW, PromptH, Color.white, true);
            promptText = Ui.Body(prompt.transform, "", 16, 16, 9, PromptW - 32, 16, TextAlignmentOptions.Left);
            promptText.textWrappingMode = TextWrappingModes.NoWrap;
            promptText.overflowMode = TextOverflowModes.Ellipsis;
            expendButton = Ui.Btn(root, "Expend", PromptX + PromptW + 8, BottomY - PromptH - 2, 250, PromptH + 2, ToggleExpend, 16);
            expendButton.gameObject.SetActive(false);

            // ---- bottom: command, the hand, end turn
            var bottom = Ui.Plate(root, "Bottom", 0, BottomY, Ui.RefW, Ui.RefH - BottomY);
            Ui.Stamp(bottom.transform, "Command", 12, 30, 24, 200, 14, TextAlignmentOptions.Left, Skin.BrassDark);
            for (int i = 0; i < 6; i++)
            {
                var socket = Ui.Img(bottom.transform, "Pip" + i, Skin.PipSocket, 28 + i * 44, 44, 41, 47);
                Tip.On(socket, "Command", () => Gloss.Explain(Gloss.Find("command")) + " The number in the corner of a card is what it costs.");
                var fill = Ui.Img(socket.transform, "Fill", Skin.Pip, 10, 12, 21, 23, Skin.Brass);
                pips.Add(socket);
                pipFills.Add(fill);
            }
            // What you can still do this turn, in plain words, so nothing has to be guessed.
            actionsText = Ui.Label(bottom.transform, "", Skin.Stencil, 12, Skin.Text, 30, 108, 340, 120, TextAlignmentOptions.TopLeft);
            actionsText.lineSpacing = 50f;
            pilesText = Ui.Label(bottom.transform, "", Skin.Stencil, 12, Skin.Faint, 30, 264, 340, 30, TextAlignmentOptions.TopLeft);
            pilesText.lineSpacing = 40f;

            handRoot = Ui.Box(bottom.transform, "Hand", 300, 0, 1320, Ui.RefH - BottomY);

            endTurnButton = Ui.Btn(bottom.transform, "End turn", 1660, 112, 232, 84, OnEndTurn, 32);
            Tip.On(endTurnButton.targetGraphic, "End turn", () => "In order: every ship drifts ahead by its Speed, the enemy carries out the orders shown top right, shields come back and heat drains, and your hand is discarded. Then you draw 5 and get 3 command.\n\nAn enemy order misses if you have left its arc or range by then.");
            endGlow = Ui.Img(endTurnButton.transform, "Glow", Skin.Frame, -4, -4, 240, 92, Skin.BrassLight, true);
            endGlow.gameObject.AddComponent<Blink>();
            endGlow.enabled = false;
            endHint = Ui.Stamp(bottom.transform, "", 12, 1620, 206, 312, 30, TextAlignmentOptions.Top, Skin.Dim);

            tutorialRoot = Ui.Box(root, "Tutorial", 0, 0, Ui.RefW, Ui.RefH);
            overlay = Ui.Box(root, "Overlay", 0, 0, Ui.RefW, Ui.RefH);
            overlay.gameObject.SetActive(false);
        }

        /// <summary>The top bar says whose move it is: yours, the drift, or the enemy's.</summary>
        void ShowPhase(string phase)
        {
            if (phaseText == null) return;
            switch (phase)
            {
                case "Drift": phaseText.text = "DRIFT"; phaseText.color = Skin.Cyan; break;
                case "EnemyAction": phaseText.text = "ENEMY ACTS"; phaseText.color = Skin.Bad; break;
                case "End": phaseText.text = "END OF TURN"; phaseText.color = Skin.Dim; break;
                case "Command": phaseText.text = "YOUR ORDERS"; phaseText.color = Skin.BrassLight; break;
            }
        }

        /// <summary>
        /// Bottom left: what is still open this turn (cards you can afford, darts that have not acted), how ships
        /// move, and under End Turn whether anything is left. End Turn blinks once nothing is.
        /// </summary>
        void RefreshActions()
        {
            if (actionsText == null || Battle == null) return;
            bool mine = mode != Mode.Busy && mode != Mode.Over && mode != Mode.Intro;
            if (!mine)
            {
                actionsText.text = mode == Mode.Busy ? "<color=#8a90a0>Wait: orders are being carried out.</color>" : "";
                endHint.text = "";
                endGlow.enabled = false;
                return;
            }
            ShowPhase("Command");
            int playable = 0;
            foreach (var c in Battle.Deck.Hand)
            {
                bool alive = c.SourceId < 0 || (Battle.Get(c.SourceId) != null && Battle.Get(c.SourceId).Alive);
                if (!c.Def.Unplayable && alive && Battle.CostOf(c) <= Battle.Command) playable++;
            }
            int darts = Battle.LiveUnits(Side.Player).Count(u => u.Kind == UnitKind.Craft && !u.CraftActed);
            int dartsOut = Battle.PlayerCraftCount();

            var lines = new List<string>();
            lines.Add(playable > 0
                ? $"<color=#e8c870>CARDS</color>  {playable} you can play"
                : "<color=#555b6a>CARDS  none you can afford</color>");
            if (dartsOut > 0)
                lines.Add(darts > 0
                    ? $"<color=#7fdcf2>{CraftWord.ToUpperInvariant()}S</color>  {darts} ready: click one to move it"
                    : $"<color=#555b6a>{CraftWord.ToUpperInvariant()}S  all have acted</color>");
            int helms = Battle.LiveUnits(Side.Player).Count(u => u.IsCapital && !u.ThrottleUsed);
            lines.Add(helms > 0
                ? "<color=#7fdcf2>HELM</color>  set speed with - + on a ship card"
                : "<color=#555b6a>HELM  speed is set for this turn</color>");
            Ui.Glossed(actionsText, true).text = Gloss.Mark(string.Join("\n", lines), true, true);

            bool nothingLeft = playable == 0 && darts == 0;
            var parts = new List<string>();
            if (playable > 0) parts.Add($"{playable} card{(playable > 1 ? "s" : "")}");
            if (darts > 0) parts.Add($"{darts} {CraftWord}{(darts > 1 ? "s" : "")}");
            endHint.text = nothingLeft ? "<color=#e8c870>Nothing left to do</color>" : string.Join(" and ", parts) + " still open";
            endGlow.enabled = nothingLeft || (InTutorial && Step.EndTurn && !Step.Darts);
        }

        void ToggleLog()
        {
            logOpen = !logOpen;
            logPanel.gameObject.SetActive(logOpen);
            RefreshLog();
        }

        /// <summary>The rules a new player needs, on request instead of always on screen.</summary>
        void ShowHelp()
        {
            if (mode == Mode.Busy || mode == Mode.Over || mode == Mode.Intro) return;
            var d = OpenOverlay(860, 470, 0.7f);
            Ui.Heading(d, "Standing orders", 32, 40, 30, 780, 34, TextAlignmentOptions.Left, Skin.Brass);
            string[] rules =
            {
                "Enemies show their orders before you act. A solid red line will hit; a faded one misses as things stand.",
                "Cards cost command. A weapon needs its target inside the weapon's arc and range.",
                "Ships drift straight ahead by their Speed when you end the turn. Set Speed with - and + on a ship card, one step a turn. Cards turn the ship.",
                "A hit lands on the arc facing the attacker: shields soak it first, then armor, then hull.",
                "Leave an enemy's arc or range and its shot misses. Heat over the cap adds Fire on Deck. Darts move by hand: click one, then a hex or an enemy.",
            };
            for (int i = 0; i < rules.Length; i++)
            {
                Ui.Number(d, (i + 1).ToString(), 24, 40, 92 + i * 56, 30, 24, TextAlignmentOptions.Left, Skin.Brass);
                Ui.Body(d, rules[i], 16, 80, 90 + i * 56, 740, 40, TextAlignmentOptions.TopLeft, Skin.Text);
            }
            Ui.Stamp(d, "Keys: 1-9 cards   E end turn   Esc or right-click cancel", 12, 40, 384, 600, 14, TextAlignmentOptions.Left, Skin.Dim);
            Ui.Btn(d, "Close", 660, 392, 160, 48, () => { HideOverlay(); }, 16);
        }

        // ---------------------------------------------------------------- refresh

        void Refresh()
        {
            if (Battle == null) return;
            turnText.text = $"TURN {Battle.Turn}";
            titleText.text = (string.IsNullOrEmpty(Battle.Title) ? "" : Battle.Title.ToUpperInvariant()) +
                             (string.IsNullOrEmpty(BattleLauncher.Subtitle) ? "" : "  -  " + BattleLauncher.Subtitle.ToUpperInvariant());
            ordinalsText.text = Battle.Ordinals.ToString();
            ordinalsText.color = Battle.Ordinals <= 0 ? Skin.Bad : Skin.Cyan;
            dartsText.text = Battle.DartsInHangar < 0 ? "-" : Battle.DartsInHangar.ToString();
            Gloss.Crew = Battle.CrewName;
            Gloss.Craft = CraftWord + "s";
            Ui.Glossed(crewLabel, true).text = Gloss.Mark(Battle.CrewName.ToUpperInvariant(), true, true);
            Ui.Glossed(craftLabel, true).text = Gloss.Mark((CraftWord + "s").ToUpperInvariant(), true, true);

            for (int i = 0; i < pips.Count; i++)
            {
                pips[i].enabled = i < Mathf.Max(Battle.CommandPerTurn, Battle.Command);
                pipFills[i].enabled = i < Battle.Command;
            }
            int left = Battle.TurnLimit - Battle.Turn;
            RefreshActions();
            pilesText.text = $"DECK {Battle.Deck.DrawPile.Count}    DISCARD {Battle.Deck.Discard.Count}" +
                             (Battle.Deck.Exhausted.Count > 0 ? $"    EXHAUST {Battle.Deck.Exhausted.Count}" : "") +
                             (left <= 5 ? $"\n<color=#f0a040>{Mathf.Max(0, left)} TURNS UNTIL BOTH FLEETS WITHDRAW</color>" : "");

            RebuildHand();
            RefreshIntents();
            board.RefreshOverlay();
            RefreshChips();
            RefreshUnitPanel();
            RefreshLog();
            RefreshPrompt();
            endTurnButton.interactable = mode != Mode.Busy && mode != Mode.Over && mode != Mode.Intro;
            if (mode == Mode.Idle) RefreshSelection();
            else RefreshTutorial();
        }

        void RefreshPrompt()
        {
            if (promptText == null || Battle == null) return;
            Ui.Glossed(promptText, true).text = Gloss.Mark(PromptText());
            bool showExpend = mode == Mode.Target && selectedCard != null && selectedCard.Def.ExpendOptional && selectedCard.Def.ExpendCost > 0;
            expendButton.gameObject.SetActive(showExpend);
            if (showExpend)
            {
                var def = selectedCard.Def;
                bool can = Battle.Ordinals >= def.ExpendCost;
                expendButton.interactable = can;
                Ui.SetButtonLabel(expendButton, (expend ? "Expending " : "Expend ") + def.ExpendCost + (can ? "" : " (too few)"));
                expendButton.targetGraphic.color = expend ? Skin.BrassLight : Color.white;
            }
        }

        // ---------------------------------------------------------------- hand

        void RebuildHand()
        {
            foreach (var v in handViews)
                if (v != null) Destroy(v.gameObject);
            handViews.Clear();
            hoveredCard = null;
            foreach (var c in Battle.Deck.Hand)
            {
                var source = c.SourceId >= 0 ? Battle.Get(c.SourceId) : null;
                var view = CardView.Create(handRoot, c.Def, source != null ? source.Name : "", ArtFor(c.Def, source), Battle.CostOf(c), RulesText(c.Def), false);
                view.InstanceId = c.Id;
                var card = c;
                view.Clicked = _ => OnCardClicked(card);
                view.Hovered = v => { hoveredCard = v; v.transform.SetAsLastSibling(); RefreshUnitPanel(); };
                view.Unhovered = v => { if (hoveredCard == v) { hoveredCard = null; RefreshUnitPanel(); } };
                handViews.Add(view);
            }
            LayoutHand(true);
            RefreshHandState();
        }

        TextMeshProUGUI crewLabel, craftLabel;

        /// <summary>A card's text as it holds in this battle: in a nebula every "range N" on a weapon reads one less.</summary>
        string RulesText(CardDef def)
        {
            if (Battle.RangePenalty <= 0 || def.Type != CardType.Weapon) return def.Text;
            return System.Text.RegularExpressions.Regex.Replace(def.Text, @"(range |within )(\d+)", m => m.Groups[1].Value + Battle.Reach(int.Parse(m.Groups[2].Value)));
        }

        /// <summary>What this fleet's hand-flown craft are called: "dart" or "gunship".</summary>
        string CraftWord => Battle != null && Battle.DartSpec.Name.ToLowerInvariant().Contains("gunship") ? "gunship" : "dart";

        string ArtFor(CardDef def, Unit source)
        {
            if (def.IsJunk) return def.Id == "fire_on_deck" ? "status_fire" : "status_warning";
            if (def.Type == CardType.Hangar) return Battle.DartSpec.Id + "_E";
            if (source == null) source = Battle.Flagship ?? Battle.Units.Find(u => u.Side == Side.Player && u.IsCapital);
            string key = source != null ? source.SpecId : "asc_cruiser";
            // The flagships have a larger drawing for card crops.
            return Silence.Data.ArtLibrary.Instance.Sprite(key + "_E128") != null ? key + "_E128" : key + "_E";
        }

        void RefreshHandState()
        {
            foreach (var v in handViews)
            {
                if (v == null) continue;
                var c = Battle.Deck.FindInHand(v.InstanceId);
                if (c == null) continue;
                bool sourceAlive = c.SourceId < 0 || (Battle.Get(c.SourceId) != null && Battle.Get(c.SourceId).Alive);
                bool playable = !c.Def.Unplayable && Battle.CostOf(c) <= Battle.Command && sourceAlive && Battle.Outcome == BattleOutcome.Ongoing;
                v.SetPlayable(playable, Battle.CostOf(c));
                v.SetSelected(selectedCard == c);
            }
        }

        /// <summary>Cards sit on whole pixels and lift when hovered or selected; they are never scaled, so the pixel art stays crisp.</summary>
        void LayoutHand(bool snap)
        {
            int n = handViews.Count;
            if (n == 0) return;
            float areaW = 1320f, step = Mathf.Min(CardView.W + 10f, (areaW - CardView.W) / Mathf.Max(1, n - 1));
            float total = step * (n - 1) + CardView.W, x0 = (areaW - total) / 2f + CardView.W / 2f;
            for (int i = 0; i < n; i++)
            {
                var v = handViews[i];
                if (v == null) continue;
                bool up = v == hoveredCard || (selectedCard != null && v.InstanceId == selectedCard.Id);
                var target = new Vector2(Mathf.Round(x0 + i * step), -(Ui.RefH - BottomY - 7f) + (up ? 24f : 0f));
                var rt = v.Rect;
                var pos = snap ? target : Vector2.Lerp(rt.anchoredPosition, target, 0.35f);
                if ((pos - target).sqrMagnitude < 1f) pos = target;
                rt.anchoredPosition = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
            }
        }

        void AnimateHand() => LayoutHand(false);

        // ---------------------------------------------------------------- fleet cards and target info

        void RefreshUnitPanel()
        {
            if (unitPanel == null || Battle == null) return;
            Ui.Clear(unitPanel);

            float y = 0;
            var focus = Battle.Get(hoverUnitId) ?? Battle.Get(panelUnitId);
            foreach (var ship in Battle.Units.Where(u => u.Side == Side.Player && u.IsCapital))
            {
                FleetCard(ship, y, focus == ship);
                y += 148;
            }

            var notes = new List<string>();
            foreach (var u in Battle.LiveUnits(Side.Player).Where(u => u.IsCapital))
            {
                if (u.LanceBonus > 0) notes.Add($"{u.Name}: next lance +{u.LanceBonus}");
                if (u.Guarding) notes.Add($"{u.Name} is screening");
                if (u.NoBurnThisTurn) notes.Add($"{u.Name}: engines hit, no Burn");
            }
            if (Battle.WaymindPrimed) notes.Add("Waymind: next weapon ignores armor");
            if (notes.Count > 0)
            {
                var n = Ui.Label(unitPanel, string.Join("\n", notes), Skin.Stencil, 12, Skin.Brass, 8, y, FleetW - 16, notes.Count * 18f);
                n.lineSpacing = 40f;
                y += notes.Count * 18f + 10;
            }

            if (hoveredCard != null && Glossary(hoveredCard.Def, y)) return;
            if (focus != null && focus.Alive && !(focus.Side == Side.Player && focus.IsCapital)) TargetCard(focus, y);
        }

        /// <summary>While a card is under the pointer, spell out the rules words on it. False if it has none.</summary>
        bool Glossary(CardDef def, float y)
        {
            var lines = Gloss.TermsIn(def.Text).Take(5).Select(t => $"<color=#{t.Colour}>{t.Title.ToUpperInvariant()}</color>  {Gloss.Short(t)}").ToList();
            if (lines.Count == 0) return false;
            var card = Ui.Panel(unitPanel, "Glossary", 0, y, FleetW, 100);
            Ui.Stamp(card.transform, def.Name, 12, 16, 12, FleetW - 32, 14, TextAlignmentOptions.Left, Skin.BrassDark);
            var text = Ui.Label(card.transform, string.Join("\n", lines), Skin.Stencil, 12, Skin.Text, 16, 32, FleetW - 32, 400, TextAlignmentOptions.TopLeft);
            text.lineSpacing = 30f;
            text.paragraphSpacing = 40f;
            text.ForceMeshUpdate();
            card.rectTransform.sizeDelta = new Vector2(FleetW, 32 + Mathf.Ceil(text.preferredHeight) + 16);
            card.raycastTarget = false;
            return true;
        }

        /// <summary>One of your capital ships at a glance: hull, heat, shields and armor per arc, speed and heading.</summary>
        void FleetCard(Unit u, float y, bool focused)
        {
            var card = Ui.Panel(unitPanel, "Ship " + u.Name, 0, y, FleetW, 140);
            var t = card.transform;
            if (focused) Ui.Img(t, "Focus", Skin.Frame, 0, 0, FleetW, 140, Skin.Brass, true);
            Ui.Heading(t, u.Name, 16, 16, 12, 200, 20, TextAlignmentOptions.Left, u.Alive ? Skin.Text : Skin.Faint);
            Ui.Stamp(t, u.IsFlagship ? "Flagship" : "Escort", 12, 190, 16, 94, 14, TextAlignmentOptions.Right, Skin.BrassDark);
            if (!u.Alive)
            {
                Ui.Heading(t, "Lost", 32, 16, 54, 268, 34, TextAlignmentOptions.Center, Skin.Bad);
                return;
            }

            float frac = u.MaxHull > 0 ? u.Hull / (float)u.MaxHull : 0f;
            Ui.Stamp(t, "Hull", 12, 16, 41, 36, 14, TextAlignmentOptions.Left, Skin.Dim);
            Ui.Bar(t, "Hull", 54, 38, 168, 18, frac, frac > 0.34f ? Skin.Good : Skin.Bad);
            Ui.Number(t, $"{u.Hull}/{u.MaxHull}", 12, 224, 41, 62, 14, TextAlignmentOptions.Right);

            if (u.HeatCap > 0)
            {
                float max = u.HeatCap + Battle.ReactorStrainMargin;
                Ui.Stamp(t, "Heat", 12, 16, 63, 36, 14, TextAlignmentOptions.Left, Skin.Dim);
                var bar = Ui.Bar(t, "Heat", 54, 60, 168, 18, u.Heat / max, u.Heat > u.HeatCap ? Skin.Bad : Skin.Warn);
                Ui.Fill(bar.transform, "Cap", 4 + Mathf.Round(160f * u.HeatCap / max), 2, 2, 14, Skin.Text);
                Ui.Number(t, $"{u.Heat}/{u.HeatCap}", 12, 224, 63, 62, 14, TextAlignmentOptions.Right, u.Heat > u.HeatCap ? Skin.Bad : Skin.Text);
            }

            ArcRow(t, u, 84);
            // The helm: - and + change Speed one step, once a turn. Lit while this ship can still use it.
            Ui.Stamp(t, $"Speed {u.Speed}", 12, 16, 118, 70, 14, TextAlignmentOptions.Left, u.ThrottleUsed ? Skin.Dim : Skin.Cyan);
            for (int i = 0; i < u.MaxSpeed; i++)
                Ui.Img(t, "Chevron", Skin.Arrow, 80 + i * 15, 117, 14, 14, i < u.Speed ? Skin.Cyan : Skin.Hex("1c2a33"));
            int id = u.Id;
            bool mine = mode != Mode.Busy && mode != Mode.Over && mode != Mode.Intro;
            var speedTip = Ui.Catcher(t, "SpeedTip", 12, 110, 180, 28);
            Tip.On(speedTip.GetComponent<Image>(), "Speed and the helm", () => Gloss.Explain(Gloss.Find("speed")) + "\n\nThe ghost on the board marked DRIFT is where she will end the turn.", Gloss.Shield);
            HelmButton(t, 130, 113, "-", mine && Battle.CheckThrottle(id, -1) == null, () => ClickThrottle(id, -1));
            HelmButton(t, 162, 113, "+", mine && Battle.CheckThrottle(id, 1) == null, () => ClickThrottle(id, 1));
            Ui.Stamp(t, "Heading " + u.Facing, 12, 196, 118, 88, 14, TextAlignmentOptions.Right, Skin.Dim);
        }

        /// <summary>A small square key for the helm: cyan when it can be pressed, dark when it can't.</summary>
        static void HelmButton(Transform t, float x, float y, string glyph, bool enabled, System.Action onClick)
        {
            var edge = Ui.Fill(t, "Helm " + glyph, x, y, 28, 20, enabled ? Skin.Cyan : Skin.Hex("2c2f36"));
            Ui.Fill(edge.transform, "Face", 1, 1, 26, 18, enabled ? Skin.Hex("12303a") : Skin.Hex("16171b"));
            Ui.Heading(edge.transform, glyph, 16, 0, 0, 28, 20, TextAlignmentOptions.Center, enabled ? Skin.Cyan : Skin.Faint);
            if (!enabled) return;
            edge.raycastTarget = true;
            edge.gameObject.AddComponent<PointerRelay>().Click = _ => { Sfx.Play("sfx_click"); onClick(); };
        }

        /// <summary>Shields (cyan) and armor (grey) for the four arcs in one row: F, P, S, A.</summary>
        static void ArcRow(Transform t, Unit u, float y)
        {
            string[] letters = { "F", "S", "A", "P" };
            int[] order = { ArcIndex.Fore, ArcIndex.Port, ArcIndex.Starboard, ArcIndex.Aft };
            for (int i = 0; i < 4; i++)
            {
                int arc = order[i];
                float x = 14 + i * 68;
                var slot = Ui.Img(t, "Arc", Skin.Slot, x, y, 64, 28, Color.white, true);
                string[] arcNames = { "Fore", "Starboard", "Aft", "Port" };
                string[] arcWhere = { "ahead of the ship", "on her right-hand flank", "behind her", "on her left-hand flank" };
                int shieldsNow = u.Shields[arc], armorNow = u.Armor[arc], regen = u.ShieldRegen;
                string arcName = arcNames[arc], where = arcWhere[arc];
                Tip.On(slot, arcName + " arc", () => $"Shields {shieldsNow} / armor {armorNow}, against a hit from {where}.\n\n" +
                    $"A hit here loses {shieldsNow} to shields, then {armorNow} to armor, and the rest comes off hull. Shields come back {regen} a turn; armor does not wear down.\n\n" +
                    "Turn the ship to show an arc that still has shields.", Gloss.Shield);
                Ui.Stamp(slot.transform, letters[arc], 12, 8, 8, 12, 14, TextAlignmentOptions.Left, Skin.BrassDark);
                string sh = ColorUtility.ToHtmlStringRGB(u.Shields[arc] > u.MaxShields ? Color.white : u.Shields[arc] > 0 ? Skin.Cyan : Skin.Faint);
                Ui.Number(slot.transform, $"<color=#{sh}>{u.Shields[arc]}</color><color=#555b6a>/</color><color=#8a90a0>{u.Armor[arc]}</color>",
                    12, 20, 8, 40, 14, TextAlignmentOptions.Center);
            }
        }

        /// <summary>What the pointer is on: an enemy with its order and what comes next, or a small craft.</summary>
        void TargetCard(Unit u, float y)
        {
            bool enemy = u.Side == Side.Enemy;
            var intent = enemy ? Battle.Intents.FirstOrDefault(i => i.UnitId == u.Id) : null;
            // A commander's card also says when the ship will run and what it was refitted against.
            var notes = new List<string>();
            if (u.FleesAtHull > 0) notes.Add($"Breaks off at {u.FleesAtHull} hull, after the enemy acts.");
            if (u.LanceWard > 0) notes.Add($"Lance baffles: lance hits deal {u.LanceWard} less.");
            if (u.CraftWard > 0) notes.Add($"Point defence: darts deal {u.CraftWard} less.");
            float h = (u.IsCapital ? 190 : 118) + notes.Count * 34;
            var card = Ui.Panel(unitPanel, "Target", 0, y, FleetW, h);
            var t = card.transform;
            Ui.Heading(t, u.Name, 16, 16, 12, 200, 20, TextAlignmentOptions.Left, enemy ? Skin.Bad : Skin.Text);
            Ui.Stamp(t, enemy ? (u.Kind == UnitKind.Torpedo ? "Torpedo" : u.IsCommander ? "Commander" : u.IsCapital ? "Enemy" : "Craft") : CraftWord, 12, 190, 16, 94, 14,
                TextAlignmentOptions.Right, Skin.BrassDark);
            float frac = u.MaxHull > 0 ? u.Hull / (float)u.MaxHull : 0f;
            Ui.Stamp(t, "Hull", 12, 16, 41, 36, 14, TextAlignmentOptions.Left, Skin.Dim);
            Ui.Bar(t, "Hull", 54, 38, 168, 18, frac, enemy ? Skin.Bad : Skin.Good);
            Ui.Number(t, $"{u.Hull}/{u.MaxHull}", 12, 224, 41, 62, 14, TextAlignmentOptions.Right);

            float ty = 64;
            if (u.IsCapital)
            {
                ArcRow(t, u, 62);
                ty = 98;
            }
            string text;
            if (intent != null)
            {
                text = IntentText(intent, false);
                if (u.Cycle.Length > 0)
                {
                    var next = new List<string>();
                    for (int k = 1; k <= Mathf.Min(2, u.Cycle.Length); k++) next.Add(StepName(u.Cycle[(u.CycleIndex + k) % u.Cycle.Length]));
                    text += "\n<color=#8a90a0>Then " + string.Join(", then ", next) + ".</color>";
                }
            }
            else if (u.Kind == UnitKind.Torpedo) text = $"Hits for {u.AttackDamage}. One hit destroys it.";
            else if (u.IsCapital) text = u.StunnedThroughTurn >= Battle.Turn ? "Relay cut: no orders this turn." : "No orders this turn.";
            else text = u.CraftActed ? "Acted this turn." : $"Moves {u.Move + u.CraftBonusMove}, then attacks for {u.AttackDamage}.";
            foreach (string note in notes) text += "\n<color=#f0a040>" + note + "</color>";
            var body = Ui.Label(t, char.ToUpperInvariant(text[0]) + text.Substring(1), Skin.Stencil, 12, Skin.Text, 16, ty, FleetW - 32, h - ty - 10);
            body.lineSpacing = 30f;
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

        // ---------------------------------------------------------------- intents

        static string IntentIcon(Intent i)
        {
            switch (i.Kind)
            {
                case IntentKind.Broadside:
                case IntentKind.CraftAttack: return "intent_attack";
                case IntentKind.TorpedoRun:
                case IntentKind.LaunchTorpedo: return "intent_torpedo";
                case IntentKind.Maneuver: return "intent_move";
                case IntentKind.LaunchCraft: return "intent_spawn";
                case IntentKind.PatchUp:
                case IntentKind.JointFire: return "intent_buff";
                default: return "intent_unknown";
            }
        }

        static Color IntentColor(Intent i)
        {
            switch (i.Kind)
            {
                case IntentKind.Broadside:
                case IntentKind.CraftAttack: return Skin.Bad;
                case IntentKind.TorpedoRun:
                case IntentKind.LaunchTorpedo:
                case IntentKind.JointFire: return Skin.Warn;
                case IntentKind.PatchUp: return Skin.Good;
                default: return Skin.Dim;
            }
        }

        /// <summary>Short chip text ("BROADSIDE 9").</summary>
        static string IntentShort(Intent i)
        {
            switch (i.Kind)
            {
                case IntentKind.Broadside: return $"{(i.Label ?? "Broadside").ToUpperInvariant()} {i.Damage + i.Bonus}";
                case IntentKind.CraftAttack: return $"ATTACK {i.Damage + i.Bonus}";
                case IntentKind.TorpedoRun: return $"IMPACT {i.Damage + i.Bonus}";
                case IntentKind.LaunchTorpedo: return "TORPEDO";
                case IntentKind.Maneuver: return "MANEUVER";
                case IntentKind.LaunchCraft: return $"LAUNCH {i.Damage}";
                case IntentKind.PatchUp: return $"REPAIR {i.Damage}";
                case IntentKind.JointFire: return "JOINT FIRE";
                default: return "?";
            }
        }

        string IntentText(Intent i, bool withActor)
        {
            var u = Battle.Get(i.UnitId);
            var t = Battle.Get(i.TargetId);
            string who = withActor && u != null ? u.Name + ": " : "";
            string target = t != null ? t.Name : "nobody";
            switch (i.Kind)
            {
                case IntentKind.Broadside:
                {
                    string arcs = i.Arcs == Arc.Fore ? "fore arc" : i.Arcs == (Arc.Port | Arc.Starboard) ? "side arcs" : "any arc";
                    string hit = Battle.ForecastHit(i) ? "On course to hit." : "Misses as things stand.";
                    return $"{who}{i.Label ?? "Broadside"} for {i.Damage + i.Bonus} at {target} ({arcs}, range {i.Range}). {hit}";
                }
                case IntentKind.CraftAttack: return $"{who}moves to {target}'s aft and attacks for {i.Damage + i.Bonus}.";
                case IntentKind.TorpedoRun: return $"{who}closes on {target}; {i.Damage + i.Bonus} on impact. Shoot it down or brace.";
                case IntentKind.LaunchTorpedo: return $"{who}launches a torpedo at {target}. It strikes on a later turn.";
                case IntentKind.Maneuver: return $"{who}turns and moves to line up its guns.";
                case IntentKind.LaunchCraft: return $"{who}launches {i.Damage} gunship{(i.Damage > 1 ? "s" : "")}.";
                case IntentKind.PatchUp: return $"{who}repairs {i.Damage} hull on {target} and adds {EnemyRules.PatchUpShields} shields.";
                case IntentKind.JointFire: return $"{who}calls Joint Fire: every attack goes to {target}, +{EnemyRules.JointFireBonus} damage.";
                default: return who + i.Kind;
            }
        }

        /// <summary>Enemy orders in resolve order: one short row each. Hovering a row lights that enemy on the board.</summary>
        void RefreshIntents()
        {
            Ui.Clear(intentRoot);
            var list = Battle.Intents.Where(i => Battle.Get(i.UnitId) != null && Battle.Get(i.UnitId).Alive).ToList();
            if (list.Count == 0) return;

            int shown = Mathf.Min(list.Count, 12);
            var panel = Ui.Panel(intentRoot, "Panel", 0, 0, OrdersW, 40 + shown * 32 + 10);
            Ui.Stamp(panel.transform, "Enemy orders", 12, 16, 14, 200, 14, TextAlignmentOptions.Left, Skin.BrassDark);
            for (int n = 0; n < shown; n++)
            {
                var i = list[n];
                var u = Battle.Get(i.UnitId);
                var row = Ui.Img(panel.transform, "Order", Skin.Slot, 10, 36 + n * 32, OrdersW - 20, 30, Color.white, true);
                string orderText = i.Describe(Battle);
                Tip.On(row, "Enemy order", () => orderText + ".\n\nThis is what the ship will do after you end your turn. An attack misses if its target has left the arc or range by then: the line on the board is bright if it will land as things stand, and faint if it will miss.", Gloss.Danger);
                var color = i.Cancelled ? Skin.Faint : IntentColor(i);
                Ui.Fill(row.transform, "Edge", 3, 4, 3, 22, color);
                Ui.Art(row.transform, IntentIcon(i), 10, 3, 1f, i.Cancelled ? Skin.Faint : Color.white);
                string head = $"<color=#8a90a0>{ShortName(u)}</color>  <color=#{ColorUtility.ToHtmlStringRGB(color)}>{(i.Cancelled ? "CANCELLED" : IntentShort(i))}</color>";
                var h = Ui.Label(row.transform, head, Skin.Stencil, 12, Skin.Text, 40, 9, OrdersW - 70, 14);
                h.textWrappingMode = TextWrappingModes.NoWrap;
                h.overflowMode = TextOverflowModes.Ellipsis;

                int unitId = i.UnitId;
                var relay = row.gameObject.AddComponent<PointerRelay>();
                row.raycastTarget = true;
                relay.Enter = () =>
                {
                    if (mode == Mode.Busy || board == null) return;
                    hoverUnitId = unitId;
                    board.RefreshOverlay(unitId);
                    board.SetRing(unitId, Skin.Bad);
                    RefreshUnitPanel();
                    RefreshPrompt();
                };
                relay.Exit = () =>
                {
                    if (mode == Mode.Busy || board == null) return;
                    if (hoverUnitId == unitId) hoverUnitId = -1;
                    board.RefreshOverlay();
                    RefreshSelection();
                    RefreshUnitPanel();
                    RefreshPrompt();
                };
            }
        }

        /// <summary>A small dark tab with a coloured underline, centred above a board position.</summary>
        void Tab(Vector2 p, float dy, string text, Color color)
        {
            float w = 12f + text.Length * 7.2f;
            var chip = Ui.Fill(chipRoot, "Tab", Mathf.Round(p.x - w / 2f), Mathf.Round(p.y + dy), w, 16, new Color(0.02f, 0.02f, 0.04f, 0.9f));
            Ui.Fill(chip.transform, "Edge", 0, 14, w, 2, color);
            Ui.Label(chip.transform, text, Skin.Stencil, 12, color, 0, 1, w, 14, TextAlignmentOptions.Center).textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>"Concord frigate" reads as FRIGATE in a narrow row.</summary>
        static string ShortName(Unit u)
        {
            string n = u.Name.ToUpperInvariant();
            int space = n.LastIndexOf(' ');
            return n.Length > 12 && space >= 0 ? n.Substring(space + 1) : n;
        }

        /// <summary>A small plate over each enemy saying what it will do, so orders read on the board itself.</summary>
        void RefreshChips()
        {
            Ui.Clear(chipRoot);
            if (Battle == null || Battle.Outcome != BattleOutcome.Ongoing) return;

            // Your side: where each ship will drift to, and which darts can still be moved.
            foreach (var u in Battle.LiveUnits(Side.Player))
            {
                if (u.IsCapital && u.Speed > 0)
                {
                    var dest = Battle.PredictDrift(u);
                    if (dest != u.Pos) Tab(WorldToRef(board.World(dest)), -58f, $"DRIFT {u.Pos.DistanceTo(dest)}", Skin.Cyan);
                }
                else if (u.Kind == UnitKind.Craft && !u.CraftActed && mode != Mode.Busy && board.Views.TryGetValue(u.Id, out var dv))
                    Tab(WorldToRef(dv.Root.position), -34f, "READY", Skin.Cyan);
            }

            foreach (var i in Battle.Intents)
            {
                var u = Battle.Get(i.UnitId);
                if (u == null || !u.Alive) continue;
                if (!board.Views.TryGetValue(u.Id, out var view)) continue;
                var p = WorldToRef(view.Root.position);
                string text = i.Cancelled ? "CANCELLED" : IntentShort(i);
                var color = i.Cancelled ? Skin.Faint : IntentColor(i);
                if (u.IsCapital)
                {
                    float w = Mathf.Round(48f + text.Length * 7.2f);
                    var chip = Ui.Img(chipRoot, "Chip", Skin.Chip, Mathf.Round(p.x - w / 2f), Mathf.Round(p.y - 70f), w, 28, Color.white, true);
                    Ui.Art(chip.transform, IntentIcon(i), 8, 2);
                    var label = Ui.Label(chip.transform, text, Skin.Stencil, 12, color, 36, 8, w, 14);
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                }
                else
                {
                    // Small craft and torpedoes: just the number that matters, on a dark tab.
                    int amount = i.Damage + i.Bonus;
                    string shortText = amount > 0 && !i.Cancelled ? amount.ToString() : "-";
                    float w = 12f + shortText.Length * 8f;
                    var chip = Ui.Fill(chipRoot, "Chip", Mathf.Round(p.x - w / 2f), Mathf.Round(p.y - 36f), w, 16, new Color(0.02f, 0.02f, 0.04f, 0.9f));
                    Ui.Fill(chip.transform, "Edge", 0, 14, w, 2, color);
                    Ui.Label(chip.transform, shortText, Skin.Stencil, 12, color, 0, 1, w, 14, TextAlignmentOptions.Center);
                }
            }
        }

        Vector2 WorldToRef(Vector3 world) => new Vector2(Ui.RefW / 2f + world.x * 32f, Ui.RefH / 2f - world.y * 32f);

        // ---------------------------------------------------------------- log

        void AddLog(BattleEvent e)
        {
            string color;
            switch (e.Kind)
            {
                case EventKind.TurnStarted: color = "c9a24a"; break;
                case EventKind.Damage:
                case EventKind.Destroyed:
                case EventKind.Critical:
                case EventKind.ReactorStrain:
                case EventKind.Collision: color = "d9573f"; break;
                case EventKind.CardPlayed: color = "d6dae4"; break;
                case EventKind.CardAdded: color = "f0a040"; break;
                case EventKind.Miss:
                case EventKind.IntentCancelled: color = "8cc47a"; break;
                case EventKind.Victory:
                case EventKind.Defeat:
                case EventKind.Escaped:
                case EventKind.Withdrawal: color = "c9a24a"; break;
                case EventKind.Attack:
                case EventKind.Spawned:
                case EventKind.Repaired:
                case EventKind.ShieldsRaised:
                case EventKind.Buff:
                case EventKind.Turned:
                case EventKind.Ordinals:
                case EventKind.CardsRemoved: color = "8a90a0"; break;
                default: return; // phases, draws, drift steps, regen and the like stay out of the log
            }
            string text = e.Text;
            if (e.Kind == EventKind.Damage)
            {
                // Core's damage line is complete but long; the log keeps the part a player scans for.
                var hit = Battle.Get(e.UnitId);
                string who = hit != null ? hit.Name : "Target";
                int open = text.IndexOf('('), close = text.IndexOf(')');
                string arc = hit != null && hit.IsCapital && open >= 0 && close > open && close - open < 14 ? " " + text.Substring(open, close - open + 1) : "";
                text = e.Amount > 0 ? $"{who}{arc}: -{e.Amount} hull" : $"{who}{arc}: hit absorbed";
                if (e.Amount == 0) color = "7fdcf2";
            }
            logLines.Add($"<color=#{color}>{text}</color>");
            if (logLines.Count > 80) logLines.RemoveAt(0);
        }

        void RefreshLog()
        {
            if (!logOpen) return;
            logText.text = string.Join("\n", logLines.Skip(Mathf.Max(0, logLines.Count - 14)));
        }

        // ---------------------------------------------------------------- floating text

        /// <summary>Pixel text that rises and fades over a unit, with a dark copy behind it so it reads on any backdrop.</summary>
        void Floater(Vector3 world, string text, Color color, float size = 32f)
        {
            var p = WorldToRef(world);
            var holder = Ui.Box(floatRoot, "Floater", Mathf.Round(p.x - 160), Mathf.Round(p.y - 30), 320, 36);
            var shadow = Ui.Label(holder, text, Skin.Heading, size, new Color(0f, 0f, 0f, 0.9f), 2, 2, 320, 36, TextAlignmentOptions.Center, 0.4f);
            var t = Ui.Label(holder, text, Skin.Heading, size, color, 0, 0, 320, 36, TextAlignmentOptions.Center, 0.4f);
            shadow.textWrappingMode = t.textWrappingMode = TextWrappingModes.NoWrap;
            StartCoroutine(FloatUp(holder, t, shadow));
        }

        System.Collections.IEnumerator FloatUp(RectTransform rt, TextMeshProUGUI t, TextMeshProUGUI shadow)
        {
            var start = rt.anchoredPosition;
            const float life = 0.9f;
            for (float s = 0; s < life; s += Time.deltaTime)
            {
                if (rt == null) yield break;
                rt.anchoredPosition = start + new Vector2(0f, Mathf.Round(46f * (s / life)));
                float a = s < life * 0.6f ? 1f : 1f - (s - life * 0.6f) / (life * 0.4f);
                t.alpha = a;
                shadow.alpha = a * 0.9f;
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        // ---------------------------------------------------------------- overlays

        void HideOverlay()
        {
            Ui.Clear(overlay);
            overlay.gameObject.SetActive(false);
        }

        RectTransform OpenOverlay(float w, float h, float dimAlpha = 0.72f)
        {
            Ui.Clear(overlay);
            overlay.gameObject.SetActive(true);
            var dim = Ui.Fill(overlay, "Dim", 0, 0, Ui.RefW, Ui.RefH, new Color(0f, 0f, 0f, dimAlpha));
            dim.raycastTarget = true;
            var plate = Ui.Plate(overlay, "Dialog", (Ui.RefW - w) / 2f, (Ui.RefH - h) / 2f, w, h);
            return plate.rectTransform;
        }

        void ShowSkirmishMenu()
        {
            mode = Mode.Intro;
            EndTutorial();
            var d = OpenOverlay(880, 680, Battle == null ? 1f : 0.8f);
            Ui.Label(d, "The Silence", Skin.Logo, 64, Skin.Text, 0, 30, 880, 80, TextAlignmentOptions.Center);
            Ui.Stamp(d, "Combat skirmish - single fights, no run", 15, 0, 118, 880, 20, TextAlignmentOptions.Center, Skin.Brass);
            Ui.Body(d,
                "You command the Ascendancy lance-cruiser <b>Sixth Verdict</b> and her Blade escort against a Concord patrol.\n" +
                "Enemies show what they will do before you act. Your ships drift straight ahead by their Speed every turn; cards turn them, " +
                "speed them up and fire their weapons. Weapons only reach targets inside their arc and range.",
                19, 60, 156, 760, 120, TextAlignmentOptions.TopLeft, Skin.Dim);

            Ui.Btn(d, "1  Intercept at the Ring", 60, 290, 370, 70, () => { skirmishSeed++; previousSkirmish = null; StartSkirmish(1, false); }, 16);
            Ui.Body(d, "Two frigates and a gunship wing. Destroy both frigates.", 16, 64, 366, 370, 44, TextAlignmentOptions.TopLeft, Skin.Dim);

            bool canCarry = previousSkirmish != null && skirmishScenario == 1 && previousSkirmish.Outcome == BattleOutcome.Victory;
            Ui.Btn(d, canCarry ? "2  The Long Promise (carry over)" : "2  The Long Promise", 450, 290, 370, 70, () => StartSkirmish(2, canCarry), 16);
            Ui.Body(d, canCarry
                    ? "The carrier and a frigate. Your damage, junk, Ordinals and dart losses come with you; two reward cards are added."
                    : "The carrier and a frigate, with two reward cards added. Win scenario 1 first to carry your damage over.",
                16, 454, 366, 370, 66, TextAlignmentOptions.TopLeft, Skin.Dim);

            Ui.Btn(d, "3  Lance Patrol - the Concord", 60, 446, 370, 70, () => { skirmishSeed++; previousSkirmish = null; StartSkirmish(3, false); }, 16);
            Ui.Body(d, "Fly the trimaran Long Promise and her Outrider against an Ascendancy lance-cruiser, a Blade and darts. A mass driver, gunship wings, and crews that mend.",
                16, 454, 450, 370, 80, TextAlignmentOptions.TopLeft, Skin.Dim);

            Ui.Btn(d, "Tutorial", 60, 590, 370, 54, StartTutorial, 16);
            if (Application.CanStreamedLevelBeLoaded("Menu"))
                Ui.Btn(d, "Main menu", 600, 590, 220, 54, () => SceneManager.LoadScene("Menu"), 16);
        }

        void ShowEndScreen()
        {
            mode = Mode.Over;
            var d = OpenOverlay(760, 470, 0.66f);
            string title = tutorialBattle && Battle.Outcome == BattleOutcome.Victory ? "Tutorial complete" : Battle.Outcome == BattleOutcome.Victory ? "Victory" : Battle.Outcome == BattleOutcome.Defeat ? "Defeat" : "Withdrawal";
            var color = Battle.Outcome == BattleOutcome.Victory ? Skin.Brass : Battle.Outcome == BattleOutcome.Defeat ? Skin.Bad : Skin.Warn;
            Ui.Label(d, title, Skin.Logo, 64, color, 0, 36, 760, 80, TextAlignmentOptions.Center);
            string line = Battle.Outcome == BattleOutcome.Victory ? "Every enemy capital ship is destroyed."
                : Battle.Outcome == BattleOutcome.Defeat ? "Your last capital ship is gone. The fleet is lost."
                : $"After {Battle.Turn} turns neither fleet can finish the other. Both disengage.";
            var ran = Battle.Units.Find(u => u.Escaped);
            if (ran != null && Battle.Outcome != BattleOutcome.Defeat) line = $"{ran.Name} broke off with {ran.Hull} hull and jumped clear. " + (Battle.Outcome == BattleOutcome.Victory ? "The rest of the fleet is destroyed." : "Both fleets disengage.");
            Ui.Body(d, line, 22, 40, 150, 680, 34, TextAlignmentOptions.Center);

            var ships = Battle.Units.Where(u => u.Side == Side.Player && u.IsCapital).Select(u =>
                u.Alive ? $"{u.Name}  <color=#8cc47a>{u.Hull}/{u.MaxHull} hull</color>" : $"{u.Name}  <color=#d9573f>LOST</color>");
            int junk = Battle.Deck.AllPiles().SelectMany(p => p).Count(c => c.Def.IsJunk);
            string stats = string.Join("     ", ships) + $"\nTurns {Battle.Turn}     {Battle.CrewName} {Battle.Ordinals}     Junk cards {junk}";
            var s = Ui.Number(d, stats, 20, 40, 200, 680, 70, TextAlignmentOptions.Center, Skin.Dim);
            s.lineSpacing = 20f;

            if (BattleLauncher.Finished != null)
            {
                Ui.Btn(d, "Continue", 250, 330, 260, 74, LeaveBattle, 32);
                return;
            }
            bool won = Battle.Outcome == BattleOutcome.Victory;
            if (tutorialBattle)
            {
                Ui.Btn(d, won ? "Play a skirmish" : "Try again", 110, 330, 260, 74, () => { if (won) StartSkirmish(1, false); else StartTutorial(); }, 16);
                Ui.Btn(d, "Scenarios", 390, 330, 260, 74, LeaveBattle, 16);
                return;
            }
            if (won && skirmishScenario == 1)
                Ui.Btn(d, "On to scenario 2", 110, 330, 260, 74, () => { previousSkirmish = Battle; StartSkirmish(2, true); }, 16);
            else
                Ui.Btn(d, "Try again", 110, 330, 260, 74, () => StartSkirmish(skirmishScenario, false), 16);
            Ui.Btn(d, "Scenarios", 390, 330, 260, 74, LeaveBattle, 16);
        }
    }
}
