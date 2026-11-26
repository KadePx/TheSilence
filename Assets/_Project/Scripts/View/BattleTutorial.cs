using System;
using System.Collections.Generic;
using System.Linq;
using Silence.Core.Battle;
using Silence.Core.Hex;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Silence.View
{
    /// <summary>
    /// The guided tutorial battle (Scenarios.Tutorial). A list of steps, each either something to read
    /// (Continue) or one thing to do. While a step is up, only the action it asks for is allowed, so the
    /// first two turns always play out the same way; the last step hands the battle over.
    /// </summary>
    public sealed partial class BattleScreen
    {
        enum Focus { None, Orders, Fleet, Flagship, Enemy, Card, Darts, EndTurn }

        sealed class TutorialStep
        {
            public string Title, Text;
            /// <summary>True: something to read, with a Continue button. False: wait for <see cref="Done"/>.</summary>
            public bool Info;
            public Func<bool> Done;
            /// <summary>Id of the one card that may be played in this step.</summary>
            public string Card;
            public bool Darts, EndTurn, Free;
            public Focus Focus;
        }

        List<TutorialStep> tutorial;
        int tutorialIndex;
        bool tutorialBattle;
        RectTransform tutorialRoot;

        bool InTutorial => tutorial != null && tutorialIndex < tutorial.Count;
        TutorialStep Step => InTutorial ? tutorial[tutorialIndex] : null;

        /// <summary>Start the tutorial battle (also used by the main menu's Tutorial button).</summary>
        public void StartTutorial()
        {
            tutorial = BuildTutorial();
            tutorialIndex = 0;
            tutorialBattle = true;
            skirmishScenario = 0;
            previousSkirmish = null;
            Begin(Scenarios.Tutorial());
        }

        void EndTutorial()
        {
            tutorial = null;
            tutorialIndex = 0;
            if (tutorialRoot != null) Ui.Clear(tutorialRoot);
        }

        bool PlayedThisTurn(string cardName)
            => Battle.Log.Any(e => e.Kind == EventKind.CardPlayed && e.Turn == Battle.Turn && e.Text.StartsWith("Play " + cardName));

        Unit Picket => Battle.Units.FirstOrDefault(u => u.Side == Side.Enemy && u.IsCapital);

        List<TutorialStep> BuildTutorial() => new List<TutorialStep>
        {
            new TutorialStep
            {
                Title = "Enemy orders", Info = true, Focus = Focus.Orders,
                Text = "Every turn starts with the enemy's orders, listed top right and drawn on the board.\n\n" +
                       "This picket will fire a <color=#d9573f>BROADSIDE</color> for 4. Its guns reach 3 hexes, out to its sides.",
            },
            new TutorialStep
            {
                Title = "Ships drift", Info = true, Focus = Focus.Flagship,
                Text = "You never move a ship by hand. The <color=#7fdcf2>Sixth Verdict</color> drifts straight ahead by her Speed when the turn ends. " +
                       "The faded copy marked <color=#7fdcf2>DRIFT</color> shows where she will be.\n\n" +
                       "She is 4 hexes from the picket now. Her drift carries her into its range.",
            },
            new TutorialStep
            {
                Title = "Fire the lance", Card = "lance_volley", Focus = Focus.Card,
                Done = () => PlayedThisTurn("Lance Volley"),
                Text = "Cards are your orders. Each costs <color=#c9a24a>command</color>: the brass hexes, bottom left, 3 a turn.\n\n" +
                       "Click <color=#c9a24a>LANCE VOLLEY</color>. The lit hexes are where it can reach: straight ahead, 4 hexes. Then click the picket.",
            },
            new TutorialStep
            {
                Title = "Brace", Card = "brace", Focus = Focus.Card,
                Done = () => PlayedThisTurn("Brace"),
                Text = "The broadside is still coming.\n\nPlay <color=#c9a24a>BRACE</color>: +4 shields on every arc this turn. " +
                       "A hit is soaked by shields first, then armor, and only what is left reaches the hull.",
            },
            new TutorialStep
            {
                Title = "End the turn", EndTurn = true, Focus = Focus.EndTurn,
                Done = () => Battle.Turn >= 2,
                Text = "No command left. Click <color=#c9a24a>END TURN</color>.\n\nYour ship drifts first. Then the enemy carries out its orders.",
            },
            new TutorialStep
            {
                Title = "Shields held", Info = true, Focus = Focus.Fleet,
                Text = "The broadside hit your fore arc and the shields took all of it.\n\n" +
                       "Each ship has four arcs: <color=#c9a24a>F</color>ore, <color=#c9a24a>P</color>ort, <color=#c9a24a>S</color>tarboard, <color=#c9a24a>A</color>ft. " +
                       "Each shows <color=#7fdcf2>shields</color> / <color=#8a90a0>armor</color>. A hit lands on the arc facing the attacker. Shields return a little each turn.",
            },
            new TutorialStep
            {
                Title = "Come about", Card = "hard_about", Focus = Focus.Card,
                Done = () => Battle.Flagship != null && Battle.Flagship.Facing != HexDirection.E,
                Text = "The picket is dead ahead, where your side guns cannot bear.\n\n" +
                       "Play <color=#c9a24a>HARD ABOUT</color>, then click an arrow to turn the ship 60 degrees. " +
                       "Turning is how you aim. It is also how you leave an enemy's arc so its shot misses.",
            },
            new TutorialStep
            {
                Title = "Broadside", Card = "broadside_battery", Focus = Focus.Card,
                Done = () => PlayedThisTurn("Broadside Battery"),
                Text = "Now the picket is on your flank.\n\nPlay <color=#c9a24a>BROADSIDE BATTERY</color> and click the picket. " +
                       "Before you click, the line under the board tells you exactly what the shot will do.",
            },
            new TutorialStep
            {
                Title = "Launch darts", Card = "launch_darts", Focus = Focus.Card,
                Done = () => Battle.PlayerCraftCount() > 0,
                Text = "One command left.\n\nClick <color=#c9a24a>LAUNCH DARTS</color>, then click it again to confirm (it needs no target). Two small craft appear beside the flagship.",
            },
            new TutorialStep
            {
                Title = "Move a dart", Darts = true, Focus = Focus.Darts,
                Done = () => Battle.LiveUnits(Side.Player).Any(u => u.Kind == UnitKind.Craft && u.CraftActed) || Battle.PlayerCraftCount() == 0,
                Text = "Darts are the only units you move by hand, and they cost no command.\n\n" +
                       "Click a dart marked <color=#7fdcf2>READY</color>, then click the picket. It flies in and attacks. Each dart acts once a turn.",
            },
            new TutorialStep
            {
                Title = "End the turn", Darts = true, EndTurn = true, Focus = Focus.EndTurn,
                Done = () => Battle.Turn >= 3,
                Text = "Send the other dart in if you like, then click <color=#c9a24a>END TURN</color>.",
            },
            new TutorialStep
            {
                Title = "Heat", Info = true, Focus = Focus.Fleet,
                Text = "Weapons heat the ship that fires them, and she vents 2 a turn.\n\n" +
                       "End a turn with heat over the cap (the white tick on the <color=#f0a040>HEAT</color> bar) and a junk card, Fire on Deck, joins your deck. " +
                       "Vent Plasma dumps heat in a hurry.",
            },
            new TutorialStep
            {
                Title = "Finish it", Free = true, Focus = Focus.Enemy,
                Done = () => Battle.Outcome != BattleOutcome.Ongoing,
                Text = "The battle is yours. Destroy the picket.\n\n" +
                       "Turn to bring a weapon to bear, and mind where your drift takes you. " +
                       "The bottom left always says what you can still do this turn. <color=#c9a24a>HELP</color> has the rules.",
            },
        };

        // ---------------------------------------------------------------- gates

        string TutorialBlocksCard(CardDef def)
        {
            var s = Step;
            if (s == null || s.Free) return null;
            if (s.Info) return "Read the note on the right, then click Continue.";
            if (s.Card == def.Id) return null;
            return s.Card != null ? $"Tutorial: play {PaperCards.Get(s.Card).Name} for now." : "Tutorial: " + NudgeFor(s);
        }

        string TutorialBlocksDart()
        {
            var s = Step;
            if (s == null || s.Free || s.Darts) return null;
            return s.Info ? "Read the note on the right, then click Continue." : "Tutorial: " + NudgeFor(s);
        }

        string TutorialBlocksEndTurn()
        {
            var s = Step;
            if (s == null || s.Free || s.EndTurn) return null;
            return s.Info ? "Read the note on the right, then click Continue." : "Tutorial: " + NudgeFor(s);
        }

        static string NudgeFor(TutorialStep s)
            => s.Card != null ? $"play {PaperCards.Get(s.Card).Name} first." : s.Darts ? "move a dart first." : s.EndTurn ? "end the turn." : "follow the note on the right.";

        // ---------------------------------------------------------------- display

        /// <summary>Move past finished steps, then redraw the note and point at what the step is about.</summary>
        void RefreshTutorial()
        {
            if (tutorialRoot == null) return;
            Ui.Clear(tutorialRoot);
            if (tutorial == null || Battle == null) return;
            if (Battle.Outcome != BattleOutcome.Ongoing) { EndTutorial(); return; }
            while (InTutorial && !Step.Info && Step.Done != null && Step.Done()) tutorialIndex++;
            var s = Step;
            if (s == null || mode == Mode.Busy) return;

            const float x = 1492f, w = 416f, y = 300f;
            var note = Ui.Panel(tutorialRoot, "Note", x, y, w, 200);
            Ui.Stamp(note.transform, $"Tutorial  {tutorialIndex + 1}/{tutorial.Count}", 12, 18, 14, 250, 14, TextAlignmentOptions.Left, Skin.BrassDark);
            Ui.Heading(note.transform, s.Title, 16, 18, 34, w - 36, 20, TextAlignmentOptions.Left, Skin.BrassLight);
            var body = Ui.Body(note.transform, s.Text, 16, 18, 64, w - 36, 400, TextAlignmentOptions.TopLeft, Skin.Text);
            body.ForceMeshUpdate();
            float h = 64 + Mathf.Ceil(body.preferredHeight) + (s.Info ? 70 : 20);
            note.rectTransform.sizeDelta = new Vector2(w, h);
            if (s.Info)
            {
                var go = Ui.Btn(note.transform, "Continue", w - 178, h - 58, 160, 44, () => { tutorialIndex++; Refresh(); }, 16);
                Pointer(go.transform, -4, -4, 168, 52);
            }

            switch (s.Focus)
            {
                case Focus.Orders: Pointer(tutorialRoot, OrdersX - 5, TopH + 7, OrdersW + 10, 92); break;
                case Focus.Fleet: Pointer(tutorialRoot, FleetX - 5, TopH + 7, FleetW + 10, 150); break;
                case Focus.EndTurn: Pointer(tutorialRoot, 1654, BottomY + 106, 244, 96); break;
                case Focus.Card:
                    var view = handViews.FirstOrDefault(v => v != null && v.Def.Id == s.Card);
                    if (view != null) Pointer(view.transform, -5, -5, CardView.W + 10, CardView.H + 10);
                    break;
                case Focus.Flagship:
                    if (Battle.Flagship != null) board.SetRing(Battle.Flagship.Id, Skin.BrassLight);
                    break;
                case Focus.Enemy:
                    if (Picket != null && mode == Mode.Idle) board.SetRing(Picket.Id, Skin.BrassLight);
                    break;
            }
        }

        /// <summary>A blinking brass frame that says "this is the thing".</summary>
        static void Pointer(Transform parent, float x, float y, float w, float h)
        {
            var img = Ui.Img(parent, "Pointer", Skin.Frame, x, y, w, h, Skin.BrassLight, true);
            img.gameObject.AddComponent<Blink>();
        }
    }
}
