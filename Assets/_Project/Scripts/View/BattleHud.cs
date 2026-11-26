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
        // Layout in 1920x1080 reference pixels.
        const float TopH = 58f, LeftW = 330f, RightW = 350f, BottomY = 800f, PromptH = 34f;
        static readonly Vector2 BoardCentrePx = new Vector2((LeftW + (Ui.RefW - RightW)) / 2f, (TopH + (BottomY - PromptH)) / 2f);

        static Vector2 BoardCentre => new Vector2((BoardCentrePx.x - Ui.RefW / 2f) / 32f, (Ui.RefH / 2f - BoardCentrePx.y) / 32f);

        RectTransform root, chipRoot, floatRoot, handRoot, unitPanel, intentRoot, overlay;
        TextMeshProUGUI turnText, titleText, ordinalsText, dartsText, phaseText, promptText, logText, pilesText, commandText;
        readonly List<Image> pips = new List<Image>();
        Button endTurnButton, expendButton;
        readonly List<CardView> handViews = new List<CardView>();
        CardView hoveredCard;
        readonly List<string> logLines = new List<string>();

        void BuildHud()
        {
            var canvas = Ui.Canvas("Battle HUD");
            canvas.transform.SetParent(transform, false);
            root = Ui.Root(canvas);

            var catcher = Ui.Catcher(root, "BoardInput", LeftW, TopH, Ui.RefW - LeftW - RightW, BottomY - TopH);
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

            // ---- top bar
            var top = Ui.Plate(root, "TopBar", 0, 0, Ui.RefW, TopH);
            turnText = Ui.Heading(top.transform, "TURN 1", 34, 22, 6, 150, 46);
            phaseText = Ui.Stamp(top.transform, "COMMAND", 15, 170, 20, 220, 22, TextAlignmentOptions.Left, Skin.Brass);
            titleText = Ui.Heading(top.transform, "", 30, 560, 8, 800, 42, TextAlignmentOptions.Center, Skin.Dim);
            Ui.Stamp(top.transform, "Ordinals", 13, 1380, 8, 110, 18);
            ordinalsText = Ui.Number(top.transform, "24", 26, 1380, 22, 110, 32, TextAlignmentOptions.Left, Skin.Cyan);
            Ui.Stamp(top.transform, "Darts aboard", 13, 1500, 8, 140, 18);
            dartsText = Ui.Number(top.transform, "6", 26, 1500, 22, 110, 32, TextAlignmentOptions.Left, Skin.Text);
            Ui.Btn(top.transform, "Quit", 1790, 9, 116, 40, QuitBattle, 22);

            // ---- left: selected ship
            var left = Ui.Plate(root, "ShipPanel", 0, TopH, LeftW, BottomY - TopH);
            unitPanel = Ui.Box(left.transform, "Content", 0, 0, LeftW, BottomY - TopH);

            // ---- right: intents and log
            var right = Ui.Plate(root, "IntentPanel", Ui.RefW - RightW, TopH, RightW, BottomY - TopH);
            Ui.Heading(right.transform, "Enemy intents", 28, 18, 12, 300, 36);
            Ui.Stamp(right.transform, "Resolve in this order", 11, 20, 46, 300, 16);
            intentRoot = Ui.Box(right.transform, "Intents", 12, 68, RightW - 24, 430);
            Ui.Heading(right.transform, "Log", 24, 18, 506, 200, 30, TextAlignmentOptions.Left, Skin.Dim);
            Ui.Well(right.transform, "LogWell", 12, 538, RightW - 24, 192);
            logText = Ui.Body(right.transform, "", 14, 20, 544, RightW - 40, 182, TextAlignmentOptions.BottomLeft, Skin.Dim);
            logText.lineSpacing = -12f;
            logText.overflowMode = TextOverflowModes.Truncate;

            // ---- prompt line
            var prompt = Ui.Well(root, "Prompt", LeftW + 6, BottomY - PromptH, Ui.RefW - LeftW - RightW - 12, PromptH - 2);
            promptText = Ui.Body(prompt.transform, "", 19, 14, 3, Ui.RefW - LeftW - RightW - 240, PromptH - 6, TextAlignmentOptions.Left);
            promptText.textWrappingMode = TextWrappingModes.NoWrap;
            promptText.overflowMode = TextOverflowModes.Ellipsis;
            expendButton = Ui.Btn(prompt.transform, "Expend", Ui.RefW - LeftW - RightW - 12 - 330, 1, 328, PromptH - 4, ToggleExpend, 18);
            expendButton.gameObject.SetActive(false);

            // ---- bottom: command, piles, hand, end turn
            var bottom = Ui.Plate(root, "Bottom", 0, BottomY, Ui.RefW, Ui.RefH - BottomY);
            Ui.Stamp(bottom.transform, "Command", 14, 26, 16, 200, 20);
            for (int i = 0; i < 6; i++)
            {
                var pip = Ui.Img(bottom.transform, "Pip" + i, Skin.Pip, 24 + i * 34, 40, 30, 30, Skin.Brass);
                pips.Add(pip);
            }
            commandText = Ui.Number(bottom.transform, "3/3", 22, 24, 74, 200, 28, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Well(bottom.transform, "PilesWell", 18, 112, 214, 150);
            pilesText = Ui.Number(bottom.transform, "", 19, 32, 122, 190, 136, TextAlignmentOptions.TopLeft, Skin.Text);
            pilesText.lineSpacing = 12f;

            handRoot = Ui.Box(bottom.transform, "Hand", 250, 0, 1380, Ui.RefH - BottomY);

            endTurnButton = Ui.Btn(bottom.transform, "End turn", 1660, 96, 236, 84, OnEndTurn, 38);
            Ui.Stamp(bottom.transform, "Drift, then the enemy acts", 11, 1660, 186, 240, 16, TextAlignmentOptions.Center);

            overlay = Ui.Box(root, "Overlay", 0, 0, Ui.RefW, Ui.RefH);
            overlay.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- refresh

        void Refresh()
        {
            if (Battle == null) return;
            turnText.text = $"TURN {Battle.Turn}";
            titleText.text = (string.IsNullOrEmpty(Battle.Title) ? "" : Battle.Title.ToUpperInvariant()) +
                             (string.IsNullOrEmpty(BattleLauncher.Subtitle) ? "" : "  -  " + BattleLauncher.Subtitle.ToUpperInvariant());
            phaseText.text = mode == Mode.Busy ? Battle.Phase.ToString().ToUpperInvariant() : "COMMAND STEP";
            ordinalsText.text = Battle.Ordinals.ToString();
            ordinalsText.color = Battle.Ordinals <= 0 ? Skin.Bad : Skin.Cyan;
            dartsText.text = Battle.DartsInHangar < 0 ? "-" : Battle.DartsInHangar.ToString();

            for (int i = 0; i < pips.Count; i++)
            {
                bool shown = i < Mathf.Max(Battle.CommandPerTurn, Battle.Command);
                pips[i].enabled = shown;
                pips[i].sprite = i < Battle.Command ? Skin.Pip : Skin.PipOutline;
                pips[i].color = i < Battle.Command ? Skin.Brass : Skin.BrassDark;
            }
            commandText.text = $"{Battle.Command}/{Battle.CommandPerTurn}";
            pilesText.text = $"DRAW     {Battle.Deck.DrawPile.Count,2}\nDISCARD  {Battle.Deck.Discard.Count,2}\nEXHAUST  {Battle.Deck.Exhausted.Count,2}\nTURNS    {Battle.Turn,2}/{Battle.TurnLimit}";

            RebuildHand();
            RefreshIntents();
            board.RefreshOverlay();
            RefreshChips();
            RefreshUnitPanel();
            RefreshLog();
            RefreshPrompt();
            endTurnButton.interactable = mode != Mode.Busy && mode != Mode.Over && mode != Mode.Intro;
            if (mode == Mode.Idle) RefreshSelection();
        }

        void RefreshPrompt()
        {
            if (promptText == null || Battle == null) return;
            promptText.text = PromptText();
            bool showExpend = mode == Mode.Target && selectedCard != null && selectedCard.Def.ExpendOptional && selectedCard.Def.ExpendCost > 0;
            expendButton.gameObject.SetActive(showExpend);
            if (showExpend)
            {
                var def = selectedCard.Def;
                bool can = Battle.Ordinals >= def.ExpendCost;
                expendButton.interactable = can;
                Ui.SetButtonLabel(expendButton, (expend ? "[X] " : "[ ] ") + $"Expend {def.ExpendCost} Ordinals" + (can ? "" : " (too few)"));
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
                var view = CardView.Create(handRoot, c.Def, source != null ? source.Name : "", ArtFor(c.Def, source), Battle.CostOf(c));
                view.InstanceId = c.Id;
                var card = c;
                view.Clicked = _ => OnCardClicked(card);
                view.Hovered = v => { hoveredCard = v; v.transform.SetAsLastSibling(); };
                view.Unhovered = v => { if (hoveredCard == v) hoveredCard = null; };
                handViews.Add(view);
            }
            LayoutHand(true);
            RefreshHandState();
        }

        static string ArtFor(CardDef def, Unit source)
        {
            if (def.IsJunk) return def.Id == "fire_on_deck" ? "status_fire" : "status_warning";
            if (def.Type == CardType.Hangar) return "asc_dart_E";
            if (source == null) return "asc_cruiser_E128";
            return source.SpecId == "asc_cruiser" ? "asc_cruiser_E128" : source.SpecId + "_E";
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

        void LayoutHand(bool snap)
        {
            int n = handViews.Count;
            if (n == 0) return;
            float areaW = 1380f, step = Mathf.Min(CardView.W + 10f, (areaW - CardView.W) / Mathf.Max(1, n - 1));
            float total = step * (n - 1) + CardView.W, x0 = (areaW - total) / 2f + CardView.W / 2f;
            for (int i = 0; i < n; i++)
            {
                var v = handViews[i];
                if (v == null) continue;
                bool up = v == hoveredCard || (selectedCard != null && v.InstanceId == selectedCard.Id);
                var target = new Vector2(x0 + i * step, -(Ui.RefH - BottomY - 2f) + (up ? 26f : 0f));
                float scale = v == hoveredCard ? 1.12f : 1f;
                var rt = v.Rect;
                rt.anchoredPosition = snap ? target : Vector2.Lerp(rt.anchoredPosition, target, 0.3f);
                rt.localScale = Vector3.one * (snap ? scale : Mathf.Lerp(rt.localScale.x, scale, 0.3f));
            }
        }

        void AnimateHand() => LayoutHand(false);

        // ---------------------------------------------------------------- selected ship panel

        void RefreshUnitPanel()
        {
            if (unitPanel == null || Battle == null) return;
            Ui.Clear(unitPanel);
            var u = Battle.Get(hoverUnitId) ?? Battle.Get(panelUnitId);
            if (u == null || !u.Alive) u = Battle.Flagship ?? Battle.LiveUnits(Side.Player).FirstOrDefault(x => x.IsCapital);
            if (u == null) return;

            var t = unitPanel;
            bool enemy = u.Side == Side.Enemy;
            string role = enemy ? (u.Kind == UnitKind.Torpedo ? "Enemy torpedo" : u.IsCapital ? "Enemy ship" : "Enemy craft")
                : u.IsFlagship ? "Flagship" : u.IsCapital ? "Escort" : "Small craft";
            Ui.Stamp(t, role, 13, 20, 14, 290, 18, TextAlignmentOptions.Left, enemy ? Skin.Bad : Skin.Brass);
            var name = Ui.Heading(t, u.Name, 38, 18, 30, 296, 46);
            name.enableAutoSizing = true;
            name.fontSizeMax = 38;
            name.fontSizeMin = 22;

            var well = Ui.Well(t, "Portrait", 16, 82, 298, 150);
            well.gameObject.AddComponent<RectMask2D>();
            string spriteName = u.SpecId == "asc_cruiser" ? "asc_cruiser_E128" : u.SpecId == "con_trimaran" ? "con_trimaran_E128" : u.SpecId + "_E";
            var sprite = ArtLibrary.Instance.Sprite(spriteName) ?? ArtLibrary.Instance.Sprite("con_outrider_E");
            if (sprite != null)
            {
                float scale = sprite.rect.width <= 32 ? 3f : sprite.rect.width <= 64 ? 2f : 1f;
                float w = sprite.rect.width * scale, h = sprite.rect.height * scale;
                Ui.Img(well.transform, "Ship", sprite, (298 - w) / 2f, (150 - h) / 2f, w, h);
            }

            float y = 244;
            Ui.Stamp(t, "Hull", 13, 20, y, 100, 18);
            Ui.Number(t, $"{u.Hull}/{u.MaxHull}", 20, 200, y - 4, 112, 24, TextAlignmentOptions.Right);
            Ui.Well(t, "HullWell", 16, y + 22, 298, 22);
            float frac = u.MaxHull > 0 ? Mathf.Clamp01(u.Hull / (float)u.MaxHull) : 0f;
            Ui.Fill(t, "HullFill", 19, y + 25, 292 * frac, 16, frac > 0.34f ? Skin.Good : Skin.Bad);
            y += 56;

            if (!u.IsCapital)
            {
                Ui.Body(t, u.Kind == UnitKind.Torpedo
                        ? $"Closes {u.Move} hexes on its target each enemy step and hits for {u.AttackDamage}. One hit from a dart or flak destroys it."
                        : enemy ? $"Moves up to {u.Move} toward a capital ship's aft, then hits for {u.AttackDamage}."
                        : $"Moves up to {u.Move + u.CraftBonusMove}, then attacks an adjacent enemy for {u.AttackDamage}. Shoots down torpedoes." +
                          (u.CraftActed ? "\n\n<color=#8a90a0>Acted this turn.</color>" : ""),
                    17, 20, y, 290, 160);
                return;
            }

            // Shields and armor per arc: cyan cells for shields, a grey number for armor.
            Ui.Stamp(t, "Shields", 13, 20, y, 120, 18);
            Ui.Stamp(t, "Armor", 13, 250, y, 64, 18, TextAlignmentOptions.Right);
            y += 22;
            Ui.Well(t, "ArcWell", 16, y, 298, 112);
            string[] labels = { "Fore", "Starboard", "Aft", "Port" };
            int[] order = { ArcIndex.Fore, ArcIndex.Port, ArcIndex.Starboard, ArcIndex.Aft };
            for (int row = 0; row < 4; row++)
            {
                int arc = order[row];
                float ry = y + 8 + row * 25;
                Ui.Stamp(t, labels[arc], 12, 26, ry + 2, 96, 18, TextAlignmentOptions.Left, Skin.Text);
                int cells = Mathf.Max(u.MaxShields, u.Shields[arc]);
                for (int i = 0; i < Mathf.Min(cells, 10); i++)
                {
                    bool on = i < u.Shields[arc];
                    Ui.Fill(t, "Cell", 122 + i * 14, ry + 2, 11, 16, on ? (i >= u.MaxShields ? Color.white : Skin.Cyan) : Skin.Hex("1c2a33"));
                }
                Ui.Number(t, u.Armor[arc].ToString(), 19, 262, ry - 2, 44, 22, TextAlignmentOptions.Right, Skin.Dim);
            }
            y += 122;

            if (u.HeatCap > 0)
            {
                Ui.Stamp(t, "Heat", 13, 20, y, 100, 18);
                Ui.Number(t, $"{u.Heat}/{u.HeatCap}", 20, 200, y - 4, 112, 24, TextAlignmentOptions.Right, u.Heat > u.HeatCap ? Skin.Bad : Skin.Text);
                y += 22;
                int cells = u.HeatCap + Battle.ReactorStrainMargin;
                float cw = Mathf.Floor(296f / cells);
                for (int i = 0; i < cells; i++)
                {
                    bool over = i >= u.HeatCap;
                    bool on = i < u.Heat;
                    var c = on ? (over ? Skin.Bad : Skin.Warn) : (over ? Skin.Hex("3a1a15") : Skin.Hex("33302a"));
                    Ui.Fill(t, "Heat", 17 + i * cw, y, cw - 2, 18, c);
                }
                Ui.Fill(t, "Cap", 17 + u.HeatCap * cw - 2, y - 3, 2, 24, Skin.Text);
                Ui.Body(t, $"Vents {u.Vent} a turn. Over the cap adds Fire on Deck.", 13, 20, y + 21, 296, 18, TextAlignmentOptions.TopLeft, Skin.Faint);
                y += 44;
            }

            Ui.Stamp(t, "Speed", 13, 20, y, 100, 18);
            for (int i = 0; i < u.MaxSpeed; i++)
                Ui.Img(t, "Chevron", Skin.Arrow, 86 + i * 26, y - 3, 24, 24, i < u.Speed ? Skin.Cyan : Skin.Hex("1c2a33"));
            Ui.Stamp(t, "Heading " + u.Facing, 13, 190, y, 124, 18, TextAlignmentOptions.Right, Skin.Text);
            y += 30;
            Ui.Body(t, u.Speed == 0 ? "Holding position." : $"Drifts {u.Speed} hex{(u.Speed > 1 ? "es" : "")} straight ahead at end of turn.", 15, 20, y, 296, 22, TextAlignmentOptions.TopLeft, Skin.Dim);
            y += 28;

            if (enemy)
            {
                var intent = Battle.Intents.FirstOrDefault(i => i.UnitId == u.Id);
                if (intent != null)
                {
                    Ui.Stamp(t, "This turn", 13, 20, y, 200, 18, TextAlignmentOptions.Left, Skin.Bad);
                    Ui.Body(t, IntentText(intent, true), 16, 20, y + 20, 296, 64);
                    y += 84;
                }
                if (u.Cycle.Length > 0)
                {
                    Ui.Stamp(t, "Then", 13, 20, y, 200, 18);
                    var next = new List<string>();
                    for (int k = 1; k <= Mathf.Min(3, u.Cycle.Length); k++)
                    {
                        var step = u.Cycle[(u.CycleIndex + k) % u.Cycle.Length];
                        next.Add(StepName(step));
                    }
                    Ui.Body(t, string.Join("  >  ", next), 15, 20, y + 20, 296, 44, TextAlignmentOptions.TopLeft, Skin.Dim);
                }
            }
            else
            {
                var notes = new List<string>();
                if (u.LanceBonus > 0) notes.Add($"Next lance +{u.LanceBonus}");
                if (u.Guarding) notes.Add("Screening adjacent allies");
                if (u.NoBurnThisTurn) notes.Add("Engines hit: no Burn this turn");
                if (Battle.WaymindPrimed) notes.Add("Waymind: next weapon ignores armor, crits");
                if (notes.Count > 0) Ui.Body(t, string.Join("\n", notes), 15, 20, y, 296, 80, TextAlignmentOptions.TopLeft, Skin.Brass);

                // Standing orders: the rules a new player needs, always in view.
                float hy = BottomY - TopH - 172;
                Ui.Well(t, "HelpWell", 12, hy, LeftW - 24, 160);
                Ui.Stamp(t, "Standing orders", 12, 22, hy + 8, 280, 16, TextAlignmentOptions.Left, Skin.BrassDark);
                var help = Ui.Body(t,
                    "<color=#c9a24a>1</color> Enemies show their plan first. Solid red lines will hit.\n" +
                    "<color=#c9a24a>2</color> Play cards with command. Weapons need the target in their arc and range.\n" +
                    "<color=#c9a24a>3</color> Ships drift by Speed when you end the turn. Leave an arc and the shot misses.\n" +
                    "<color=#c9a24a>4</color> Shields soak a hit on the arc it lands on, then armor, then hull.",
                    13.5f, 22, hy + 28, LeftW - 46, 128, TextAlignmentOptions.TopLeft, Skin.Dim);
                help.lineSpacing = -14f;
            }
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

        void RefreshIntents()
        {
            Ui.Clear(intentRoot);
            var list = Battle.Intents.Where(i => Battle.Get(i.UnitId) != null && Battle.Get(i.UnitId).Alive).ToList();
            if (list.Count == 0)
            {
                Ui.Body(intentRoot, "No enemy actions this turn.", 16, 6, 6, 300, 24, TextAlignmentOptions.TopLeft, Skin.Dim);
                return;
            }
            float rowH = Mathf.Clamp(430f / list.Count, 34f, 62f);
            bool compact = rowH < 50f;
            for (int n = 0; n < list.Count && n < 12; n++)
            {
                var i = list[n];
                var u = Battle.Get(i.UnitId);
                float y = n * rowH;
                var row = Ui.Well(intentRoot, "Intent", 0, y, RightW - 24, rowH - 4);
                var color = i.Cancelled ? Skin.Faint : IntentColor(i);
                Ui.Fill(row.transform, "Edge", 0, 0, 5, rowH - 4, color);
                var icon = Ui.Art(row.transform, IntentIcon(i), 12, (rowH - 4 - 24) / 2f, 1f, i.Cancelled ? Skin.Faint : Color.white);
                string head = $"{u.Name.ToUpperInvariant()}  <color=#{ColorUtility.ToHtmlStringRGB(color)}>{IntentShort(i)}</color>";
                if (i.Cancelled) head += "  <color=#8a90a0>CANCELLED</color>";
                var h = Ui.Label(row.transform, head, Skin.Heading, compact ? 19 : 21, Skin.Text, 44, compact ? 2 : 4, RightW - 80, 26);
                h.textWrappingMode = TextWrappingModes.NoWrap;
                if (!compact)
                {
                    var t = Battle.Get(i.TargetId);
                    string detail = i.Kind == IntentKind.Broadside
                        ? $"at {(t != null ? t.Name : "-")} - " + (Battle.ForecastHit(i) ? "<color=#d9573f>on course to hit</color>" : "<color=#8a90a0>misses as things stand</color>")
                        : i.Kind == IntentKind.Maneuver ? "lines up its guns" : i.Kind == IntentKind.LaunchCraft ? "new gunships next turn"
                        : t != null ? "at " + t.Name : "";
                    var d = Ui.Body(row.transform, detail, 14, 44, 28, RightW - 80, 22, TextAlignmentOptions.TopLeft, Skin.Dim);
                    d.textWrappingMode = TextWrappingModes.NoWrap;
                }
                int unitId = i.UnitId;
                var relay = row.gameObject.AddComponent<PointerRelay>();
                row.raycastTarget = true;
                relay.Enter = () => { if (mode != Mode.Busy && board != null) { board.RefreshOverlay(unitId); board.SetRing(unitId, Skin.Bad); } };
                relay.Exit = () => { if (mode != Mode.Busy && board != null) { board.RefreshOverlay(); RefreshSelection(); } };
            }
        }

        /// <summary>A small label over each enemy saying what it will do, so intents read on the board itself.</summary>
        void RefreshChips()
        {
            Ui.Clear(chipRoot);
            if (Battle == null || Battle.Outcome != BattleOutcome.Ongoing) return;
            foreach (var i in Battle.Intents)
            {
                var u = Battle.Get(i.UnitId);
                if (u == null || !u.Alive) continue;
                if (!board.Views.TryGetValue(u.Id, out var view)) continue;
                var p = WorldToRef(view.Root.position);
                string text = i.Cancelled ? "CANCELLED" : IntentShort(i);
                bool small = !u.IsCapital;
                float w = small ? 22f + text.Length * 7.6f : 34f + text.Length * 9.4f, h = small ? 18f : 24f;
                float y = p.y - (u.IsCapital ? 64f : 34f);
                var chip = Ui.Img(chipRoot, "Chip", Skin.Pixel, p.x - w / 2f, y, w, h, new Color(0.03f, 0.03f, 0.05f, 0.88f));
                var color = i.Cancelled ? Skin.Faint : IntentColor(i);
                Ui.Fill(chip.transform, "Edge", 0, 0, 3, h, color);
                if (!small) Ui.Art(chip.transform, IntentIcon(i), 5, 0, 1f);
                var label = Ui.Label(chip.transform, text, Skin.Heading, small ? 14 : 18, color, small ? 7 : 31, small ? 0 : 1, w, h, TextAlignmentOptions.Left);
                label.textWrappingMode = TextWrappingModes.NoWrap;
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
            logText.text = string.Join("\n", logLines.Skip(Mathf.Max(0, logLines.Count - 11)));
        }

        // ---------------------------------------------------------------- floating text

        void Floater(Vector3 world, string text, Color color, float size = 26f)
        {
            var p = WorldToRef(world);
            var t = Ui.Label(floatRoot, text, Skin.Heading, size, color, p.x - 120, p.y - 30, 240, 34, TextAlignmentOptions.Center);
            t.fontMaterial.EnableKeyword("OUTLINE_ON");
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(0, 0, 0, 255);
            StartCoroutine(FloatUp(t));
        }

        System.Collections.IEnumerator FloatUp(TextMeshProUGUI t)
        {
            var rt = t.rectTransform;
            var start = rt.anchoredPosition;
            const float life = 0.9f;
            for (float s = 0; s < life; s += Time.deltaTime)
            {
                if (t == null) yield break;
                rt.anchoredPosition = start + new Vector2(0f, 46f * (s / life));
                t.alpha = s < life * 0.6f ? 1f : 1f - (s - life * 0.6f) / (life * 0.4f);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
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
            var d = OpenOverlay(880, 560, Battle == null ? 1f : 0.8f);
            Ui.Label(d, "The Silence", Skin.Logo, 76, Skin.Text, 0, 26, 880, 90, TextAlignmentOptions.Center);
            Ui.Stamp(d, "Combat skirmish - paper prototype scenarios", 15, 0, 118, 880, 20, TextAlignmentOptions.Center, Skin.Brass);
            Ui.Body(d,
                "You command the Ascendancy lance-cruiser <b>Sixth Verdict</b> and her Blade escort against a Concord patrol.\n" +
                "Enemies show what they will do before you act. Your ships drift straight ahead by their Speed every turn; cards turn them, " +
                "speed them up and fire their weapons. Weapons only reach targets inside their arc and range.",
                19, 60, 156, 760, 120, TextAlignmentOptions.TopLeft, Skin.Dim);

            Ui.Btn(d, "1  Intercept at the Ring", 60, 290, 370, 70, () => { skirmishSeed++; previousSkirmish = null; StartSkirmish(1, false); }, 30);
            Ui.Body(d, "Two frigates and a gunship wing. Destroy both frigates.", 16, 64, 366, 370, 44, TextAlignmentOptions.TopLeft, Skin.Dim);

            bool canCarry = previousSkirmish != null && skirmishScenario == 1 && previousSkirmish.Outcome == BattleOutcome.Victory;
            Ui.Btn(d, canCarry ? "2  The Long Promise (carry over)" : "2  The Long Promise", 450, 290, 370, 70, () => StartSkirmish(2, canCarry), 30);
            Ui.Body(d, canCarry
                    ? "The carrier and a frigate. Your damage, junk, Ordinals and dart losses come with you; two reward cards are added."
                    : "The carrier and a frigate, with two reward cards added. Win scenario 1 first to carry your damage over.",
                16, 454, 366, 370, 66, TextAlignmentOptions.TopLeft, Skin.Dim);

            if (Application.CanStreamedLevelBeLoaded("Menu"))
                Ui.Btn(d, "Main menu", 330, 470, 220, 54, () => SceneManager.LoadScene("Menu"), 24);
        }

        void ShowEndScreen()
        {
            mode = Mode.Over;
            var d = OpenOverlay(760, 470, 0.66f);
            string title = Battle.Outcome == BattleOutcome.Victory ? "Victory" : Battle.Outcome == BattleOutcome.Defeat ? "Defeat" : "Withdrawal";
            var color = Battle.Outcome == BattleOutcome.Victory ? Skin.Brass : Battle.Outcome == BattleOutcome.Defeat ? Skin.Bad : Skin.Warn;
            Ui.Label(d, title, Skin.Logo, 92, color, 0, 28, 760, 110, TextAlignmentOptions.Center);
            string line = Battle.Outcome == BattleOutcome.Victory ? "Every enemy capital ship is destroyed."
                : Battle.Outcome == BattleOutcome.Defeat ? "Your last capital ship is gone. The fleet is lost."
                : $"After {Battle.Turn} turns neither fleet can finish the other. Both disengage.";
            Ui.Body(d, line, 22, 40, 150, 680, 34, TextAlignmentOptions.Center);

            var ships = Battle.Units.Where(u => u.Side == Side.Player && u.IsCapital).Select(u =>
                u.Alive ? $"{u.Name}  <color=#8cc47a>{u.Hull}/{u.MaxHull} hull</color>" : $"{u.Name}  <color=#d9573f>LOST</color>");
            int junk = Battle.Deck.AllPiles().SelectMany(p => p).Count(c => c.Def.IsJunk);
            string stats = string.Join("     ", ships) + $"\nTurns {Battle.Turn}     Ordinals {Battle.Ordinals}     Junk cards {junk}";
            var s = Ui.Number(d, stats, 20, 40, 200, 680, 70, TextAlignmentOptions.Center, Skin.Dim);
            s.lineSpacing = 20f;

            if (BattleLauncher.Finished != null)
            {
                Ui.Btn(d, "Continue", 250, 330, 260, 74, LeaveBattle, 36);
                return;
            }
            bool won = Battle.Outcome == BattleOutcome.Victory;
            if (won && skirmishScenario == 1)
                Ui.Btn(d, "On to scenario 2", 110, 330, 260, 74, () => { previousSkirmish = Battle; StartSkirmish(2, true); }, 30);
            else
                Ui.Btn(d, "Try again", 110, 330, 260, 74, () => StartSkirmish(skirmishScenario, false), 30);
            Ui.Btn(d, "Scenarios", 390, 330, 260, 74, LeaveBattle, 30);
        }
    }
}
